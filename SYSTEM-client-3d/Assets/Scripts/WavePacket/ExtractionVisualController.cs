using UnityEngine;
using SpacetimeDB.Types;
using System.Collections.Generic;

namespace SYSTEM.WavePacket
{
    /// <summary>
    /// Controls extraction visual effects for wave packet mining
    /// Integrates with WavePacketMiningSystem to show detailed ring visualization
    /// </summary>
    public class ExtractionVisualController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WavePacketRenderer waveRenderer;
        [SerializeField] private GameObject extractedPacketPrefab; // Prefab with WavePacketVisual component

        [Header("Settings")]
        [SerializeField] private float packetTravelSpeed = 5f;
        [SerializeField] private bool autoCreateRenderer = true;

        private Dictionary<ulong, ExtractionInstance> activeExtractions = new Dictionary<ulong, ExtractionInstance>();

        private class ExtractionInstance
        {
            public ulong orbId;
            public WavePacketSample[] samples;
            public Vector3 position;
            public float startTime;
            public bool isActive;
        }

        void Awake()
        {
            if (waveRenderer == null && autoCreateRenderer)
            {
                GameObject rendererObj = new GameObject("WavePacketRenderer");
                rendererObj.transform.SetParent(transform);
                waveRenderer = WavePacketFactory.CreateRenderer(rendererObj);
                UnityEngine.Debug.Log($"[ExtractionVisual] Auto-created {waveRenderer.GetType().Name}");
            }
        }

        /// <summary>
        /// Start extraction visual for an orb
        /// </summary>
        public void StartExtraction(ulong orbId, WavePacketSample[] samples, Vector3 orbPosition)
        {
            if (waveRenderer == null)
            {
                UnityEngine.Debug.LogError("[ExtractionVisual] No waveRenderer available!");
                return;
            }

            // Stop any existing extraction for this orb
            if (activeExtractions.ContainsKey(orbId))
            {
                StopExtraction(orbId);
            }

            var instance = new ExtractionInstance
            {
                orbId = orbId,
                samples = samples,
                position = orbPosition,
                startTime = Time.time,
                isActive = true
            };

            activeExtractions[orbId] = instance;
            waveRenderer.StartExtraction(samples, orbPosition);

            UnityEngine.Debug.Log($"[ExtractionVisual] Started extraction for orb {orbId} with {samples.Length} frequencies");
        }

        /// <summary>
        /// Stop extraction visual for an orb
        /// </summary>
        public void StopExtraction(ulong orbId)
        {
            if (activeExtractions.TryGetValue(orbId, out var instance))
            {
                instance.isActive = false;
                activeExtractions.Remove(orbId);

                if (waveRenderer != null)
                {
                    waveRenderer.EndExtraction();
                }

                UnityEngine.Debug.Log($"[ExtractionVisual] Stopped extraction for orb {orbId}");
            }
        }

        /// <summary>
        /// Manually update extraction progress (0-1)
        /// </summary>
        public void UpdateExtractionProgress(ulong orbId, float progress)
        {
            if (activeExtractions.ContainsKey(orbId) && waveRenderer != null)
            {
                waveRenderer.UpdateExtraction(progress);
            }
        }

        /// <summary>
        /// Create a flying packet with trajectory animation (legacy signature).
        /// Returns the GameObject for tracking.
        /// </summary>
        public GameObject SpawnFlyingPacket(WavePacketSample[] samples, Vector3 startPosition, Vector3 targetPosition, float speed = 0f, System.Action onArrival = null)
        {
            // Use legacy behavior - no rotation or height specified
            return SpawnFlyingPacket(samples, startPosition, Quaternion.identity, targetPosition, Quaternion.identity, 0f, 0f, speed, onArrival);
        }

        /// <summary>
        /// Create a flying packet with full trajectory animation including rotation and height transitions.
        /// Returns the GameObject for tracking.
        /// </summary>
        public GameObject SpawnFlyingPacket(WavePacketSample[] samples,
                                           Vector3 startPosition, Quaternion startRotation,
                                           Vector3 targetPosition, Quaternion targetRotation,
                                           float startHeight, float endHeight,
                                           float speed = 0f, System.Action onArrival = null)
        {
            float actualSpeed = speed > 0 ? speed : packetTravelSpeed;

            // Try prefab-based approach first
            if (extractedPacketPrefab != null)
            {
                GameObject packet = Instantiate(extractedPacketPrefab, startPosition, startRotation);
                packet.name = $"ExtractedPacket_{Time.frameCount}";

                // Initialize WavePacketVisual
                var visual = packet.GetComponent<SYSTEM.Game.WavePacketVisual>();
                if (visual != null)
                {
                    var sampleList = new List<WavePacketSample>(samples);
                    uint totalPackets = 0;
                    foreach (var sample in samples) totalPackets += sample.Count;

                    Color packetColor = FrequencyConstants.GetColorForFrequency(samples[0].Frequency);
                    visual.Initialize(0, packetColor, totalPackets, 0, sampleList);
                }

                // Add trajectory with rotation and height support
                var trajectory = packet.AddComponent<PacketTrajectory>();
                trajectory.Initialize(targetPosition, targetRotation, actualSpeed, startHeight, endHeight, onArrival);

                UnityEngine.Debug.Log($"[ExtractionVisual] Spawned prefab packet from {startPosition} to {targetPosition} with rotation and height {startHeight}->{endHeight}");
                return packet;
            }

            // Fallback to renderer
            if (waveRenderer != null)
            {
                GameObject packet = waveRenderer.CreateFlyingPacket(samples, startPosition, targetPosition, actualSpeed);
                if (packet != null)
                {
                    UnityEngine.Debug.Log($"[ExtractionVisual] Spawned renderer packet (no rotation support)");
                }
                return packet;
            }

            UnityEngine.Debug.LogWarning("[ExtractionVisual] No prefab or renderer!");
            return null;
        }

        void Update()
        {
            // Auto-cleanup finished extractions
            var toRemove = new List<ulong>();

            foreach (var kvp in activeExtractions)
            {
                if (!kvp.Value.isActive)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var orbId in toRemove)
            {
                StopExtraction(orbId);
            }
        }

        void OnDestroy()
        {
            // Clean up all active extractions
            foreach (var orbId in new List<ulong>(activeExtractions.Keys))
            {
                StopExtraction(orbId);
            }
        }
    }
}
