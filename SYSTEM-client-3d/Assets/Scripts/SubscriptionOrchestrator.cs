using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpacetimeDB.Types;

/// <summary>
/// Orchestrates all controller subscriptions - only active in GameScene
/// </summary>
public class SubscriptionOrchestrator : MonoBehaviour
{
    private static SubscriptionOrchestrator instance;
    public static SubscriptionOrchestrator Instance => instance;
    
    private List<SubscribableController> controllers = new List<SubscribableController>();
    private WorldCoords currentWorldCoords;
    
    [Header("Subscription Settings")]
    [SerializeField] private float resubscribeDelay = 0.5f; // Delay when changing worlds
    [SerializeField] private string gameSceneName = "GameScene"; // Only operate in this scene
    
    private bool isInGameScene = false;
    
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Subscribe to scene changes
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeAllControllers();
        StopAllCoroutines();
    }
    
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        isInGameScene = scene.name == gameSceneName;
        
        if (isInGameScene)
        {
            Debug.Log("[SubscriptionOrchestrator] Entered GameScene, starting orchestration");
            Start();
        }
        else
        {
            Debug.Log($"[SubscriptionOrchestrator] Not in GameScene ({scene.name}), stopping orchestration");
            StopAllCoroutines();
            UnsubscribeAllControllers();
        }
    }
    
    void Start()
    {
        // Only run if we're in the game scene
        if (!isInGameScene)
        {
            Debug.Log("[SubscriptionOrchestrator] Not in GameScene, skipping initialization");
            return;
        }
        
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
        
        // Also wait for local player to exist
        while (GameManager.GetLocalPlayer() == null)
        {
            yield return new WaitForSeconds(0.5f);
        }
        
        // Connected with player!
        OnConnected();
        
        // Monitor for disconnection
        StartCoroutine(MonitorConnection());
    }
    
    System.Collections.IEnumerator MonitorConnection()
    {
        while (isInGameScene)
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
        
        while (isInGameScene)
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
            
            // If already connected and in game scene, subscribe immediately
            if (isInGameScene && GameManager.IsConnected() && GameManager.GetLocalPlayer() != null)
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
        if (!isInGameScene) return;
        
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
        if (!isInGameScene) return;
        
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
}