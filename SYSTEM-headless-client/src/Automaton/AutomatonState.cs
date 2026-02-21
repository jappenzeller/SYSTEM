namespace SYSTEM.HeadlessClient.Automaton;

/// <summary>
/// States for the Automaton finite state machine.
/// Flow: FIND_ORB → START_MINING → COLLECT_PACKETS → FIND_STORAGE → TRANSFER → COMPLETE_TRANSFER → ASSESS_WORLD → (loop)
/// </summary>
public enum AutomatonState
{
    /// <summary>
    /// Initial state - FSM not running or paused
    /// </summary>
    Idle,

    /// <summary>
    /// Searching for a wave packet source (orb) to mine.
    /// Explores/wanders if none found within range.
    /// Transitions to START_MINING when source found.
    /// </summary>
    FindOrb,

    /// <summary>
    /// Initiating mining on a discovered source.
    /// Calls StartMiningV2 reducer with default crystal.
    /// Transitions to COLLECT_PACKETS when mining session confirmed.
    /// </summary>
    StartMining,

    /// <summary>
    /// Actively mining and collecting packets into inventory.
    /// Monitors inventory level and packets in flight.
    /// Transitions to FIND_STORAGE when inventory nearly full.
    /// May transition back to FIND_ORB if source depleted.
    /// </summary>
    CollectPackets,

    /// <summary>
    /// Searching for a storage device to deposit packets.
    /// Walks toward nearest storage device.
    /// Transitions to TRANSFER when within interaction range.
    /// </summary>
    FindStorage,

    /// <summary>
    /// Initiating transfer from inventory to storage.
    /// Calls InitiateTransfer reducer.
    /// Transitions to COMPLETE_TRANSFER immediately.
    /// </summary>
    Transfer,

    /// <summary>
    /// Waiting for transfer to complete.
    /// Monitors PacketTransfer table for completion.
    /// Transitions to ASSESS_WORLD when transfer done.
    /// </summary>
    CompleteTransfer,

    /// <summary>
    /// Assessing world state for tunnel activation or world travel.
    /// Checks tunnel charges, considers world crystallization.
    /// Transitions back to FIND_ORB to continue the loop.
    /// </summary>
    AssessWorld
}

/// <summary>
/// Transition reasons for logging and debugging.
/// </summary>
public enum TransitionReason
{
    // FindOrb transitions
    SourceFound,
    ExploreTimeout,
    NoSourcesAvailable,

    // StartMining transitions
    MiningSessionStarted,
    MiningStartFailed,

    // CollectPackets transitions
    InventoryFull,
    SourceDepleted,
    MiningInterrupted,
    PacketsCollected,

    // FindStorage transitions
    StorageFound,
    StorageReached,
    NoStorageAvailable,

    // Transfer transitions
    TransferInitiated,
    TransferFailed,

    // CompleteTransfer transitions
    TransferComplete,
    TransferTimeout,

    // AssessWorld transitions
    TunnelActivated,
    WorldChanged,
    AssessmentComplete,

    // General
    Timeout,
    Error,
    ManualOverride,
    AutomationDisabled
}

/// <summary>
/// Context data passed between states.
/// </summary>
public class AutomatonContext
{
    /// <summary>
    /// Current target source ID (for mining states)
    /// </summary>
    public ulong? TargetSourceId { get; set; }

    /// <summary>
    /// Current target storage device ID (for transfer states)
    /// </summary>
    public ulong? TargetStorageId { get; set; }

    /// <summary>
    /// Current transfer ID being tracked
    /// </summary>
    public ulong? ActiveTransferId { get; set; }

    /// <summary>
    /// Time when current state was entered
    /// </summary>
    public DateTime StateEnteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Time spent in current state (seconds)
    /// </summary>
    public float TimeInState => (float)(DateTime.UtcNow - StateEnteredAt).TotalSeconds;

    /// <summary>
    /// Number of packets when entering a state (for tracking progress)
    /// </summary>
    public int InventoryCountAtStateEntry { get; set; }

    /// <summary>
    /// Count of consecutive failures (for backoff/abort)
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Last transition reason (for debugging)
    /// </summary>
    public TransitionReason? LastTransitionReason { get; set; }

    /// <summary>
    /// Whether storage creation was attempted in current FindStorage state
    /// </summary>
    public bool StorageCreationAttempted { get; set; }

    /// <summary>
    /// Reset context for a new state
    /// </summary>
    public void ResetForNewState()
    {
        StateEnteredAt = DateTime.UtcNow;
        StorageCreationAttempted = false;
    }

    /// <summary>
    /// Clear all target references
    /// </summary>
    public void ClearTargets()
    {
        TargetSourceId = null;
        TargetStorageId = null;
        ActiveTransferId = null;
    }
}

/// <summary>
/// Event args for state transitions.
/// </summary>
public class StateTransitionEventArgs : EventArgs
{
    public AutomatonState FromState { get; }
    public AutomatonState ToState { get; }
    public TransitionReason Reason { get; }
    public DateTime TransitionTime { get; }

    public StateTransitionEventArgs(AutomatonState from, AutomatonState to, TransitionReason reason)
    {
        FromState = from;
        ToState = to;
        Reason = reason;
        TransitionTime = DateTime.UtcNow;
    }
}
