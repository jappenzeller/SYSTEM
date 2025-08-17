using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpacetimeDB.Types
{
    /// <summary>
    /// Centralized event system for managing game state and event flow
    /// </summary>
    public class GameEventBus : MonoBehaviour
    {
        private static GameEventBus instance;
        public static GameEventBus Instance
        {
            get
            {
                if (instance == null)
                {
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

        // State (basic for now, not enforced yet)
        public enum GameState
        {
            Disconnected,
            Connecting,
            Connected,
            Subscribing,
            TablesReady,
            Authenticated,
            PlayerReady,
            WorldReady,
            InGame
        }

        private GameState currentState = GameState.Disconnected;
        public GameState CurrentState => currentState;

        #region Core Event System

        /// <summary>
        /// Publish an event to all subscribers
        /// </summary>
        public void Publish<T>(T eventData) where T : IGameEvent
        {
            if (eventData == null)
            {
                Debug.LogError("[EventBus] Attempted to publish null event");
                return;
            }

            // Set timestamp if not already set
            if (eventData.Timestamp == default(DateTime))
            {
                eventData.Timestamp = DateTime.Now;
            }

            // Log the event
            LogEvent(eventData);

            // Dispatch to handlers
            lock (eventLock)
            {
                Type eventType = typeof(T);
                if (eventHandlers.ContainsKey(eventType) && eventHandlers[eventType] != null)
                {
                    // Create a copy to avoid modification during iteration
                    List<Delegate> handlers = new List<Delegate>(eventHandlers[eventType]);

                    foreach (Delegate handler in handlers)
                    {
                        try
                        {
                            (handler as Action<T>)?.Invoke(eventData);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[EventBus] Error in handler for {eventType.Name}: {e.Message}\n{e.StackTrace}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Subscribe to an event type
        /// </summary>
        public void Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (handler == null)
            {
                Debug.LogError("[EventBus] Attempted to subscribe null handler");
                return;
            }

            lock (eventLock)
            {
                Type eventType = typeof(T);
                if (!eventHandlers.ContainsKey(eventType))
                {
                    eventHandlers[eventType] = new List<Delegate>();
                }

                if (!eventHandlers[eventType].Contains(handler))
                {
                    eventHandlers[eventType].Add(handler);
                    if (enableLogging)
                    {
                        Debug.Log($"[EventBus] Subscribed to {eventType.Name} (Total handlers: {eventHandlers[eventType].Count})");
                    }
                }
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
                        Debug.Log($"[EventBus] Unsubscribed from {eventType.Name} (Remaining handlers: {eventHandlers[eventType].Count})");
                    }
                }
            }
        }

        #endregion

        #region State Management (Basic for now)

        /// <summary>
        /// Set the current game state (no validation yet)
        /// </summary>
        public void SetState(GameState newState)
        {
            if (currentState != newState)
            {
                GameState oldState = currentState;
                currentState = newState;

                if (enableLogging)
                {
                    Debug.Log($"[EventBus] State changed: {oldState} → {newState}");
                }
            }
        }

        #endregion

        #region Logging and Debug

        private void LogEvent<T>(T eventData) where T : IGameEvent
        {
            if (!enableLogging) return;

            string eventInfo = $"[EventBus] EVENT: {eventData.EventName} at {eventData.Timestamp:HH:mm:ss.fff}";

            // Add additional info based on event type (we'll expand this)
            if (eventData is LoginSuccessfulEvent login)
            {
                eventInfo += $" - User: {login.Username}";
            }

            Debug.Log(eventInfo);

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
        /// Dump the event history to console
        /// </summary>
        public void DumpEventHistory()
        {
            Debug.Log($"[EventBus] === Event History ({eventHistory.Count} events) ===");
            foreach (var entry in eventHistory)
            {
                Debug.Log($"  {entry.Timestamp:HH:mm:ss.fff} [{entry.State}] {entry.EventName}");
            }
            Debug.Log("[EventBus] === End Event History ===");
        }

        /// <summary>
        /// Clear all event handlers (useful for testing)
        /// </summary>
        public void ClearAllHandlers()
        {
            lock (eventLock)
            {
                eventHandlers.Clear();
                Debug.LogWarning("[EventBus] All event handlers cleared");
            }
        }

        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[EventBus] GameEventBus initialized");
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

    #region Event Interfaces

    /// <summary>
    /// Base interface for all game events
    /// </summary>
    public interface IGameEvent
    {
        DateTime Timestamp { get; set; }
        string EventName { get; }
    }

    #endregion

    #region Login Events

    /// <summary>
    /// Fired when login is successful and session is established
    /// </summary>
    public class LoginSuccessfulEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "LoginSuccessful";

        public string Username { get; set; }
        public ulong AccountId { get; set; }
        public string SessionToken { get; set; }
    }

    /// <summary>
    /// Fired when an existing session is restored
    /// </summary>
    public class SessionRestoredEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "SessionRestored";

        public string Username { get; set; }
        public ulong AccountId { get; set; }
    }

    /// <summary>
    /// Fired when the system starts checking for local player
    /// </summary>
    public class LocalPlayerCheckStartedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "LocalPlayerCheckStarted";

        public string Username { get; set; }
        public bool FoundExistingPlayer { get; set; }
    }

    #endregion

// Add these event types to GameEventBus.cs, right after the LocalPlayerCheckStartedEvent class
// and before the closing #endregion and closing brace

    #region Player Events

    /// <summary>
    /// Fired when a player is created for the first time
    /// </summary>
    public class LocalPlayerCreatedEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "LocalPlayerCreated";
        
        public Player Player { get; set; }
        public bool IsNewPlayer { get; set; }
    }

    /// <summary>
    /// Fired when an existing player is restored to a new identity
    /// </summary>
    public class LocalPlayerRestoredEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "LocalPlayerRestored";
        
        public Player Player { get; set; }
        public Identity OldIdentity { get; set; }
        public Identity NewIdentity { get; set; }
    }

    /// <summary>
    /// Fired when the local player is fully ready (created or restored)
    /// </summary>
    public class LocalPlayerReadyEvent : IGameEvent
    {
        public DateTime Timestamp { get; set; }
        public string EventName => "LocalPlayerReady";
        
        public Player Player { get; set; }
    }

    #endregion
}