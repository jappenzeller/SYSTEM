using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;
using SYSTEM.Game;

/// <summary>
/// Dedicated player tracking system for SYSTEM.
/// Handles pure player data tracking, spatial queries, and proximity detection.
/// Separates player data management from GameObject management (handled by WorldManager).
/// </summary>
public class PlayerTracker : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private bool enableProximityTracking = true;
    [SerializeField] private float proximityRadius = 50f;
    [SerializeField] private float proximityUpdateInterval = 0.5f;
    
    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;
    
    // Core player data
    private Dictionary<ulong, PlayerData> trackedPlayers = new Dictionary<ulong, PlayerData>();
    private PlayerData localPlayerData;
    private WorldCoords currentWorldCoords;
    
    // Cached queries for performance
    private List<PlayerData> cachedNearbyPlayers = new List<PlayerData>();
    private float lastProximityUpdate = 0f;
    
    // References
    private DbConnection conn;
    
    // Events - Clean API for other systems to subscribe
    public System.Action<PlayerData> OnPlayerJoinedWorld;
    public System.Action<PlayerData> OnPlayerLeftWorld;
    public System.Action<PlayerData, PlayerData> OnPlayerUpdated; // oldData, newData
    public System.Action<PlayerData> OnLocalPlayerChanged;
    public System.Action<List<PlayerData>> OnNearbyPlayersChanged;
    
    // System readiness
    private bool isInitialized = false;

    #region PlayerData Class
    
    /// <summary>
    /// Structured player data with additional tracking information
    /// </summary>
    public class PlayerData
    {
        public Player Player { get; set; }
        public bool IsLocal { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public float LastUpdateTime { get; set; }
        public bool IsNearby { get; set; }
        public float DistanceFromLocal { get; set; }
        
        // Convenience properties
        public ulong PlayerId => Player.PlayerId;
        public string Name => Player.Name;
        public WorldCoords WorldCoords => Player.CurrentWorld;
        public Identity Identity => Player.Identity;
        
        public PlayerData(Player player, bool isLocal = false)
        {
            Player = player;
            IsLocal = isLocal;
            UpdateFromPlayer(player);
        }
        
        public void UpdateFromPlayer(Player newPlayer)
        {
            Player = newPlayer;
            Position = new Vector3(newPlayer.Position.X, newPlayer.Position.Y, newPlayer.Position.Z);
            Rotation = new Quaternion(newPlayer.Rotation.X, newPlayer.Rotation.Y, newPlayer.Rotation.Z, newPlayer.Rotation.W);
            LastUpdateTime = Time.time;
        }
        
        public float DistanceTo(PlayerData other)
        {
            return Vector3.Distance(Position, other.Position);
        }
        
        public float DistanceTo(Vector3 position)
        {
            return Vector3.Distance(Position, position);
        }
    }
    
    #endregion

    #region Unity Lifecycle
    
    void Start()
    {
        Log("PlayerTracker Start() called, initializing");
        // GameEventBus guaranteed to exist via RuntimeInitializeOnLoadMethod
        Initialize();
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
        
        // Subscribe to EventBus for world changes
        GameEventBus.Instance.Subscribe<LocalPlayerReadyEvent>(OnLocalPlayerReadyEvent);
        GameEventBus.Instance.Subscribe<WorldLoadedEvent>(OnWorldLoadedEvent);
        
        SubscribeToPlayerEvents();
        InitializePlayerTracking();
        
        // Mark as initialized
        isInitialized = true;
        
        Log("PlayerTracker initialized successfully");
    }
    
    void Update()
    {
        if (enableProximityTracking && Time.time >= lastProximityUpdate + proximityUpdateInterval)
        {
            UpdateProximityTracking();
            lastProximityUpdate = Time.time;
        }
    }
    
    void OnDestroy()
    {
        UnsubscribeFromPlayerEvents();
        
        // Unsubscribe from EventBus
        if (GameEventBus.Instance != null)
        {
            GameEventBus.Instance.Unsubscribe<LocalPlayerReadyEvent>(OnLocalPlayerReadyEvent);
            GameEventBus.Instance.Unsubscribe<WorldLoadedEvent>(OnWorldLoadedEvent);
        }
    }
    
    #endregion

    #region EventBus Handlers
    
    void OnLocalPlayerReadyEvent(LocalPlayerReadyEvent evt)
    {
        Log($"Local player ready via EventBus: {evt.Player.Name}");
        currentWorldCoords = evt.Player.CurrentWorld;
        RefreshWorldTracking();
    }
    
    void OnWorldLoadedEvent(WorldLoadedEvent evt)
    {
        Log($"World loaded via EventBus: {evt.World.WorldName}");
        currentWorldCoords = evt.World.WorldCoords;
        RefreshWorldTracking();
    }
    
    #endregion

    #region SpacetimeDB Event Handlers
    
    void SubscribeToPlayerEvents()
    {
        if (conn == null || conn.Db == null)
        {
            LogError("Cannot subscribe to player events - conn or Db is null!");
            return;
        }
        
        conn.Db.Player.OnInsert += HandlePlayerInsert;
        conn.Db.Player.OnUpdate += HandlePlayerUpdate;
        conn.Db.Player.OnDelete += HandlePlayerDelete;
        Log("Subscribed to SpacetimeDB player events");
    }
    
    void UnsubscribeFromPlayerEvents()
    {
        if (conn != null)
        {
            conn.Db.Player.OnInsert -= HandlePlayerInsert;
            conn.Db.Player.OnUpdate -= HandlePlayerUpdate;
            conn.Db.Player.OnDelete -= HandlePlayerDelete;
        }
    }
    
    void HandlePlayerInsert(EventContext ctx, Player player)
    {
        // Only track players in our current world
        if (!IsInCurrentWorld(player)) return;
        
        bool isLocal = player.Identity == conn.Identity;
        var playerData = new PlayerData(player, isLocal);
        
        trackedPlayers[player.PlayerId] = playerData;
        
        if (isLocal)
        {
            localPlayerData = playerData;
            OnLocalPlayerChanged?.Invoke(playerData);
        }
        
        OnPlayerJoinedWorld?.Invoke(playerData);
        Log($"Player {player.Name} joined world (Local: {isLocal})");
    }
    
    void HandlePlayerUpdate(EventContext ctx, Player oldPlayer, Player newPlayer)
    {
        bool wasInWorld = IsInCurrentWorld(oldPlayer);
        bool isInWorld = IsInCurrentWorld(newPlayer);
        bool isLocal = newPlayer.Identity == conn.Identity;
        
        if (!wasInWorld && isInWorld)
        {
            // Player entered our world
            var playerData = new PlayerData(newPlayer, isLocal);
            trackedPlayers[newPlayer.PlayerId] = playerData;
            
            if (isLocal)
            {
                localPlayerData = playerData;
                OnLocalPlayerChanged?.Invoke(playerData);
            }
            
            OnPlayerJoinedWorld?.Invoke(playerData);
            Log($"Player {newPlayer.Name} entered world");
        }
        else if (wasInWorld && !isInWorld)
        {
            // Player left our world
            if (trackedPlayers.TryGetValue(oldPlayer.PlayerId, out PlayerData leavingPlayer))
            {
                trackedPlayers.Remove(oldPlayer.PlayerId);
                
                if (isLocal)
                {
                    localPlayerData = null;
                }
                
                OnPlayerLeftWorld?.Invoke(leavingPlayer);
                Log($"Player {oldPlayer.Name} left world");
            }
        }
        else if (isInWorld && trackedPlayers.TryGetValue(newPlayer.PlayerId, out PlayerData existingPlayer))
        {
            // Update existing player in our world
            var oldPlayerData = new PlayerData(existingPlayer.Player, existingPlayer.IsLocal);
            existingPlayer.UpdateFromPlayer(newPlayer);
            
            if (isLocal)
            {
                localPlayerData = existingPlayer;
                // Only fire OnLocalPlayerChanged for meaningful changes (not just position updates)
                // OnLocalPlayerChanged is meant for player identity/world changes, not position updates
            }
            
            OnPlayerUpdated?.Invoke(oldPlayerData, existingPlayer);
        }
    }
    
    void HandlePlayerDelete(EventContext ctx, Player player)
    {
        if (trackedPlayers.TryGetValue(player.PlayerId, out PlayerData playerData))
        {
            trackedPlayers.Remove(player.PlayerId);
            
            if (playerData.IsLocal)
            {
                localPlayerData = null;
            }
            
            OnPlayerLeftWorld?.Invoke(playerData);
            Log($"Player {player.Name} deleted from tracking");
        }
    }
    
    #endregion

    #region Initialization and World Management
    
    void InitializePlayerTracking()
    {
        if (currentWorldCoords == null)
        {
            // Try to get current world from GameManager
            var localPlayer = GameManager.GetLocalPlayer();
            if (localPlayer != null)
            {
                currentWorldCoords = localPlayer.CurrentWorld;
            }
        }
        
        if (currentWorldCoords != null)
        {
            RefreshWorldTracking();
        }
        else
        {
            Log("No current world coordinates available, waiting for world load event");
        }
    }
    
    void RefreshWorldTracking()
    {
        if (currentWorldCoords == null || !GameManager.IsConnected()) return;
        
        // WebGL safety check
        if (conn == null)
        {
            LogError("RefreshWorldTracking: conn is null!");
            return;
        }
        
        if (conn.Db == null)
        {
            LogError("RefreshWorldTracking: conn.Db is null!");
            return;
        }
        
        trackedPlayers.Clear();
        localPlayerData = null;
        
        Log($"Refreshing player tracking for world ({currentWorldCoords.X}, {currentWorldCoords.Y}, {currentWorldCoords.Z})");
        
        // Track all players in the current world and fire events
        foreach (var player in conn.Db.Player.Iter())
        {
            if (IsInCurrentWorld(player))
            {
                bool isLocal = player.Identity == conn.Identity;
                var playerData = new PlayerData(player, isLocal);
                
                trackedPlayers[player.PlayerId] = playerData;
                
                if (isLocal)
                {
                    localPlayerData = playerData;
                    // Fire local player changed event
                    OnLocalPlayerChanged?.Invoke(playerData);
                }
                
                // CRITICAL: Fire the OnPlayerJoinedWorld event for each player found
                OnPlayerJoinedWorld?.Invoke(playerData);
                
                Log($"Now tracking: {player.Name} (Local: {isLocal}) - Event fired");
            }
        }
        
        Log($"Tracking {trackedPlayers.Count} players in current world - All events fired");
    }
    
    #endregion

    #region Proximity and Spatial Queries
    
    void UpdateProximityTracking()
    {
        if (localPlayerData == null) return;
        
        var previousNearbyPlayers = new List<PlayerData>(cachedNearbyPlayers);
        cachedNearbyPlayers.Clear();
        
        foreach (var playerData in trackedPlayers.Values)
        {
            if (playerData.IsLocal) continue;
            
            float distance = playerData.DistanceTo(localPlayerData.Position);
            playerData.DistanceFromLocal = distance;
            
            bool wasNearby = playerData.IsNearby;
            bool isNearby = distance <= proximityRadius;
            
            playerData.IsNearby = isNearby;
            
            if (isNearby)
            {
                cachedNearbyPlayers.Add(playerData);
            }
        }
        
        // Sort by distance
        cachedNearbyPlayers.Sort((a, b) => a.DistanceFromLocal.CompareTo(b.DistanceFromLocal));
        
        // Check if nearby players changed
        if (!ListsEqual(previousNearbyPlayers, cachedNearbyPlayers))
        {
            OnNearbyPlayersChanged?.Invoke(new List<PlayerData>(cachedNearbyPlayers));
        }
    }
    
    bool ListsEqual(List<PlayerData> a, List<PlayerData> b)
    {
        if (a.Count != b.Count) return false;
        
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].PlayerId != b[i].PlayerId) return false;
        }
        
        return true;
    }
    
    #endregion

    #region Public API
    
    /// <summary>
    /// Get the local player data
    /// </summary>
    public PlayerData GetLocalPlayer() => localPlayerData;
    
    /// <summary>
    /// Get all tracked players in the current world
    /// </summary>
    public IReadOnlyDictionary<ulong, PlayerData> GetAllPlayers() => trackedPlayers;
    
    /// <summary>
    /// Get a specific player by ID
    /// </summary>
    public PlayerData GetPlayer(ulong playerId)
    {
        trackedPlayers.TryGetValue(playerId, out PlayerData player);
        return player;
    }
    
    /// <summary>
    /// Check if a player is being tracked (i.e., in our world)
    /// </summary>
    public bool IsPlayerTracked(ulong playerId) => trackedPlayers.ContainsKey(playerId);
    
    /// <summary>
    /// Get all players within a specific radius of a position
    /// </summary>
    public List<PlayerData> GetPlayersInRadius(Vector3 center, float radius)
    {
        return trackedPlayers.Values
            .Where(p => p.DistanceTo(center) <= radius)
            .OrderBy(p => p.DistanceTo(center))
            .ToList();
    }
    
    /// <summary>
    /// Get nearby players (within proximityRadius of local player)
    /// </summary>
    public List<PlayerData> GetNearbyPlayers() => new List<PlayerData>(cachedNearbyPlayers);
    
    /// <summary>
    /// Get the closest player to a given position
    /// </summary>
    public PlayerData GetClosestPlayer(Vector3 position, bool excludeLocal = true)
    {
        PlayerData closest = null;
        float closestDistance = float.MaxValue;
        
        foreach (var playerData in trackedPlayers.Values)
        {
            if (excludeLocal && playerData.IsLocal) continue;
            
            float distance = playerData.DistanceTo(position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = playerData;
            }
        }
        
        return closest;
    }
    
    /// <summary>
    /// Get the closest player to the local player
    /// </summary>
    public PlayerData GetClosestPlayerToLocal()
    {
        if (localPlayerData == null) return null;
        return GetClosestPlayer(localPlayerData.Position, true);
    }
    
    /// <summary>
    /// Get players sorted by distance from a position
    /// </summary>
    public List<PlayerData> GetPlayersSortedByDistance(Vector3 position, bool excludeLocal = true)
    {
        return trackedPlayers.Values
            .Where(p => !excludeLocal || !p.IsLocal)
            .OrderBy(p => p.DistanceTo(position))
            .ToList();
    }
    
    /// <summary>
    /// Get the number of players in the current world
    /// </summary>
    public int GetPlayerCount() => trackedPlayers.Count;
    
    /// <summary>
    /// Get the number of other players (excluding local player)
    /// </summary>
    public int GetOtherPlayerCount() => localPlayerData != null ? trackedPlayers.Count - 1 : trackedPlayers.Count;
    
    /// <summary>
    /// Get current world coordinates
    /// </summary>
    public WorldCoords GetCurrentWorldCoords() => currentWorldCoords;
    
    #endregion

    #region Helper Methods
    
    bool IsInCurrentWorld(Player player)
    {
        if (currentWorldCoords == null) return false;
        return IsSameWorldCoords(player.CurrentWorld, currentWorldCoords);
    }
    
    bool IsSameWorldCoords(WorldCoords w1, WorldCoords w2)
    {
        return w1.X == w2.X && w1.Y == w2.Y && w1.Z == w2.Z;
    }
    
    #endregion

    #region Logging and Debug
    
    void Log(string message)
    {
        if (debugLogging)
            Debug.Log($"[PlayerTracker] {message}");
    }
    
    void LogError(string message)
    {
        Debug.LogError($"[PlayerTracker] {message}");
    }
    
    void OnGUI()
    {
        // Debug display removed - now handled by WebGLDebugOverlay
        // Set showDebugInfo in inspector if you need detailed PlayerTracker logs
    }
    
    #endregion
}