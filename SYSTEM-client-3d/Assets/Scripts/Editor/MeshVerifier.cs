using UnityEngine;
using UnityEditor;

namespace SYSTEM.Editor
{
    public static class MeshVerifier
    {
        [MenuItem("SYSTEM/Verify High-Res Sphere Meshes")]
        public static void VerifyMeshes()
        {
            string[] meshPaths = new string[]
            {
                "Assets/Meshes/HighResSphere_LOD0.asset",
                "Assets/Meshes/HighResSphere_LOD1.asset",
                "Assets/Meshes/HighResSphere_LOD2.asset"
            };

            UnityEngine.Debug.Log("========== MESH VERIFICATION ==========");

            foreach (string path in meshPaths)
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                {
                    UnityEngine.Debug.LogError($"[MeshVerifier] Mesh not found: {path}");
                    continue;
                }

                VerifyMesh(mesh, path);
            }

            UnityEngine.Debug.Log("========== VERIFICATION COMPLETE ==========");
        }

        private static void VerifyMesh(Mesh mesh, string path)
        {
            UnityEngine.Debug.Log($"\n[MeshVerifier] Analyzing: {path}");
            UnityEngine.Debug.Log($"  Name: {mesh.name}");
            UnityEngine.Debug.Log($"  Vertices: {mesh.vertexCount}");
            UnityEngine.Debug.Log($"  Triangles: {mesh.triangles.Length / 3}");

            // Check vertex radius
            Vector3[] vertices = mesh.vertices;
            float minRadius = float.MaxValue;
            float maxRadius = 0f;
            float sumRadius = 0f;

            foreach (Vector3 vertex in vertices)
            {
                float radius = vertex.magnitude;
                minRadius = Mathf.Min(minRadius, radius);
                maxRadius = Mathf.Max(maxRadius, radius);
                sumRadius += radius;
            }

            float avgRadius = sumRadius / vertices.Length;
            float radiusVariance = maxRadius - minRadius;

            UnityEngine.Debug.Log($"  Min Radius: {minRadius:F8}");
            UnityEngine.Debug.Log($"  Max Radius: {maxRadius:F8}");
            UnityEngine.Debug.Log($"  Avg Radius: {avgRadius:F8}");
            UnityEngine.Debug.Log($"  Variance: {radiusVariance:F8}");

            // Check bounds
            Bounds bounds = mesh.bounds;
            UnityEngine.Debug.Log($"  Bounds Center: {bounds.center}");
            UnityEngine.Debug.Log($"  Bounds Size: {bounds.size}");
            UnityEngine.Debug.Log($"  Bounds Extents: {bounds.extents}");

            // Validate
            if (Mathf.Abs(avgRadius - 1.0f) < 0.0001f && radiusVariance < 0.0001f)
            {
                UnityEngine.Debug.Log($"  ✅ PASS: Mesh has perfectly normalized vertices at radius 1.0");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"  ⚠️ FAIL: Mesh vertices are not normalized to radius 1.0");
                UnityEngine.Debug.LogWarning($"     Expected radius: 1.0, Got: {avgRadius:F8} (variance: {radiusVariance:F8})");
                UnityEngine.Debug.LogWarning($"     REGENERATE MESH: SYSTEM → Create High-Res Sphere Mesh");
            }

            // Check bounds
            Vector3 expectedSize = Vector3.one * 2f; // Diameter = 2 for radius 1
            float boundsError = (bounds.size - expectedSize).magnitude;
            if (boundsError < 0.01f)
            {
                UnityEngine.Debug.Log($"  ✅ PASS: Bounds are correct (size: {bounds.size})");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"  ⚠️ FAIL: Bounds incorrect. Expected: {expectedSize}, Got: {bounds.size}");
            }
        }

        [MenuItem("SYSTEM/Regenerate All High-Res Sphere Meshes")]
        public static void RegenerateAllMeshes()
        {
            UnityEngine.Debug.Log("[MeshVerifier] Regenerating all high-res sphere meshes...");
            HighResSphereCreator.CreateAllLODs();
            UnityEngine.Debug.Log("[MeshVerifier] Regeneration complete. Running verification...");
            VerifyMeshes();
        }
    }
}