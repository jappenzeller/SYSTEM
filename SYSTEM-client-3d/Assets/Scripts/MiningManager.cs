using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine.Pool;
using UnityEngine.InputSystem;
using SYSTEM.Game;
using SYSTEM.WavePacket;
using SYSTEM.WavePacket.Movement;
using SYSTEM.UI;

public class MiningManager : MonoBehaviour
{
    // Singleton
    private static MiningManager instance;
    public static MiningManager Instance => instance;

    [Header("Mining Configuration")]
    [SerializeField] private float extractionTime = 2f;

    [SerializeField] private float maxMiningRange = 30f;
    [SerializeField] private float packetSpeed = 5f;
    [SerializeField] private float reachDistance = 1f;

    [Header("Visual Settings")]
    [SerializeField] private GameObject wavePacketPrefab;
    [SerializeField] private GameObject particleEffectPrefab;
    [SerializeField] private int particlePoolSize = 20;
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Extraction Visuals")]
    [SerializeField] private SYSTEM.WavePacket.WavePacketPrefabManager prefabManager;

    // Cached extracted packet prefab and settings
    private GameObject extractedPacketPrefab;
    private SYSTEM.WavePacket.WavePacketSettings extractedPacketSettings;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // Core references
    private DbConnection conn;
    private PlayerController playerController;
    private Transform playerTransform;
    
    // Mining state
    private bool isMining = false;
    private bool pendingMiningStart = false; // Guards against race condition while waiting for server response
    private WavePacketSource currentTarget;
    private ulong currentOrbId;
    private ulong currentSessionId; // Track the mining session ID
    private float miningTimer;
    private float extractionTimer;

    // Retargeting state
    private int failedExtractionCount = 0;
    private const int MAX_FAILED_EXTRACTIONS = 3; // Trigger retargeting after 3 failures (~6 seconds)

    // Constants
    private const float EXTRACTION_INTERVAL = 2f; // Extract every 2 seconds
    private const uint MAX_INVENTORY_CAPACITY = 300; // Maximum inventory capacity

    // Input System
    private PlayerInputActions playerInputActions;

    // Active packets tracking
    private Dictionary<ulong, GameObject> activePackets = new Dictionary<ulong, GameObject>();
    private Dictionary<ulong, Coroutine> packetMovementCoroutines = new Dictionary<ulong, Coroutine>();

    // Cache orb positions for extractions (survives orb deletion for multiplayer support)
    // Key: orb ID, Value: orb position
    // This cache persists even after orbs are deleted, allowing extraction visuals to spawn
    // from the correct location when multiple players are mining the same depleting orb
    private Dictionary<ulong, Vector3> orbPositionCache = new Dictionary<ulong, Vector3>();

    // Particle effects pool
    private IObjectPool<GameObject> particlePool;

    // Crystal config window reference (for M key mining)
    private CrystalConfigWindow crystalConfigWindow;

    // Events
    public event Action<bool> OnMiningStateChanged;

    public event Action<WavePacketSample> OnWavePacketExtracted;

    // Public properties for MiningRequestController access
    public ulong CurrentSessionId => currentSessionId;
    public ulong CurrentOrbId => currentOrbId;
    public bool IsMining => isMining;

    #region Unity Lifecycle
    
    void Awake()
    {
        // Singleton pattern
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("[Mining] MiningManager Awake - Initializing...");

        // Load prefab manager from Resources if not assigned
        if (prefabManager == null)
        {
            prefabManager = Resources.Load<SYSTEM.WavePacket.WavePacketPrefabManager>("WavePacketPrefabManager");
            if (prefabManager == null)
            {
                Debug.LogError("[Mining] WavePacketPrefabManager not found in Resources!");
            }
        }

        // Get extracted packet prefab and settings from manager
        if (prefabManager != null)
        {
            var (prefab, settings) = prefabManager.GetPrefabAndSettings(SYSTEM.WavePacket.WavePacketPrefabManager.PacketType.Extracted);
            extractedPacketPrefab = prefab;
            extractedPacketSettings = settings;
            Debug.Log($"[Mining] Loaded extracted packet config: prefab={prefab != null}, settings={settings != null}");
        }

        // Get connection reference
        conn = GameManager.Conn;
        if (conn == null)
        {
            Debug.LogError("[Mining] MiningManager: No database connection available!");
            enabled = false;
            return;
        }

        // Set up input actions
        Debug.Log("[Mining] Setting up Input System for E key interaction");
        playerInputActions = new PlayerInputActions();
        playerInputActions.Gameplay.Interact.performed += OnInteractPressed;

        // Set up particle pool
        particlePool = new ObjectPool<GameObject>(
            CreatePooledParticle,
            OnGetFromPool,
            OnReturnToPool,
            OnDestroyPoolObject,
            collectionCheck: true,
            defaultCapacity: particlePoolSize,
            maxSize: particlePoolSize * 2
        );

        // Find player controller
        playerController = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (playerController != null)
        {
            playerTransform = playerController.transform;
            Debug.Log($"[Mining] Found PlayerController at {playerTransform.position}");
        }
        else
        {
            Debug.LogWarning("[Mining] PlayerController not found in Awake - will retry later");
        }
    }
    
    void OnEnable()
    {
        // Enable input actions
        playerInputActions?.Enable();
        Debug.Log("[Mining] Input actions enabled - E key should now work for mining");

        // Subscribe to SpacetimeDB events
        if (conn != null)
        {
            // Subscribe to v2 reducer events
            conn.Reducers.OnStartMiningV2 += HandleStartMiningV2Result;
            conn.Reducers.OnStopMiningV2 += HandleStopMiningV2Result;
            conn.Reducers.OnExtractPacketsV2 += HandleExtractPacketsV2Result;
            conn.Reducers.OnCaptureExtractedPacketV2 += HandleCaptureExtractedPacketV2Result;

            // Subscribe to old events for compatibility
            // conn.Reducers.OnCaptureWavePacket += HandleWavePacketCaptured;

            // Subscribe to table events
            conn.Db.WavePacketExtraction.OnInsert += HandleWavePacketExtracted;
            conn.Db.WavePacketExtraction.OnDelete += HandleWavePacketExtractionRemoved;

            // Subscribe to mining session events for tracking
            conn.Db.MiningSession.OnInsert += HandleMiningSessionCreated;
            conn.Db.MiningSession.OnUpdate += HandleMiningSessionUpdated;
            conn.Db.MiningSession.OnDelete += HandleMiningSessionDeleted;

            // Subscribe to cleanup result
            conn.Reducers.OnCleanupMyMiningSessions += HandleCleanupResult;

            // Clean up any stale mining sessions from previous sessions
            conn.Reducers.CleanupMyMiningSessions();
            Debug.Log("[Mining] Sent cleanup request for any stale mining sessions");
        }
    }

    private void HandleCleanupResult(ReducerEventContext ctx)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            Debug.Log("[Mining] Stale mining sessions cleaned up successfully");
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            Debug.LogWarning($"[Mining] Failed to cleanup stale sessions: {reason}");
        }
    }
    
    void OnDestroy()
    {
        // Clean up input actions
        if (playerInputActions != null)
        {
            playerInputActions.Gameplay.Interact.performed -= OnInteractPressed;
            playerInputActions.Disable();
            playerInputActions.Dispose();
        }
    }

    void OnDisable()
    {
        // Disable input actions
        playerInputActions?.Disable();

        // Unsubscribe from events
        if (conn != null)
        {
            // Unsubscribe from v2 reducer events
            conn.Reducers.OnStartMiningV2 -= HandleStartMiningV2Result;
            conn.Reducers.OnStopMiningV2 -= HandleStopMiningV2Result;
            conn.Reducers.OnExtractPacketsV2 -= HandleExtractPacketsV2Result;
            conn.Reducers.OnCaptureExtractedPacketV2 -= HandleCaptureExtractedPacketV2Result;

            // conn.Reducers.OnCaptureWavePacket -= HandleWavePacketCaptured;

            conn.Db.WavePacketExtraction.OnInsert -= HandleWavePacketExtracted;
            conn.Db.WavePacketExtraction.OnDelete -= HandleWavePacketExtractionRemoved;

            // Unsubscribe from mining session events
            conn.Db.MiningSession.OnInsert -= HandleMiningSessionCreated;
            conn.Db.MiningSession.OnUpdate -= HandleMiningSessionUpdated;
            conn.Db.MiningSession.OnDelete -= HandleMiningSessionDeleted;

            // Unsubscribe from cleanup event
            conn.Reducers.OnCleanupMyMiningSessions -= HandleCleanupResult;
        }

        StopAllCoroutines();
    }
    
    void Update()
    {
        // Handle M key for mining toggle (uses crystal config)
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            HandleMKeyPressed();
        }

        if (isMining && currentTarget != null)
        {
            // Update mining timers
            miningTimer += Time.deltaTime;
            extractionTimer += Time.deltaTime;

            // Check if it's time to extract (v2 uses automatic extraction)
            if (extractionTimer >= EXTRACTION_INTERVAL && currentSessionId > 0)
            {
                // Get the active mining session to determine crystal composition
                var session = conn.Db.MiningSession.SessionId.Find(currentSessionId);
                if (session != null && session.CrystalComposition.Count > 0)
                {
                    // Build extraction request from session's crystal composition
                    var extractionRequest = new List<ExtractionRequest>();

                    foreach (var crystal in session.CrystalComposition)
                    {
                        extractionRequest.Add(new ExtractionRequest
                        {
                            Frequency = crystal.Frequency,
                            Count = 1  // Extract 1 packet per crystal per tick
                        });
                    }

                    Debug.Log($"[Mining] Requesting {extractionRequest.Count} frequencies from session {currentSessionId}");

                    // Call extract_packets_v2 with session ID and request
                    conn.Reducers.ExtractPacketsV2(currentSessionId, extractionRequest);
                    extractionTimer = 0f;
                }
                else
                {
                    Debug.LogWarning($"[Mining] Session {currentSessionId} not found or has no crystal composition!");
                }
            }

            // Check if target is still valid
            if (!IsOrbInRange(currentTarget))
            {
                StopMining();
            }
        }
    }
    
    void OnInteractPressed(InputAction.CallbackContext context)
    {
        Debug.Log($"[Mining] E key pressed! isMining={isMining}, playerTransform={playerTransform != null}");

        // Handle E key press for mining
        if (!isMining)
        {
            // Try to find nearest orb to start mining
            Debug.Log("[Mining] Looking for nearest source...");
            WavePacketSource nearestSource = FindNearestOrb();
            if (nearestSource != null)
            {
                Debug.Log($"[Mining] Found orb {nearestSource.SourceId} - starting mining!");
                StartMining(nearestSource);
            }
            else
            {
                Debug.Log($"[Mining] No orb in range to mine (max range: {maxMiningRange})");
            }
        }
        else
        {
            Debug.Log("[Mining] Stopping mining...");
            // Stop current mining
            StopMining();
        }
    }

    /// <summary>
    /// Handle M key press for mining with crystal config composition.
    /// </summary>
    private void HandleMKeyPressed()
    {
        // Find crystal config window if not cached
        if (crystalConfigWindow == null)
        {
            crystalConfigWindow = UnityEngine.Object.FindFirstObjectByType<CrystalConfigWindow>();
        }

        // If already mining or pending, stop
        if (isMining || pendingMiningStart)
        {
            Debug.Log("[Mining] M key: Stopping mining");
            StopMining();
            return;
        }

        // Check if we have a valid crystal config
        if (crystalConfigWindow == null)
        {
            Debug.LogWarning("[Mining] M key: CrystalConfigWindow not found! Press C to open config first.");
            return;
        }

        if (!crystalConfigWindow.HasValidConfig)
        {
            Debug.Log("[Mining] M key: No crystals configured. Press C to configure crystals first.");
            return;
        }

        // Find nearest source
        WavePacketSource nearestSource = FindNearestOrb();
        if (nearestSource == null)
        {
            Debug.Log($"[Mining] M key: No source in range (max: {maxMiningRange})");
            return;
        }

        // Get composition from crystal config
        var composition = crystalConfigWindow.GetMiningComposition();
        if (composition.Count == 0)
        {
            Debug.Log("[Mining] M key: Empty composition from crystal config");
            return;
        }

        Debug.Log($"[Mining] M key: Starting mining with {composition.Count} crystal types on source {nearestSource.SourceId}");
        StartMiningWithComposition(nearestSource, composition);
    }

    #endregion

    #region Mining Controls
    
    public void StartMining(WavePacketSource source)
    {
        if (isMining || source == null) return;

        // Check range
        if (!IsOrbInRange(source))
        {
            // Debug.Log("Orb is out of range");
            return;
        }

        // Get player's identity to verify connection
        var localPlayer = GameManager.GetLocalPlayer();
        if (localPlayer == null)
        {
            Debug.LogError("Cannot start mining - no local player");
            return;
        }

        // Check inventory capacity before starting mining
        uint currentInventory = GetPlayerInventoryCount();
        if (currentInventory >= MAX_INVENTORY_CAPACITY)
        {
            SystemDebug.LogWarning(SystemDebug.Category.Mining,
                $"[Mining] Cannot start mining - inventory full ({currentInventory}/{MAX_INVENTORY_CAPACITY})");
            UnityEngine.Debug.Log($"<color=yellow>[Mining] Cannot start mining - inventory full ({currentInventory}/{MAX_INVENTORY_CAPACITY})</color>");
            return;
        }

        // Send start mining v2 request to server with crystal composition
        currentOrbId = source.SourceId;
        currentTarget = source;

        // Cache orb position for extraction visuals (survives orb deletion)
        orbPositionCache[source.SourceId] = GetOrbWorldPosition(source);

        // Build crystal composition from selected crystal
        var composition = BuildCrystalComposition(GameData.Instance.SelectedCrystal);

        // Call the v2 reducer that uses database sessions
        conn.Reducers.StartMiningV2(currentOrbId, composition);

        Debug.Log($"[Mining] Starting mining session on orb {currentOrbId} with {composition.Count} crystal frequencies");
    }

    /// <summary>
    /// Start mining with a custom crystal composition (called by CrystalMiningUI)
    /// </summary>
    public void StartMiningWithComposition(WavePacketSource source, System.Collections.Generic.List<WavePacketSample> composition)
    {
        if (isMining || pendingMiningStart || source == null) return;

        // Check range
        Debug.Log($"[Mining] Checking range for orb {source.SourceId}...");
        if (!IsOrbInRange(source))
        {
            Debug.Log("[Mining] Orb is out of range");
            return;
        }
        Debug.Log($"[Mining] Orb {source.SourceId} is in range!");

        // Get player's identity to verify connection
        var localPlayer = GameManager.GetLocalPlayer();
        if (localPlayer == null)
        {
            Debug.LogError("[Mining] Cannot start mining - no local player");
            return;
        }

        if (composition == null || composition.Count == 0)
        {
            Debug.LogError("[Mining] Cannot start mining - no crystals selected");
            return;
        }

        // Check inventory capacity before starting mining
        uint currentInventory = GetPlayerInventoryCount();
        if (currentInventory >= MAX_INVENTORY_CAPACITY)
        {
            SystemDebug.LogWarning(SystemDebug.Category.Mining,
                $"[Mining] Cannot start mining - inventory full ({currentInventory}/{MAX_INVENTORY_CAPACITY})");
            UnityEngine.Debug.Log($"<color=yellow>[Mining] Cannot start mining - inventory full ({currentInventory}/{MAX_INVENTORY_CAPACITY})</color>");
            return;
        }

        // Send start mining v2 request to server with custom composition
        currentOrbId = source.SourceId;
        currentTarget = source;

        // Cache orb position for extraction visuals (survives orb deletion)
        orbPositionCache[source.SourceId] = GetOrbWorldPosition(source);

        // Call the v2 reducer with custom composition
        pendingMiningStart = true; // Guard against race condition
        conn.Reducers.StartMiningV2(currentOrbId, composition);

        Debug.Log($"[Mining] Starting mining session on orb {currentOrbId} with custom composition: {composition.Count} frequencies");
        foreach (var sample in composition)
        {
            Debug.Log($"  - Frequency {sample.Frequency:F3} x{sample.Count}");
        }

    }

    public void StopMining()
    {
        if (!isMining && !pendingMiningStart) return;

        // Send stop mining v2 request to server with session ID
        if (currentSessionId > 0)
        {
            conn.Reducers.StopMiningV2(currentSessionId);
            Debug.Log($"[Mining] Stopping mining session {currentSessionId}");
        }

        // Reset local state
        pendingMiningStart = false;
        isMining = false;
        currentTarget = null;
        currentOrbId = 0;
        currentSessionId = 0;
        miningTimer = 0f;
        extractionTimer = 0f;
        failedExtractionCount = 0; // Reset failure counter

        OnMiningStateChanged?.Invoke(false);
    }
    
    public void ToggleMining(WavePacketSource source = null)
    {
        if (isMining || pendingMiningStart)
        {
            StopMining();
        }
        else if (source != null)
        {
            StartMining(source);
        }
    }
    
    #endregion
    
    #region Server Event Handlers

    // V2 Reducer Handlers
    private void HandleStartMiningV2Result(ReducerEventContext ctx, ulong orbId, System.Collections.Generic.List<WavePacketSample> crystalComposition)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            // Look for the created session to get the session ID
            // The session should be created right after this reducer succeeds
            pendingMiningStart = false; // Clear guard - session event will set isMining
            Debug.Log($"[Mining] Successfully started mining orb {orbId} with {crystalComposition.Count} crystal frequencies");
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            Debug.LogError($"[Mining] Failed to start mining: {reason}");

            // Reset mining state on failure
            pendingMiningStart = false; // Clear guard
            isMining = false;
            currentTarget = null;
            currentOrbId = 0;
            currentSessionId = 0;

            OnMiningStateChanged?.Invoke(false);
        }
    }

    private void HandleStopMiningV2Result(ReducerEventContext ctx, ulong sessionId)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            Debug.Log($"[Mining] Successfully stopped mining session {sessionId}");
        }

        // Reset state regardless of result
        isMining = false;
        currentTarget = null;
        currentOrbId = 0;
        currentSessionId = 0;
        miningTimer = 0f;
        extractionTimer = 0f;

        OnMiningStateChanged?.Invoke(false);
    }

    private void HandleExtractPacketsV2Result(ReducerEventContext ctx, ulong sessionId, List<ExtractionRequest> requestedFrequencies)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            // Reset failure counter on successful extraction
            failedExtractionCount = 0;

        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            if (reason.Contains("cooldown"))
            {
                // This is expected, just wait for next interval
                SystemDebug.Log(SystemDebug.Category.Mining, $"[Mining] Extraction on cooldown: {reason}");
            }
            else if (reason.Contains("depleted") || reason.Contains("no longer exists"))
            {
                SystemDebug.Log(SystemDebug.Category.Mining, "[Mining] Source depleted or deleted, stopping mining");
                StopMining();
            }
            else if (reason.Contains("Cannot fulfill"))
            {
                // Track failed extractions for automatic retargeting
                failedExtractionCount++;

                SystemDebug.LogWarning(SystemDebug.Category.Mining,
                    $"[Mining] Request cannot be fulfilled ({failedExtractionCount}/{MAX_FAILED_EXTRACTIONS}): {reason}");

                // After multiple failures, try to find alternative orb
                if (failedExtractionCount >= MAX_FAILED_EXTRACTIONS)
                {

                    // Get current session to read crystal composition
                    var session = conn.Db.MiningSession.SessionId.Find(currentSessionId);
                    if (session != null && session.CrystalComposition.Count > 0)
                    {
                        // Find alternative orb with matching frequencies
                        WavePacketSource compatibleOrb = FindCompatibleOrb(session.CrystalComposition);

                        if (compatibleOrb != null)
                        {

                            UnityEngine.Debug.Log($"<color=yellow>[Mining] Target depleted, automatically switching to orb {compatibleOrb.SourceId}</color>");

                            // Stop current session
                            conn.Reducers.StopMiningV2(currentSessionId);

                            // Start mining new compatible orb
                            // Use a coroutine to delay slightly so the stop completes first
                            StartCoroutine(RetargetToNewOrb(compatibleOrb, session.CrystalComposition));

                            // Reset failure counter
                            failedExtractionCount = 0;
                        }
                        else
                        {
                            SystemDebug.LogWarning(SystemDebug.Category.Mining,
                                "[Mining] No compatible orbs available in range - stopping mining");

                            UnityEngine.Debug.Log("<color=yellow>[Mining] No compatible orbs available, stopping mining</color>");

                            // Stop mining - no alternatives available
                            StopMining();
                        }
                    }
                    else
                    {
                        SystemDebug.LogError(SystemDebug.Category.Mining,
                            $"[Mining] Cannot retarget - session {currentSessionId} not found or has no crystal composition");
                        StopMining();
                    }
                }
            }
            else
            {
                SystemDebug.LogError(SystemDebug.Category.Mining, $"[Mining] Failed to extract packets: {reason}");
            }
        }
    }

    private void HandleCaptureExtractedPacketV2Result(ReducerEventContext ctx, ulong packetId)
    {
        if (ctx.Event.Status is Status.Committed)
        {
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            if (reason.Contains("Inventory full"))
            {
                SystemDebug.LogWarning(SystemDebug.Category.Mining,
                    $"[Mining] Inventory full - stopping mining");

                UnityEngine.Debug.Log("<color=yellow>[Mining] Inventory full (300/300) - stopping mining</color>");

                // Stop mining because inventory is full
                StopMining();
            }
            else
            {
                SystemDebug.LogError(SystemDebug.Category.Mining,
                    $"[Mining] Failed to capture packet {packetId}: {reason}");
            }
        }
    }

    /// <summary>
    /// Coroutine to switch to a new orb target with slight delay
    /// Ensures previous session cleanup completes before starting new session
    /// </summary>
    private IEnumerator RetargetToNewOrb(WavePacketSource newOrb, List<WavePacketSample> crystalComposition)
    {
        // Wait a frame for the stop command to process
        yield return new WaitForSeconds(0.2f);

        // Start mining the new orb with the same crystal composition
        StartMiningWithComposition(newOrb, crystalComposition);

    }

    // Mining Session Table Event Handlers
    private void HandleMiningSessionCreated(EventContext ctx, MiningSession session)
    {
        // Check if this session belongs to us
        var localIdentity = GameManager.LocalIdentity;
        if (localIdentity.HasValue && session.PlayerIdentity == localIdentity.Value && session.SourceId == currentOrbId)
        {
            currentSessionId = session.SessionId;
            pendingMiningStart = false; // Clear guard - we're now confirmed mining
            isMining = true;
            miningTimer = 0f;
            extractionTimer = 0f;
            failedExtractionCount = 0; // Reset failure counter for new session

            Debug.Log($"[Mining] Session created with ID: {currentSessionId}");
            OnMiningStateChanged?.Invoke(true);
        }
    }

    private void HandleMiningSessionUpdated(EventContext ctx, MiningSession oldSession, MiningSession newSession)
    {
        if (newSession.SessionId == currentSessionId)
        {
            // Check if session became inactive
            if (!newSession.IsActive && oldSession.IsActive)
            {
                Debug.Log($"[Mining] Session {currentSessionId} became inactive - allowing pending extractions to complete");

                // DON'T call StopMining() - that would destroy in-flight packet visuals!
                // Session inactive means "no new extractions", not "cancel existing packets"
                // Let the packets complete their flight and add to inventory

                // Just clean up local mining state
                isMining = false;
                currentTarget = null;
                currentOrbId = 0;
                currentSessionId = 0;
                miningTimer = 0f;
                extractionTimer = 0f;
                failedExtractionCount = 0; // Reset failure counter

                OnMiningStateChanged?.Invoke(false);
            }
        }
    }

    private void HandleMiningSessionDeleted(EventContext ctx, MiningSession session)
    {
        if (session.SessionId == currentSessionId)
        {
            Debug.Log($"[Mining] Session {currentSessionId} was deleted");
            // Reset local state
            isMining = false;
            currentTarget = null;
            currentOrbId = 0;
            currentSessionId = 0;
            miningTimer = 0f;
            extractionTimer = 0f;
            failedExtractionCount = 0; // Reset failure counter

            OnMiningStateChanged?.Invoke(false);
        }
    }

    
    /// <summary>
    /// Handles wave packet extraction events - VISUALIZES ALL PLAYERS for multiplayer sync.
    /// Server authoritative: Only creates WavePacketExtraction after validating cooldowns.
    /// </summary>
    private void HandleWavePacketExtracted(EventContext ctx, WavePacketExtraction extraction)
    {
        // MULTIPLAYER FIX: Visualize ALL players' mining, not just local player

        // Log composition
        foreach (var sample in extraction.Composition)
        {
        }

        // Invoke event with first signature for backwards compatibility (only for local player)
        var localPlayer = GameManager.GetLocalPlayer();
        if (localPlayer != null && extraction.PlayerId == localPlayer.PlayerId)
        {
            if (extraction.Composition.Count > 0)
            {
                OnWavePacketExtracted?.Invoke(new WavePacketSample
                {
                    Frequency = extraction.Composition[0].Frequency,
                    Amplitude = extraction.Composition[0].Amplitude,
                    Phase = extraction.Composition[0].Phase
                });
            }
        }

        // Create visual packet for ALL players
        CreateVisualPacket(extraction);
    }
    
    private void HandleWavePacketExtractionRemoved(EventContext ctx, WavePacketExtraction extraction)
    {
        // Clean up visual if it exists
        if (activePackets.TryGetValue(extraction.PacketId, out GameObject packet))
        {
            if (packetMovementCoroutines.TryGetValue(extraction.PacketId, out Coroutine coroutine))
            {
                StopCoroutine(coroutine);
                packetMovementCoroutines.Remove(extraction.PacketId);
            }

            Destroy(packet);
            activePackets.Remove(extraction.PacketId);

        }
    }
    
    private void HandleWavePacketCaptured(ReducerEventContext ctx, ulong packetId)
    {
        // Debug.Log($"Wave packet {packetId} captured!");
        
        if (ctx.Event.Status is Status.Failed(var reason))
        {
            Debug.LogError($"Failed to capture packet: {reason}");
            return;
        }
        
        // The capture was successful
        // OnWavePacketCaptured event will be fired when we see the storage update
    }
    
    #endregion
    
    #region Visual Effects
    
    private void CreateVisualPacket(WavePacketExtraction extraction)
    {
        // Get source position based on source type
        Vector3 sourcePos = Vector3.zero;
        bool foundSource = false;

        if (extraction.SourceType == "orb")
        {
            // First, try to find the actual GameObject (which has correct position from ServerDrivenMovement)
            var sourceObj = GameObject.Find($"WavePacketSource_{extraction.SourceId}");
            if (sourceObj != null)
            {
                // Use GameObject position (includes proper height from movement component)
                sourcePos = sourceObj.transform.position;
                orbPositionCache[extraction.SourceId] = sourcePos;
                foundSource = true;
            }
            else if (orbPositionCache.TryGetValue(extraction.SourceId, out Vector3 cachedPos))
            {
                // Source GameObject was deleted but we have cached position (happens on depletion)
                sourcePos = cachedPos;
                foundSource = true;
            }
            else
            {
                // Fallback: try database position
                var source = conn.Db.WavePacketSource.SourceId.Find(extraction.SourceId);
                if (source != null)
                {
                    sourcePos = GetOrbWorldPosition(source);
                    orbPositionCache[extraction.SourceId] = sourcePos;
                    foundSource = true;
                }
                else
                {
                    SystemDebug.LogWarning(SystemDebug.Category.Mining,
                        $"Could not find source {extraction.SourceId} GameObject or database entry");
                }
            }
        }
        // Add other source types later (circuit, device)

        if (!foundSource)
            return;

        // Get player world position
        Vector3 playerWorldPos = playerTransform.position;

        // Calculate rotations for surface orientation (but use base positions, not height-adjusted)
        Vector3 sourceNormal = SYSTEM.WavePacket.PacketPositionHelper.GetSurfaceNormal(sourcePos);
        Vector3 playerNormal = SYSTEM.WavePacket.PacketPositionHelper.GetSurfaceNormal(playerWorldPos);
        Quaternion startRotation = SYSTEM.WavePacket.PacketPositionHelper.GetOrientationForSurface(sourceNormal);
        Quaternion targetRotation = SYSTEM.WavePacket.PacketPositionHelper.GetOrientationForSurface(playerNormal);

        GameObject packet = null;

        // Create flying extraction packet
        if (extractedPacketPrefab != null && extractedPacketSettings != null)
        {
            // Capture packet ID for lambda closure
            ulong packetId = extraction.PacketId;

            // Instantiate packet at source position
            packet = Instantiate(extractedPacketPrefab, sourcePos, startRotation);
            packet.name = $"ExtractedPacket_{extraction.PacketId}";

            // Initialize WavePacketVisual with proper settings
            var visual = packet.GetComponent<WavePacketVisual>();
            if (visual != null)
            {
                var sampleList = new List<WavePacketSample>(extraction.Composition);
                uint totalPackets = 0;
                foreach (var sample in extraction.Composition) totalPackets += sample.Count;

                Color packetColor = FrequencyConstants.GetColorForFrequency(extraction.Composition[0].Frequency);
                visual.Initialize(extractedPacketSettings, 0, packetColor, totalPackets, 0, sampleList);
            }

            // Add trajectory for mining extraction (constant height direct movement)
            PacketMovementFactory.CreateMiningTrajectory(
                packet,
                playerWorldPos,
                packetSpeed,
                () => {
                    // Callback when packet arrives at player
                    SpawnCaptureEffect(playerWorldPos);
                    conn.Reducers.CaptureExtractedPacketV2(packetId);
                }
            );

            // Add packet ID for tracking
            var flyingPacket = packet.GetComponent<FlyingPacket>();
            if (flyingPacket != null)
            {
                flyingPacket.packetId = extraction.PacketId;
            }
        }
        else
        {
            SystemDebug.LogWarning(SystemDebug.Category.Mining,
                "[Mining] Missing extracted packet prefab or settings - packets will not be visualized!");
            return;
        }

        if (packet == null)
            return;

        // Track it
        activePackets[extraction.PacketId] = packet;

    }
    
    private void ConfigurePacketVisual(GameObject packet, WavePacketSample signature)
    {
        // Get color based on signature
        Color color = SignatureToColor(signature);
        
        // Apply to renderer
        var renderer = packet.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
            renderer.material.SetColor("_EmissionColor", color * 2f);
        }
        
        // Apply to particle system if present
        var particleSystem = packet.GetComponentInChildren<ParticleSystem>();
        if (particleSystem != null)
        {
            var main = particleSystem.main;
            main.startColor = color;
        }
    }
    
    private Color SignatureToColor(WavePacketSample signature)
    {
        // Map frequency to color
        float hue = signature.Frequency / (2f * Mathf.PI);
        return Color.HSVToRGB(hue, 0.8f, 1f);
    }
    
    private IEnumerator MovePacketToPlayer(ulong packetId, GameObject packet)
    {
        if (playerTransform == null) yield break;
        
        Vector3 startPos = packet.transform.position;
        float distance = Vector3.Distance(startPos, playerTransform.position);
        float duration = distance / packetSpeed;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            if (packet == null || playerTransform == null) yield break;
            
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float curvedT = movementCurve.Evaluate(t);
            
            // Update position
            Vector3 targetPos = playerTransform.position + Vector3.up * 0.5f;
            packet.transform.position = Vector3.Lerp(startPos, targetPos, curvedT);
            
            // Check if close enough to capture
            if (Vector3.Distance(packet.transform.position, targetPos) < reachDistance)
            {
                // Request capture
                conn.Reducers.CaptureExtractedPacketV2(packetId);
                
                // Spawn particle effect
                SpawnCaptureEffect(packet.transform.position);
                
                // Remove from tracking
                packetMovementCoroutines.Remove(packetId);
                activePackets.Remove(packetId);

                // Destroy the packet visual
                Destroy(packet);
                yield break;
            }
            
            yield return null;
        }
    }
    
    private void SpawnCaptureEffect(Vector3 position)
    {
        if (particleEffectPrefab == null) return;
        
        var effect = particlePool.Get();
        effect.transform.position = position;
        
        // Return to pool after 2 seconds
        StartCoroutine(ReturnEffectToPool(effect, 2f));
    }
    
    private IEnumerator ReturnEffectToPool(GameObject effect, float delay)
    {
        yield return new WaitForSeconds(delay);
        particlePool.Release(effect);
    }
    
    #endregion

    #region Composite Packet Helpers

    private IEnumerator MoveCompositePacketToPlayer(ulong packetId, GameObject visual, Vector3 startPos, Vector3 initialTargetPos, ulong flightTimeMs)
    {
        float duration = flightTimeMs / 1000f; // Convert ms to seconds
        float elapsed = 0f;

        while (elapsed < duration && visual != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Get current player position BEFORE calculating packet position
            // This ensures we always track the player's latest position
            Vector3 currentPlayerPos = playerTransform.position;

            // Use movement curve for smooth animation
            float curveValue = movementCurve.Evaluate(t);
            visual.transform.position = Vector3.Lerp(startPos, currentPlayerPos, curveValue);

            yield return null;
        }

        // Packet reached player - call capture reducer
        if (visual != null)
        {
            // Spawn capture effect at player's CURRENT position (in case they moved during flight)
            SpawnCaptureEffect(playerTransform.position);

            // Call the capture reducer to add packets to inventory
            conn.Reducers.CaptureExtractedPacketV2(packetId);


            // Visual cleanup happens in HandleWavePacketExtractionRemoved
        }
    }

    private Vector3 GetOrbWorldPosition(WavePacketSource source)
    {
        // Convert orb's world coordinates and local position to Unity world position
        var worldManager = FindFirstObjectByType<SYSTEM.Game.WorldManager>();
        if (worldManager != null)
        {
            // Check if WorldManager has the conversion method
            var method = worldManager.GetType().GetMethod("ConvertOrbPositionToUnityWorld");
            if (method != null)
            {
                return (Vector3)method.Invoke(worldManager, new object[] { source.WorldCoords, source.Position });
            }
        }

        // Fallback: use orb position directly (assumes same world)
        return new Vector3(source.Position.X, source.Position.Y, source.Position.Z);
    }

    #endregion

    #region Utility Methods
    
// [DEPRECATED]     private void RequestExtraction()
// [DEPRECATED]     {
// [DEPRECATED]         if (!isMining || currentOrbId == 0) return;
// [DEPRECATED]         
// [DEPRECATED]         // Debug.Log("Requesting wave packet extraction...");
// [DEPRECATED]         conn.Reducers.ExtractWavePacket();
// [DEPRECATED]     }
    
    private bool IsOrbInRange(WavePacketSource source)
    {
        if (source == null)
        {
            return false;
        }

        // Try to find playerTransform if not set
        if (playerTransform == null)
        {
            playerController = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (playerController != null)
            {
                playerTransform = playerController.transform;
            }
            else
            {
                return false;
            }
        }

        Vector3 orbPosition;

        // Try to find orb GameObject first
        var orbObj = GameObject.Find($"WavePacketSource_{source.SourceId}");
        if (orbObj != null)
        {
            orbPosition = orbObj.transform.position;
        }
        else
        {
            // Fallback: use database position
            orbPosition = new Vector3(source.Position.X, source.Position.Y, source.Position.Z);
            SystemDebug.LogWarning(SystemDebug.Category.Mining,
                $"Could not find GameObject for WavePacketSource_{source.SourceId}, using database position {orbPosition}");
        }

        float distance = Vector3.Distance(playerTransform.position, orbPosition);

        return distance <= maxMiningRange;
    }

    private WavePacketSource FindNearestOrb()
    {
        if (playerTransform == null)
        {
            Debug.LogWarning("[Mining] playerTransform is null - trying to find PlayerController");
            playerController = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (playerController != null)
            {
                playerTransform = playerController.transform;
                Debug.Log($"[Mining] Found PlayerController at {playerTransform.position}");
            }
            else
            {
                Debug.LogError("[Mining] Could not find PlayerController!");
                return null;
            }
        }

        WavePacketSource nearestSource = null;
        float nearestDistance = maxMiningRange;
        int orbCount = 0;
        int skippedDepleted = 0;
        int missingGameObjects = 0;

        // Check all orbs in the database
        foreach (var source in conn.Db.WavePacketSource.Iter())
        {
            orbCount++;

            // Skip depleted orbs
            if (source.TotalWavePackets == 0)
            {
                skippedDepleted++;
                continue;
            }

            // Find the GameObject for this source
            var orbObj = GameObject.Find($"WavePacketSource_{source.SourceId}");
            if (orbObj == null)
            {
                missingGameObjects++;
                Debug.LogWarning($"[Mining] Could not find GameObject 'WavePacketSource_{source.SourceId}' for source {source.SourceId}");
                continue;
            }

            // Check distance
            float distance = Vector3.Distance(playerTransform.position, orbObj.transform.position);
            Debug.Log($"[Mining] Orb {source.SourceId} at position {orbObj.transform.position} - distance: {distance:F1} (max: {maxMiningRange})");

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestSource = source;
            }
        }

        Debug.Log($"[Mining] Scanned {orbCount} orbs (skipped {skippedDepleted} depleted, {missingGameObjects} missing GameObjects)");

        if (nearestSource != null)
        {
            Debug.Log($"[Mining] Found nearest orb {nearestSource.SourceId} at distance {nearestDistance:F1}");
        }
        else
        {
            Debug.Log($"[Mining] No valid orb found within range {maxMiningRange}");
        }

        return nearestSource;
    }

    /// <summary>
    /// Find nearest orb that has frequencies compatible with the given crystal composition
    /// Used for automatic retargeting when current orb is depleted
    /// </summary>
    private WavePacketSource FindCompatibleOrb(List<WavePacketSample> crystalComposition)
    {
        if (playerTransform == null || crystalComposition == null || crystalComposition.Count == 0)
        {
            SystemDebug.LogWarning(SystemDebug.Category.Mining, "[Mining] Cannot find compatible orb - missing player transform or crystal composition");
            return null;
        }

        WavePacketSource nearestCompatibleOrb = null;
        float nearestDistance = maxMiningRange;
        int orbCount = 0;
        int compatibleCount = 0;
        int incompatibleCount = 0;

        SystemDebug.Log(SystemDebug.Category.Mining, $"[Mining] Searching for compatible orbs with {crystalComposition.Count} crystal frequencies...");

        // Check all orbs in the database
        foreach (var source in conn.Db.WavePacketSource.Iter())
        {
            orbCount++;

            // Skip depleted orbs
            if (source.TotalWavePackets == 0)
            {
                continue;
            }

            // Check if orb has matching frequencies
            if (!OrbHasMatchingFrequencies(source, crystalComposition))
            {
                incompatibleCount++;
                continue;
            }

            compatibleCount++;

            // Find the GameObject for this source to check distance
            var orbObj = GameObject.Find($"WavePacketSource_{source.SourceId}");
            if (orbObj == null)
            {
                // Try using database position as fallback
                Vector3 orbDbPos = new Vector3(source.Position.X, source.Position.Y, source.Position.Z);
                float dbDistance = Vector3.Distance(playerTransform.position, orbDbPos);

                if (dbDistance < nearestDistance)
                {
                    nearestDistance = dbDistance;
                    nearestCompatibleOrb = source;
                }
                continue;
            }

            // Check distance
            float distance = Vector3.Distance(playerTransform.position, orbObj.transform.position);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestCompatibleOrb = source;
            }
        }


        if (nearestCompatibleOrb != null)
        {
        }
        else
        {
            SystemDebug.LogWarning(SystemDebug.Category.Mining,
                "[Mining] No compatible orbs found in range");
        }

        return nearestCompatibleOrb;
    }

    /// <summary>
    /// Check if an orb has any frequencies that match the crystal composition
    /// </summary>
    private bool OrbHasMatchingFrequencies(WavePacketSource source, List<WavePacketSample> crystalComposition)
    {
        if (source == null || source.WavePacketComposition == null || source.WavePacketComposition.Count == 0)
        {
            return false;
        }

        const float FREQUENCY_TOLERANCE = 0.01f; // Small tolerance for floating point comparison

        // Check if any crystal frequency exists in the orb
        foreach (var crystal in crystalComposition)
        {
            foreach (var orbSample in source.WavePacketComposition)
            {
                // Compare frequencies with tolerance
                if (Mathf.Abs(orbSample.Frequency - crystal.Frequency) < FREQUENCY_TOLERANCE)
                {
                    // Found a match - orb has this frequency
                    if (orbSample.Count > 0)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Get the current player's inventory count
    /// Returns 0 if inventory not found or player not found
    /// </summary>
    private uint GetPlayerInventoryCount()
    {
        try
        {
            var localPlayer = GameManager.GetLocalPlayer();
            if (localPlayer == null)
            {
                SystemDebug.LogWarning(SystemDebug.Category.Mining, "[Mining] Cannot check inventory - local player not found");
                return 0;
            }

            var inventory = conn.Db.PlayerInventory.PlayerId.Find(localPlayer.PlayerId);
            if (inventory != null)
            {
                return inventory.TotalCount;
            }
            else
            {
                // Inventory not created yet - treat as empty
                return 0;
            }
        }
        catch (System.Exception ex)
        {
            SystemDebug.LogError(SystemDebug.Category.Mining, $"[Mining] Error checking inventory: {ex.Message}");
            return 0;
        }
    }

    public bool CanMineOrb(WavePacketSource source)
    {
        if (source == null) return false;
        
        // Check if we have a compatible crystal
        var localPlayer = GameManager.GetLocalPlayer();
        if (localPlayer == null) return false;
        
        foreach (var crystal in conn.Db.PlayerCrystal.Iter())
        {
            if (crystal.PlayerId == localPlayer.PlayerId)
            {
                // Check if crystal frequency matches any packet in the orb
                // This would require checking the orb's available packets
                return true; // Simplified for now
            }
        }
        
        return false;
    }
    
    #endregion

    #region Pool Management
    
    private GameObject CreatePooledParticle()
    {
        if (particleEffectPrefab == null)
        {
            return new GameObject("Particle Effect");
        }
        return Instantiate(particleEffectPrefab);
    }
    
    private void OnGetFromPool(GameObject obj)
    {
        obj.SetActive(true);
    }
    
    private void OnReturnToPool(GameObject obj)
    {
        obj.SetActive(false);
    }
    
    private void OnDestroyPoolObject(GameObject obj)
    {
        Destroy(obj);
    }
    
    #endregion
    
    #region Debug
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 200, 300, 200));
        GUILayout.Label($"Mining: {isMining}");
        if (currentTarget != null)
        {
            GUILayout.Label($"Target Orb: {currentOrbId}");
            GUILayout.Label($"Mining Time: {miningTimer:F1}s");
            GUILayout.Label($"Next Extraction: {extractionTime - extractionTimer:F1}s");
        }
        GUILayout.Label($"Active Packets: {activePackets.Count}");
        GUILayout.EndArea();
    }

    #endregion

    #region Crystal Composition Helpers

    /// <summary>
    /// Gets the frequency value for a crystal type
    /// </summary>
    private float GetCrystalFrequency(CrystalType crystalType)
    {
        switch (crystalType)
        {
            case CrystalType.Red:
                return 0.0f;      // Red (0°)
            case CrystalType.Green:
                return 2.094f;    // Green (120° = 2π/3)
            case CrystalType.Blue:
                return 4.189f;    // Blue (240° = 4π/3)
            default:
                return 0.0f;
        }
    }

    /// <summary>
    /// Builds a wave packet composition from a crystal type
    /// Maps old CrystalType enum to new unified wave packet system
    /// </summary>
    private System.Collections.Generic.List<WavePacketSample> BuildCrystalComposition(CrystalType crystalType)
    {
        var composition = new System.Collections.Generic.List<WavePacketSample>();

        switch (crystalType)
        {
            case CrystalType.Red:
                // Red crystal: pure red frequency
                composition.Add(new WavePacketSample
                {
                    Frequency = 0.0f,      // Red (0°)
                    Amplitude = 1.0f,
                    Phase = 0.0f,
                    Count = 1
                });
                break;

            case CrystalType.Green:
                // Green crystal: pure green frequency
                composition.Add(new WavePacketSample
                {
                    Frequency = 2.094f,    // Green (120° = 2π/3)
                    Amplitude = 1.0f,
                    Phase = 0.0f,
                    Count = 1
                });
                break;

            case CrystalType.Blue:
                // Blue crystal: pure blue frequency
                composition.Add(new WavePacketSample
                {
                    Frequency = 4.189f,    // Blue (240° = 4π/3)
                    Amplitude = 1.0f,
                    Phase = 0.0f,
                    Count = 1
                });
                break;
        }

        return composition;
    }

    /// <summary>
    /// Builds custom crystal composition for advanced mining
    /// Allows multiple frequencies and counts
    /// </summary>
    public System.Collections.Generic.List<WavePacketSample> BuildCustomCrystalComposition(int red, int green, int blue)
    {
        var composition = new System.Collections.Generic.List<WavePacketSample>();

        if (red > 0)
        {
            composition.Add(new WavePacketSample
            {
                Frequency = 0.0f,      // Red
                Amplitude = 1.0f,
                Phase = 0.0f,
                Count = (uint)red
            });
        }

        if (green > 0)
        {
            composition.Add(new WavePacketSample
            {
                Frequency = 2.094f,    // Green
                Amplitude = 1.0f,
                Phase = 0.0f,
                Count = (uint)green
            });
        }

        if (blue > 0)
        {
            composition.Add(new WavePacketSample
            {
                Frequency = 4.189f,    // Blue
                Amplitude = 1.0f,
                Phase = 0.0f,
                Count = (uint)blue
            });
        }

        return composition;
    }

    #endregion

    #region Static Helpers

    /// <summary>
    /// Ensures the MiningManager exists in the scene.
    /// Logs error if not found - component must be added to WorldScene manually.
    /// </summary>
    public static void EnsureMiningManager()
    {
        if (instance == null)
        {
            Debug.LogError("[MiningManager] MiningManager not found in scene! Add MiningManager component to WorldScene.");
        }
    }

    #endregion
}