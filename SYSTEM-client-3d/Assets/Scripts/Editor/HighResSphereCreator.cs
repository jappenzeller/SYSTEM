using UnityEngine;
using UnityEditor;
using System.IO;

namespace SYSTEM.Editor
{
    public static class HighResSphereCreator
    {
        private const string MESHES_FOLDER = "Assets/Meshes";

        [MenuItem("SYSTEM/Create High-Res Sphere Mesh/LOD 0 (20k triangles)")]
        public static void CreateLOD0()
        {
            CreateHighResSphere("HighResSphere_LOD0", 1f, 5);
        }

        [MenuItem("SYSTEM/Create High-Res Sphere Mesh/LOD 1 (5k triangles)")]
        public static void CreateLOD1()
        {
            CreateHighResSphere("HighResSphere_LOD1", 1f, 4);
        }

        [MenuItem("SYSTEM/Create High-Res Sphere Mesh/LOD 2 (1k triangles)")]
        public static void CreateLOD2()
        {
            CreateHighResSphere("HighResSphere_LOD2", 1f, 3);
        }

        [MenuItem("SYSTEM/Create High-Res Sphere Mesh/All LOD Levels")]
        public static void CreateAllLODs()
        {
            CreateLOD0();
            CreateLOD1();
            CreateLOD2();
            UnityEngine.Debug.Log("[HighResSphere] Created all LOD levels");
        }

        [MenuItem("SYSTEM/Create High-Res Sphere Mesh/Custom...")]
        public static void CreateCustom()
        {
            CustomSphereWindow.ShowWindow();
        }

        private static void CreateHighResSphere(string meshName, float radius, int subdivisions)
        {
            EnsureMeshesFolderExists();

#pragma warning disable CS0618
            Mesh mesh = ProceduralSphereGenerator.GenerateIcosphere(radius, subdivisions, false);
#pragma warning restore CS0618

            // Additional normalization pass to ensure perfect radius
            Vector3[] vertices = mesh.vertices;
            float maxRadius = 0f;
            float minRadius = float.MaxValue;

            // First pass: measure actual radius
            for (int i = 0; i < vertices.Length; i++)
            {
                float dist = vertices[i].magnitude;
                maxRadius = Mathf.Max(maxRadius, dist);
                minRadius = Mathf.Min(minRadius, dist);
            }

            UnityEngine.Debug.Log($"[HighResSphere] Before normalization - Min: {minRadius:F6}, Max: {maxRadius:F6}");

            // Second pass: normalize all vertices to exactly radius
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = vertices[i].normalized * radius;
            }

            // Verify normalization
            maxRadius = 0f;
            minRadius = float.MaxValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                float dist = vertices[i].magnitude;
                maxRadius = Mathf.Max(maxRadius, dist);
                minRadius = Mathf.Min(minRadius, dist);
            }

            UnityEngine.Debug.Log($"[HighResSphere] After normalization - Min: {minRadius:F6}, Max: {maxRadius:F6}");

            mesh.name = meshName;
            mesh.vertices = vertices;

            // Explicitly set bounds for radius = 1.0 sphere
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (radius * 2f));

            string assetPath = $"{MESHES_FOLDER}/{meshName}.asset";

            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existingMesh != null)
            {
                existingMesh.Clear();
                existingMesh.vertices = mesh.vertices;
                existingMesh.triangles = mesh.triangles;
                existingMesh.uv = mesh.uv;
                existingMesh.normals = mesh.normals;
                existingMesh.bounds = mesh.bounds;
                existingMesh.RecalculateTangents();
                existingMesh.Optimize();
                EditorUtility.SetDirty(existingMesh);
                UnityEngine.Debug.Log($"[HighResSphere] Updated existing mesh: {assetPath}");
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, assetPath);
                UnityEngine.Debug.Log($"[HighResSphere] Created new mesh: {assetPath}");
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);

            int vertexCount = mesh.vertexCount;
            int triangleCount = mesh.triangles.Length / 3;
            UnityEngine.Debug.Log($"[HighResSphere] {meshName}: {vertexCount} vertices, {triangleCount} triangles, Bounds: {mesh.bounds.size}");
        }

        private static void EnsureMeshesFolderExists()
        {
            if (!AssetDatabase.IsValidFolder(MESHES_FOLDER))
            {
                string parentFolder = Path.GetDirectoryName(MESHES_FOLDER).Replace("\\", "/");
                string folderName = Path.GetFileName(MESHES_FOLDER);
                AssetDatabase.CreateFolder(parentFolder, folderName);
                UnityEngine.Debug.Log($"[HighResSphere] Created folder: {MESHES_FOLDER}");
            }
        }

        public class CustomSphereWindow : EditorWindow
        {
            private string meshName = "CustomSphere";
            private float radius = 1f;
            private int subdivisions = 4;

            public static void ShowWindow()
            {
                GetWindow<CustomSphereWindow>("Create Custom Sphere");
            }

            private void OnGUI()
            {
                GUILayout.Label("Custom Sphere Settings", EditorStyles.boldLabel);

                meshName = EditorGUILayout.TextField("Mesh Name", meshName);
                radius = EditorGUILayout.FloatField("Radius", radius);
                subdivisions = EditorGUILayout.IntSlider("Subdivisions (0-6)", subdivisions, 0, 6);

                int estimatedTriangles = CalculateTriangleCount(subdivisions);
                EditorGUILayout.LabelField("Estimated Triangles", estimatedTriangles.ToString("N0"));

                GUILayout.Space(10);

                if (GUILayout.Button("Create Mesh"))
                {
                    CreateHighResSphere(meshName, radius, subdivisions);
                    Close();
                }
            }

            private int CalculateTriangleCount(int subdivisions)
            {
                return 20 * (int)Mathf.Pow(4, subdivisions);
            }
        }
    }
}