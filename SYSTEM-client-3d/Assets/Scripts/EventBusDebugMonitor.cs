using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Debug component to monitor EventBus events
/// </summary>
public class EventBusDebugMonitor : MonoBehaviour
{
    void Start()
    {
        Debug.Log("[EventBusMonitor] Starting event monitoring...");
        
        // Subscribe to all events we want to monitor
        GameEventBus.Instance.Subscribe<LoginSuccessfulEvent>(OnLoginSuccessful);
        GameEventBus.Instance.Subscribe<LocalPlayerCheckStartedEvent>(OnPlayerCheckStarted);
        GameEventBus.Instance.Subscribe<LocalPlayerCreatedEvent>(OnPlayerCreated);
        GameEventBus.Instance.Subscribe<LocalPlayerRestoredEvent>(OnPlayerRestored);
        GameEventBus.Instance.Subscribe<LocalPlayerReadyEvent>(OnPlayerReady);
        
        // Dump any existing history
        GameEventBus.Instance.DumpEventHistory();
    }
    
    void OnDestroy()
    {
        // Unsubscribe
        GameEventBus.Instance.Unsubscribe<LoginSuccessfulEvent>(OnLoginSuccessful);
        GameEventBus.Instance.Unsubscribe<LocalPlayerCheckStartedEvent>(OnPlayerCheckStarted);
        GameEventBus.Instance.Unsubscribe<LocalPlayerCreatedEvent>(OnPlayerCreated);
        GameEventBus.Instance.Unsubscribe<LocalPlayerRestoredEvent>(OnPlayerRestored);
        GameEventBus.Instance.Unsubscribe<LocalPlayerReadyEvent>(OnPlayerReady);
    }
    
    void OnLoginSuccessful(LoginSuccessfulEvent evt)
    {
        Debug.Log($"[EventBusMonitor] LOGIN SUCCESSFUL: User={evt.Username}, Token={evt.SessionToken?.Substring(0, 20)}...");
    }
    
    void OnPlayerCheckStarted(LocalPlayerCheckStartedEvent evt)
    {
        Debug.Log($"[EventBusMonitor] PLAYER CHECK: User={evt.Username}, Found={evt.FoundExistingPlayer}");
    }
    
    void OnPlayerCreated(LocalPlayerCreatedEvent evt)
    {
        Debug.Log($"[EventBusMonitor] PLAYER CREATED: Name={evt.Player.Name}, ID={evt.Player.PlayerId}, New={evt.IsNewPlayer}");
    }
    
    void OnPlayerRestored(LocalPlayerRestoredEvent evt)
    {
        Debug.Log($"[EventBusMonitor] PLAYER RESTORED: Name={evt.Player.Name}, OldIdentity={evt.OldIdentity}, NewIdentity={evt.NewIdentity}");
    }
    
    void OnPlayerReady(LocalPlayerReadyEvent evt)
    {
        Debug.Log($"[EventBusMonitor] PLAYER READY: Name={evt.Player.Name}, ID={evt.Player.PlayerId}, World=({evt.Player.CurrentWorld.X},{evt.Player.CurrentWorld.Y},{evt.Player.CurrentWorld.Z})");
        Debug.Log($"[EventBusMonitor] >>> READY TO LOAD GAME SCENE <<<");
    }
}