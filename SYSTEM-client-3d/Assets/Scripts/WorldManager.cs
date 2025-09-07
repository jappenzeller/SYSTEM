using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;

/// <summary>
/// Manages the current world environment and player GameObject spawning/despawning.
/// UPDATED: Now integrates with PlayerTracker for player data management.
/// WorldManager focuses on GameObject management while PlayerTracker handles data.
/// Event-driven architecture with EventBus integration.
/// Implements ISystemReadiness for proper dependency management.
/// </summary>
public class WorldManager : MonoBehaviour, ISystemReadinessOptional
{
    [Header("World Settings")]
    [SerializeField] private GameObject worldSurfacePrefab;
    [SerializeField] private float worldRadius = 5000f;
    [SerializeField] private Transform circuitSpawnPoint;
    
    [Header("Player Settings")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject localPlayerPrefab;
    
    [Header("World Circuit")]
    [SerializeField] private GameObject worldCircuitPrefab;
    
    [Header("UI References")]
    [SerializeField] private TMPro.TextMeshProUGUI worldNameText;
    [SerializeField] private TMPro.TextMeshProUGUI worldCoordsText;
    [SerializeField] private GameObject loadingIndicator;
    
    [Header("Integration")]
    [SerializeField] private PlayerTracker playerTracker; // NEW: Reference to PlayerTracker
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    
    // State
    private WorldCoords currentWorldCoords;
    private World currentWorldData;
    private GameObject worldSurfaceObject;
    private GameObject worldCircuitObject;
    private GameObject localPlayerObject;
    private Dictionary<ulong, GameObject> playerObjects = new Dictionary<ulong, GameObject>();
    
    // References
    private DbConnection conn;
    
    // Events (preserved for compatibility)
    public static event System.Action<WorldCoords> OnWorldLoaded;
    public static event System.Action<WorldCoords> OnWorldUnloaded;
    public static event System.Action<Player> OnPlayerSpawned;
    public static event System.Action<Player> OnPlayerDespawned;
    
    #region ISystemReadiness Implementation
    
    public string SystemName => "WorldManager";
    public string[] RequiredSystems => new string[] { "GameManager", "GameEventBus" };
    public string[] OptionalSystems => new string[] { "PlayerTracker" };
    public bool IsReady { get; private set; }
    public event Action<string> OnSystemReady;
    public float InitializationTimeout => 15f;
    
    public void OnDependenciesReady()
    {
        Debug.Log("[WorldManager] Dependencies ready, initializing...");
        Initialize();
    }
    
    public void OnInitializationTimeout()
    {
        LogError("WorldManager initialization timed out waiting for dependencies");
    }
    
    public bool IsSystemRequired(string systemName)
    {
        return systemName == "GameManager" || systemName == "GameEventBus";
    }
    
    public void OnOptionalSystemReady(string systemName)
    {
        if (systemName == "PlayerTracker" && playerTracker == null)
        {
            playerTracker = FindFirstObjectByType<PlayerTracker>();
            Log($"PlayerTracker became available: {playerTracker != null}");
        }
    }
    
    #endregion
    
    void Awake()
    {
        // Try to find PlayerTracker if not assigned
        if (playerTracker == null)
        {
            playerTracker = FindFirstObjectByType<PlayerTracker>();
            if (playerTracker == null && showDebugInfo)
            {
                Debug.LogWarning("[WorldManager] PlayerTracker not found in Awake, will wait for system readiness");
            }
        }
    }

    void Start()
    {
        Debug.Log("[WorldManager] Start() called, registering with SystemReadinessManager");
        
        // Register with SystemReadinessManager
        SystemReadinessManager.RegisterSystem(this);
    }
    
    private void Initialize()
    {
        if (!GameManager.IsConnected())
        {
            LogError("GameManager not connected during initialization!");
            enabled = false;
            return;
        }

        conn = GameManager.Conn;
        if (conn == null)
        {
            LogError("GameManager.Conn is null during initialization!");
            enabled = false;
            return;
        }
        
        // Try to find PlayerTracker if not found yet
        if (playerTracker == null)
        {
            playerTracker = FindFirstObjectByType<PlayerTracker>();
            if (playerTracker == null && showDebugInfo)
            {
                Debug.LogWarning("[WorldManager] PlayerTracker not found during initialization, continuing without it");
            }
        }
        
        SubscribeToEvents();

        // Subscribe to EventBus for proper initialization
        GameEventBus.Instance.Subscribe<LocalPlayerReadyEvent>(OnLocalPlayerReadyEvent);
        GameEventBus.Instance.Subscribe<SceneLoadedEvent>(OnSceneLoadedEvent);

        Debug.Log($"[WorldManager] Initialized. GameManager connected: {GameManager.IsConnected()}");
        Debug.Log($"[WorldManager] PlayerTracker found: {playerTracker != null}");
        
        // Mark system as ready
        IsReady = true;
        OnSystemReady?.Invoke(SystemName);
        
        // Publish system ready event
        GameEventBus.Instance.Publish(new SystemReadyEvent
        {
            Timestamp = DateTime.Now,
            SystemName = SystemName,
            IsReady = true
        });
        
        // Pure event-driven: We wait for PlayerTracker to tell us about the local player
        Debug.Log("[WorldManager] Initialization complete, waiting for events...");
    }
    
    void OnDestroy()
    {
        UnsubscribeFromEvents();
        
        // Unsubscribe from EventBus
        if (GameEventBus.Instance != null)
        {
            GameEventBus.Instance.Unsubscribe<LocalPlayerReadyEvent>(OnLocalPlayerReadyEvent);
            GameEventBus.Instance.Unsubscribe<SceneLoadedEvent>(OnSceneLoadedEvent);
        }
    }
    
    void SubscribeToEvents()
    {
        // CHANGED: Now subscribe to PlayerTracker events instead of direct SpacetimeDB events
        if (playerTracker != null)
        {
            playerTracker.OnPlayerJoinedWorld += HandlePlayerJoinedWorld;
            playerTracker.OnPlayerLeftWorld += HandlePlayerLeftWorld;
            playerTracker.OnPlayerUpdated += HandlePlayerUpdated;
            playerTracker.OnLocalPlayerChanged += HandleLocalPlayerChanged;
            
            Log("Subscribed to PlayerTracker events");
        }
        else
        {
            LogError("Cannot subscribe to PlayerTracker events - PlayerTracker is null!");
        }
        
        // Keep world circuit subscriptions (these stay with SpacetimeDB)
        if (conn != null)
        {
            conn.Db.WorldCircuit.OnInsert += HandleWorldCircuitInsert;
            conn.Db.WorldCircuit.OnUpdate += HandleWorldCircuitUpdate;
            conn.Db.WorldCircuit.OnDelete += HandleWorldCircuitDelete;
        }
    }
    
    void UnsubscribeFromEvents()
    {
        // CHANGED: Unsubscribe from PlayerTracker events
        if (playerTracker != null)
        {
            playerTracker.OnPlayerJoinedWorld -= HandlePlayerJoinedWorld;
            playerTracker.OnPlayerLeftWorld -= HandlePlayerLeftWorld;
            playerTracker.OnPlayerUpdated -= HandlePlayerUpdated;
            playerTracker.OnLocalPlayerChanged -= HandleLocalPlayerChanged;
        }
        
        // Unsubscribe from world circuit events
        if (conn != null)
        {
            conn.Db.WorldCircuit.OnInsert -= HandleWorldCircuitInsert;
            conn.Db.WorldCircuit.OnUpdate -= HandleWorldCircuitUpdate;
            conn.Db.WorldCircuit.OnDelete -= HandleWorldCircuitDelete;
        }
    }

    // ============================================================================
    // EventBus Handlers
    // ============================================================================
    
    void OnLocalPlayerReadyEvent(LocalPlayerReadyEvent evt)
    {
        Log($"Local player ready via EventBus: {evt.Player.Name}");
        LoadWorld(evt.Player.CurrentWorld);
    }
    
    void OnSceneLoadedEvent(SceneLoadedEvent evt)
    {
        // Scene loaded - PlayerTracker will handle player discovery and fire events
        // We just wait for OnPlayerJoinedWorld events
        Log("Scene loaded - waiting for PlayerTracker events");
    }

    // ============================================================================
    // NEW: PlayerTracker Event Handlers
    // ============================================================================
    
    void HandlePlayerJoinedWorld(PlayerTracker.PlayerData playerData)
    {
        Debug.Log($"[WorldManager] EVENT: HandlePlayerJoinedWorld - {playerData.Name} (Local: {playerData.IsLocal})");
        
        // Check if we have a world surface to spawn into
        if (worldSurfaceObject == null)
        {
            Debug.Log($"[WorldManager] World surface not ready yet, deferring spawn of {playerData.Name}");
            return;
        }
        
        // Only spawn if player is in our current world
        if (currentWorldCoords == null || !IsInCurrentWorld(playerData.Player))
        {
            Debug.Log($"[WorldManager] Player {playerData.Name} joined different world, not spawning");
            return;
        }
        
        Debug.Log($"[WorldManager] Player {playerData.Name} joined our world, spawning GameObject");
        SpawnPlayer(playerData.Player, playerData.IsLocal);
    }
    
    void HandlePlayerLeftWorld(PlayerTracker.PlayerData playerData)
    {
        Log($"Player {playerData.Name} left world, despawning GameObject");
        DespawnPlayer(playerData.PlayerId);
    }
    
    void HandlePlayerUpdated(PlayerTracker.PlayerData oldData, PlayerTracker.PlayerData newData)
    {
        // Check world transitions
        bool wasInWorld = IsInCurrentWorld(oldData.Player);
        bool isInWorld = IsInCurrentWorld(newData.Player);
        
        if (!wasInWorld && isInWorld)
        {
            // Player entered our world
            Log($"Player {newData.Name} entered our world via update");
            SpawnPlayer(newData.Player, newData.IsLocal);
        }
        else if (wasInWorld && !isInWorld)
        {
            // Player left our world
            Log($"Player {newData.Name} left our world via update");
            DespawnPlayer(newData.PlayerId);
        }
        else if (isInWorld)
        {
            // Update existing player in our world
            UpdatePlayer(newData.Player);
        }
    }
    
    void HandleLocalPlayerChanged(PlayerTracker.PlayerData playerData)
    {
        if (playerData != null)
        {
            // Only log for actual world changes, not position updates
            if (currentWorldCoords == null || !IsSameWorldCoords(playerData.WorldCoords, currentWorldCoords))
            {
                Log($"Local player changed world to: {playerData.Name} at ({playerData.WorldCoords.X},{playerData.WorldCoords.Y},{playerData.WorldCoords.Z})");
                LoadWorld(playerData.WorldCoords);
            }
        }
    }

    // ============================================================================
    // World Management (preserved)
    // ============================================================================

    void CreateWorldSurface()
    {
        if (worldSurfacePrefab != null && worldSurfaceObject == null)
        {
            worldSurfaceObject = Instantiate(worldSurfacePrefab, transform);
            worldSurfaceObject.name = "CenterWorld";

            // Get actual radius from the instantiated world
            CenterWorldController worldController = worldSurfaceObject.GetComponent<CenterWorldController>();
            if (worldController != null)
            {
                worldRadius = worldController.Radius;
            }
            
            Log($"World surface created with radius: {worldRadius}");
        }
        else if (worldSurfacePrefab == null)
        {
            LogError("World surface prefab not assigned!");
        }
    }

    public void LoadWorld(WorldCoords coords)
    {
        if (coords == null)
        {
            LogError("LoadWorld called with null coordinates!");
            return;
        }
        
        // Check if already in this world
        if (currentWorldCoords != null && 
            coords.X == currentWorldCoords.X && 
            coords.Y == currentWorldCoords.Y && 
            coords.Z == currentWorldCoords.Z)
        {
            Log($"Already in world ({coords.X}, {coords.Y}, {coords.Z})");
            return;
        }
        
        Log($"Loading world ({coords.X}, {coords.Y}, {coords.Z})");
        
        // Clean up current world (only if we have one)
        if (currentWorldCoords != null)
        {
            UnloadCurrentWorld();
        }
        
        // Update state
        currentWorldCoords = coords;
        
        // Create world surface FIRST (before spawning anything)
        CreateWorldSurface();
        
        // Load world data
        LoadWorldData();
        
        // PlayerTracker will fire events for existing players
        // We don't need to manually spawn them
        Log("World created - PlayerTracker will handle player discovery");
        
        // Request PlayerTracker to re-send events for existing players now that world is ready
        RequestPlayerTrackerRefresh();
        
        // Notify subscribers
        OnWorldLoaded?.Invoke(coords);

        // Transition to InGame state after world is loaded
        Debug.Log("[WorldManager] World loaded, transitioning to InGame state");
        GameEventBus.Instance.TrySetState(GameEventBus.GameState.InGame);
    }
    
    void UnloadCurrentWorld()
    {
        if (currentWorldCoords != null)
        {
            Log($"Unloading world ({currentWorldCoords.X}, {currentWorldCoords.Y}, {currentWorldCoords.Z})");
        }
        else
        {
            Log("Unloading world (no current world)");
        }
        
        // Clear players
        foreach (var kvp in playerObjects.ToList())
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }
        playerObjects.Clear();
        localPlayerObject = null;
        
        // Clear world surface
        if (worldSurfaceObject != null)
        {
            Destroy(worldSurfaceObject);
            worldSurfaceObject = null;
        }
        
        // Clear world circuit
        if (worldCircuitObject != null)
        {
            Destroy(worldCircuitObject);
            worldCircuitObject = null;
        }
        
        OnWorldUnloaded?.Invoke(currentWorldCoords);
    }
    
    void LoadWorldData()
    {
        if (!GameManager.IsConnected()) return;
        
        var worlds = GameManager.Conn.Db.World.Iter();
        foreach (var world in worlds)
        {
            if (world.WorldCoords.X == currentWorldCoords.X &&
                world.WorldCoords.Y == currentWorldCoords.Y &&
                world.WorldCoords.Z == currentWorldCoords.Z)
            {
                currentWorldData = world;
                UpdateWorldUI();
                break;
            }
        }
    }
    
    void UpdateWorldUI()
    {
        if (worldNameText != null && currentWorldData != null)
        {
            worldNameText.text = currentWorldData.WorldName;
        }
        
        if (worldCoordsText != null && currentWorldCoords != null)
        {
            worldCoordsText.text = $"({currentWorldCoords.X}, {currentWorldCoords.Y}, {currentWorldCoords.Z})";
        }
    }

    // REMOVED: SpawnExistingPlayersInWorld - This hybrid approach is no longer needed
    // WorldManager is now purely event-driven and relies on PlayerTracker events
    
    void RequestPlayerTrackerRefresh()
    {
        // After world is loaded, request PlayerTracker to re-fire events for existing players
        if (playerTracker != null)
        {
            Debug.Log("[WorldManager] Requesting PlayerTracker to refresh player events");
            
            // Get all tracked players and fire spawn events for them
            var allPlayers = playerTracker.GetAllPlayers();
            foreach (var kvp in allPlayers)
            {
                var playerData = kvp.Value;
                if (IsInCurrentWorld(playerData.Player))
                {
                    Debug.Log($"[WorldManager] Requesting spawn for existing player: {playerData.Name}");
                    HandlePlayerJoinedWorld(playerData);
                }
            }
        }
        else
        {
            Debug.LogWarning("[WorldManager] PlayerTracker not available for refresh");
        }
    }
    
    // ============================================================================
    // World Circuit Event Handlers (preserved)
    // ============================================================================

    void HandleWorldCircuitInsert(EventContext ctx, WorldCircuit circuit)
    {
        if (currentWorldCoords == null || !IsSameWorldCoords(circuit.WorldCoords, currentWorldCoords))
            return;

        SpawnWorldCircuit(circuit);
    }

    void HandleWorldCircuitUpdate(EventContext ctx, WorldCircuit oldCircuit, WorldCircuit newCircuit)
    {
        if (currentWorldCoords == null || !IsSameWorldCoords(newCircuit.WorldCoords, currentWorldCoords))
            return;

        // For now, just update the reference
        // Could add visual updates here if needed
    }

    void HandleWorldCircuitDelete(EventContext ctx, WorldCircuit circuit)
    {
        if (currentWorldCoords == null || !IsSameWorldCoords(circuit.WorldCoords, currentWorldCoords))
            return;

        if (worldCircuitObject != null)
        {
            Destroy(worldCircuitObject);
            worldCircuitObject = null;
        }
    }

    void SpawnWorldCircuit(WorldCircuit circuit)
    {
        if (worldCircuitObject == null && worldCircuitPrefab != null)
        {
            worldCircuitObject = Instantiate(worldCircuitPrefab, transform);
            worldCircuitObject.name = "World Circuit";
            
            Vector3 circuitPos = circuitSpawnPoint != null ? 
                circuitSpawnPoint.position : 
                new Vector3(0, 100, 0);
            worldCircuitObject.transform.position = circuitPos;
            
            Log($"World circuit spawned at {circuitPos}");
        }
    }
    
    // ============================================================================
    // Player GameObject Management (preserved with minimal changes)
    // ============================================================================
    
    void SpawnPlayer(Player playerData, bool isLocal)
    {
        if (playerObjects.ContainsKey(playerData.PlayerId))
        {
            Log($"Player {playerData.Name} already spawned");
            return;
        }
        
        GameObject prefab = isLocal ? 
            (localPlayerPrefab != null ? localPlayerPrefab : playerPrefab) : 
            playerPrefab;
            
        if (prefab == null)
        {
            LogError($"No prefab assigned for {(isLocal ? "local" : "remote")} player!");
            return;
        }
        
        // Check if world is ready for WebGL
        if (Application.platform == RuntimePlatform.WebGLPlayer && worldSurfaceObject == null)
        {
            Log($"[WebGL] World surface not ready, deferring spawn for {playerData.Name}");
            StartCoroutine(DeferredSpawnPlayer(playerData, isLocal, prefab));
            return;
        }
        
        // Get saved position from player data
        Vector3 savedPosition = new Vector3(
            playerData.Position.X,
            playerData.Position.Y,
            playerData.Position.Z
        );
        
        Quaternion savedRotation = new Quaternion(
            playerData.Rotation.X,
            playerData.Rotation.Y,
            playerData.Rotation.Z,
            playerData.Rotation.W
        );
        
        // Enhanced position validation
        bool positionTooCloseToOrigin = savedPosition.magnitude < 10f;
        bool isOldDefaultPosition = Mathf.Approximately(savedPosition.x, 0f) && 
                                    Mathf.Approximately(savedPosition.y, 100f) && 
                                    Mathf.Approximately(savedPosition.z, 0f);
        bool isInvalidPosition = positionTooCloseToOrigin || isOldDefaultPosition;
        
        // Check for new server spawn position (should be around Y=3001)
        bool isValidServerSpawn = savedPosition.y > 2900f && savedPosition.y < 3100f;
        
        bool hasSavedPosition = !isInvalidPosition || isValidServerSpawn;
        
        if (isInvalidPosition && !isValidServerSpawn)
        {
            Log($"[SPAWN WARNING] Player {playerData.Name} has invalid position: {savedPosition} (magnitude: {savedPosition.magnitude:F2})");
            Log($"  - Too close to origin: {positionTooCloseToOrigin}");
            Log($"  - Is old default: {isOldDefaultPosition}");
            Log($"  - Will use fallback spawn position");
        }
        
        GameObject playerObj;
        
        if (hasSavedPosition)
        {
            // Use saved position
            Log($"Restoring player {playerData.Name} at saved position: {savedPosition}");
            playerObj = Instantiate(prefab, savedPosition, savedRotation);
            playerObj.name = $"Player_{playerData.Name}";
            
            // Ensure proper alignment with sphere surface
            CenterWorldController worldController = worldSurfaceObject?.GetComponent<CenterWorldController>();
            if (worldController != null)
            {
                // Snap to surface in case of drift
                Vector3 snappedPos = worldController.SnapToSurface(savedPosition, 1.0f);
                playerObj.transform.position = snappedPos;
                
                // Preserve rotation but ensure up vector is correct
                Vector3 up = worldController.GetUpVector(snappedPos);
                Vector3 forward = playerObj.transform.forward;
                forward = Vector3.ProjectOnPlane(forward, up).normalized;
                if (forward.magnitude < 0.1f)
                    forward = Vector3.Cross(up, Vector3.right).normalized;
                
                playerObj.transform.rotation = Quaternion.LookRotation(forward, up);
                
                Log($"Player restored at saved position (snapped): {snappedPos}");
            }
        }
        else
        {
            // No valid position - calculate proper fallback spawn
            Log($"Calculating fallback spawn position for player {playerData.Name}");
            playerObj = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            playerObj.name = $"Player_{playerData.Name}";
            
            // Calculate robust fallback position
            Vector3 fallbackSpawnPos = CalculateFallbackSpawnPosition();
            playerObj.transform.position = fallbackSpawnPos;
            
            // Position player on sphere surface
            CenterWorldController worldController = worldSurfaceObject?.GetComponent<CenterWorldController>();
            if (worldController != null)
            {
                // Ensure we're on the sphere surface
                Vector3 spawnPos = worldController.SnapToSurface(fallbackSpawnPos, 1.0f);
                playerObj.transform.position = spawnPos;
                
                // Align rotation with sphere surface
                Vector3 up = worldController.GetUpVector(spawnPos);
                Vector3 forward = Vector3.Cross(up, Vector3.right).normalized;
                if (forward.magnitude < 0.1f)
                    forward = Vector3.Cross(up, Vector3.forward).normalized;
                
                playerObj.transform.rotation = Quaternion.LookRotation(forward, up);
                
                Log($"Player spawned at fallback position: {spawnPos} (original: {fallbackSpawnPos})");
            }
            else
            {
                // World controller not available yet
                Log($"World controller not ready, using calculated fallback: {fallbackSpawnPos}");
                playerObj.transform.rotation = Quaternion.identity;
            }
        }
        
        // Set up player controller
        var controller = playerObj.GetComponent<PlayerController>();
        if (controller != null)
        {
            // Initialize the player controller with all necessary data
            controller.Initialize(playerData, isLocal, worldRadius);
        }
        else
        {
            LogError($"No PlayerController component found on {playerObj.name}!");
        }
        
        // Track the player object
        playerObjects[playerData.PlayerId] = playerObj;
        
        if (isLocal)
        {
            localPlayerObject = playerObj;
        }
        
        OnPlayerSpawned?.Invoke(playerData);
        Log($"{(isLocal ? "Local" : "Remote")} player {playerData.Name} spawned successfully");
    }
    
    void DespawnPlayer(ulong playerId)
    {
        if (playerObjects.TryGetValue(playerId, out GameObject playerObj))
        {
            if (playerObj == localPlayerObject)
            {
                localPlayerObject = null;
            }
            
            var playerData = GameManager.Conn?.Db.Player.PlayerId.Find(playerId);
            if (playerData != null)
            {
                OnPlayerDespawned?.Invoke(playerData);
            }
            
            Destroy(playerObj);
            playerObjects.Remove(playerId);
            
            Log($"Player {playerId} despawned");
        }
    }
    
    void UpdatePlayer(Player playerData)
    {
        if (!playerObjects.TryGetValue(playerData.PlayerId, out GameObject playerObj))
        {
            // Player not in our world
            return;
        }
        
        // Update position
        Vector3 position = new Vector3(
            playerData.Position.X,
            playerData.Position.Y,
            playerData.Position.Z
        );
        
        Quaternion rotation = new Quaternion(
            playerData.Rotation.X,
            playerData.Rotation.Y,
            playerData.Rotation.Z,
            playerData.Rotation.W
        );
        
        var controller = playerObj.GetComponent<PlayerController>();
        if (controller != null)
        {
            // Use the controller's update method which handles network interpolation
            controller.UpdateData(playerData);
        }
        else
        {
            // Fallback: directly update transform
            playerObj.transform.position = position;
            playerObj.transform.rotation = rotation;
            
            // Ensure proper orientation on sphere
            playerObj.transform.up = position.normalized;
        }
        
        // Log if this is a significant position change (for debugging)
        if (showDebugInfo)
        {
            float distance = Vector3.Distance(playerObj.transform.position, position);
            if (distance > 0.1f)
            {
                Log($"Player {playerData.Name} moved {distance:F2} units");
            }
        }
    }
    
    // ============================================================================
    // Helper Methods (preserved)
    // ============================================================================

    bool IsInCurrentWorld(Player player)
    {
        return currentWorldCoords != null && IsSameWorldCoords(player.CurrentWorld, currentWorldCoords);
    }

    bool IsSameWorldCoords(WorldCoords w1, WorldCoords w2)
    {
        return w1.X == w2.X && w1.Y == w2.Y && w1.Z == w2.Z;
    }
    
    // ============================================================================
    // Public API (preserved for compatibility)
    // ============================================================================
    
    public WorldCoords GetCurrentWorldCoords() => currentWorldCoords;
    public World GetCurrentWorldData() => currentWorldData;
    public float GetWorldRadius() => worldRadius;
    public GameObject GetLocalPlayerObject() => localPlayerObject;
    public Dictionary<ulong, GameObject> GetAllPlayers() => new Dictionary<ulong, GameObject>(playerObjects);
    
    // NEW: Additional API methods for PlayerTracker integration
    public PlayerTracker GetPlayerTracker() => playerTracker;
    public bool HasPlayerTracker() => playerTracker != null;
    
    // ============================================================================
    // Debug (preserved)
    // ============================================================================
    
    void OnGUI()
    {
        // Debug display removed - now handled by WebGLDebugOverlay
        // Set showDebugInfo in inspector if you need detailed WorldManager logs
    }
    
    void Log(string message)
    {
        if (showDebugInfo)
             Debug.Log($"[WorldManager] {message}");
    }
    
    void LogError(string message)
    {
        Debug.LogError($"[WorldManager] {message}");
    }
    
    // ============================================================================
    // Deferred Spawning (for when world surface isn't ready)
    // ============================================================================
    
    IEnumerator DeferredSpawnPlayer(Player playerData, bool isLocal, GameObject prefab)
    {
        Log($"Deferring spawn for {playerData.Name} until world surface is ready");
        
        // Instead of polling, just wait one frame for world initialization
        // The world surface should be created in LoadWorld which happens before player spawning
        yield return null;
        
        // Check if world surface is now available
        if (worldSurfaceObject != null)
        {
            Log($"World surface ready, spawning {playerData.Name}");
        }
        else
        {
            LogError($"World surface still not ready after deferral, creating fallback");
            // Create a basic world surface as fallback
            CreateWorldSurface();
        }
        
        // Re-check if player wasn't spawned while waiting
        if (!playerObjects.ContainsKey(playerData.PlayerId))
        {
            SpawnPlayer(playerData, isLocal);
        }
    }
    
    Vector3 CalculateFallbackSpawnPosition()
    {
        // Use the proper world radius constant (3000)
        const float WORLD_RADIUS = 3000f;
        const float SURFACE_OFFSET = 1f;
        
        // First try to get radius from CenterWorldController if available
        CenterWorldController worldController = worldSurfaceObject?.GetComponent<CenterWorldController>();
        float actualRadius = worldController != null ? worldController.Radius : WORLD_RADIUS;
        
        // Calculate spawn at north pole (positive Y direction)
        Vector3 fallbackPos = new Vector3(0, actualRadius + SURFACE_OFFSET, 0);
        
        Log($"Calculated fallback spawn position: {fallbackPos} (radius: {actualRadius})");
        
        // Add some randomization if multiple players spawn at same time
        if (playerObjects.Count > 0)
        {
            float angle = UnityEngine.Random.Range(0, 360) * Mathf.Deg2Rad;
            float offsetDistance = 5f; // 5 units from north pole
            fallbackPos.x = Mathf.Sin(angle) * offsetDistance;
            fallbackPos.z = Mathf.Cos(angle) * offsetDistance;
            
            // Adjust Y to maintain sphere surface position
            Vector3 direction = fallbackPos.normalized;
            fallbackPos = direction * (actualRadius + SURFACE_OFFSET);
            
            Log($"Adjusted fallback position for multiple players: {fallbackPos}");
        }
        
        return fallbackPos;
    }
}