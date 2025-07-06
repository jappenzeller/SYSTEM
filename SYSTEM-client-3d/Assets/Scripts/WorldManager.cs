using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;

/// <summary>
/// Manages the current world environment and player spawning/despawning.
/// Now subscribes directly to SpaceTimeDB events instead of using EventBus.
/// </summary>
public class WorldManager : MonoBehaviour
{
    [Header("World Settings")]
    [SerializeField] private GameObject worldSurfacePrefab;
    [SerializeField] private float worldRadius = 5000f;
    [SerializeField] private Transform circuitSpawnPoint;
    
    [Header("Player Settings")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject localPlayerPrefab;
    [SerializeField] private float playerSpawnHeight = 1f;
    
    [Header("World Circuit")]
    [SerializeField] private GameObject worldCircuitPrefab;
    
    [Header("UI References")]
    [SerializeField] private TMPro.TextMeshProUGUI worldNameText;
    [SerializeField] private TMPro.TextMeshProUGUI worldCoordsText;
    [SerializeField] private GameObject loadingIndicator;
    
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
    private PlayerSubscriptionController playerSubscription;
    
    // Events
    public static event System.Action<WorldCoords> OnWorldLoaded;
    public static event System.Action<WorldCoords> OnWorldUnloaded;
    public static event System.Action<Player> OnPlayerSpawned;
    public static event System.Action<Player> OnPlayerDespawned;
    
    void Awake()
    {
        // Find player subscription controller
        playerSubscription = GetComponent<PlayerSubscriptionController>();
        if (playerSubscription == null)
        {
            playerSubscription = gameObject.AddComponent<PlayerSubscriptionController>();
        }
    }
    
    void Start()
    {
        if (!GameManager.IsConnected())
        {
            Log("Waiting for connection...");
            enabled = false;
            return;
        }
        
        conn = GameManager.Conn;
        SubscribeToEvents();
        
        // Initialize if we already have a current world
        if (GameData.Instance != null)
        {
            var coords = GameData.Instance.GetCurrentWorldCoords();
            if (coords != null)
            {
                LoadWorld(coords);
            }
        }
    }
    
    void OnDestroy()
    {
        UnsubscribeFromEvents();
    }
    
    void SubscribeToEvents()
    {
        // Subscribe to game events
        GameManager.OnLocalPlayerReady += HandleLocalPlayerReady;
        GameManager.OnWorldChanged += HandleWorldChanged;
        
        // Subscribe to SpaceTimeDB player events
        conn.Db.Player.OnInsert += HandlePlayerInsert;
        conn.Db.Player.OnUpdate += HandlePlayerUpdate;
        conn.Db.Player.OnDelete += HandlePlayerDelete;
        
        // Subscribe to world circuit events
        conn.Db.WorldCircuit.OnInsert += HandleWorldCircuitInsert;
        conn.Db.WorldCircuit.OnUpdate += HandleWorldCircuitUpdate;
        conn.Db.WorldCircuit.OnDelete += HandleWorldCircuitDelete;
    }
    
    void UnsubscribeFromEvents()
    {
        // Unsubscribe from game events
        GameManager.OnLocalPlayerReady -= HandleLocalPlayerReady;
        GameManager.OnWorldChanged -= HandleWorldChanged;
        
        // Unsubscribe from SpaceTimeDB events
        if (conn != null)
        {
            conn.Db.Player.OnInsert -= HandlePlayerInsert;
            conn.Db.Player.OnUpdate -= HandlePlayerUpdate;
            conn.Db.Player.OnDelete -= HandlePlayerDelete;
            
            conn.Db.WorldCircuit.OnInsert -= HandleWorldCircuitInsert;
            conn.Db.WorldCircuit.OnUpdate -= HandleWorldCircuitUpdate;
            conn.Db.WorldCircuit.OnDelete -= HandleWorldCircuitDelete;
        }
    }

    // ============================================================================
    // World Management
    // ============================================================================

    void CreateWorldSurface()
    {
        if (worldSurfacePrefab != null && worldSurfaceObject == null)
        {
            worldSurfaceObject = Instantiate(worldSurfacePrefab, transform);
            worldSurfaceObject.name = "CenterWorld";
            
            Log("World surface created from prefab");
            
            // Get actual radius from the instantiated world
            CenterWorldController worldController = worldSurfaceObject.GetComponent<CenterWorldController>();
            if (worldController != null)
            {
                worldRadius = worldController.Radius;
                Log($"World radius set to {worldRadius} from CenterWorldController");
            }
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
            LogError("Cannot load world - coords are null");
            return;
        }

        Log($"Loading world ({coords.X}, {coords.Y}, {coords.Z})");

        // Clean up previous world
        if (currentWorldCoords != null)
        {
            UnloadCurrentWorld();
        }

        currentWorldCoords = coords;

        // Show loading indicator
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(true);
        }

        // Create world surface
        CreateWorldSurface();

        // Load world data
        LoadWorldData();

        // Update UI
        UpdateWorldUI();

        // Spawn existing players in this world
        SpawnExistingPlayersInWorld();

        // Hide loading indicator
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(false);
        }

        OnWorldLoaded?.Invoke(coords);
    }
    
    void UnloadCurrentWorld()
    {
        Log($"Unloading world ({currentWorldCoords.X}, {currentWorldCoords.Y}, {currentWorldCoords.Z})");
        
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

    void SpawnExistingPlayersInWorld()
    {
        if (playerSubscription == null || currentWorldCoords == null) return;

        // Spawn all tracked players (already filtered by world)
        foreach (var player in playerSubscription.TrackedPlayers.Values)
        {
            if (!playerObjects.ContainsKey(player.PlayerId))
            {
                bool isLocal = player.Identity == conn.Identity;
                SpawnPlayer(player, isLocal);
            }
        }
    }
    
    // ============================================================================
    // Event Handlers
    // ============================================================================
    
    void HandleLocalPlayerReady(Player player)
    {
        Log($"Local player ready: {player.Name}");
        LoadWorld(player.CurrentWorld);
    }
    
    void HandleWorldChanged(WorldCoords newCoords)
    {
        Log($"World changed to ({newCoords.X}, {newCoords.Y}, {newCoords.Z})");
        LoadWorld(newCoords);
    }
    
    // ============================================================================
    // Player Event Handlers (from SpaceTimeDB)
    // ============================================================================

    void HandlePlayerInsert(EventContext ctx, Player player)
    {
        // Only handle players in our current world
        if (currentWorldCoords == null || !IsInCurrentWorld(player)) return;

        bool isLocal = player.Identity == conn.Identity;
        SpawnPlayer(player, isLocal);
    }

    void HandlePlayerUpdate(EventContext ctx, Player oldPlayer, Player newPlayer)
    {
        bool wasInWorld = IsInCurrentWorld(oldPlayer);
        bool isInWorld = IsInCurrentWorld(newPlayer);

        if (!wasInWorld && isInWorld)
        {
            // Player entered our world
            bool isLocal = newPlayer.Identity == conn.Identity;
            SpawnPlayer(newPlayer, isLocal);
        }
        else if (wasInWorld && !isInWorld)
        {
            // Player left our world
            DespawnPlayer(oldPlayer.PlayerId);
        }
        else if (isInWorld)
        {
            // Update existing player
            UpdatePlayer(newPlayer);
        }
    }

    void HandlePlayerDelete(EventContext ctx, Player player)
    {
        DespawnPlayer(player.PlayerId);
    }

    // ============================================================================
    // World Circuit Event Handlers
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
    // Player Spawning
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
        
        Vector3 position = new Vector3(
            playerData.Position.X,
            playerData.Position.Y,
            playerData.Position.Z
        );
        
        GameObject playerObj = Instantiate(prefab, position, Quaternion.identity);
        playerObj.name = $"Player_{playerData.Name}";
        
        // Set up player controller
        var controller = playerObj.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.SetPlayerData(playerData);
            if (isLocal)
            {
                controller.SetAsLocalPlayer();
            }
        }
        
        // Track the player object
        playerObjects[playerData.PlayerId] = playerObj;
        
        if (isLocal)
        {
            localPlayerObject = playerObj;
        }
        
        OnPlayerSpawned?.Invoke(playerData);
        Log($"{(isLocal ? "Local" : "Remote")} player spawned: {playerData.Name}");
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
        
        var controller = playerObj.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.UpdateData(playerData);
        }
        else
        {
            playerObj.transform.position = position;
        }
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
    
    // ============================================================================
    // Helper Methods
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
    // Public API
    // ============================================================================
    
    public WorldCoords GetCurrentWorldCoords() => currentWorldCoords;
    public World GetCurrentWorldData() => currentWorldData;
    public float GetWorldRadius() => worldRadius;
    public GameObject GetLocalPlayerObject() => localPlayerObject;
    public Dictionary<ulong, GameObject> GetAllPlayers() => new Dictionary<ulong, GameObject>(playerObjects);
    
    // ============================================================================
    // Debug
    // ============================================================================
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 200, 300, 200));
        GUILayout.Box("World Manager Debug");
        GUILayout.Label($"World: {currentWorldData?.WorldName ?? "Unknown"}");
        GUILayout.Label($"Coords: ({currentWorldCoords?.X ?? 0}, {currentWorldCoords?.Y ?? 0}, {currentWorldCoords?.Z ?? 0})");
        GUILayout.Label($"Players: {playerObjects.Count}");
        GUILayout.Label($"Circuit: {(worldCircuitObject != null ? "Active" : "None")}");
        GUILayout.EndArea();
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
}