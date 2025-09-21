using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using SYSTEM.Game;

namespace SYSTEM.Game
{
    /// <summary>
    /// Centralized camera management system that owns and controls the single active camera.
    /// Subscribes to PlayerTracker events and switches camera targets when local player changes.
    /// Replaces per-player camera ownership to prevent camera switching issues in multiplayer.
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        [Header("Camera Configuration")]
        [SerializeField] private CinemachineCamera playerCamera;
        [Tooltip("Configure camera behavior through Cinemachine components in the Inspector")]
        [SerializeField] private bool autoFindCamera = true;
        
        [Header("Camera Following Settings")]
        [SerializeField] private bool rigidFollowing = true;
        [SerializeField] private Vector3 cameraOffset = new Vector3(0, 2, -5); // Behind and above character
        [SerializeField] private float cameraDistance = 5f;
        [SerializeField] private float cameraHeight = 2f;
        
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

        // Camera pitch for vertical look
        private float cameraPitch = 0f;
        
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
            if (currentTarget != null && playerCamera != null && rigidFollowing)
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
            
            // Find or create the CinemachineCamera
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<CinemachineCamera>();
                if (playerCamera == null)
                {
                    // Try to find in scene
                    playerCamera = FindFirstObjectByType<CinemachineCamera>();
                    if (playerCamera == null)
                    {
                        LogError("No CinemachineCamera found! Please add a CinemachineCamera to the scene.");
                        return;
                    }
                    else
                    {
                        Log($"Found CinemachineCamera: {playerCamera.name}");
                    }
                }
                else
                {
                    Log($"Found CinemachineCamera in children: {playerCamera.name}");
                }
            }
            else
            {
                Log($"CinemachineCamera already assigned: {playerCamera.name}");
            }
            
            // Configure camera for spherical world following
            ConfigureCamera();
            
            // Ensure Main Camera has CinemachineBrain
            if (Camera.main != null && Camera.main.GetComponent<CinemachineBrain>() == null)
            {
                Camera.main.gameObject.AddComponent<CinemachineBrain>();
                Log("Added CinemachineBrain to Main Camera");
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
        /// Configure the Cinemachine camera for spherical world following
        /// </summary>
        private void ConfigureCamera()
        {
            if (playerCamera == null) return;

            if (rigidFollowing)
            {
                // For rigid following, disable Cinemachine's automatic following
                // We'll handle position and rotation in LateUpdate
                playerCamera.Follow = null;
                playerCamera.LookAt = null;

                Log("Camera configured for rigid character following (Minecraft style)");
            }
            else
            {
                // Use Cinemachine's built-in following (configured in Inspector)
                // Initially set to no target
                playerCamera.Follow = null;
                playerCamera.LookAt = null;

                Log("Camera configured - Cinemachine components should be set in Unity Inspector");
            }

            // Set priority to ensure this camera is active
            playerCamera.Priority = 10;
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
                playerCamera = FindFirstObjectByType<CinemachineCamera>();
                if (playerCamera == null)
                {
                    LogError("Still no CinemachineCamera found!");
                    return;
                }
            }
            
            currentTarget = playerTransform;

            if (playerTransform == null)
            {
                if (!rigidFollowing)
                {
                    playerCamera.Follow = null;
                    playerCamera.LookAt = null;
                }
                Log("Camera target cleared");
                return;
            }

            if (rigidFollowing)
            {
                // In rigid following mode, we don't use Cinemachine's Follow/LookAt
                // Position will be updated in LateUpdate
                cameraPitch = 0f; // Reset pitch when targeting new player
                Log($"Camera target set for rigid following: {playerTransform.name}");
            }
            else
            {
                // Use Cinemachine's automatic following
                playerCamera.Follow = playerTransform;
                playerCamera.LookAt = playerTransform;
                Log($"Camera following with Cinemachine: {playerTransform.name}");
            }
            
            // For spherical worlds, adjust camera to respect player's up vector
            AdjustCameraForSphericalWorld(playerTransform);

            // If rigid following, immediately position camera correctly
            if (rigidFollowing)
            {
                SnapCameraToTarget();
            }
            
            Log($"Camera now following: {playerTransform.name}");
        }
        
        /// <summary>
        /// Adjust camera settings for spherical world geometry
        /// </summary>
        private void AdjustCameraForSphericalWorld(Transform playerTransform)
        {
            if (playerCamera == null || playerTransform == null) return;
            
            // The camera behavior should handle spherical following through its configuration
            // This is typically done through Cinemachine's Third Person Follow or custom extensions
            // configured in the Unity Inspector for maximum flexibility
            
            Log("Camera target set for spherical world - ensure Cinemachine components are configured in Inspector");
        }
        
        /// <summary>
        /// Get the currently active camera
        /// </summary>
        public CinemachineCamera GetActiveCamera()
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
            UnityEngine.Debug.Log($"[CAMERA] SetCameraPitch received: {pitch:F2}°");
            UnityEngine.Debug.Log($"[CAMERA] Previous cameraPitch: {cameraPitch:F2}°");
            cameraPitch = pitch;
            UnityEngine.Debug.Log($"[CAMERA] New cameraPitch: {cameraPitch:F2}°");

            // Force immediate camera update
            if (currentTarget != null && playerCamera != null)
            {
                UnityEngine.Debug.Log($"[CAMERA] Triggering immediate camera update for target: {currentTarget.name}");
                UpdateCameraFollowCharacter();
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[CAMERA] Cannot update - target:{currentTarget?.name} camera:{playerCamera?.name}");
            }
        }

        /// <summary>
        /// Update camera to rigidly follow character with pitch control
        /// </summary>
        private void UpdateCameraFollowCharacter()
        {
            UnityEngine.Debug.Log($"[CAMERA] === UpdateCameraFollowCharacter START ===");
            UnityEngine.Debug.Log($"[CAMERA] Current target: {currentTarget?.name}");
            UnityEngine.Debug.Log($"[CAMERA] Camera pitch: {cameraPitch:F2}°");
            UnityEngine.Debug.Log($"[CAMERA] Camera offset: {cameraOffset}");

            if (currentTarget == null || playerCamera == null)
            {
                UnityEngine.Debug.LogWarning($"[CAMERA] Aborting - target:{currentTarget?.name} camera:{playerCamera?.name}");
                return;
            }

            // Get character's position and orientation
            Vector3 characterPos = currentTarget.position;
            Vector3 characterUp = currentTarget.up; // Sphere surface normal
            Vector3 characterForward = currentTarget.forward;
            Vector3 characterRight = Vector3.Cross(characterUp, characterForward).normalized;

            UnityEngine.Debug.Log($"[CAMERA] Character pos: {characterPos}");
            UnityEngine.Debug.Log($"[CAMERA] Character up: {characterUp}");
            UnityEngine.Debug.Log($"[CAMERA] Character forward: {characterForward}");
            UnityEngine.Debug.Log($"[CAMERA] Character right: {characterRight}");

            // Recalculate forward for orthogonal basis
            characterForward = Vector3.Cross(characterRight, characterUp).normalized;

            // Calculate base camera offset in character's local coordinate system
            Vector3 localOffset = characterRight * cameraOffset.x +
                                  characterUp * cameraOffset.y +
                                  characterForward * cameraOffset.z;

            // Apply vertical pitch rotation to offset
            if (Mathf.Abs(cameraPitch) > 0.01f)
            {
                Quaternion pitchRotation = Quaternion.AngleAxis(cameraPitch, characterRight);
                localOffset = pitchRotation * localOffset;
            }

            // Set camera position
            playerCamera.transform.position = characterPos + localOffset;

            // Calculate look direction with pitch applied
            Vector3 baseLookDirection = characterForward;
            Quaternion pitchRotation2 = Quaternion.AngleAxis(-cameraPitch, characterRight);
            Vector3 lookDirection = pitchRotation2 * baseLookDirection;

            // Set camera rotation
            playerCamera.transform.rotation = Quaternion.LookRotation(lookDirection, characterUp);

            UnityEngine.Debug.Log($"[CAMERA] Final camera position: {playerCamera.transform.position}");
            UnityEngine.Debug.Log($"[CAMERA] Final camera rotation: {playerCamera.transform.rotation.eulerAngles}");
            UnityEngine.Debug.Log($"[CAMERA] === UpdateCameraFollowCharacter END ===");
        }


        /// <summary>
        /// Immediately snap camera to correct position and orientation for current target
        /// </summary>
        private void SnapCameraToTarget()
        {
            if (currentTarget == null || playerCamera == null || playerCamera.transform == null) return;

            if (rigidFollowing)
            {
                // For rigid following, just trigger an immediate update
                UpdateCameraFollowCharacter();
                Log("Camera snapped to follow position");
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

                // Position camera behind and above character
                Vector3 localOffset = characterRight * cameraOffset.x +
                                      characterUp * cameraOffset.y +
                                      characterForward * cameraOffset.z;

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
            GUI.Label(new Rect(10, y, 300, 20), $"Mode: {(rigidFollowing ? "Rigid Following" : "Cinemachine")}");
            y += 20;
            if (rigidFollowing)
            {
                GUI.Label(new Rect(10, y, 300, 20), $"Camera Pitch: {cameraPitch:F1}°");
            }
        }
        
        #endregion
    }
}