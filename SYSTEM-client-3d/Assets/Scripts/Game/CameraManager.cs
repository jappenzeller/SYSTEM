using System.Collections;
using UnityEngine;
using SYSTEM.Game;

namespace SYSTEM.Game
{
    /// <summary>
    /// Centralized camera management system for Minecraft-style third-person camera.
    /// Follows the local player with smooth orbital camera movement.
    /// No Cinemachine dependency - direct camera control for simplicity.
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        [Header("Camera Configuration")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private bool autoFindCamera = true;
        
        [Header("Orbital Camera Settings")]
        [SerializeField] private bool useOrbitalCamera = true;
        [SerializeField] private float cameraDistance = 6f; // Distance behind character
        [SerializeField] private float cameraHeight = 2.5f; // Height above character center
        [SerializeField] private float minPitch = -60f; // Minimum pitch angle (looking down)
        [SerializeField] private float maxPitch = 85f; // Maximum pitch angle (looking up)

        [Header("Camera Smoothing")]
        [SerializeField] private float smoothTime = 0.05f; // Reduced for more responsive following
        [SerializeField] private bool enableSmoothing = false; // DISABLED to prevent pitch/Y caching - enable if needed
        [SerializeField] private bool bypassAllSmoothing = true; // FORCE fresh position every frame for testing

        [Header("Collision Detection")]
        [SerializeField] private float collisionOffset = 0.2f; // Offset from collision point
        [SerializeField] private LayerMask collisionMask = -1; // Layers to check for collision
        [SerializeField] private bool enableCollisionDetection = true;
        
        [Header("Debug")]
        [SerializeField] private bool debugLogging = true;
        
        // Singleton instance
        private static CameraManager _instance;
        public static CameraManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<CameraManager>();
                    if (_instance == null)
                    {
                        UnityEngine.Debug.LogError("[CameraManager] No CameraManager instance found in scene!");
                    }
                }
                return _instance;
            }
        }
        
        // Current tracking state
        private Transform currentTarget;
        private PlayerTracker playerTracker;
        private bool isInitialized = false;

        // Camera orbital state
        private float currentPitch = 0f; // Current pitch angle - THE ONLY pitch storage
        private Vector3 currentCameraPosition; // Current camera position for smoothing
        private Vector3 cameraVelocity; // For smooth damp
        private Quaternion currentCameraRotation; // Current camera rotation for smoothing

        // Movement state tracking to reset smoothing when state changes
        private bool wasMovingLastFrame = false;

        // Debug tracking - DO NOT use these for calculations, only for debug comparisons
        private Vector3 lastTargetForward = Vector3.zero; // Only for detecting changes between frames
        private Vector3 lastIdealPosition = Vector3.zero;  // Only for detecting jumps
        private float lastLoggedPitch = 0f; // For tracking pitch changes
        
        void Awake()
        {
            // Singleton pattern enforcement
            if (_instance != null && _instance != this)
            {
                UnityEngine.Debug.LogWarning("[CameraManager] Duplicate CameraManager instance detected, destroying this instance");
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            Log("CameraManager Awake() - Singleton instance created");
        }
        
        void Start()
        {
            Initialize();
        }
        
        void LateUpdate()
        {
            if (currentTarget != null && playerCamera != null && useOrbitalCamera)
            {
                UpdateCameraFollowCharacter();
            }
        }
        
        void OnEnable()
        {
            // Subscribe to PlayerTracker events if available
            SubscribeToPlayerEvents();
        }
        
        void OnDisable()
        {
            // Unsubscribe from PlayerTracker events
            UnsubscribeFromPlayerEvents();
        }
        
        void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            UnsubscribeFromPlayerEvents();
        }
        
        /// <summary>
        /// Initialize the camera system and set up event subscriptions
        /// </summary>
        public void Initialize()
        {
            if (isInitialized)
            {
                Log("Already initialized, skipping");
                return;
            }
            
            Log("Initializing CameraManager");
            
            // Find or create the Camera
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
                if (playerCamera == null)
                {
                    // Try to find main camera in scene
                    playerCamera = Camera.main;
                    if (playerCamera == null)
                    {
                        // Create a new camera
                        GameObject cameraObj = new GameObject("PlayerCamera");
                        cameraObj.transform.parent = transform;
                        playerCamera = cameraObj.AddComponent<Camera>();
                        playerCamera.fieldOfView = 60f;
                        playerCamera.nearClipPlane = 0.1f;
                        playerCamera.farClipPlane = 1000f;
                        playerCamera.tag = "MainCamera";

                        // Add audio listener
                        if (cameraObj.GetComponent<AudioListener>() == null)
                        {
                            cameraObj.AddComponent<AudioListener>();
                        }

                        Log("Created new camera");
                    }
                    else
                    {
                        Log($"Found main camera: {playerCamera.name}");
                    }
                }
                else
                {
                    Log($"Found camera in children: {playerCamera.name}");
                }
            }
            else
            {
                Log($"Camera already assigned: {playerCamera.name}");
            }
            
            // Configure camera for spherical world following
            ConfigureCamera();
            
            // Initialize camera position for smoothing
            if (playerCamera != null)
            {
                currentCameraPosition = playerCamera.transform.position;
                currentCameraRotation = playerCamera.transform.rotation;
            }
            
            // Find PlayerTracker
            playerTracker = FindFirstObjectByType<PlayerTracker>();
            if (playerTracker == null)
            {
                LogWarning("PlayerTracker not found, will retry when available");
                StartCoroutine(WaitForPlayerTracker());
            }
            else
            {
                SubscribeToPlayerEvents();
            }
            
            isInitialized = true;
            Log("CameraManager initialization complete");
        }
        
        /// <summary>
        /// Configure the camera for spherical world following
        /// </summary>
        private void ConfigureCamera()
        {
            if (playerCamera == null) return;

            // Initialize camera settings
            if (playerCamera.fieldOfView == 0)
            {
                playerCamera.fieldOfView = 60f;
            }

            // Ensure we have an audio listener
            if (playerCamera.GetComponent<AudioListener>() == null)
            {
                playerCamera.gameObject.AddComponent<AudioListener>();
            }

            Log("Camera configured for Minecraft-style orbital third-person view");
        }
        
        /// <summary>
        /// Wait for PlayerTracker to become available (for WebGL compatibility)
        /// </summary>
        private IEnumerator WaitForPlayerTracker()
        {
            float waitTime = 0f;
            const float maxWaitTime = 10f;
            const float checkInterval = 0.5f;
            
            while (playerTracker == null && waitTime < maxWaitTime)
            {
                yield return new WaitForSeconds(checkInterval);
                waitTime += checkInterval;
                
                playerTracker = FindFirstObjectByType<PlayerTracker>();
                if (playerTracker != null)
                {
                    Log("PlayerTracker found after waiting");
                    SubscribeToPlayerEvents();
                    break;
                }
            }
            
            if (playerTracker == null)
            {
                LogError("PlayerTracker not found after waiting 10 seconds");
            }
        }
        
        /// <summary>
        /// Subscribe to PlayerTracker events
        /// </summary>
        private void SubscribeToPlayerEvents()
        {
            if (playerTracker == null)
            {
                playerTracker = FindFirstObjectByType<PlayerTracker>();
                if (playerTracker == null)
                {
                    LogWarning("Cannot subscribe to events - PlayerTracker not found");
                    return;
                }
            }
            
            // Unsubscribe first to prevent duplicate subscriptions
            UnsubscribeFromPlayerEvents();
            
            // Subscribe to local player change event
            playerTracker.OnLocalPlayerChanged += OnLocalPlayerChanged;
            Log("Subscribed to PlayerTracker.OnLocalPlayerChanged event");
            
            // Check if there's already a local player
            var localPlayer = playerTracker.GetLocalPlayer();
            if (localPlayer != null)
            {
                Log("Local player already exists, switching camera");
                OnLocalPlayerChanged(localPlayer);
            }
        }
        
        /// <summary>
        /// Unsubscribe from PlayerTracker events
        /// </summary>
        private void UnsubscribeFromPlayerEvents()
        {
            if (playerTracker != null)
            {
                playerTracker.OnLocalPlayerChanged -= OnLocalPlayerChanged;
                Log("Unsubscribed from PlayerTracker events");
            }
        }
        
        /// <summary>
        /// Handle local player change event from PlayerTracker
        /// </summary>
        private void OnLocalPlayerChanged(PlayerTracker.PlayerData playerData)
        {
            if (playerData == null)
            {
                Log("Local player changed to null, clearing camera target");
                SetFollowTarget(null);
                return;
            }
            
            Log($"Local player changed to: {playerData.Name} (ID: {playerData.PlayerId})");
            
            // Find the player GameObject in the scene
            StartCoroutine(FindAndSetPlayerTarget(playerData));
        }
        
        /// <summary>
        /// Find the player GameObject and set as camera target
        /// </summary>
        private IEnumerator FindAndSetPlayerTarget(PlayerTracker.PlayerData playerData)
        {
            // Wait a frame to ensure player GameObject is spawned
            yield return null;
            
            // Find all PlayerController instances
            var playerControllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            
            foreach (var controller in playerControllers)
            {
                var controllerData = controller.GetPlayerData();
                if (controllerData != null && controllerData.PlayerId == playerData.PlayerId)
                {
                    Log($"Found player GameObject for {playerData.Name}, setting as camera target");
                    SetFollowTarget(controller.transform);
                    yield break;
                }
            }
            
            // If not found immediately, wait and retry
            LogWarning($"Player GameObject not found immediately for {playerData.Name}, retrying...");
            
            float waitTime = 0f;
            const float maxWaitTime = 5f;
            const float checkInterval = 0.2f;
            
            while (waitTime < maxWaitTime)
            {
                yield return new WaitForSeconds(checkInterval);
                waitTime += checkInterval;
                
                playerControllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
                foreach (var controller in playerControllers)
                {
                    var controllerData = controller.GetPlayerData();
                    if (controllerData != null && controllerData.PlayerId == playerData.PlayerId)
                    {
                        Log($"Found player GameObject for {playerData.Name} after {waitTime:F1}s");
                        SetFollowTarget(controller.transform);
                        yield break;
                    }
                }
            }
            
            LogError($"Failed to find player GameObject for {playerData.Name} after {maxWaitTime}s");
        }
        
        /// <summary>
        /// Set the camera to follow a specific transform target
        /// </summary>
        public void SetFollowTarget(Transform playerTransform)
        {
            if (playerCamera == null)
            {
                LogError("Cannot set follow target - playerCamera is null");
                
                // Try to find camera again
                playerCamera = Camera.main;
                if (playerCamera == null)
                {
                    LogError("Still no Camera found!");
                    return;
                }
            }
            
            currentTarget = playerTransform;

            if (playerTransform == null)
            {
                // Clear target reference
                Log("Camera target cleared");
                return;
            }

            if (useOrbitalCamera)
            {
                // Initialize orbital camera state
                // Don't reset pitch if we already have a target (preserving look angle)
                if (currentTarget == null)
                {
                    currentPitch = 0f; // Only reset pitch when first setting a target
                }
                else if (debugLogging)
                {
                    UnityEngine.Debug.Log($"[CAM] SetFollowTarget called but preserving pitch: {currentPitch:F1}°");
                }

                // Initialize camera position for smooth start
                if (playerTransform != null)
                {
                    // Use CURRENT values from the player - don't cache them
                    Vector3 characterPos = playerTransform.position;
                    Vector3 characterUp = playerTransform.up;
                    Vector3 characterForward = playerTransform.forward; // Current forward, not cached!
                    Vector3 characterRight = Vector3.Cross(characterUp, characterForward).normalized;
                    // DON'T recalculate forward - use the player's actual forward direction

                    // Set initial camera position behind character using CURRENT forward
                    currentCameraPosition = characterPos - characterForward * cameraDistance + characterUp * cameraHeight;
                    currentCameraRotation = Quaternion.LookRotation(characterForward, characterUp);

                    if (debugLogging)
                    {
                        UnityEngine.Debug.Log($"[CAM INIT] Setting initial camera position using player forward: {characterForward:F3}");
                    }

                    // Snap camera immediately to position
                    if (playerCamera != null)
                    {
                        playerCamera.transform.position = currentCameraPosition;
                        playerCamera.transform.rotation = currentCameraRotation;
                    }
                }

                Log($"Camera target set for orbital following: {playerTransform.name}");
            }
            else
            {
                // For non-orbital camera, you might implement a different follow style here
                Log($"Camera target set: {playerTransform.name}");
            }

            // For spherical worlds, adjust camera to respect player's up vector
            AdjustCameraForSphericalWorld(playerTransform);
            
            Log($"Camera now following: {playerTransform.name}");
        }
        
        /// <summary>
        /// Adjust camera settings for spherical world geometry
        /// </summary>
        private void AdjustCameraForSphericalWorld(Transform playerTransform)
        {
            if (playerCamera == null || playerTransform == null) return;

            // Direct camera control for spherical world - no Cinemachine needed
            // The orbital camera system handles spherical following automatically

            Log("Camera configured for spherical world - using direct orbital following");
        }
        
        /// <summary>
        /// Get the currently active camera
        /// </summary>
        public Camera GetActiveCamera()
        {
            return playerCamera;
        }
        
        /// <summary>
        /// Get the current follow target
        /// </summary>
        public Transform GetCurrentTarget()
        {
            return currentTarget;
        }
        
        /// <summary>
        /// Check if camera has a valid target
        /// </summary>
        public bool HasTarget()
        {
            return currentTarget != null;
        }
        
        /// <summary>
        /// Force refresh camera settings
        /// </summary>
        public void RefreshCamera()
        {
            if (currentTarget != null)
            {
                SetFollowTarget(currentTarget);
                Log("Camera settings refreshed");
            }
        }

        /// <summary>
        /// Set camera pitch angle for vertical look (Minecraft style)
        /// </summary>
        public void SetCameraPitch(float pitch)
        {
            // Store old pitch for debugging
            float oldPitch = currentPitch;

            // Clamp the pitch to valid range - DIRECT assignment, no smoothing
            currentPitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            // Debug to detect if pitch is being restored
            if (debugLogging && Mathf.Abs(oldPitch - currentPitch) > 0.1f)
            {
                StartCoroutine(CheckPitchNextFrame(currentPitch));
            }

            if (debugLogging)
            {
                UnityEngine.Debug.Log($"[CAMERA] SetCameraPitch - Input: {pitch:F2}°, Clamped: {currentPitch:F2}°");
            }
        }

        /// <summary>
        /// Update camera with Minecraft-style orbital third-person view
        /// </summary>
        private void UpdateCameraFollowCharacter()
        {
            if (currentTarget == null || playerCamera == null) return;

            // Get PlayerController once for reuse
            PlayerController playerController = currentTarget.GetComponent<PlayerController>();
            bool isMoving = playerController != null && playerController.IsMoving();

            // Check if pitch changed unexpectedly
            if (debugLogging && Mathf.Abs(currentPitch - lastLoggedPitch) > 0.1f)
            {
                UnityEngine.Debug.LogWarning($"==================================================");
                UnityEngine.Debug.LogWarning($"[CAM PITCH CHANGE] Pitch changed from {lastLoggedPitch:F1}° to {currentPitch:F1}°");
                UnityEngine.Debug.LogWarning($"  Player state: {(isMoving ? "MOVING" : "IDLE")}");
                UnityEngine.Debug.LogWarning($"==================================================");
                lastLoggedPitch = currentPitch;
            }

            // ALWAYS get fresh values from the player - these are the current, real-time values
            Vector3 characterPos = currentTarget.position;
            Vector3 characterUp = currentTarget.up; // Sphere surface normal (radial from center)
            Vector3 characterForward = currentTarget.forward; // This MUST be current, not cached

            // Debug to verify we're getting the current forward and pitch state
            if (debugLogging && Time.frameCount % 30 == 0)
            {
                UnityEngine.Debug.Log($"[CAM] State: {(isMoving ? "MOVING" : "IDLE")}, Pitch: {currentPitch:F1}°, Forward: {characterForward:F3}");
            }

            // Calculate right vector from current values
            Vector3 characterRight = Vector3.Cross(characterUp, characterForward).normalized;
            // DON'T recalculate forward - use the player's actual forward direction

            // Track if forward vector suddenly changes
            if (debugLogging && lastTargetForward != Vector3.zero)
            {
                float forwardChange = Vector3.Angle(lastTargetForward, characterForward);
                if (forwardChange > 1f)
                {
                    UnityEngine.Debug.LogWarning($"==================================================");
                    UnityEngine.Debug.LogWarning($"[CAM] Player's forward changed by {forwardChange:F1}°!");
                    UnityEngine.Debug.LogWarning($"  Previous frame forward: {lastTargetForward:F3}");
                    UnityEngine.Debug.LogWarning($"  Current frame forward: {characterForward:F3}");
                    UnityEngine.Debug.LogWarning($"  Right: {characterRight:F3}");
                    UnityEngine.Debug.LogWarning($"  Up: {characterUp:F3}");
                    UnityEngine.Debug.LogWarning($"  Player Pos: {characterPos:F2}");
                    UnityEngine.Debug.LogWarning($"==================================================");
                }
            }

            // Check if we're using the wrong forward during movement
            if (debugLogging && playerController != null)
            {
                // Log every frame during movement to catch the exact moment of the bug
                if (isMoving)
                {
                    UnityEngine.Debug.LogWarning($"[CAM WHILE MOVING] Frame {Time.frameCount}");
                    UnityEngine.Debug.LogWarning($"  Character forward from target: {characterForward:F3}");
                    UnityEngine.Debug.LogWarning($"  Target.forward direct check: {currentTarget.forward:F3}");
                    UnityEngine.Debug.LogWarning($"  Character position: {characterPos:F2}");

                    // Double-check that we're using the actual current forward
                    if (characterForward != currentTarget.forward)
                    {
                        UnityEngine.Debug.LogError($"[CAM ERROR] Forward mismatch during movement!");
                        UnityEngine.Debug.LogError($"  Using: {characterForward:F3}");
                        UnityEngine.Debug.LogError($"  Should be: {currentTarget.forward:F3}");
                    }

                    // Check if the forward we're using matches what the player sees
                    float angleFromLastFrame = Vector3.Angle(lastTargetForward, characterForward);
                    if (lastTargetForward != Vector3.zero && angleFromLastFrame > 30f)
                    {
                        UnityEngine.Debug.LogError($"[CAM SNAP] Forward jumped {angleFromLastFrame:F1}° in one frame during movement!");
                        UnityEngine.Debug.LogError($"  This is likely the spawn rotation bug manifesting!");
                    }
                }
                else if (Time.frameCount % 60 == 0) // Only log occasionally when idle
                {
                    UnityEngine.Debug.Log($"[CAM IDLE] Using forward: {characterForward:F3}");
                }
            }

            lastTargetForward = characterForward;

            // Debug output for tracking forward vector issues
            if (debugLogging && Time.frameCount % 30 == 0) // Every half second
            {
                UnityEngine.Debug.Log($"[CAM UPDATE] Player Forward: {characterForward:F2} | Right: {characterRight:F2} | Up: {characterUp:F2}");
            }

            // Calculate ideal camera position using spherical coordinates
            Vector3 idealCameraPosition = CalculateOrbitalCameraPosition(
                characterPos,
                characterForward,
                characterRight,
                characterUp
            );

            // Debug large position changes with more detail
            if (debugLogging)
            {
                float distanceFromCurrent = Vector3.Distance(currentCameraPosition, idealCameraPosition);
                float distanceFromLastIdeal = lastIdealPosition != Vector3.zero ? Vector3.Distance(lastIdealPosition, idealCameraPosition) : 0f;

                if (distanceFromCurrent > 2f)
                {
                    UnityEngine.Debug.LogError($"============================================");
                    UnityEngine.Debug.LogError($"[CAMERA JUMP DETECTED!] Distance: {distanceFromCurrent:F2}m");
                    UnityEngine.Debug.LogError($"============================================");
                    UnityEngine.Debug.LogError($"CURRENT STATE:");
                    UnityEngine.Debug.LogError($"  Current cam pos: {currentCameraPosition:F3}");
                    UnityEngine.Debug.LogError($"  Last ideal pos: {lastIdealPosition:F3}");
                    UnityEngine.Debug.LogError($"  NEW ideal pos: {idealCameraPosition:F3}");
                    UnityEngine.Debug.LogError($"");
                    UnityEngine.Debug.LogError($"PLAYER STATE:");
                    UnityEngine.Debug.LogError($"  Player pos: {characterPos:F3}");
                    UnityEngine.Debug.LogError($"  Player forward: {characterForward:F3}");
                    UnityEngine.Debug.LogError($"  Player up: {characterUp:F3}");
                    UnityEngine.Debug.LogError($"  Player right: {characterRight:F3}");
                    UnityEngine.Debug.LogError($"");
                    UnityEngine.Debug.LogError($"CALCULATION BREAKDOWN:");
                    UnityEngine.Debug.LogError($"  Base offset = -forward * distance + up * height");
                    UnityEngine.Debug.LogError($"  = -{characterForward:F3} * {cameraDistance}");
                    UnityEngine.Debug.LogError($"  + {characterUp:F3} * {cameraHeight}");
                    Vector3 backwardOffset = -characterForward * cameraDistance;
                    Vector3 upOffset = characterUp * cameraHeight;
                    UnityEngine.Debug.LogError($"  Backward offset: {backwardOffset:F3}");
                    UnityEngine.Debug.LogError($"  Up offset: {upOffset:F3}");
                    UnityEngine.Debug.LogError($"  Combined offset: {(backwardOffset + upOffset):F3}");
                    UnityEngine.Debug.LogError($"  Final position: {characterPos:F3} + offset = {idealCameraPosition:F3}");

                    if (distanceFromLastIdeal > 2f)
                    {
                        UnityEngine.Debug.LogError($"");
                        UnityEngine.Debug.LogError($">>> IDEAL POSITION JUMPED {distanceFromLastIdeal:F2}m from last ideal!");
                    }
                    UnityEngine.Debug.LogError($"============================================");
                }

                lastIdealPosition = idealCameraPosition;
            }

            // Apply collision detection if enabled
            if (enableCollisionDetection)
            {
                idealCameraPosition = HandleCameraCollision(characterPos, idealCameraPosition, characterUp);
            }

            // Check if movement state changed - if so, reset smoothing to prevent old values
            if (isMoving != wasMovingLastFrame)
            {
                // Movement state changed - CLEAR stored positions to prevent snap-back
                currentCameraPosition = idealCameraPosition;
                currentCameraRotation = Quaternion.identity; // Will be set fresh below
                wasMovingLastFrame = isMoving;

                if (debugLogging)
                {
                    UnityEngine.Debug.LogWarning($"[CAM STATE CHANGE] Movement state changed to {(isMoving ? "MOVING" : "IDLE")} - Reset smoothing!");
                    UnityEngine.Debug.LogWarning($"[CAM STATE CHANGE] Current pitch: {currentPitch:F1}°");
                }
            }

            // Debug Y position changes
            float oldY = playerCamera.transform.position.y;
            float newY = idealCameraPosition.y;

            if (debugLogging && Mathf.Abs(newY - oldY) > 1f)
            {
                UnityEngine.Debug.LogError($"[CAM Y JUMP] Y position jump detected: {oldY:F2} -> {newY:F2} (delta: {newY - oldY:F2})");
                UnityEngine.Debug.LogError($"[CAM Y JUMP] currentCameraPosition.y: {currentCameraPosition.y:F2}");
                UnityEngine.Debug.LogError($"[CAM Y JUMP] idealCameraPosition.y: {idealCameraPosition.y:F2}");
                UnityEngine.Debug.LogError($"[CAM Y JUMP] Player moving: {isMoving}");

                if (enableSmoothing)
                {
                    UnityEngine.Debug.LogError("[CAM Y JUMP] Smoothing is ENABLED - this is likely preserving old Y!");
                }
            }

            // Apply smoothing if enabled (unless bypass is on)
            Vector3 targetCameraPosition = idealCameraPosition;

            if (bypassAllSmoothing)
            {
                // BYPASS MODE: Use fresh position every frame - no smoothing at all
                currentCameraPosition = targetCameraPosition;

                if (debugLogging && Time.frameCount % 60 == 0)
                {
                    UnityEngine.Debug.Log($"[CAM BYPASS] Using fresh position directly: {targetCameraPosition:F2}");
                }
            }
            else if (enableSmoothing && Application.isPlaying)
            {
                // FIX: Smooth X and Z separately from Y to prevent Y position caching
                // This prevents the camera from reverting to old Y values

                // Smooth horizontal position (X and Z)
                Vector3 smoothedXZ = Vector3.Lerp(
                    new Vector3(currentCameraPosition.x, 0, currentCameraPosition.z),
                    new Vector3(targetCameraPosition.x, 0, targetCameraPosition.z),
                    1f - Mathf.Exp(-15f * Time.deltaTime)
                );

                // Always use fresh Y from ideal calculation, never smooth it
                currentCameraPosition = new Vector3(
                    smoothedXZ.x,
                    targetCameraPosition.y,  // ALWAYS use fresh Y - no smoothing!
                    smoothedXZ.z
                );

                if (debugLogging && Time.frameCount % 60 == 0)
                {
                    UnityEngine.Debug.Log($"[CAM SMOOTH] Using fresh Y: {targetCameraPosition.y:F2}, Smoothed XZ: ({smoothedXZ.x:F2}, {smoothedXZ.z:F2})");
                }
            }
            else
            {
                currentCameraPosition = targetCameraPosition;
            }

            // Set camera position
            playerCamera.transform.position = currentCameraPosition;

            // Calculate look rotation with pitch
            Vector3 lookTarget = characterPos + characterUp * cameraHeight * 0.3f; // Look slightly above character center
            Vector3 lookDirection = (lookTarget - currentCameraPosition).normalized;

            // Set camera rotation - maintaining character's up vector
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection, characterUp);

            // Debug: Check if camera rotation is snapping back
            if (debugLogging && currentCameraRotation != Quaternion.identity)
            {
                float rotationChange = Quaternion.Angle(currentCameraRotation, targetRotation);
                if (rotationChange > 30f)
                {
                    UnityEngine.Debug.LogWarning($"[CAM ROTATION] Large rotation change detected: {rotationChange:F1}°");
                    UnityEngine.Debug.LogWarning($"  Current rotation euler: {currentCameraRotation.eulerAngles:F1}");
                    UnityEngine.Debug.LogWarning($"  Target rotation euler: {targetRotation.eulerAngles:F1}");
                    UnityEngine.Debug.LogWarning($"  Look direction: {lookDirection:F3}");
                    UnityEngine.Debug.LogWarning($"  Camera pos: {currentCameraPosition:F2}");
                    UnityEngine.Debug.LogWarning($"  Look target: {lookTarget:F2}");

                    if (playerController != null && playerController.IsMoving())
                    {
                        UnityEngine.Debug.LogError($"[CAM SNAP] Camera snapped {rotationChange:F1}° DURING MOVEMENT!");
                        UnityEngine.Debug.LogError($"  This is the spawn rotation bug!");
                    }
                }
            }

            if (enableSmoothing && Application.isPlaying && !bypassAllSmoothing)
            {
                currentCameraRotation = Quaternion.Slerp(
                    currentCameraRotation,
                    targetRotation,
                    1f - Mathf.Exp(-10f * Time.deltaTime)
                );
                playerCamera.transform.rotation = currentCameraRotation;
            }
            else
            {
                playerCamera.transform.rotation = targetRotation;
                currentCameraRotation = targetRotation;
            }

            if (debugLogging && Time.frameCount % 60 == 0) // Log once per second
            {
                UnityEngine.Debug.Log($"[CAMERA] Orbital Update - Pitch: {currentPitch:F1}°, Distance: {cameraDistance:F1}, Height: {cameraHeight:F1}");
            }
        }

        /// <summary>
        /// Calculate camera position using orbital mechanics
        /// </summary>
        private Vector3 CalculateOrbitalCameraPosition(Vector3 characterPos, Vector3 forward, Vector3 right, Vector3 up)
        {
            // Debug log to verify we're using the correct forward vector
            if (debugLogging)
            {
                // Double-check against actual player forward
                if (currentTarget != null)
                {
                    float angle = Vector3.Angle(forward, currentTarget.forward);
                    if (angle > 1f)
                    {
                        UnityEngine.Debug.LogError($"============================================");
                        UnityEngine.Debug.LogError($"[CALC ERROR] Forward is {angle:F1}° off from player!");
                        UnityEngine.Debug.LogError($"  Received forward param: {forward:F3}");
                        UnityEngine.Debug.LogError($"  Player's actual forward: {currentTarget.forward:F3}");
                        UnityEngine.Debug.LogError($"  This will cause camera to snap to wrong position!");
                        UnityEngine.Debug.LogError($"============================================");
                    }
                }

                if (Time.frameCount % 30 == 0)
                {
                    UnityEngine.Debug.Log($"[CAM CALC] Calculating position with forward: {forward:F3}");
                    UnityEngine.Debug.Log($"[CAM CALC] Player's actual forward: {currentTarget?.forward ?? Vector3.zero}");
                }
            }

            // Start with base offset behind character (using the CURRENT forward)
            Vector3 baseOffset = -forward * cameraDistance + up * cameraHeight;

            if (debugLogging && Time.frameCount % 60 == 0) // Log every second
            {
                UnityEngine.Debug.Log($"[ORBITAL CALC] Using forward: {forward:F3}, Distance: {cameraDistance}, Height: {cameraHeight}");
                UnityEngine.Debug.Log($"[ORBITAL CALC] Base offset before pitch: {baseOffset:F3}");
            }

            // Apply pitch rotation around character's right axis
            if (Mathf.Abs(currentPitch) > 0.01f)
            {
                Quaternion pitchRotation = Quaternion.AngleAxis(currentPitch, right);
                Vector3 offsetBeforePitch = baseOffset;
                baseOffset = pitchRotation * baseOffset;

                if (debugLogging)
                {
                    // Check movement state for pitch debugging
                    if (currentTarget != null)
                    {
                        PlayerController pc = currentTarget.GetComponent<PlayerController>();
                        bool isMoving = pc != null && pc.IsMoving();
                        if (Time.frameCount % 60 == 0) // Log every second
                        {
                            UnityEngine.Debug.Log($"[ORBITAL CALC] State: {(isMoving ? "MOVING" : "IDLE")}, Applying pitch: {currentPitch:F1}°");
                        }
                    }

                    if (Mathf.Abs(currentPitch) > 5f)
                    {
                        UnityEngine.Debug.Log($"[ORBITAL CALC] Pitch rotation applied: {currentPitch:F1}°");
                        UnityEngine.Debug.Log($"[ORBITAL CALC] Offset after pitch: {baseOffset:F3} (was: {offsetBeforePitch:F3})");
                    }
                }
            }
            else if (debugLogging && Time.frameCount % 120 == 0)
            {
                UnityEngine.Debug.Log($"[ORBITAL CALC] No pitch applied (currentPitch = {currentPitch:F2}°)");
            }

            // Return world position
            return characterPos + baseOffset;
        }

        /// <summary>
        /// Handle camera collision with environment
        /// </summary>
        private Vector3 HandleCameraCollision(Vector3 characterPos, Vector3 idealCameraPos, Vector3 characterUp)
        {
            Vector3 direction = idealCameraPos - characterPos;
            float distance = direction.magnitude;

            if (distance <= 0.01f) return idealCameraPos;

            // Raycast from character to ideal camera position
            RaycastHit hit;
            Vector3 rayStart = characterPos + characterUp * 0.5f; // Start slightly above character center

            if (Physics.Raycast(rayStart, direction.normalized, out hit, distance, collisionMask))
            {
                // Camera would collide, pull it closer
                float adjustedDistance = hit.distance - collisionOffset;
                adjustedDistance = Mathf.Max(adjustedDistance, 1f); // Minimum distance

                if (debugLogging)
                {
                    UnityEngine.Debug.Log($"[CAMERA] Collision detected at {hit.distance:F1}m, adjusting to {adjustedDistance:F1}m");
                }

                return rayStart + direction.normalized * adjustedDistance;
            }

            return idealCameraPos;
        }


        /// <summary>
        /// Immediately snap camera to correct position and orientation for current target
        /// </summary>
        private void SnapCameraToTarget()
        {
            if (currentTarget == null || playerCamera == null || playerCamera.transform == null) return;

            if (useOrbitalCamera)
            {
                // ALWAYS get fresh values from the current target
                Vector3 characterPos = currentTarget.position;
                Vector3 characterUp = currentTarget.up;
                Vector3 characterForward = currentTarget.forward; // Current, not cached!
                Vector3 characterRight = Vector3.Cross(characterUp, characterForward).normalized;
                // DON'T recalculate forward - use the player's actual forward direction

                // Calculate ideal camera position using CURRENT forward
                Vector3 idealPos = CalculateOrbitalCameraPosition(characterPos, characterForward, characterRight, characterUp);

                // Set position without smoothing
                currentCameraPosition = idealPos;
                playerCamera.transform.position = idealPos;

                // Set rotation to look at character
                Vector3 lookTarget = characterPos + characterUp * cameraHeight * 0.3f;
                Vector3 lookDirection = (lookTarget - idealPos).normalized;
                Quaternion targetRot = Quaternion.LookRotation(lookDirection, characterUp);

                currentCameraRotation = targetRot;
                playerCamera.transform.rotation = targetRot;

                Log("Camera snapped to orbital position");
            }
            else
            {
                // Legacy snap for manual following
                Vector3 characterPos = currentTarget.position;
                Vector3 characterUp = currentTarget.up;
                Vector3 characterForward = currentTarget.forward;
                Vector3 characterRight = Vector3.Cross(characterUp, characterForward).normalized;
                // Don't recalculate characterForward - use the player's actual forward direction

                // Position camera behind and above character using default values
                Vector3 localOffset = characterRight * 0f +
                                      characterUp * 2f +
                                      characterForward * -5f;

                playerCamera.transform.position = characterPos + localOffset;

                // CRITICAL: Set camera's up vector to match character's surface normal
                playerCamera.transform.up = characterUp;

                // Look at the character with the correct up vector
                Vector3 lookTarget = characterPos + characterUp * 0.5f;
                Vector3 lookDirection = lookTarget - playerCamera.transform.position;
                if (lookDirection.magnitude > 0.01f)
                {
                    playerCamera.transform.rotation = Quaternion.LookRotation(lookDirection, characterUp);
                }

                Log("Camera snapped to follow position behind character");
            }
        }
        
        
        
        #region Logging
        
        private void Log(string message)
        {
            if (debugLogging)
                UnityEngine.Debug.Log($"[CameraManager] {message}");
        }
        
        private void LogWarning(string message)
        {
            if (debugLogging)
                UnityEngine.Debug.LogWarning($"[CameraManager] {message}");
        }
        
        private void LogError(string message)
        {
            UnityEngine.Debug.LogError($"[CameraManager] {message}");
        }

        #endregion

        #region Debug Helpers

        /// <summary>
        /// Coroutine to check if pitch value is being overwritten in the next frame
        /// </summary>
        private System.Collections.IEnumerator CheckPitchNextFrame(float expectedPitch)
        {
            yield return null; // Wait one frame

            if (Mathf.Abs(currentPitch - expectedPitch) > 0.1f)
            {
                UnityEngine.Debug.LogError($"[PITCH OVERWRITE] Pitch was set to {expectedPitch:F2}° but is now {currentPitch:F2}°!");
                UnityEngine.Debug.LogError("[PITCH OVERWRITE] Something is restoring old pitch value!");
                UnityEngine.Debug.LogError($"[PITCH OVERWRITE] Current smoothing values:");
                UnityEngine.Debug.LogError($"  - currentCameraPosition: {currentCameraPosition:F2}");
                UnityEngine.Debug.LogError($"  - currentCameraRotation: {currentCameraRotation.eulerAngles:F1}");
                UnityEngine.Debug.LogError($"  - wasMovingLastFrame: {wasMovingLastFrame}");
            }
        }

        #endregion

        #region Debug GUI
        
        void OnGUI()
        {
            // Only show minimal debug info when explicitly enabled
            if (!debugLogging) return;

            int y = 10;
            GUI.Label(new Rect(10, y, 300, 20), $"Camera Target: {(currentTarget != null ? currentTarget.name : "None")}");
            y += 20;
            GUI.Label(new Rect(10, y, 300, 20), $"Mode: {(useOrbitalCamera ? "Orbital Third-Person" : "Cinemachine")}");
            y += 20;
            if (useOrbitalCamera)
            {
                GUI.Label(new Rect(10, y, 300, 20), $"Camera Pitch: {currentPitch:F1}° (Range: {minPitch}° to {maxPitch}°)");
                y += 20;
                GUI.Label(new Rect(10, y, 300, 20), $"Distance: {cameraDistance:F1}m, Height: {cameraHeight:F1}m");
                y += 20;
                GUI.Label(new Rect(10, y, 300, 20), $"Smoothing: {(enableSmoothing ? "On" : "Off")}, Collision: {(enableCollisionDetection ? "On" : "Off")}");
            }
        }
        
        #endregion
    }
}