using UnityEngine;
using System.Collections;

namespace SYSTEM.Debug
{
    /// <summary>
    /// Pre-warms shaders by instantiating prefabs at scene start to prevent runtime compilation stutter.
    /// This eliminates the frame freeze that occurs when creating wave packet meshes during active gameplay.
    /// </summary>
    public class ShaderPrewarmer : MonoBehaviour
    {
        [Header("Prefabs to Pre-warm")]
        [SerializeField] private GameObject[] prefabsToPrewarm;

        [Header("Settings")]
        [SerializeField] private bool prewarmOnStart = true;
        [SerializeField] private Vector3 offscreenPosition = new Vector3(0, -1000, 0);

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        void Start()
        {
            if (prewarmOnStart && prefabsToPrewarm != null && prefabsToPrewarm.Length > 0)
            {
                StartCoroutine(PrewarmShaders());
            }
        }

        private IEnumerator PrewarmShaders()
        {
            if (showDebugLogs)
            {
                SystemDebug.Log(SystemDebug.Category.Performance, $"[ShaderPrewarmer] Pre-warming {prefabsToPrewarm.Length} prefab shaders...");
            }

            GameObject[] dummyObjects = new GameObject[prefabsToPrewarm.Length];

            // Instantiate all prefabs off-screen
            for (int i = 0; i < prefabsToPrewarm.Length; i++)
            {
                if (prefabsToPrewarm[i] != null)
                {
                    dummyObjects[i] = Instantiate(prefabsToPrewarm[i], offscreenPosition, Quaternion.identity);
                    dummyObjects[i].name = $"PrewarmDummy_{prefabsToPrewarm[i].name}";

                    if (showDebugLogs)
                    {
                        SystemDebug.Log(SystemDebug.Category.Performance, $"[ShaderPrewarmer] Instantiated {prefabsToPrewarm[i].name} for shader compilation");
                    }
                }
            }

            // Wait 2 frames for shader compilation to complete
            yield return null;
            yield return null;

            // Clean up dummy objects
            foreach (var dummy in dummyObjects)
            {
                if (dummy != null)
                {
                    Destroy(dummy);
                }
            }

            if (showDebugLogs)
            {
                SystemDebug.Log(SystemDebug.Category.Performance, "[ShaderPrewarmer] Shader pre-warming complete. Shaders are compiled and cached.");
            }
        }

        /// <summary>
        /// Manually trigger shader pre-warming (useful for testing)
        /// </summary>
        public void PrewarmNow()
        {
            StartCoroutine(PrewarmShaders());
        }
    }
}
