using UnityEngine;

public class CameraDebugger : MonoBehaviour
{
    private Camera mainCamera;
    private Transform followTarget;
    
    void Start()
    {
        Debug.Log("[CameraDebug] Camera debugger starting");
        
        mainCamera = GetComponent<Camera>();
        if (mainCamera == null)
        {
            Debug.LogError("[CameraDebug] No Camera component found!");
            return;
        }
        
        Debug.Log($"[CameraDebug] Camera position: {transform.position}");
        Debug.Log($"[CameraDebug] Camera parent: {transform.parent?.name ?? "None"}");
        
        // Check for Cinemachine components using string comparison
        var components = GetComponents<MonoBehaviour>();
        foreach (var comp in components)
        {
            Debug.Log($"[CameraDebug] Component found: {comp.GetType().Name}");
        }
    }
    
    void Update()
    {
        if (Time.frameCount % 60 == 0) // Log every second
        {
            Debug.Log($"[CameraDebug] Camera at: {transform.position}, Parent: {transform.parent?.name ?? "None"}");
            
            // Try multiple ways to find the player
            var player = GameObject.Find("Player(Clone)") 
                ?? GameObject.Find("LocalPlayer(Clone)")
                ?? GameObject.Find("superstringman")  // Your player name
                ?? GameObject.FindWithTag("Player")   // If tagged
                ?? FindFirstObjectByType<PlayerController>()?.gameObject;  // Find by component

            if (player != null)
            {
                Debug.Log($"[CameraDebug] Player found: {player.name} at: {player.transform.position}");
                Debug.Log($"[CameraDebug] Distance to player: {Vector3.Distance(transform.position, player.transform.position)}");
            }
            else
            {
                // List all GameObjects to see what's actually there
                Debug.LogWarning("[CameraDebug] No player found! Listing all root objects:");
                var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var obj in rootObjects)
                {
                    Debug.Log($"[CameraDebug] Root object: {obj.name}");
                    // Check children too
                    var playerController = obj.GetComponentInChildren<PlayerController>();
                    if (playerController != null)
                    {
                        Debug.Log($"[CameraDebug] Found PlayerController on: {playerController.gameObject.name}");
                    }
                }
            }
        }
    }
}