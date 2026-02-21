using SpacetimeDB.Types;
using SYSTEM.HeadlessClient.Connection;
using SYSTEM.HeadlessClient.Inventory;
using SYSTEM.HeadlessClient.Mining;
using SYSTEM.HeadlessClient.Sensing;
using SYSTEM.HeadlessClient.World;

namespace SYSTEM.HeadlessClient.Automaton;

/// <summary>
/// Finite State Machine for autonomous agent behavior.
/// Orchestrates the full gameplay loop: mine → collect → transfer → assess world.
/// </summary>
public class AutomatonAgent
{
    private readonly SpacetimeConnection _connection;
    private readonly WorldManager _worldManager;
    private readonly SourceDetector _sourceDetector;
    private readonly MiningController _miningController;
    private readonly InventoryTracker _inventoryTracker;
    private readonly AutomatonConfig _config;
    private readonly AutomatonContext _context;

    private AutomatonState _currentState = AutomatonState.Idle;
    private readonly Random _random = new();

    // Events
    public event EventHandler<StateTransitionEventArgs>? OnStateTransition;
    public event EventHandler<string>? OnStatusMessage;

    // Public state access
    public AutomatonState CurrentState => _currentState;
    public AutomatonContext Context => _context;
    public bool IsRunning => _currentState != AutomatonState.Idle;

    public AutomatonAgent(
        SpacetimeConnection connection,
        WorldManager worldManager,
        SourceDetector sourceDetector,
        MiningController miningController,
        InventoryTracker inventoryTracker,
        AutomatonConfig? config = null)
    {
        _connection = connection;
        _worldManager = worldManager;
        _sourceDetector = sourceDetector;
        _miningController = miningController;
        _inventoryTracker = inventoryTracker;
        _config = config ?? new AutomatonConfig();
        _context = new AutomatonContext();
    }

    /// <summary>
    /// Start the automaton from Idle state
    /// </summary>
    public void Start()
    {
        if (_currentState != AutomatonState.Idle)
        {
            Log("Already running, ignoring Start()");
            return;
        }

        Log("Starting automaton FSM");
        _context.ConsecutiveFailures = 0;
        TransitionTo(AutomatonState.FindOrb, TransitionReason.ManualOverride);
    }

    /// <summary>
    /// Stop the automaton and return to Idle
    /// </summary>
    public void Stop()
    {
        if (_currentState == AutomatonState.Idle)
        {
            return;
        }

        Log("Stopping automaton FSM");

        // Clean up any active operations
        if (_miningController.IsMining)
        {
            _miningController.StopMining();
        }

        _worldManager.StopWalking();
        _context.ClearTargets();
        TransitionTo(AutomatonState.Idle, TransitionReason.ManualOverride);
    }

    /// <summary>
    /// Main tick function - call this each frame or at regular intervals
    /// </summary>
    public void Tick()
    {
        if (!_config.AutomationEnabled || _currentState == AutomatonState.Idle)
        {
            return;
        }

        // Check for state timeout
        if (_context.TimeInState > _config.StateTimeoutSeconds)
        {
            Log($"State timeout after {_context.TimeInState:F1}s in {_currentState}");
            HandleStateTimeout();
            return;
        }

        // Execute current state logic
        switch (_currentState)
        {
            case AutomatonState.FindOrb:
                TickFindOrb();
                break;
            case AutomatonState.StartMining:
                TickStartMining();
                break;
            case AutomatonState.CollectPackets:
                TickCollectPackets();
                break;
            case AutomatonState.FindStorage:
                TickFindStorage();
                break;
            case AutomatonState.Transfer:
                TickTransfer();
                break;
            case AutomatonState.CompleteTransfer:
                TickCompleteTransfer();
                break;
            case AutomatonState.AssessWorld:
                TickAssessWorld();
                break;
        }
    }

    #region State Handlers

    private void TickFindOrb()
    {
        // Check if we already have a source in range
        var sources = _sourceDetector.SourcesInRange;
        if (sources.Count > 0)
        {
            // Find the richest source
            var target = _sourceDetector.GetRichestSource();
            if (target != null)
            {
                _context.TargetSourceId = target.SourceId;
                Log($"Found source {target.SourceId} with {target.TotalWavePackets} packets");
                TransitionTo(AutomatonState.StartMining, TransitionReason.SourceFound);
                return;
            }
        }

        // No sources in range - explore
        if (!_worldManager.IsWalking)
        {
            if (_context.TimeInState > _config.FindOrbTimeoutSeconds)
            {
                // Been searching too long, just keep wandering
                Log("Extended search for sources...");
            }

            // Pick random direction and walk
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            float forward = MathF.Cos(angle);
            float right = MathF.Sin(angle);

            Log($"Exploring: direction ({forward:F2}, {right:F2})");
            _worldManager.StartWalkingForDistance(forward, right, _config.ExploreWanderDistance);
        }
    }

    private void TickStartMining()
    {
        if (!_context.TargetSourceId.HasValue)
        {
            Log("No target source ID, returning to FindOrb");
            TransitionTo(AutomatonState.FindOrb, TransitionReason.NoSourcesAvailable);
            return;
        }

        // Check if mining already started
        if (_miningController.IsMining)
        {
            if (_miningController.CurrentSourceId == _context.TargetSourceId)
            {
                _context.InventoryCountAtStateEntry = _inventoryTracker.TotalCount;
                TransitionTo(AutomatonState.CollectPackets, TransitionReason.MiningSessionStarted);
                return;
            }
        }

        // Initiate mining if not already pending
        if (_context.TimeInState < 0.5f)
        {
            // Just entered state, start mining
            Log($"Starting mining on source {_context.TargetSourceId}");
            _miningController.StartMiningWithDefaultCrystal(_context.TargetSourceId.Value);
        }
        else if (_context.TimeInState > 5f)
        {
            // Mining didn't start, maybe source is gone
            Log("Mining start timeout, returning to FindOrb");
            _context.ConsecutiveFailures++;
            TransitionTo(AutomatonState.FindOrb, TransitionReason.MiningStartFailed);
        }
    }

    private void TickCollectPackets()
    {
        // Check inventory level
        float inventoryPercent = (float)_inventoryTracker.TotalCount / InventoryTracker.MAX_CAPACITY;

        if (inventoryPercent >= _config.InventoryFullThreshold)
        {
            Log($"Inventory at {inventoryPercent:P0}, transitioning to FindStorage");
            _miningController.StopMining();
            TransitionTo(AutomatonState.FindStorage, TransitionReason.InventoryFull);
            return;
        }

        // Check if mining is still active
        if (!_miningController.IsMining)
        {
            // Mining stopped - check if source depleted or we moved out of range
            int packetsCollected = _inventoryTracker.TotalCount - _context.InventoryCountAtStateEntry;
            Log($"Mining stopped, collected {packetsCollected} packets this session");

            // Wait for in-flight packets
            if (_miningController.PacketsInFlight > 0)
            {
                // Still have packets in flight, wait
                return;
            }

            // All packets collected, check if inventory has enough to transfer
            if (_inventoryTracker.TotalCount > 0)
            {
                TransitionTo(AutomatonState.FindStorage, TransitionReason.PacketsCollected);
            }
            else
            {
                TransitionTo(AutomatonState.FindOrb, TransitionReason.SourceDepleted);
            }
            return;
        }

        // Still mining - check for packets in flight limit
        if (_miningController.PacketsInFlight >= _config.MaxPacketsInFlight)
        {
            // Too many packets in flight, mining controller will handle throttling
        }
    }

    private void TickFindStorage()
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        // Find nearest storage device
        StorageDevice? nearestStorage = null;
        float nearestDistance = float.MaxValue;

        foreach (var storage in conn.Db.StorageDevice.Iter())
        {
            float distance = WorldManager.Distance(_worldManager.Position, storage.Position);

            if (distance <= _config.StorageSearchRange && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestStorage = storage;
            }
        }

        if (nearestStorage == null)
        {
            // No storage device found
            if (_context.TimeInState > 10f)
            {
                Log("No storage device found, creating one at current position");
                // TODO: Call CreateStorageDevice reducer
                // For now, just go back to mining
                TransitionTo(AutomatonState.FindOrb, TransitionReason.NoStorageAvailable);
            }
            return;
        }

        _context.TargetStorageId = nearestStorage.DeviceId;

        // Check if we're close enough to interact
        if (nearestDistance <= _config.StorageInteractionRange)
        {
            Log($"Reached storage {nearestStorage.DeviceId}");
            _worldManager.StopWalking();
            TransitionTo(AutomatonState.Transfer, TransitionReason.StorageReached);
            return;
        }

        // Walk toward storage
        if (!_worldManager.IsWalking)
        {
            var (forward, right) = _worldManager.GetDirectionTo(nearestStorage.Position);

            // Normalize direction
            float mag = MathF.Sqrt(forward * forward + right * right);
            if (mag > 0.001f)
            {
                forward /= mag;
                right /= mag;
            }

            Log($"Walking toward storage {nearestStorage.DeviceId} ({nearestDistance:F1} units away)");
            _worldManager.StartWalkingForDistance(forward, right, nearestDistance);
        }
    }

    private void TickTransfer()
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        if (!_context.TargetStorageId.HasValue)
        {
            Log("No target storage, returning to FindStorage");
            TransitionTo(AutomatonState.FindStorage, TransitionReason.TransferFailed);
            return;
        }

        // Initiate transfer if just entered state
        if (_context.TimeInState < 0.5f)
        {
            Log($"Initiating transfer to storage {_context.TargetStorageId}");

            // Transfer all inventory to storage
            var composition = _inventoryTracker.Composition.ToList();
            if (composition.Count == 0)
            {
                Log("No inventory to transfer");
                TransitionTo(AutomatonState.AssessWorld, TransitionReason.TransferComplete);
                return;
            }

            // Call InitiateTransfer reducer
            // Parameters: composition, destinationDeviceId
            conn.Reducers.InitiateTransfer(
                composition,
                _context.TargetStorageId.Value
            );

            TransitionTo(AutomatonState.CompleteTransfer, TransitionReason.TransferInitiated);
        }
    }

    private void TickCompleteTransfer()
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        // Check if inventory is empty (transfer complete)
        if (_inventoryTracker.TotalCount == 0)
        {
            Log("Transfer complete, inventory empty");
            _context.ConsecutiveFailures = 0;
            TransitionTo(AutomatonState.AssessWorld, TransitionReason.TransferComplete);
            return;
        }

        // Check for active packet transfers
        bool hasActiveTransfer = false;
        foreach (var transfer in conn.Db.PacketTransfer.Iter())
        {
            // Check if this transfer is from our inventory (not completed)
            if (transfer.SourceObjectType == "inventory" && !transfer.Completed)
            {
                hasActiveTransfer = true;
                break;
            }
        }

        if (!hasActiveTransfer && _context.TimeInState > 2f)
        {
            // No active transfers and inventory not empty - transfer may have failed
            Log($"Transfer may have failed, inventory still has {_inventoryTracker.TotalCount} packets");

            if (_context.TimeInState > _config.TransferTimeoutSeconds)
            {
                _context.ConsecutiveFailures++;
                TransitionTo(AutomatonState.AssessWorld, TransitionReason.TransferTimeout);
            }
        }
    }

    private void TickAssessWorld()
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        // Check quantum tunnels for activation opportunities
        foreach (var tunnel in conn.Db.QuantumTunnel.Iter())
        {
            if (tunnel.RingCharge >= _config.TunnelActivationThreshold &&
                tunnel.TunnelStatus == "Charging")
            {
                Log($"Tunnel {tunnel.TunnelId} ready for activation (charge: {tunnel.RingCharge:F0}%)");

                // TODO: Call activate_tunnel reducer when bindings are regenerated
                // conn.Reducers.ActivateTunnel(tunnel.TunnelId);
            }
        }

        // Assessment complete, return to mining loop
        if (_context.TimeInState > 1f)
        {
            TransitionTo(AutomatonState.FindOrb, TransitionReason.AssessmentComplete);
        }
    }

    #endregion

    #region State Management

    private void TransitionTo(AutomatonState newState, TransitionReason reason)
    {
        if (newState == _currentState)
        {
            return;
        }

        var oldState = _currentState;
        _currentState = newState;
        _context.LastTransitionReason = reason;
        _context.ResetForNewState();

        if (_config.VerboseLogging)
        {
            Log($"State: {oldState} → {newState} ({reason})");
        }

        OnStateTransition?.Invoke(this, new StateTransitionEventArgs(oldState, newState, reason));
    }

    private void HandleStateTimeout()
    {
        _context.ConsecutiveFailures++;

        switch (_currentState)
        {
            case AutomatonState.FindOrb:
            case AutomatonState.StartMining:
            case AutomatonState.CollectPackets:
                // Mining-related timeouts - try again
                TransitionTo(AutomatonState.FindOrb, TransitionReason.Timeout);
                break;

            case AutomatonState.FindStorage:
            case AutomatonState.Transfer:
            case AutomatonState.CompleteTransfer:
                // Transfer-related timeouts - go to assessment
                TransitionTo(AutomatonState.AssessWorld, TransitionReason.Timeout);
                break;

            case AutomatonState.AssessWorld:
                // Assessment timeout - restart loop
                TransitionTo(AutomatonState.FindOrb, TransitionReason.Timeout);
                break;
        }
    }

    private void Log(string message)
    {
        string prefix = $"[Automaton:{_currentState}]";
        Console.WriteLine($"{prefix} {message}");
        OnStatusMessage?.Invoke(this, message);
    }

    #endregion

    #region Status

    /// <summary>
    /// Get current automaton status for display/API
    /// </summary>
    public string GetStatusString()
    {
        if (_currentState == AutomatonState.Idle)
        {
            return "Idle (not running)";
        }

        string stateDesc = _currentState switch
        {
            AutomatonState.FindOrb => "Searching for wave packet sources",
            AutomatonState.StartMining => $"Starting mining on source {_context.TargetSourceId}",
            AutomatonState.CollectPackets => $"Mining ({_inventoryTracker.TotalCount}/{InventoryTracker.MAX_CAPACITY} packets)",
            AutomatonState.FindStorage => $"Looking for storage device",
            AutomatonState.Transfer => $"Transferring to storage {_context.TargetStorageId}",
            AutomatonState.CompleteTransfer => "Waiting for transfer to complete",
            AutomatonState.AssessWorld => "Assessing world state",
            _ => _currentState.ToString()
        };

        return $"{stateDesc} ({_context.TimeInState:F0}s)";
    }

    #endregion
}
