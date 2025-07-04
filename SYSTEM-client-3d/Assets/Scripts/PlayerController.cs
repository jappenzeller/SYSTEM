using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using SpacetimeDB.Types;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 10f;
    [SerializeField] private float rotationSpeed = 100f;
    
    [Header("Camera Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float arrowKeySensitivity = 100f;
    [SerializeField] private float maxLookAngle = 60f;
    [SerializeField] private float minLookAngle = -60f;
    [SerializeField] private bool invertMouseY = false;
    [SerializeField] private bool invertArrowY = false;
    
    [Header("Camera Zoom")]
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minZoomDistance = 2f;
    [SerializeField] private float maxZoomDistance = 10f;
    
    [Header("Visual Components")]
    [SerializeField] private Renderer playerRenderer;
    [SerializeField] private Light playerLight;
    [SerializeField] private Material localPlayerMaterial;
    [SerializeField] private Material remotePlayerMaterial;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip walkSound;
    [SerializeField] private AudioClip jumpSound;
    
    [Header("UI")]
    [SerializeField] private Canvas nameCanvas;
    [SerializeField] private TextMeshProUGUI nameText;
    
    [Header("Animation")]
    [SerializeField] private Animator playerAnimator;
    
    [Header("Camera")]
    public GameObject playerCameraGameObject;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    
    private Player playerData;
    private bool isLocalPlayer = false;
    private bool isInitialized = false;
    
    // Movement and positioning
    private Vector3 lastPosition;
    private Vector3 targetPosition;
    private float sphereRadius;
    private const float desiredSurfaceOffset = 1.0f;
    
    // Input system
    private PlayerInputActions playerInputActions;
    private Vector2 moveInput;
    
    // Camera state
    private float cameraPitch = 30f;
    private float currentZoomDistance = 5f;
    private bool isMouseLooking = false;
    
    // Network update timing
    private float lastNetworkUpdateTime = 0f;
    private const float networkUpdateInterval = 0.1f;
    private Vector3 lastSentPosition;
    private Quaternion lastSentRotation;
    
    private Camera playerCamera;
    
    // Animation parameters
    private static readonly int IsWalking = Animator.StringToHash("IsWalking");
    private static readonly int IsRunning = Animator.StringToHash("IsRunning");
    private static readonly int WalkSpeed = Animator.StringToHash("WalkSpeed");
    
    void Awake()
    {
        // Get components
        if (playerRenderer == null)
            playerRenderer = GetComponent<Renderer>();
            
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
            
        if (playerAnimator == null)
            playerAnimator = GetComponent<Animator>();
        
        if (playerAnimator == null)
        {
            Debug.LogWarning($"[PlayerController.Awake] Player Animator component not found on {gameObject.name}. Animations will not play.");
        }
    }
    
    void Start()
    {
        // Set up UI canvases
        SetupUI();
    }
    
    void OnEnable()
    {
        if (playerInputActions != null)
        {
            playerInputActions.Gameplay.Enable();
        }
        else if (isLocalPlayer && playerInputActions == null)
        {
            playerInputActions = new PlayerInputActions();
            playerInputActions.Gameplay.Enable();
            Debug.LogWarning("[PlayerController.OnEnable] playerInputActions was null for local player, created and enabled.");
        }
        
        if (isLocalPlayer)
        {
            // Restore cursor when enabled
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
    
    void OnDisable()
    {
        if (isLocalPlayer)
        {
            // Restore cursor when disabled
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
        playerInputActions?.Gameplay.Disable();
    }
    
    void OnDestroy()
    {
        if (isLocalPlayer)
        {
            // Make sure cursor is restored
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
        if (playerInputActions != null)
        {
            playerInputActions.Dispose();
        }
    }
    
    public void Initialize(Player data, bool isLocal, float worldSphereRadius)
    {
        Debug.Log($"[PlayerController] Initialize called - isLocal: {isLocal}");
        
        playerData = data;
        isLocalPlayer = isLocal;
        this.sphereRadius = worldSphereRadius;
        lastPosition = transform.position;
        targetPosition = transform.position;
        lastSentPosition = transform.position;
        lastSentRotation = transform.rotation;
        
        if (isLocalPlayer)
        {
            if (playerInputActions == null)
            {
                playerInputActions = new PlayerInputActions();
                Debug.Log("[PlayerController] Created new PlayerInputActions");
            }
            if (!playerInputActions.Gameplay.enabled)
            {
                playerInputActions.Gameplay.Enable();
                Debug.Log("[PlayerController] Enabled Gameplay action map");
            }
            
            // Initialize camera
            if (playerCamera != null)
            {
                cameraPitch = 30f;
                currentZoomDistance = 5f;
                playerCamera.transform.localEulerAngles = new Vector3(cameraPitch, 0, 0);
                playerCamera.transform.localPosition = new Vector3(0, 2f, -currentZoomDistance);
            }
            
            // Set initial cursor state
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
        SnapToSurface();
        SetupPlayerAppearance();
        
        if (isLocalPlayer)
        {
            SetupLocalPlayerCamera();
        }
        
        UpdateNameDisplay();
        
        isInitialized = true;
    }
    
    void Update()
    {
        if (!isInitialized) return;
        
        if (isLocalPlayer)
        {
            HandleInput();
            HandleCameraControls();
            HandleMovementAndRotation();
        }
        
        UpdateMovementAnimation();
        UpdateUIOrientation();
    }
    
    void LateUpdate()
    {
        if (isLocalPlayer && playerCamera != null)
        {
            // Optional: Add camera collision detection here
        }
    }
    
    void HandleInput()
    {
        if (!isLocalPlayer) return;
        
        if (playerInputActions == null)
        {
            if (isLocalPlayer) Debug.LogWarning("[PlayerController.HandleInput] playerInputActions is null for local player.");
            return;
        }
        
        // Movement input (WASD)
        moveInput = playerInputActions.Gameplay.Move.ReadValue<Vector2>();
    }
    
    void HandleCameraControls()
    {
        // Get input from multiple sources
        float horizontalRotation = 0f;
        float verticalRotation = 0f;
        
        // Arrow key input
        if (Input.GetKey(KeyCode.LeftArrow)) horizontalRotation -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) horizontalRotation += 1f;
        if (Input.GetKey(KeyCode.UpArrow)) verticalRotation += (invertArrowY ? -1f : 1f);
        if (Input.GetKey(KeyCode.DownArrow)) verticalRotation -= (invertArrowY ? -1f : 1f);
        
        // Apply arrow key sensitivity
        horizontalRotation *= arrowKeySensitivity * Time.deltaTime;
        verticalRotation *= arrowKeySensitivity * Time.deltaTime;
        
        // Mouse input
        bool rightMouseHeld = Input.GetMouseButton(1);
        bool middleMouseHeld = Input.GetMouseButton(2);
        
        if (rightMouseHeld || middleMouseHeld)
        {
            if (!isMouseLooking)
            {
                isMouseLooking = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
            
            if (invertMouseY) mouseY *= -1f;
            
            horizontalRotation += mouseX;
            verticalRotation += mouseY;
        }
        else if (isMouseLooking)
        {
            isMouseLooking = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
        // Apply horizontal rotation to player
        if (Mathf.Abs(horizontalRotation) > 0.01f)
        {
            transform.Rotate(Vector3.up, horizontalRotation);
        }
        
        // Apply vertical rotation to camera
        if (Mathf.Abs(verticalRotation) > 0.01f && playerCamera != null)
        {
            cameraPitch -= verticalRotation;
            cameraPitch = Mathf.Clamp(cameraPitch, minLookAngle, maxLookAngle);
            playerCamera.transform.localEulerAngles = new Vector3(cameraPitch, 0, 0);
        }
        
        // Mouse wheel zoom
        float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollDelta) > 0.01f && playerCamera != null)
        {
            currentZoomDistance -= scrollDelta * zoomSpeed;
            currentZoomDistance = Mathf.Clamp(currentZoomDistance, minZoomDistance, maxZoomDistance);
            
            Vector3 localPos = playerCamera.transform.localPosition;
            localPos.z = -currentZoomDistance;
            playerCamera.transform.localPosition = localPos;
        }
    }
    
    void HandleMovementAndRotation()
    {
        // Movement
        float speed = walkSpeed;
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        
        Vector3 movement = (forward * moveInput.y + right * moveInput.x) * speed * Time.deltaTime;
        Vector3 newPosition = transform.position + movement;
        
        // Keep player on sphere surface
        newPosition = newPosition.normalized * (this.sphereRadius + desiredSurfaceOffset);
        transform.position = newPosition;
        
        // Update up vector to match position on sphere
        transform.up = transform.position.normalized;
        
        // Send position update
        SendPositionUpdate();
    }
    
    void SendPositionUpdate()
    {
        if (Time.time - lastNetworkUpdateTime > networkUpdateInterval)
        {
            Vector3 currentPos = transform.position;
            Quaternion currentRot = transform.rotation;
            
            // Only send if position or rotation changed significantly
            if (Vector3.Distance(currentPos, lastSentPosition) > 0.01f ||
                Quaternion.Angle(currentRot, lastSentRotation) > 0.1f)
            {
                // GameManager.Conn is a static property, not instance
                var conn = GameManager.Conn;
                if (conn != null && conn.IsActive)
                {
                    // TODO: Check the actual UpdatePlayerPosition reducer signature
                    // This is a placeholder - verify the actual parameters required
                    // conn.Reducers.UpdatePlayerPosition(/* actual parameters */);
                    
                    lastSentPosition = currentPos;
                    lastSentRotation = currentRot;
                    lastNetworkUpdateTime = Time.time;
                }
            }
        }
    }
    
    void SnapToSurface()
    {
        Vector3 currentPos = transform.position;
        float currentDistance = currentPos.magnitude;
        
        if (Mathf.Abs(currentDistance - (this.sphereRadius + desiredSurfaceOffset)) > 0.5f)
        {
            Vector3 targetHoverPos = currentPos.normalized * (this.sphereRadius + desiredSurfaceOffset);
            transform.position = targetHoverPos;
        }
        
        transform.up = transform.position.normalized;
    }
    
    void SetupPlayerAppearance()
    {
        if (playerRenderer != null)
        {
            Material materialToUse = isLocalPlayer ? localPlayerMaterial : remotePlayerMaterial;
            if (materialToUse != null)
            {
                playerRenderer.material = materialToUse;
            }
            else
            {
                playerRenderer.material.color = isLocalPlayer ? Color.yellow : Color.white;
            }
        }
        
        if (playerLight != null)
        {
            playerLight.color = isLocalPlayer ? Color.yellow : Color.white;
            playerLight.intensity = isLocalPlayer ? 2f : 1f;
            playerLight.range = 10f;
        }
    }
    
    void SetupLocalPlayerCamera()
    {
        if (!isLocalPlayer) return;
        
        if (playerCameraGameObject != null)
        {
            playerCameraGameObject.SetActive(true);
            playerCamera = playerCameraGameObject.GetComponent<Camera>();
            
            if (playerCamera == null)
            {
                Debug.LogError("[PlayerController] Assigned playerCameraGameObject does not have a Camera component!");
                return;
            }
            
            if (!playerCamera.enabled)
            {
                Debug.LogWarning("[PlayerController] Player camera component was disabled. Enabling it now.");
                playerCamera.enabled = true;
            }
            
            Camera currentMain = Camera.main;
            if (currentMain != null && currentMain != playerCamera)
            {
                Debug.LogWarning($"[PlayerController] Another camera ('{currentMain.name}') is currently tagged as MainCamera.");
            }
            
            playerCameraGameObject.tag = "MainCamera";
            
            DebugCameraState();
        }
        else
        {
            Debug.LogError("[PlayerController] playerCameraGameObject is not assigned in the Inspector!");
        }
    }
    
    void SetupUI()
    {
        if (nameCanvas != null)
        {
            nameCanvas.worldCamera = Camera.main;
            nameCanvas.transform.localPosition = Vector3.up * 2.5f;
            if (nameText != null) nameText.text = "";
        }
    }
    
    void DebugCameraState()
    {
        if (!isLocalPlayer || playerCamera == null) return;
        // Minimal logging to avoid spam
    }
    
    void UpdateMovementAnimation()
    {
        if (playerAnimator == null) return;
        
        bool isMoving = moveInput.magnitude > 0.01f;
        playerAnimator.SetBool(IsWalking, isMoving);
        playerAnimator.SetFloat(WalkSpeed, moveInput.magnitude);
    }
    
    void UpdateUIOrientation()
    {
        if (nameCanvas != null && Camera.main != null)
        {
            nameCanvas.transform.LookAt(Camera.main.transform);
            nameCanvas.transform.rotation = Quaternion.LookRotation(
                nameCanvas.transform.position - Camera.main.transform.position
            );
        }
    }
    
    public void UpdateFromNetwork(Vector3 position, Quaternion rotation)
    {
        if (!isLocalPlayer)
        {
            targetPosition = position;
            transform.rotation = rotation;
            transform.up = position.normalized;
            
            // Smooth position updates for remote players
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10f);
        }
    }
    
    // Add this method that WorldManager is expecting
    public void UpdateData(Player newPlayerData)
    {
        playerData = newPlayerData;
        
        // Update position and rotation from player data
        Vector3 newPosition = new Vector3(
            playerData.Position.X,
            playerData.Position.Y,
            playerData.Position.Z
        );
        
        Quaternion newRotation = new Quaternion(
            playerData.Rotation.X,
            playerData.Rotation.Y,
            playerData.Rotation.Z,
            playerData.Rotation.W
        );
        
        UpdateFromNetwork(newPosition, newRotation);
        UpdateNameDisplay();
    }
    
    void UpdateNameDisplay()
    {
        if (nameText != null && playerData != null)
        {
            nameText.text = playerData.Name;
        }
    }
    
    public Player GetPlayerData()
    {
        return playerData;
    }
    
    public void SetPlayerData(Player data)
    {
        playerData = data;
        UpdateNameDisplay();
    }
    
    void OnGUI()
    {
        if (isLocalPlayer && showDebugInfo)
        {
            int y = 200;
            GUI.Label(new Rect(10, y, 400, 20), "=== Camera Controls ===");
            y += 20;
            GUI.Label(new Rect(10, y, 400, 20), "Arrow Keys: Look around");
            y += 20;
            GUI.Label(new Rect(10, y, 400, 20), "Right Mouse Button: Free look");
            y += 20;
            GUI.Label(new Rect(10, y, 400, 20), "Mouse Wheel: Zoom in/out");
            y += 20;
            GUI.Label(new Rect(10, y, 400, 20), "WASD: Move");
            y += 30;
            GUI.Label(new Rect(10, y, 400, 20), $"Camera Pitch: {cameraPitch:F1}°");
            y += 20;
            GUI.Label(new Rect(10, y, 400, 20), $"Player Rotation: {transform.eulerAngles.y:F1}°");
            y += 20;
            GUI.Label(new Rect(10, y, 400, 20), $"Zoom Distance: {currentZoomDistance:F1}");
            y += 20;
            GUI.Label(new Rect(10, y, 400, 20), $"Mouse Look Active: {isMouseLooking}");
        }
    }
}