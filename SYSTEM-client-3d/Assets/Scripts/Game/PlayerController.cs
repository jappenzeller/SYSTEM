using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using SpacetimeDB.Types;
using Unity.Cinemachine;
using SYSTEM.Game;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float rotationSpeed = 100f;
    
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
    public GameObject playerCameraGameObject; // Old camera - can be removed later
    
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
    
    // Network update timing
    private float lastNetworkUpdateTime = 0f;
    private const float networkUpdateInterval = 0.1f;
    private Vector3 lastSentPosition;
    private Quaternion lastSentRotation;
    
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
        // Enhanced logging for WebGL
        bool isWebGL = Application.platform == RuntimePlatform.WebGLPlayer;
        string platformPrefix = isWebGL ? "[WebGL]" : "";
        
        Debug.Log($"{platformPrefix}[PlayerController] Initialize - Player: {data.Name}, isLocal: {isLocal}, radius: {worldSphereRadius}");
        
        playerData = data;
        isLocalPlayer = isLocal;
        
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
                Debug.Log("[PlayerController] Created new PlayerInputActions");
            }
            if (!playerInputActions.Gameplay.enabled)
            {
                playerInputActions.Gameplay.Enable();
                Debug.Log("[PlayerController] Enabled Gameplay action map");
            }
            
            // Set initial cursor state
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
        
        if (isLocalPlayer)
        {
            SetupLocalPlayerCamera();
        }
        
        UpdateNameDisplay();
        
        isInitialized = true;
        
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
    
    void Update()
    {
        if (!isInitialized) return;
        
        if (isLocalPlayer)
        {
            HandleInput();
            HandleMovementAndRotation();
        }
        
        UpdateMovementAnimation();
        UpdateUIOrientation();
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
    
    void HandleMovementAndRotation()
    {
        if (moveInput.magnitude < 0.01f) return;
        
        // Get camera forward and right directions
        Camera cam = Camera.main;
        if (cam == null) return;
        
        Vector3 camForward = cam.transform.forward;
        Vector3 camRight = cam.transform.right;
        
        // Project camera directions onto the sphere tangent plane
        Vector3 up = transform.position.normalized;
        camForward = Vector3.ProjectOnPlane(camForward, up).normalized;
        camRight = Vector3.ProjectOnPlane(camRight, up).normalized;
        
        // Calculate movement direction
        Vector3 moveDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;
        
        // Rotate player to face movement direction
        if (moveDirection.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, 
                targetRotation, 
                rotationSpeed * Time.deltaTime
            );
        }
        
        // Move player
        float currentSpeed = walkSpeed; // TODO: Add run support
        Vector3 movement = moveDirection * currentSpeed * Time.deltaTime;
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
    
    void SetupLocalPlayerCamera()
    {
        if (!isLocalPlayer) return;
        
        // Disable old camera system if it exists
        if (playerCameraGameObject != null)
        {
            playerCameraGameObject.SetActive(false);
            // Debug.Log("[PlayerController] Disabled old camera system");
        }
        
        // Enable Cinemachine camera for local player
        var cinemachineCamera = GetComponentInChildren<CinemachineCamera>();
        if (cinemachineCamera != null)
        {
            cinemachineCamera.gameObject.SetActive(true);
            // Debug.Log("[PlayerController] Enabled Cinemachine Camera");
            
            // Ensure Cinemachine Brain exists on main camera
            if (Camera.main != null && Camera.main.GetComponent<CinemachineBrain>() == null)
            {
                Camera.main.gameObject.AddComponent<CinemachineBrain>();
                // Debug.Log("[PlayerController] Added Cinemachine Brain to Main Camera");
            }
        }
        else
        {
            Debug.LogWarning("[PlayerController] No CinemachineCamera found in children!");
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
        if (!isLocalPlayer)
        {
            targetPosition = position;
            transform.rotation = rotation;
            transform.up = position.normalized;
            
            // Smooth position updates for remote players
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10f);
        }
    }
    
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
            GUI.Label(new Rect(10, y, 400, 20), "=== Player Info ===");
            y += 20;
            GUI.Label(new Rect(10, y, 400, 20), $"Position: {transform.position}");
            y += 20;
            GUI.Label(new Rect(10, y, 400, 20), $"Move Input: {moveInput}");
            y += 20;
            GUI.Label(new Rect(10, y, 400, 20), $"Is Moving: {moveInput.magnitude > 0.01f}");
        }
    }
}