using System;
using System.Collections;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;
using SYSTEM.Game;

/// <summary>
/// Bridge between SpacetimeDB and EventBus
/// Handles ALL SpacetimeDB interactions including subscriptions
/// Subscribes to INSERT and DELETE events only (not UPDATE)
/// </summary>
public class SpacetimeDBEventBridge : MonoBehaviour
{
    private DbConnection conn;
    private bool isSubscribed = false;
    private bool hasCheckedForPlayer = false;

    // Debug: Track callback invocations
    private static int insertCallbackCount = 0;
    private static int handlerInvocationCounter = 0;
    private int bridgeInstanceId;

    void Awake()
    {
        bridgeInstanceId = GetInstanceID();
        SystemDebug.Log(SystemDebug.Category.Connection,
            $"[BRIDGE] SpacetimeDBEventBridge CREATED - InstanceID={bridgeInstanceId}");
    }

    void Start()
    {
        SystemDebug.Log(SystemDebug.Category.Connection, "SpacetimeDBEventBridge Started");


        // Make this persist across scene changes
        DontDestroyOnLoad(gameObject);

        // Subscribe to connection events from GameManager
        GameManager.OnConnected += HandleConnected;
        GameManager.OnDisconnected += HandleDisconnected;
        GameManager.OnConnectionError += HandleConnectionError;

        // Subscribe to scene load events to re-fire initial data when WorldScene loads
        GameEventBus.Instance.Subscribe<SceneLoadCompletedEvent>(OnSceneLoadCompleted);

        // Check if already connected
        if (GameManager.IsConnected())
        {
            SystemDebug.Log(SystemDebug.Category.Connection, "Already connected, initializing bridge");
            conn = GameManager.Conn;
            HandleConnected();
        }
        else
        {
            SystemDebug.Log(SystemDebug.Category.Connection, "Not yet connected, waiting for connection");
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe from GameManager events
        GameManager.OnConnected -= HandleConnected;
        GameManager.OnDisconnected -= HandleDisconnected;
        GameManager.OnConnectionError -= HandleConnectionError;

        // Unsubscribe from scene load events
        GameEventBus.Instance.Unsubscribe<SceneLoadCompletedEvent>(OnSceneLoadCompleted);

        UnsubscribeFromTableEvents();
    }

    #region Connection Handling
    
    void HandleConnected()
    {
        SystemDebug.Log(SystemDebug.Category.Connection, $"[DEBUG] HandleConnected called - isSubscribed={isSubscribed}");

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
            SystemDebug.LogError(SystemDebug.Category.Connection, "Connected but no identity!");
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

    /// <summary>
    /// Re-fires initial load events when WorldScene loads.
    /// This ensures visualization managers get data even if the initial load event
    /// fired before they existed (timing issue with LoginScene → WorldScene transition).
    /// </summary>
    void OnSceneLoadCompleted(SceneLoadCompletedEvent evt)
    {
        // Only re-fire for WorldScene (center world)
        if (evt.SceneName != "WorldScene")
        {
            return;
        }

        SystemDebug.Log(SystemDebug.Category.SpireSystem,
            $"WorldScene loaded, re-firing initial load events for world ({evt.WorldCoords.X},{evt.WorldCoords.Y},{evt.WorldCoords.Z})");

        // Re-fire initial load events for the current world
        // The Bridge already has the connection and can query the tables
        LoadInitialSourcesForWorld(evt.WorldCoords);
        LoadInitialSpiresForWorld(evt.WorldCoords);

        // Also load storage devices for this world
        LoadInitialStorageDevicesForWorld(evt.WorldCoords);
    }

    #endregion

    #region Subscription Handling
    
    void HandleSubscriptionApplied(SubscriptionEventContext ctx)
    {
        SystemDebug.Log(SystemDebug.Category.Subscription, $"[DEBUG] HandleSubscriptionApplied called - isSubscribed={isSubscribed}");

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

        SystemDebug.Log(SystemDebug.Category.PlayerSystem, "Checking for local player...");

        var localPlayer = GetLocalPlayer();
        if (localPlayer != null)
        {
            SystemDebug.Log(SystemDebug.Category.PlayerSystem, $"Local player found: {localPlayer.Name} at world ({localPlayer.CurrentWorld.X},{localPlayer.CurrentWorld.Y},{localPlayer.CurrentWorld.Z})");

            // Publish player ready event
            GameEventBus.Instance.Publish(new LocalPlayerReadyEvent
            {
                Player = localPlayer
            });

            // Load initial orbs for the player's current world
            SystemDebug.Log(SystemDebug.Category.WavePacketSystem, "Loading orbs for player's current world");
            LoadInitialSourcesForWorld(localPlayer.CurrentWorld);

            // Load initial spires for the player's current world
            SystemDebug.Log(SystemDebug.Category.SpireSystem, "Loading spires for player's current world");
            LoadInitialSpiresForWorld(localPlayer.CurrentWorld);

            // Load initial storage devices for this world
            SystemDebug.Log(SystemDebug.Category.WavePacketSystem, "Loading storage devices for current world");
            LoadInitialStorageDevicesForWorld(localPlayer.CurrentWorld);
        }
        else
        {
            SystemDebug.Log(SystemDebug.Category.PlayerSystem, "Local player NOT found - player doesn't exist yet");

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
        SystemDebug.Log(SystemDebug.Category.Subscription,
            $"[SUB] SubscribeToTableEvents ENTER - BridgeID={bridgeInstanceId}, isSubscribed={isSubscribed}, conn={conn != null}");

        if (isSubscribed || conn == null)
        {
            SystemDebug.Log(SystemDebug.Category.Subscription,
                $"[SUB] SubscribeToTableEvents BLOCKED - BridgeID={bridgeInstanceId}, isSubscribed={isSubscribed}");
            return;
        }

        SystemDebug.Log(SystemDebug.Category.Subscription,
            $"[SUB] SubscribeToTableEvents PROCEEDING - BridgeID={bridgeInstanceId}");

        // Player table events
        conn.Db.Player.OnInsert += OnPlayerInsert;
        conn.Db.Player.OnUpdate += OnPlayerUpdate;
        conn.Db.Player.OnDelete += OnPlayerDelete;

        // Session events
        conn.Db.SessionResult.OnInsert += OnSessionResultInsert;

        // World events
        conn.Db.World.OnInsert += OnWorldInsert;

        // WavePacketSource table events
        SystemDebug.Log(SystemDebug.Category.Subscription,
            $"[SUB] Subscribing to WavePacketSource.OnUpdate - BridgeID={bridgeInstanceId}");
        conn.Db.WavePacketSource.OnInsert += OnWavePacketSourceInsert;
        conn.Db.WavePacketSource.OnUpdate += OnWavePacketSourceUpdate;
        conn.Db.WavePacketSource.OnDelete += OnWavePacketSourceDelete;

        // Energy Spire table events
        SystemDebug.Log(SystemDebug.Category.Subscription,
            $"[SUB] Subscribing to WorldCircuit.OnUpdate - BridgeID={bridgeInstanceId}");
        conn.Db.DistributionSphere.OnInsert += OnDistributionSphereInsert;
        conn.Db.DistributionSphere.OnUpdate += OnDistributionSphereUpdate;
        conn.Db.DistributionSphere.OnDelete += OnDistributionSphereDelete;
        conn.Db.QuantumTunnel.OnInsert += OnQuantumTunnelInsert;
        conn.Db.QuantumTunnel.OnUpdate += OnQuantumTunnelUpdate;
        conn.Db.QuantumTunnel.OnDelete += OnQuantumTunnelDelete;
        conn.Db.WorldCircuit.OnInsert += OnWorldCircuitInsert;
        conn.Db.WorldCircuit.OnUpdate += OnWorldCircuitUpdate;
        conn.Db.WorldCircuit.OnDelete += OnWorldCircuitDelete;

        // Storage Device table events
        conn.Db.StorageDevice.OnInsert += OnStorageDeviceInsert;
        conn.Db.StorageDevice.OnUpdate += OnStorageDeviceUpdate;
        conn.Db.StorageDevice.OnDelete += OnStorageDeviceDelete;

        // PacketTransfer table events (energy transfer visualization)
        conn.Db.PacketTransfer.OnInsert += OnPacketTransferInsert;
        conn.Db.PacketTransfer.OnUpdate += OnPacketTransferUpdate;
        conn.Db.PacketTransfer.OnDelete += OnPacketTransferDelete;

        // Reducer response events
        conn.Reducers.OnCreatePlayer += OnCreatePlayerResponse;
        conn.Reducers.OnLoginWithSession += OnLoginWithSessionResponse;

        isSubscribed = true;
        SystemDebug.Log(SystemDebug.Category.Subscription,
            $"[SUB] SubscribeToTableEvents COMPLETE - BridgeID={bridgeInstanceId}, isSubscribed={isSubscribed}");
    }
    
    void UnsubscribeFromTableEvents()
    {
        if (!isSubscribed || conn == null) return;

        conn.Db.Player.OnInsert -= OnPlayerInsert;
        conn.Db.Player.OnUpdate -= OnPlayerUpdate;
        conn.Db.Player.OnDelete -= OnPlayerDelete;
        conn.Db.SessionResult.OnInsert -= OnSessionResultInsert;
        conn.Db.World.OnInsert -= OnWorldInsert;
        conn.Db.WavePacketSource.OnInsert -= OnWavePacketSourceInsert;
        conn.Db.WavePacketSource.OnUpdate -= OnWavePacketSourceUpdate;
        conn.Db.WavePacketSource.OnDelete -= OnWavePacketSourceDelete;
        conn.Db.DistributionSphere.OnInsert -= OnDistributionSphereInsert;
        conn.Db.DistributionSphere.OnUpdate -= OnDistributionSphereUpdate;
        conn.Db.DistributionSphere.OnDelete -= OnDistributionSphereDelete;
        conn.Db.QuantumTunnel.OnInsert -= OnQuantumTunnelInsert;
        conn.Db.QuantumTunnel.OnUpdate -= OnQuantumTunnelUpdate;
        conn.Db.QuantumTunnel.OnDelete -= OnQuantumTunnelDelete;
        conn.Db.WorldCircuit.OnInsert -= OnWorldCircuitInsert;
        conn.Db.WorldCircuit.OnUpdate -= OnWorldCircuitUpdate;
        conn.Db.WorldCircuit.OnDelete -= OnWorldCircuitDelete;
        conn.Db.StorageDevice.OnInsert -= OnStorageDeviceInsert;
        conn.Db.StorageDevice.OnUpdate -= OnStorageDeviceUpdate;
        conn.Db.StorageDevice.OnDelete -= OnStorageDeviceDelete;
        conn.Db.PacketTransfer.OnInsert -= OnPacketTransferInsert;
        conn.Db.PacketTransfer.OnUpdate -= OnPacketTransferUpdate;
        conn.Db.PacketTransfer.OnDelete -= OnPacketTransferDelete;
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
            SystemDebug.Log(SystemDebug.Category.PlayerSystem,
                $"Player '{player.Name}' inserted at position: " +
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

    void OnPlayerUpdate(EventContext ctx, Player oldPlayer, Player newPlayer)
    {
        if (newPlayer.Identity == conn.Identity)
        {
            GameEventBus.Instance.Publish(new LocalPlayerChangedEvent
            {
                Player = newPlayer
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
            SystemDebug.Log(SystemDebug.Category.Session, "No player found after login, need to create one");
            
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
        SystemDebug.Log(SystemDebug.Category.WorldSystem, $"OnWorldInsert called for world ({world.WorldCoords.X},{world.WorldCoords.Y},{world.WorldCoords.Z})");

        // Check if this is the world we're loading
        var localPlayer = GetLocalPlayer();
        if (localPlayer != null)
        {
            SystemDebug.Log(SystemDebug.Category.WorldSystem, $"Local player found in world ({localPlayer.CurrentWorld.X},{localPlayer.CurrentWorld.Y},{localPlayer.CurrentWorld.Z})");

            if (world.WorldCoords.X == localPlayer.CurrentWorld.X &&
                world.WorldCoords.Y == localPlayer.CurrentWorld.Y &&
                world.WorldCoords.Z == localPlayer.CurrentWorld.Z)
            {
                SystemDebug.Log(SystemDebug.Category.WorldSystem, "World matches player's current world, publishing events");

                GameEventBus.Instance.Publish(new WorldLoadedEvent
                {
                    World = world
                });

                // Load initial spires for this world
                LoadInitialSpiresForWorld(world.WorldCoords);

                // Load initial storage devices for this world
                LoadInitialStorageDevicesForWorld(world.WorldCoords);
            }
            else
            {
                SystemDebug.Log(SystemDebug.Category.WorldSystem, "World does not match player's current world");
            }
        }
        else
        {
            SystemDebug.Log(SystemDebug.Category.PlayerSystem, "Local player not found during world insert check");
        }
    }

    void LoadInitialSourcesForWorld(WorldCoords worldCoords)
    {
        SystemDebug.Log(SystemDebug.Category.WavePacketSystem, $"LoadInitialSourcesForWorld called for world ({worldCoords.X},{worldCoords.Y},{worldCoords.Z})");

        if (conn == null)
        {
            SystemDebug.LogError(SystemDebug.Category.WavePacketSystem, "Connection is null in LoadInitialSourcesForWorld");
            return;
        }

        var sourcesInWorld = new System.Collections.Generic.List<WavePacketSource>();
        int totalOrbs = 0;

        foreach (var source in conn.Db.WavePacketSource.Iter())
        {
            totalOrbs++;
            SystemDebug.Log(SystemDebug.Category.WavePacketSystem, $"Found orb {source.SourceId} at world ({source.WorldCoords.X},{source.WorldCoords.Y},{source.WorldCoords.Z})");

            if (source.WorldCoords.X == worldCoords.X &&
                source.WorldCoords.Y == worldCoords.Y &&
                source.WorldCoords.Z == worldCoords.Z)
            {
                sourcesInWorld.Add(source);
                SystemDebug.Log(SystemDebug.Category.WavePacketSystem, $"Orb {source.SourceId} matches target world");
            }
        }

        SystemDebug.Log(SystemDebug.Category.WavePacketSystem, $"Total orbs in database: {totalOrbs}, orbs in this world: {sourcesInWorld.Count}");

        if (sourcesInWorld.Count > 0)
        {
            SystemDebug.Log(SystemDebug.Category.WavePacketSystem, $"Publishing InitialSourcesLoadedEvent with {sourcesInWorld.Count} orbs");
            GameEventBus.Instance.Publish(new InitialSourcesLoadedEvent
            {
                Sources = sourcesInWorld
            });
        }
        else
        {
            SystemDebug.Log(SystemDebug.Category.WavePacketSystem, "No orbs found in this world, not publishing event");
        }
    }

    #endregion

    #region Orb Events

    void OnWavePacketSourceInsert(EventContext ctx, WavePacketSource source)
    {
        insertCallbackCount++;
        SystemDebug.Log(SystemDebug.Category.WavePacketSystem,
            $"[DEBUG #{insertCallbackCount}] OnWavePacketSourceInsert from SpacetimeDB - Source ID={source.SourceId}, Packets={source.TotalWavePackets}");

        GameEventBus.Instance.Publish(new WavePacketSourceInsertedEvent
        {
            Source = source
        });
    }

    void OnWavePacketSourceDelete(EventContext ctx, WavePacketSource source)
    {
        GameEventBus.Instance.Publish(new WavePacketSourceDeletedEvent
        {
            Source = source
        });
    }

    void OnWavePacketSourceUpdate(EventContext ctx, WavePacketSource oldSource, WavePacketSource newSource)
    {
        var invocationId = ++handlerInvocationCounter;
        SystemDebug.Log(SystemDebug.Category.WavePacketSystem,
            $"[HANDLER:{invocationId}] OnWavePacketSourceUpdate - BridgeID={bridgeInstanceId}, " +
            $"source_id={newSource.SourceId}, old_packets={oldSource.TotalWavePackets}, new_packets={newSource.TotalWavePackets}");

        GameEventBus.Instance.Publish(new WavePacketSourceUpdatedEvent
        {
            OldSource = oldSource,
            NewSource = newSource
        });

        SystemDebug.Log(SystemDebug.Category.WavePacketSystem,
            $"[HANDLER:{invocationId}] Published WavePacketSourceUpdatedEvent");
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

    #region Energy Spire Events

    // Distribution Sphere handlers
    void OnDistributionSphereInsert(EventContext ctx, DistributionSphere sphere)
    {
        GameEventBus.Instance.Publish(new DistributionSphereInsertedEvent
        {
            Sphere = sphere
        });
    }

    void OnDistributionSphereDelete(EventContext ctx, DistributionSphere sphere)
    {
        GameEventBus.Instance.Publish(new DistributionSphereDeletedEvent
        {
            Sphere = sphere
        });
    }

    void OnDistributionSphereUpdate(EventContext ctx, DistributionSphere oldValue, DistributionSphere newValue)
    {
        GameEventBus.Instance.Publish(new DistributionSphereUpdatedEvent
        {
            OldSphere = oldValue,
            NewSphere = newValue
        });
    }

    // Quantum Tunnel handlers
    void OnQuantumTunnelInsert(EventContext ctx, QuantumTunnel tunnel)
    {
        GameEventBus.Instance.Publish(new QuantumTunnelInsertedEvent
        {
            Tunnel = tunnel
        });
    }

    void OnQuantumTunnelDelete(EventContext ctx, QuantumTunnel tunnel)
    {
        GameEventBus.Instance.Publish(new QuantumTunnelDeletedEvent
        {
            Tunnel = tunnel
        });
    }

    void OnQuantumTunnelUpdate(EventContext ctx, QuantumTunnel oldValue, QuantumTunnel newValue)
    {
        GameEventBus.Instance.Publish(new QuantumTunnelUpdatedEvent
        {
            OldTunnel = oldValue,
            NewTunnel = newValue
        });
    }

    // World Circuit handlers
    void OnWorldCircuitInsert(EventContext ctx, WorldCircuit circuit)
    {
        GameEventBus.Instance.Publish(new WorldCircuitInsertedEvent
        {
            Circuit = circuit
        });
    }

    void OnWorldCircuitDelete(EventContext ctx, WorldCircuit circuit)
    {
        GameEventBus.Instance.Publish(new WorldCircuitDeletedEvent
        {
            Circuit = circuit
        });
    }

    void OnWorldCircuitUpdate(EventContext ctx, WorldCircuit oldValue, WorldCircuit newValue)
    {
        var invocationId = ++handlerInvocationCounter;
        SystemDebug.Log(SystemDebug.Category.SpireSystem,
            $"[HANDLER:{invocationId}] OnWorldCircuitUpdate - BridgeID={bridgeInstanceId}, " +
            $"circuit_id={newValue.CircuitId}, direction={newValue.CardinalDirection}");

        GameEventBus.Instance.Publish(new WorldCircuitUpdatedEvent
        {
            OldCircuit = oldValue,
            NewCircuit = newValue
        });

        SystemDebug.Log(SystemDebug.Category.SpireSystem,
            $"[HANDLER:{invocationId}] Published WorldCircuitUpdatedEvent");
    }

    // Storage Device handlers
    void OnStorageDeviceInsert(EventContext ctx, StorageDevice device)
    {
        GameEventBus.Instance.Publish(new DeviceInsertedEvent
        {
            Device = device
        });
    }

    void OnStorageDeviceDelete(EventContext ctx, StorageDevice device)
    {
        GameEventBus.Instance.Publish(new DeviceDeletedEvent
        {
            Device = device
        });
    }

    void OnStorageDeviceUpdate(EventContext ctx, StorageDevice oldValue, StorageDevice newValue)
    {
        GameEventBus.Instance.Publish(new DeviceUpdatedEvent
        {
            OldDevice = oldValue,
            NewDevice = newValue
        });
    }

    // PacketTransfer handlers
    void OnPacketTransferInsert(EventContext ctx, PacketTransfer transfer)
    {
        GameEventBus.Instance.Publish(new PacketTransferInsertedEvent
        {
            Timestamp = DateTime.UtcNow,
            Transfer = transfer
        });
    }

    void OnPacketTransferDelete(EventContext ctx, PacketTransfer transfer)
    {
        GameEventBus.Instance.Publish(new PacketTransferDeletedEvent
        {
            Timestamp = DateTime.UtcNow,
            Transfer = transfer
        });
    }

    void OnPacketTransferUpdate(EventContext ctx, PacketTransfer oldValue, PacketTransfer newValue)
    {
        SystemDebug.Log(SystemDebug.Category.Network,
            $"[BRIDGE] PacketTransfer UPDATE: ID={newValue.TransferId} " +
            $"| Old: {oldValue.CurrentLegType} leg {oldValue.CurrentLeg} " +
            $"| New: {newValue.CurrentLegType} leg {newValue.CurrentLeg}");

        GameEventBus.Instance.Publish(new PacketTransferUpdatedEvent
        {
            Timestamp = DateTime.UtcNow,
            OldTransfer = oldValue,
            NewTransfer = newValue
        });
    }

    // Load initial storage devices for a world (all devices, not filtered by owner)
    void LoadInitialStorageDevicesForWorld(WorldCoords worldCoords)
    {
        SystemDebug.Log(SystemDebug.Category.WavePacketSystem,
            $"LoadInitialStorageDevicesForWorld called for world ({worldCoords.X},{worldCoords.Y},{worldCoords.Z})");

        if (conn == null)
        {
            SystemDebug.LogError(SystemDebug.Category.WavePacketSystem, "Connection is null in LoadInitialStorageDevicesForWorld");
            return;
        }

        var devicesInWorld = new System.Collections.Generic.List<StorageDevice>();

        // Load ALL Storage Devices in this world (visible to all players)
        foreach (var device in conn.Db.StorageDevice.Iter())
        {
            if (device.WorldCoords.X == worldCoords.X &&
                device.WorldCoords.Y == worldCoords.Y &&
                device.WorldCoords.Z == worldCoords.Z)
            {
                devicesInWorld.Add(device);
            }
        }

        SystemDebug.Log(SystemDebug.Category.WavePacketSystem,
            $"Found {devicesInWorld.Count} storage devices in world ({worldCoords.X},{worldCoords.Y},{worldCoords.Z})");

        if (devicesInWorld.Count > 0)
        {
            GameEventBus.Instance.Publish(new InitialDevicesLoadedEvent
            {
                Devices = devicesInWorld
            });
        }
    }

    // Load initial spires for a world (called when player enters world)
    void LoadInitialSpiresForWorld(WorldCoords worldCoords)
    {
        SystemDebug.Log(SystemDebug.Category.SpireSystem, $"LoadInitialSpiresForWorld called for world ({worldCoords.X},{worldCoords.Y},{worldCoords.Z})");

        if (conn == null)
        {
            SystemDebug.LogError(SystemDebug.Category.SpireSystem, "Connection is null in LoadInitialSpiresForWorld");
            return;
        }

        var spheresInWorld = new System.Collections.Generic.List<DistributionSphere>();
        var tunnelsInWorld = new System.Collections.Generic.List<QuantumTunnel>();
        var circuitsInWorld = new System.Collections.Generic.List<WorldCircuit>();

        // Load Distribution Spheres
        foreach (var sphere in conn.Db.DistributionSphere.Iter())
        {
            if (sphere.WorldCoords.X == worldCoords.X &&
                sphere.WorldCoords.Y == worldCoords.Y &&
                sphere.WorldCoords.Z == worldCoords.Z)
            {
                spheresInWorld.Add(sphere);
                SystemDebug.Log(SystemDebug.Category.SpireSystem, $"Found DistributionSphere {sphere.SphereId} at {sphere.CardinalDirection}");
            }
        }

        // Load Quantum Tunnels
        foreach (var tunnel in conn.Db.QuantumTunnel.Iter())
        {
            if (tunnel.WorldCoords.X == worldCoords.X &&
                tunnel.WorldCoords.Y == worldCoords.Y &&
                tunnel.WorldCoords.Z == worldCoords.Z)
            {
                tunnelsInWorld.Add(tunnel);
                SystemDebug.Log(SystemDebug.Category.SpireSystem, $"Found QuantumTunnel {tunnel.TunnelId} at {tunnel.CardinalDirection}");
            }
        }

        // Load World Circuits
        foreach (var circuit in conn.Db.WorldCircuit.Iter())
        {
            if (circuit.WorldCoords.X == worldCoords.X &&
                circuit.WorldCoords.Y == worldCoords.Y &&
                circuit.WorldCoords.Z == worldCoords.Z)
            {
                circuitsInWorld.Add(circuit);
                SystemDebug.Log(SystemDebug.Category.SpireSystem, $"Found WorldCircuit {circuit.CircuitId} at {circuit.CardinalDirection}");
            }
        }

        SystemDebug.Log(SystemDebug.Category.SpireSystem,
            $"Found {spheresInWorld.Count} spheres, {tunnelsInWorld.Count} tunnels, {circuitsInWorld.Count} circuits in this world");

        // Always publish event, even if counts are zero (managers need to know world is loaded)
        GameEventBus.Instance.Publish(new InitialSpiresLoadedEvent
        {
            Spheres = spheresInWorld,
            Tunnels = tunnelsInWorld,
            Circuits = circuitsInWorld
        });
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
