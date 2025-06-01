using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player controller for movement on spherical world surface using new Input System
/// </summary>
public class SphericalPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Speed of movement on sphere surface")]
    public float moveSpeed = 5f;
    
    [Tooltip("How quickly player rotates to face movement direction")]
    public float rotationSpeed = 10f;
    
    [Tooltip("Radius of the world sphere")]
    public float worldRadius = 100f;

    [Header("References")]
    [Tooltip("Reference to WorldManager to get world info")]
    public WorldManager worldManager;

    // Input Actions
    private InputAction moveAction;
    
    // Movement state
    private Vector2 moveInput;
    private bool isMoving = false;

    void Start()
    {
        // Find world manager if not assigned
        if (worldManager == null)
        {
            worldManager = FindFirstObjectByType<WorldManager>();
        }
        
        // Get world radius from world manager if available
        if (worldManager != null)
        {
            worldRadius = worldManager.worldRadius;
        }
        
        // Setup input actions
        SetupInputActions();
        
        // Ensure we're on the surface
        SnapToSurface();
        
        Debug.Log("Spherical player controller initialized with world radius: " + worldRadius);
    }

    void SetupInputActions()
    {
        // Create WASD movement input action
        moveAction = new InputAction("Move", InputActionType.Value);
        
        // Add WASD bindings
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
            
        // Add arrow key bindings as alternative
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
        
        moveAction.Enable();
    }

    void Update()
    {
        HandleInput();
        HandleMovement();
    }

    void HandleInput()
    {
        // Get movement input
        moveInput = moveAction.ReadValue<Vector2>();
        isMoving = moveInput.magnitude > 0.1f;
    }

    void HandleMovement()
    {
        if (!isMoving) return;

        Vector3 currentPos = transform.position;
        Vector3 surfaceNormal = currentPos.normalized; // Normal pointing away from world center
        
        // Create movement relative to camera view for better control
        Camera playerCamera = Camera.main;
        if (playerCamera == null) return;
        
        Vector3 cameraForward = playerCamera.transform.forward;
        Vector3 cameraRight = playerCamera.transform.right;
        
        // Project camera directions onto the sphere's tangent plane
        Vector3 tangentForward = Vector3.ProjectOnPlane(cameraForward, surfaceNormal).normalized;
        Vector3 tangentRight = Vector3.ProjectOnPlane(cameraRight, surfaceNormal).normalized;
        
        // Calculate movement direction on sphere surface
        Vector3 moveDirection = (tangentForward * moveInput.y + tangentRight * moveInput.x).normalized;
        
        if (moveDirection.magnitude > 0.1f)
        {
            // Move along the sphere surface
            Vector3 newPosition = currentPos + moveDirection * moveSpeed * Time.deltaTime;
            
            // Project back onto sphere surface
            newPosition = newPosition.normalized * worldRadius;
            transform.position = newPosition;
            
            // Rotate player to face movement direction
            Vector3 newSurfaceNormal = newPosition.normalized;
            Vector3 lookDirection = Vector3.ProjectOnPlane(moveDirection, newSurfaceNormal);
            
            if (lookDirection.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection, newSurfaceNormal);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
            
            // Update camera to follow player
            UpdateCameraPosition();
        }
    }

    void SnapToSurface()
    {
        Vector3 currentPos = transform.position;
        Vector3 surfacePosition = currentPos.normalized * worldRadius;
        transform.position = surfacePosition;
        
        // Orient to surface
        Vector3 surfaceNormal = surfacePosition.normalized;
        transform.rotation = Quaternion.LookRotation(transform.forward, surfaceNormal);
    }

    // In SphericalPlayerController.cs, modify UpdateCameraPosition:
    void UpdateCameraPosition()
    {
        Debug.Log("[SphericalPlayerController] UpdateCameraPosition called");
        
        Camera playerCamera = Camera.main;
        if (playerCamera == null) return;
        
        // Add debug
        Debug.Log($"[SphericalPlayerController] Moving camera from {playerCamera.transform.position}");
        
        Vector3 playerPos = transform.position;
        Vector3 playerForward = transform.forward;
        Vector3 playerUp = transform.up;
        
        float playerHeight = 2f; // Standard capsule height
        
        // Position camera behind and above the player
        Vector3 cameraOffset = 
            -playerForward * playerHeight +           // One player height behind
            playerUp * (playerHeight * 2f);           // Twice player height above
        
        Vector3 cameraPosition = playerPos + cameraOffset;
        
        // Look toward the player (slightly above their head)
        Vector3 lookTarget = playerPos + playerUp * (playerHeight * 0.8f);
        
        playerCamera.transform.position = cameraPosition;
        playerCamera.transform.LookAt(lookTarget);
        
        Debug.Log($"[SphericalPlayerController] Moved camera to {cameraPosition}, looking at {lookTarget}");
    }

    // Public method to teleport player to a position
    public void TeleportToPosition(Vector3 worldPosition)
    {
        Vector3 surfacePos = worldPosition.normalized * worldRadius;
        transform.position = surfacePos;
        SnapToSurface();
        
        // Update camera
        UpdateCameraPosition();
    }

    public void SetWorldRadius(float newRadius)
    {
        worldRadius = newRadius;
        SnapToSurface(); // Re-snap to surface with new radius
    }

    void OnGUI()
    {
        // Show movement controls
        GUI.Label(new Rect(10, 300, 200, 80), 
            "Player Controls:\n" +
            "WASD / Arrow Keys: Move\n" +
            $"Speed: {moveSpeed:F1}\n" +
            $"World Radius: {worldRadius:F1}");
    }

    void OnDestroy()
    {
        // Clean up input actions
        moveAction?.Disable();
        moveAction?.Dispose();
    }
}