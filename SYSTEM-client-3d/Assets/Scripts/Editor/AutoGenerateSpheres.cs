using UnityEngine;
using UnityEditor;

namespace SYSTEM.Editor
{
    [InitializeOnLoad]
    public static class AutoGenerateSpheres
    {
        private const string PREF_KEY = "HighResSphereGenerated";

        static AutoGenerateSpheres()
        {
            // Disabled auto-generation - use manual menu item instead
            // EditorApplication.delayCall += GenerateSpheresOnce;
        }

        private static void GenerateSpheresOnce()
        {
            if (EditorPrefs.GetBool(PREF_KEY, false))
            {
                return;
            }

            UnityEngine.Debug.Log("[AutoGenerate] Generating high-res sphere meshes...");
            HighResSphereCreator.CreateAllLODs();
            EditorPrefs.SetBool(PREF_KEY, true);

            EditorApplication.delayCall += UpdateWorldPrefab;
        }

        private static void UpdateWorldPrefab()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab CenterWorld");

            if (prefabGuids.Length == 0)
            {
                UnityEngine.Debug.LogWarning("[AutoGenerate] CenterWorld prefab not found");
                return;
            }

            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[0]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                UnityEngine.Debug.LogWarning("[AutoGenerate] Failed to load CenterWorld prefab");
                return;
            }

            MeshFilter meshFilter = prefab.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                UnityEngine.Debug.LogWarning("[AutoGenerate] CenterWorld prefab has no MeshFilter");
                return;
            }

            Mesh highResMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Meshes/HighResSphere_LOD1.asset");
            if (highResMesh == null)
            {
                UnityEngine.Debug.LogWarning("[AutoGenerate] HighResSphere_LOD1 mesh not found");
                return;
            }

            string prefabAssetPath = AssetDatabase.GetAssetPath(prefab);
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabAssetPath);

            MeshFilter prefabMeshFilter = prefabContents.GetComponent<MeshFilter>();
            if (prefabMeshFilter != null)
            {
                prefabMeshFilter.sharedMesh = highResMesh;
                PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabAssetPath);
                UnityEngine.Debug.Log($"[AutoGenerate] Updated CenterWorld prefab mesh to HighResSphere_LOD1");
            }

            PrefabUtility.UnloadPrefabContents(prefabContents);
        }

        [MenuItem("SYSTEM/Reset High-Res Sphere Generation")]
        private static void ResetGeneration()
        {
            EditorPrefs.DeleteKey(PREF_KEY);
            UnityEngine.Debug.Log("[AutoGenerate] Reset complete. Meshes will regenerate on next script reload.");
        }
    }
}