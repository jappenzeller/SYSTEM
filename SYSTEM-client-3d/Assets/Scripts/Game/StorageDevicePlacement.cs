using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SpacetimeDB.Types;

namespace SYSTEM.Game
{
    /// <summary>
    /// Handles placement of storage devices in the world.
    /// Player presses Interact key (E) to place a device in front of them.
    /// Validates proximity to prevent overlapping placements.
    /// </summary>
    public class StorageDevicePlacement : MonoBehaviour
    {
        [Header("Placement Settings")]
        [SerializeField] private float placementDistance = 5f; // Units in front of player
        [SerializeField] private float proximityCheckRadius = 1f; // Minimum distance between objects
        [SerializeField] private string defaultDeviceName = "Storage Device";
        [SerializeField] private bool showDebugInfo = true;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject placementPreviewPrefab; // Optional preview indicator
        [SerializeField] private bool showPlacementPreview = false;

        // Input system
        private PlayerInputActions playerInputActions;
        private PlayerController localPlayerController;
        private bool isLocalPlayer = false;
        private GameObject worldSphere;
        private float sphereRadius = 300f;

        // Placement preview
        private GameObject previewObject;
        private int deviceCounter = 1; // For auto-naming devices

        void Awake()
        {
            SystemDebug.Log(SystemDebug.Category.StorageSystem, "[StorageDevicePlacement] Component initialized");
        }

        void Start()
        {
            // Find the world sphere for surface positioning
            FindWorldSphere();

            // Find local player controller
            FindLocalPlayer();

            // Setup input only if this is the local player
            if (isLocalPlayer)
            {
                SetupInput();
            }
        }

        void OnEnable()
        {
            if (playerInputActions != null && isLocalPlayer)
            {
                playerInputActions.Enable();
            }
        }

        void OnDisable()
        {
            if (playerInputActions != null)
            {
                playerInputActions.Disable();
            }

            // Clean up preview
            if (previewObject != null)
            {
                Destroy(previewObject);
            }
        }

        void OnDestroy()
        {
            if (playerInputActions != null)
            {
                playerInputActions.Gameplay.PlaceDevice.performed -= OnPlaceDevicePressed;
                playerInputActions.Dispose();
            }
        }

        void Update()
        {
            if (showPlacementPreview && isLocalPlayer && localPlayerController != null)
            {
                UpdatePlacementPreview();
            }
        }

        #region Initialization

        void FindWorldSphere()
        {
            worldSphere = GameObject.Find("CenterWorld");
            if (worldSphere != null)
            {
                SphereCollider sphereCollider = worldSphere.GetComponent<SphereCollider>();
                if (sphereCollider != null)
                {
                    sphereRadius = sphereCollider.radius * worldSphere.transform.lossyScale.x;
                }
                else
                {
                    sphereRadius = 300f; // Default WORLD_RADIUS
                }

                SystemDebug.Log(SystemDebug.Category.StorageSystem,
                    $"[StorageDevicePlacement] Found world sphere with radius {sphereRadius}");
            }
            else
            {
                SystemDebug.LogWarning(SystemDebug.Category.StorageSystem,
                    "[StorageDevicePlacement] Could not find CenterWorld object, using default radius");
                sphereRadius = 300f;
            }
        }

        void FindLocalPlayer()
        {
            // Check if this component is on the local player
            localPlayerController = GetComponent<PlayerController>();
            if (localPlayerController != null)
            {
                isLocalPlayer = localPlayerController.IsLocalPlayer();

                if (isLocalPlayer)
                {
                    SystemDebug.Log(SystemDebug.Category.StorageSystem,
                        "[StorageDevicePlacement] Attached to local player - placement enabled");
                }
                else
                {
                    SystemDebug.Log(SystemDebug.Category.StorageSystem,
                        "[StorageDevicePlacement] Attached to remote player - placement disabled");
                }
            }
            else
            {
                SystemDebug.LogWarning(SystemDebug.Category.StorageSystem,
                    "[StorageDevicePlacement] No PlayerController found on this GameObject");
            }
        }

        void SetupInput()
        {
            try
            {
                SystemDebug.Log(SystemDebug.Category.StorageSystem,
                    "[StorageDevicePlacement] Creating PlayerInputActions...");

                playerInputActions = new PlayerInputActions();

                SystemDebug.Log(SystemDebug.Category.StorageSystem,
                    "[StorageDevicePlacement] Accessing Gameplay.PlaceDevice action...");

                // Try to get the PlaceDevice action
                var placeDeviceAction = playerInputActions.Gameplay.PlaceDevice;

                if (placeDeviceAction == null)
                {
                    SystemDebug.LogError(SystemDebug.Category.StorageSystem,
                        "[StorageDevicePlacement] ❌ PlaceDevice action is NULL!");
                    SystemDebug.LogError(SystemDebug.Category.StorageSystem,
                        "[StorageDevicePlacement] → PlayerInputActions.cs needs to be regenerated");
                    SystemDebug.LogError(SystemDebug.Category.StorageSystem,
                        "[StorageDevicePlacement] → In Unity: Right-click PlayerInputActions.inputactions → 'Generate C# Class'");
                    return;
                }

                SystemDebug.Log(SystemDebug.Category.StorageSystem,
                    "[StorageDevicePlacement] ✅ PlaceDevice action found, subscribing to performed event...");

                placeDeviceAction.performed += OnPlaceDevicePressed;

                // CRITICAL: Enable the input actions NOW (can't rely on OnEnable because isLocalPlayer isn't set yet)
                playerInputActions.Enable();
                SystemDebug.Log(SystemDebug.Category.StorageSystem,
                    "[StorageDevicePlacement] ✅ Input actions ENABLED");

                SystemDebug.Log(SystemDebug.Category.StorageSystem,
                    "[StorageDevicePlacement] ✅ Input system configured - press R to place device");
            }
            catch (System.Exception ex)
            {
                SystemDebug.LogError(SystemDebug.Category.StorageSystem,
                    $"[StorageDevicePlacement] ❌ EXCEPTION in SetupInput: {ex.Message}");
                SystemDebug.LogError(SystemDebug.Category.StorageSystem,
                    $"[StorageDevicePlacement] Exception type: {ex.GetType().Name}");
                SystemDebug.LogError(SystemDebug.Category.StorageSystem,
                    "[StorageDevicePlacement] → This likely means PlaceDevice action doesn't exist in PlayerInputActions.cs");
                SystemDebug.LogError(SystemDebug.Category.StorageSystem,
                    "[StorageDevicePlacement] → Regenerate: Right-click PlayerInputActions.inputactions → 'Generate C# Class'");
            }
        }

        #endregion

        #region Input Handling

        void OnPlaceDevicePressed(InputAction.CallbackContext context)
        {
            SystemDebug.Log(SystemDebug.Category.StorageSystem,
                "[StorageDevicePlacement] ========== R KEY PRESSED ==========");

            if (!isLocalPlayer)
            {
                SystemDebug.LogWarning(SystemDebug.Category.StorageSystem,
                    "[StorageDevicePlacement] Not local player - ignoring input");
                return;
            }

            if (localPlayerController == null)
            {
                SystemDebug.LogError(SystemDebug.Category.StorageSystem,
                    "[StorageDevicePlacement] PlayerController is null!");
                return;
            }

            if (!GameManager.IsConnected())
            {
                SystemDebug.LogWarning(SystemDebug.Category.StorageSystem,
                    "[StorageDevicePlacement] Cannot place device - not connected to server");
                return;
            }

            SystemDebug.Log(SystemDebug.Category.StorageSystem,
                $"[StorageDevicePlacement] Player position: {transform.position}, Forward: {transform.forward}");

            // Calculate placement position
            Vector3 placementPos = CalculatePlacementPosition();

            SystemDebug.Log(SystemDebug.Category.StorageSystem,
                $"[StorageDevicePlacement] Calculated placement position: {placementPos}");

            // Validate proximity
            if (!IsPositionValid(placementPos))
            {
                SystemDebug.LogWarning(SystemDebug.Category.StorageSystem,
                    "[StorageDevicePlacement] ❌ PLACEMENT BLOCKED - too close to another object (min distance: 1 unit)");
                return;
            }

            SystemDebug.Log(SystemDebug.Category.StorageSystem,
                "[StorageDevicePlacement] ✓ Position valid - proceeding with placement");

            // Place the device
            PlaceDevice(placementPos);
        }

        #endregion

        #region Placement Logic

        Vector3 CalculatePlacementPosition()
        {
            // Get player position and forward direction
            Vector3 playerPos = transform.position;
            Vector3 playerForward = transform.forward;

            // Calculate position in front of player
            Vector3 targetPos = playerPos + playerForward * placementDistance;

            // Project onto sphere surface
            if (worldSphere != null)
            {
                Vector3 sphereCenter = worldSphere.transform.position;
                Vector3 direction = (targetPos - sphereCenter).normalized;

                // Place on surface (slightly above for clearance)
                targetPos = sphereCenter + direction * (sphereRadius + 1f);
            }

            return targetPos;
        }

        bool IsPositionValid(Vector3 position)
        {
            // Check for overlapping colliders within proximity radius
            Collider[] nearbyColliders = Physics.OverlapSphere(position, proximityCheckRadius);

            if (nearbyColliders.Length > 0)
            {
                if (showDebugInfo)
                {
                    SystemDebug.Log(SystemDebug.Category.StorageSystem,
                        $"[StorageDevicePlacement] Found {nearbyColliders.Length} objects within {proximityCheckRadius} units:");
                    foreach (var col in nearbyColliders)
                    {
                        SystemDebug.Log(SystemDebug.Category.StorageSystem,
                            $"  - {col.gameObject.name} at distance {Vector3.Distance(position, col.transform.position):F2}");
                    }
                }
                return false;
            }

            return true;
        }

        void PlaceDevice(Vector3 position)
        {
            // Generate device name with auto-incrementing counter
            string deviceName = $"{defaultDeviceName} #{deviceCounter}";
            deviceCounter++;

            SystemDebug.Log(SystemDebug.Category.StorageSystem,
                $"[StorageDevicePlacement] 🔧 Calling server reducer: CreateStorageDevice");
            SystemDebug.Log(SystemDebug.Category.StorageSystem,
                $"[StorageDevicePlacement]    Device Name: '{deviceName}'");
            SystemDebug.Log(SystemDebug.Category.StorageSystem,
                $"[StorageDevicePlacement]    Position: ({position.x:F2}, {position.y:F2}, {position.z:F2})");

            // Call server reducer to create storage device
            try
            {
                if (GameManager.Conn == null)
                {
                    SystemDebug.LogError(SystemDebug.Category.StorageSystem,
                        "[StorageDevicePlacement] ❌ GameManager.Conn is NULL!");
                    return;
                }

                if (GameManager.Conn.Reducers == null)
                {
                    SystemDebug.LogError(SystemDebug.Category.StorageSystem,
                        "[StorageDevicePlacement] ❌ GameManager.Conn.Reducers is NULL!");
                    return;
                }

                GameManager.Conn.Reducers.CreateStorageDevice(
                    position.x,
                    position.y,
                    position.z,
                    deviceName
                );

                SystemDebug.Log(SystemDebug.Category.StorageSystem,
                    $"[StorageDevicePlacement] ✅ Reducer call sent to server - waiting for response...");
            }
            catch (System.Exception ex)
            {
                SystemDebug.LogError(SystemDebug.Category.StorageSystem,
                    $"[StorageDevicePlacement] ❌ EXCEPTION: {ex.Message}");
                SystemDebug.LogError(SystemDebug.Category.StorageSystem,
                    $"[StorageDevicePlacement] Stack trace: {ex.StackTrace}");
            }
        }

        #endregion

        #region Placement Preview (Optional)

        void UpdatePlacementPreview()
        {
            Vector3 previewPos = CalculatePlacementPosition();
            bool isValid = IsPositionValid(previewPos);

            // Create or update preview object
            if (previewObject == null && placementPreviewPrefab != null)
            {
                previewObject = Instantiate(placementPreviewPrefab);
                previewObject.name = "PlacementPreview";
            }

            if (previewObject != null)
            {
                previewObject.transform.position = previewPos;

                // Orient to sphere surface
                if (worldSphere != null)
                {
                    Vector3 sphereCenter = worldSphere.transform.position;
                    Vector3 surfaceNormal = (previewPos - sphereCenter).normalized;
                    previewObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
                }

                // Color code preview: green = valid, red = blocked
                Renderer renderer = previewObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = isValid ? new Color(0f, 1f, 0f, 0.5f) : new Color(1f, 0f, 0f, 0.5f);
                }
            }
        }

        #endregion

        #region Public Utilities

        /// <summary>
        /// Manually trigger device placement (for UI buttons or other systems)
        /// </summary>
        public void TriggerPlacement()
        {
            if (!isLocalPlayer || !GameManager.IsConnected())
            {
                SystemDebug.LogWarning(SystemDebug.Category.StorageSystem,
                    "[StorageDevicePlacement] Cannot place device - not local player or not connected");
                return;
            }

            Vector3 placementPos = CalculatePlacementPosition();

            if (IsPositionValid(placementPos))
            {
                PlaceDevice(placementPos);
            }
            else
            {
                SystemDebug.LogWarning(SystemDebug.Category.StorageSystem,
                    "[StorageDevicePlacement] Placement blocked - position not valid");
            }
        }

        /// <summary>
        /// Check if a position is valid for placement without actually placing
        /// </summary>
        public bool CheckPositionValidity(Vector3 position)
        {
            return IsPositionValid(position);
        }

        #endregion
    }
}
