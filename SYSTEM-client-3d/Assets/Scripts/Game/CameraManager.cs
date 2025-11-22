using System.Collections;
using UnityEngine;
using SYSTEM.Game;

namespace SYSTEM.Game
{
    /// <summary>
    /// Simplified camera management system with NO SMOOTHING.
    /// Camera directly follows the player position for immediate response.
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        [Header("Camera Configuration")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private bool autoFindCamera = true;

        [Header("Orbital Camera Settings")]
        [SerializeField] private bool useOrbitalCamera = true;
        [SerializeField] private float cameraDistance = 7f; // Distance behind character
        [SerializeField] private float cameraHeight = 2.5f; // Height above character center
        [SerializeField] private float minPitch = -60f; // Minimum pitch angle (looking down)
        [SerializeField] private float maxPitch = 85f; // Maximum pitch angle (looking up)

        [Header("Collision Detection")]
        [SerializeField] private float collisionOffset = 0.2f; // Offset from collision point
        [SerializeField] private LayerMask collisionMask = -1; // Layers to check for collision
        [SerializeField] private bool enableCollisionDetection = true;

        [Header("Debug")]
        [SerializeField] private bool debugLogging = false; // Disabled by default for clean console

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
                        // UnityEngine.Debug.LogError("[CameraManager] No CameraManager instance found in scene!");
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
        private float currentPitch = 0f; // Current pitch angle for vertical look
        private bool cameraUpdateEnabled = true; // Can be disabled by external systems (like CursorController)

        void Awake()
        {
            // Singleton pattern enforcement
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            Initialize();
        }

        void LateUpdate()
        {
            if (currentTarget != null && playerCamera != null && useOrbitalCamera && cameraUpdateEnabled)
            {
                UpdateCameraFollowCharacter();
            }
        }

        void OnEnable()
        {
            SubscribeToPlayerEvents();
        }

        void OnDisable()
        {
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
        /// Initialize the camera system
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            // Find or create the Camera
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
                if (playerCamera == null)
                {
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
                    }
                }
            }

            ConfigureCamera();

            // Find PlayerTracker
            playerTracker = FindFirstObjectByType<PlayerTracker>();
            if (playerTracker == null)
            {
                StartCoroutine(WaitForPlayerTracker());
            }
            else
            {
                SubscribeToPlayerEvents();
            }

            isInitialized = true;
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
        }

        /// <summary>
        /// Wait for PlayerTracker to become available
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
                    SubscribeToPlayerEvents();
                    break;
                }
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
                if (playerTracker == null) return;
            }

            // Unsubscribe first to prevent duplicate subscriptions
            UnsubscribeFromPlayerEvents();

            // Subscribe to local player change event
            playerTracker.OnLocalPlayerChanged += OnLocalPlayerChanged;

            // Check if there's already a local player
            var localPlayer = playerTracker.GetLocalPlayer();
            if (localPlayer != null)
            {
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
            }
        }

        /// <summary>
        /// Handle local player change event from PlayerTracker
        /// </summary>
        private void OnLocalPlayerChanged(PlayerTracker.PlayerData playerData)
        {
            if (playerData == null)
            {
                SetFollowTarget(null);
                return;
            }

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
                    SetFollowTarget(controller.transform);
                    yield break;
                }
            }

            // If not found immediately, wait and retry
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
                        SetFollowTarget(controller.transform);
                        yield break;
                    }
                }
            }
        }

        /// <summary>
        /// Set the camera to follow a specific transform target
        /// </summary>
        public void SetFollowTarget(Transform playerTransform)
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
                if (playerCamera == null) return;
            }

            currentTarget = playerTransform;

            if (playerTransform == null) return;

            if (useOrbitalCamera)
            {
                // Initialize pitch if this is a new target
                if (currentTarget == null)
                {
                    currentPitch = 0f;
                }

                // Immediately position camera behind player
                Vector3 characterPos = playerTransform.position;
                Vector3 characterUp = playerTransform.up;
                Vector3 characterForward = playerTransform.forward;
                Vector3 characterRight = Vector3.Cross(characterUp, characterForward).normalized;

                Vector3 idealPos = CalculateOrbitalCameraPosition(
                    characterPos,
                    characterForward,
                    characterRight,
                    characterUp
                );

                playerCamera.transform.position = idealPos;

                // Look at player
                Vector3 lookTarget = characterPos + characterUp * cameraHeight * 0.3f;
                Vector3 lookDirection = (lookTarget - idealPos).normalized;
                playerCamera.transform.rotation = Quaternion.LookRotation(lookDirection, characterUp);
            }
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
            }
        }

        /// <summary>
        /// Set camera pitch angle for vertical look
        /// </summary>
        public void SetCameraPitch(float pitch)
        {
            // Clamp the pitch to valid range
            currentPitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        /// <summary>
        /// Update camera - NO SMOOTHING, direct following
        /// </summary>
        private void UpdateCameraFollowCharacter()
        {
            if (currentTarget == null || playerCamera == null) return;

            // Get current player values
            Vector3 characterPos = currentTarget.position;
            Vector3 characterUp = currentTarget.up;
            Vector3 characterForward = currentTarget.forward;
            Vector3 characterRight = Vector3.Cross(characterUp, characterForward).normalized;

            // Calculate ideal camera position
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

            // DIRECTLY SET CAMERA POSITION - NO SMOOTHING
            playerCamera.transform.position = idealCameraPosition;

            // Calculate look rotation
            Vector3 lookTarget = characterPos + characterUp * cameraHeight * 0.3f;
            Vector3 lookDirection = (lookTarget - idealCameraPosition).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection, characterUp);

            // DIRECTLY SET CAMERA ROTATION - NO SMOOTHING
            playerCamera.transform.rotation = targetRotation;
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
            Vector3 rayStart = characterPos + characterUp * 0.5f;

            if (Physics.Raycast(rayStart, direction.normalized, out hit, distance, collisionMask))
            {
                // Camera would collide, pull it closer
                float adjustedDistance = hit.distance - collisionOffset;
                adjustedDistance = Mathf.Max(adjustedDistance, 1f);

                return rayStart + direction.normalized * adjustedDistance;
            }

            return idealCameraPos;
        }

        #region Debug GUI

        void OnGUI()
        {
            if (!debugLogging) return;

            int y = 10;
            GUI.Label(new Rect(10, y, 300, 20), $"Camera Target: {(currentTarget != null ? currentTarget.name : "None")}");
            y += 20;
            GUI.Label(new Rect(10, y, 300, 20), $"Camera Pitch: {currentPitch:F1}°");
            y += 20;
            GUI.Label(new Rect(10, y, 300, 20), $"Distance: {cameraDistance:F1}m, Height: {cameraHeight:F1}m");
        }

        #endregion
    }
}
