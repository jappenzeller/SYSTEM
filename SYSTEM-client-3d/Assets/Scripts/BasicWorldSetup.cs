using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Sets up the basic world sphere and player representation
/// </summary>
public class BasicWorldSetup : MonoBehaviour
{
    [Header("World Settings")]
    [Tooltip("Radius of the world sphere")]
    public float worldRadius = 100f;
    
    [Tooltip("Material for the world sphere (optional)")]
    public Material worldMaterial;

    [Header("Player Settings")]
    [Tooltip("Prefab for player representation (optional)")]
    public GameObject playerPrefab;
    
    [Tooltip("Scale for player objects")]
    public float playerScale = 1f;

    [Header("Visual Settings")]
    [Tooltip("Should the world sphere rotate slowly?")]
    public bool rotateWorld = true;
    
    [Tooltip("Rotation speed for the world")]
    public float rotationSpeed = 2f;

    // Private variables
    private GameObject worldSphere;
    private GameObject localPlayerObject;

    void Start()
    {
        CreateWorldSphere();
        CreateLocalPlayer();
        
        // Position camera near the player and set orbit center
        SetupCameraForPlayer();
    }
    
    void SetupCameraForPlayer()
    {
        var camera = Camera.main;
        if (camera != null && localPlayerObject != null)
        {
            Vector3 playerPos = localPlayerObject.transform.position;
            
            // Get player's forward direction (away from sphere center)
            Vector3 playerForward = localPlayerObject.transform.forward;
            Vector3 playerUp = localPlayerObject.transform.up;
            
            // Calculate player height (assume capsule height)
            float playerHeight = 2f; // Standard capsule height
            
            // Position camera behind and above the player
            Vector3 cameraOffset = 
                -playerForward * playerHeight +           // One player height behind
                playerUp * (playerHeight * 2f);           // Twice player height above
            
            Vector3 cameraPosition = playerPos + cameraOffset;
            
            // Look toward the player (slightly above their head)
            Vector3 lookTarget = playerPos + playerUp * (playerHeight * 0.8f);
            
            camera.transform.position = cameraPosition;
            camera.transform.LookAt(lookTarget);
            
            // Disable the orbit camera controls - camera is bound to player
            var inputCamera = camera.GetComponent<NewInputSystemCamera>();
            if (inputCamera != null)
            {
                inputCamera.enabled = false; // Disable orbit controls
            }
            
            Debug.Log($"Over-shoulder camera positioned at {cameraPosition}, looking at player head at {lookTarget}");
        }
    }

    void Update()
    {
        // Rotate the world slowly if enabled
        if (rotateWorld && worldSphere != null)
        {
            worldSphere.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
    }

    void CreateWorldSphere()
    {
        // Create the world sphere
        worldSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        worldSphere.name = "World Sphere";
        worldSphere.transform.position = Vector3.zero;
        worldSphere.transform.localScale = Vector3.one * worldRadius * 2f; // Scale is diameter
        
        // Apply material or create a basic one
        var renderer = worldSphere.GetComponent<Renderer>();
        if (worldMaterial != null)
        {
            renderer.material = worldMaterial;
        }
        else
        {
            // Create a simple brown world material using the most basic shader
            Material basicWorldMaterial = new Material(Shader.Find("Unlit/Color"));
            
            // If even Unlit/Color fails, try the absolute fallback
            if (basicWorldMaterial.shader == null || basicWorldMaterial.shader.name.Contains("Error"))
            {
                basicWorldMaterial.shader = Shader.Find("Sprites/Default");
            }
            
            // Set brown color
            basicWorldMaterial.color = new Color(0.6f, 0.4f, 0.2f); // Brown color
            renderer.material = basicWorldMaterial;
            
            Debug.Log($"World material using shader: {basicWorldMaterial.shader.name}");
        }
        
        // Remove the collider since we don't need physics
        var collider = worldSphere.GetComponent<SphereCollider>();
        if (collider != null)
        {
            DestroyImmediate(collider);
        }
        
        // Make sure camera starts outside the sphere
        SetupCameraForPlayer();
        
        Debug.Log($"Created world sphere with radius {worldRadius}");
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
            Material playerMaterial = new Material(Shader.Find("Unlit/Color"));
            
            // Fallback shader if Unlit/Color fails
            if (playerMaterial.shader == null || playerMaterial.shader.name.Contains("Error"))
            {
                playerMaterial.shader = Shader.Find("Sprites/Default");
            }
            
            playerMaterial.color = Color.yellow;
            renderer.material = playerMaterial;
        }
        
        playerObj.name = "Local Player";
        playerObj.transform.localScale = Vector3.one * playerScale;
        
        // Position player on the sphere surface (random position for now)
        Vector3 randomDirection = Random.onUnitSphere;
        Vector3 surfacePosition = randomDirection * worldRadius;
        playerObj.transform.position = surfacePosition;
        
        // Orient player to stand on the surface
        playerObj.transform.LookAt(Vector3.zero);
        playerObj.transform.Rotate(-90f, 0f, 0f); // Stand upright
        
        // Add player controller
        var playerController = playerObj.AddComponent<SphericalPlayerController>();
        playerController.worldRadius = worldRadius;
        playerController.worldSetup = this;
        
        localPlayerObject = playerObj;
        
        Debug.Log($"Created local player with controller at position {surfacePosition}");
    }

    // Public methods for external control
    public Vector3 GetWorldCenter()
    {
        return Vector3.zero;
    }
    
    public float GetWorldRadius()
    {
        return worldRadius;
    }
    
    public Vector3 GetSurfacePosition(Vector3 worldPosition)
    {
        return worldPosition.normalized * worldRadius;
    }
    
    public GameObject GetLocalPlayer()
    {
        return localPlayerObject;
    }
    
    public void MovePlayerToPosition(Vector3 worldPosition)
    {
        if (localPlayerObject != null)
        {
            Vector3 surfacePosition = GetSurfacePosition(worldPosition);
            localPlayerObject.transform.position = surfacePosition;
            
            // Orient to surface
            localPlayerObject.transform.LookAt(Vector3.zero);
            localPlayerObject.transform.Rotate(-90f, 0f, 0f);
            
            // Update camera to follow player
            UpdateCameraPosition();
        }
    }
    
    public void UpdateCameraPosition()
    {
        var camera = Camera.main;
        if (camera != null && localPlayerObject != null)
        {
            Vector3 playerPos = localPlayerObject.transform.position;
            Vector3 playerForward = localPlayerObject.transform.forward;
            Vector3 playerUp = localPlayerObject.transform.up;
            
            float playerHeight = 2f;
            
            // Recalculate camera position relative to player
            Vector3 cameraOffset = 
                -playerForward * playerHeight +           // Behind player
                playerUp * (playerHeight * 2f);           // Above player
            
            Vector3 cameraPosition = playerPos + cameraOffset;
            Vector3 lookTarget = playerPos + playerUp * (playerHeight * 0.8f);
            
            camera.transform.position = cameraPosition;
            camera.transform.LookAt(lookTarget);
        }
    }

    // Create a simple test orb (for testing without server)
    public void CreateTestOrb()
    {
        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "Test Orb";
        orb.transform.position = Vector3.up * 50f; // Above center
        orb.transform.localScale = Vector3.one * 0.5f;
        
        // Make it glow red
        var renderer = orb.GetComponent<Renderer>();
        Material orbMaterial = new Material(Shader.Find("Unlit/Color"));
        
        // Fallback shader
        if (orbMaterial.shader == null || orbMaterial.shader.name.Contains("Error"))
        {
            orbMaterial.shader = Shader.Find("Sprites/Default");
        }
        
        orbMaterial.color = Color.red;
        renderer.material = orbMaterial;
        
        // Add simple falling physics
        var rb = orb.AddComponent<Rigidbody>();
        rb.useGravity = false; // We'll use custom gravity toward center
        
        // Add a simple script to fall toward sphere
        orb.AddComponent<FallToSphere>();
        
        Debug.Log("Created test orb");
    }

    void OnGUI()
    {
        // Simple controls
        if (GUI.Button(new Rect(10, 150, 120, 30), "Create Test Orb"))
        {
            CreateTestOrb();
        }
        
        if (GUI.Button(new Rect(10, 190, 120, 30), "Move Player"))
        {
            // Move player to a random position
            Vector3 randomPos = Random.onUnitSphere * worldRadius;
            MovePlayerToPosition(randomPos);
        }
        
        GUI.Label(new Rect(10, 230, 200, 60), 
            $"World Radius: {worldRadius}\n" +
            $"Player Position: {(localPlayerObject != null ? localPlayerObject.transform.position.ToString("F1") : "None")}");
    }
}

/// <summary>
/// Simple script to make objects fall toward the sphere center
/// </summary>
public class FallToSphere : MonoBehaviour
{
    public float fallSpeed = 20f;
    public float worldRadius = 100f;
    
    private Rigidbody rb;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // Find the world setup to get radius
        var worldSetup = FindFirstObjectByType<BasicWorldSetup>();
        if (worldSetup != null)
        {
            worldRadius = worldSetup.GetWorldRadius();
        }
    }
    
    void FixedUpdate()
    {
        if (rb != null)
        {
            // Apply gravity toward center
            Vector3 directionToCenter = (Vector3.zero - transform.position).normalized;
            rb.AddForce(directionToCenter * fallSpeed, ForceMode.Acceleration);
            
            // Check if we hit the surface
            float distanceFromCenter = transform.position.magnitude;
            if (distanceFromCenter <= worldRadius + 1f)
            {
                // Hit the surface - stop or create puddle effect
                Debug.Log("Orb hit surface!");
                
                // For now, just destroy the orb
                Destroy(gameObject);
            }
        }
    }
}