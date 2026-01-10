using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpacetimeDB.Types
{
    /// <summary>
    /// Centralized event system for decoupled component communication
    /// </summary>
    public class GameEventBus : MonoBehaviour
    {
        /// <summary>
        /// Guaranteed early creation before any scene loads (fixes WebGL race condition)
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureGameEventBusExists()
        {
            if (instance == null)
            {
                // Debug.Log("[GameEventBus] 🚀 Creating GameEventBus via RuntimeInitializeOnLoadMethod");
                GameObject go = new GameObject("GameEventBus");
                instance = go.AddComponent<GameEventBus>();
                DontDestroyOnLoad(go);
            }
        }
        private static GameEventBus instance;
        public static GameEventBus Instance
        {
            get
            {
                if (instance == null && Application.isPlaying)
                {
                    // Debug.LogError("[GameEventBus] Instance is null! RuntimeInitializeOnLoadMethod should have created it.");
                    // Fallback creation
                    GameObject go = new GameObject("GameEventBus");
                    instance = go.AddComponent<GameEventBus>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        // Event storage
        private Dictionary<Type, List<Delegate>> eventHandlers = new Dictionary<Type, List<Delegate>>();
        private List<EventLogEntry> eventHistory = new List<EventLogEntry>();
        private readonly object eventLock = new object();

        // Settings
        [SerializeField] private bool enableLogging = true;
        [SerializeField] private int maxEventHistory = 100;

        // State Management
        public enum GameState
        {
            Disconnected,      // Initial state
            Connecting,        // Connecting to server
            Connected,         // Connected but not authenticated
            CheckingPlayer,    // Checking if player exists (NEW)
            WaitingForLogin,   // No player found, waiting for login (NEW)
            Authenticating,    // Login in progress
            Authenticated,     // Login successful, checking player
            CreatingPlayer,    // Creating/restoring player
            PlayerReady,       // Player loaded, ready for world
            LoadingWorld,      // Loading world scene
            InGame            // Fully in game
        }


        private GameState currentState = GameState.Disconnected;
        public GameState CurrentState => currentState;

        // State transition rules - which states can transition to which
        private readonly Dictionary<GameState, HashSet<GameState>> allowedTransitions = new Dictionary<GameState, HashSet<GameState>>
        {
            { GameState.Disconnected, new HashSet<GameState> { GameState.Connecting } },
            { GameState.Connecting, new HashSet<GameState> { GameState.Connected, GameState.Disconnected } },
            { GameState.Connected, new HashSet<GameState> { GameState.CheckingPlayer, GameState.Disconnected } },
            { GameState.CheckingPlayer, new HashSet<GameState> { GameState.WaitingForLogin, GameState.PlayerReady, GameState.Disconnected } },
            { GameState.WaitingForLogin, new HashSet<GameState> { GameState.Authenticating, GameState.Disconnected } },
            { GameState.Authenticating, new HashSet<GameState> { GameState.Authenticated, GameState.WaitingForLogin, GameState.Disconnected } },
            { GameState.Authenticated, new HashSet<GameState> { GameState.InGame, GameState.CheckingPlayer, GameState.CreatingPlayer, GameState.PlayerReady, GameState.Disconnected } },
            { GameState.CreatingPlayer, new HashSet<GameState> { GameState.PlayerReady, GameState.Authenticated, GameState.Disconnected } },
            { GameState.PlayerReady, new HashSet<GameState> { GameState.InGame, GameState.LoadingWorld, GameState.Disconnected } },
            { GameState.LoadingWorld, new HashSet<GameState> { GameState.InGame, GameState.PlayerReady, GameState.Disconnected } },
            { GameState.InGame, new HashSet<GameState> { GameState.LoadingWorld, GameState.PlayerReady, GameState.Disconnected } }
        };


        #region Core Event System

        /// <summary>
        /// Publish an event to all subscribers
        /// </summary>
        public bool Publish<T>(T eventData) where T : IGameEvent
        {
            if (eventData == null)
            {
                // Debug.LogError("[EventBus] Attempted to publish null event");
                return false;
            }

            // Set timestamp if not already set
            if (eventData.Timestamp == default(DateTime))
            {
                eventData.Timestamp = DateTime.Now;
            }

            // Log every event being published
            Type eventType = typeof(T);

            // Log the event
            LogEvent(eventData);

            // Get handlers
            List<Action<T>> handlers = null;
            lock (eventLock)
            {
                if (eventHandlers.ContainsKey(eventType))
                {
                    handlers = new List<Action<T>>();
                    foreach (var handler in eventHandlers[eventType])
                    {
                        handlers.Add((Action<T>)handler);
                    }
                }
            }

            // Execute handlers outside of lock
            if (handlers != null)
            {
                SystemDebug.Log(SystemDebug.Category.EventBus,
                    $"[EVENTBUS] Publishing {eventType.Name} - HandlerCount={handlers.Count}");
                for (int i = 0; i < handlers.Count; i++)
                {
                    try
                    {
                        SystemDebug.Log(SystemDebug.Category.EventBus,
                            $"[EVENTBUS] Executing handler {i + 1}/{handlers.Count} for {eventType.Name}");
                        handlers[i](eventData);
                    }
                    catch (Exception e)
                    {
                        SystemDebug.LogError(SystemDebug.Category.EventBus,
                            $"[EVENTBUS] Error in handler {i + 1} for {eventType.Name}: {e.Message}\n{e.StackTrace}");
                    }
                }
            }
            else
            {
                SystemDebug.Log(SystemDebug.Category.EventBus,
                    $"[EVENTBUS] No handlers registered for {eventType.Name}");
            }

            // Handle state transitions based on events
            HandleStateTransition(eventData);

            return true;
        }

        /// <summary>
        /// Subscribe to an event type
        /// </summary>
        public void Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (handler == null) return;

            lock (eventLock)
            {
                Type eventType = typeof(T);
                if (!eventHandlers.ContainsKey(eventType))
                {
                    eventHandlers[eventType] = new List<Delegate>();
                }
                eventHandlers[eventType].Add(handler);

                SystemDebug.Log(SystemDebug.Category.EventBus, $"Handler subscribed to {eventType.Name} (total handlers: {eventHandlers[eventType].Count})");
            }
        }

        /// <summary>
        /// Unsubscribe from an event type
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (handler == null) return;

            lock (eventLock)
            {
                Type eventType = typeof(T);
                if (eventHandlers.ContainsKey(eventType))
                {
                    eventHandlers[eventType].Remove(handler);
                    if (enableLogging)
                    {
                    }
                }
            }
        }

        #endregion

        #region State Management

        /// <summary>
        /// Attempt to transition to a new state
        /// </summary>
        public bool TrySetState(GameState newState)
        {
            if (currentState == newState) 
            {
                return true;
            }

            if (!allowedTransitions.ContainsKey(currentState))
            {
                // Debug.LogError($"[EventBus] No transitions defined for state {currentState}!");
                return false;
            }
            
            if (!allowedTransitions[currentState].Contains(newState))
            {
                // Debug.LogWarning($"[EventBus] Invalid state transition: {currentState} → {newState}. Allowed transitions from {currentState}: {string.Join(", ", allowedTransitions[currentState])}");
                return false;
            }

            GameState oldState = currentState;
            currentState = newState;

            if (enableLogging)
            {
                // Debug.Log($"State: {oldState} → {newState}");
            }

            // Publish state change event
            Publish(new StateChangedEvent
            {
                OldState = oldState,
                NewState = newState
            });

            return true;
        }

        /// <summary>
        /// Force set state (use only for initialization or error recovery)
        /// </summary>
        public void ForceSetState(GameState newState)
        {
            GameState oldState = currentState;
            currentState = newState;
        }

        /// <summary>
        /// Handle automatic state transitions based on events
        /// </summary>
        private void HandleStateTransition(IGameEvent eventData)
        {
            // Define automatic state transitions based on events
            switch (eventData)
            {
                case ConnectionStartedEvent:
                    // Debug.Log($"[EventBus] Handling ConnectionStartedEvent, current state: {currentState}");
                    if (!TrySetState(GameState.Connecting))
                    {
                        // Debug.LogError($"[EventBus] Failed to transition from {currentState} to Connecting!");
                    }
                    break;
                case ConnectionEstablishedEvent:
                    TrySetState(GameState.Connected);
                    break;
                case SubscriptionReadyEvent:
                    TrySetState(GameState.CheckingPlayer);
                    break;
                case LocalPlayerNotFoundEvent:
                    // After checking for player, if not found, wait for login
                    if (currentState == GameState.CheckingPlayer)
                    {
                        TrySetState(GameState.WaitingForLogin);
                    }
                    // If we're authenticated and no player found, move to creating player
                    else if (currentState == GameState.Authenticated)
                    {
                        TrySetState(GameState.CreatingPlayer);
                    }
                    break;
                case LoginStartedEvent:
                    TrySetState(GameState.Authenticating);
                    break;
                case LoginSuccessfulEvent:
                    TrySetState(GameState.Authenticated);
                    break;
                case SessionCreatedEvent:
                    // Session created, stay in Authenticated state for now
                    // The proper flow is: Authenticated -> CheckingPlayer -> PlayerReady -> InGame
                    break;
                case LocalPlayerCheckStartedEvent:
                    // If we're authenticated, we're checking after login
                    if (currentState == GameState.Authenticated)
                    {
                        TrySetState(GameState.CheckingPlayer);
                    }
                    break;
                case PlayerCreationStartedEvent:
                    if (currentState == GameState.Authenticated || currentState == GameState.WaitingForLogin)
                    {
                        TrySetState(GameState.CreatingPlayer);
                    }
                    break;
                case LocalPlayerReadyEvent:
                    // Proper flow: Always go to PlayerReady state first
                    TrySetState(GameState.PlayerReady);
                    break;
                case LocalPlayerRestoredEvent:
                    // Proper flow: Always go to PlayerReady state first
                    TrySetState(GameState.PlayerReady);
                    break;
                case WorldLoadStartedEvent:
                    TrySetState(GameState.LoadingWorld);
                    break;
                case WorldLoadedEvent:
                    TrySetState(GameState.InGame);
                    break;
                case ConnectionLostEvent:
                    TrySetState(GameState.Disconnected);
                    break;

            }
        }

        #endregion

        #region Logging and Debug

        private void LogEvent<T>(T eventData) where T : IGameEvent
        {
            if (!enableLogging) return;

            string eventInfo = $"[EventBus] EVENT: {eventData.EventName} at {eventData.Timestamp:HH:mm:ss.fff} [{currentState}]";

            // Add additional info based on event type
            switch (eventData)
            {
                case LoginSuccessfulEvent login:
                    eventInfo += $" - User: {login.Username}";
                    break;
                case LocalPlayerReadyEvent ready:
                    eventInfo += $" - Player: {ready.Player?.Name}";
                    break;
                case StateChangedEvent state:
                    eventInfo += $" - {state.OldState} → {state.NewState}";
                    break;
            }

            // Debug.Log(eventInfo);

            // Add to history
            eventHistory.Add(new EventLogEntry
            {
                Timestamp = eventData.Timestamp,
                EventName = eventData.EventName,
                EventType = eventData.GetType(),
                State = currentState
            });

            // Trim history if needed
            while (eventHistory.Count > maxEventHistory)
            {
                eventHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// Get a snapshot of current state info
        /// </summary>
        public string GetStateInfo()
        {
            return $"State: {currentState}, Handlers: {eventHandlers.Count} types, History: {eventHistory.Count} events";
        }

        /// <summary>
        /// Dump the event history to console
        /// </summary>
        public void DumpEventHistory()
        {
            // Debug.Log($"[EventBus] === Event History ({eventHistory.Count} events) ===");
            foreach (var entry in eventHistory)
            {
                // Debug.Log($"  {entry.Timestamp:HH:mm:ss.fff} [{entry.State}] {entry.EventName}");
            }
            // Debug.Log("[EventBus] === End Event History ===");
        }

        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            #if UNITY_WEBGL && !UNITY_EDITOR
            // Debug.Log($"[GameEventBus] WebGL: Awake() called, instance before = {instance}");
            #endif

            if (instance != null && instance != this)
            {
                #if UNITY_WEBGL && !UNITY_EDITOR
                // Debug.Log("[GameEventBus] WebGL: Duplicate instance detected, destroying");
                #endif
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            #if UNITY_WEBGL && !UNITY_EDITOR
            // Debug.Log($"[GameEventBus] WebGL: Awake() complete, instance = {instance}");
            #endif

            #if UNITY_WEBGL && !UNITY_EDITOR
            // Debug.Log($"[GameEventBus] WebGL: Awake() complete, instance = {instance}");
            #endif
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        #endregion

        #region Helper Classes

        private class EventLogEntry
        {
            public DateTime Timestamp { get; set; }
            public string EventName { get; set; }
            public Type EventType { get; set; }
            public GameState State { get; set; }
        }

        #endregion
    }

    #region Event Interfaces and Base Types

    /// <summary>
    /// Base interface for all game events
    /// </summary>
    public interface IGameEvent
    {
        DateTime Timestamp { get; set; }
        string EventName { get; }
    }

    #endregion

    #region Connection Events

    public class ConnectionStartedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "ConnectionStarted";
        public string ServerUrl { get; set; }
    }

    public class ConnectionEstablishedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "ConnectionEstablished";
        public Identity Identity { get; set; }
        public string Token { get; set; }
    }

    public class ConnectionFailedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string EventName => "ConnectionFailed";
        public string Error { get; set; }
    }

    public class ConnectionLostEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "ConnectionLost";
        public string Reason { get; set; }
    }

    #endregion

    #region Login Events

    public class LoginStartedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "LoginStarted";
        public string Username { get; set; }
    }

    public class LoginSuccessfulEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "LoginSuccessful";
        public string Username { get; set; }
        public ulong AccountId { get; set; }
        public string SessionToken { get; set; }
    }

    public class LoginFailedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "LoginFailed";
        public string Username { get; set; }
        public string Reason { get; set; }
    }

    public class LogoutEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string EventName => "Logout";
    }

    #endregion

    #region Player Events

    public class SceneLoadedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string EventName => "SceneLoaded";
        public string SceneName { get; set; }
        public bool IsGameScene { get; set; }
    }


    public class ReducerErrorEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string EventName => "ReducerError";
        public string ReducerName { get; set; }
        public string Error { get; set; }
    }


    public class LocalPlayerCheckStartedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "LocalPlayerCheckStarted";
        public string Username { get; set; }
        public bool FoundExistingPlayer { get; set; }
    }

    public class LocalPlayerCreatedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "LocalPlayerCreated";
        public Player Player { get; set; }
        public bool IsNewPlayer { get; set; }
    }

    public class LocalPlayerRestoredEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "LocalPlayerRestored";
        public Player Player { get; set; }
        public Identity OldIdentity { get; set; }
        public Identity NewIdentity { get; set; }
    }

    public class LocalPlayerReadyEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "LocalPlayerReady";
        public Player Player { get; set; }
    }

    public class PlayerCreationFailedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "PlayerCreationFailed";
        public string Reason { get; set; }
    }

    public class PlayerCreationStartedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "PlayerCreationStarted";
        public string Username { get; set; }
    }

    public class LocalPlayerChangedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "LocalPlayerChanged";
        public Player Player { get; set; }
    }

    #endregion

    #region Scene Events

    public class SceneLoadStartedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "SceneLoadStarted";
        public string SceneName { get; set; }
        public WorldCoords TargetCoords { get; set; }
    }

    public class SceneLoadCompletedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "SceneLoadCompleted";
        public string SceneName { get; set; }
        public WorldCoords WorldCoords { get; set; }
    }

    #endregion

    #region World Events

    public class WorldLoadStartedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "WorldLoadStarted";
        public WorldCoords TargetWorld { get; set; }
    }

    public class WorldLoadedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "WorldLoaded";
        public World World { get; set; }
    }

    public class OrbsLoadedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "OrbsLoaded";
        public int OrbCount { get; set; }
        public WorldCoords WorldCoords { get; set; }
    }

    public class WorldLoadFailedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "WorldLoadFailed";
        public string Reason { get; set; }
    }

    public class WorldTransitionStartedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "WorldTransitionStarted";
        public WorldCoords FromWorld { get; set; }
        public WorldCoords ToWorld { get; set; }
    }

    #endregion

    #region System Events

    public class StateChangedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "StateChanged";
        public GameEventBus.GameState OldState { get; set; }
        public GameEventBus.GameState NewState { get; set; }
    }

    #endregion

    #region Orb Events

    public class WavePacketSourceInsertedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "OrbInserted";
        public WavePacketSource Source { get; set; }
    }

    public class WavePacketSourceUpdatedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "OrbUpdated";
        public WavePacketSource OldSource { get; set; }
        public WavePacketSource NewSource { get; set; }
    }

    public class WavePacketSourceDeletedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "OrbDeleted";
        public WavePacketSource Source { get; set; }
    }

    public class InitialSourcesLoadedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "InitialOrbsLoaded";
        public System.Collections.Generic.List<WavePacketSource> Sources { get; set; }
    }

    #endregion

    #region Energy Spire Events

    // Distribution Sphere events
    public class DistributionSphereInsertedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "DistributionSphereInserted";
        public DistributionSphere Sphere { get; set; }
    }

    public class DistributionSphereUpdatedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "DistributionSphereUpdated";
        public DistributionSphere OldSphere { get; set; }
        public DistributionSphere NewSphere { get; set; }
    }

    public class DistributionSphereDeletedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "DistributionSphereDeleted";
        public DistributionSphere Sphere { get; set; }
    }

    // Quantum Tunnel events
    public class QuantumTunnelInsertedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "QuantumTunnelInserted";
        public QuantumTunnel Tunnel { get; set; }
    }

    public class QuantumTunnelUpdatedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "QuantumTunnelUpdated";
        public QuantumTunnel OldTunnel { get; set; }
        public QuantumTunnel NewTunnel { get; set; }
    }

    public class QuantumTunnelDeletedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "QuantumTunnelDeleted";
        public QuantumTunnel Tunnel { get; set; }
    }

    // World Circuit events (optional component)
    public class WorldCircuitInsertedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "WorldCircuitInserted";
        public WorldCircuit Circuit { get; set; }
    }

    public class WorldCircuitUpdatedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "WorldCircuitUpdated";
        public WorldCircuit OldCircuit { get; set; }
        public WorldCircuit NewCircuit { get; set; }
    }

    public class WorldCircuitDeletedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "WorldCircuitDeleted";
        public WorldCircuit Circuit { get; set; }
    }

    // Initial load event for bulk spire loading
    public class InitialSpiresLoadedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "InitialSpiresLoaded";
        public System.Collections.Generic.List<DistributionSphere> Spheres { get; set; }
        public System.Collections.Generic.List<QuantumTunnel> Tunnels { get; set; }
        public System.Collections.Generic.List<WorldCircuit> Circuits { get; set; }
    }

    #endregion

    #region Storage Device Events

    /// <summary>
    /// Fired when a storage device is created in the database
    /// </summary>
    public class DeviceInsertedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "DeviceInserted";
        public StorageDevice Device { get; set; }
    }

    /// <summary>
    /// Fired when a storage device is updated (e.g., composition changed)
    /// </summary>
    public class DeviceUpdatedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "DeviceUpdated";
        public StorageDevice OldDevice { get; set; }
        public StorageDevice NewDevice { get; set; }
    }

    /// <summary>
    /// Fired when a storage device is deleted
    /// </summary>
    public class DeviceDeletedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "DeviceDeleted";
        public StorageDevice Device { get; set; }
    }

    /// <summary>
    /// Fired when initial storage devices are loaded for a player/world
    /// </summary>
    public class InitialDevicesLoadedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "InitialDevicesLoaded";
        public System.Collections.Generic.List<StorageDevice> Devices { get; set; }
    }

    #endregion

    #region Session Events

    /// <summary>
    /// Fired when a session is created after login
    /// </summary>
    public class SessionCreatedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "SessionCreated";

        public string Username { get; set; }
        public string SessionToken { get; set; }
        public Identity Identity { get; set; }
    }

    /// <summary>
    /// Fired when an existing session is restored
    /// </summary>
    public class SessionRestoredEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "SessionRestored";

        public string Username { get; set; }
        public string SessionToken { get; set; }
        public Identity Identity { get; set; }
    }

    #endregion

    // MVP: Add this event for when player doesn't exist yet
    public class LocalPlayerNotFoundEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "LocalPlayerNotFound";
        public string Username { get; set; }
    }

    // MVP: Add this event for subscription ready
    public class SubscriptionReadyEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "SubscriptionReady";
    }

    // MVP: Add this event for subscription errors
    public class SubscriptionErrorEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "SubscriptionError";
        public string Error { get; set; }
    }
    
    // System readiness event for dependency management
    public class SystemReadyEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "SystemReady";
        public string SystemName { get; set; }
        public bool IsReady { get; set; }
    }

    // Energy Transfer Events
    public class PacketTransferInsertedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "PacketTransferInserted";
        public PacketTransfer Transfer { get; set; }
    }

    public class PacketTransferUpdatedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "PacketTransferUpdated";
        public PacketTransfer OldTransfer { get; set; }
        public PacketTransfer NewTransfer { get; set; }
    }

    public class PacketTransferDeletedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "PacketTransferDeleted";
        public PacketTransfer Transfer { get; set; }
    }

    // Broadcast Chat Message Events (for in-game chat bubbles)
    public class BroadcastMessageReceivedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "BroadcastMessageReceived";
        public ulong MessageId { get; set; }
        public ulong SenderPlayerId { get; set; }
        public string SenderName { get; set; }
        public string Content { get; set; }
        public ulong SentAt { get; set; }
    }
}
