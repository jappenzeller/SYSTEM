using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using SpacetimeDB.Types;
using SYSTEM.Game;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 6f;  // Minecraft-like speed
    [SerializeField] private float characterRotationSpeed = 720f;  // Fast rotation for auto-facing
    
    [Header("Mouse Look Settings")]
    [SerializeField] private float mouseSensitivity = 0.5f;  // Reduced from 2.0 to 0.5
    [SerializeField] private float verticalSensitivity = 0.2f;  // Even lower Y-axis sensitivity
    [SerializeField] private float verticalLookLimit = 60f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private bool enableMouseLook = true;
    
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
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true; // Set to true to enable debugging
    
    private Player playerData;
    private bool isLocalPlayer = false;
    private bool isInitialized = false;

    // Cached components
    private Rigidbody cachedRigidbody;
    private CharacterController cachedCharController;
    private Animator cachedAnimator;

    // Movement and positioning
    private Vector3 lastPosition;
    private Vector3 targetPosition;
    private float sphereRadius;
    private const float desiredSurfaceOffset = 1.0f;
    
    // Input system
    private PlayerInputActions playerInputActions;
    private Vector2 moveInput;
    private Vector2 mouseInput;
    private float verticalRotation = 0f; // Camera pitch angle
    
    // Network update timing
    private float lastNetworkUpdateTime = 0f;
    private const float networkUpdateInterval = 0.1f;
    private Vector3 lastSentPosition;
    private Quaternion lastSentRotation;

    // Transform tracking for debugging
    private Vector3 lastTrackedPosition;
    private Quaternion lastTrackedRotation;

    // Rotation override prevention
    private bool justAppliedLocalRotation = false;
    private int rotationProtectionFrames = 0;

    // Deferred rotation system
    private bool hasPendingRotation = false;
    private Quaternion pendingRotation = Quaternion.identity;
    
    // Animation parameters
    private static readonly int IsWalking = Animator.StringToHash("IsWalking");
    private static readonly int IsRunning = Animator.StringToHash("IsRunning");
    private static readonly int WalkSpeed = Animator.StringToHash("WalkSpeed");
    
    void Awake()
    {
        UnityEngine.Debug.Log($"[CORE] PlayerController.Awake() - GameObject: {gameObject.name}");

        // Cache physics and animation components
        cachedRigidbody = GetComponent<Rigidbody>();
        cachedCharController = GetComponent<CharacterController>();
        cachedAnimator = GetComponent<Animator>();

        // Log what components we found
        UnityEngine.Debug.Log($"[CORE] Components found - Rigidbody: {cachedRigidbody != null}, CharController: {cachedCharController != null}, Animator: {cachedAnimator != null}");

        // Get components
        if (playerRenderer == null)
            playerRenderer = GetComponent<Renderer>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (playerAnimator == null)
            playerAnimator = cachedAnimator;

        if (playerAnimator == null)
        {
            UnityEngine.Debug.LogWarning($"[PlayerController] Animator component not found on {gameObject.name}");
        }

        // Initialize PlayerInputActions early
        if (playerInputActions == null)
        {
            playerInputActions = new PlayerInputActions();

            // Verify action maps exist
            try
            {
                if (playerInputActions != null)
                {
                    // Try to access Gameplay actions
                    var testMove = playerInputActions.Gameplay.Move;
                    var testLook = playerInputActions.Gameplay.Look;
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[CRITICAL] Failed to verify input actions: {e.Message}");
            }
        }
        else
        {
            UnityEngine.Debug.Log("[CORE] PlayerInputActions already exists");
        }
    }
    
    void Start()
    {

        // Log mouse settings for debugging
        LogMouseSettings();

        // Diagnose common issues
        DiagnoseCommonIssues();

        // Set up UI canvases
        SetupUI();
    }

    void LogMouseSettings()
    {
        UnityEngine.Debug.Log($"[SETTINGS] === Mouse Settings ===");
        UnityEngine.Debug.Log($"[SETTINGS] Mouse sensitivity: {mouseSensitivity}");
        UnityEngine.Debug.Log($"[SETTINGS] Vertical look limit: {verticalLookLimit}");
        UnityEngine.Debug.Log($"[SETTINGS] Invert Y: {invertY}");
        UnityEngine.Debug.Log($"[SETTINGS] Enable mouse look: {enableMouseLook}");
        UnityEngine.Debug.Log($"[SETTINGS] Walk speed: {walkSpeed}");
        UnityEngine.Debug.Log($"[SETTINGS] Character rotation speed: {characterRotationSpeed}");
    }

    void DiagnoseCommonIssues()
    {
        // Check if object is active
        UnityEngine.Debug.Log($"[DIAGNOSE] GameObject active: {gameObject.activeInHierarchy}");
        UnityEngine.Debug.Log($"[DIAGNOSE] Component enabled: {enabled}");
        UnityEngine.Debug.Log($"[DIAGNOSE] isLocalPlayer: {isLocalPlayer}");
        UnityEngine.Debug.Log($"[DIAGNOSE] isInitialized: {isInitialized}");

        // Check for PlayerInput component
        var playerInputComponent = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (playerInputComponent != null)
        {
            UnityEngine.Debug.Log($"[DIAGNOSE] PlayerInput component found - Actions: {playerInputComponent.actions?.name}");
            UnityEngine.Debug.Log($"[DIAGNOSE] PlayerInput enabled: {playerInputComponent.enabled}");
        }
        else
        {
            UnityEngine.Debug.Log("[DIAGNOSE] NO PlayerInput component found (this is OK if using PlayerInputActions)");
        }

        // Check Input System settings
        var settings = UnityEngine.InputSystem.InputSystem.settings;
        if (settings != null)
        {
            UnityEngine.Debug.Log($"[DIAGNOSE] Input System update mode: {settings.updateMode}");
        }

        // Check for competing input components
        var allComponents = GetComponents<MonoBehaviour>();
        foreach (var comp in allComponents)
        {
            if (comp.GetType().Name.Contains("Input") && comp != this)
            {
                UnityEngine.Debug.Log($"[DIAGNOSE] Other input-related component: {comp.GetType().Name}");
            }
        }
    }
    
    void OnEnable()
    {
        UnityEngine.Debug.Log($"[CORE] PlayerController.OnEnable() - isLocalPlayer: {isLocalPlayer}, GameObject: {gameObject.name}");

        if (playerInputActions == null)
        {
            UnityEngine.Debug.Log("[CORE] Creating PlayerInputActions in OnEnable");
            playerInputActions = new PlayerInputActions();
        }

        if (isLocalPlayer)
        {
            UnityEngine.Debug.Log("[CORE] Enabling PlayerInputActions for local player");
            playerInputActions.Enable();
            playerInputActions.Gameplay.Enable(); // Explicitly enable the Gameplay action map

            // Verify it's actually enabled
            UnityEngine.Debug.Log($"[CORE] PlayerInputActions asset enabled: {playerInputActions.asset?.enabled}");

            // Test if actions work immediately after enabling
            try
            {
                var testMove = playerInputActions.Gameplay.Move.ReadValue<Vector2>();
                var testLook = playerInputActions.Gameplay.Look.ReadValue<Vector2>();
                UnityEngine.Debug.Log($"[CORE] Post-enable test - Move: {testMove}, Look: {testLook}");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[CORE] Post-enable test FAILED: {e.Message}");
            }

            if (enableMouseLook)
            {
                // Lock cursor for mouse look
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                UnityEngine.Debug.Log("[CORE] Mouse look enabled, cursor locked");
            }
        }
        else
        {
            UnityEngine.Debug.Log($"[CORE] NOT enabling input - not local player for {gameObject.name}");
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
        // Enhanced logging for WebGL
        bool isWebGL = Application.platform == RuntimePlatform.WebGLPlayer;
        string platformPrefix = isWebGL ? "[WebGL]" : "";

        UnityEngine.Debug.Log($"[CORE] Initialize() called - Player: {data.Name}, isLocal: {isLocal}, radius: {worldSphereRadius}");
        Debug.Log($"{platformPrefix}[PlayerController] Initialize - Player: {data.Name}, isLocal: {isLocal}, radius: {worldSphereRadius}");

        playerData = data;
        isLocalPlayer = isLocal;
        UnityEngine.Debug.Log($"[CORE] isLocalPlayer set to: {isLocalPlayer}");

        // Robust sphere radius validation
        if (worldSphereRadius <= 0)
        {
            Debug.LogError($"{platformPrefix}[PlayerController] Invalid radius <= 0: {worldSphereRadius}, using default 3000");
            this.sphereRadius = 3000f;
        }
        else if (worldSphereRadius > 10000)
        {
            Debug.LogWarning($"{platformPrefix}[PlayerController] Unusually large radius: {worldSphereRadius}, capping at 10000");
            this.sphereRadius = 10000f;
        }
        else if (float.IsNaN(worldSphereRadius) || float.IsInfinity(worldSphereRadius))
        {
            Debug.LogError($"{platformPrefix}[PlayerController] NaN/Infinity radius detected, using default 3000");
            this.sphereRadius = 3000f;
        }
        else
        {
            this.sphereRadius = worldSphereRadius;
        }
        
        Debug.Log($"{platformPrefix}[PlayerController] Using sphere radius: {this.sphereRadius}");
        
        // Comprehensive position validation
        Vector3 currentPos = transform.position;
        bool positionCorrected = false;
        
        // Check for invalid Vector3 values
        if (!IsValidVector3(currentPos))
        {
            Debug.LogError($"{platformPrefix}[PlayerController] Invalid position (NaN/Infinity): {currentPos}");
            currentPos = new Vector3(0, sphereRadius + desiredSurfaceOffset, 0);
            transform.position = currentPos;
            positionCorrected = true;
        }
        // Check if too close to origin
        else if (currentPos.magnitude < 10f)
        {
            Debug.LogWarning($"{platformPrefix}[PlayerController] Player {data.Name} too close to origin: {currentPos} (mag: {currentPos.magnitude:F2})");
            
            // Calculate proper spawn position at north pole
            Vector3 correctedPos = new Vector3(0, sphereRadius + desiredSurfaceOffset, 0);
            transform.position = correctedPos;
            currentPos = correctedPos;
            positionCorrected = true;
            
            Debug.Log($"{platformPrefix}[PlayerController] Corrected to north pole: {correctedPos}");
        }
        // Additional check for old default positions
        else if (Mathf.Approximately(currentPos.y, 100f) && currentPos.magnitude < 200f)
        {
            Debug.LogWarning($"{platformPrefix}[PlayerController] Detected old default position (y=100): {currentPos}");
            
            Vector3 correctedPos = new Vector3(0, sphereRadius + desiredSurfaceOffset, 0);
            transform.position = correctedPos;
            currentPos = correctedPos;
            positionCorrected = true;
            
            Debug.Log($"{platformPrefix}[PlayerController] Corrected old default to: {correctedPos}");
        }
        
        // Set tracking variables to validated position
        lastPosition = currentPos;
        targetPosition = currentPos;
        lastSentPosition = currentPos;
        
        // Validate rotation
        if (!IsValidQuaternion(transform.rotation))
        {
            Debug.LogWarning($"{platformPrefix}[PlayerController] Invalid rotation detected, resetting");
            transform.rotation = Quaternion.identity;
        }
        lastSentRotation = transform.rotation;
        
        if (isLocalPlayer)
        {
            if (playerInputActions == null)
            {
                playerInputActions = new PlayerInputActions();
            }
            
            if (!playerInputActions.Gameplay.enabled)
            {
                playerInputActions.Gameplay.Enable();
            }
            
            // Set cursor state for mouse look
            if (enableMouseLook)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
        
        // Snap to surface (this may adjust position further)
        Vector3 preSnapPos = transform.position;
        SnapToSurface();
        Vector3 postSnapPos = transform.position;
        
        // Log position changes from snapping
        float snapDistance = Vector3.Distance(preSnapPos, postSnapPos);
        if (snapDistance > 0.01f || positionCorrected || isWebGL)
        {
            Debug.Log($"{platformPrefix}[PlayerController] Snap adjustment: {preSnapPos:F2} -> {postSnapPos:F2} (delta: {snapDistance:F2})");
        }
        
        // Final position validation
        if (!IsValidVector3(transform.position))
        {
            Debug.LogError($"{platformPrefix}[PlayerController] CRITICAL: Position still invalid after snap!");
            transform.position = new Vector3(0, sphereRadius + desiredSurfaceOffset, 0);
        }
        
        // Verify we're on the sphere surface
        float finalDistance = transform.position.magnitude;
        float expectedDistance = sphereRadius + desiredSurfaceOffset;
        float error = Mathf.Abs(finalDistance - expectedDistance);
        
        if (error > 1f)
        {
            Debug.LogWarning($"{platformPrefix}[PlayerController] Not on sphere surface! Distance: {finalDistance:F2}, Expected: {expectedDistance:F2}, Error: {error:F2}");
        }
        else if (isWebGL)
        {
            Debug.Log($"{platformPrefix}[PlayerController] Successfully on sphere surface (error: {error:F3})");
        }
        
        SetupPlayerAppearance();
        UpdateNameDisplay();
        
        isInitialized = true;
        UnityEngine.Debug.Log($"[CORE] Initialization complete - isLocalPlayer: {isLocalPlayer}, isInitialized: {isInitialized}");

        // Re-enable input actions if local player (in case they were created during Initialize)
        if (isLocalPlayer && playerInputActions != null)
        {
            playerInputActions.Enable();
            playerInputActions.Gameplay.Enable(); // Explicitly enable the Gameplay action map
            UnityEngine.Debug.Log("[CORE] Re-enabled PlayerInputActions and Gameplay map after initialization");
        }

        // Final initialization summary for WebGL
        if (isWebGL)
        {
            Debug.Log($"{platformPrefix}[PlayerController] === INIT COMPLETE ===");
            Debug.Log($"{platformPrefix}  Player: {data.Name} (Local: {isLocal})");
            Debug.Log($"{platformPrefix}  Position: {transform.position:F2}");
            Debug.Log($"{platformPrefix}  Radius: {sphereRadius:F1}");
            Debug.Log($"{platformPrefix}  Surface Distance: {transform.position.magnitude:F2}");
            Debug.Log($"{platformPrefix}====================");
        }
        else
        {
            Debug.Log($"[PlayerController] Initialization complete for {data.Name}");
        }
    }
    
    void LateUpdate()
    {
        // Apply pending rotation AFTER all other updates
        if (hasPendingRotation && isLocalPlayer)
        {
            UnityEngine.Debug.Log($"[ROTATION-LATE] Applying deferred rotation: {pendingRotation.eulerAngles}");
            Vector3 beforeLate = transform.rotation.eulerAngles;

            // Force the rotation
            transform.rotation = pendingRotation;

            Vector3 afterLate = transform.rotation.eulerAngles;
            UnityEngine.Debug.Log($"[ROTATION-LATE] Before: {beforeLate}, After: {afterLate}");

            hasPendingRotation = false;
            pendingRotation = Quaternion.identity;

            // Mark protection
            rotationProtectionFrames = 5;
        }

        // Detect if rotation is being changed after Update
        if (isLocalPlayer && showDebugInfo && mouseInput.magnitude > 0.01f)
        {
            if (Quaternion.Angle(transform.rotation, lastTrackedRotation) > 0.1f)
            {
                UnityEngine.Debug.LogWarning($"[ROTATION] OVERWRITTEN in LateUpdate!");
                UnityEngine.Debug.LogWarning($"[ROTATION]   Was: {lastTrackedRotation.eulerAngles}");
                UnityEngine.Debug.LogWarning($"[ROTATION]   Now: {transform.rotation.eulerAngles}");
                UnityEngine.Debug.LogWarning($"[ROTATION]   Delta: {Quaternion.Angle(transform.rotation, lastTrackedRotation):F2} degrees");
            }
        }
    }

    void Update()
    {
        // CRITICAL: Verify Update() is being called
        if (Time.frameCount % 120 == 0) // Every 2 seconds at 60fps
        {
            UnityEngine.Debug.Log($"[CORE] Update() called - Frame: {Time.frameCount}, isInitialized: {isInitialized}, isLocalPlayer: {isLocalPlayer}, GameObject: {gameObject.name}");
        }

        // Decrement rotation protection counter
        if (rotationProtectionFrames > 0)
        {
            rotationProtectionFrames--;
            if (showDebugInfo)
            {
                UnityEngine.Debug.Log($"[ROTATION] Protection frames remaining: {rotationProtectionFrames}");
            }
        }

        if (!isInitialized)
        {
            if (Time.frameCount % 120 == 0)
            {
                UnityEngine.Debug.LogWarning("[CORE] Update() skipped - not initialized");
            }
            return;
        }

        if (isLocalPlayer)
        {
            // CRITICAL: Verify HandleInput is being called
            if (Time.frameCount % 60 == 0) // Every second
            {
                UnityEngine.Debug.Log("[CORE] About to call HandleInput()");
            }

            HandleInput();

            if (Time.frameCount % 60 == 0)
            {
                UnityEngine.Debug.Log("[CORE] HandleInput() completed");
            }

            // Store rotation before movement handling
            if (showDebugInfo && mouseInput.magnitude > 0.01f)
            {
                lastTrackedRotation = transform.rotation;
            }

            // CRITICAL: Check mouseInput right before HandleMovementAndRotation
           
            HandleMovementAndRotation();
        }
        else if (Time.frameCount % 120 == 0)
        {
            UnityEngine.Debug.Log($"[CORE] Update() - Not local player, skipping input for {gameObject.name}");
        }

        UpdateMovementAnimation();
        UpdateUIOrientation();

        // Track transform changes for debugging
        if (isLocalPlayer && Time.frameCount % 30 == 0) // Every 0.5 seconds at 60fps
        {
            bool positionChanged = Vector3.Distance(transform.position, lastTrackedPosition) > 0.01f;
            bool rotationChanged = Quaternion.Angle(transform.rotation, lastTrackedRotation) > 0.1f;

            if (positionChanged || rotationChanged)
            {
                UnityEngine.Debug.Log($"[TRANSFORM] === Transform Changes Detected ===");
                UnityEngine.Debug.Log($"[TRANSFORM] Position changed: {positionChanged} (distance: {Vector3.Distance(transform.position, lastTrackedPosition):F4})");
                UnityEngine.Debug.Log($"[TRANSFORM] Rotation changed: {rotationChanged} (angle: {Quaternion.Angle(transform.rotation, lastTrackedRotation):F2}°)");
                UnityEngine.Debug.Log($"[TRANSFORM] Current position: {transform.position}");
                UnityEngine.Debug.Log($"[TRANSFORM] Current rotation: {transform.rotation.eulerAngles}");
            }

            lastTrackedPosition = transform.position;
            lastTrackedRotation = transform.rotation;
        }
    }
    
    void HandleInput()
    {
        if (!isLocalPlayer) return;

        // Ensure input actions are initialized
        if (playerInputActions == null)
        {
            playerInputActions = new PlayerInputActions();
            playerInputActions.Enable();
        }

        try
        {
            // Read movement input
            moveInput = playerInputActions.Gameplay.Move.ReadValue<Vector2>();

            // Read mouse input
            if (enableMouseLook)
            {
                Vector2 lookInput = playerInputActions.Gameplay.Look.ReadValue<Vector2>();

                if (lookInput.magnitude > 0.01f)
                {
                    // Apply separate sensitivities for X and Y axes
                    lookInput.x *= mouseSensitivity;
                    lookInput.y *= verticalSensitivity;
                    if (invertY) lookInput.y = -lookInput.y;

                    mouseInput = lookInput;
                }
                else
                {
                    mouseInput = Vector2.zero;
                }
            }
            else
            {
                mouseInput = Vector2.zero;
            }
        }
        catch (System.Exception e)
        {
            if (showDebugInfo)
            {
                UnityEngine.Debug.LogError($"[INPUT] Exception: {e.Message}");
            }
        }
    }
    
    void HandleMovementAndRotation()
    {
        // Handle mouse rotation (Minecraft style)
        HandleMouseRotation();

        // Handle character movement relative to facing
        HandleCharacterMovement();

        // Send position update if anything changed
        if (moveInput.magnitude > 0.01f || mouseInput.magnitude > 0.01f)
        {
            SendPositionUpdate();
        }
    }

    void HandleMouseRotation()
    {
        if (mouseInput.magnitude < 0.01f) return;

        // Apply tuned sensitivity values for smooth rotation
        float rotationScaleX = 0.05f;  // Horizontal rotation scale
        float rotationScaleY = 0.02f;  // Vertical rotation scale (keep lower)

        float mouseX = mouseInput.x * rotationScaleX;
        float mouseY = mouseInput.y * rotationScaleY;

        // Only log when debugging is explicitly enabled
        if (showDebugInfo && Time.frameCount % 60 == 0)  // Log every second at 60fps
        {
            UnityEngine.Debug.Log($"[ROTATION] Input: {mouseInput:F2}, Rotation: X={mouseX:F3}°, Y={mouseY:F3}°, Pitch: {verticalRotation:F1}°");
        }

        // HORIZONTAL ROTATION (Yaw) - Rotate character around sphere surface normal
        if (Mathf.Abs(mouseX) > 0.001f)
        {
            Vector3 sphereUp = transform.position.normalized;

            // Apply rotation based on whether we have a Rigidbody
            if (cachedRigidbody != null)
            {
                // Use Rigidbody rotation
                Quaternion deltaRotation = Quaternion.AngleAxis(mouseX, sphereUp);
                Quaternion newRotation = deltaRotation * cachedRigidbody.rotation;

                cachedRigidbody.MoveRotation(newRotation);
                if (!cachedRigidbody.isKinematic)
                    cachedRigidbody.rotation = newRotation;
                else
                    transform.rotation = newRotation;

                transform.up = sphereUp;
            }
            else
            {
                // Use Transform rotation
                Quaternion deltaRotation = Quaternion.AngleAxis(mouseX, sphereUp);
                transform.rotation = deltaRotation * transform.rotation;
                transform.up = sphereUp;
            }

            // Mark rotation as applied for network protection
            justAppliedLocalRotation = true;
            rotationProtectionFrames = 3;
        }

        // VERTICAL ROTATION (Pitch) - Store for camera, don't rotate character
        if (Mathf.Abs(mouseY) > 0.001f)
        {
            verticalRotation -= mouseY;
            verticalRotation = Mathf.Clamp(verticalRotation, -verticalLookLimit, verticalLookLimit);

            // Send vertical rotation to camera
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SetCameraPitch(verticalRotation);
            }
        }
    }

    void HandleCharacterMovement()
    {
        if (moveInput.magnitude < 0.01f) return;

        if (showDebugInfo)
        {
            UnityEngine.Debug.Log($"[MOVEMENT] Move input: {moveInput}");
        }

        // Movement is relative to character's facing direction (Minecraft style)
        Vector3 characterForward = transform.forward;
        Vector3 characterRight = transform.right;
        Vector3 sphereUp = transform.position.normalized;

        // Project movement directions onto sphere surface
        characterForward = Vector3.ProjectOnPlane(characterForward, sphereUp).normalized;
        characterRight = Vector3.ProjectOnPlane(characterRight, sphereUp).normalized;

        // Calculate movement direction
        Vector3 moveDirection = (characterForward * moveInput.y + characterRight * moveInput.x).normalized;

        if (moveDirection.magnitude > 0.01f)
        {
            // Move character
            float currentSpeed = walkSpeed;
            Vector3 movement = moveDirection * currentSpeed * Time.deltaTime;
            Vector3 oldPosition = transform.position;
            Vector3 newPosition = oldPosition + movement;

            // Keep on sphere surface
            newPosition = newPosition.normalized * (sphereRadius + desiredSurfaceOffset);
            transform.position = newPosition;

            // Update up vector to match new position on sphere
            transform.up = transform.position.normalized;

            if (showDebugInfo)
            {
                UnityEngine.Debug.Log($"[MOVEMENT] Moved from {oldPosition} to {newPosition}");
            }
        }
    }

    public float GetVerticalRotation()
    {
        return verticalRotation;
    }
    
    void SendPositionUpdate()
    {
        // Only send updates for local player
        if (!isLocalPlayer) return;
        
        if (Time.time - lastNetworkUpdateTime > networkUpdateInterval)
        {
            Vector3 currentPos = transform.position;
            Quaternion currentRot = transform.rotation;
            
            // Only send if position or rotation changed significantly
            if (Vector3.Distance(currentPos, lastSentPosition) > 0.01f ||
                Quaternion.Angle(currentRot, lastSentRotation) > 0.1f)
            {
                // Send position update to server
                if (GameManager.IsConnected())
                {
                    var dbPosition = new DbVector3 
                    { 
                        X = currentPos.x, 
                        Y = currentPos.y, 
                        Z = currentPos.z 
                    };
                    
                    var dbRotation = new DbQuaternion 
                    { 
                        X = currentRot.x, 
                        Y = currentRot.y, 
                        Z = currentRot.z, 
                        W = currentRot.w 
                    };
                    
                    // Call the reducer to update position on server
                    GameManager.Conn.Reducers.UpdatePlayerPosition(dbPosition, dbRotation);
                    
                    // Debug log every 100th update to avoid spam (or disable completely in production)
                    if (showDebugInfo && Random.Range(0, 100) == 0)
                    {
                        Debug.Log($"[PlayerController] Position update sent: Pos({dbPosition.X:F2},{dbPosition.Y:F2},{dbPosition.Z:F2})");
                    }
                }
                
                lastSentPosition = currentPos;
                lastSentRotation = currentRot;
                lastNetworkUpdateTime = Time.time;
            }
        }
    }
    
    void SnapToSurface()
    {
        // Enhanced snap to surface with robust error handling
        Vector3 currentPos = transform.position;
        
        // Validate position is not NaN or invalid
        if (!IsValidVector3(currentPos))
        {
            Debug.LogWarning($"[PlayerController] Invalid position detected: {currentPos}, resetting to north pole");
            currentPos = new Vector3(0, sphereRadius + desiredSurfaceOffset, 0);
            transform.position = currentPos;
        }
        
        float currentDistance = currentPos.magnitude;
        
        // Handle edge case: at origin or very close to it
        if (currentDistance < 0.1f)
        {
            Debug.LogWarning($"[PlayerController] Player at origin (distance: {currentDistance}), moving to north pole");
            // Default to north pole position
            Vector3 northPolePos = new Vector3(0, sphereRadius + desiredSurfaceOffset, 0);
            transform.position = northPolePos;
            transform.up = Vector3.up;
            
            // Log for WebGL debugging
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                Debug.Log($"[WebGL] Snap recovered from origin to: {northPolePos}");
            }
            return;
        }
        
        // Calculate normalized direction (safe because we checked magnitude > 0.1)
        Vector3 normalizedDir = currentPos.normalized;
        
        // Validate normalized direction
        if (!IsValidVector3(normalizedDir))
        {
            Debug.LogWarning("[PlayerController] Failed to normalize position, using up vector");
            normalizedDir = Vector3.up;
        }
        
        // Calculate target position on sphere surface
        float targetDistance = sphereRadius + desiredSurfaceOffset;
        Vector3 targetHoverPos = normalizedDir * targetDistance;
        
        // Only snap if we're significantly off the surface
        float distanceError = Mathf.Abs(currentDistance - targetDistance);
        if (distanceError > 0.5f)
        {
            // Validate target position before applying
            if (IsValidVector3(targetHoverPos))
            {
                transform.position = targetHoverPos;
                
                if (showDebugInfo || Application.platform == RuntimePlatform.WebGLPlayer)
                {
                    Debug.Log($"[PlayerController] Snapped to surface: {currentPos:F2} -> {targetHoverPos:F2} (error: {distanceError:F2})");
                }
            }
            else
            {
                Debug.LogError($"[PlayerController] Invalid target position calculated: {targetHoverPos}");
            }
        }
        
        // Set up vector to match surface normal
        transform.up = normalizedDir;
        
        // Ensure rotation is valid and not producing NaN
        if (!IsValidQuaternion(transform.rotation))
        {
            Debug.LogWarning("[PlayerController] Invalid rotation detected, resetting to identity");
            transform.rotation = Quaternion.LookRotation(Vector3.forward, normalizedDir);
        }
    }
    
    // Helper method to validate Vector3
    bool IsValidVector3(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
               !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }
    
    // Helper method to validate Quaternion
    bool IsValidQuaternion(Quaternion q)
    {
        return !float.IsNaN(q.x) && !float.IsNaN(q.y) && !float.IsNaN(q.z) && !float.IsNaN(q.w) &&
               !float.IsInfinity(q.x) && !float.IsInfinity(q.y) && !float.IsInfinity(q.z) && !float.IsInfinity(q.w);
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
    
    void SetupUI()
    {
        if (nameCanvas != null)
        {
            nameCanvas.worldCamera = Camera.main;
            nameCanvas.transform.localPosition = Vector3.up * 2.5f;
            if (nameText != null) nameText.text = "";
        }
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
        // CRITICAL: Log when network updates arrive
        if (showDebugInfo)
        {
            UnityEngine.Debug.Log($"[NETWORK] UpdateFromNetwork called - isLocalPlayer: {isLocalPlayer}, position: {position}, rotation: {rotation.eulerAngles}");
        }

        // Check if we're protecting local rotation
        if (isLocalPlayer && rotationProtectionFrames > 0)
        {
            UnityEngine.Debug.LogWarning($"[NETWORK] BLOCKING network rotation update - protecting local rotation ({rotationProtectionFrames} frames left)");
            return; // Don't apply ANY network updates while rotating locally
        }

        if (!isLocalPlayer)
        {
            targetPosition = position;
            transform.rotation = rotation;
            transform.up = position.normalized;

            // Smooth position updates for remote players
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10f);
        }
        else if (showDebugInfo)
        {
            // WARNING: Network update for local player - this shouldn't happen!
            UnityEngine.Debug.LogWarning($"[NETWORK] UpdateFromNetwork called for LOCAL player - rotation would be: {rotation.eulerAngles}");
        }
    }
    
    public void UpdateData(Player newPlayerData)
    {
        // CRITICAL: Log when player data is updated from server
        if (showDebugInfo)
        {
            Vector3 oldRotation = transform.rotation.eulerAngles;
            Vector3 newRotationEuler = new Quaternion(
                newPlayerData.Rotation.X,
                newPlayerData.Rotation.Y,
                newPlayerData.Rotation.Z,
                newPlayerData.Rotation.W
            ).eulerAngles;

            UnityEngine.Debug.Log($"[NETWORK] UpdateData called - isLocalPlayer: {isLocalPlayer}");
            UnityEngine.Debug.Log($"[NETWORK]   Old rotation: {oldRotation}");
            UnityEngine.Debug.Log($"[NETWORK]   New rotation from server: {newRotationEuler}");
        }

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
        // Only show debug info if explicitly enabled
        if (isLocalPlayer && showDebugInfo)
        {
            int y = 200;
            GUI.Label(new Rect(10, y, 400, 20), "=== Player Debug ===");
            y += 20;
            GUI.Label(new Rect(10, y, 400, 20), $"Position: {transform.position:F1}");
            y += 20;
            GUI.Label(new Rect(10, y, 400, 20), $"Move Input: {moveInput}");
            y += 20;
            GUI.Label(new Rect(10, y, 400, 20), $"Mouse Input: {mouseInput}");
            y += 20;
            GUI.Label(new Rect(10, y, 400, 20), $"Mouse Look: {enableMouseLook}");
        }
    }
}