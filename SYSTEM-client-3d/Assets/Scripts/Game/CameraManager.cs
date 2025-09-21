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
        [SerializeField] private bool enableSmoothing = true;

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
        private float currentPitch = 0f; // Current pitch angle
        private Vector3 currentCameraPosition; // Current camera position for smoothing
        private Vector3 cameraVelocity; // For smooth damp
        private Quaternion currentCameraRotation; // Current camera rotation for smoothing
        
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
                currentPitch = 0f; // Reset pitch when targeting new player

                // Initialize camera position for smooth start
                if (playerTransform != null)
                {
                    Vector3 characterPos = playerTransform.position;
                    Vector3 characterUp = playerTransform.up;
                    Vector3 characterForward = playerTransform.forward;
                    Vector3 characterRight = Vector3.Cross(characterUp, characterForward).normalized;
                    characterForward = Vector3.Cross(characterRight, characterUp).normalized;

                    // Set initial camera position behind character
                    currentCameraPosition = characterPos - characterForward * cameraDistance + characterUp * cameraHeight;
                    currentCameraRotation = Quaternion.LookRotation(characterForward, characterUp);

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
            // Clamp the pitch to valid range
            currentPitch = Mathf.Clamp(pitch, minPitch, maxPitch);

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

            // Get character's position and local coordinate system
            Vector3 characterPos = currentTarget.position;
            Vector3 characterUp = currentTarget.up; // Sphere surface normal (radial from center)
            Vector3 characterForward = currentTarget.forward;
            Vector3 characterRight = Vector3.Cross(characterUp, characterForward).normalized;

            // Ensure orthogonal basis
            characterForward = Vector3.Cross(characterRight, characterUp).normalized;

            // Calculate ideal camera position using spherical coordinates
            Vector3 idealCameraPosition = CalculateOrbitalCameraPosition(
                characterPos,
                characterForward,
                characterRight,
                characterUp
            );

            // Apply collision detection if enabled
            if (enableCollisionDetection)
            {
                idealCameraPosition = HandleCameraCollision(characterPos, idealCameraPosition, characterUp);
            }

            // Apply smoothing if enabled
            Vector3 targetCameraPosition = idealCameraPosition;
            if (enableSmoothing && Application.isPlaying)
            {
                // Fast, responsive smoothing for Minecraft-style camera
                currentCameraPosition = Vector3.Lerp(
                    currentCameraPosition,
                    targetCameraPosition,
                    1f - Mathf.Exp(-15f * Time.deltaTime)  // Very responsive exponential smoothing
                );
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

            if (enableSmoothing && Application.isPlaying)
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
            // Start with base offset behind character
            Vector3 baseOffset = -forward * cameraDistance + up * cameraHeight;

            // Apply pitch rotation around character's right axis
            if (Mathf.Abs(currentPitch) > 0.01f)
            {
                Quaternion pitchRotation = Quaternion.AngleAxis(currentPitch, right);
                baseOffset = pitchRotation * baseOffset;
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
                // For orbital camera, calculate and set position immediately
                Vector3 characterPos = currentTarget.position;
                Vector3 characterUp = currentTarget.up;
                Vector3 characterForward = currentTarget.forward;
                Vector3 characterRight = Vector3.Cross(characterUp, characterForward).normalized;
                characterForward = Vector3.Cross(characterRight, characterUp).normalized;

                // Calculate ideal camera position
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

                // Recalculate forward for orthogonal basis
                characterForward = Vector3.Cross(characterRight, characterUp).normalized;

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