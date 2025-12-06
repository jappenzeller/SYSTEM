using UnityEngine;
using SpacetimeDB.Types;
using System.Collections.Generic;
using System.Diagnostics;

namespace SYSTEM.WavePacket
{
    public static class WavePacketMeshGenerator
    {
        public static Mesh GenerateWavePacketMesh(WavePacketSample[] samples, WavePacketSettings settings, float progress = 1.0f)
        {
            Stopwatch totalTimer = Stopwatch.StartNew();
            uint totalPackets = 0;
            if (samples != null)
                foreach (var sample in samples)
                    totalPackets += sample.Count;
            UnityEngine.Debug.Log($"[MeshGen] GenerateWavePacketMesh: {samples?.Length ?? 0} samples, {totalPackets} total packets");

            if (settings == null)
            {
                UnityEngine.Debug.LogError("WavePacketSettings is null!");
                return null;
            }

            int resolution = settings.GetMeshResolution();
            float maxRadius = settings.discRadius * progress;

            Mesh mesh = new Mesh();
            mesh.name = "WavePacketDisc";

            List<Vector3> vertices = new List<Vector3>();
            List<Color> colors = new List<Color>();
            List<Vector3> normals = new List<Vector3>();
            List<int> triangles = new List<int>();

            int gridSize = resolution + 1;

            Stopwatch vertexTimer = Stopwatch.StartNew();

            // Generate vertices for top face
            for (int z = 0; z <= resolution; z++)
            {
                for (int x = 0; x <= resolution; x++)
                {
                    float u = (x / (float)resolution) * 2f - 1f;
                    float v = (z / (float)resolution) * 2f - 1f;
                    float radius = Mathf.Sqrt(u * u + v * v);

                    if (radius > maxRadius)
                    {
                        vertices.Add(new Vector3(u * maxRadius, 0, v * maxRadius));
                        colors.Add(new Color(0, 0, 0, 0));
                        normals.Add(Vector3.up);
                    }
                    else
                    {
                        float height = CalculateHeightAtRadius(radius, samples, settings);
                        Color vertexColor = CalculateColorAtRadius(radius, samples, settings);

                        vertices.Add(new Vector3(u * maxRadius, height, v * maxRadius));
                        colors.Add(vertexColor);
                        normals.Add(Vector3.up);
                    }
                }
            }

            // Generate vertices for bottom face (mirrored in -Y)
            int bottomOffset = vertices.Count;
            for (int z = 0; z <= resolution; z++)
            {
                for (int x = 0; x <= resolution; x++)
                {
                    float u = (x / (float)resolution) * 2f - 1f;
                    float v = (z / (float)resolution) * 2f - 1f;
                    float radius = Mathf.Sqrt(u * u + v * v);

                    if (radius > maxRadius)
                    {
                        vertices.Add(new Vector3(u * maxRadius, 0, v * maxRadius));
                        colors.Add(new Color(0, 0, 0, 0));
                        normals.Add(Vector3.down);
                    }
                    else
                    {
                        float height = CalculateHeightAtRadius(radius, samples, settings);
                        Color vertexColor = CalculateColorAtRadius(radius, samples, settings);

                        vertices.Add(new Vector3(u * maxRadius, -height, v * maxRadius));
                        colors.Add(vertexColor);
                        normals.Add(Vector3.down);
                    }
                }
            }

            vertexTimer.Stop();
            Stopwatch triangleTimer = Stopwatch.StartNew();

            // Generate triangles for top face
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int i = z * gridSize + x;

                    triangles.Add(i);
                    triangles.Add(i + gridSize);
                    triangles.Add(i + 1);

                    triangles.Add(i + 1);
                    triangles.Add(i + gridSize);
                    triangles.Add(i + gridSize + 1);
                }
            }

            // Generate triangles for bottom face (reversed winding)
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int i = bottomOffset + z * gridSize + x;

                    triangles.Add(i);
                    triangles.Add(i + 1);
                    triangles.Add(i + gridSize);

                    triangles.Add(i + 1);
                    triangles.Add(i + gridSize + 1);
                    triangles.Add(i + gridSize);
                }
            }

            triangleTimer.Stop();
            Stopwatch setDataTimer = Stopwatch.StartNew();

            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);

            setDataTimer.Stop();
            totalTimer.Stop();

            UnityEngine.Debug.Log($"[MeshGen] Total: {totalTimer.ElapsedMilliseconds}ms | Vertices: {vertexTimer.ElapsedMilliseconds}ms | Triangles: {triangleTimer.ElapsedMilliseconds}ms | SetData: {setDataTimer.ElapsedMilliseconds}ms | Resolution: {resolution}");

            return mesh;
        }

        private static float CalculateHeightAtRadius(float radius, WavePacketSample[] samples, WavePacketSettings settings)
        {
            float height = 0f;

            foreach (var sample in samples)
            {
                // Skip samples with no packets
                if (sample.Count == 0)
                    continue;

                int ringIndex = settings.GetRingIndexForFrequency(sample.Frequency);
                if (ringIndex < 0 || ringIndex >= settings.ringRadii.Length)
                    continue;

                float ringRadius = settings.ringRadii[ringIndex];
                float distanceFromRing = radius - ringRadius;

                float gaussian = Mathf.Exp(-(distanceFromRing * distanceFromRing) / (2f * settings.ringWidth * settings.ringWidth));
                height += sample.Count * settings.heightScale * gaussian;
            }

            return height;
        }

        private static Color CalculateColorAtRadius(float radius, WavePacketSample[] samples, WavePacketSettings settings)
        {
            if (samples == null || samples.Length == 0)
                return new Color(0, 0, 0, 0);

            float closestDistance = float.MaxValue;
            Color closestColor = new Color(0, 0, 0, 0);
            float closestContribution = 0f;

            foreach (var sample in samples)
            {
                // Skip samples with no packets
                if (sample.Count == 0)
                    continue;

                int ringIndex = settings.GetRingIndexForFrequency(sample.Frequency);
                if (ringIndex < 0 || ringIndex >= settings.ringRadii.Length)
                    continue;

                float ringRadius = settings.ringRadii[ringIndex];
                float distanceFromRing = Mathf.Abs(radius - ringRadius);

                if (distanceFromRing < closestDistance)
                {
                    closestDistance = distanceFromRing;
                    closestColor = settings.GetColorForFrequency(sample.Frequency);

                    float gaussian = Mathf.Exp(-(distanceFromRing * distanceFromRing) / (2f * settings.ringWidth * settings.ringWidth));
                    closestContribution = sample.Count * gaussian;
                }
            }

            float brightness = Mathf.Clamp01(closestContribution / 5f);
            brightness = Mathf.Max(0.5f, brightness);

            return closestColor * brightness;
        }
    }
}
