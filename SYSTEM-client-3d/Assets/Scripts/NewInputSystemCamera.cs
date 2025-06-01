using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Orbital camera controller using Unity's new Input System
/// </summary>
public class NewInputSystemCamera : MonoBehaviour
{
    [Header("Orbit Controls")]
    [Tooltip("Speed of orbital rotation around the sphere")]
    public float orbitSpeed = 50f;
    
    [Tooltip("Speed of zooming in/out")]
    public float zoomSpeed = 10f;
    
    [Tooltip("Minimum distance from sphere center")]
    public float minDistance = 120f;
    
    [Tooltip("Maximum distance from sphere center")]
    public float maxDistance = 500f;
    
    [Tooltip("Smoothing factor for camera movement")]
    public float smoothTime = 0.3f;

    // Private variables
    private Vector3 sphereCenter = Vector3.zero;
    private float currentDistance = 200f;
    private float targetDistance = 200f;
    private Vector3 currentPosition;
    private Vector3 targetPosition;
    private Vector3 velocity = Vector3.zero;
    
    // Input tracking
    private Vector2 mouseInput;
    private Vector2 keyboardInput;
    private float scrollInput;
    private bool isMouseDown;
    
    // Input Actions - these will be created in code
    private InputAction mouseDeltaAction;
    private InputAction mouseLeftButtonAction;
    private InputAction mouseRightButtonAction;
    private InputAction scrollAction;
    private InputAction moveAction;

    void Start()
    {
        // Initialize camera position
        currentDistance = targetDistance = Vector3.Distance(transform.position, sphereCenter);
        currentPosition = targetPosition = transform.position;
        
        // Always look at sphere center initially
        transform.LookAt(sphereCenter);
        
        // Setup input actions
        SetupInputActions();
        
        Debug.Log("NewInputSystemCamera initialized at distance: " + currentDistance);
    }

    void SetupInputActions()
    {
        // Create input actions programmatically (no Input Action Asset needed)
        
        // Mouse delta for camera rotation
        mouseDeltaAction = new InputAction("MouseDelta", InputActionType.Value, "<Mouse>/delta");
        mouseDeltaAction.Enable();
        
        // Left mouse button for enabling rotation
        mouseLeftButtonAction = new InputAction("MouseLeftButton", InputActionType.Button, "<Mouse>/leftButton");
        mouseLeftButtonAction.performed += ctx => isMouseDown = true;
        mouseLeftButtonAction.canceled += ctx => isMouseDown = false;
        mouseLeftButtonAction.Enable();
        
        // Right mouse button as alternative
        mouseRightButtonAction = new InputAction("MouseRightButton", InputActionType.Button, "<Mouse>/rightButton");
        mouseRightButtonAction.performed += ctx => isMouseDown = true;
        mouseRightButtonAction.canceled += ctx => isMouseDown = false;
        mouseRightButtonAction.Enable();
        
        // Mouse scroll for zoom
        scrollAction = new InputAction("Scroll", InputActionType.Value, "<Mouse>/scroll");
        scrollAction.Enable();
        
        // WASD keys for keyboard orbit
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // We'll handle WASD manually in HandleInput since composite binding is complex
        }
    }

    void Update()
    {
        HandleInput();
        UpdateCameraTarget();
        UpdateCameraPosition();
    }

    void HandleInput()
    {
        // Reset inputs
        mouseInput = Vector2.zero;
        keyboardInput = Vector2.zero;
        scrollInput = 0f;
        
        // Get mouse input when button is held
        if (isMouseDown && mouseDeltaAction != null)
        {
            Vector2 mouseDelta = mouseDeltaAction.ReadValue<Vector2>();
            mouseInput = mouseDelta * 0.05f; // Scale down the sensitivity
        }
        
        // Get scroll input
        if (scrollAction != null)
        {
            Vector2 scrollValue = scrollAction.ReadValue<Vector2>();
            scrollInput = scrollValue.y * 0.1f; // Scale down scroll sensitivity
        }
        
        // Get keyboard input using current Keyboard
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            float horizontal = 0f;
            float vertical = 0f;
            
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                horizontal -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                horizontal += 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                vertical += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                vertical -= 1f;
                
            keyboardInput = new Vector2(horizontal, vertical);
        }
        
        // Combine mouse and keyboard input
        if (keyboardInput.magnitude > 0.1f)
        {
            mouseInput.x += keyboardInput.x * 2f; // Keyboard is slower than mouse
            mouseInput.y += keyboardInput.y * 2f; // Same direction as keyboard
        }
    }

    void UpdateCameraTarget()
    {
        // Manual orbit controls
        if (mouseInput.magnitude > 0.01f)
        {
            // Rotate around sphere center
            Vector3 directionToCamera = (currentPosition - sphereCenter).normalized;
            
            // Calculate right and up vectors for camera space rotation
            Vector3 right = Vector3.Cross(Vector3.up, directionToCamera);
            Vector3 up = Vector3.Cross(directionToCamera, right);
            
            // Apply rotation
            Quaternion horizontalRotation = Quaternion.AngleAxis(mouseInput.x * orbitSpeed * Time.deltaTime, Vector3.up);
            Quaternion verticalRotation = Quaternion.AngleAxis(-mouseInput.y * orbitSpeed * Time.deltaTime, right);
            
            Vector3 newDirection = horizontalRotation * verticalRotation * directionToCamera;
            targetPosition = sphereCenter + newDirection * targetDistance;
        }
        
        // Zoom controls
        if (Mathf.Abs(scrollInput) > 0.01f)
        {
            targetDistance -= scrollInput * zoomSpeed;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
            
            // Update target position with new distance
            Vector3 directionToCamera = (targetPosition - sphereCenter).normalized;
            targetPosition = sphereCenter + directionToCamera * targetDistance;
        }
    }

    void UpdateCameraPosition()
    {
        // Smooth camera movement
        currentPosition = Vector3.SmoothDamp(currentPosition, targetPosition, ref velocity, smoothTime);
        currentDistance = Vector3.Distance(currentPosition, sphereCenter);
        
        // Apply position and look at center
        transform.position = currentPosition;
        transform.LookAt(sphereCenter);
        
        // Slight upward tilt for better viewing angle
        transform.Rotate(5f, 0f, 0f);
    }

    // Public methods for external control
    public void FocusOnPosition(Vector3 worldPosition, float distance = 150f)
    {
        Vector3 direction = (worldPosition - sphereCenter).normalized;
        targetPosition = worldPosition + direction * distance;
        targetDistance = distance;
    }
    
    public void SetOrbitCenter(Vector3 center)
    {
        sphereCenter = center;
    }

    void OnGUI()
    {
        // Show controls
        GUI.Label(new Rect(10, 10, 400, 120), 
            "New Input System Camera Controls:\n" +
            "Hold Left/Right Mouse + Drag: Orbit\n" +
            "Mouse Scroll: Zoom\n" +
            "WASD/Arrow Keys: Orbit\n" +
            $"Distance: {currentDistance:F1}\n" +
            $"Mouse Down: {isMouseDown}");
    }

    void OnDestroy()
    {
        // Clean up input actions
        mouseDeltaAction?.Disable();
        mouseDeltaAction?.Dispose();
        
        mouseLeftButtonAction?.Disable();
        mouseLeftButtonAction?.Dispose();
        
        mouseRightButtonAction?.Disable();
        mouseRightButtonAction?.Dispose();
        
        scrollAction?.Disable();
        scrollAction?.Dispose();
    }
}