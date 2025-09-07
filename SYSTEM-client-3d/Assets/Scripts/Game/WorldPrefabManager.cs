using UnityEngine;

namespace SYSTEM.Game
{
    /// <summary>
    /// Manages world prefab references and provides them to world controllers
    /// </summary>
    [CreateAssetMenu(fileName = "WorldPrefabManager", menuName = "SYSTEM/World Prefab Manager")]
    public class WorldPrefabManager : ScriptableObject
    {
        [Header("World Prefabs")]
        [Tooltip("Default world sphere prefab")]
        public GameObject defaultWorldSpherePrefab;
        
        [Tooltip("Special world prefabs by type")]
        public WorldPrefabEntry[] specialWorldPrefabs;
        
        [Header("Materials")]
        [Tooltip("Default world material for WebGL compatibility")]
        public Material defaultWorldMaterial;
        
        [Tooltip("Alternative materials for different world types")]
        public Material[] alternativeMaterials;
        
        [Header("Default Settings")]
        public float defaultWorldRadius = 300f;
        public Color defaultWorldColor = new Color(0.2f, 0.3f, 0.5f);
        
        [System.Serializable]
        public class WorldPrefabEntry
        {
            public string worldName;
            public GameObject prefab;
            public Material material;
            public float radius = 300f;
        }
        
        /// <summary>
        /// Get the appropriate prefab for a world
        /// </summary>
        public GameObject GetWorldPrefab(string worldName = null)
        {
            if (!string.IsNullOrEmpty(worldName))
            {
                foreach (var entry in specialWorldPrefabs)
                {
                    if (entry.worldName == worldName && entry.prefab != null)
                    {
                        return entry.prefab;
                    }
                }
            }
            
            return defaultWorldSpherePrefab;
        }
        
        /// <summary>
        /// Get the appropriate material for a world
        /// </summary>
        public Material GetWorldMaterial(string worldName = null)
        {
            if (!string.IsNullOrEmpty(worldName))
            {
                foreach (var entry in specialWorldPrefabs)
                {
                    if (entry.worldName == worldName && entry.material != null)
                    {
                        return entry.material;
                    }
                }
            }
            
            return defaultWorldMaterial;
        }
        
        /// <summary>
        /// Get the radius for a specific world
        /// </summary>
        public float GetWorldRadius(string worldName = null)
        {
            if (!string.IsNullOrEmpty(worldName))
            {
                foreach (var entry in specialWorldPrefabs)
                {
                    if (entry.worldName == worldName)
                    {
                        return entry.radius;
                    }
                }
            }
            
            return defaultWorldRadius;
        }
        
        /// <summary>
        /// Validate that all required assets are assigned
        /// </summary>
        public bool ValidateAssets()
        {
            if (defaultWorldSpherePrefab == null)
            {
                UnityEngine.Debug.LogError("[WorldPrefabManager] Default world sphere prefab is not assigned!");
                return false;
            }
            
            if (defaultWorldMaterial == null)
            {
                UnityEngine.Debug.LogWarning("[WorldPrefabManager] Default world material is not assigned, will create at runtime");
            }
            
            return true;
        }
    }
}