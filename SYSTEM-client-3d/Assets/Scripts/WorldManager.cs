// WorldManager.cs - Cleaned version without energy system
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
    
    [Header("Tunnel System")]
    [SerializeField] private GameObject tunnelPrefab;
    [SerializeField] private Transform tunnelsContainer;
    [SerializeField] private float tunnelVisualScale = 1f;
    
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
    
    // Player tracking
    private Dictionary<uint, GameObject> playerObjects = new Dictionary<uint, GameObject>();
    private GameObject localPlayerObject;
    
    // Tunnel tracking
    private Dictionary<ulong, GameObject> tunnelObjects = new Dictionary<ulong, GameObject>();
    
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
    
    void CreateWorldSurface()
    {
        if (worldSurfaceObject != null) return;
        
        if (worldSurfacePrefab != null)
        {
            worldSurfaceObject = Instantiate(worldSurfacePrefab, Vector3.zero, Quaternion.identity, transform);
            worldSurfaceObject.name = "World Surface";
            worldSurfaceObject.transform.localScale = Vector3.one * worldRadius * 2f;
            
            if (worldMaterial != null)
            {
                var renderer = worldSurfaceObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = worldMaterial;
                }
            }
        }
        else
        {
            // Create a default sphere if no prefab is assigned
            worldSurfaceObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            worldSurfaceObject.name = "World Surface";
            worldSurfaceObject.transform.parent = transform;
            worldSurfaceObject.transform.localPosition = Vector3.zero;
            worldSurfaceObject.transform.localScale = Vector3.one * worldRadius * 2f;
            
            // Remove collider for visual-only sphere
            var collider = worldSurfaceObject.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }
        
        Log("World surface created");
    }
    
    public void LoadWorld(WorldCoords coords)
    {
        if (currentWorldCoords != null && 
            currentWorldCoords.X == coords.X && 
            currentWorldCoords.Y == coords.Y && 
            currentWorldCoords.Z == coords.Z)
        {
            LogDebug("Already in this world");
            return;
        }
        
        Log($"Loading world at ({coords.X}, {coords.Y}, {coords.Z})");
        
        if (loadingIndicator != null) loadingIndicator.SetActive(true);
        
        // Unload previous world
        if (currentWorldCoords != null)
        {
            UnloadCurrentWorld();
        }
        
        currentWorldCoords = coords;
        
        // Update UI
        UpdateWorldUI();
        
        // Load world data from database
        if (GameManager.IsConnected())
        {
            LoadWorldData();
            LoadTunnels();
        }
        
        OnWorldLoaded?.Invoke(coords);
        
        if (loadingIndicator != null) loadingIndicator.SetActive(false);
    }
    
    void UnloadCurrentWorld()
    {
        Log($"Unloading world at ({currentWorldCoords.X}, {currentWorldCoords.Y}, {currentWorldCoords.Z})");
        
        // Clear all players except local
        foreach (var kvp in playerObjects.ToList())
        {
            if (kvp.Value != localPlayerObject)
            {
                Destroy(kvp.Value);
                playerObjects.Remove(kvp.Key);
            }
        }
        
        // Clear tunnels
        foreach (var tunnel in tunnelObjects.Values)
        {
            Destroy(tunnel);
        }
        tunnelObjects.Clear();
        
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
    
    void LoadTunnels()
    {
        if (!GameManager.IsConnected()) return;
        
        var tunnels = GameManager.Conn.Db.Tunnel.Iter();
        foreach (var tunnel in tunnels)
        {
            // Check if tunnel connects to current world
            if ((tunnel.FromWorld.X == currentWorldCoords.X && 
                 tunnel.FromWorld.Y == currentWorldCoords.Y && 
                 tunnel.FromWorld.Z == currentWorldCoords.Z) ||
                (tunnel.ToWorld.X == currentWorldCoords.X && 
                 tunnel.ToWorld.Y == currentWorldCoords.Y && 
                 tunnel.ToWorld.Z == currentWorldCoords.Z))
            {
                CreateTunnelVisual(tunnel);
            }
        }
    }
    
    void CreateTunnelVisual(Tunnel tunnel)
    {
        if (tunnelPrefab == null || tunnelObjects.ContainsKey(tunnel.TunnelId)) return;
        
        GameObject tunnelObj = Instantiate(tunnelPrefab, tunnelsContainer ?? transform);
        tunnelObj.name = $"Tunnel_{tunnel.TunnelId}";
        
        // Position tunnel
        Vector3 position = CalculateTunnelPosition(tunnel);
        tunnelObj.transform.position = position;
        tunnelObj.transform.localScale = Vector3.one * tunnelVisualScale;
        
        // Add tunnel interactor component
        var interactor = tunnelObj.GetComponent<TunnelInteractor>();
        if (interactor == null)
        {
            interactor = tunnelObj.AddComponent<TunnelInteractor>();
        }
        interactor.Initialize(tunnel);
        
        tunnelObjects[tunnel.TunnelId] = tunnelObj;
        
        LogDebug($"Created tunnel visual {tunnel.TunnelId}");
    }
    
    Vector3 CalculateTunnelPosition(Tunnel tunnel)
    {
        // Determine if this is origin or destination
        bool isOrigin = tunnel.FromWorld.X == currentWorldCoords.X &&
                       tunnel.FromWorld.Y == currentWorldCoords.Y &&
                       tunnel.FromWorld.Z == currentWorldCoords.Z;
        
        // Get the direction to the other world
        WorldCoords otherWorld = isOrigin ? tunnel.ToWorld : tunnel.FromWorld;
        Vector3 direction = new Vector3(
            otherWorld.X - currentWorldCoords.X,
            otherWorld.Y - currentWorldCoords.Y,
            otherWorld.Z - currentWorldCoords.Z
        ).normalized;
        
        // Position tunnel on world surface in that direction
        return direction * (worldRadius + 10f);
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
        DespawnPlayer(evt.Player.PlayerId);
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
    
    void SpawnPlayer(Player playerData, bool isLocal)
    {
        if (playerPrefab == null)
        {
            LogError("Player prefab not assigned!");
            return;
        }
        
        // Don't spawn if already exists
        if (playerObjects.ContainsKey(playerData.PlayerId)) return;
        
        GameObject playerObj = Instantiate(playerPrefab, playersContainer ?? transform);
        playerObj.name = $"Player_{playerData.Name}";
        
        // Set position
        Vector3 position = new Vector3(
            playerData.Position.X,
            playerData.Position.Y,
            playerData.Position.Z
        );
        playerObj.transform.position = position;
        
        // Set rotation
        Quaternion rotation = new Quaternion(
            playerData.Rotation.X,
            playerData.Rotation.Y,
            playerData.Rotation.Z,
            playerData.Rotation.W
        );
        playerObj.transform.rotation = rotation;
        
        // Initialize player controller
        var controller = playerObj.GetComponent<PlayerController>();
        if (controller != null)
        {
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
    
    void DespawnPlayer(uint playerId)
    {
        if (playerObjects.TryGetValue(playerId, out GameObject playerObj))
        {
            var controller = playerObj.GetComponent<PlayerController>();
            if (controller != null && controller.PlayerData != null)
            {
                OnPlayerDespawned?.Invoke(controller.PlayerData);
            }
            
            Destroy(playerObj);
            playerObjects.Remove(playerId);
            
            Log($"Player despawned: {playerId}");
        }
    }
    
    void UpdatePlayer(Player playerData)
    {
        if (playerObjects.TryGetValue(playerData.PlayerId, out GameObject playerObj))
        {
            var controller = playerObj.GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.UpdateData(playerData);
            }
        }
    }
    
    // ============================================================================
    // Public API
    // ============================================================================
    
    public float GetWorldRadius() => worldRadius;
    
    public WorldCoords GetCurrentWorldCoords() => currentWorldCoords;
    
    public World GetCurrentWorldData() => currentWorldData;
    
    public GameObject GetLocalPlayer() => localPlayerObject;
    
    public Dictionary<uint, GameObject> GetAllPlayers() => new Dictionary<uint, GameObject>(playerObjects);
    
    public bool IsInWorld(WorldCoords coords)
    {
        return currentWorldCoords != null &&
               currentWorldCoords.X == coords.X &&
               currentWorldCoords.Y == coords.Y &&
               currentWorldCoords.Z == coords.Z;
    }
    
    // ============================================================================
    // Debug & Logging
    // ============================================================================
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 100, 300, 400));
        GUILayout.Label($"World: {currentWorldData?.WorldName ?? "Unknown"}");
        GUILayout.Label($"Coords: ({currentWorldCoords?.X ?? 0}, {currentWorldCoords?.Y ?? 0}, {currentWorldCoords?.Z ?? 0})");
        GUILayout.Label($"Players: {playerObjects.Count}");
        GUILayout.Label($"Tunnels: {tunnelObjects.Count}");
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