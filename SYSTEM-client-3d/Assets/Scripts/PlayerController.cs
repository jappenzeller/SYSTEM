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
    [SerializeField] private float mouseSensitivity = 2f;
    
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
    
    private Player playerData;
    private bool isLocalPlayer = false;
    private bool isInitialized = false;
    
    // Movement and positioning
    private Vector3 lastPosition;
    private Vector3 targetPosition;
    private float sphereRadius; 
    private const float desiredSurfaceOffset = 1.0f; 
    
    // Reference to the generated Input Actions class
    private PlayerInputActions playerInputActions; 
    
    private Vector2 moveInput; 

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
        // Set up UI canvases
        SetupUI();
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

        isInitialized = true;
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
                Debug.LogWarning($"[PlayerController] Another camera ('{currentMain.name}') is currently tagged as MainCamera. Player's camera will attempt to take over.");
            }

            playerCameraGameObject.tag = "MainCamera";

            if (Camera.main == playerCamera) {
                //Debug.Log(("[PlayerController] Player camera is now successfully set as Camera.main.");
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
            HandleInput(); 
            HandleMovementAndRotation(); 
        }

        UpdateMovementAnimation();
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
    }

    void HandleMovementAndRotation()
    {
        // Movement
        float speed = walkSpeed; // Just use walk speed for now
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
            
            float positionDelta = Vector3.Distance(currentPos, lastSentPosition);
            float rotationDelta = Quaternion.Angle(currentRot, lastSentRotation);
            
            if (positionDelta > 0.1f || rotationDelta > 1f)
            {
                if (GameManager.IsConnected() && GameManager.Conn != null && playerData != null)
                {
                    // FIXED: Use the correct signature with WorldCoords, DbVector3, and DbQuaternion
                    GameManager.Conn.Reducers.UpdatePlayerPosition(
                        playerData.CurrentWorld,  // Use the player's current world coordinates
                        new DbVector3 { X = currentPos.x, Y = currentPos.y, Z = currentPos.z },
                        new DbQuaternion { X = currentRot.x, Y = currentRot.y, Z = currentRot.z, W = currentRot.w }
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
                isSprinting = false; // No sprint for now
            }
            else
            {
                float speed = (transform.position - lastPosition).magnitude / Time.deltaTime;
                isMovingForwardOrBackward = speed > 0.1f; 
                isSprinting = false; // No sprint detection for remote players either
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

    void UpdateUIOrientation()
    {
        if (nameCanvas != null && Camera.main != null)
        {
            nameCanvas.transform.LookAt(Camera.main.transform);
            nameCanvas.transform.Rotate(0, 180, 0);
        }
    }

    public void UpdateNameDisplay()
    {
        if (nameText != null && playerData != null)
        {
            nameText.text = playerData.Name;
            nameCanvas.gameObject.SetActive(!isLocalPlayer);
        }
    }

    public void UpdateData(Player newData)
    {
        playerData = newData;
        
        if (!isLocalPlayer)
        {
            // Smoothly update position and rotation for remote players
            targetPosition = new Vector3(newData.Position.X, newData.Position.Y, newData.Position.Z);
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10f);
            
            // Apply rotation from server for remote players
            Quaternion targetRotation = new Quaternion(
                newData.Rotation.X,
                newData.Rotation.Y,
                newData.Rotation.Z,
                newData.Rotation.W
            );
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            
            SnapToSurface();
        }
        
        UpdateNameDisplay();
    }

    void OnDestroy()
    {
        playerInputActions?.Dispose();
    }
}