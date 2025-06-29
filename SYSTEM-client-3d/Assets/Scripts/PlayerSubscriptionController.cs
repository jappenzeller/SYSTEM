using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;
using SpacetimeDB.ClientApi;

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
            .OnApplied(() => 
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
        
         Debug.Log($"[{GetControllerName()}] Initial load - Local: {(localPlayer != null ? localPlayer.Name : "Not Found")}");
        
        // Process all players in the same world
        if (localPlayer != null)
        {
            foreach (var player in conn.Db.Player.Iter())
            {
                if (IsInSameWorld(player, localPlayer))
                {
                    trackedPlayers[(uint)player.PlayerId] = player;
                    
                    if (player.PlayerId == localPlayer.PlayerId)
                    {
                        EventBus.Publish(new LocalPlayerSpawnedEvent { Player = player });
                    }
                    else
                    {
                        EventBus.Publish(new RemotePlayerJoinedEvent { Player = player });
                    }
                }
            }
        }
    }
    
    void HandlePlayerInsert(EventContext ctx, Player player)
    {
        if (localPlayer == null || !IsInSameWorld(player, localPlayer))
            return;
            
        trackedPlayers[(uint)player.PlayerId] = player;
        
        if (player.Identity == conn.Identity)
        {
            localPlayer = player;
            EventBus.Publish(new LocalPlayerSpawnedEvent { Player = player });
        }
        else
        {
            EventBus.Publish(new RemotePlayerJoinedEvent { Player = player });
        }
        
        Debug.Log($"[{GetControllerName()}] Player joined: {player.Name} (ID: {player.PlayerId})");
    }
    
    void HandlePlayerUpdate(EventContext ctx, Player oldPlayer, Player newPlayer)
    {
        // Check if player moved worlds
        if (!IsSameWorldCoords(oldPlayer.CurrentWorld, newPlayer.CurrentWorld))
        {
            if (localPlayer != null && IsInSameWorld(oldPlayer, localPlayer))
            {
                // Player left our world
                trackedPlayers.Remove((uint)oldPlayer.PlayerId);
                EventBus.Publish(new RemotePlayerLeftEvent { Player = oldPlayer });
            }
            else if (localPlayer != null && IsInSameWorld(newPlayer, localPlayer))
            {
                // Player entered our world
                trackedPlayers[(uint)newPlayer.PlayerId] = newPlayer;
                EventBus.Publish(new RemotePlayerJoinedEvent { Player = newPlayer });
            }
        }
        else if (trackedPlayers.ContainsKey((uint)newPlayer.PlayerId))
        {
            // Update tracked player
            trackedPlayers[(uint)newPlayer.PlayerId] = newPlayer;
            EventBus.Publish(new RemotePlayerUpdatedEvent 
            { 
                OldPlayer = oldPlayer, 
                NewPlayer = newPlayer 
            });
        }
        
        // Update local player reference if needed
        if (newPlayer.Identity == conn.Identity)
        {
            localPlayer = newPlayer;
        }
    }
    
    void HandlePlayerDelete(EventContext ctx, Player player)
    {
        if (trackedPlayers.ContainsKey((uint)player.PlayerId))
        {
            trackedPlayers.Remove((uint)player.PlayerId);
            EventBus.Publish(new RemotePlayerLeftEvent { Player = player });
            
            Debug.Log($"[{GetControllerName()}] Player left: {player.Name} (ID: {player.PlayerId})");
        }
        
        if (player.Identity == conn.Identity)
        {
            localPlayer = null;
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
}