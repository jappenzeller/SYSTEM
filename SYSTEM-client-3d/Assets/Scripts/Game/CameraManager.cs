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
        
        [Header("Spherical World Camera Settings")]
        [SerializeField] private bool manualCameraControl = true;
        [SerializeField] private Vector3 cameraOffset = new Vector3(0, 10, -20);
        [SerializeField] private float cameraSmoothing = 5f;
        [SerializeField] private float rotationSmoothing = 5f;
        
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
            // Manual camera control for spherical world
            if (manualCameraControl && currentTarget != null)
            {
                UpdateCameraOrientation();
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
                }
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
            
            if (manualCameraControl)
            {
                // For manual control, disable Cinemachine's automatic following
                // We'll handle position and rotation in LateUpdate
                playerCamera.Follow = null;
                playerCamera.LookAt = null;
                
                Log("Camera configured for manual spherical world control");
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
                return;
            }
            
            currentTarget = playerTransform;
            
            if (playerTransform == null)
            {
                if (!manualCameraControl)
                {
                    playerCamera.Follow = null;
                    playerCamera.LookAt = null;
                }
                Log("Camera target cleared");
                return;
            }
            
            if (manualCameraControl)
            {
                // In manual control mode, we don't use Cinemachine's Follow/LookAt
                // Position will be updated in LateUpdate
                Log($"Camera target set for manual control: {playerTransform.name}");
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
            
            // If manual control, immediately position camera correctly
            if (manualCameraControl)
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
        /// Immediately snap camera to correct position and orientation for current target
        /// </summary>
        private void SnapCameraToTarget()
        {
            if (currentTarget == null || playerCamera == null || playerCamera.transform == null) return;
            
            // Get player's surface normal
            Vector3 playerUp = currentTarget.up;
            
            // Calculate and set camera position immediately
            Vector3 cameraPos = CalculateCameraPosition(currentTarget.position, playerUp);
            playerCamera.transform.position = cameraPos;
            
            // CRITICAL: Set camera's up vector to match player's surface normal
            playerCamera.transform.up = playerUp;
            
            // Look at the player with the correct up vector
            Vector3 lookDirection = currentTarget.position - cameraPos;
            if (lookDirection.magnitude > 0.01f)
            {
                playerCamera.transform.rotation = Quaternion.LookRotation(lookDirection, playerUp);
            }
            
            Log("Camera snapped to target position and orientation");
        }
        
        /// <summary>
        /// Update camera position and rotation for spherical world
        /// Called in LateUpdate for smooth camera following
        /// </summary>
        private void UpdateCameraOrientation()
        {
            if (currentTarget == null || playerCamera == null) return;
            
            // Get player's surface normal (up vector on sphere)
            Vector3 playerUp = currentTarget.up;
            
            // Calculate camera position relative to player's surface normal
            Vector3 targetCameraPos = CalculateCameraPosition(currentTarget.position, playerUp);
            
            // Smooth camera position
            if (playerCamera.transform != null)
            {
                playerCamera.transform.position = Vector3.Lerp(
                    playerCamera.transform.position,
                    targetCameraPos,
                    Time.deltaTime * cameraSmoothing
                );
                
                // CRITICAL: Set camera's up vector to match player's surface normal
                // This ensures camera "rolls" with the sphere surface
                playerCamera.transform.up = Vector3.Slerp(
                    playerCamera.transform.up,
                    playerUp,
                    Time.deltaTime * rotationSmoothing
                );
                
                // Calculate look direction
                Vector3 lookDirection = currentTarget.position - playerCamera.transform.position;
                
                // Create rotation that looks at player with correct up vector
                if (lookDirection.magnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(lookDirection, playerUp);
                    
                    // Smooth camera rotation
                    playerCamera.transform.rotation = Quaternion.Slerp(
                        playerCamera.transform.rotation,
                        targetRotation,
                        Time.deltaTime * rotationSmoothing
                    );
                }
            }
        }
        
        /// <summary>
        /// Calculate camera position for spherical world based on player position and surface normal
        /// </summary>
        private Vector3 CalculateCameraPosition(Vector3 playerPos, Vector3 playerUp)
        {
            // Get player's local coordinate system on sphere
            Vector3 up = playerUp.normalized;  // Surface normal
            Vector3 forward = currentTarget.forward;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            
            // Recalculate forward to ensure orthogonal basis
            if (right.magnitude < 0.01f)
            {
                // Handle edge case where forward and up are parallel
                right = Vector3.Cross(up, Vector3.up).normalized;
                if (right.magnitude < 0.01f)
                {
                    right = Vector3.Cross(up, Vector3.forward).normalized;
                }
            }
            forward = Vector3.Cross(right, up).normalized;
            
            // Transform camera offset from player's local space to world space
            Vector3 worldOffset = 
                right * cameraOffset.x + 
                up * cameraOffset.y + 
                forward * cameraOffset.z;
            
            return playerPos + worldOffset;
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
            if (!debugLogging) return;
            
            int y = 10;
            GUI.Label(new Rect(10, y, 500, 20), "=== Camera Manager ===");
            y += 20;
            GUI.Label(new Rect(10, y, 500, 20), $"Initialized: {isInitialized}");
            y += 20;
            GUI.Label(new Rect(10, y, 500, 20), $"Current Target: {(currentTarget != null ? currentTarget.name : "None")}");
            y += 20;
            GUI.Label(new Rect(10, y, 500, 20), $"Camera Active: {(playerCamera != null ? "Yes" : "No")}");
            y += 20;
            GUI.Label(new Rect(10, y, 500, 20), $"Manual Control: {manualCameraControl}");
            y += 20;
            
            if (playerCamera != null && playerCamera.transform != null && currentTarget != null)
            {
                Vector3 camUp = playerCamera.transform.up;
                Vector3 playerUp = currentTarget.up;
                float upAlignment = Vector3.Dot(camUp, playerUp);
                
                GUI.Label(new Rect(10, y, 500, 20), $"Camera Up: {camUp:F2}");
                y += 20;
                GUI.Label(new Rect(10, y, 500, 20), $"Player Up: {playerUp:F2}");
                y += 20;
                GUI.Label(new Rect(10, y, 500, 20), $"Up Vector Alignment: {upAlignment:F3} (should be ~1.0)");
                y += 20;
            }
            
            GUI.Label(new Rect(10, y, 500, 20), $"PlayerTracker: {(playerTracker != null ? "Connected" : "Not Found")}");
        }
        
        #endregion
    }
}