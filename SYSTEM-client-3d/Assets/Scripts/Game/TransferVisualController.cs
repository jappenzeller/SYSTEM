using UnityEngine;
using SpacetimeDB.Types;
using System.Collections.Generic;
using SYSTEM.WavePacket;

namespace SYSTEM.Game
{
    /// <summary>
    /// Controls visual effects for energy packet transfers between inventory and storage devices.
    /// Uses wave packet rendering system for consistent visuals with mining extraction.
    /// Handles multi-waypoint routing through distribution spheres with spire flash effects.
    /// </summary>
    public class TransferVisualController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WavePacketRenderer waveRenderer;
        [SerializeField] private GameObject transferPacketPrefab; // Prefab with WavePacketVisual component

        [Header("Transfer Packet Settings")]
        [SerializeField] private float transferSpeed = 5f;
        [SerializeField] private float packetScale = 1.5f;
        [SerializeField] private AnimationCurve transferCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private bool autoCreateRenderer = true;

        [Header("Visual Effects")]
        [SerializeField] private GameObject spireArrivalEffectPrefab;
        [SerializeField] private GameObject deviceArrivalEffectPrefab;
        [SerializeField] private bool showPacketTrail = true;
        [SerializeField] private Color spireFlashColor = new Color(0, 1, 1, 1); // Cyan
        [SerializeField] private float spireFlashDuration = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;

        private Dictionary<ulong, List<GameObject>> activeTransferPackets = new Dictionary<ulong, List<GameObject>>();

        void Awake()
        {
            if (waveRenderer == null && autoCreateRenderer)
            {
                GameObject rendererObj = new GameObject("TransferWavePacketRenderer");
                rendererObj.transform.SetParent(transform);
                waveRenderer = WavePacketFactory.CreateRenderer(rendererObj);

                if (showDebugLogs)
                {
                    UnityEngine.Debug.Log($"[TransferVisual] Auto-created {waveRenderer.GetType().Name}");
                }
            }

            // Load transfer packet prefab from Resources if not assigned
            if (transferPacketPrefab == null)
            {
                transferPacketPrefab = Resources.Load<GameObject>("WavePacketVisual");
                if (transferPacketPrefab != null && showDebugLogs)
                {
                    UnityEngine.Debug.Log("[TransferVisual] Loaded WavePacketVisual prefab from Resources");
                }
            }
        }

        /// <summary>
        /// Spawn a transfer packet that flies from start to end position
        /// Returns the GameObject for tracking
        /// </summary>
        public GameObject SpawnTransferPacket(
            WavePacketSample[] composition,
            Vector3 startPosition,
            Vector3 endPosition,
            float customSpeed,
            System.Action onArrival = null)
        {
            float actualSpeed = customSpeed > 0 ? customSpeed : transferSpeed;

            if (showDebugLogs)
            {
                UnityEngine.Debug.Log($"[TransferVisual] Spawning packet: {startPosition} → {endPosition}, speed: {actualSpeed}");
            }

            // Try prefab-based approach first
            if (transferPacketPrefab != null)
            {
                GameObject packet = Instantiate(transferPacketPrefab, startPosition, Quaternion.identity);
                packet.name = $"TransferPacket_{Time.frameCount}";
                packet.transform.localScale = Vector3.one * packetScale;

                // Initialize WavePacketVisual
                var visual = packet.GetComponent<WavePacketVisual>();
                if (visual != null)
                {
                    var sampleList = new List<WavePacketSample>(composition);
                    uint totalPackets = 0;
                    foreach (var sample in composition) totalPackets += sample.Count;

                    Color packetColor = FrequencyConstants.GetColorForFrequency(composition[0].Frequency);
                    visual.Initialize(null, 0, packetColor, totalPackets, 0, sampleList);
                }

                // Add trajectory component for movement
                var trajectory = packet.GetComponent<PacketTrajectory>();
                if (trajectory == null)
                {
                    trajectory = packet.AddComponent<PacketTrajectory>();
                }
                trajectory.Initialize(endPosition, actualSpeed, onArrival);

                // Add trail if enabled
                if (showPacketTrail)
                {
                    var trail = packet.GetComponent<TrailRenderer>();
                    if (trail == null)
                    {
                        trail = packet.AddComponent<TrailRenderer>();
                        ConfigurePacketTrail(trail, composition[0].Frequency);
                    }
                }

                return packet;
            }

            // Fallback to renderer
            if (waveRenderer != null)
            {
                GameObject packet = waveRenderer.CreateFlyingPacket(composition, startPosition, endPosition, actualSpeed);
                if (packet != null && showDebugLogs)
                {
                    UnityEngine.Debug.Log($"[TransferVisual] Spawned renderer-based packet");
                }
                return packet;
            }

            UnityEngine.Debug.LogWarning("[TransferVisual] No prefab or renderer available!");
            return null;
        }

        /// <summary>
        /// Start a multi-waypoint transfer animation
        /// Spawns packets sequentially through each waypoint segment
        /// </summary>
        public void StartTransferAnimation(
            ulong transferId,
            WavePacketSample[] composition,
            Vector3[] waypoints,
            float speed,
            System.Action onComplete)
        {
            if (waypoints == null || waypoints.Length < 2)
            {
                UnityEngine.Debug.LogError("[TransferVisual] Need at least 2 waypoints for transfer");
                return;
            }

            if (!activeTransferPackets.ContainsKey(transferId))
            {
                activeTransferPackets[transferId] = new List<GameObject>();
            }

            if (showDebugLogs)
            {
                UnityEngine.Debug.Log($"[TransferVisual] Starting transfer {transferId} with {waypoints.Length} waypoints");
            }

            // Start the first segment
            SpawnSegmentPacket(transferId, composition, waypoints, 0, speed, onComplete);
        }

        /// <summary>
        /// Recursively spawn packets for each waypoint segment
        /// </summary>
        private void SpawnSegmentPacket(
            ulong transferId,
            WavePacketSample[] composition,
            Vector3[] waypoints,
            int segmentIndex,
            float speed,
            System.Action onComplete)
        {
            if (segmentIndex >= waypoints.Length - 1)
            {
                // All segments complete
                if (showDebugLogs)
                {
                    UnityEngine.Debug.Log($"[TransferVisual] Transfer {transferId} animation complete");
                }
                onComplete?.Invoke();
                return;
            }

            Vector3 startPos = waypoints[segmentIndex];
            Vector3 endPos = waypoints[segmentIndex + 1];

            // Spawn packet for this segment
            GameObject packet = SpawnTransferPacket(composition, startPos, endPos, speed, () =>
            {
                // Arrival callback for this segment
                if (showDebugLogs)
                {
                    UnityEngine.Debug.Log($"[TransferVisual] Segment {segmentIndex} complete");
                }

                // Flash spire effect if not first or last segment
                if (segmentIndex > 0 && segmentIndex < waypoints.Length - 2)
                {
                    FlashSpireAt(endPos);
                }

                // Spawn arrival effect if this is the final destination
                if (segmentIndex == waypoints.Length - 2)
                {
                    SpawnDeviceArrivalEffect(endPos);
                }

                // Spawn next segment
                SpawnSegmentPacket(transferId, composition, waypoints, segmentIndex + 1, speed, onComplete);
            });

            // Track the spawned packet
            if (packet != null && activeTransferPackets.ContainsKey(transferId))
            {
                activeTransferPackets[transferId].Add(packet);
            }
        }

        /// <summary>
        /// Stop and clean up a transfer animation
        /// </summary>
        public void StopTransfer(ulong transferId)
        {
            if (activeTransferPackets.TryGetValue(transferId, out List<GameObject> packets))
            {
                foreach (var packet in packets)
                {
                    if (packet != null)
                    {
                        Destroy(packet);
                    }
                }
                packets.Clear();
                activeTransferPackets.Remove(transferId);

                if (showDebugLogs)
                {
                    UnityEngine.Debug.Log($"[TransferVisual] Stopped transfer {transferId}");
                }
            }
        }

        /// <summary>
        /// Flash effect at a spire position
        /// </summary>
        private void FlashSpireAt(Vector3 position)
        {
            if (spireArrivalEffectPrefab != null)
            {
                GameObject effect = Instantiate(spireArrivalEffectPrefab, position, Quaternion.identity);
                Destroy(effect, spireFlashDuration);
            }

            // TODO: Find actual spire GameObject at this position and flash its material
            // This would require integration with EnergySpireManager

            if (showDebugLogs)
            {
                UnityEngine.Debug.Log($"[TransferVisual] Flashing spire at {position}");
            }
        }

        /// <summary>
        /// Arrival effect at storage device
        /// </summary>
        private void SpawnDeviceArrivalEffect(Vector3 position)
        {
            if (deviceArrivalEffectPrefab != null)
            {
                GameObject effect = Instantiate(deviceArrivalEffectPrefab, position, Quaternion.identity);
                Destroy(effect, 2f);
            }

            if (showDebugLogs)
            {
                UnityEngine.Debug.Log($"[TransferVisual] Device arrival effect at {position}");
            }
        }

        /// <summary>
        /// Configure trail renderer for transfer packet
        /// </summary>
        private void ConfigurePacketTrail(TrailRenderer trail, float frequency)
        {
            Color trailColor = FrequencyConstants.GetColorForFrequency(frequency);
            trailColor.a = 0.6f;

            trail.time = 0.5f;
            trail.startWidth = 0.3f;
            trail.endWidth = 0.05f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = trailColor;
            trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
            trail.numCornerVertices = 5;
            trail.numCapVertices = 5;
        }

        /// <summary>
        /// Clean up all active transfers
        /// </summary>
        void OnDestroy()
        {
            foreach (var kvp in activeTransferPackets)
            {
                foreach (var packet in kvp.Value)
                {
                    if (packet != null)
                    {
                        Destroy(packet);
                    }
                }
            }
            activeTransferPackets.Clear();
        }
    }
}
