using UnityEngine;
using SpacetimeDB.Types;
using SYSTEM.Game;
using System.Collections.Generic;
using SYSTEM.Debug;

namespace SYSTEM.WavePacket.Test
{
    /// <summary>
    /// Test controller for standalone orb visualization testing
    /// Allows setting custom composition without SpacetimeDB connection
    /// </summary>
    public class WavePacketSourceTestController : MonoBehaviour
    {
        [Header("Test Orb Settings")]
        [SerializeField] private GameObject orbPrefab;
        [SerializeField] private Vector3 spawnPosition = new Vector3(0, 1.5f, 0);
        [SerializeField] private ulong testOrbId = 999;
        [SerializeField] private uint totalPackets = 100;
        [SerializeField] private uint minerCount = 0;

        [Header("Composition Setup")]
        [Tooltip("Red frequency (0.0 or 0/6 normalized)")]
        [SerializeField] private uint redCount = 0;
        [Tooltip("Yellow frequency (1.047 or 1/6 normalized)")]
        [SerializeField] private uint yellowCount = 0;
        [Tooltip("Green frequency (2.094 or 2/6 normalized)")]
        [SerializeField] private uint greenCount = 30;
        [Tooltip("Cyan frequency (3.142 or 3/6 normalized)")]
        [SerializeField] private uint cyanCount = 0;
        [Tooltip("Blue frequency (4.189 or 4/6 normalized)")]
        [SerializeField] private uint blueCount = 50;
        [Tooltip("Magenta frequency (5.236 or 5/6 normalized)")]
        [SerializeField] private uint magentaCount = 0;

        [Header("Frequency Range")]
        [Tooltip("Use normalized 0-1 range instead of radians")]
        [SerializeField] private bool useNormalizedFrequencies = true;

        [Header("Runtime Controls")]
        [SerializeField] private bool spawnOnStart = true;

        private GameObject spawnedOrb;

        void Start()
        {
            if (spawnOnStart)
            {
                SpawnTestOrb();
            }
        }

        [ContextMenu("Spawn Test Orb")]
        public void SpawnTestOrb()
        {
            if (orbPrefab == null)
            {
                SystemDebug.LogError(SystemDebug.Category.WavePacketSystem, "[WavePacketSourceTest] No orb prefab assigned!");
                return;
            }

            // Clear existing orb
            if (spawnedOrb != null)
            {
                Destroy(spawnedOrb);
            }

            // Create composition from inspector values
            List<WavePacketSample> composition = CreateComposition();

            // Spawn orb
            spawnedOrb = Instantiate(orbPrefab, spawnPosition, Quaternion.identity);
            spawnedOrb.name = $"TestOrb_{testOrbId}";

            // Initialize the WavePacketVisual component
            var orbVisual = spawnedOrb.GetComponent<WavePacketVisual>();
            if (orbVisual != null)
            {
                // Use a neutral color - the wave packet composition will determine appearance
                Color neutralColor = Color.white;
                orbVisual.Initialize(null, testOrbId, neutralColor, totalPackets, minerCount, composition);

                SystemDebug.Log(SystemDebug.Category.WavePacketSystem, $"[WavePacketSourceTest] Spawned test orb with {composition.Count} frequency components, {totalPackets} total packets");
            }
            else
            {
                SystemDebug.LogError(SystemDebug.Category.WavePacketSystem, "[WavePacketSourceTest] Orb prefab doesn't have WavePacketVisual component!");
            }
        }

        [ContextMenu("Update Composition")]
        public void UpdateComposition()
        {
            if (spawnedOrb == null)
            {
                SystemDebug.LogWarning(SystemDebug.Category.WavePacketSystem, "[WavePacketSourceTest] No orb spawned. Use 'Spawn Test Orb' first.");
                return;
            }

            var orbVisual = spawnedOrb.GetComponent<WavePacketVisual>();
            if (orbVisual != null)
            {
                List<WavePacketSample> composition = CreateComposition();
                orbVisual.UpdateComposition(composition);
                orbVisual.UpdatePacketCount(totalPackets);
                orbVisual.UpdateMinerCount(minerCount);

                SystemDebug.Log(SystemDebug.Category.WavePacketSystem, $"[WavePacketSourceTest] Updated composition: {composition.Count} frequencies, {totalPackets} packets");
            }
        }

        [ContextMenu("Clear Test Orb")]
        public void ClearTestOrb()
        {
            if (spawnedOrb != null)
            {
                Destroy(spawnedOrb);
                spawnedOrb = null;
                SystemDebug.Log(SystemDebug.Category.WavePacketSystem, "[WavePacketSourceTest] Test orb cleared");
            }
        }

        private List<WavePacketSample> CreateComposition()
        {
            List<WavePacketSample> composition = new List<WavePacketSample>();

            // Add each frequency if count > 0
            if (redCount > 0)
                composition.Add(CreateSample(0, redCount));
            if (yellowCount > 0)
                composition.Add(CreateSample(1, yellowCount));
            if (greenCount > 0)
                composition.Add(CreateSample(2, greenCount));
            if (cyanCount > 0)
                composition.Add(CreateSample(3, cyanCount));
            if (blueCount > 0)
                composition.Add(CreateSample(4, blueCount));
            if (magentaCount > 0)
                composition.Add(CreateSample(5, magentaCount));

            return composition;
        }

        private WavePacketSample CreateSample(int frequencyIndex, uint count)
        {
            float frequency;

            if (useNormalizedFrequencies)
            {
                // Normalized 0-1 range (matches server)
                frequency = frequencyIndex / 6.0f;
            }
            else
            {
                // Radian range (legacy)
                float[] radianFrequencies = { 0.0f, 1.047f, 2.094f, 3.142f, 4.189f, 5.236f };
                frequency = radianFrequencies[frequencyIndex];
            }

            return new WavePacketSample
            {
                Frequency = frequency,
                Amplitude = 1.0f,
                Phase = 0.0f,
                Count = count
            };
        }

        void OnDrawGizmos()
        {
            // Draw spawn position
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(spawnPosition, 0.5f);
        }
    }
}
