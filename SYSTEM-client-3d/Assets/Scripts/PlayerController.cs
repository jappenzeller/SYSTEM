using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
    // hoverHeight and hoverSpeed are no longer used due to constant surface offset
    
    [Header("Materials")]
    public Material localPlayerMaterial;
    public Material remotePlayerMaterial;
    public Material[] energyMaterials = new Material[6]; // For each energy type
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip walkSound;
    public AudioClip energyCollectSound;
    public AudioClip energySpendSound;
    
    [Header("Input (Local Player Only)")]
    public KeyCode interactKey = KeyCode.E;
    public KeyCode inventoryKey = KeyCode.Tab;
    public float mouseSensitivity = 2f;
    
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
    
    // Local player input - New Input System
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction interactAction;
    private InputAction inventoryAction;
    private InputAction escapeAction;
    
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool isSprintPressed;
    private bool showInventory = false;
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

        // Setup input actions
        SetupInputActions();
        
        // SnapToSurface(); // Moved to Initialize to ensure it runs before camera setup
    }


    void SnapToSurface()
    {
        Vector3 currentPos = transform.position;
        float currentDistance = currentPos.magnitude;
        
        Debug.Log($"[PlayerController] Current distance from center: {currentDistance}");
        
        // Check if player is too far from the desired hover distance
        if (Mathf.Abs(currentDistance - (this.sphereRadius + desiredSurfaceOffset)) > 0.5f)
        {
            // We're inside the sphere or too far out, snap to surface
            Vector3 targetHoverPos = currentPos.normalized * (this.sphereRadius + desiredSurfaceOffset);
            transform.position = targetHoverPos;
            Debug.Log($"[PlayerController] Snapped to hover position: {targetHoverPos} (magnitude: {targetHoverPos.magnitude})");
        }
        
        // Orient player to stand on sphere
        transform.up = transform.position.normalized;
    }

    void SetupInputActions()
    {
        // Movement
        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        
        // Mouse look
        lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
        
        // Sprint
        sprintAction = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
        
        // Interact
        interactAction = new InputAction("Interact", InputActionType.Button, $"<Keyboard>/{interactKey.ToString().ToLower()}");
        
        // Inventory
        inventoryAction = new InputAction("Inventory", InputActionType.Button, $"<Keyboard>/{inventoryKey.ToString().ToLower()}");
        
        // Escape
        escapeAction = new InputAction("Escape", InputActionType.Button, "<Keyboard>/escape");
    }

    void OnEnable()
    {
        // Enable all input actions
        moveAction?.Enable();
        lookAction?.Enable();
        sprintAction?.Enable();
        interactAction?.Enable();
        inventoryAction?.Enable();
        escapeAction?.Enable();
    }

    void OnDisable()
    {
        // Disable all input actions
        moveAction?.Disable();
        lookAction?.Disable();
        sprintAction?.Disable();
        interactAction?.Disable();
        inventoryAction?.Disable();
        escapeAction?.Disable();
    }

    public void Initialize(Player data, bool isLocal, float worldSphereRadius)
    {
        playerData = data;
        isLocalPlayer = isLocal;
        this.sphereRadius = worldSphereRadius; // Set the sphere radius from WorldManager
        lastPosition = transform.position;
        targetPosition = transform.position;
        Debug.Log($"[PlayerController.Initialize] Name: {playerData.Name}, IsLocal: {isLocalPlayer}, SphereRadius: {this.sphereRadius}");

        // Ensure player is correctly positioned and oriented BEFORE camera setup
        SnapToSurface();

        // Set up appearance
        SetupPlayerAppearance();

        // Set up camera for local player
        if (isLocalPlayer)
        {
            SetupLocalPlayerCamera();

            // Lock cursor for first-person control
            Cursor.lockState = CursorLockMode.Locked;
        }

        // Update name display
        UpdateNameDisplay();

        // Subscribe to energy storage events for this player
        SubscribeToEnergyEvents();

        // Load initial energy state (will be populated by events)
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
                // Fallback colors
                playerRenderer.material.color = isLocalPlayer ? Color.yellow : Color.white;
            }
        }
        
        // Configure light
        if (playerLight != null)
        {
            playerLight.color = isLocalPlayer ? Color.yellow : Color.white;
            playerLight.intensity = isLocalPlayer ? 2f : 1f;
            playerLight.range = 10f;
        }
        
        // Configure particle aura
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
            // Ensure the GameObject itself is active
            playerCameraGameObject.SetActive(true);
            playerCamera = playerCameraGameObject.GetComponent<Camera>();

            if (playerCamera == null)
            {
                Debug.LogError("[PlayerController] Assigned playerCameraGameObject does not have a Camera component!");
                return;
            }

            // Ensure the Camera component is enabled
            if (!playerCamera.enabled)
            {
                Debug.LogWarning("[PlayerController] Player camera component was disabled. Enabling it now.");
                playerCamera.enabled = true;
            }

            // Check if another camera is currently Camera.main
            Camera currentMain = Camera.main;
            if (currentMain != null && currentMain != playerCamera)
            {
                Debug.LogWarning($"[PlayerController] Another camera ('{currentMain.name}') is currently tagged as MainCamera. Player's camera will attempt to take over.");
                // Optionally, you could disable the other camera here:
                // currentMain.gameObject.SetActive(false);
                // Or untag it:
                // currentMain.tag = "Untagged";
                // For now, just logging and proceeding to tag ours.
            }

            // Tag the player's camera as MainCamera
            playerCameraGameObject.tag = "MainCamera";
            Debug.Log($"[PlayerController] Player camera '{playerCamera.name}' (GameObject: '{playerCameraGameObject.name}') tagged as MainCamera.");

            // Verify Camera.main after tagging
            if (Camera.main == playerCamera) {
                Debug.Log("[PlayerController] Player camera is now successfully set as Camera.main.");
            } else {
                Debug.LogError($"[PlayerController] FAILED to set player camera as Camera.main. Current Camera.main is: {(Camera.main != null ? Camera.main.name : "NULL")}");
            }
            
            DebugCameraState(); // Call debug state immediately
        }
        else
        {
            Debug.LogError("[PlayerController] playerCameraGameObject is not assigned in the Inspector for the local player!");
        }
    }


    void SetupUI()
    {
        // Set up name canvas
        if (nameCanvas != null)
        {
            nameCanvas.worldCamera = Camera.main;
            nameCanvas.transform.localPosition = Vector3.up * 2.5f;
            
            if (nameText != null)
            {
                nameText.text = "";
            }
        }
        
        // Set up inventory canvas (only for local player)
        if (inventoryCanvas != null)
        {
            inventoryCanvas.worldCamera = Camera.main;
            inventoryCanvas.transform.localPosition = Vector3.up * 3.5f;
            inventoryCanvas.gameObject.SetActive(false);
        }
    }

    void DebugCameraState()
    {
        if (!isLocalPlayer) return;

        if (playerCamera != null)
        {
        /*    Debug.Log($"[PlayerController] Camera state:");
            Debug.Log($"  - Position: {playerCamera.transform.position}");
            Debug.Log($"  - Parent: {playerCamera.transform.parent?.name ?? "NULL"}");
            Debug.Log($"  - Is child of player: {playerCamera.transform.IsChildOf(transform)}");
            Debug.Log($"  - Local position: {playerCamera.transform.localPosition}");
            Debug.Log($"  - Local rotation: {playerCamera.transform.localEulerAngles}");*/
        }
        else
        {
            Debug.LogError("[PlayerController] playerCamera is null!");
        }
    }

    void Update()
    {
        if (!isInitialized) return;

        if (isLocalPlayer && Time.frameCount % 60 == 0) // Log status periodically for local player
        {
            Debug.Log($"[PlayerController.Update] Local Player Update. Initialized: {isInitialized}, IsLocal: {isLocalPlayer}");

            // Debug.Log($"[PlayerController.Update] Local Player Update. Initialized: {isInitialized}, MoveInput: {moveInput}, LookInput: {lookInput}");
        }


        // Handle input for local player
        if (isLocalPlayer)
        {
            HandleInput();
            HandleMovement();
        }

        // Update animations and visuals
        UpdateMovementAnimation();
        UpdateEnergyOrbs();

        // Update UI to face camera
        UpdateUIOrientation();
        
        if (Time.frameCount % 120 == 0)
        {
            DebugCameraState();
        }
    }

    void HandleInput()
    {
        // Read input values
        moveInput = moveAction.ReadValue<Vector2>();
        lookInput = lookAction.ReadValue<Vector2>();
        isSprintPressed = sprintAction.IsPressed();
        // This log is useful for initial input debugging but can be spammy.
        // Only log if there's actual movement input or periodically.
        // if (moveInput.magnitude > 0.01f || Time.frameCount % 120 == 0) {
        //     Debug.Log($"[PlayerController.HandleInput] MoveInput: {moveInput}, LookInput: {lookInput}, Sprint: {isSprintPressed}");
        // }
        
        // Mouse look
        Vector2 mouseDelta = lookInput * mouseSensitivity * 0.1f; // Scale down for new input system
        
        if (playerCamera != null)
        {
            // Apply mouse rotation
            transform.Rotate(Vector3.up, mouseDelta.x);
            
            // Apply vertical look to camera only
            float currentPitch = playerCamera.transform.localEulerAngles.x;
            if (currentPitch > 180f) currentPitch -= 360f;
            
            float newPitch = currentPitch - mouseDelta.y;
            newPitch = Mathf.Clamp(newPitch, -90f, 90f);
            
            playerCamera.transform.localRotation = Quaternion.Euler(newPitch, 0f, 0f);
        }
        
        // Handle button presses
        if (inventoryAction.WasPressedThisFrame())
        {
            ToggleInventory();
        }
        
        if (interactAction.WasPressedThisFrame())
        {
            TryInteract();
        }
        
        if (escapeAction.WasPressedThisFrame())
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? 
                CursorLockMode.None : CursorLockMode.Locked;
        }
    }

    void HandleMovement()
    {
        Debug.Log($"[PlayerController.HandleMovement] Called. moveInput: {moveInput}");
        if (moveInput.magnitude > 0.1f)
        {
            Debug.Log($"[PlayerController.HandleMovement] moveInput.magnitude ({moveInput.magnitude}) > 0.1f. Processing movement.");
            // Calculate movement direction relative to player orientation
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            
            // Project onto sphere surface (remove radial component)
            Vector3 playerToCenter = -transform.position.normalized;
            forward = Vector3.ProjectOnPlane(forward, playerToCenter).normalized;
            right = Vector3.ProjectOnPlane(right, playerToCenter).normalized;
            
            Vector3 moveDirection = (forward * moveInput.y + right * moveInput.x).normalized;
            
            // Move player
            float speed = isSprintPressed ? runSpeed : walkSpeed;
            // Debug.Log($"[PlayerController.HandleMovement] Calculated moveDirection: {moveDirection}, Speed: {speed}");

            Vector3 newPosition = transform.position + moveDirection * speed * Time.deltaTime;
            // Debug.Log($"[PlayerController.HandleMovement] Position before snap: {newPosition}");
            
            // Project to sphere surface + desired offset
            newPosition = newPosition.normalized * (sphereRadius + desiredSurfaceOffset);
            // Debug.Log($"[PlayerController.HandleMovement] Position after snap: {newPosition}");
            
            // Update position (in real game, this would send to server)
            transform.position = newPosition;
            
            // Maintain proper orientation on sphere
            Vector3 up = transform.position.normalized;
            // Debug.Log($"[PlayerController.HandleMovement] Current transform.up: {transform.up}, Target up (normalized position): {up}");
            transform.rotation = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
        }
        // else
        // {
            // Debug.Log($"[PlayerController.HandleMovement] moveInput.magnitude ({moveInput.magnitude}) <= 0.1f. No movement processed.");
        // }
    }

    void UpdateMovementAnimation()
    {
        if (playerAnimator != null)
        {
            bool wasMoving = Vector3.Distance(transform.position, lastPosition) > 0.01f;
            
            playerAnimator.SetBool(IsWalking, wasMoving);
            playerAnimator.SetBool(IsRunning, wasMoving && isSprintPressed);
            
            if (wasMoving)
            {
                float speed = Vector3.Distance(transform.position, lastPosition) / Time.deltaTime;
                playerAnimator.SetFloat(WalkSpeed, speed / walkSpeed);
                
                // Play footstep sounds
                if (!audioSource.isPlaying && walkSound != null)
                {
                    audioSource.clip = walkSound;
                    audioSource.Play();
                }
            }
            else
            {
                if (audioSource.clip == walkSound)
                {
                    audioSource.Stop();
                }
            }
        }
        
        lastPosition = transform.position;
    }

    void UpdateEnergyOrbs()
    {
        // Rotate energy orbs around player
        if (energyOrbContainer != null)
        {
            energyOrbContainer.Rotate(Vector3.up, orbRotationSpeed * Time.deltaTime);
        }
        
        // Update orb positions and visibility based on energy levels
        UpdateEnergyOrbPositions();
    }

    void UpdateEnergyOrbPositions()
    {
        int orbIndex = 0;
        float totalEnergy = 0f;
        
        // Calculate total energy
        foreach (var kvp in currentEnergy)
        {
            totalEnergy += kvp.Value;
        }
        
        // Position orbs for each energy type
        foreach (var kvp in currentEnergy)
        {
            if (kvp.Value > 0f && orbIndex < maxVisibleOrbs)
            {
                // Get or create orb
                GameObject orb = GetOrCreateEnergyOrb(orbIndex, kvp.Key);
                
                // Position around player
                float angle = (orbIndex / (float)maxVisibleOrbs) * 360f;
                Vector3 orbPosition = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * orbRadius, // X position
                    Mathf.Sin(Time.time * orbBobbingSpeed + orbIndex) * 0.3f + 1.5f, // Y position (bobbing)
                    Mathf.Sin(angle * Mathf.Deg2Rad) * orbRadius
                );
                
                orb.transform.localPosition = orbPosition;
                orb.SetActive(true);
                
                // Scale based on energy amount
                float scale = Mathf.Lerp(0.3f, 0.8f, kvp.Value / (playerData.InventoryCapacity / 6f));
                orb.transform.localScale = Vector3.one * scale;
                
                orbIndex++;
            }
        }
        
        // Hide unused orbs
        for (int i = orbIndex; i < energyOrbs.Count; i++)
        {
            if (energyOrbs[i] != null)
            {
                energyOrbs[i].SetActive(false);
            }
        }
    }

    GameObject GetOrCreateEnergyOrb(int index, EnergyType energyType)
    {
        // Expand orb list if needed
        while (energyOrbs.Count <= index)
        {
            energyOrbs.Add(null);
        }
        
        // Create orb if it doesn't exist
        if (energyOrbs[index] == null)
        {
            GameObject orb = Instantiate(energyOrbPrefab, energyOrbContainer);
            orb.name = $"Energy Orb {index}";
            energyOrbs[index] = orb;
        }
        
        // Apply energy type material
        GameObject orbObj = energyOrbs[index];
        var renderer = orbObj.GetComponent<Renderer>();
        if (renderer != null && energyMaterials != null && (int)energyType < energyMaterials.Length)
        {
            renderer.material = energyMaterials[(int)energyType];
        }
        
        return orbObj;
    }

    void UpdateUIOrientation()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;
        
        // Update name canvas
        if (nameCanvas != null)
        {
            nameCanvas.transform.LookAt(mainCamera.transform);
            nameCanvas.transform.Rotate(0, 180, 0);
        }
        
        // Update inventory canvas
        if (inventoryCanvas != null && showInventory)
        {
            inventoryCanvas.transform.LookAt(mainCamera.transform);
            inventoryCanvas.transform.Rotate(0, 180, 0);
        }
    }

    public void UpdateData(Player newData, float worldSphereRadius)
    {
        Vector3 oldPosition = transform.position;
        playerData = newData;
        this.sphereRadius = worldSphereRadius; // Update sphere radius
        // Debug.Log($"[PlayerController.UpdateData] Name: {playerData.Name}, IsLocal: {isLocalPlayer}, Updated SphereRadius: {this.sphereRadius}");

        // Smooth position update for remote players
        if (!isLocalPlayer)
        {
            Vector3 newPosition = new Vector3(newData.Position.X, newData.Position.Y, newData.Position.Z);
            targetPosition = newPosition.normalized * (sphereRadius + desiredSurfaceOffset); // Ensure target is at hover height
            
            // Start movement animation
            if (Vector3.Distance(oldPosition, newPosition) > 0.1f)
            {
                StartCoroutine(SmoothMoveTo(newPosition));
            }
        }
        
        // Update name display
        UpdateNameDisplay();
        
        // Energy state will be populated through events automatically
        // No need to reload since we're using event-driven approach
    }

    System.Collections.IEnumerator SmoothMoveTo(Vector3 targetPos)
    {
        Vector3 startPos = transform.position;
        float journey = 0f;
        float journeyTime = 1f;
        
        while (journey <= journeyTime)
        {
            journey += Time.deltaTime;
            float fraction = journey / journeyTime;
            
            // Lerp towards the target position which is already at the correct hover height
            transform.position = Vector3.Lerp(startPos, targetPos.normalized * (sphereRadius + desiredSurfaceOffset), fraction);
            
            // Maintain orientation to sphere surface
            Vector3 up = transform.position.normalized;
            transform.rotation = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
            
            yield return null;
        }
    }

    void SubscribeToEnergyEvents()
    {
        if (GameManager.Conn != null)
        {
            GameManager.Conn.Db.EnergyStorage.OnInsert += OnEnergyStorageInsert;
            GameManager.Conn.Db.EnergyStorage.OnUpdate += OnEnergyStorageUpdate;
            GameManager.Conn.Db.EnergyStorage.OnDelete += OnEnergyStorageDelete;
            
            Debug.Log($"Subscribed to energy storage events for player {playerData.Name}");
        }
    }

    void InitializeEnergyState()
    {
        // Initialize all energy types to zero
        // Real values will come from SpacetimeDB events
        totalInventoryUsed = 0f;
        
        foreach (EnergyType energyType in System.Enum.GetValues(typeof(EnergyType)))
        {
            currentEnergy[energyType] = 0f;
        }
        
        UpdateInventoryDisplay();
    }

    // Event handlers for energy storage changes
    void OnEnergyStorageInsert(EventContext ctx, EnergyStorage storage)
    {
        if (IsPlayerEnergyStorage(storage))
        {
            currentEnergy[storage.EnergyType] = storage.Amount;
            RecalculateTotalEnergy();
            UpdateInventoryDisplay();
            UpdateEnergyEffects();
            
            Debug.Log($"Player {playerData.Name} gained {storage.Amount} {storage.EnergyType} energy");
        }
    }

    void OnEnergyStorageUpdate(EventContext ctx, EnergyStorage oldStorage, EnergyStorage newStorage)
    {
        if (IsPlayerEnergyStorage(newStorage))
        {
            float oldAmount = currentEnergy[newStorage.EnergyType];
            currentEnergy[newStorage.EnergyType] = newStorage.Amount;
            RecalculateTotalEnergy();
            UpdateInventoryDisplay();
            UpdateEnergyEffects();
            
            float change = newStorage.Amount - oldAmount;
            if (change > 0)
            {
                PlaySound(energyCollectSound);
                Debug.Log($"Player {playerData.Name} gained {change} {newStorage.EnergyType} energy");
            }
            else if (change < 0)
            {
                PlaySound(energySpendSound);
                Debug.Log($"Player {playerData.Name} spent {-change} {newStorage.EnergyType} energy");
            }
        }
    }

    void OnEnergyStorageDelete(EventContext ctx, EnergyStorage storage)
    {
        if (IsPlayerEnergyStorage(storage))
        {
            currentEnergy[storage.EnergyType] = 0f;
            RecalculateTotalEnergy();
            UpdateInventoryDisplay();
            UpdateEnergyEffects();
            
            Debug.Log($"Player {playerData.Name} lost all {storage.EnergyType} energy");
        }
    }

    bool IsPlayerEnergyStorage(EnergyStorage storage)
    {
        return storage.OwnerType == "player" && storage.OwnerId == playerData.PlayerId;
    }

    void RecalculateTotalEnergy()
    {
        totalInventoryUsed = 0f;
        foreach (var kvp in currentEnergy)
        {
            totalInventoryUsed += kvp.Value;
        }
    }

    void UpdateEnergyEffects()
    {
        // Update particle effect intensity based on total energy
        if (energyAura != null)
        {
            var emission = energyAura.emission;
            emission.rateOverTime = 10f + (totalInventoryUsed / playerData.InventoryCapacity) * 40f;
        }
        
        // Update light intensity
        if (playerLight != null)
        {
            float baseIntensity = isLocalPlayer ? 2f : 1f;
            float energyMultiplier = 1f + (totalInventoryUsed / playerData.InventoryCapacity) * 0.5f;
            playerLight.intensity = baseIntensity * energyMultiplier;
        }
    }

    void UpdateNameDisplay()
    {
        if (nameText != null)
        {
            nameText.text = playerData.Name;
            nameText.color = isLocalPlayer ? Color.yellow : Color.white;
        }
        
        // Hide name for local player
        if (nameCanvas != null)
        {
            nameCanvas.gameObject.SetActive(!isLocalPlayer);
        }
    }

    void UpdateInventoryDisplay()
    {
        if (inventoryText != null)
        {
            string inventoryInfo = $"Inventory ({totalInventoryUsed:F1}/{playerData.InventoryCapacity:F1})\n";
            
            foreach (var kvp in currentEnergy)
            {
                if (kvp.Value > 0f)
                {
                    inventoryInfo += $"{kvp.Key}: {kvp.Value:F1}\n";
                }
            }
            
            inventoryText.text = inventoryInfo;
        }
    }

    void ToggleInventory()
    {
        showInventory = !showInventory;
        if (inventoryCanvas != null)
        {
            inventoryCanvas.gameObject.SetActive(showInventory);
        }
    }

    void TryInteract()
    {
        // Raycast to find interactable objects
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, 5f))
        {
            // Check for energy puddles
            var puddleController = hit.collider.GetComponent<EnergyPuddleController>();
            if (puddleController != null)
            {
                puddleController.OnPlayerInteract();
                PlaySound(energyCollectSound);
                return;
            }
            
            // Check for distribution spheres
            var sphereController = hit.collider.GetComponent<DistributionSphereController>();
            if (sphereController != null)
            {
                sphereController.ToggleCoverageVisualization();
                return;
            }
            
            // Check for tunnels (if we add tunnel objects)
            Debug.Log($"Interacted with {hit.collider.name}");
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    public void OnEnergyCollected(EnergyType energyType, float amount)
    {
        // This method is now handled by the OnEnergyStorageInsert/Update events
        // But we can keep it for manual triggering of effects
        PlaySound(energyCollectSound);
        
        // Trigger a visual effect
        if (energyAura != null)
        {
            var burst = energyAura.emission.GetBurst(0);
            energyAura.Emit(Mathf.RoundToInt(amount * 2)); // Burst effect
        }
    }

    public void OnEnergySpent(EnergyType energyType, float amount)
    {
        // This method is now handled by the OnEnergyStorageUpdate events
        // But we can keep it for manual triggering of effects
        PlaySound(energySpendSound);
        
        // Trigger a visual effect (maybe particles flowing away)
        if (energyAura != null)
        {
            var main = energyAura.main;
            var originalSpeed = main.startSpeed;
            main.startSpeed = originalSpeed.constant * 2f; // Faster particles briefly
            
            // Reset after a short time
            Invoke(nameof(ResetParticleSpeed), 0.5f);
        }
    }

    void ResetParticleSpeed()
    {
        if (energyAura != null)
        {
            var main = energyAura.main;
            main.startSpeed = 2f; // Reset to normal speed
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from energy events
        if (GameManager.Conn != null)
        {
            GameManager.Conn.Db.EnergyStorage.OnInsert -= OnEnergyStorageInsert;
            GameManager.Conn.Db.EnergyStorage.OnUpdate -= OnEnergyStorageUpdate;
            GameManager.Conn.Db.EnergyStorage.OnDelete -= OnEnergyStorageDelete;
        }
        
        // Clean up input actions
        moveAction?.Dispose();
        lookAction?.Dispose();
        sprintAction?.Dispose();
        interactAction?.Dispose();
        inventoryAction?.Dispose();
        escapeAction?.Dispose();
        
        // Clean up
        StopAllCoroutines();
        
        if (isLocalPlayer)
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }

    // Debug information
    void OnDrawGizmosSelected()
    {
        if (isInitialized)
        {
            // Draw interaction range
            Gizmos.color = Color.green;
            if (playerCamera != null)
            {
                Gizmos.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * 5f);
            }
            
            // Draw energy orb positions
            Gizmos.color = Color.yellow;
            for (int i = 0; i < maxVisibleOrbs; i++)
            {
                float angle = (i / (float)maxVisibleOrbs) * 360f;
                Vector3 orbPosition = transform.position + new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * orbRadius,
                    1.5f,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * orbRadius
                );
                Gizmos.DrawWireSphere(orbPosition, 0.3f);
            }
        }

#if UNITY_EDITOR
        if (isInitialized)
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 4f, 
                $"Player: {playerData.Name}\nLocal: {isLocalPlayer}\nEnergy: {totalInventoryUsed:F1}/{playerData.InventoryCapacity:F1}");
        }
#endif
    }
}
