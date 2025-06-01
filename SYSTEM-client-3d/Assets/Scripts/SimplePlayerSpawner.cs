using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Simple player spawner for testing - creates a local player representation
/// that can move around the sphere surface independently of SpacetimeDB
/// </summary>
public class SimplePlayerSpawner : MonoBehaviour
{
    [Header("Player Settings")]
    [Tooltip("Prefab for local player (optional - will create capsule if null)")]
    public GameObject playerPrefab;
    
    [Tooltip("Scale for the player")]
    public float playerScale = 1f;
    
    [Header("Camera Settings")]
    [Tooltip("Should we setup the camera to follow the player?")]
    public bool setupCamera = true;
    
    [Tooltip("Camera follow distance")]
    public float cameraDistance = 5f;
    
    [Tooltip("Camera height above player")]
    public float cameraHeight = 3f;

    // References
    private GameObject localPlayerObject;
    private WorldManager worldManager;
    private Camera playerCamera;

    void Start()
    {
        // Find world manager
        worldManager = FindFirstObjectByType<WorldManager>();
        if (worldManager == null)
        {
            Debug.LogError("SimplePlayerSpawner: No WorldManager found!");
            return;
        }

        // Find main camera
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindFirstObjectByType<Camera>();
        }

        // Only create player if we're not connected to SpacetimeDB or don't have a local player
        if (!GameManager.IsConnected() || !HasLocalPlayerInSpacetimeDB())
        {
            CreateLocalPlayer();
        }
        else
        {
            Debug.Log("SpacetimeDB player exists, not creating local test player");
        }
    }

    bool HasLocalPlayerInSpacetimeDB()
    {
        if (!GameManager.IsConnected()) return false;
        
        var localPlayer = GameManager.Instance.GetCurrentPlayer();
        return localPlayer != null;
    }

    void CreateLocalPlayer()
    {
        GameObject playerObj;
        
        if (playerPrefab != null)
        {
            // Use provided prefab
            playerObj = Instantiate(playerPrefab);
        }
        else
        {
            // Create a simple capsule as player
            playerObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            
            // Make it yellow to distinguish as local player
            var renderer = playerObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Standard"));
                material.color = Color.yellow;
                renderer.material = material;
            }
        }
        
        playerObj.name = "Local Test Player";
        playerObj.transform.localScale = Vector3.one * playerScale;
        
        // Position player on the sphere surface (random position for now)
        float worldRadius = worldManager.worldRadius;
        Vector3 randomDirection = Random.onUnitSphere;
        Vector3 surfacePosition = randomDirection * worldRadius;
        playerObj.transform.position = surfacePosition;
        
        // Orient player to stand on the surface
        playerObj.transform.LookAt(Vector3.zero);
        playerObj.transform.Rotate(-90f, 0f, 0f); // Stand upright
        
        // Add player controller
        var playerController = playerObj.GetComponent<SphericalPlayerController>();
        if (playerController == null)
        {
            playerController = playerObj.AddComponent<SphericalPlayerController>();
        }
        
        playerController.worldRadius = worldRadius;
        playerController.worldManager = worldManager;
        
        localPlayerObject = playerObj;
        
        // Setup camera
        if (setupCamera && playerCamera != null)
        {
            SetupCameraForPlayer();
            
            // Disable any orbital camera controls
            var orbitCamera = playerCamera.GetComponent<NewInputSystemCamera>();
            if (orbitCamera != null)
            {
                orbitCamera.enabled = false;
            }
        }
        
        Debug.Log($"Created local test player at position {surfacePosition}");
    }

    void SetupCameraForPlayer()
    {
        if (playerCamera == null || localPlayerObject == null) return;
        
        Vector3 playerPos = localPlayerObject.transform.position;
        Vector3 playerForward = localPlayerObject.transform.forward;
        Vector3 playerUp = localPlayerObject.transform.up;
        
        // Position camera behind and above the player
        Vector3 cameraOffset = 
            -playerForward * cameraDistance +     // Behind player
            playerUp * cameraHeight;              // Above player
        
        Vector3 cameraPosition = playerPos + cameraOffset;
        Vector3 lookTarget = playerPos + playerUp * (cameraHeight * 0.5f);
        
        playerCamera.transform.position = cameraPosition;
        playerCamera.transform.LookAt(lookTarget);
        
        Debug.Log($"Camera positioned at {cameraPosition}, looking at {lookTarget}");
    }

    // Public methods
    public GameObject GetLocalPlayer()
    {
        return localPlayerObject;
    }
    
    public void MovePlayerToPosition(Vector3 worldPosition)
    {
        if (localPlayerObject != null && worldManager != null)
        {
            Vector3 surfacePosition = worldManager.GetSurfacePosition(worldPosition);
            localPlayerObject.transform.position = surfacePosition;
            
            // Orient to surface
            localPlayerObject.transform.LookAt(Vector3.zero);
            localPlayerObject.transform.Rotate(-90f, 0f, 0f);
            
            // Update camera
            if (setupCamera)
            {
                SetupCameraForPlayer();
            }
        }
    }

    public void DestroyLocalPlayer()
    {
        if (localPlayerObject != null)
        {
            Destroy(localPlayerObject);
            localPlayerObject = null;
            
            // Re-enable orbital camera
            if (playerCamera != null)
            {
                var orbitCamera = playerCamera.GetComponent<NewInputSystemCamera>();
                if (orbitCamera != null)
                {
                    orbitCamera.enabled = true;
                }
            }
        }
    }

    void OnGUI()
    {
        if (localPlayerObject != null)
        {
            if (GUI.Button(new Rect(10, 150, 150, 30), "Move to Random Pos"))
            {
                Vector3 randomPos = Random.onUnitSphere;
                MovePlayerToPosition(randomPos);
            }
            
            if (GUI.Button(new Rect(10, 190, 150, 30), "Destroy Test Player"))
            {
                DestroyLocalPlayer();
            }
            
            GUI.Label(new Rect(10, 230, 300, 40), 
                $"Test Player Position: {localPlayerObject.transform.position.ToString("F1")}");
        }
        else if (!HasLocalPlayerInSpacetimeDB())
        {
            if (GUI.Button(new Rect(10, 150, 150, 30), "Create Test Player"))
            {
                CreateLocalPlayer();
            }
        }
    }

    void OnDestroy()
    {
        DestroyLocalPlayer();
    }
}