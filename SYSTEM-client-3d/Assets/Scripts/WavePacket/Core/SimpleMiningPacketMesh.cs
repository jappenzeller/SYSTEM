using UnityEngine;
using SpacetimeDB.Types;

namespace SYSTEM.WavePacket
{
    /// <summary>
    /// Optimized mesh generator for mining packets
    /// Uses low resolution and simple sphere approximation for performance
    /// </summary>
    public static class SimpleMiningPacketMesh
    {
        private static Mesh cachedLowResMesh;

        /// <summary>
        /// Get a simple low-res sphere mesh for mining packets (cached)
        /// Much faster than full wave packet generation
        /// </summary>
        public static Mesh GetSimpleSphere(float radius = 0.5f)
        {
            if (cachedLowResMesh == null)
            {
                cachedLowResMesh = CreateSimpleSphere(8); // 8 segments = very low poly
            }

            return cachedLowResMesh;
        }

        /// <summary>
        /// Create a simple UV sphere mesh
        /// </summary>
        private static Mesh CreateSimpleSphere(int segments)
        {
            Mesh mesh = new Mesh();
            mesh.name = "SimpleMiningPacket";

            int rings = segments;
            int slices = segments * 2;

            // Calculate vertex count
            int vertexCount = (rings - 1) * slices + 2; // middle rings + top/bottom poles

            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Color[] colors = new Color[vertexCount];

            int vertIndex = 0;

            // Top pole
            vertices[vertIndex] = Vector3.up;
            normals[vertIndex] = Vector3.up;
            colors[vertIndex] = Color.white;
            vertIndex++;

            // Middle rings
            for (int r = 1; r < rings; r++)
            {
                float phi = Mathf.PI * r / rings;
                float y = Mathf.Cos(phi);
                float ringRadius = Mathf.Sin(phi);

                for (int s = 0; s < slices; s++)
                {
                    float theta = 2f * Mathf.PI * s / slices;
                    float x = ringRadius * Mathf.Cos(theta);
                    float z = ringRadius * Mathf.Sin(theta);

                    Vector3 position = new Vector3(x, y, z);
                    vertices[vertIndex] = position;
                    normals[vertIndex] = position.normalized;
                    colors[vertIndex] = Color.white;
                    vertIndex++;
                }
            }

            // Bottom pole
            vertices[vertIndex] = Vector3.down;
            normals[vertIndex] = Vector3.down;
            colors[vertIndex] = Color.white;

            // Generate triangles
            int[] triangles = new int[(rings - 1) * slices * 6];
            int triIndex = 0;

            // Top cap
            for (int s = 0; s < slices; s++)
            {
                int next = (s + 1) % slices;
                triangles[triIndex++] = 0; // top pole
                triangles[triIndex++] = s + 1;
                triangles[triIndex++] = next + 1;
            }

            // Middle quads
            for (int r = 0; r < rings - 2; r++)
            {
                int ringStart = 1 + r * slices;
                int nextRingStart = 1 + (r + 1) * slices;

                for (int s = 0; s < slices; s++)
                {
                    int next = (s + 1) % slices;

                    int a = ringStart + s;
                    int b = ringStart + next;
                    int c = nextRingStart + next;
                    int d = nextRingStart + s;

                    triangles[triIndex++] = a;
                    triangles[triIndex++] = b;
                    triangles[triIndex++] = c;

                    triangles[triIndex++] = a;
                    triangles[triIndex++] = c;
                    triangles[triIndex++] = d;
                }
            }

            // Bottom cap
            int bottomPole = vertexCount - 1;
            int lastRingStart = 1 + (rings - 2) * slices;

            for (int s = 0; s < slices; s++)
            {
                int next = (s + 1) % slices;
                triangles[triIndex++] = bottomPole;
                triangles[triIndex++] = lastRingStart + next;
                triangles[triIndex++] = lastRingStart + s;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.triangles = triangles;

            return mesh;
        }

        /// <summary>
        /// Clear cached mesh (call when changing settings)
        /// </summary>
        public static void ClearCache()
        {
            cachedLowResMesh = null;
        }
    }
}
