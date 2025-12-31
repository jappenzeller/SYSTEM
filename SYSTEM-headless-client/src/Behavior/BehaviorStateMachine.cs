using SYSTEM.HeadlessClient.Inventory;
using SYSTEM.HeadlessClient.Mining;
using SYSTEM.HeadlessClient.Sensing;
using SYSTEM.HeadlessClient.World;

namespace SYSTEM.HeadlessClient.Behavior;

/// <summary>
/// Agent behavior states
/// </summary>
public enum AgentState
{
    Idle,           // Waiting, no sources nearby
    Mining,         // Actively mining a source
    InventoryFull,  // Inventory at capacity, need to offload
    Wandering       // Moving to explore/find sources
}

/// <summary>
/// Configuration for agent behavior
/// </summary>
public class BehaviorConfig
{
    public bool AutoMine { get; set; } = true;
    public bool WanderWhenIdle { get; set; } = true;
    public float WanderInterval { get; set; } = 60f;    // seconds between wanders
    public float WanderDistance { get; set; } = 20f;    // units to walk
    public float IdleWanderChance { get; set; } = 0.1f; // 10% chance per check
}

/// <summary>
/// State machine for autonomous agent behavior.
/// Manages transitions between Idle, Mining, InventoryFull, and Wandering states.
/// </summary>
public class BehaviorStateMachine
{
    private readonly InventoryTracker _inventory;
    private readonly MiningController _mining;
    private readonly SourceDetector _sources;
    private readonly WorldManager _world;
    private readonly BehaviorConfig _config;

    public AgentState CurrentState { get; private set; } = AgentState.Idle;
    public AgentState PreviousState { get; private set; } = AgentState.Idle;

    private float _stateTimer;
    private float _wanderCooldown;
    private Random _random = new();

    public event Action<AgentState, AgentState>? OnStateChanged; // (from, to)

    public BehaviorStateMachine(
        InventoryTracker inventory,
        MiningController mining,
        SourceDetector sources,
        WorldManager world,
        BehaviorConfig? config = null)
    {
        _inventory = inventory;
        _mining = mining;
        _sources = sources;
        _world = world;
        _config = config ?? new BehaviorConfig();

        // Subscribe to events
        _inventory.OnInventoryFull += HandleInventoryFull;
        _mining.OnMiningStarted += HandleMiningStarted;
        _mining.OnMiningStopped += HandleMiningStopped;
        _sources.OnSourceEnterRange += HandleSourceEnterRange;
    }

    public void Update(float deltaTime)
    {
        _stateTimer += deltaTime;
        _wanderCooldown -= deltaTime;

        switch (CurrentState)
        {
            case AgentState.Idle:
                UpdateIdle(deltaTime);
                break;

            case AgentState.Mining:
                UpdateMining(deltaTime);
                break;

            case AgentState.InventoryFull:
                UpdateInventoryFull(deltaTime);
                break;

            case AgentState.Wandering:
                UpdateWandering(deltaTime);
                break;
        }
    }

    private void UpdateIdle(float deltaTime)
    {
        // Check if we should start mining
        if (_config.AutoMine && !_inventory.IsFull)
        {
            var source = _sources.GetClosestSource();
            if (source != null)
            {
                _mining.StartMiningWithDefaultCrystal(source.SourceId);
                return; // Mining started event will change state
            }
        }

        // Check if we should wander
        if (_config.WanderWhenIdle && _wanderCooldown <= 0)
        {
            if (_random.NextDouble() < _config.IdleWanderChance)
            {
                StartWandering();
            }
            _wanderCooldown = _config.WanderInterval;
        }
    }

    private void UpdateMining(float deltaTime)
    {
        // Mining is handled by MiningController
        // State changes come from events (inventory full, mining stopped)
    }

    private void UpdateInventoryFull(float deltaTime)
    {
        // In the future: navigate to storage, transfer packets
        // For now: just wait and eventually wander
        if (_stateTimer > 10f) // Wait 10 seconds then wander
        {
            StartWandering();
        }
    }

    private void UpdateWandering(float deltaTime)
    {
        // Check if walking is complete
        if (!_world.IsWalking)
        {
            TransitionTo(AgentState.Idle);
        }
    }

    private void StartWandering()
    {
        // Pick a random direction and walk
        float angle = (float)(_random.NextDouble() * Math.PI * 2);
        float forward = MathF.Cos(angle);
        float right = MathF.Sin(angle);

        Console.WriteLine($"[Behavior] Starting wander: forward={forward:F2}, right={right:F2}, distance={_config.WanderDistance}");
        _world.StartWalkingForDistance(forward, right, _config.WanderDistance);

        TransitionTo(AgentState.Wandering);
    }

    private void HandleInventoryFull()
    {
        Console.WriteLine("[Behavior] Inventory full - stopping mining");
        if (_mining.IsMining)
        {
            _mining.StopMining();
        }
        TransitionTo(AgentState.InventoryFull);
    }

    private void HandleMiningStarted(ulong sourceId)
    {
        TransitionTo(AgentState.Mining);
    }

    private void HandleMiningStopped(ulong sourceId, uint totalExtracted)
    {
        // Don't transition if inventory is full
        if (!_inventory.IsFull)
        {
            TransitionTo(AgentState.Idle);
        }
    }

    private void HandleSourceEnterRange(SpacetimeDB.Types.WavePacketSource source)
    {
        // If idle and not full, start mining
        if (CurrentState == AgentState.Idle && !_inventory.IsFull && _config.AutoMine)
        {
            Console.WriteLine($"[Behavior] Source {source.SourceId} entered range, starting mining");
            _mining.StartMiningWithDefaultCrystal(source.SourceId);
        }
    }

    private void TransitionTo(AgentState newState)
    {
        if (newState == CurrentState) return;

        PreviousState = CurrentState;
        CurrentState = newState;
        _stateTimer = 0;

        Console.WriteLine($"[Behavior] State: {PreviousState} → {CurrentState}");
        OnStateChanged?.Invoke(PreviousState, CurrentState);
    }

    public string GetStatusString()
    {
        return CurrentState switch
        {
            AgentState.Idle => "Idle (waiting for sources)",
            AgentState.Mining => $"Mining ({_inventory.TotalCount}/{InventoryTracker.MAX_CAPACITY} packets)",
            AgentState.InventoryFull => $"Inventory Full ({_inventory.TotalCount} packets)",
            AgentState.Wandering => "Wandering (exploring)",
            _ => CurrentState.ToString()
        };
    }
}
