using System;
using System.Collections;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;
using SYSTEM.Game;

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
        conn = GameManager.Conn;
        
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
        conn.SubscriptionBuilder()
            .OnApplied(HandleSubscriptionApplied)
            .OnError(HandleSubscriptionError)
            .SubscribeToAllTables();
    }
    
    void HandleDisconnected()
    {
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
        
        GameEventBus.Instance.Publish(new SubscriptionErrorEvent
        {
            Error = error.Message
        });
    }
    
    void CheckLocalPlayer()
    {
        if (hasCheckedForPlayer) return;
        hasCheckedForPlayer = true;
        
        var localPlayer = GetLocalPlayer();
        if (localPlayer != null)
        {
            
            // Publish player ready event
            GameEventBus.Instance.Publish(new LocalPlayerReadyEvent
            {
                Player = localPlayer
            });
        }
        else
        {
            
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
        if (player.Identity == conn.Identity)
        {
            // Log restored position
            Debug.Log($"[EventBridge] Player '{player.Name}' inserted at position: " +
                $"World({player.CurrentWorld.X},{player.CurrentWorld.Y},{player.CurrentWorld.Z}), " +
                $"Pos({player.Position.X:F2},{player.Position.Y:F2},{player.Position.Z:F2})");
            
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
        // Check if this is a restoration (identity change to ours)
        if (oldPlayer.Identity != conn.Identity && newPlayer.Identity == conn.Identity)
        {
            
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
        if (player.Identity == conn.Identity)
        {
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
        // Check if this is our session
        if (result.Identity == conn.Identity)
        {
            
            // Get username from GameData (set by LoginUIController before login)
            string username = GameData.Instance.Username;
            if (string.IsNullOrEmpty(username))
            {
                return;
            }
            
            // Save the session with username
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
            
            // MVP: For new users without a player, we need to create one
            // Check if we have a player after a short delay to allow table updates
            StartCoroutine(CheckForPlayerAfterLogin());
        }
    }
    
    IEnumerator CheckForPlayerAfterLogin()
    {
        // Wait a moment for any existing player data to arrive
        yield return new WaitForSeconds(0.5f);
        
        var localPlayer = GetLocalPlayer();
        if (localPlayer == null)
        {
            Debug.Log("[EventBridge] No player found after login, need to create one");
            
            // MVP: For now, automatically create a player with the username
            string username = GameData.Instance.Username;
            if (!string.IsNullOrEmpty(username))
            {
                conn.Reducers.CreatePlayer(username);
            }
        }
        else
        {
            // Publish player ready event
            GameEventBus.Instance.Publish(new LocalPlayerReadyEvent
            {
                Player = localPlayer
            });
        }
    }

    #endregion

    #region World Events

    void OnWorldInsert(EventContext ctx, World world)
    {
        
        // Check if this is the world we're loading
        var localPlayer = GetLocalPlayer();
        if (localPlayer != null && 
            world.WorldCoords.X == localPlayer.CurrentWorld.X &&
            world.WorldCoords.Y == localPlayer.CurrentWorld.Y &&
            world.WorldCoords.Z == localPlayer.CurrentWorld.Z)
        {
            GameEventBus.Instance.Publish(new WorldLoadedEvent
            {
                World = world
            });
        }
    }
    
    void OnWorldUpdate(EventContext ctx, World oldWorld, World newWorld)
    {
        
        // Handle world updates if needed
    }
    
    #endregion
    
    #region Reducer Response Events
    
    void OnCreatePlayerResponse(ReducerEventContext ctx, string playerName)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            // Player will appear via OnPlayerInsert
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            // Check if it's the "already has player" error (restoration case)
            if (reason != null && reason.Contains("already has a player"))
            {
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
            // SessionResult will be created by server
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            
            GameEventBus.Instance.Publish(new LoginFailedEvent
            {
                Username = username,
                Reason = reason ?? "Unknown error"
            });
        }
    }
    
    #endregion
}

// Note: These event types are already defined in GameEventBus.cs
// Keeping them here for reference only

/*
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
*/