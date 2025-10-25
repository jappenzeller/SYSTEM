using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;

namespace SYSTEM.Game
{
    /// <summary>
    /// Manages visualization of storage devices placed by players.
    /// Subscribes to GameEventBus for StorageDevice events and creates/updates GameObjects.
    /// Uses event-driven architecture - does NOT directly access SpacetimeDB tables.
    /// </summary>
    public class StorageDeviceManager : MonoBehaviour
    {
        [Header("Visualization Settings")]
        [SerializeField] private GameObject storageDevicePrefab;
        [SerializeField] private float deviceVisualScale = 3f;
        [SerializeField] private bool showDebugInfo = true;

        [Header("Capacity Visualization")]
        [SerializeField] private Color emptyColor = new Color(0.3f, 0.3f, 0.5f, 0.8f); // Dark blue when empty
        [SerializeField] private Color fullColor = new Color(0f, 1f, 1f, 1f); // Bright cyan when full
        [SerializeField] private float minEmissionIntensity = 0.5f;
        [SerializeField] private float maxEmissionIntensity = 3f;

        [Header("Frequency Colors")]
        [SerializeField] private Color redColor = new Color(1f, 0f, 0f);      // 0.0
        [SerializeField] private Color yellowColor = new Color(1f, 1f, 0f);   // 1.047
        [SerializeField] private Color greenColor = new Color(0f, 1f, 0f);    // 2.094
        [SerializeField] private Color cyanColor = new Color(0f, 1f, 1f);     // 3.142
        [SerializeField] private Color blueColor = new Color(0f, 0f, 1f);     // 4.189
        [SerializeField] private Color magentaColor = new Color(1f, 0f, 1f);  // 5.236

        // Tracking
        private Dictionary<ulong, GameObject> activeDevices = new Dictionary<ulong, GameObject>();
        private Material deviceMaterial;

        void Awake()
        {
            SystemDebug.Log(SystemDebug.Category.OrbVisualization, "[StorageDeviceManager] Awake - Component initialized");

            // Create material for storage devices
            CreateDeviceMaterial();
        }

        void OnEnable()
        {
            // ONLY subscribe to GameEventBus events, no direct database subscriptions
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.Subscribe<DeviceInsertedEvent>(OnDeviceInsertedEvent);
                GameEventBus.Instance.Subscribe<DeviceUpdatedEvent>(OnDeviceUpdatedEvent);
                GameEventBus.Instance.Subscribe<DeviceDeletedEvent>(OnDeviceDeletedEvent);
                GameEventBus.Instance.Subscribe<InitialDevicesLoadedEvent>(OnInitialDevicesLoadedEvent);
                GameEventBus.Instance.Subscribe<WorldTransitionStartedEvent>(OnWorldTransitionEvent);

                SystemDebug.Log(SystemDebug.Category.OrbVisualization, "[StorageDeviceManager] Subscribed to GameEventBus device events");
            }
            else
            {
                SystemDebug.LogError(SystemDebug.Category.OrbVisualization, "[StorageDeviceManager] GameEventBus.Instance is null!");
            }
        }

        void OnDisable()
        {
            // Unsubscribe from GameEventBus
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.Unsubscribe<DeviceInsertedEvent>(OnDeviceInsertedEvent);
                GameEventBus.Instance.Unsubscribe<DeviceUpdatedEvent>(OnDeviceUpdatedEvent);
                GameEventBus.Instance.Unsubscribe<DeviceDeletedEvent>(OnDeviceDeletedEvent);
                GameEventBus.Instance.Unsubscribe<InitialDevicesLoadedEvent>(OnInitialDevicesLoadedEvent);
                GameEventBus.Instance.Unsubscribe<WorldTransitionStartedEvent>(OnWorldTransitionEvent);
            }

            // Clean up all visualizations
            foreach (var device in activeDevices.Values)
            {
                if (device != null)
                    Destroy(device);
            }
            activeDevices.Clear();
        }

        #region GameEventBus Event Handlers

        private void OnDeviceInsertedEvent(DeviceInsertedEvent evt)
        {
            SystemDebug.Log(SystemDebug.Category.OrbVisualization,
                $"[StorageDeviceManager] Device inserted: {evt.Device.DeviceId} - {evt.Device.DeviceName}");
            CreateDeviceVisualization(evt.Device);
        }

        private void OnDeviceUpdatedEvent(DeviceUpdatedEvent evt)
        {
            SystemDebug.Log(SystemDebug.Category.OrbVisualization,
                $"[StorageDeviceManager] Device updated: {evt.NewDevice.DeviceId}");
            UpdateDeviceVisualization(evt.NewDevice);
        }

        private void OnDeviceDeletedEvent(DeviceDeletedEvent evt)
        {
            SystemDebug.Log(SystemDebug.Category.OrbVisualization,
                $"[StorageDeviceManager] Device deleted: {evt.Device.DeviceId}");
            RemoveDeviceVisualization(evt.Device.DeviceId);
        }

        private void OnInitialDevicesLoadedEvent(InitialDevicesLoadedEvent evt)
        {
            SystemDebug.Log(SystemDebug.Category.OrbVisualization,
                $"[StorageDeviceManager] Loading {evt.Devices.Count} initial devices");

            foreach (var device in evt.Devices)
            {
                CreateDeviceVisualization(device);
            }
        }

        private void OnWorldTransitionEvent(WorldTransitionStartedEvent evt)
        {
            SystemDebug.Log(SystemDebug.Category.OrbVisualization, "[StorageDeviceManager] World transition - clearing all devices");

            // Clear all device visualizations when changing worlds
            foreach (var device in activeDevices.Values)
            {
                if (device != null)
                    Destroy(device);
            }
            activeDevices.Clear();
        }

        #endregion

        #region Visualization Management

        private void CreateDeviceVisualization(StorageDevice device)
        {
            // Don't create duplicate
            if (activeDevices.ContainsKey(device.DeviceId))
            {
                SystemDebug.LogWarning(SystemDebug.Category.OrbVisualization,
                    $"[StorageDeviceManager] Device {device.DeviceId} already exists, updating instead");
                UpdateDeviceVisualization(device);
                return;
            }

            // Create GameObject
            GameObject deviceObj;
            if (storageDevicePrefab != null)
            {
                deviceObj = Instantiate(storageDevicePrefab);
            }
            else
            {
                // Fallback: create primitive cube
                deviceObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SystemDebug.LogWarning(SystemDebug.Category.OrbVisualization,
                    "[StorageDeviceManager] No prefab assigned, using primitive cube");
            }

            deviceObj.name = $"StorageDevice_{device.DeviceId}_{device.DeviceName}";

            // Convert database position to Unity world position
            Vector3 position = new Vector3(device.Position.X, device.Position.Y, device.Position.Z);
            deviceObj.transform.position = position;
            deviceObj.transform.localScale = Vector3.one * deviceVisualScale;

            // Orient to sphere surface normal
            Vector3 surfaceNormal = position.normalized;
            deviceObj.transform.rotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);

            // Apply material and color based on stored composition
            ApplyDeviceMaterial(deviceObj, device);

            // Add to tracking dictionary
            activeDevices[device.DeviceId] = deviceObj;

            SystemDebug.Log(SystemDebug.Category.OrbVisualization,
                $"[StorageDeviceManager] Created device visualization for {device.DeviceId} at {position}");
        }

        private void UpdateDeviceVisualization(StorageDevice device)
        {
            if (!activeDevices.TryGetValue(device.DeviceId, out GameObject deviceObj))
            {
                SystemDebug.LogWarning(SystemDebug.Category.OrbVisualization,
                    $"[StorageDeviceManager] Device {device.DeviceId} not found for update, creating new");
                CreateDeviceVisualization(device);
                return;
            }

            if (deviceObj == null)
            {
                SystemDebug.LogError(SystemDebug.Category.OrbVisualization,
                    $"[StorageDeviceManager] Device GameObject for {device.DeviceId} is null!");
                activeDevices.Remove(device.DeviceId);
                return;
            }

            // Update position (in case device moved - though unlikely)
            Vector3 position = new Vector3(device.Position.X, device.Position.Y, device.Position.Z);
            deviceObj.transform.position = position;

            // Update material/color based on new composition
            ApplyDeviceMaterial(deviceObj, device);

            SystemDebug.Log(SystemDebug.Category.OrbVisualization,
                $"[StorageDeviceManager] Updated device {device.DeviceId}");
        }

        private void RemoveDeviceVisualization(ulong deviceId)
        {
            if (activeDevices.TryGetValue(deviceId, out GameObject deviceObj))
            {
                if (deviceObj != null)
                {
                    Destroy(deviceObj);
                }
                activeDevices.Remove(deviceId);

                SystemDebug.Log(SystemDebug.Category.OrbVisualization,
                    $"[StorageDeviceManager] Removed device visualization for {deviceId}");
            }
            else
            {
                SystemDebug.LogWarning(SystemDebug.Category.OrbVisualization,
                    $"[StorageDeviceManager] Device {deviceId} not found for removal");
            }
        }

        #endregion

        #region Material and Color Management

        private void CreateDeviceMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }
            }

            if (shader != null)
            {
                deviceMaterial = new Material(shader);

                // Try to set URP properties (will silently fail if not URP)
                if (deviceMaterial.HasProperty("_Surface"))
                    deviceMaterial.SetFloat("_Surface", 0); // Opaque
                if (deviceMaterial.HasProperty("_Metallic"))
                    deviceMaterial.SetFloat("_Metallic", 0.5f);
                if (deviceMaterial.HasProperty("_Smoothness"))
                    deviceMaterial.SetFloat("_Smoothness", 0.8f);

                SystemDebug.Log(SystemDebug.Category.OrbVisualization,
                    $"[StorageDeviceManager] Created material with shader: {shader.name}");
            }
            else
            {
                SystemDebug.LogError(SystemDebug.Category.OrbVisualization,
                    "[StorageDeviceManager] Could not find any shader for device material!");
            }
        }

        private void ApplyDeviceMaterial(GameObject deviceObj, StorageDevice device)
        {
            var renderer = deviceObj.GetComponent<Renderer>();
            if (renderer == null)
            {
                SystemDebug.LogWarning(SystemDebug.Category.OrbVisualization,
                    $"[StorageDeviceManager] No renderer on device {device.DeviceId}");
                return;
            }

            // Clone material instance
            if (deviceMaterial != null)
            {
                renderer.material = new Material(deviceMaterial);
            }

            // Calculate fullness percentage
            int totalStored = 0;
            foreach (var sample in device.StoredComposition)
            {
                totalStored += (int)sample.Count;
            }

            // Maximum capacity = capacity_per_frequency * 6 frequencies
            int maxCapacity = (int)(device.CapacityPerFrequency * 6);
            float fullnessPercent = maxCapacity > 0 ? (float)totalStored / maxCapacity : 0f;

            // Determine dominant color from stored composition
            Color dominantColor = GetDominantColor(device.StoredComposition);

            // Blend between empty and full color based on fullness
            Color finalColor = Color.Lerp(emptyColor, dominantColor, fullnessPercent);

            // Apply color
            renderer.material.color = finalColor;

            // Apply emission for glow effect
            if (renderer.material.HasProperty("_EmissionColor"))
            {
                float emissionIntensity = Mathf.Lerp(minEmissionIntensity, maxEmissionIntensity, fullnessPercent);
                Color emissionColor = dominantColor * emissionIntensity;
                renderer.material.SetColor("_EmissionColor", emissionColor);
                renderer.material.EnableKeyword("_EMISSION");
            }

            if (showDebugInfo)
            {
                SystemDebug.Log(SystemDebug.Category.OrbVisualization,
                    $"[StorageDeviceManager] Device {device.DeviceId}: {totalStored}/{maxCapacity} packets ({fullnessPercent:P0}) - Color: {dominantColor}");
            }
        }

        private Color GetDominantColor(List<WavePacketSample> composition)
        {
            if (composition == null || composition.Count == 0)
                return emptyColor;

            // Find frequency with highest count
            uint maxCount = 0;
            float dominantFrequency = 0f;

            foreach (var sample in composition)
            {
                if (sample.Count > maxCount)
                {
                    maxCount = sample.Count;
                    dominantFrequency = sample.Frequency;
                }
            }

            // If no packets stored, return empty color
            if (maxCount == 0)
                return emptyColor;

            // Map frequency to color
            return GetColorForFrequency(dominantFrequency);
        }

        private Color GetColorForFrequency(float frequency)
        {
            // Match frequency to nearest color (frequencies in radians)
            // Red: 0.0, Yellow: 1.047, Green: 2.094, Cyan: 3.142, Blue: 4.189, Magenta: 5.236

            if (Mathf.Abs(frequency - 0.0f) < 0.5f) return redColor;
            if (Mathf.Abs(frequency - 1.047f) < 0.5f) return yellowColor;
            if (Mathf.Abs(frequency - 2.094f) < 0.5f) return greenColor;
            if (Mathf.Abs(frequency - 3.142f) < 0.5f) return cyanColor;
            if (Mathf.Abs(frequency - 4.189f) < 0.5f) return blueColor;
            if (Mathf.Abs(frequency - 5.236f) < 0.5f) return magentaColor;

            // Default to white if no match
            return Color.white;
        }

        #endregion

        #region Public Utilities

        /// <summary>
        /// Get the GameObject for a specific storage device ID
        /// </summary>
        public GameObject GetDeviceGameObject(ulong deviceId)
        {
            activeDevices.TryGetValue(deviceId, out GameObject deviceObj);
            return deviceObj;
        }

        /// <summary>
        /// Get all active storage device GameObjects
        /// </summary>
        public IEnumerable<GameObject> GetAllDeviceGameObjects()
        {
            return activeDevices.Values;
        }

        #endregion
    }
}
