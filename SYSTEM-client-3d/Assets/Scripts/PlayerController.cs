using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // Make sure this is present
using SpacetimeDB.Types;
using TMPro;

public class PlayerController : MonoBehaviour
{
    [Header("Visual Components")]
    public Renderer playerRenderer;
    public Canvas nameCanvas;
    public TextMeshProUGUI nameText;
    public Canvas inventoryCanvas;
    public TextMeshProUGUI inventoryText;
    public ParticleSystem energyAura;
    public Transform energyOrbContainer;
    public Light playerLight;
    
    [Header("Energy Visualization")]
    public GameObject energyOrbPrefab;
    public float orbRadius = 1.5f;
    public float orbBobbingSpeed = 1.0f; // Speed for the energy orbs' bobbing animation
    public float orbRotationSpeed = 30f;
    public int maxVisibleOrbs = 6;
    
    [Header("Animation Settings")]
    public Animator playerAnimator;
    public float walkSpeed = 5f;
    public float runSpeed = 10f;    
    
    [Header("Materials")]
    public Material localPlayerMaterial;
    public Material remotePlayerMaterial;
    public Material[] energyMaterials = new Material[6]; // For each energy type
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip walkSound;
    public AudioClip energyCollectSound;
    public AudioClip energySpendSound;
    
    [Header("Input Settings (Local Player Only)")]
    // public KeyCode interactKey = KeyCode.E; // Replaced by Input Action Asset
    public float mouseSensitivity = 1.0f; // Adjust this for new Input System
    public float playerRotationSpeed = 120f; // Degrees per second for A/D rotation
    
    [Header("Camera Setup (Local Player)")]
    [Tooltip("Assign the Camera GameObject that is a child of this player prefab.")]
    public GameObject playerCameraGameObject; // Assign your prefab's child camera here
    
    private Player playerData;
    private bool isLocalPlayer = false;
    private bool isInitialized = false;
    
    // Movement and positioning
    private Vector3 lastPosition;
    private Vector3 targetPosition;
    private float sphereRadius; // World radius, will be set by WorldManager
    private const float desiredSurfaceOffset = 1.0f; // Desired height above the surface
    
    // Energy visualization
    private List<GameObject> energyOrbs = new List<GameObject>();
    private Dictionary<EnergyType, float> currentEnergy = new Dictionary<EnergyType, float>();
    private float totalInventoryUsed = 0f;
    
    // Reference to the generated Input Actions class
    private PlayerInputActions playerInputActions; // Name this based on your Input Action Asset file
    
    private Vector2 moveInput; // Stores WASD input as Vector2
    private Vector2 lookInput; // Stores mouse delta for looking
    private bool isSprintPressed; // True if sprint key is held
    private bool showInventory = false;
    private Camera playerCamera; // This will be the Camera component from playerCameraGameObject
    
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

        // Initialize Input Actions for local player early if it's a local player prefab already in scene
        // This is a safety net; primary initialization is in Initialize()
        // Note: isLocalPlayer might not be set yet in Awake if Initialize hasn't run.
        // Consider moving this to Start or ensuring Initialize is called before first Update/OnEnable.
    }

    void Start()
    {
        // Initialize energy dictionary
        foreach (EnergyType energyType in System.Enum.GetValues(typeof(EnergyType)))
        {
            currentEnergy[energyType] = 0f;
        }

        // Set up UI canvases
        SetupUI();

        // Set up energy orb container
        if (energyOrbContainer == null)
        {
            GameObject container = new GameObject("Energy Orbs");
            container.transform.SetParent(transform);
            container.transform.localPosition = Vector3.zero;
            energyOrbContainer = container.transform;
        }
        
        // SnapToSurface(); // Moved to Initialize to ensure it runs before camera setup
    }

    void SnapToSurface()
    {
        Vector3 currentPos = transform.position;
        float currentDistance = currentPos.magnitude;
        
        // Debug.Log($"[PlayerController.SnapToSurface] Current distance from center: {currentDistance}, SphereRadius: {this.sphereRadius}");
        
        // Check if player is too far from the desired hover distance
        if (Mathf.Abs(currentDistance - (this.sphereRadius + desiredSurfaceOffset)) > 0.5f)
        {
            Vector3 targetHoverPos = currentPos.normalized * (this.sphereRadius + desiredSurfaceOffset);
            transform.position = targetHoverPos;
            // Debug.Log($"[PlayerController.SnapToSurface] Snapped to hover position: {targetHoverPos} (magnitude: {targetHoverPos.magnitude})");
        }
        
        // Orient player to stand on sphere
        transform.up = transform.position.normalized;
    }

    void OnEnable()
    {
        if (playerInputActions != null)
        {
            playerInputActions.Gameplay.Enable();
        }
        // If OnEnable is called before Initialize for a local player (e.g. prefab in scene)
        // and playerInputActions hasn't been created yet.
        else if (isLocalPlayer && playerInputActions == null) 
        {
            playerInputActions = new PlayerInputActions();
            playerInputActions.Gameplay.Enable();
            Debug.LogWarning("[PlayerController.OnEnable] playerInputActions was null for local player, created and enabled. Ensure Initialize is called promptly.");
        }
    }

    void OnDisable()
    {
        playerInputActions?.Gameplay.Disable();
    }

    public void Initialize(Player data, bool isLocal, float worldSphereRadius)
    {
        playerData = data;
        isLocalPlayer = isLocal;
        this.sphereRadius = worldSphereRadius; 
        lastPosition = transform.position;
        targetPosition = transform.position;
        Debug.Log($"[PlayerController.Initialize] Name: {playerData.Name}, IsLocal: {isLocalPlayer}, SphereRadius: {this.sphereRadius}");

        if (isLocalPlayer)
        {
            if (playerInputActions == null) // Ensure it's created if not already
            {
                playerInputActions = new PlayerInputActions();
            }
            // Enable actions if not already (e.g. if OnEnable was called before Initialize)
            if (!playerInputActions.Gameplay.enabled)
            {
                 playerInputActions.Gameplay.Enable();
            }
        }

        SnapToSurface();
        SetupPlayerAppearance();

        if (isLocalPlayer)
        {
            SetupLocalPlayerCamera();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        UpdateNameDisplay();
        SubscribeToEnergyEvents();
        InitializeEnergyState();

        isInitialized = true;
        Debug.Log($"Initialized player {data.Name} (Local: {isLocalPlayer})");
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
        
        if (energyAura != null)
        {
            var main = energyAura.main;
            main.startColor = isLocalPlayer ? Color.yellow : Color.white;
            var emission = energyAura.emission;
            emission.rateOverTime = isLocalPlayer ? 20f : 10f;
        }
    }

    void SetupLocalPlayerCamera()
    {
        if (!isLocalPlayer) return;

        Debug.Log($"[PlayerController.SetupLocalPlayerCamera] Attempting to set up local player camera. Current Camera.main: {(Camera.main != null ? Camera.main.name : "NULL")}");

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
                Debug.LogWarning($"[PlayerController] Another camera ('{currentMain.name}') is currently tagged as MainCamera. Player's camera will attempt to take over.");
            }

            playerCameraGameObject.tag = "MainCamera";
            Debug.Log($"[PlayerController] Player camera '{playerCamera.name}' (GameObject: '{playerCameraGameObject.name}') tagged as MainCamera.");

            if (Camera.main == playerCamera) {
                Debug.Log("[PlayerController] Player camera is now successfully set as Camera.main.");
            } else {
                Debug.LogError($"[PlayerController] FAILED to set player camera as Camera.main. Current Camera.main is: {(Camera.main != null ? Camera.main.name : "NULL")}");
            }
            
            DebugCameraState();
        }
        else
        {
            Debug.LogError("[PlayerController] playerCameraGameObject is not assigned in the Inspector for the local player!");
        }
    }

    void SetupUI()
    {
        if (nameCanvas != null)
        {
            nameCanvas.worldCamera = Camera.main; // Will be updated if Camera.main changes
            nameCanvas.transform.localPosition = Vector3.up * 2.5f;
            if (nameText != null) nameText.text = "";
        }
        
        if (inventoryCanvas != null)
        {
            inventoryCanvas.worldCamera = Camera.main; // Will be updated
            inventoryCanvas.transform.localPosition = Vector3.up * 3.5f;
            inventoryCanvas.gameObject.SetActive(false);
        }
    }

    void DebugCameraState()
    {
        if (!isLocalPlayer || playerCamera == null) return;
        // Minimal logging to avoid spam, focus on critical state
        // Debug.Log($"[PlayerController.DebugCameraState] PlayerCam: {playerCamera.name}, Parent: {playerCamera.transform.parent?.name ?? "NULL"}, LocalPos: {playerCamera.transform.localPosition}");
    }

    void Update()
    {
        if (!isInitialized) return;

        if (isLocalPlayer)
        {
            HandleInput();
            HandleMovement();
        }

        UpdateMovementAnimation();
        UpdateEnergyOrbs();
        UpdateUIOrientation();
        
        // Periodic debug for camera if needed, less frequently
        // if (isLocalPlayer && Time.frameCount % 300 == 0) DebugCameraState();
    }

    void HandleInput()
    {
        if (playerInputActions == null)
        {
            if (isLocalPlayer) Debug.LogWarning("[PlayerController.HandleInput] playerInputActions is null for local player. Input will not be processed.");
            return;
        }

        // Read all gameplay inputs
        moveInput = playerInputActions.Gameplay.Move.ReadValue<Vector2>();
        // --- DEBUG A/D PRESSES ---
        if (Mathf.Abs(moveInput.x) > 0.01f) // If A or D is pressed
        {
            Debug.Log($"[PlayerController.HandleInput] MoveInput X (A/D): {moveInput.x}");
        }
        // --- END DEBUG ---

        // Check if "Look" action exists and read it
        var lookAction = playerInputActions.Gameplay.Get().FindAction("Look");
        if (lookAction != null)
        {
            lookInput = lookAction.ReadValue<Vector2>();
        }
        else
        {
            lookInput = Vector2.zero; // Default if action not found
        }

        // Check if "Sprint" action exists and read its state
        var sprintAction = playerInputActions.Gameplay.Get().FindAction("Sprint");
        if (sprintAction != null)
        {
            isSprintPressed = sprintAction.IsPressed();
        }
        else
        {
            isSprintPressed = false; // Default if action not found
        }
        
        // --- Mouse Look ---
        // Mouse X input controls player body's yaw (rotation around Up axis)
        // Mouse Y input controls camera's pitch (rotation around its local Right axis)
        if (playerCamera != null && lookInput.sqrMagnitude > 0.001f) // Only apply look if there's input
        {
            // Yaw (Player Body Rotation from Mouse X)
            // Note: lookInput.x is typically mouse delta X.
            // mouseSensitivity should be tuned considering no Time.deltaTime here.
            float yawDelta = lookInput.x * mouseSensitivity * Time.deltaTime; // Using Time.deltaTime for smoother/framerate independent rotation speed
            transform.Rotate(transform.up, yawDelta, Space.World); // Rotate around player's local up axis
            
            // Pitch (Camera Rotation from Mouse Y)
            // Note: lookInput.y is typically mouse delta Y.
            float currentPitch = playerCamera.transform.localEulerAngles.x;
            if (currentPitch > 180f) currentPitch -= 360f; // Normalize to -180 to 180 range
            
            // A negative lookInput.y (mouse down) should typically pitch camera up.
            // So, we subtract it from current pitch.
            float pitchAmount = -lookInput.y * mouseSensitivity * Time.deltaTime; // Using Time.deltaTime
            float newPitch = currentPitch + pitchAmount;
            newPitch = Mathf.Clamp(newPitch, -89f, 89f); // Clamp pitch to avoid flipping
            
            playerCamera.transform.localRotation = Quaternion.Euler(newPitch, 0f, 0f);
        }
        
        // --- Other Actions ---
        if (playerInputActions.Gameplay.ToggleInventory.WasPressedThisFrame())
        {
            ToggleInventory();
        }

        // Example for Interact key (if you add an "Interact" action)
        // var interactAction = playerInputActions.Gameplay.FindAction("Interact");
        // if (playerInputActions.Gameplay.Get().FindAction("Interact") != null && playerInputActions.Gameplay.Get().FindAction("Interact").WasPressedThisFrame())
        // {
        //     TryInteract();
        // }
    }

    void HandleMovement()
    {
        if (!isLocalPlayer) return;

        // --- Player Rotation from A/D keys ---
        float rotationInput = moveInput.x; // Get X component from WASD (A/D)
        if (Mathf.Abs(rotationInput) > 0.01f)
        {
            // --- DEBUG A/D ROTATION ---
            Debug.Log($"[PlayerController.HandleMovement] Applying rotation. Input: {rotationInput}, Speed: {playerRotationSpeed}, DeltaTime: {Time.deltaTime}");
            // --- END DEBUG ---
            transform.Rotate(transform.up, rotationInput * playerRotationSpeed * Time.deltaTime, Space.World); // Rotate around player's local up axis
        }

        // --- Player Forward/Backward Movement from W/S keys ---
        float forwardInput = moveInput.y; // Get Y component from WASD (W/S)
        if (Mathf.Abs(forwardInput) > 0.01f)
        {
            float currentSpeed = isSprintPressed ? runSpeed : walkSpeed;
            Vector3 moveDirectionIntent = transform.forward * forwardInput; // Move along player's current forward

            // Project movement onto the sphere's tangent plane
            Vector3 surfaceNormal = transform.position.normalized; // Player's up should be sphere normal
            Vector3 actualMoveDirection = Vector3.ProjectOnPlane(moveDirectionIntent, surfaceNormal).normalized;
            
            Vector3 movement = actualMoveDirection * currentSpeed * Time.deltaTime;
            targetPosition = transform.position + movement;
        }
        else
        {
            // If no forward/backward input, target position is current position
            // This ensures snapping still happens if only rotating
            targetPosition = transform.position;
        }

        // --- Apply Movement and Snap to Sphere ---
        // Ensure the new position is on the sphere surface at the desired offset
        targetPosition = targetPosition.normalized * (sphereRadius + desiredSurfaceOffset);
        transform.position = targetPosition;

        // Ensure player remains upright on the sphere (transform.up points away from sphere center)
        transform.up = transform.position.normalized;

        // TODO: Send position and rotation updates to the server for multiplayer
        // if (GameManager.IsConnected())
        // {
        //     SpacetimeDB.Types.DbVector3 dbPos = new SpacetimeDB.Types.DbVector3 { X = transform.position.x, Y = transform.position.y, Z = transform.position.z };
        //     // SpacetimeDB.Types.DbQuaternion dbRot = new SpacetimeDB.Types.DbQuaternion { X = transform.rotation.x, Y = transform.rotation.y, Z = transform.rotation.z, W = transform.rotation.w };
        //     // GameManager.Conn.Reducers.UpdatePlayerTransform(dbPos, dbRot); 
        // }
    }

    void UpdateMovementAnimation()
    {
        if (playerAnimator != null)
        {
            bool isMovingForwardOrBackward;
            bool isSprinting;

            if (isLocalPlayer)
            {
                // For local player, animation is driven by input for responsiveness
                isMovingForwardOrBackward = Mathf.Abs(moveInput.y) > 0.01f;
                isSprinting = isMovingForwardOrBackward && isSprintPressed;
            }
            else
            {
                // For remote players, animation is driven by actual position change
                float speed = (transform.position - lastPosition).magnitude / Time.deltaTime;
                isMovingForwardOrBackward = speed > 0.1f; // Threshold for detecting movement
                // We don't know remote player's sprint input, so assume walking speed or derive from speed if playerData has it
                isSprinting = isMovingForwardOrBackward && (speed > (walkSpeed + runSpeed) * 0.5f); // Basic guess for sprint
            }
            
            playerAnimator.SetBool(IsWalking, isMovingForwardOrBackward);
            playerAnimator.SetBool(IsRunning, isSprinting);
            
            if (isMovingForwardOrBackward)
            {
                float animationSpeedMultiplier = isSprinting ? runSpeed / walkSpeed : 1.0f;
                playerAnimator.SetFloat(WalkSpeed, animationSpeedMultiplier); 
                
                // Play walk sound for local player
                if (isLocalPlayer && !audioSource.isPlaying && walkSound != null)
                {
                    audioSource.clip = walkSound;
                    audioSource.Play();
                }
            }
            else
            {
                // Stop walk sound for local player
                if (isLocalPlayer && audioSource.isPlaying && audioSource.clip == walkSound)
                {
                    audioSource.Stop();
                }
            }
        }
        lastPosition = transform.position; 
    }

    void UpdateEnergyOrbs()
    {
        if (energyOrbContainer != null)
        {
            energyOrbContainer.Rotate(Vector3.up, orbRotationSpeed * Time.deltaTime);
        }
        UpdateEnergyOrbPositions();
    }

    void UpdateEnergyOrbPositions()
    {
        int orbIndex = 0;
        
        foreach (var kvp in currentEnergy)
        {
            if (kvp.Value > 0f && orbIndex < maxVisibleOrbs)
            {
                GameObject orb = GetOrCreateEnergyOrb(orbIndex, kvp.Key);
                
                float angle = (orbIndex / (float)maxVisibleOrbs) * 360f;
                Vector3 orbPosition = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * orbRadius,
                    Mathf.Sin(Time.time * orbBobbingSpeed + orbIndex) * 0.3f + 1.5f,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * orbRadius
                );
                
                orb.transform.localPosition = orbPosition;
                orb.SetActive(true);
                
                float scale = Mathf.Lerp(0.3f, 0.8f, kvp.Value / (playerData.InventoryCapacity > 0 ? (playerData.InventoryCapacity / 6f) : 1f));
                orb.transform.localScale = Vector3.one * scale;
                
                orbIndex++;
            }
        }
        
        for (int i = orbIndex; i < energyOrbs.Count; i++)
        {
            if (energyOrbs[i] != null) energyOrbs[i].SetActive(false);
        }
    }

    GameObject GetOrCreateEnergyOrb(int index, EnergyType energyType)
    {
        while (energyOrbs.Count <= index) energyOrbs.Add(null);
        
        if (energyOrbs[index] == null)
        {
            GameObject orb = Instantiate(energyOrbPrefab, energyOrbContainer);
            orb.name = $"Energy Orb {index}";
            energyOrbs[index] = orb;
        }
        
        GameObject orbObj = energyOrbs[index];
        var renderer = orbObj.GetComponent<Renderer>();
        if (renderer != null && energyMaterials != null && (int)energyType < energyMaterials.Length && energyMaterials[(int)energyType] != null)
        {
            renderer.material = energyMaterials[(int)energyType];
        }
        return orbObj;
    }

    void UpdateUIOrientation()
    {
        Camera cam = Camera.main; 
        if (cam == null && playerCamera != null) cam = playerCamera; 
        if (cam == null) return;
        
        if (nameCanvas != null && nameCanvas.gameObject.activeInHierarchy)
        {
            if (nameCanvas.worldCamera != cam) nameCanvas.worldCamera = cam;
            nameCanvas.transform.LookAt(nameCanvas.transform.position + cam.transform.rotation * Vector3.forward,
                                        cam.transform.rotation * Vector3.up);
        }
        
        if (inventoryCanvas != null && inventoryCanvas.gameObject.activeInHierarchy)
        {
            if (inventoryCanvas.worldCamera != cam) inventoryCanvas.worldCamera = cam;
            inventoryCanvas.transform.LookAt(inventoryCanvas.transform.position + cam.transform.rotation * Vector3.forward,
                                             cam.transform.rotation * Vector3.up);
        }
    }

    public void UpdateData(Player newData, float worldSphereRadius)
    {
        Vector3 oldPosition = transform.position;
        playerData = newData;
        this.sphereRadius = worldSphereRadius;

        if (!isLocalPlayer)
        {
            Vector3 newDbPosition = new Vector3(newData.Position.X, newData.Position.Y, newData.Position.Z);
            targetPosition = newDbPosition.normalized * (sphereRadius + desiredSurfaceOffset); 
            
            if (Vector3.Distance(oldPosition, targetPosition) > 0.01f) 
            {
                StartCoroutine(SmoothMoveTo(targetPosition)); 
            }
        }
        UpdateNameDisplay();
    }

    System.Collections.IEnumerator SmoothMoveTo(Vector3 targetPosOnSurface) 
    {
        Vector3 startPos = transform.position;
        float journeyDuration = 0.2f; 
        float elapsedTime = 0f;
        
        while (elapsedTime < journeyDuration)
        {
            elapsedTime += Time.deltaTime;
            float fraction = Mathf.Clamp01(elapsedTime / journeyDuration);
            
            transform.position = Vector3.Lerp(startPos, targetPosOnSurface, fraction);
            
            Vector3 up = transform.position.normalized;
            transform.rotation = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
            
            yield return null;
        }
        transform.position = targetPosOnSurface; 
        transform.up = transform.position.normalized; 
    }

    void SubscribeToEnergyEvents()
    {
        if (GameManager.Conn?.Db?.EnergyStorage != null) 
        {
            GameManager.Conn.Db.EnergyStorage.OnInsert += OnEnergyStorageInsert;
            GameManager.Conn.Db.EnergyStorage.OnUpdate += OnEnergyStorageUpdate;
            GameManager.Conn.Db.EnergyStorage.OnDelete += OnEnergyStorageDelete;
            Debug.Log($"Subscribed to energy storage events for player {playerData?.Name ?? "UNKNOWN"}");
        }
    }

    void InitializeEnergyState()
    {
        totalInventoryUsed = 0f;
        foreach (EnergyType energyType in System.Enum.GetValues(typeof(EnergyType)))
        {
            currentEnergy[energyType] = 0f;
        }
        UpdateInventoryDisplay();
    }

    void OnEnergyStorageInsert(EventContext ctx, EnergyStorage storage)
    {
        if (playerData != null && IsPlayerEnergyStorage(storage))
        {
            currentEnergy[storage.EnergyType] = storage.Amount;
            RecalculateTotalEnergy();
            UpdateInventoryDisplay();
            UpdateEnergyEffects();
            Debug.Log($"Player {playerData.Name} gained {storage.Amount} {storage.EnergyType} energy (Insert)");
        }
    }

    void OnEnergyStorageUpdate(EventContext ctx, EnergyStorage oldStorage, EnergyStorage newStorage)
    {
        if (playerData != null && IsPlayerEnergyStorage(newStorage))
        {
            float oldAmount = currentEnergy.ContainsKey(newStorage.EnergyType) ? currentEnergy[newStorage.EnergyType] : 0f;
            currentEnergy[newStorage.EnergyType] = newStorage.Amount;
            RecalculateTotalEnergy();
            UpdateInventoryDisplay();
            UpdateEnergyEffects();
            
            float change = newStorage.Amount - oldAmount;
            if (Mathf.Abs(change) > 0.01f) 
            {
                if (change > 0) PlaySound(energyCollectSound);
                else PlaySound(energySpendSound);
                Debug.Log($"Player {playerData.Name} energy {newStorage.EnergyType} changed by {change} to {newStorage.Amount} (Update)");
            }
        }
    }

    void OnEnergyStorageDelete(EventContext ctx, EnergyStorage storage)
    {
        if (playerData != null && IsPlayerEnergyStorage(storage))
        {
            currentEnergy[storage.EnergyType] = 0f;
            RecalculateTotalEnergy();
            UpdateInventoryDisplay();
            UpdateEnergyEffects();
            Debug.Log($"Player {playerData.Name} lost all {storage.EnergyType} energy (Delete)");
        }
    }

    bool IsPlayerEnergyStorage(EnergyStorage storage)
    {
        return playerData != null && storage.OwnerType == "player" && storage.OwnerId == playerData.PlayerId;
    }

    void RecalculateTotalEnergy()
    {
        totalInventoryUsed = 0f;
        foreach (var kvp in currentEnergy) totalInventoryUsed += kvp.Value;
    }

    void UpdateEnergyEffects()
    {
        if (energyAura != null)
        {
            var emission = energyAura.emission;
            emission.rateOverTime = 10f + (playerData.InventoryCapacity > 0 ? (totalInventoryUsed / playerData.InventoryCapacity) : 0) * 40f;
        }
        
        if (playerLight != null)
        {
            float baseIntensity = isLocalPlayer ? 2f : 1f;
            float energyMultiplier = 1f + (playerData.InventoryCapacity > 0 ? (totalInventoryUsed / playerData.InventoryCapacity) : 0) * 0.5f;
            playerLight.intensity = baseIntensity * energyMultiplier;
        }
    }

    void UpdateNameDisplay()
    {
        if (nameText != null && playerData != null)
        {
            nameText.text = playerData.Name;
            nameText.color = isLocalPlayer ? Color.yellow : Color.white;
        }
        if (nameCanvas != null) nameCanvas.gameObject.SetActive(!isLocalPlayer);
    }

    void UpdateInventoryDisplay()
    {
        if (inventoryText != null && playerData != null)
        {
            string inventoryInfo = $"Inventory ({totalInventoryUsed:F1}/{(playerData.InventoryCapacity > 0 ? playerData.InventoryCapacity.ToString("F1") : "N/A")})\n";
            foreach (var kvp in currentEnergy)
            {
                if (kvp.Value > 0.01f) inventoryInfo += $"{kvp.Key}: {kvp.Value:F1}\n";
            }
            inventoryText.text = inventoryInfo;
        }
    }

    void ToggleInventory()
    {
        showInventory = !showInventory;
        if (inventoryCanvas != null) inventoryCanvas.gameObject.SetActive(showInventory);
    }

    void TryInteract()
    {
        Camera cam = playerCamera ?? Camera.main; 
        if (cam == null) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 5f))
        {
            var puddleController = hit.collider.GetComponent<EnergyPuddleController>();
            if (puddleController != null)
            {
                puddleController.OnPlayerInteract();
                PlaySound(energyCollectSound);
                return;
            }
            
            var sphereController = hit.collider.GetComponent<DistributionSphereController>();
            if (sphereController != null)
            {
                sphereController.ToggleCoverageVisualization();
                return;
            }
            Debug.Log($"Interacted with {hit.collider.name}");
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    public void OnEnergyCollected(EnergyType energyType, float amount)
    {
        PlaySound(energyCollectSound);
        if (energyAura != null) energyAura.Emit(Mathf.RoundToInt(amount * 2));
    }

    public void OnEnergySpent(EnergyType energyType, float amount)
    {
        PlaySound(energySpendSound);
        if (energyAura != null)
        {
            var main = energyAura.main;
            var originalSpeed = main.startSpeed;
            main.startSpeed = originalSpeed.constant * 2f;
            Invoke(nameof(ResetParticleSpeed), 0.5f);
        }
    }

    void ResetParticleSpeed()
    {
        if (energyAura != null)
        {
            var main = energyAura.main;
            main.startSpeed = 2f;
        }
    }

    void OnDestroy()
    {
        if (GameManager.Conn?.Db?.EnergyStorage != null)
        {
            GameManager.Conn.Db.EnergyStorage.OnInsert -= OnEnergyStorageInsert;
            GameManager.Conn.Db.EnergyStorage.OnUpdate -= OnEnergyStorageUpdate;
            GameManager.Conn.Db.EnergyStorage.OnDelete -= OnEnergyStorageDelete;
        }
        
        playerInputActions?.Dispose(); 
        
        StopAllCoroutines();
        
        if (isLocalPlayer && Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!isInitialized) return;

        Gizmos.color = Color.green;
        Camera cam = playerCamera ?? Camera.main;
        if (cam != null)
        {
            Gizmos.DrawRay(cam.transform.position, cam.transform.forward * 5f);
        }
        
        Gizmos.color = Color.yellow;
        if (energyOrbContainer != null) 
        {
            for (int i = 0; i < maxVisibleOrbs; i++)
            {
                float angle = (i / (float)maxVisibleOrbs) * 360f;
                Vector3 orbLocalPosition = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * orbRadius,
                    1.5f, 
                    Mathf.Sin(angle * Mathf.Deg2Rad) * orbRadius
                );
                Vector3 orbWorldPosition = energyOrbContainer.TransformPoint(orbLocalPosition);
                Gizmos.DrawWireSphere(orbWorldPosition, 0.3f);
            }
        }

#if UNITY_EDITOR
        if (playerData != null) 
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 4f, 
                $"Player: {playerData.Name}\nLocal: {isLocalPlayer}\nEnergy: {totalInventoryUsed:F1}/{(playerData.InventoryCapacity > 0 ? playerData.InventoryCapacity.ToString("F1") : "N/A")}");
        }
#endif
    }
}
