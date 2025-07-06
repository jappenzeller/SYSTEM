// WorldManager.cs - Fixed version with Tunnel code temporarily commented out
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SpacetimeDB.Types;
using TMPro;

public class WorldManager : MonoBehaviour
{
    [Header("World Configuration")]
    [SerializeField] private float worldRadius = 300f;
    [SerializeField] private GameObject worldSurfacePrefab;
    [SerializeField] private Material worldMaterial;
    [SerializeField] private bool autoCreateWorldOnStart = true;
    
    [Header("Player Management")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform playersContainer;
    [SerializeField] private float playerSpawnHeight = 2f;
    
    [Header("World Circuit")]
    [SerializeField] private GameObject worldCircuitPrefab;
    [SerializeField] private Transform circuitSpawnPoint;
    [SerializeField] private float circuitHeight = 100f;
    

    
    [Header("UI References")]
    [SerializeField] private Canvas worldInfoCanvas;
    [SerializeField] private TextMeshProUGUI worldNameText;
    [SerializeField] private TextMeshProUGUI worldCoordsText;
    [SerializeField] private GameObject loadingIndicator;
    
    [Header("Debug Settings")]
    [SerializeField] private bool showDebugInfo = false;
    [SerializeField] private bool verboseLogging = false;
    
    // World state
    private WorldCoords currentWorldCoords;
    private World currentWorldData;
    private GameObject worldSurfaceObject;
    private GameObject worldCircuitObject;
    
    // Player tracking - FIXED: Changed from uint to ulong
    private Dictionary<ulong, GameObject> playerObjects = new Dictionary<ulong, GameObject>();
    private GameObject localPlayerObject;

    
    // Subscription controllers
    private PlayerSubscriptionController playerController;
    private WorldCircuitSubscriptionController circuitController;
    
    // Events
    public static event Action<WorldCoords> OnWorldLoaded;
    public static event Action<WorldCoords> OnWorldUnloaded;
    public static event Action<Player> OnPlayerSpawned;
    public static event Action<Player> OnPlayerDespawned;
    
    void Awake()
    {
        // Get subscription controllers
        playerController = GetComponent<PlayerSubscriptionController>();
        circuitController = GetComponent<WorldCircuitSubscriptionController>();
        
        if (playerController == null)
        {
            LogWarning("PlayerSubscriptionController not found - adding one");
            playerController = gameObject.AddComponent<PlayerSubscriptionController>();
        }
        
        if (circuitController == null)
        {
            LogWarning("WorldCircuitSubscriptionController not found - adding one");
            circuitController = gameObject.AddComponent<WorldCircuitSubscriptionController>();
        }
    }
    
    void Start()
    {
        if (autoCreateWorldOnStart)
        {
            CreateWorldSurface();
        }
        
        // Subscribe to game events
        GameManager.OnLocalPlayerReady += HandleLocalPlayerReady;
        GameManager.OnWorldChanged += HandleWorldChanged;
        
        // Subscribe to player events
        EventBus.Subscribe<LocalPlayerSpawnedEvent>(OnLocalPlayerSpawned);
        EventBus.Subscribe<RemotePlayerJoinedEvent>(OnRemotePlayerJoined);
        EventBus.Subscribe<RemotePlayerLeftEvent>(OnRemotePlayerLeft);
        EventBus.Subscribe<RemotePlayerUpdatedEvent>(OnRemotePlayerUpdated);
        
        // Subscribe to world circuit events
        EventBus.Subscribe<WorldCircuitSpawnedEvent>(OnWorldCircuitSpawned);
        EventBus.Subscribe<WorldCircuitUpdatedEvent>(OnWorldCircuitUpdated);
        EventBus.Subscribe<WorldCircuitDespawnedEvent>(OnWorldCircuitDespawned);
        
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
        // Unsubscribe from events
        GameManager.OnLocalPlayerReady -= HandleLocalPlayerReady;
        GameManager.OnWorldChanged -= HandleWorldChanged;
        
        EventBus.Unsubscribe<LocalPlayerSpawnedEvent>(OnLocalPlayerSpawned);
        EventBus.Unsubscribe<RemotePlayerJoinedEvent>(OnRemotePlayerJoined);
        EventBus.Unsubscribe<RemotePlayerLeftEvent>(OnRemotePlayerLeft);
        EventBus.Unsubscribe<RemotePlayerUpdatedEvent>(OnRemotePlayerUpdated);
        
        EventBus.Unsubscribe<WorldCircuitSpawnedEvent>(OnWorldCircuitSpawned);
        EventBus.Unsubscribe<WorldCircuitUpdatedEvent>(OnWorldCircuitUpdated);
        EventBus.Unsubscribe<WorldCircuitDespawnedEvent>(OnWorldCircuitDespawned);
    }

    // ============================================================================
    // World Management
    // ============================================================================

    // Also simplify CreateWorldSurface to use the new prefab system
    void CreateWorldSurface()
    {
        if (worldSurfacePrefab != null && worldSurfaceObject == null)
        {
            worldSurfaceObject = Instantiate(worldSurfacePrefab, transform);
            worldSurfaceObject.name = "CenterWorld";
            
            // No need for scaling or material adjustment - prefab handles it
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

        // Load world data
        LoadWorldData();

        // Update UI
        UpdateWorldUI();

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
                playerObjects.Remove(kvp.Key);
            }
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
    
    void OnLocalPlayerSpawned(LocalPlayerSpawnedEvent evt)
    {
        SpawnPlayer(evt.Player, true);
    }
    
    void OnRemotePlayerJoined(RemotePlayerJoinedEvent evt)
    {
        SpawnPlayer(evt.Player, false);
    }
    
    void OnRemotePlayerLeft(RemotePlayerLeftEvent evt)
    {
        DespawnPlayer(evt.Player.PlayerId); // FIXED: No cast needed, PlayerId is already ulong
    }
    
    void OnRemotePlayerUpdated(RemotePlayerUpdatedEvent evt)
    {
        UpdatePlayer(evt.NewPlayer);
    }
    
    void OnWorldCircuitSpawned(WorldCircuitSpawnedEvent evt)
    {
        if (worldCircuitObject == null && worldCircuitPrefab != null)
        {
            worldCircuitObject = Instantiate(worldCircuitPrefab, transform);
            worldCircuitObject.name = "World Circuit";
            
            Vector3 circuitPos = circuitSpawnPoint != null ? 
                circuitSpawnPoint.position : 
                new Vector3(0, circuitHeight, 0);
                
            worldCircuitObject.transform.position = circuitPos;
            
            Log($"World circuit spawned with {evt.Circuit.QubitCount} qubits");
        }
    }
    
    void OnWorldCircuitUpdated(WorldCircuitUpdatedEvent evt)
    {
        LogDebug($"World circuit updated");
    }
    
    void OnWorldCircuitDespawned(WorldCircuitDespawnedEvent evt)
    {
        if (worldCircuitObject != null)
        {
            Destroy(worldCircuitObject);
            worldCircuitObject = null;
            Log("World circuit despawned");
        }
    }

    // ============================================================================
    // Player Management
    // ============================================================================

    // Simplified SpawnPlayer method for WorldManager.cs
    // Replace the existing SpawnPlayer method with this cleaner version

    void SpawnPlayer(Player playerData, bool isLocal)
    {
        if (playerPrefab == null)
        {
            LogError("Player prefab not assigned!");
            return;
        }
        
        // Don't spawn if already exists
        if (playerObjects.ContainsKey(playerData.PlayerId)) return;
        
        // Instantiate player
        GameObject playerObj = Instantiate(playerPrefab, playersContainer ?? transform);
        playerObj.name = $"Player_{playerData.Name}";
        
        // Get spawn system (could be cached in Awake)
        WorldSpawnSystem spawnSystem = GetComponent<WorldSpawnSystem>();
        if (spawnSystem == null)
        {
            spawnSystem = GetComponentInChildren<WorldSpawnSystem>();
        }
        
        if (spawnSystem != null)
        {
            // Use spawn system for proper positioning
            spawnSystem.SetupPlayerSpawn(playerObj, isLocal);
        }
        else
        {
            // Fallback: Simple spawn at north pole
            Debug.LogWarning("[WorldManager] No WorldSpawnSystem found, using fallback spawn");
            
            // Get world radius from CenterWorldController
            CenterWorldController worldController = GetComponentInChildren<CenterWorldController>();
            float radius = worldController != null ? worldController.Radius : 300f;
            
            // Position at north pole
            Vector3 spawnPos = Vector3.up * (radius + 1f);
            playerObj.transform.position = spawnPos;
            playerObj.transform.up = spawnPos.normalized;
            playerObj.transform.forward = Vector3.forward;
        }
        
        // Initialize player controller
        var controller = playerObj.GetComponent<PlayerController>();
        if (controller != null)
        {
            // Get actual world radius
            float worldRadius = 300f; // Default
            CenterWorldController worldCtrl = GetComponentInChildren<CenterWorldController>();
            if (worldCtrl != null)
            {
                worldRadius = worldCtrl.Radius;
            }
            
            controller.Initialize(playerData, isLocal, worldRadius);
        }
        
        // Track player
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
            // FIXED: Changed from UpdateFromServer to UpdateData
            controller.UpdateData(playerData);
        }
        else
        {
            playerObj.transform.position = position;
        }
    }
    
    // FIXED: Changed parameter from uint to ulong
    void DespawnPlayer(ulong playerId)
    {
        if (playerObjects.TryGetValue(playerId, out GameObject playerObj))
        {
            if (playerObj == localPlayerObject)
            {
                localPlayerObject = null;
            }
            
            // FIXED: No cast needed, playerId is already ulong
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
    // Public API
    // ============================================================================
    
    public WorldCoords GetCurrentWorldCoords() => currentWorldCoords;
    public World GetCurrentWorldData() => currentWorldData;
    public float GetWorldRadius() => worldRadius;
    public GameObject GetLocalPlayerObject() => localPlayerObject;
    // FIXED: Return type changed to Dictionary<ulong, GameObject>
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
        Debug.Log($"[WorldManager] {message}");
    }
    
    void LogDebug(string message)
    {
        if (verboseLogging)
        {
            Debug.Log($"[WorldManager] {message}");
        }
    }
    
    void LogWarning(string message)
    {
        Debug.LogWarning($"[WorldManager] {message}");
    }
    
    void LogError(string message)
    {
        Debug.LogError($"[WorldManager] {message}");
    }
}