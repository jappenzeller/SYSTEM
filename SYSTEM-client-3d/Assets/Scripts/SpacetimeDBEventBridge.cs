using System;
using System.Collections;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;

/// <summary>
/// Bridge between SpacetimeDB and EventBus
/// Handles ALL SpacetimeDB interactions including subscriptions
/// </summary>
public class SpacetimeDBEventBridge : MonoBehaviour
{
    private DbConnection conn;
    private bool isSubscribed = false;
    private bool hasCheckedForPlayer = false;
    
    void Start()
    {
        Debug.Log("[EventBridge] Starting SpacetimeDB event bridge...");
        
        // Subscribe to connection events from GameManager
        GameManager.OnConnected += HandleConnected;
        GameManager.OnDisconnected += HandleDisconnected;
        GameManager.OnConnectionError += HandleConnectionError;
        
        // Check if already connected
        if (GameManager.IsConnected())
        {
            conn = GameManager.Conn;
            HandleConnected();
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe from GameManager events
        GameManager.OnConnected -= HandleConnected;
        GameManager.OnDisconnected -= HandleDisconnected;
        GameManager.OnConnectionError -= HandleConnectionError;
        
        UnsubscribeFromTableEvents();
    }
    
    #region Connection Handling
    
    void HandleConnected()
    {
        Debug.Log("[EventBridge] GameManager connected");
        conn = GameManager.Conn;
        
        // Force state transition if needed
        if (GameEventBus.Instance.CurrentState == GameEventBus.GameState.Disconnected)
        {
            Debug.LogWarning("[EventBridge] State was still Disconnected after connection - forcing to Connected");
            GameEventBus.Instance.ForceSetState(GameEventBus.GameState.Connected);
        }
        
        // Publish connection established event
        if (conn.Identity.HasValue)
        {
            GameEventBus.Instance.Publish(new ConnectionEstablishedEvent
            {
                Identity = conn.Identity.Value,
                Token = AuthToken.LoadToken()
            });
        }
        else
        {
            Debug.LogError("[EventBridge] Connected but no identity!");
        }
        
        // Subscribe to all tables
        Debug.Log("[EventBridge] Subscribing to all tables...");
        conn.SubscriptionBuilder()
            .OnApplied(HandleSubscriptionApplied)
            .OnError(HandleSubscriptionError)
            .SubscribeToAllTables();
    }
    
    void HandleDisconnected()
    {
        Debug.Log("[EventBridge] GameManager disconnected");
        UnsubscribeFromTableEvents();
        hasCheckedForPlayer = false;
        
        // Publish connection lost event
        GameEventBus.Instance.Publish(new ConnectionLostEvent
        {
            Reason = "Connection to server lost"
        });
    }
    
    void HandleConnectionError(string error)
    {
        Debug.LogError($"[EventBridge] Connection error: {error}");
        
        // Publish connection failed event
        GameEventBus.Instance.Publish(new ConnectionFailedEvent
        {
            Error = error
        });
    }
    
    #endregion
    
    #region Subscription Handling
    
    void HandleSubscriptionApplied(SubscriptionEventContext ctx)
    {
        Debug.Log("[EventBridge] Table subscriptions applied successfully");
        
        // Subscribe to table events
        SubscribeToTableEvents();
        
        // Publish subscription ready event
        GameEventBus.Instance.Publish(new SubscriptionReadyEvent());
        
        // Check for local player
        CheckLocalPlayer();
    }
    
    void HandleSubscriptionError(ErrorContext ctx, Exception error)
    {
        Debug.LogError($"[EventBridge] Subscription error: {error.Message}");
        
        GameEventBus.Instance.Publish(new SubscriptionErrorEvent
        {
            Error = error.Message
        });
    }
    
    void CheckLocalPlayer()
    {
        if (hasCheckedForPlayer) return;
        hasCheckedForPlayer = true;
        
        Debug.Log("[EventBridge] Checking for local player...");
        
        var localPlayer = GetLocalPlayer();
        if (localPlayer != null)
        {
            Debug.Log($"[EventBridge] Found existing player: {localPlayer.Name}");
            
            // Publish player ready event
            GameEventBus.Instance.Publish(new LocalPlayerReadyEvent
            {
                Player = localPlayer
            });
        }
        else
        {
            Debug.Log("[EventBridge] No local player found");
            
            // Publish player not found event
            GameEventBus.Instance.Publish(new LocalPlayerNotFoundEvent());
        }
    }
    
    Player GetLocalPlayer()
    {
        if (conn == null || !conn.Identity.HasValue) return null;
        
        foreach (var player in conn.Db.Player.Iter())
        {
            if (player.Identity == conn.Identity)
            {
                return player;
            }
        }
        return null;
    }
    
    #endregion
    
    #region Table Event Subscription
    
    void SubscribeToTableEvents()
    {
        if (isSubscribed || conn == null) return;
        
        // Player table events
        conn.Db.Player.OnInsert += OnPlayerInsert;
        conn.Db.Player.OnUpdate += OnPlayerUpdate;
        conn.Db.Player.OnDelete += OnPlayerDelete;
        
        // Session events
        conn.Db.SessionResult.OnInsert += OnSessionResultInsert;
        
        // World events
        conn.Db.World.OnInsert += OnWorldInsert;
        conn.Db.World.OnUpdate += OnWorldUpdate;
        
        // Reducer response events
        conn.Reducers.OnCreatePlayer += OnCreatePlayerResponse;
        conn.Reducers.OnLoginWithSession += OnLoginWithSessionResponse;
        
        isSubscribed = true;
        Debug.Log("[EventBridge] Subscribed to SpacetimeDB events");
    }
    
    void UnsubscribeFromTableEvents()
    {
        if (!isSubscribed || conn == null) return;
        
        conn.Db.Player.OnInsert -= OnPlayerInsert;
        conn.Db.Player.OnUpdate -= OnPlayerUpdate;
        conn.Db.Player.OnDelete -= OnPlayerDelete;
        conn.Db.SessionResult.OnInsert -= OnSessionResultInsert;
        conn.Db.World.OnInsert -= OnWorldInsert;
        conn.Db.World.OnUpdate -= OnWorldUpdate;
        conn.Reducers.OnCreatePlayer -= OnCreatePlayerResponse;
        conn.Reducers.OnLoginWithSession -= OnLoginWithSessionResponse;
        
        isSubscribed = false;
    }
    
    #endregion
    
    #region Player Events
    
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
            
            // Player is ready immediately after creation
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
            
            // Player is ready after restoration
            GameEventBus.Instance.Publish(new LocalPlayerReadyEvent
            {
                Player = newPlayer
            });
        }
    }
    
    void OnPlayerDelete(EventContext ctx, Player player)
    {
        Debug.Log($"[EventBridge] Player deleted: {player.Name}");
        
        if (player.Identity == conn.Identity)
        {
            Debug.Log($"[EventBridge] Our player was deleted!");
            // This is a critical error - handle appropriately
            GameEventBus.Instance.Publish(new ConnectionLostEvent
            {
                Reason = "Player was deleted from server"
            });
        }
    }
    
    #endregion
    
    #region Session Events
    
    void OnSessionResultInsert(EventContext ctx, SessionResult result)
    {
        Debug.Log($"[EventBridge] Session result for identity: {result.Identity}");
        
        // Check if this is our session
        if (result.Identity == conn.Identity)
        {
            Debug.Log($"[EventBridge] This is our session!");
            
            // Check if we have a stored username (LoginUIController should have saved it)
            string username = AuthToken.LoadUsername();
            if (string.IsNullOrEmpty(username))
            {
                Debug.LogWarning("[EventBridge] Session created but no username stored");
                return;
            }
            
            // Save the session
            AuthToken.SaveSession(result.SessionToken, username);
            
            // SessionResult creation means login was successful
            GameEventBus.Instance.Publish(new LoginSuccessfulEvent
            {
                Username = username,
                AccountId = 0, // We don't have account ID in SessionResult
                SessionToken = result.SessionToken
            });
            
            GameEventBus.Instance.Publish(new SessionCreatedEvent
            {
                Username = username,
                SessionToken = result.SessionToken,
                Identity = result.Identity
            });
            
            // Check for player after session is created
            StartCoroutine(CheckForPlayerAfterDelay());
        }
    }  
    IEnumerator CheckForPlayerAfterDelay()
    {
        // Wait a frame to ensure data is synced
        yield return null;
        
        // Check for local player
        CheckLocalPlayer();
    }

    #endregion

    #region World Events

    void OnWorldInsert(EventContext ctx, World world)
    {
        Debug.Log($"[EventBridge] World created at ({world.WorldCoords.X},{world.WorldCoords.Y},{world.WorldCoords.Z})");
        
        // Check if this is the world we're loading
        var localPlayer = GetLocalPlayer();
        if (localPlayer != null && world.WorldCoords.Equals(localPlayer.CurrentWorld))
        {
            GameEventBus.Instance.Publish(new WorldLoadedEvent
            {
                World = world
            });
        }
    }
    
    void OnWorldUpdate(EventContext ctx, World oldWorld, World newWorld)
    {
        Debug.Log($"[EventBridge] World updated at ({newWorld.WorldCoords.X},{newWorld.WorldCoords.Y},{newWorld.WorldCoords.Z})");
        
        // Handle world updates if needed
    }
    
    #endregion
    
    #region Reducer Response Events
    
    void OnCreatePlayerResponse(ReducerEventContext ctx, string playerName)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            Debug.Log($"[EventBridge] Player creation committed: {playerName}");
            // Player will appear via OnPlayerInsert
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            Debug.LogError($"[EventBridge] Player creation failed: {reason}");
            
            // Check if it's the "already has player" error (restoration case)
            if (reason != null && reason.Contains("already has a player"))
            {
                Debug.Log($"[EventBridge] Player exists, waiting for restoration...");
                // Server will restore via OnPlayerUpdate
            }
            else
            {
                GameEventBus.Instance.Publish(new PlayerCreationFailedEvent
                {
                    Reason = reason ?? "Unknown error"
                });
            }
        }
    }
    
    void OnLoginWithSessionResponse(ReducerEventContext ctx, string username, string pin, string deviceInfo)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            Debug.Log($"[EventBridge] Login committed for: {username}");
            // SessionResult will be created by server
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            Debug.LogError($"[EventBridge] Login failed: {reason}");
            
            GameEventBus.Instance.Publish(new LoginFailedEvent
            {
                Username = username,
                Reason = reason ?? "Unknown error"
            });
        }
    }
    
    #endregion
}

// Add these new event types to GameEventBus.cs:

public class SubscriptionReadyEvent : IGameEvent
{
    public DateTime Timestamp { get; set; }
    public string EventName => "SubscriptionReady";
}

public class SubscriptionErrorEvent : IGameEvent
{
    public DateTime Timestamp { get; set; }
    public string EventName => "SubscriptionError";
    public string Error { get; set; }
}

public class LocalPlayerNotFoundEvent : IGameEvent
{
    public DateTime Timestamp { get; set; }
    public string EventName => "LocalPlayerNotFound";
}