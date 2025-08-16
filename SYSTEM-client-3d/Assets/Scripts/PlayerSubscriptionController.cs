using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;

/// <summary>
/// Manages player subscriptions and tracks players in the same world.
/// No longer publishes EventBus events - consumers should subscribe directly to SpaceTimeDB.
/// </summary>
public class PlayerSubscriptionController : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;
    
    // Cached references
    private DbConnection conn;
    private Dictionary<uint, Player> trackedPlayers = new Dictionary<uint, Player>();
    private Player localPlayer;
    
    // Properties
    public Player LocalPlayer => localPlayer;
    public IReadOnlyDictionary<uint, Player> TrackedPlayers => trackedPlayers;
    
    void Start()
    {
        if (!GameManager.IsConnected())
        {
            LogError("GameManager not connected!");
            enabled = false;
            return;
        }
        
        conn = GameManager.Conn;
        SubscribeToPlayerEvents();
        InitializePlayers();
    }
    
    void OnDestroy()
    {
        UnsubscribeFromPlayerEvents();
    }
    
    void SubscribeToPlayerEvents()
    {
        // Subscribe to SpaceTimeDB player events
        conn.Db.Player.OnInsert += HandlePlayerInsert;
        conn.Db.Player.OnUpdate += HandlePlayerUpdate;
        conn.Db.Player.OnDelete += HandlePlayerDelete;
        
        Log("Subscribed to player events");
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
    
    void InitializePlayers()
    {
        trackedPlayers.Clear();
        // Find local player
        foreach (var player in conn.Db.Player.Iter())
        {
            if (player.Identity == conn.Identity)
            {
                localPlayer = player;
                break;
            }
        }
        
        Log($"Local player found: {(localPlayer != null ? localPlayer.Name : "Not Found")}");
        
        // Track all players in the same world
        if (localPlayer != null)
        {
            foreach (var player in conn.Db.Player.Iter())
            {
                if (IsInSameWorld(player, localPlayer))
                {
                    trackedPlayers[(uint)player.PlayerId] = player;
                    Log($"Tracking player: {player.Name} (ID: {player.PlayerId})");
                }
            }
        }
    }
    
    void HandlePlayerInsert(EventContext ctx, Player player)
    {
        // Update local player reference if this is us
        if (player.Identity == conn.Identity)
        {
            localPlayer = player;
            Log($"Local player set: {player.Name}");
        }
        
        // Track player if they're in our world
        if (localPlayer != null && IsInSameWorld(player, localPlayer))
        {
            trackedPlayers[(uint)player.PlayerId] = player;
            Log($"Player joined our world: {player.Name} (ID: {player.PlayerId})");
        }
    }
    
    void HandlePlayerUpdate(EventContext ctx, Player oldPlayer, Player newPlayer)
    {
        // Update local player reference if needed
        if (newPlayer.Identity == conn.Identity)
        {
            localPlayer = newPlayer;
        }
        
        // Handle world transitions
        bool wasInOurWorld = localPlayer != null && IsInSameWorld(oldPlayer, localPlayer);
        bool isInOurWorld = localPlayer != null && IsInSameWorld(newPlayer, localPlayer);
        
        if (wasInOurWorld && !isInOurWorld)
        {
            // Player left our world
            trackedPlayers.Remove((uint)oldPlayer.PlayerId);
            Log($"Player left our world: {oldPlayer.Name}");
        }
        else if (!wasInOurWorld && isInOurWorld)
        {
            // Player entered our world
            trackedPlayers[(uint)newPlayer.PlayerId] = newPlayer;
            Log($"Player entered our world: {newPlayer.Name}");
        }
        else if (isInOurWorld)
        {
            // Update tracked player data
            trackedPlayers[(uint)newPlayer.PlayerId] = newPlayer;
        }
    }
    
    void HandlePlayerDelete(EventContext ctx, Player player)
    {
        // Clear local player if it was us
        if (player.Identity == conn.Identity)
        {
            localPlayer = null;
            Log("Local player deleted");
        }
        
        // Remove from tracked players
        if (trackedPlayers.ContainsKey((uint)player.PlayerId))
        {
            trackedPlayers.Remove((uint)player.PlayerId);
            Log($"Player removed: {player.Name} (ID: {player.PlayerId})");
        }
    }
    
    bool IsInSameWorld(Player player1, Player player2)
    {
        return IsSameWorldCoords(player1.CurrentWorld, player2.CurrentWorld);
    }
    
    bool IsSameWorldCoords(WorldCoords w1, WorldCoords w2)
    {
        return w1.X == w2.X && w1.Y == w2.Y && w1.Z == w2.Z;
    }
    
    public Dictionary<uint, Player> GetTrackedPlayers() => new Dictionary<uint, Player>(trackedPlayers);
    public Player GetLocalPlayer() => localPlayer;
    
    /// <summary>
    /// Check if a specific player is in our world
    /// </summary>
    public bool IsPlayerInOurWorld(ulong playerId)
    {
        return trackedPlayers.ContainsKey((uint)playerId);
    }
    
    /// <summary>
    /// Get a specific tracked player by ID
    /// </summary>
    public Player GetTrackedPlayer(ulong playerId)
    {
        trackedPlayers.TryGetValue((uint)playerId, out Player player);
        return player;
    }
    
    #region Logging
    
    void Log(string message)
    {
        if (debugLogging)
            Debug.Log($"[PlayerSubscription] {message}");
    }
    
    void LogError(string message)
    {
        Debug.LogError($"[PlayerSubscription] {message}");
    }
    
    string GetControllerName()
    {
        return gameObject.name;
    }
    
    #endregion
}