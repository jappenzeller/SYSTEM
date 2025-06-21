using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Manages player subscriptions with dynamic radius
/// </summary>
public class PlayerSubscriptionController : SubscribableController
{
    public override string GetControllerName() => "PlayerController";
    
    private Dictionary<uint, Player> trackedPlayers = new Dictionary<uint, Player>();
    private Player localPlayer;
    
    void Start()
    {
        SubscriptionOrchestrator.Instance?.RegisterController(this);
    }
    
    public override void Subscribe(WorldCoords worldCoords)
    {
        if (!GameManager.IsConnected()) return;
        
        Unsubscribe();
        
        // Subscribe to all players, filter client-side
        string[] queries = new string[]
        {
            "SELECT * FROM player"
        };
        
        currentSubscription = conn.SubscriptionBuilder()
            .OnApplied((ctx) => 
            {
                OnSubscriptionApplied();
                LoadInitialPlayers();
            })
            .OnError((ctx, error) => OnSubscriptionError(error))
            .Subscribe(queries);
            
        // Setup event handlers
        conn.Db.Player.OnInsert += HandlePlayerInsert;
        conn.Db.Player.OnUpdate += HandlePlayerUpdate;
        conn.Db.Player.OnDelete += HandlePlayerDelete;
    }
    
    public override void Unsubscribe()
    {
        currentSubscription?.Unsubscribe();
        currentSubscription = null;
        
        if (conn != null)
        {
            conn.Db.Player.OnInsert -= HandlePlayerInsert;
            conn.Db.Player.OnUpdate -= HandlePlayerUpdate;
            conn.Db.Player.OnDelete -= HandlePlayerDelete;
        }
        
        trackedPlayers.Clear();
        isSubscribed = false;
    }
    
    void LoadInitialPlayers()
    {
        // Find local player
        foreach (var player in conn.Db.Player.Iter())
        {
            if (player.Identity == conn.Identity)
            {
                localPlayer = player;
                break;
            }
        }
        
        Debug.Log($"[{GetControllerName()}] Initial load - Players: {trackedPlayers.Count}, Local: {localPlayer?.Name ?? "None"}");
    }
    
    void HandlePlayerInsert(EventContext ctx, Player player)
    {
        if (IsInCurrentWorld(player.CurrentWorld))
        {
            trackedPlayers[player.PlayerId] = player;
            
            if (player.Identity == conn.Identity)
            {
                localPlayer = player;
                EventBus.Publish(new LocalPlayerSpawnedEvent { Player = player });
            }
            else
            {
                EventBus.Publish(new RemotePlayerJoinedEvent { Player = player });
            }
        }
    }
    
    void HandlePlayerUpdate(EventContext ctx, Player oldPlayer, Player newPlayer)
    {
        if (newPlayer.Identity == conn.Identity)
        {
            localPlayer = newPlayer;
        }
        
        // Handle world transitions
        if (!WorldCoordsEqual(oldPlayer.CurrentWorld, newPlayer.CurrentWorld))
        {
            if (IsInCurrentWorld(oldPlayer.CurrentWorld))
            {
                // Player left our world
                trackedPlayers.Remove(newPlayer.PlayerId);
                EventBus.Publish(new RemotePlayerLeftEvent { Player = newPlayer });
            }
            else if (IsInCurrentWorld(newPlayer.CurrentWorld))
            {
                // Player entered our world
                trackedPlayers[newPlayer.PlayerId] = newPlayer;
                EventBus.Publish(new RemotePlayerJoinedEvent { Player = newPlayer });
            }
        }
        else if (trackedPlayers.ContainsKey(newPlayer.PlayerId))
        {
            // Player updated within our world
            trackedPlayers[newPlayer.PlayerId] = newPlayer;
            EventBus.Publish(new RemotePlayerUpdatedEvent { OldPlayer = oldPlayer, NewPlayer = newPlayer });
        }
    }
    
    void HandlePlayerDelete(EventContext ctx, Player player)
    {
        if (trackedPlayers.Remove(player.PlayerId))
        {
            EventBus.Publish(new RemotePlayerLeftEvent { Player = player });
        }
    }
    
    bool IsInCurrentWorld(WorldCoords coords)
    {
        var currentCoords = GameData.Instance?.GetCurrentWorldCoords();
        return currentCoords != null && WorldCoordsEqual(coords, currentCoords);
    }
    
    bool WorldCoordsEqual(WorldCoords a, WorldCoords b)
    {
        return a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    }
    
    // Public API
    public Player GetLocalPlayer() => localPlayer;
    public IEnumerable<Player> GetTrackedPlayers() => trackedPlayers.Values;
    public Player GetPlayerById(uint playerId) => trackedPlayers.GetValueOrDefault(playerId);
    public int PlayerCount => trackedPlayers.Count;
}