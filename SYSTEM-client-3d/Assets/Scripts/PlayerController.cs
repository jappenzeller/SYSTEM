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
    public float orbRotationSpeed = 30f;
    public int maxVisibleOrbs = 6;
    
    [Header("Animation Settings")]
    public Animator playerAnimator;
    public float walkSpeed = 5f;
    public float runSpeed = 10f;
    public float rotationSpeed = 720f;
    public float hoverHeight = 0.1f;
    public float hoverSpeed = 2f;
    
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
    
    private Player playerData;
    private bool isLocalPlayer = false;
    private bool isInitialized = false;
    
    // Movement and positioning
    private Vector3 lastPosition;
    private Vector3 targetPosition;
    private bool isMoving = false;
    private float sphereRadius = 100f; // World radius
    
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
        
        SnapToSurface();
    }


    void SnapToSurface()
    {
        float worldRadius = 100f; // Or get from WorldManager
        
        Vector3 currentPos = transform.position;
        float currentDistance = currentPos.magnitude;
        
        Debug.Log($"[PlayerController] Current distance from center: {currentDistance}");
        
        if (currentDistance < worldRadius - 0.5f || currentDistance > worldRadius + 5f)
        {
            // We're inside the sphere or too far out, snap to surface
            Vector3 surfacePos = currentPos.normalized * worldRadius;
            transform.position = surfacePos;
            Debug.Log($"[PlayerController] Snapped to surface: {surfacePos} (magnitude: {surfacePos.magnitude})");
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

    public void Initialize(Player data, bool isLocal)
    {
        playerData = data;
        isLocalPlayer = isLocal;
        lastPosition = transform.position;
        targetPosition = transform.position;

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

        SnapToSurface();
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
        // Find the existing camera
        playerCamera = Camera.main;
        
        if (playerCamera != null)
        {
            Debug.Log($"Found existing camera: {playerCamera.name} at {playerCamera.transform.position}");
            
            // Remove any scripts that might interfere
            var existingControllers = playerCamera.GetComponents<MonoBehaviour>();
            foreach (var controller in existingControllers)
            {
                if (controller.GetType().Name.Contains("Controller") && controller != this)
                {
                    Destroy(controller);
                    Debug.Log($"Removed {controller.GetType().Name} from camera");
                }
            }
            
            // Take ownership of the camera
            playerCamera.transform.SetParent(transform);
            playerCamera.transform.localPosition = Vector3.up * 1.8f; // Eye level
            playerCamera.transform.localRotation = Quaternion.identity;
            
            // Rename to indicate it's now the player camera
            playerCamera.name = "Player Camera";
            
            Debug.Log("Camera successfully attached to player");
        }
        else
        {
            // Create new camera if somehow none exists
            GameObject cameraObj = new GameObject("Player Camera");
            playerCamera = cameraObj.AddComponent<Camera>();
            cameraObj.AddComponent<AudioListener>();
            
            playerCamera.transform.SetParent(transform);
            playerCamera.transform.localPosition = Vector3.up * 1.8f;
            playerCamera.transform.localRotation = Quaternion.identity;
            
            Debug.Log("Created new camera for player");
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

        // Handle input for local player
        if (isLocalPlayer)
        {
            HandleInput();
            HandleMovement();
        }

        // Update animations and visuals
        UpdateMovementAnimation();
        UpdateEnergyOrbs();
        UpdateHoverEffect();

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
        if (moveInput.magnitude > 0.1f)
        {
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
            Vector3 newPosition = transform.position + moveDirection * speed * Time.deltaTime;
            
            // Project to sphere surface
            newPosition = newPosition.normalized * sphereRadius;
            
            // Update position (in real game, this would send to server)
            transform.position = newPosition;
            
            // Maintain proper orientation on sphere
            Vector3 up = transform.position.normalized;
            transform.rotation = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
            
            isMoving = true;
        }
        else
        {
            isMoving = false;
        }
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
                    Mathf.Cos(angle * Mathf.Deg2Rad) * orbRadius,
                    Mathf.Sin(Time.time * hoverSpeed + orbIndex) * 0.3f + 1.5f,
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

    void UpdateHoverEffect()
    {
        // Gentle hover animation
        Vector3 basePosition = transform.position.normalized * sphereRadius;
        float hoverOffset = Mathf.Sin(Time.time * hoverSpeed) * hoverHeight;
        transform.position = basePosition + transform.up * hoverOffset;
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

    public void UpdateData(Player newData)
    {
        Vector3 oldPosition = transform.position;
        playerData = newData;
        
        // Smooth position update for remote players
        if (!isLocalPlayer)
        {
            Vector3 newPosition = new Vector3(newData.Position.X, newData.Position.Y, newData.Position.Z);
            targetPosition = newPosition;
            
            // Start movement animation
            if (Vector3.Distance(oldPosition, newPosition) > 0.1f)
            {
                isMoving = true;
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
            
            transform.position = Vector3.Lerp(startPos, targetPos, fraction);
            
            // Maintain orientation to sphere surface
            Vector3 up = transform.position.normalized;
            transform.rotation = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
            
            yield return null;
        }
        
        isMoving = false;
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