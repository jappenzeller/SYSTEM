using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Orchestrates all controller subscriptions
/// </summary>
public class SubscriptionOrchestrator : MonoBehaviour
{
    private static SubscriptionOrchestrator instance;
    public static SubscriptionOrchestrator Instance => instance;
    
    private List<SubscribableController> controllers = new List<SubscribableController>();
    private WorldCoords currentWorldCoords;
    
    [Header("Subscription Settings")]
    [SerializeField] private float resubscribeDelay = 0.5f; // Delay when changing worlds
    
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    void Start()
    {
        // Get initial world coordinates
        if (GameData.Instance != null)
        {
            currentWorldCoords = GameData.Instance.GetCurrentWorldCoords();
        }
        
        // Start checking for connection
        StartCoroutine(WaitForConnection());
        
        // Start monitoring world changes
        StartCoroutine(MonitorWorldChanges());
    }
    
    System.Collections.IEnumerator WaitForConnection()
    {
        // Wait for GameManager to be connected
        while (!GameManager.IsConnected())
        {
            yield return new WaitForSeconds(0.5f);
        }
        
        // Connected!
        OnConnected();
        
        // Monitor for disconnection
        StartCoroutine(MonitorConnection());
    }
    
    System.Collections.IEnumerator MonitorConnection()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            
            if (!GameManager.IsConnected() && controllers.Count > 0)
            {
                // We've disconnected
                OnDisconnected();
                
                // Wait for reconnection
                yield return WaitForConnection();
                break;
            }
        }
    }
    
    System.Collections.IEnumerator MonitorWorldChanges()
    {
        WorldCoords lastKnownCoords = currentWorldCoords;
        
        while (true)
        {
            yield return new WaitForSeconds(0.5f);
            
            if (GameData.Instance != null)
            {
                var newCoords = GameData.Instance.GetCurrentWorldCoords();
                
                // Check if world changed
                if (newCoords.X != lastKnownCoords.X || 
                    newCoords.Y != lastKnownCoords.Y || 
                    newCoords.Z != lastKnownCoords.Z)
                {
                    Debug.Log($"[SubscriptionOrchestrator] Detected world change to ({newCoords.X},{newCoords.Y},{newCoords.Z})");
                    lastKnownCoords = newCoords;
                    OnWorldChanged(newCoords);
                }
            }
        }
    }
    
    public void RegisterController(SubscribableController controller)
    {
        if (!controllers.Contains(controller))
        {
            controllers.Add(controller);
            Debug.Log($"[SubscriptionOrchestrator] Registered {controller.GetControllerName()}");
            
            // If already connected, subscribe immediately
            if (GameManager.IsConnected() && isActiveAndEnabled)
            {
                controller.Subscribe(currentWorldCoords);
            }
        }
    }
    
    public void UnregisterController(SubscribableController controller)
    {
        if (controllers.Remove(controller))
        {
            controller.Unsubscribe();
            Debug.Log($"[SubscriptionOrchestrator] Unregistered {controller.GetControllerName()}");
        }
    }
    
    void OnConnected()
    {
        Debug.Log("[SubscriptionOrchestrator] Connected! Subscribing all controllers...");
        SubscribeAllControllers();
    }
    
    void OnDisconnected()
    {
        Debug.Log("[SubscriptionOrchestrator] Disconnected! Unsubscribing all controllers...");
        UnsubscribeAllControllers();
    }
    
    void OnWorldChanged(WorldCoords newWorldCoords)
    {
        Debug.Log($"[SubscriptionOrchestrator] World changed to ({newWorldCoords.X},{newWorldCoords.Y},{newWorldCoords.Z})");
        currentWorldCoords = newWorldCoords;
        
        // Resubscribe all controllers with a small delay
        StartCoroutine(ResubscribeWithDelay());
    }
    
    System.Collections.IEnumerator ResubscribeWithDelay()
    {
        UnsubscribeAllControllers();
        yield return new WaitForSeconds(resubscribeDelay);
        SubscribeAllControllers();
    }
    
    void SubscribeAllControllers()
    {
        foreach (var controller in controllers)
        {
            if (controller != null)
            {
                controller.Subscribe(currentWorldCoords);
            }
        }
    }
    
    void UnsubscribeAllControllers()
    {
        foreach (var controller in controllers)
        {
            if (controller != null)
            {
                controller.Unsubscribe();
            }
        }
    }
    
    void OnDestroy()
    {
        UnsubscribeAllControllers();
        StopAllCoroutines();
    }
}