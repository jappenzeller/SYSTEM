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

public class WavePacketMiningSystem : MonoBehaviour
{
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
    [SerializeField] private ExtractionVisualController extractionVisualController;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // Core references
    private DbConnection conn;
    private PlayerController playerController;
    private Transform playerTransform;
    
    // Mining state
    private bool isMining = false;
    private WavePacketOrb currentTarget;
    private ulong currentOrbId;
    private ulong currentSessionId; // Track the mining session ID
    private float miningTimer;
    private float extractionTimer;

    // Constants
    private const float EXTRACTION_INTERVAL = 2f; // Extract every 2 seconds

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
    
    // Events
    public event Action<bool> OnMiningStateChanged;

    public event Action<WavePacketSignature> OnWavePacketExtracted;

    // Public properties for MiningRequestController access
    public ulong CurrentSessionId => currentSessionId;
    public ulong CurrentOrbId => currentOrbId;
    public bool IsMining => isMining;

    #region Unity Lifecycle
    
    void Awake()
    {
        Debug.Log("[Mining] WavePacketMiningSystem Awake - Initializing...");

        // Auto-create ExtractionVisualController if not assigned
        if (extractionVisualController == null)
        {
            GameObject controllerObj = new GameObject("ExtractionVisualController");
            controllerObj.transform.SetParent(transform);
            extractionVisualController = controllerObj.AddComponent<ExtractionVisualController>();
            Debug.Log("[Mining] Auto-created ExtractionVisualController");
        }

        // Load wave packet prefab from Resources if not assigned
        if (wavePacketPrefab == null)
        {
            wavePacketPrefab = Resources.Load<GameObject>("WavePacketVisual");
            if (wavePacketPrefab == null)
            {
                Debug.LogError("[WavePacketMiningSystem] Failed to load WavePacketVisual prefab from Resources!");
            }
            else
            {
                Debug.Log("[WavePacketMiningSystem] Loaded WavePacketVisual prefab from Resources");
            }
        }

        // Get connection reference
        conn = GameManager.Conn;
        if (conn == null)
        {
            Debug.LogError("WavePacketMiningSystem: No database connection available!");
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

            // Subscribe to old events for compatibility
            conn.Reducers.OnCaptureWavePacket += HandleWavePacketCaptured;

            // Subscribe to table events
            conn.Db.WavePacketExtraction.OnInsert += HandleWavePacketExtracted;
            conn.Db.WavePacketExtraction.OnDelete += HandleWavePacketExtractionRemoved;

            // Subscribe to mining session events for tracking
            conn.Db.MiningSession.OnInsert += HandleMiningSessionCreated;
            conn.Db.MiningSession.OnUpdate += HandleMiningSessionUpdated;
            conn.Db.MiningSession.OnDelete += HandleMiningSessionDeleted;
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

            conn.Reducers.OnCaptureWavePacket -= HandleWavePacketCaptured;

            conn.Db.WavePacketExtraction.OnInsert -= HandleWavePacketExtracted;
            conn.Db.WavePacketExtraction.OnDelete -= HandleWavePacketExtractionRemoved;

            // Unsubscribe from mining session events
            conn.Db.MiningSession.OnInsert -= HandleMiningSessionCreated;
            conn.Db.MiningSession.OnUpdate -= HandleMiningSessionUpdated;
            conn.Db.MiningSession.OnDelete -= HandleMiningSessionDeleted;
        }

        StopAllCoroutines();
    }
    
    void Update()
    {
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
            Debug.Log("[Mining] Looking for nearest orb...");
            WavePacketOrb nearestOrb = FindNearestOrb();
            if (nearestOrb != null)
            {
                Debug.Log($"[Mining] Found orb {nearestOrb.OrbId} - starting mining!");
                StartMining(nearestOrb);
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

    #endregion

    #region Mining Controls
    
    public void StartMining(WavePacketOrb orb)
    {
        if (isMining || orb == null) return;

        // Check range
        if (!IsOrbInRange(orb))
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

        // Send start mining v2 request to server with crystal composition
        currentOrbId = orb.OrbId;
        currentTarget = orb;

        // Cache orb position for extraction visuals (survives orb deletion)
        orbPositionCache[orb.OrbId] = GetOrbWorldPosition(orb);

        // Build crystal composition from selected crystal
        var composition = BuildCrystalComposition(GameData.Instance.SelectedCrystal);

        // Call the v2 reducer that uses database sessions
        conn.Reducers.StartMiningV2(currentOrbId, composition);

        Debug.Log($"[Mining] Starting mining session on orb {currentOrbId} with {composition.Count} crystal frequencies");
    }

    /// <summary>
    /// Start mining with a custom crystal composition (called by CrystalMiningUI)
    /// </summary>
    public void StartMiningWithComposition(WavePacketOrb orb, System.Collections.Generic.List<WavePacketSample> composition)
    {
        if (isMining || orb == null) return;

        // Check range
        Debug.Log($"[Mining] Checking range for orb {orb.OrbId}...");
        if (!IsOrbInRange(orb))
        {
            Debug.Log("[Mining] Orb is out of range");
            return;
        }
        Debug.Log($"[Mining] Orb {orb.OrbId} is in range!");

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

        // Send start mining v2 request to server with custom composition
        currentOrbId = orb.OrbId;
        currentTarget = orb;

        // Cache orb position for extraction visuals (survives orb deletion)
        orbPositionCache[orb.OrbId] = GetOrbWorldPosition(orb);

        // Call the v2 reducer with custom composition
        conn.Reducers.StartMiningV2(currentOrbId, composition);

        Debug.Log($"[Mining] Starting mining session on orb {currentOrbId} with custom composition: {composition.Count} frequencies");
        foreach (var sample in composition)
        {
            Debug.Log($"  - Frequency {sample.Frequency:F3} x{sample.Count}");
        }

        // Start extraction visual effect
        if (extractionVisualController != null)
        {
            Vector3 orbPosition = new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z);
            extractionVisualController.StartExtraction(orb.OrbId, orb.WavePacketComposition.ToArray(), orbPosition);
            Debug.Log($"[Mining] Started extraction visual for orb {orb.OrbId}");
        }
    }

    public void StopMining()
    {
        if (!isMining) return;

        // Send stop mining v2 request to server with session ID
        if (currentSessionId > 0)
        {
            conn.Reducers.StopMiningV2(currentSessionId);
            Debug.Log($"[Mining] Stopping mining session {currentSessionId}");
        }

        // Stop extraction visual effect
        if (extractionVisualController != null && currentOrbId > 0)
        {
            extractionVisualController.StopExtraction(currentOrbId);
            Debug.Log($"[Mining] Stopped extraction visual for orb {currentOrbId}");
        }

        // Reset local state
        isMining = false;
        currentTarget = null;
        currentOrbId = 0;
        currentSessionId = 0;
        miningTimer = 0f;
        extractionTimer = 0f;

        OnMiningStateChanged?.Invoke(false);
    }
    
    public void ToggleMining(WavePacketOrb orb = null)
    {
        if (isMining)
        {
            StopMining();
        }
        else if (orb != null)
        {
            StartMining(orb);
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
            Debug.Log($"[Mining] Successfully started mining orb {orbId} with {crystalComposition.Count} crystal frequencies");
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            Debug.LogError($"[Mining] Failed to start mining: {reason}");

            // Reset mining state on failure
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
            SystemDebug.Log(SystemDebug.Category.Mining,
                $"[Mining] Successfully extracted {requestedFrequencies.Count} frequency types from session {sessionId}");
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            if (reason.Contains("cooldown"))
            {
                // This is expected, just wait for next interval
                SystemDebug.Log(SystemDebug.Category.Mining, $"[Mining] Extraction on cooldown: {reason}");
            }
            else if (reason.Contains("depleted"))
            {
                SystemDebug.Log(SystemDebug.Category.Mining, "[Mining] Orb depleted, stopping mining");
                StopMining();
            }
            else if (reason.Contains("Cannot fulfill"))
            {
                SystemDebug.LogWarning(SystemDebug.Category.Mining, $"[Mining] Request cannot be fulfilled: {reason}");
            }
            else
            {
                SystemDebug.LogError(SystemDebug.Category.Mining, $"[Mining] Failed to extract packets: {reason}");
            }
        }
    }

    // Mining Session Table Event Handlers
    private void HandleMiningSessionCreated(EventContext ctx, MiningSession session)
    {
        // Check if this session belongs to us
        var localIdentity = GameManager.LocalIdentity;
        if (localIdentity.HasValue && session.PlayerIdentity == localIdentity.Value && session.OrbId == currentOrbId)
        {
            currentSessionId = session.SessionId;
            isMining = true;
            miningTimer = 0f;
            extractionTimer = 0f;

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

            OnMiningStateChanged?.Invoke(false);
        }
    }

    
    private void HandleWavePacketExtracted(EventContext ctx, WavePacketExtraction extraction)
    {
        var localPlayer = GameManager.GetLocalPlayer();
        if (localPlayer != null && extraction.PlayerId == localPlayer.PlayerId)
        {
            SystemDebug.Log(SystemDebug.Category.Mining,
                $"[Mining] Extraction created: Packet {extraction.PacketId}, Total: {extraction.TotalCount} from {extraction.SourceType} {extraction.SourceId}");

            // Log composition
            foreach (var sample in extraction.Composition)
            {
                SystemDebug.Log(SystemDebug.Category.Mining,
                    $"  Frequency {sample.Frequency:F2}: {sample.Count} packets");
            }

            // Invoke event with first signature for backwards compatibility
            if (extraction.Composition.Count > 0)
            {
                OnWavePacketExtracted?.Invoke(new WavePacketSignature
                {
                    Frequency = extraction.Composition[0].Frequency,
                    Amplitude = extraction.Composition[0].Amplitude,
                    Phase = extraction.Composition[0].Phase
                });
            }

            // Create visual packet
            CreateVisualPacket(extraction);
        }
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

            SystemDebug.Log(SystemDebug.Category.Mining,
                $"[Mining] Extraction {extraction.PacketId} removed/captured");
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
            var orb = conn.Db.WavePacketOrb.OrbId.Find(extraction.SourceId);
            if (orb != null)
            {
                // Orb still exists - use and cache its position
                sourcePos = GetOrbWorldPosition(orb);
                orbPositionCache[extraction.SourceId] = sourcePos;
                foundSource = true;
            }
            else if (orbPositionCache.TryGetValue(extraction.SourceId, out Vector3 cachedPos))
            {
                // Orb was deleted but we have cached position (happens on depletion)
                sourcePos = cachedPos;
                foundSource = true;
                SystemDebug.Log(SystemDebug.Category.Mining,
                    $"Using cached position for deleted orb {extraction.SourceId}");
            }
            else
            {
                // Orb not found and no cache - this shouldn't happen
                SystemDebug.LogWarning(SystemDebug.Category.Mining,
                    $"Could not find source orb {extraction.SourceId} and no cached position");
            }
        }
        // Add other source types later (circuit, device)

        if (!foundSource)
            return;

        // Get player world position
        Vector3 playerWorldPos = playerTransform.position;

        GameObject packet = null;

        // Use NEW integrated extraction visual controller
        if (extractionVisualController != null)
        {
            // Capture packet ID for lambda closure
            ulong packetId = extraction.PacketId;

            // Create flying packet with trajectory animation and arrival callback
            packet = extractionVisualController.SpawnFlyingPacket(
                extraction.Composition.ToArray(),
                sourcePos,
                playerWorldPos,
                packetSpeed,
                () => {
                    // Callback when packet arrives at player
                    SpawnCaptureEffect(playerWorldPos);
                    conn.Reducers.CaptureExtractedPacketV2(packetId);
                    SystemDebug.Log(SystemDebug.Category.Mining,
                        $"[Mining] Packet {packetId} captured - calling server reducer");
                }
            );

            if (packet != null)
            {
                packet.name = $"WavePacket_{extraction.PacketId}";

                // Add packet ID for tracking
                var flyingPacket = packet.GetComponent<FlyingPacket>();
                if (flyingPacket != null)
                {
                    flyingPacket.packetId = extraction.PacketId;
                }

                SystemDebug.Log(SystemDebug.Category.Mining,
                    $"[WavePacketMiningSystem] Created flying packet {extraction.PacketId} with new renderer");
            }
        }
        else
        {
            SystemDebug.LogWarning(SystemDebug.Category.Mining,
                "[WavePacketMiningSystem] No ExtractionVisualController - packets will not be visualized!");
            return;
        }

        if (packet == null)
            return;

        // Track it
        activePackets[extraction.PacketId] = packet;

        SystemDebug.Log(SystemDebug.Category.Mining,
            $"Packet {extraction.PacketId} now flying from orb to player");
    }
    
    private void ConfigurePacketVisual(GameObject packet, WavePacketSignature signature)
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
    
    private Color SignatureToColor(WavePacketSignature signature)
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
                conn.Reducers.CaptureWavePacket(packetId);
                
                // Spawn particle effect
                SpawnCaptureEffect(packet.transform.position);
                
                // Remove from tracking
                packetMovementCoroutines.Remove(packetId);
                activePackets.Remove(packetId);

                // Notify visualizer to clean up enhanced effects
                var visualizer = GetComponent<WavePacketVisualizer>();
                if (visualizer != null)
                {
                    visualizer.RemovePacketVisual(packetId);
                }
                else
                {
                    // Destroy visual if not using visualizer
                    Destroy(packet);
                }
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

            SystemDebug.Log(SystemDebug.Category.Mining,
                $"[Mining] Packet {packetId} arrived and capture initiated");

            // Visual cleanup happens in HandleWavePacketExtractionRemoved
        }
    }

    private Vector3 GetOrbWorldPosition(WavePacketOrb orb)
    {
        // Convert orb's world coordinates and local position to Unity world position
        var worldManager = FindFirstObjectByType<SYSTEM.Game.WorldManager>();
        if (worldManager != null)
        {
            // Check if WorldManager has the conversion method
            var method = worldManager.GetType().GetMethod("ConvertOrbPositionToUnityWorld");
            if (method != null)
            {
                return (Vector3)method.Invoke(worldManager, new object[] { orb.WorldCoords, orb.Position });
            }
        }

        // Fallback: use orb position directly (assumes same world)
        return new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z);
    }

    #endregion

    #region Utility Methods
    
    private void RequestExtraction()
    {
        if (!isMining || currentOrbId == 0) return;
        
        // Debug.Log("Requesting wave packet extraction...");
        conn.Reducers.ExtractWavePacket();
    }
    
    private bool IsOrbInRange(WavePacketOrb orb)
    {
        if (orb == null)
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
        var orbObj = GameObject.Find($"Orb_{orb.OrbId}");
        if (orbObj != null)
        {
            orbPosition = orbObj.transform.position;
        }
        else
        {
            // Fallback: use database position
            orbPosition = new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z);
            SystemDebug.LogWarning(SystemDebug.Category.Mining,
                $"Could not find GameObject for Orb_{orb.OrbId}, using database position {orbPosition}");
        }

        float distance = Vector3.Distance(playerTransform.position, orbPosition);

        SystemDebug.Log(SystemDebug.Category.Mining,
            $"Range check: Player at {playerTransform.position}, Orb {orb.OrbId} at {orbPosition}, distance={distance:F1}, maxRange={maxMiningRange}");

        return distance <= maxMiningRange;
    }

    private WavePacketOrb FindNearestOrb()
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

        WavePacketOrb nearestOrb = null;
        float nearestDistance = maxMiningRange;
        int orbCount = 0;
        int skippedDepleted = 0;
        int missingGameObjects = 0;

        // Check all orbs in the database
        foreach (var orb in conn.Db.WavePacketOrb.Iter())
        {
            orbCount++;

            // Skip depleted orbs
            if (orb.TotalWavePackets == 0)
            {
                skippedDepleted++;
                continue;
            }

            // Find the GameObject for this orb
            var orbObj = GameObject.Find($"Orb_{orb.OrbId}");
            if (orbObj == null)
            {
                missingGameObjects++;
                Debug.LogWarning($"[Mining] Could not find GameObject 'Orb_{orb.OrbId}' for orb {orb.OrbId}");
                continue;
            }

            // Check distance
            float distance = Vector3.Distance(playerTransform.position, orbObj.transform.position);
            Debug.Log($"[Mining] Orb {orb.OrbId} at position {orbObj.transform.position} - distance: {distance:F1} (max: {maxMiningRange})");

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestOrb = orb;
            }
        }

        Debug.Log($"[Mining] Scanned {orbCount} orbs (skipped {skippedDepleted} depleted, {missingGameObjects} missing GameObjects)");

        if (nearestOrb != null)
        {
            Debug.Log($"[Mining] Found nearest orb {nearestOrb.OrbId} at distance {nearestDistance:F1}");
        }
        else
        {
            Debug.Log($"[Mining] No valid orb found within range {maxMiningRange}");
        }

        return nearestOrb;
    }
    
    public bool CanMineOrb(WavePacketOrb orb)
    {
        if (orb == null) return false;
        
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
}