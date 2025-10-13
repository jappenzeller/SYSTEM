using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpacetimeDB.Types
{
    /// <summary>
    /// Centralized event system with state-driven validation
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



        // Events allowed in each state
        private readonly Dictionary<GameState, HashSet<Type>> allowedEventsPerState = new Dictionary<GameState, HashSet<Type>>
            {
                { GameState.Disconnected, new HashSet<Type> {
                    typeof(ConnectionStartedEvent),
                    typeof(SystemReadyEvent)
                }},
                { GameState.Connecting, new HashSet<Type> {
                    typeof(ConnectionEstablishedEvent),
                    typeof(ConnectionFailedEvent),
                    typeof(SystemReadyEvent)
                }},
                { GameState.Connected, new HashSet<Type> {
                    typeof(SubscriptionReadyEvent),
                    typeof(ConnectionLostEvent),
                    typeof(SystemReadyEvent)
                }},
                { GameState.CheckingPlayer, new HashSet<Type> {
                    typeof(LocalPlayerCheckStartedEvent),
                    typeof(LocalPlayerNotFoundEvent),
                    typeof(LocalPlayerReadyEvent),
                    typeof(LocalPlayerRestoredEvent),
                    typeof(ConnectionLostEvent),
                    typeof(SystemReadyEvent)
                }},
                { GameState.WaitingForLogin, new HashSet<Type> {
                    typeof(LoginStartedEvent),
                    typeof(ConnectionLostEvent),
                    typeof(SystemReadyEvent)
                }},
                { GameState.Authenticating, new HashSet<Type> {
                    typeof(LoginSuccessfulEvent),
                    typeof(LoginFailedEvent),
                    typeof(ConnectionLostEvent),
                    typeof(SystemReadyEvent)
                }},
                { GameState.Authenticated, new HashSet<Type> {
                    typeof(LocalPlayerCheckStartedEvent),
                    typeof(PlayerCreationStartedEvent),
                    typeof(LocalPlayerReadyEvent),
                    typeof(SessionCreatedEvent),
                    typeof(SessionRestoredEvent),
                    typeof(ConnectionLostEvent),
                    typeof(SystemReadyEvent)
                }},
                { GameState.CreatingPlayer, new HashSet<Type> {
                    typeof(LocalPlayerCreatedEvent),
                    typeof(LocalPlayerRestoredEvent),
                    typeof(PlayerCreationFailedEvent),
                    typeof(ConnectionLostEvent),
                    typeof(SystemReadyEvent)
                }},
                { GameState.PlayerReady, new HashSet<Type> {
                    typeof(WorldLoadStartedEvent),
                    typeof(WorldLoadedEvent),  // Allow WorldLoadedEvent in PlayerReady state
                    typeof(SceneLoadStartedEvent),
                    typeof(SceneLoadedEvent),
                    typeof(SceneLoadCompletedEvent),
                    typeof(ConnectionLostEvent),
                    typeof(SystemReadyEvent),
                    // Orb events can start loading when player is ready
                    typeof(InitialOrbsLoadedEvent),
                    typeof(OrbInsertedEvent),
                    typeof(OrbUpdatedEvent),
                    typeof(OrbDeletedEvent),
                    // Energy Spire events
                    typeof(InitialSpiresLoadedEvent),
                    typeof(DistributionSphereInsertedEvent),
                    typeof(DistributionSphereUpdatedEvent),
                    typeof(DistributionSphereDeletedEvent),
                    typeof(QuantumTunnelInsertedEvent),
                    typeof(QuantumTunnelUpdatedEvent),
                    typeof(QuantumTunnelDeletedEvent),
                    typeof(WorldCircuitInsertedEvent),
                    typeof(WorldCircuitUpdatedEvent),
                    typeof(WorldCircuitDeletedEvent),
                    // Circuit events
                    typeof(SYSTEM.Circuits.CircuitInsertedEvent),
                    typeof(SYSTEM.Circuits.CircuitUpdatedEvent),
                    typeof(SYSTEM.Circuits.CircuitDeletedEvent),
                    typeof(SYSTEM.Circuits.TunnelFormedEvent),
                    typeof(SYSTEM.Circuits.TunnelBrokenEvent)
                }},
                { GameState.LoadingWorld, new HashSet<Type> {
                    typeof(WorldLoadedEvent),
                    typeof(WorldLoadFailedEvent),
                    typeof(ConnectionLostEvent),
                    typeof(SystemReadyEvent),
                    // Orb events during world loading
                    typeof(InitialOrbsLoadedEvent),
                    typeof(OrbInsertedEvent),
                    typeof(OrbUpdatedEvent),
                    typeof(OrbDeletedEvent),
                    // Energy Spire events
                    typeof(InitialSpiresLoadedEvent),
                    typeof(DistributionSphereInsertedEvent),
                    typeof(DistributionSphereUpdatedEvent),
                    typeof(DistributionSphereDeletedEvent),
                    typeof(QuantumTunnelInsertedEvent),
                    typeof(QuantumTunnelUpdatedEvent),
                    typeof(QuantumTunnelDeletedEvent),
                    typeof(WorldCircuitInsertedEvent),
                    typeof(WorldCircuitUpdatedEvent),
                    typeof(WorldCircuitDeletedEvent),
                    // Circuit events
                    typeof(SYSTEM.Circuits.CircuitInsertedEvent),
                    typeof(SYSTEM.Circuits.CircuitUpdatedEvent),
                    typeof(SYSTEM.Circuits.CircuitDeletedEvent),
                    typeof(SYSTEM.Circuits.TunnelFormedEvent),
                    typeof(SYSTEM.Circuits.TunnelBrokenEvent)
                }},
                { GameState.InGame, new HashSet<Type> {
                    typeof(WorldTransitionStartedEvent),
                    typeof(ConnectionLostEvent),
                    typeof(SceneLoadStartedEvent),
                    typeof(SceneLoadCompletedEvent),
                    typeof(SceneLoadedEvent),
                    typeof(LocalPlayerCreatedEvent),
                    typeof(LocalPlayerRestoredEvent),
                    typeof(LocalPlayerReadyEvent),
                    typeof(SystemReadyEvent),
                    typeof(WorldLoadStartedEvent),
                    typeof(WorldLoadedEvent),
                    // Orb events
                    typeof(InitialOrbsLoadedEvent),
                    typeof(OrbInsertedEvent),
                    typeof(OrbUpdatedEvent),
                    typeof(OrbDeletedEvent),
                    // Energy Spire events
                    typeof(InitialSpiresLoadedEvent),
                    typeof(DistributionSphereInsertedEvent),
                    typeof(DistributionSphereUpdatedEvent),
                    typeof(DistributionSphereDeletedEvent),
                    typeof(QuantumTunnelInsertedEvent),
                    typeof(QuantumTunnelUpdatedEvent),
                    typeof(QuantumTunnelDeletedEvent),
                    typeof(WorldCircuitInsertedEvent),
                    typeof(WorldCircuitUpdatedEvent),
                    typeof(WorldCircuitDeletedEvent),
                    // Circuit events
                    typeof(SYSTEM.Circuits.CircuitInsertedEvent),
                    typeof(SYSTEM.Circuits.CircuitUpdatedEvent),
                    typeof(SYSTEM.Circuits.CircuitDeletedEvent),
                    typeof(SYSTEM.Circuits.TunnelFormedEvent),
                    typeof(SYSTEM.Circuits.TunnelBrokenEvent)
                }}
            };

        #region Core Event System

        /// <summary>
        /// Publish an event with state validation
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

            // Validate event is allowed in current state
            if (!IsEventAllowedInCurrentState(eventType))
            {
                SystemDebug.LogWarning(SystemDebug.Category.EventBus, $"Event {eventType.Name} not allowed in state {currentState}");
                return false;
            }

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
                SystemDebug.Log(SystemDebug.Category.EventBus, $"Executing {handlers.Count} handlers for {eventType.Name}");
                foreach (var handler in handlers)
                {
                    try
                    {
                        handler(eventData);
                    }
                    catch (Exception e)
                    {
                        SystemDebug.LogError(SystemDebug.Category.EventBus, $"Error in handler for {eventType.Name}: {e.Message}\n{e.StackTrace}");
                    }
                }
            }
            else
            {
                SystemDebug.Log(SystemDebug.Category.EventBus, $"No handlers registered for {eventType.Name}");
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
        /// Check if an event type is allowed in the current state
        /// </summary>
        private bool IsEventAllowedInCurrentState(Type eventType)
        {
            // Always allow state change events
            if (eventType == typeof(StateChangedEvent)) return true;

            if (!allowedEventsPerState.ContainsKey(currentState))
            {
                // If state not configured, allow all events (for backward compatibility)
                return true;
            }

            return allowedEventsPerState[currentState].Contains(eventType);
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

    public class OrbInsertedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "OrbInserted";
        public WavePacketOrb Orb { get; set; }
    }

    public class OrbUpdatedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "OrbUpdated";
        public WavePacketOrb OldOrb { get; set; }
        public WavePacketOrb NewOrb { get; set; }
    }

    public class OrbDeletedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "OrbDeleted";
        public WavePacketOrb Orb { get; set; }
    }

    public class InitialOrbsLoadedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "InitialOrbsLoaded";
        public System.Collections.Generic.List<WavePacketOrb> Orbs { get; set; }
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
}
