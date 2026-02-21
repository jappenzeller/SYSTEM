namespace SYSTEM.HeadlessClient.Automaton;

/// <summary>
/// Configuration for the Automaton FSM that controls autonomous agent behavior.
/// The Automaton orchestrates the full gameplay loop: mine → collect → transfer → assess world.
/// </summary>
public class AutomatonConfig
{
    // === Mining Phase ===

    /// <summary>
    /// Maximum mining range (must match Unity client and server)
    /// </summary>
    public float MiningRange { get; set; } = 20f;

    /// <summary>
    /// How long to wait for sources before moving to explore
    /// </summary>
    public float FindOrbTimeoutSeconds { get; set; } = 30f;

    /// <summary>
    /// How far to wander when exploring for sources
    /// </summary>
    public float ExploreWanderDistance { get; set; } = 50f;

    // === Collection Phase ===

    /// <summary>
    /// Inventory capacity threshold to trigger transfer (percentage 0.0-1.0)
    /// </summary>
    public float InventoryFullThreshold { get; set; } = 0.9f;

    /// <summary>
    /// Time to wait for packets to arrive after mining stops (seconds)
    /// </summary>
    public float PacketCollectionTimeoutSeconds { get; set; } = 10f;

    /// <summary>
    /// Maximum packets in flight before pausing extraction
    /// </summary>
    public int MaxPacketsInFlight { get; set; } = 5;

    // === Storage/Transfer Phase ===

    /// <summary>
    /// Maximum range to search for storage devices
    /// </summary>
    public float StorageSearchRange { get; set; } = 100f;

    /// <summary>
    /// How close we need to be to storage to transfer
    /// </summary>
    public float StorageInteractionRange { get; set; } = 15f;

    /// <summary>
    /// Time to wait for transfer to complete (seconds)
    /// </summary>
    public float TransferTimeoutSeconds { get; set; } = 30f;

    // === World Assessment Phase ===

    /// <summary>
    /// Minimum tunnel charge threshold to consider activation (0-100)
    /// </summary>
    public float TunnelActivationThreshold { get; set; } = 100f;

    /// <summary>
    /// Explore new worlds when they crystallize
    /// </summary>
    public bool ExploreNewWorlds { get; set; } = true;

    // === General Behavior ===

    /// <summary>
    /// Enable autonomous behavior (if false, FSM pauses in IDLE)
    /// </summary>
    public bool AutomationEnabled { get; set; } = true;

    /// <summary>
    /// Delay between state machine ticks (seconds)
    /// </summary>
    public float TickIntervalSeconds { get; set; } = 0.5f;

    /// <summary>
    /// Maximum time to spend in any single state before forcing transition (seconds)
    /// </summary>
    public float StateTimeoutSeconds { get; set; } = 300f;

    /// <summary>
    /// Log state transitions and decisions
    /// </summary>
    public bool VerboseLogging { get; set; } = true;
}
