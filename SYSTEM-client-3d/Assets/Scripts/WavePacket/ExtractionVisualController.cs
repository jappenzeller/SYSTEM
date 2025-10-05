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
        [SerializeField] private WavePacketRenderer renderer;

        [Header("Settings")]
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
            if (renderer == null && autoCreateRenderer)
            {
                GameObject rendererObj = new GameObject("WavePacketRenderer");
                rendererObj.transform.SetParent(transform);
                renderer = WavePacketFactory.CreateRenderer(rendererObj);
                UnityEngine.Debug.Log($"[ExtractionVisual] Auto-created {renderer.GetType().Name}");
            }
        }

        /// <summary>
        /// Start extraction visual for an orb
        /// </summary>
        public void StartExtraction(ulong orbId, WavePacketSample[] samples, Vector3 orbPosition)
        {
            if (renderer == null)
            {
                UnityEngine.Debug.LogError("[ExtractionVisual] No renderer available!");
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
            renderer.StartExtraction(samples, orbPosition);

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

                if (renderer != null)
                {
                    renderer.EndExtraction();
                }

                UnityEngine.Debug.Log($"[ExtractionVisual] Stopped extraction for orb {orbId}");
            }
        }

        /// <summary>
        /// Manually update extraction progress (0-1)
        /// </summary>
        public void UpdateExtractionProgress(ulong orbId, float progress)
        {
            if (activeExtractions.ContainsKey(orbId) && renderer != null)
            {
                renderer.UpdateExtraction(progress);
            }
        }

        /// <summary>
        /// Create a flying packet with trajectory animation
        /// Returns the GameObject for tracking
        /// </summary>
        public GameObject SpawnFlyingPacket(WavePacketSample[] samples, Vector3 startPosition, Vector3 targetPosition, float speed = 5f)
        {
            if (renderer == null)
            {
                UnityEngine.Debug.LogWarning("[ExtractionVisual] Cannot spawn flying packet - no renderer!");
                return null;
            }

            GameObject packet = renderer.CreateFlyingPacket(samples, startPosition, targetPosition, speed);

            if (packet != null)
            {
                UnityEngine.Debug.Log($"[ExtractionVisual] Spawned flying packet from {startPosition} to {targetPosition}");
            }

            return packet;
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
