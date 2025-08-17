using System;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;

/// <summary>
/// Bridge between SpacetimeDB table events and our EventBus
/// This replaces the broken GameManager event handling
/// </summary>
public class SpacetimeDBEventBridge : MonoBehaviour
{
    private DbConnection conn;
    private bool isSubscribed = false;
    
    void Start()
    {
        Debug.Log("[EventBridge] Starting SpacetimeDB event bridge...");
        
        // Wait for connection
        StartCoroutine(WaitForConnection());
    }
    
    System.Collections.IEnumerator WaitForConnection()
    {
        // Wait for GameManager connection
        while (!GameManager.IsConnected())
        {
            yield return new WaitForSeconds(0.1f);
        }
        
        conn = GameManager.Conn;
        Debug.Log("[EventBridge] Connection established, subscribing to table events...");
        
        // Subscribe to table events directly
        SubscribeToTableEvents();
    }
    
    void SubscribeToTableEvents()
    {
        if (isSubscribed) return;
        
        // Player table events
        conn.Db.Player.OnInsert += OnPlayerInsert;
        conn.Db.Player.OnUpdate += OnPlayerUpdate;
        conn.Db.Player.OnDelete += OnPlayerDelete;
        
        // Session events
        conn.Db.SessionResult.OnInsert += OnSessionResultInsert;
        
        isSubscribed = true;
        Debug.Log("[EventBridge] Subscribed to SpacetimeDB table events");
    }
    
    void OnDestroy()
    {
        if (conn != null && isSubscribed)
        {
            conn.Db.Player.OnInsert -= OnPlayerInsert;
            conn.Db.Player.OnUpdate -= OnPlayerUpdate;
            conn.Db.Player.OnDelete -= OnPlayerDelete;
            conn.Db.SessionResult.OnInsert -= OnSessionResultInsert;
        }
    }
    
    // Player Events
    void OnPlayerInsert(EventContext ctx, Player player)
    {
        Debug.Log($"[EventBridge] Player inserted: {player.Name} (Identity: {player.Identity})");
        
        if (player.Identity == conn.Identity)
        {
            Debug.Log($"[EventBridge] This is our new player!");
            
            GameEventBus.Instance.Publish(new LocalPlayerCreatedEvent
            {
                Player = player,
                IsNewPlayer = true
            });
            
            GameEventBus.Instance.Publish(new LocalPlayerReadyEvent
            {
                Player = player
            });
        }
    }
    
    void OnPlayerUpdate(EventContext ctx, Player oldPlayer, Player newPlayer)
    {
        Debug.Log($"[EventBridge] Player updated: {newPlayer.Name} (Old Identity: {oldPlayer.Identity}, New Identity: {newPlayer.Identity})");
        
        // Check if this is a restoration (identity change to ours)
        if (oldPlayer.Identity != conn.Identity && newPlayer.Identity == conn.Identity)
        {
            Debug.Log($"[EventBridge] Player '{newPlayer.Name}' restored to our identity!");
            
            GameEventBus.Instance.Publish(new LocalPlayerRestoredEvent
            {
                Player = newPlayer,
                OldIdentity = oldPlayer.Identity,
                NewIdentity = newPlayer.Identity
            });
            
            GameEventBus.Instance.Publish(new LocalPlayerReadyEvent
            {
                Player = newPlayer
            });
        }
        else if (newPlayer.Identity == conn.Identity)
        {
            // Regular update to our player
            Debug.Log($"[EventBridge] Our player updated");
            
            // Could publish a player updated event if needed
        }
    }
    
    void OnPlayerDelete(EventContext ctx, Player player)
    {
        Debug.Log($"[EventBridge] Player deleted: {player.Name}");
        
        if (player.Identity == conn.Identity)
        {
            Debug.Log($"[EventBridge] Our player was deleted!");
            // Publish player deleted event if needed
        }
    }
    
    void OnSessionResultInsert(EventContext ctx, SessionResult result)
    {
        Debug.Log($"[EventBridge] Session result for identity: {result.Identity}");
        // Already handled by LoginUIController, but we could publish events here too
    }
}