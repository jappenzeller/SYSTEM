using UnityEngine;
using UnityEditor;
using System.IO;

namespace SYSTEM.Editor
{
    /// <summary>
    /// Editor helper for setting up world sphere prefabs
    /// </summary>
    public class WorldPrefabSetupEditor : EditorWindow
    {
        private GameObject worldSpherePrefab;
        private Material worldMaterial;
        private float worldRadius = 300f;
        private Color worldColor = new Color(0.2f, 0.3f, 0.5f);
        
        [MenuItem("SYSTEM/World Setup/Create World Sphere Prefab")]
        public static void ShowWindow()
        {
            GetWindow<WorldPrefabSetupEditor>("World Prefab Setup");
        }
        
        [MenuItem("SYSTEM/World Setup/Quick Create Default World Prefab")]
        public static void QuickCreateWorldPrefab()
        {
            CreateDefaultWorldPrefab();
        }
        
        void OnGUI()
        {
            GUILayout.Label("World Sphere Prefab Setup", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            EditorGUILayout.HelpBox(
                "This tool helps you create a WebGL-compatible world sphere prefab.\n" +
                "The prefab will use Unity's built-in sphere mesh for maximum compatibility.",
                MessageType.Info);
            
            GUILayout.Space(10);
            
            worldRadius = EditorGUILayout.FloatField("World Radius", worldRadius);
            worldColor = EditorGUILayout.ColorField("World Color", worldColor);
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Create World Sphere Prefab", GUILayout.Height(30)))
            {
                CreateWorldSpherePrefab();
            }
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("Create Default Material", GUILayout.Height(25)))
            {
                CreateDefaultMaterial();
            }
            
            if (GUILayout.Button("Create World Prefab Manager Asset", GUILayout.Height(25)))
            {
                CreateWorldPrefabManagerAsset();
            }
        }
        
        void CreateWorldSpherePrefab()
        {
            // Create the world sphere GameObject
            GameObject worldSphere = new GameObject("WorldSphere");
            
            // Add MeshFilter and set to Unity's built-in sphere
            MeshFilter meshFilter = worldSphere.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = GetUnitySphereMesh();
            
            // Add MeshRenderer
            MeshRenderer meshRenderer = worldSphere.AddComponent<MeshRenderer>();
            
            // Create or use material
            Material mat = GetOrCreateDefaultMaterial();
            meshRenderer.sharedMaterial = mat;
            
            // Add MeshCollider
            MeshCollider meshCollider = worldSphere.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = false; // Non-convex for accurate collision
            
            // Save as prefab
            string prefabPath = "Assets/Prefabs/WorldSphere.prefab";
            EnsureDirectoryExists("Assets/Prefabs");
            
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(worldSphere, prefabPath);
            
            // Clean up the scene object
            DestroyImmediate(worldSphere);
            
            // Select the created prefab
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            
            UnityEngine.Debug.Log($"[WorldPrefabSetup] Created world sphere prefab at: {prefabPath}");
            EditorUtility.DisplayDialog("Success", $"World sphere prefab created at:\n{prefabPath}", "OK");
        }
        
        static void CreateDefaultWorldPrefab()
        {
            // Create the world sphere GameObject
            GameObject worldSphere = new GameObject("WorldSphere");
            
            // Add MeshFilter and set to Unity's built-in sphere
            MeshFilter meshFilter = worldSphere.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = GetUnitySphereMesh();
            
            // Add MeshRenderer
            MeshRenderer meshRenderer = worldSphere.AddComponent<MeshRenderer>();
            
            // Create default material
            Material mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = new Color(0.2f, 0.3f, 0.5f);
            mat.name = "DefaultWorldMaterial";
            
            // Save material
            string materialPath = "Assets/Materials/DefaultWorldMaterial.mat";
            EnsureDirectoryExists("Assets/Materials");
            AssetDatabase.CreateAsset(mat, materialPath);
            
            meshRenderer.sharedMaterial = mat;
            
            // Add MeshCollider
            MeshCollider meshCollider = worldSphere.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = false;
            
            // Save as prefab
            string prefabPath = "Assets/Prefabs/WorldSphere.prefab";
            EnsureDirectoryExists("Assets/Prefabs");
            
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(worldSphere, prefabPath);
            
            // Clean up the scene object
            DestroyImmediate(worldSphere);
            
            // Select the created prefab
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            
            UnityEngine.Debug.Log($"[WorldPrefabSetup] Quick created world sphere prefab at: {prefabPath}");
        }
        
        void CreateDefaultMaterial()
        {
            Material mat = GetOrCreateDefaultMaterial();
            Selection.activeObject = mat;
            EditorGUIUtility.PingObject(mat);
            EditorUtility.DisplayDialog("Success", $"Default material created/updated", "OK");
        }
        
        Material GetOrCreateDefaultMaterial()
        {
            string materialPath = "Assets/Materials/DefaultWorldMaterial.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            
            if (mat == null)
            {
                EnsureDirectoryExists("Assets/Materials");
                
                // Create material with Unlit/Color shader for WebGL compatibility
                mat = new Material(Shader.Find("Unlit/Color"));
                if (mat.shader == null)
                {
                    // Fallback to standard shader
                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                }
                
                mat.color = worldColor;
                mat.name = "DefaultWorldMaterial";
                
                AssetDatabase.CreateAsset(mat, materialPath);
                AssetDatabase.SaveAssets();
                
                UnityEngine.Debug.Log($"[WorldPrefabSetup] Created material at: {materialPath}");
            }
            else
            {
                mat.color = worldColor;
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();
                UnityEngine.Debug.Log($"[WorldPrefabSetup] Updated existing material at: {materialPath}");
            }
            
            return mat;
        }
        
        void CreateWorldPrefabManagerAsset()
        {
            string assetPath = "Assets/ScriptableObjects/WorldPrefabManager.asset";
            EnsureDirectoryExists("Assets/ScriptableObjects");
            
            var manager = ScriptableObject.CreateInstance<Game.WorldPrefabManager>();
            manager.defaultWorldRadius = worldRadius;
            manager.defaultWorldColor = worldColor;
            
            // Try to auto-assign default prefab and material
            manager.defaultWorldSpherePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/WorldSphere.prefab");
            manager.defaultWorldMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DefaultWorldMaterial.mat");
            
            AssetDatabase.CreateAsset(manager, assetPath);
            AssetDatabase.SaveAssets();
            
            Selection.activeObject = manager;
            EditorGUIUtility.PingObject(manager);
            
            UnityEngine.Debug.Log($"[WorldPrefabSetup] Created WorldPrefabManager asset at: {assetPath}");
            EditorUtility.DisplayDialog("Success", $"WorldPrefabManager asset created at:\n{assetPath}", "OK");
        }
        
        static Mesh GetUnitySphereMesh()
        {
            // Get Unity's built-in sphere mesh
            GameObject tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh sphereMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(tempSphere);
            return sphereMesh;
        }
        
        static void EnsureDirectoryExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string[] folders = path.Split('/');
                string parentPath = folders[0];
                
                for (int i = 1; i < folders.Length; i++)
                {
                    string folderPath = parentPath + "/" + folders[i];
                    if (!AssetDatabase.IsValidFolder(folderPath))
                    {
                        AssetDatabase.CreateFolder(parentPath, folders[i]);
                    }
                    parentPath = folderPath;
                }
            }
        }
        
        [MenuItem("SYSTEM/World Setup/Test World Prefab in Scene")]
        public static void TestWorldPrefabInScene()
        {
            // Load the prefab
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/WorldSphere.prefab");
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Error", "WorldSphere prefab not found!\nPlease create it first.", "OK");
                return;
            }
            
            // Create a test world controller
            GameObject worldController = new GameObject("TestWorldController");
            var controller = worldController.AddComponent<Game.PrefabWorldController>();
            
            // Set the prefab reference (this will be done in inspector normally)
            SerializedObject so = new SerializedObject(controller);
            SerializedProperty prefabProp = so.FindProperty("worldSpherePrefab");
            if (prefabProp != null)
            {
                prefabProp.objectReferenceValue = prefab;
                so.ApplyModifiedProperties();
            }
            
            Selection.activeGameObject = worldController;
            SceneView.lastActiveSceneView.FrameSelected();
            
            UnityEngine.Debug.Log("[WorldPrefabSetup] Created test world controller in scene. Press Play to test!");
        }
    }
}