using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using SpacetimeDB.Types;
using Unity.Cinemachine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 10f;
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
        if (Time.time - lastNetworkUpdateTime > networkUpdateInterval)
        {
            Vector3 currentPos = transform.position;
            Quaternion currentRot = transform.rotation;
            
            // Only send if position or rotation changed significantly
            if (Vector3.Distance(currentPos, lastSentPosition) > 0.01f ||
                Quaternion.Angle(currentRot, lastSentRotation) > 0.1f)
            {
                // TODO: Implement actual network update using SpacetimeDB
                // SpacetimeDBClient.Instance.UpdatePlayerTransform(...)
                
                lastSentPosition = currentPos;
                lastSentRotation = currentRot;
                lastNetworkUpdateTime = Time.time;
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
        
        // Disable old camera system if it exists
        if (playerCameraGameObject != null)
        {
            playerCameraGameObject.SetActive(false);
            Debug.Log("[PlayerController] Disabled old camera system");
        }
        
        // Enable Cinemachine camera for local player
        var cinemachineCamera = GetComponentInChildren<CinemachineCamera>();
        if (cinemachineCamera != null)
        {
            cinemachineCamera.gameObject.SetActive(true);
            Debug.Log("[PlayerController] Enabled Cinemachine Camera");
            
            // Ensure Cinemachine Brain exists on main camera
            if (Camera.main != null && Camera.main.GetComponent<CinemachineBrain>() == null)
            {
                Camera.main.gameObject.AddComponent<CinemachineBrain>();
                Debug.Log("[PlayerController] Added Cinemachine Brain to Main Camera");
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