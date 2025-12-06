using UnityEngine;
using System.Collections.Generic;

namespace SYSTEM.WavePacket
{
    /// <summary>
    /// Centralized manager for wave packet prefabs and settings.
    /// Maps PacketType to prefab+settings pairs for consistent visualization.
    /// </summary>
    [CreateAssetMenu(fileName = "WavePacketPrefabManager", menuName = "SYSTEM/Wave Packet Prefab Manager")]
    public class WavePacketPrefabManager : ScriptableObject
    {
        /// <summary>
        /// Types of wave packets in the game.
        /// All energy visualization uses one of these types.
        /// </summary>
        public enum PacketType
        {
            Source,        // Stationary mineable sources
            Extracted,     // Flying packets from mining
            Transfer,      // Flying packets between devices
            Distribution   // Flying packets from spires
        }

        /// <summary>
        /// Container for prefab and its associated settings.
        /// </summary>
        [System.Serializable]
        public class PrefabSettings
        {
            [Tooltip("GameObject prefab for this packet type")]
            public GameObject prefab;

            [Tooltip("Wave packet rendering settings for this type")]
            public WavePacketSettings settings;
        }

        [Header("Default Fallback")]
        [Tooltip("Used when specific type configuration is missing")]
        [SerializeField] private PrefabSettings defaultPrefab;

        [Header("Packet Type Configurations")]
        [Tooltip("Configuration for stationary mineable sources")]
        [SerializeField] private PrefabSettings sourcePrefab;

        [Tooltip("Configuration for extracted packets from mining")]
        [SerializeField] private PrefabSettings extractedPrefab;

        [Tooltip("Configuration for transfer packets between devices")]
        [SerializeField] private PrefabSettings transferPrefab;

        [Tooltip("Configuration for distribution packets from spires")]
        [SerializeField] private PrefabSettings distributionPrefab;

        /// <summary>
        /// Get prefab and settings for the specified packet type.
        /// Falls back to default if specific configuration is missing.
        /// </summary>
        /// <param name="type">The packet type to get configuration for</param>
        /// <returns>Tuple of (prefab GameObject, WavePacketSettings)</returns>
        public (GameObject prefab, WavePacketSettings settings) GetPrefabAndSettings(PacketType type)
        {
            PrefabSettings config = type switch
            {
                PacketType.Source => sourcePrefab,
                PacketType.Extracted => extractedPrefab,
                PacketType.Transfer => transferPrefab,
                PacketType.Distribution => distributionPrefab,
                _ => defaultPrefab
            };

            // Fallback to default if specific config is missing
            if (config == null || config.prefab == null || config.settings == null)
            {
                UnityEngine.Debug.LogWarning($"[WavePacketPrefabManager] Missing config for {type}, using default");
                config = defaultPrefab;

                // Final safety check
                if (config == null || config.prefab == null || config.settings == null)
                {
                    UnityEngine.Debug.LogError($"[WavePacketPrefabManager] Default config is also missing! Cannot create wave packet.");
                    return (null, null);
                }
            }

            return (config.prefab, config.settings);
        }

        /// <summary>
        /// Validate that all packet types have valid configurations.
        /// </summary>
        public void ValidateConfigurations()
        {
            bool hasErrors = false;

            if (defaultPrefab == null || defaultPrefab.prefab == null || defaultPrefab.settings == null)
            {
                UnityEngine.Debug.LogError("[WavePacketPrefabManager] Default prefab configuration is incomplete!");
                hasErrors = true;
            }

            if (sourcePrefab == null || sourcePrefab.prefab == null || sourcePrefab.settings == null)
            {
                UnityEngine.Debug.LogWarning("[WavePacketPrefabManager] Source prefab configuration is incomplete. Will use default.");
            }

            if (extractedPrefab == null || extractedPrefab.prefab == null || extractedPrefab.settings == null)
            {
                UnityEngine.Debug.LogWarning("[WavePacketPrefabManager] Extracted prefab configuration is incomplete. Will use default.");
            }

            if (transferPrefab == null || transferPrefab.prefab == null || transferPrefab.settings == null)
            {
                UnityEngine.Debug.LogWarning("[WavePacketPrefabManager] Transfer prefab configuration is incomplete. Will use default.");
            }

            if (distributionPrefab == null || distributionPrefab.prefab == null || distributionPrefab.settings == null)
            {
                UnityEngine.Debug.LogWarning("[WavePacketPrefabManager] Distribution prefab configuration is incomplete. Will use default.");
            }

            if (!hasErrors)
            {
                UnityEngine.Debug.Log("[WavePacketPrefabManager] All configurations validated successfully.");
            }
        }

        private void OnValidate()
        {
            ValidateConfigurations();
        }
    }
}
