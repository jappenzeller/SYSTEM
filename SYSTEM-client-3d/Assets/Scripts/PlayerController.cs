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
    public float mouseSensitivity = 1.0f; 
    public float playerRotationSpeed = 120f; // Degrees per second for A/D rotation
    private Queue<(float time, Quaternion rotation, string source)> rotationHistory = new Queue<(float, Quaternion, string)>();
    private const int MAX_HISTORY = 20;

    // Add this helper method
    private void LogRotationChange(string source, Quaternion rotation)
    {
        rotationHistory.Enqueue((Time.time, rotation, source));
        if (rotationHistory.Count > MAX_HISTORY)
            rotationHistory.Dequeue();
    }

    // Call it in relevant places:
    // - After applying rotation input: LogRotationChange("INPUT", transform.rotation);
    // - After network send: LogRotationChange("SENT", currentRot);
    // - In UpdateData: LogRotationChange("SERVER_UPDATE", new Quaternion(newData.Rotation.X, ...));

    // Add debug command to dump history (call with a key press for testing):
    private void DumpRotationHistory()
    {
        Debug.Log("=== ROTATION HISTORY ===");
        foreach (var entry in rotationHistory)
        {
            Debug.Log($"[{entry.time:F3}] {entry.source}: {entry.rotation.eulerAngles} (Q: {entry.rotation.x:F3},{entry.rotation.y:F3},{entry.rotation.z:F3},{entry.rotation.w:F3})");
        }
        Debug.Log("=== END HISTORY ===");
    }
    
    [Header("Camera Setup (Local Player)")]
    [Tooltip("Assign the Camera GameObject that is a child of this player prefab.")]
    public GameObject playerCameraGameObject; 
    
    private Player playerData;
    private bool isLocalPlayer = false;
    private bool isInitialized = false;
    
    // Movement and positioning
    private Vector3 lastPosition;
    private Vector3 targetPosition;
    private float sphereRadius; 
    private const float desiredSurfaceOffset = 1.0f; 
    
    // Energy visualization
    private List<GameObject> energyOrbs = new List<GameObject>();
    private Dictionary<EnergyType, float> currentEnergy = new Dictionary<EnergyType, float>();
    private float totalInventoryUsed = 0f;
    
    // Reference to the generated Input Actions class
    private PlayerInputActions playerInputActions; 
    
    private Vector2 moveInput; 
    private Vector2 lookInput; 
    private bool isSprintPressed; 
    private bool showInventory = false;

    // Network update timing
    private float lastNetworkUpdateTime = 0f;
    private const float networkUpdateInterval = 0.1f; // Send updates 10 times per second
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
        lastSentPosition = transform.position; // Initialize last sent values
        lastSentRotation = transform.rotation; // Initialize last sent values
        Debug.Log($"[PlayerController.Initialize] Name: {playerData.Name}, IsLocal: {isLocalPlayer}, SphereRadius: {this.sphereRadius}");

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
            nameCanvas.worldCamera = Camera.main; 
            nameCanvas.transform.localPosition = Vector3.up * 2.5f;
            if (nameText != null) nameText.text = "";
        }
        
        if (inventoryCanvas != null)
        {
            inventoryCanvas.worldCamera = Camera.main; 
            inventoryCanvas.transform.localPosition = Vector3.up * 3.5f;
            inventoryCanvas.gameObject.SetActive(false);
        }
    }

    void DebugCameraState()
    {
        if (!isLocalPlayer || playerCamera == null) return;
        // Minimal logging to avoid spam
    }

    void Update()
    {
        if (!isInitialized) return;

        if (isLocalPlayer)
        {
           // Debug.Log("handle input");
            HandleInput(); 
         //   Debug.Log("handle movement");
            HandleMovementAndRotation(); 
        }

        UpdateMovementAnimation();
        UpdateEnergyOrbs();
        UpdateUIOrientation();
    }

    void HandleInput()
    {
        if (playerInputActions == null)
        {
            if (isLocalPlayer) Debug.LogWarning("[PlayerController.HandleInput] playerInputActions is null for local player. Input will not be processed.");
            return;
        }

        moveInput = playerInputActions.Gameplay.Move.ReadValue<Vector2>();
        
        var lookAction = playerInputActions.Gameplay.Get().FindAction("Look");
        if (lookAction != null)
        {
            lookInput = lookAction.ReadValue<Vector2>();
        }
        else
        {
            lookInput = Vector2.zero; 
        }

        var sprintAction = playerInputActions.Gameplay.Get().FindAction("Sprint");
        if (sprintAction != null)
        {
            isSprintPressed = sprintAction.IsPressed();
        }
        else
        {
            isSprintPressed = false; 
        }
        
        if (playerInputActions.Gameplay.ToggleInventory.WasPressedThisFrame())
        {
            ToggleInventory();
        }
    }

    void HandleMovementAndRotation()
    {
       if (!isLocalPlayer) return;

        // --- Player Forward/Backward Movement from W/S keys ---
        float forwardInput = moveInput.y; 
        Vector3 movementThisFrame = Vector3.zero;

        if (Mathf.Abs(forwardInput) > 0.01f)
        {
            float currentSpeed = isSprintPressed ? runSpeed : walkSpeed;
            Vector3 moveDirectionIntent = transform.forward * forwardInput; 

            Vector3 surfaceNormal = transform.position.normalized; 
            Vector3 actualMoveDirection = Vector3.ProjectOnPlane(moveDirectionIntent, surfaceNormal).normalized;
            
            movementThisFrame = actualMoveDirection * currentSpeed * Time.deltaTime;
        }

        // Store the current rotation before position update
        Quaternion currentRotation = transform.rotation;

        // --- Apply Movement and Snap to Sphere (Position first) ---
        if (movementThisFrame.magnitude > 0.001f)
        {
            targetPosition = transform.position + movementThisFrame;
            targetPosition = targetPosition.normalized * (sphereRadius + desiredSurfaceOffset);
            transform.position = targetPosition;
        }

        Vector3 targetPlayerUp = transform.position.normalized;

        // --- Combined Player Yaw Rotation (Mouse X and A/D keys) ---
        float yawFromMouse = lookInput.x * mouseSensitivity;
        float yawFromAD = moveInput.x * playerRotationSpeed * Time.deltaTime; 
        float totalYawThisFrame = yawFromMouse + yawFromAD;

        if (Mathf.Abs(totalYawThisFrame) > Mathf.Epsilon)
        {
            Quaternion beforeRotation = transform.rotation;
            Debug.Log($"[ROTATION INPUT] Before: {beforeRotation.eulerAngles} (Q: {beforeRotation.x:F3},{beforeRotation.y:F3},{beforeRotation.z:F3},{beforeRotation.w:F3})");
            
            // Rotate the current transform based on input
            if (targetPlayerUp != Vector3.zero)
            {
                transform.Rotate(targetPlayerUp, totalYawThisFrame, Space.World);
            }
            
            Debug.Log($"[ROTATION INPUT] After Rotate: {transform.rotation.eulerAngles} (Q: {transform.rotation.x:F3},{transform.rotation.y:F3},{transform.rotation.z:F3},{transform.rotation.w:F3})");
        }

        // --- Align player to be upright on the sphere ---
        // Only realign if we moved or if alignment is off
        if (movementThisFrame.magnitude > 0.001f || Vector3.Angle(transform.up, targetPlayerUp) > 0.1f)
        {
            Vector3 currentForward = transform.forward;
            Vector3 projectedForward = Vector3.ProjectOnPlane(currentForward, targetPlayerUp);

            if (projectedForward.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(projectedForward.normalized, targetPlayerUp);
            }
        }

        // --- Camera Pitch (Mouse Y) ---
        // Ensure playerCamera is the one from playerCameraGameObject
        Camera camToPitch = (playerCameraGameObject != null) ? playerCameraGameObject.GetComponent<Camera>() : null;
        if (camToPitch != null && Mathf.Abs(lookInput.y) > Mathf.Epsilon)
        {
            float currentPitch = playerCamera.transform.localEulerAngles.x;
            if (currentPitch > 180f) currentPitch -= 360f; 
            
            // If lookInput.y is raw mouse delta, Time.deltaTime might make it too slow or frame-dependent.
            // Consider removing Time.deltaTime if lookInput.y is already a per-frame delta.
            float pitchAmountThisFrame = -lookInput.y * mouseSensitivity; // Removed Time.deltaTime for consistency with yawFromMouse
            float newPitch = currentPitch + pitchAmountThisFrame * Time.deltaTime; // Apply deltaTime here if sensitivity is per-second
            newPitch = Mathf.Clamp(newPitch, -89f, 89f); 
            
            camToPitch.transform.localRotation = Quaternion.Euler(newPitch, 0f, 0f);
        }

        // --- Send position and rotation updates to the server ---
        if (Time.time - lastNetworkUpdateTime > networkUpdateInterval)
        {
            if (GameManager.IsConnected() && playerData != null)
            {
                Vector3 currentPos = transform.position;
                Quaternion currentRot = transform.rotation;
                
                // Check if position or rotation has changed significantly enough to warrant an update
                bool positionChanged = Vector3.Distance(currentPos, lastSentPosition) > 0.01f;
                bool rotationChanged = Quaternion.Angle(currentRot, lastSentRotation) > 0.1f;

                if (positionChanged || rotationChanged)
                {
                    // Ensure the quaternion is normalized before sending
                    currentRot.Normalize();
                    Debug.Log($"[NETWORK SEND] Sending update - Pos changed: {positionChanged}, Rot changed: {rotationChanged}");
                    Debug.Log($"[NETWORK SEND] Current Rotation: {currentRot.eulerAngles} (Q: {currentRot.x:F3},{currentRot.y:F3},{currentRot.z:F3},{currentRot.w:F3})");
                    Debug.Log($"[NETWORK SEND] Last Sent Rotation: {lastSentRotation.eulerAngles} (Q: {lastSentRotation.x:F3},{lastSentRotation.y:F3},{lastSentRotation.z:F3},{lastSentRotation.w:F3})");
                    Debug.Log($"[NETWORK SEND] Time: {Time.time:F3}");
                    
                    GameManager.Conn.Reducers.UpdatePlayerPosition(
                        currentPos.x, currentPos.y, currentPos.z,
                        currentRot.x, currentRot.y, currentRot.z, currentRot.w
                    );

                    lastSentPosition = currentPos;
                    lastSentRotation = currentRot;
                }
            }
            lastNetworkUpdateTime = Time.time;
        }
    }




    void UpdateMovementAnimation()
    {
        if (playerAnimator != null)
        {
            bool isMovingForwardOrBackward;
            bool isSprinting;

            if (isLocalPlayer)
            {
                isMovingForwardOrBackward = Mathf.Abs(moveInput.y) > 0.01f;
                isSprinting = isMovingForwardOrBackward && isSprintPressed;
            }
            else
            {
                float speed = (transform.position - lastPosition).magnitude / Time.deltaTime;
                isMovingForwardOrBackward = speed > 0.1f; 
                isSprinting = isMovingForwardOrBackward && (speed > (walkSpeed + runSpeed) * 0.5f); 
            }
            
            playerAnimator.SetBool(IsWalking, isMovingForwardOrBackward);
            playerAnimator.SetBool(IsRunning, isSprinting);
            
            if (isMovingForwardOrBackward)
            {
                float animationSpeedMultiplier = isSprinting ? runSpeed / walkSpeed : 1.0f;
                playerAnimator.SetFloat(WalkSpeed, animationSpeedMultiplier); 
                
                if (isLocalPlayer && !audioSource.isPlaying && walkSound != null)
                {
                    audioSource.clip = walkSound;
                    audioSource.Play();
                }
            }
            else
            {
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
        Debug.Log($"[UPDATE DATA] Called for {(isLocalPlayer ? "LOCAL" : "REMOTE")} player {newData.Name}");
        Debug.Log($"[UPDATE DATA] Server Rotation: {newData.Rotation.X:F3},{newData.Rotation.Y:F3},{newData.Rotation.Z:F3},{newData.Rotation.W:F3}");
        Debug.Log($"[UPDATE DATA] Current Transform Rotation: {transform.rotation.eulerAngles} (Q: {transform.rotation.x:F3},{transform.rotation.y:F3},{transform.rotation.z:F3},{transform.rotation.w:F3})");
        Debug.Log($"[UPDATE DATA] Time: {Time.time:F3}");
            // Store old rotation for comparison
    Quaternion oldRotation = transform.rotation;
        playerData = newData; // Update player data but not transform
        this.sphereRadius = worldSphereRadius;

        // Only update position/rotation for remote players
        if (!isLocalPlayer)
        {
            Vector3 newDbPosition = new Vector3(newData.Position.X, newData.Position.Y, newData.Position.Z);
            targetPosition = newDbPosition.normalized * (sphereRadius + desiredSurfaceOffset); 

            Quaternion newDbRotation = new Quaternion(newData.Rotation.X, newData.Rotation.Y, newData.Rotation.Z, newData.Rotation.W);
            newDbRotation.Normalize();

            StopCoroutine("SmoothMoveAndRotateTo");
           
            StartCoroutine(SmoothMoveAndRotateTo(targetPosition, newDbRotation));
        }
        else
        {
            Debug.Log($"[UPDATE DATA] LOCAL PLAYER - Ignoring server update");
        }

            // Check if rotation changed unexpectedly
        if (isLocalPlayer && Quaternion.Angle(oldRotation, transform.rotation) > 0.1f)
        {
            Debug.LogWarning($"[UPDATE DATA] LOCAL PLAYER ROTATION CHANGED! Old: {oldRotation.eulerAngles}, New: {transform.rotation.eulerAngles}");
        }
        

        UpdateNameDisplay();
    }

    System.Collections.IEnumerator SmoothMoveTo(Vector3 targetPosOnSurface) 
    {
        // This coroutine is now superseded by SmoothMoveAndRotateTo
        // Kept for reference or if you need separate position-only smoothing elsewhere
        yield return SmoothMoveAndRotateTo(targetPosOnSurface, transform.rotation);
    }

    System.Collections.IEnumerator SmoothMoveAndRotateTo(Vector3 targetPosOnSurface, Quaternion targetGlobalRotation)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float journeyDuration = 0.2f; 
        float elapsedTime = 0f;

        while (elapsedTime < journeyDuration)
        {
            elapsedTime += Time.deltaTime;
            float fraction = Mathf.Clamp01(elapsedTime / journeyDuration);

            transform.position = Vector3.Lerp(startPos, targetPosOnSurface, fraction);
            transform.rotation = Quaternion.Slerp(startRot, targetGlobalRotation, fraction);

            Vector3 up = transform.position.normalized;
            transform.rotation = Quaternion.FromToRotation(transform.up, up) * transform.rotation;
            yield return null;
        }
        transform.position = targetPosOnSurface;
        transform.rotation = targetGlobalRotation; // Snap to final rotation
        transform.up = transform.position.normalized; // Final upright correction
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
