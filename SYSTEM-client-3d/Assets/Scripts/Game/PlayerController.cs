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
    [SerializeField] private float mouseSensitivity = 0.05f;
    [SerializeField] private float verticalSensitivity = 0.2f;
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

    [Header("Network Sync Settings")]
    [SerializeField] private bool syncPositionFromServer = true;
    [SerializeField] private bool syncRotationFromServer = false; // Disable for local player - client authoritative

    [Header("Rotation Multipliers")]
    [SerializeField] private float horizontalMultiplier = 0.01f;
    [SerializeField] private float verticalMultiplier = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false; // Keep false for clean console

    [Header("Fine Tuning - Extra Sensitivity Control")]
    [SerializeField] private bool useExtraHorizontalReduction = false;
    [SerializeField] private float horizontalReductionFactor = 0.2f;

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

    // Animation parameters
    private bool isMoving;
    private bool isRunning;
    private bool isGrounded = true;

    // World reference
    private GameObject worldSphere;

    void Awake()
    {
        CacheComponents();
        SetupInput();
    }

    void Start()
    {
        // Find the world sphere for positioning
        FindWorldSphere();

        if (playerData != null)
        {
            InitializePlayer(playerData);
        }

        // Lock cursor for first-person style controls
        if (isLocalPlayer)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        if (!isInitialized || !isLocalPlayer)
            return;

        HandleMouseInput();
        SendPositionUpdate();
    }

    void FixedUpdate()
    {
        if (!isInitialized)
            return;

        if (isLocalPlayer)
        {
            HandleCharacterMovement();
            ApplyGravityToSphere();
            UpdateAnimations();
        }
        else
        {
            // Remote players: interpolate to network position
            InterpolateToNetworkPosition();
        }
    }

    void OnEnable()
    {
        if (playerInputActions != null)
            playerInputActions.Enable();
    }

    void OnDisable()
    {
        if (playerInputActions != null)
            playerInputActions.Disable();
    }

    void OnDestroy()
    {
        if (playerInputActions != null)
        {
            playerInputActions.Gameplay.Move.performed -= OnMove;
            playerInputActions.Gameplay.Move.canceled -= OnMove;
            playerInputActions.Gameplay.Look.performed -= OnLook;
            playerInputActions.Gameplay.Look.canceled -= OnLook;
            playerInputActions.Dispose();
        }
    }

    #region Initialization

    void CacheComponents()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        cachedCharController = GetComponent<CharacterController>();
        cachedAnimator = GetComponent<Animator>();
    }

    void SetupInput()
    {
        playerInputActions = new PlayerInputActions();

        // Setup movement input
        playerInputActions.Gameplay.Move.performed += OnMove;
        playerInputActions.Gameplay.Move.canceled += OnMove;

        // Setup mouse look input
        playerInputActions.Gameplay.Look.performed += OnLook;
        playerInputActions.Gameplay.Look.canceled += OnLook;
    }

    void FindWorldSphere()
    {
        // Find CenterWorld object which contains our spherical world
        worldSphere = GameObject.Find("CenterWorld");
        if (worldSphere != null)
        {
            // Get sphere radius from collider or renderer
            SphereCollider sphereCollider = worldSphere.GetComponent<SphereCollider>();
            if (sphereCollider != null)
            {
                sphereRadius = sphereCollider.radius * worldSphere.transform.lossyScale.x;
            }
            else
            {
                // Default fallback radius
                sphereRadius = 300f;
            }
        }
        else
        {
            sphereRadius = 300f;
        }
    }

    // Compatible with WorldManager's new initialization method
    public void Initialize(Player data, bool isLocal, float worldRadius)
    {
        this.sphereRadius = worldRadius;
        InitializePlayer(data);
    }

    // New UpdateData method for WorldManager compatibility
    public void UpdateData(Player data)
    {
        playerData = data;
        if (!isLocalPlayer)
        {
            UpdateFromNetwork(
                new Vector3(data.Position.X, data.Position.Y, data.Position.Z),
                new Quaternion(data.Rotation.X, data.Rotation.Y, data.Rotation.Z, data.Rotation.W)
            );
        }
    }

    public void InitializePlayer(Player data)
    {
        playerData = data;

        // Set name label
        if (nameText != null)
        {
            nameText.text = data.Name;
        }

        // Determine if this is the local player
        var localPlayer = GameManager.GetLocalPlayer();
        isLocalPlayer = (localPlayer != null && localPlayer.PlayerId == data.PlayerId);

        // Configure based on whether this is local or remote player
        ConfigurePlayerType();

        // Position player on sphere surface
        if (worldSphere != null)
        {
            PositionOnSphereSurface(new Vector3(data.Position.X, data.Position.Y, data.Position.Z));
        }

        // Apply rotation
        transform.rotation = new Quaternion(data.Rotation.X, data.Rotation.Y, data.Rotation.Z, data.Rotation.W);

        isInitialized = true;

        if (showDebugInfo)
        {
            UnityEngine.Debug.Log($"[PlayerController] Initialized player: {data.Name} (Local: {isLocalPlayer})");
        }
    }

    void ConfigurePlayerType()
    {
        if (isLocalPlayer)
        {
            // Local player configuration
            if (playerRenderer != null && localPlayerMaterial != null)
            {
                playerRenderer.material = localPlayerMaterial;
            }

            // Configure physics for local control
            if (cachedRigidbody != null)
            {
                cachedRigidbody.useGravity = false;
                cachedRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            }

            // Hide name tag for local player
            if (nameCanvas != null)
            {
                nameCanvas.gameObject.SetActive(false);
            }

            // Don't sync rotation from server for local player
            syncRotationFromServer = false;
        }
        else
        {
            // Remote player configuration
            if (playerRenderer != null && remotePlayerMaterial != null)
            {
                playerRenderer.material = remotePlayerMaterial;
            }

            // Configure physics for network sync
            if (cachedRigidbody != null)
            {
                cachedRigidbody.useGravity = false;
                cachedRigidbody.isKinematic = true;
            }

            // Show name tag for remote players
            if (nameCanvas != null)
            {
                nameCanvas.gameObject.SetActive(true);
            }

            // Remote players sync rotation from server
            syncRotationFromServer = true;
        }
    }

    #endregion

    #region Input Handling

    void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    void OnLook(InputAction.CallbackContext context)
    {
        if (enableMouseLook && isLocalPlayer)
        {
            mouseInput = context.ReadValue<Vector2>();
        }
    }

    void HandleMouseInput()
    {
        if (!enableMouseLook || !isLocalPlayer)
            return;

        // Apply mouse sensitivity
        float mouseX = mouseInput.x * mouseSensitivity;
        float mouseY = mouseInput.y * verticalSensitivity;

        // Apply extra reduction if enabled
        if (useExtraHorizontalReduction)
        {
            mouseX *= horizontalReductionFactor;
        }

        // Handle horizontal rotation (character turning)
        HandleMouseRotation(mouseX);

        // Handle vertical look (camera pitch)
        if (CameraManager.Instance != null)
        {
            if (invertY) mouseY = -mouseY;

            verticalRotation -= mouseY * verticalMultiplier;
            verticalRotation = Mathf.Clamp(verticalRotation, -verticalLookLimit, verticalLookLimit);

            CameraManager.Instance.SetCameraPitch(verticalRotation);
        }
    }

    void HandleMouseRotation(float mouseX)
    {
        if (Mathf.Abs(mouseX) < 0.001f)
            return;

        // Get the sphere's up vector at player position (radial from center)
        Vector3 sphereUp = GetUpVectorOnSphere();

        // Apply rotation around the sphere's up vector at this position
        float rotationAmount = mouseX * horizontalMultiplier;

        // Use RotateAround for cleaner spherical rotation
        transform.RotateAround(transform.position, sphereUp, rotationAmount);
    }

    #endregion

    #region Movement

    void HandleCharacterMovement()
    {
        if (!isLocalPlayer || moveInput.magnitude < 0.01f)
        {
            isMoving = false;
            return;
        }

        isMoving = true;

        // Get movement direction relative to character facing
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        // Calculate movement vector
        Vector3 movement = (forward * moveInput.y + right * moveInput.x).normalized;

        // Apply movement speed
        movement *= walkSpeed * Time.fixedDeltaTime;

        // Move the character
        if (cachedCharController != null)
        {
            cachedCharController.Move(movement);
        }
        else if (cachedRigidbody != null)
        {
            Vector3 newPosition = transform.position + movement;
            cachedRigidbody.MovePosition(newPosition);
        }
        else
        {
            transform.position += movement;
        }
    }

    void ApplyGravityToSphere()
    {
        if (worldSphere == null)
            return;

        Vector3 sphereCenter = worldSphere.transform.position;
        Vector3 toCenter = sphereCenter - transform.position;
        Vector3 gravityDir = toCenter.normalized;

        // Position on sphere surface
        float distanceFromCenter = toCenter.magnitude;
        float targetDistance = sphereRadius + desiredSurfaceOffset;

        if (Mathf.Abs(distanceFromCenter - targetDistance) > 0.01f)
        {
            Vector3 targetPosition = sphereCenter + (-gravityDir * targetDistance);

            if (cachedRigidbody != null)
            {
                cachedRigidbody.MovePosition(targetPosition);
            }
            else
            {
                transform.position = targetPosition;
            }
        }

        // Align player up with sphere surface normal
        AlignWithSphereNormal();
    }

    void AlignWithSphereNormal()
    {
        if (worldSphere == null)
            return;

        Vector3 sphereCenter = worldSphere.transform.position;
        Vector3 up = (transform.position - sphereCenter).normalized;

        // Calculate target rotation to align with sphere normal
        Vector3 forward = Vector3.Cross(transform.right, up).normalized;
        if (forward.magnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(forward, up);

            // Smooth rotation alignment
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
                Time.fixedDeltaTime * characterRotationSpeed / 100f);
        }
    }

    void PositionOnSphereSurface(Vector3 position)
    {
        if (worldSphere == null)
            return;

        Vector3 sphereCenter = worldSphere.transform.position;
        Vector3 direction = (position - sphereCenter).normalized;

        // Place on sphere surface
        transform.position = sphereCenter + direction * (sphereRadius + desiredSurfaceOffset);

        // Align with sphere normal
        Vector3 up = direction;
        Vector3 forward = Vector3.Cross(Vector3.right, up).normalized;
        if (forward.magnitude < 0.001f)
        {
            forward = Vector3.Cross(Vector3.forward, up).normalized;
        }

        transform.rotation = Quaternion.LookRotation(forward, up);
    }

    Vector3 GetUpVectorOnSphere()
    {
        if (worldSphere != null)
        {
            return (transform.position - worldSphere.transform.position).normalized;
        }
        return Vector3.up;
    }

    #endregion

    #region Network Sync

    public void UpdateFromNetwork(Vector3 position, Quaternion rotation)
    {
        if (isLocalPlayer)
            return;

        targetPosition = position;

        if (syncRotationFromServer)
        {
            transform.rotation = rotation;
        }
    }

    void InterpolateToNetworkPosition()
    {
        if (isLocalPlayer || targetPosition == Vector3.zero)
            return;

        // Smooth position interpolation
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.fixedDeltaTime * 10f);
    }

    void SendPositionUpdate()
    {
        if (!isLocalPlayer || !GameManager.IsConnected())
            return;

        // Only send updates at intervals and if position/rotation changed
        if (Time.time - lastNetworkUpdateTime < networkUpdateInterval)
            return;

        bool positionChanged = Vector3.Distance(transform.position, lastSentPosition) > 0.01f;
        bool rotationChanged = Quaternion.Angle(transform.rotation, lastSentRotation) > 0.1f;

        if (positionChanged || rotationChanged)
        {
            // Send position update to server
            GameManager.Conn?.Reducers.UpdatePlayerPosition(
                new DbVector3(transform.position.x, transform.position.y, transform.position.z),
                new DbQuaternion(transform.rotation.x, transform.rotation.y, transform.rotation.z, transform.rotation.w)
            );

            lastSentPosition = transform.position;
            lastSentRotation = transform.rotation;
            lastNetworkUpdateTime = Time.time;
        }
    }

    #endregion

    #region Animation

    void UpdateAnimations()
    {
        // Skip if no animator or controller is assigned
        if (cachedAnimator == null || cachedAnimator.runtimeAnimatorController == null)
            return;

        // Only set parameters if they exist in the animator controller
        // This prevents errors when animator controller is not set up or has different parameters
        bool hasIsMoving = false;
        bool hasMoveSpeed = false;

        // Check which parameters exist
        foreach (var param in cachedAnimator.parameters)
        {
            if (param.name == "IsMoving" && param.type == AnimatorControllerParameterType.Bool)
                hasIsMoving = true;
            else if (param.name == "MoveSpeed" && param.type == AnimatorControllerParameterType.Float)
                hasMoveSpeed = true;
        }

        // Only set parameters that exist
        if (hasIsMoving)
            cachedAnimator.SetBool("IsMoving", isMoving);

        if (hasMoveSpeed)
            cachedAnimator.SetFloat("MoveSpeed", moveInput.magnitude);
    }

    #endregion

    #region Public Methods

    public Player GetPlayerData()
    {
        return playerData;
    }

    public bool IsLocalPlayer()
    {
        return isLocalPlayer;
    }

    public bool IsMoving()
    {
        return isMoving;
    }

    #endregion
}