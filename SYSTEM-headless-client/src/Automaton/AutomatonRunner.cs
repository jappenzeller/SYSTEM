using SYSTEM.HeadlessClient.Connection;
using SYSTEM.HeadlessClient.Inventory;
using SYSTEM.HeadlessClient.Mining;
using SYSTEM.HeadlessClient.Sensing;
using SYSTEM.HeadlessClient.World;

namespace SYSTEM.HeadlessClient.Automaton;

/// <summary>
/// Runner that manages the AutomatonAgent lifecycle and integrates with the main update loop.
/// Handles tick timing, logging, and provides API for external control.
/// </summary>
public class AutomatonRunner
{
    private readonly AutomatonAgent _agent;
    private readonly AutomatonConfig _config;

    private float _lastTickTime;
    private bool _enabled;

    // Statistics
    private int _totalStateTransitions;
    private DateTime _startTime;
    private Dictionary<AutomatonState, TimeSpan> _timeInStates = new();

    // Events for external monitoring
    public event EventHandler<StateTransitionEventArgs>? OnStateTransition;
    public event EventHandler<string>? OnStatusMessage;

    // Public access
    public AutomatonAgent Agent => _agent;
    public AutomatonConfig Config => _config;
    public bool IsEnabled => _enabled;
    public bool IsRunning => _agent.IsRunning;
    public AutomatonState CurrentState => _agent.CurrentState;

    public AutomatonRunner(
        SpacetimeConnection connection,
        WorldManager worldManager,
        SourceDetector sourceDetector,
        MiningController miningController,
        InventoryTracker inventoryTracker,
        AutomatonConfig? config = null)
    {
        _config = config ?? new AutomatonConfig();

        _agent = new AutomatonAgent(
            connection,
            worldManager,
            sourceDetector,
            miningController,
            inventoryTracker,
            _config);

        // Wire up events
        _agent.OnStateTransition += HandleStateTransition;
        _agent.OnStatusMessage += HandleStatusMessage;

        // Initialize time tracking
        foreach (AutomatonState state in Enum.GetValues<AutomatonState>())
        {
            _timeInStates[state] = TimeSpan.Zero;
        }

        _startTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Enable the automaton runner (will start on next Update if conditions met)
    /// </summary>
    public void Enable()
    {
        if (_enabled) return;

        _enabled = true;
        Console.WriteLine("[AutomatonRunner] Enabled");

        if (_config.AutomationEnabled)
        {
            _agent.Start();
        }
    }

    /// <summary>
    /// Disable the automaton runner (stops the agent)
    /// </summary>
    public void Disable()
    {
        if (!_enabled) return;

        _enabled = false;
        _agent.Stop();
        Console.WriteLine("[AutomatonRunner] Disabled");
    }

    /// <summary>
    /// Toggle enabled state
    /// </summary>
    public void Toggle()
    {
        if (_enabled)
            Disable();
        else
            Enable();
    }

    /// <summary>
    /// Update the automaton - call this each frame
    /// </summary>
    /// <param name="currentTime">Current time in seconds since start</param>
    public void Update(float currentTime)
    {
        if (!_enabled || !_config.AutomationEnabled)
        {
            return;
        }

        // Check tick interval
        float timeSinceLastTick = currentTime - _lastTickTime;
        if (timeSinceLastTick < _config.TickIntervalSeconds)
        {
            return;
        }

        _lastTickTime = currentTime;

        // Track time in current state
        var state = _agent.CurrentState;
        if (_timeInStates.ContainsKey(state))
        {
            _timeInStates[state] += TimeSpan.FromSeconds(_config.TickIntervalSeconds);
        }

        // Tick the FSM
        _agent.Tick();
    }

    /// <summary>
    /// Force start from idle state
    /// </summary>
    public void ForceStart()
    {
        _enabled = true;
        _agent.Start();
    }

    /// <summary>
    /// Force stop and return to idle
    /// </summary>
    public void ForceStop()
    {
        _agent.Stop();
    }

    private void HandleStateTransition(object? sender, StateTransitionEventArgs e)
    {
        _totalStateTransitions++;
        OnStateTransition?.Invoke(this, e);

        if (_config.VerboseLogging)
        {
            Console.WriteLine($"[AutomatonRunner] Transition #{_totalStateTransitions}: {e.FromState} → {e.ToState}");
        }
    }

    private void HandleStatusMessage(object? sender, string message)
    {
        OnStatusMessage?.Invoke(this, message);
    }

    #region Status & Statistics

    /// <summary>
    /// Get comprehensive status for API/display
    /// </summary>
    public AutomatonStatus GetStatus()
    {
        return new AutomatonStatus
        {
            Enabled = _enabled,
            Running = _agent.IsRunning,
            CurrentState = _agent.CurrentState.ToString(),
            StateDescription = _agent.GetStatusString(),
            TimeInCurrentState = _agent.Context.TimeInState,
            TotalTransitions = _totalStateTransitions,
            ConsecutiveFailures = _agent.Context.ConsecutiveFailures,
            Uptime = (DateTime.UtcNow - _startTime).TotalSeconds,
            TargetSourceId = _agent.Context.TargetSourceId,
            TargetStorageId = _agent.Context.TargetStorageId,
            LastTransitionReason = _agent.Context.LastTransitionReason?.ToString()
        };
    }

    /// <summary>
    /// Get time spent in each state
    /// </summary>
    public Dictionary<string, double> GetStateTimings()
    {
        var result = new Dictionary<string, double>();
        foreach (var kvp in _timeInStates)
        {
            result[kvp.Key.ToString()] = kvp.Value.TotalSeconds;
        }
        return result;
    }

    /// <summary>
    /// Get simple status string
    /// </summary>
    public string GetStatusString()
    {
        if (!_enabled)
        {
            return "Automaton disabled";
        }

        return _agent.GetStatusString();
    }

    #endregion
}

/// <summary>
/// Status data structure for API responses
/// </summary>
public class AutomatonStatus
{
    public bool Enabled { get; set; }
    public bool Running { get; set; }
    public string CurrentState { get; set; } = "";
    public string StateDescription { get; set; } = "";
    public float TimeInCurrentState { get; set; }
    public int TotalTransitions { get; set; }
    public int ConsecutiveFailures { get; set; }
    public double Uptime { get; set; }
    public ulong? TargetSourceId { get; set; }
    public ulong? TargetStorageId { get; set; }
    public string? LastTransitionReason { get; set; }
}
