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

            mesh.name = meshName;

            string assetPath = $"{MESHES_FOLDER}/{meshName}.asset";

            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existingMesh != null)
            {
                existingMesh.Clear();
                existingMesh.vertices = mesh.vertices;
                existingMesh.triangles = mesh.triangles;
                existingMesh.uv = mesh.uv;
                existingMesh.normals = mesh.normals;
                existingMesh.RecalculateBounds();
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
            UnityEngine.Debug.Log($"[HighResSphere] {meshName}: {vertexCount} vertices, {triangleCount} triangles");
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