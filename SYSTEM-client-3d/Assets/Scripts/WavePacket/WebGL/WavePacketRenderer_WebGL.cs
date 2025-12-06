using UnityEngine;
using SpacetimeDB.Types;
using System.Collections.Generic;

namespace SYSTEM.WavePacket
{
    /// <summary>
    /// WebGL-optimized renderer with lower vertex counts and simplified rendering
    /// Uses pre-made ring prefabs scaled instead of procedural mesh generation
    /// </summary>
    public class WavePacketRenderer_WebGL : WavePacketRenderer
    {
        [Header("WebGL Settings")]
        [SerializeField] private int meshResolution = 32; // Lower resolution for WebGL (32x32)
        [SerializeField] private Material extractionMaterial;
        [SerializeField] private GameObject flyingPacketPrefab;

        private GameObject extractionObject;
        private MeshFilter extractionMeshFilter;
        private MeshRenderer extractionMeshRenderer;
        private Mesh extractionMesh;

        private WavePacketSample[] currentSamples;
        private Vector3 extractionPosition;
        private float extractionStartTime;

        // Extraction state
        private bool isExtracting = false;
        private float extractionProgress = 0f;

        // Configuration (could be moved to settings)
        private float rotationSpeed = 90f; // degrees per second
        private float extractionDuration = 2f; // seconds
        private float extractionDiscRadius = 20f; // units

        // Ring Configuration
        private float[] ringRadii = new float[] { 15f, 12.5f, 10f, 7.5f, 5f, 2.5f };
        private float ringWidth = 0.6f;
        private float heightScale = 0.125f;

        // Colors
        private Color colorRed = new Color(1f, 0f, 0f);
        private Color colorYellow = new Color(1f, 1f, 0f);
        private Color colorGreen = new Color(0f, 1f, 0f);
        private Color colorCyan = new Color(0f, 1f, 1f);
        private Color colorBlue = new Color(0f, 0f, 1f);
        private Color colorMagenta = new Color(1f, 0f, 1f);
        private Color colorGrey = new Color(0.5f, 0.5f, 0.5f);

        void Awake()
        {
            // Create material with WavePacketDisc shader
            if (extractionMaterial == null)
            {
                Shader shader = Shader.Find("SYSTEM/WavePacketDisc");
                if (shader != null)
                {
                    extractionMaterial = new Material(shader);
                    extractionMaterial.SetFloat("_EmissionStrength", 0.5f);
                    extractionMaterial.SetFloat("_Alpha", 1.0f);
                    UnityEngine.Debug.Log("[WavePacketRenderer_WebGL] Created extraction material with WavePacketDisc shader");
                }
                else
                {
                    UnityEngine.Debug.LogError("[WavePacketRenderer_WebGL] Could not find SYSTEM/WavePacketDisc shader!");
                    extractionMaterial = new Material(Shader.Find("Unlit/Color"));
                }
            }
        }

        public override GameObject CreateFlyingPacket(WavePacketSample[] samples, Vector3 startPosition, Vector3 targetPosition, float speed)
        {
            // For WebGL, use simple cubes (cheaper than spheres)
            GameObject packet;

            if (flyingPacketPrefab != null)
            {
                packet = Instantiate(flyingPacketPrefab, startPosition, Quaternion.identity);
            }
            else
            {
                // Fallback: simple cube (cheaper than sphere on WebGL)
                packet = GameObject.CreatePrimitive(PrimitiveType.Cube);
                packet.transform.position = startPosition;
                packet.transform.localScale = Vector3.one * 0.3f;
            }

            packet.name = "FlyingPacket_WebGL";

            Color dominantColor = GetDominantColor(samples);

            var renderer = packet.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("SYSTEM/WavePacketDisc");
                Material mat = shader != null ? new Material(shader) : new Material(Shader.Find("Unlit/Color"));
                renderer.material = mat;

                // Disable shadows for performance
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            // Apply vertex colors to mesh
            var meshFilter = packet.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.mesh != null)
            {
                Mesh mesh = meshFilter.mesh;
                Color[] colors = new Color[mesh.vertexCount];
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = dominantColor;
                }
                mesh.colors = colors;
            }

            // Add trajectory animation component
            var flyingPacket = packet.AddComponent<FlyingPacket>();
            flyingPacket.targetPosition = targetPosition;
            flyingPacket.speed = speed;

            return packet;
        }

        public override void StartExtraction(WavePacketSample[] samples, Vector3 orbPosition)
        {
            if (isExtracting)
            {
                EndExtraction();
            }

            currentSamples = samples;
            extractionPosition = orbPosition;
            extractionStartTime = Time.time;
            isExtracting = true;
            extractionProgress = 0f;

            // Create extraction visualization object
            extractionObject = new GameObject("ExtractionDisc_WebGL");
            extractionObject.transform.position = orbPosition;

            extractionMeshFilter = extractionObject.AddComponent<MeshFilter>();
            extractionMeshRenderer = extractionObject.AddComponent<MeshRenderer>();
            extractionMeshRenderer.material = extractionMaterial;

            // Disable shadows for WebGL performance
            extractionMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            extractionMeshRenderer.receiveShadows = false;

            // Generate simplified disc mesh
            GenerateSimplifiedMesh();
        }

        public override void UpdateExtraction(float progress)
        {
            if (!isExtracting || extractionObject == null)
                return;

            extractionProgress = Mathf.Clamp01(progress);

            // Rotate the disc
            float rotation = rotationSpeed * (Time.time - extractionStartTime);
            extractionObject.transform.rotation = Quaternion.Euler(0, rotation, 0);

            // Scale animation
            float scale = Mathf.Lerp(0.1f, 1f, extractionProgress);
            extractionObject.transform.localScale = Vector3.one * scale;

            // Fade out near completion
            if (extractionProgress > 0.9f)
            {
                float alpha = Mathf.Lerp(1f, 0f, (extractionProgress - 0.9f) / 0.1f);
                Color matColor = extractionMaterial.color;
                matColor.a = alpha;
                extractionMaterial.color = matColor;
            }
        }

        public override void EndExtraction()
        {
            if (extractionObject != null)
            {
                Destroy(extractionObject);
                extractionObject = null;
            }

            if (extractionMesh != null)
            {
                Destroy(extractionMesh);
                extractionMesh = null;
            }

            isExtracting = false;
            extractionProgress = 0f;
        }

        void Update()
        {
            if (isExtracting)
            {
                float elapsed = Time.time - extractionStartTime;
                float autoProgress = elapsed / extractionDuration;
                UpdateExtraction(autoProgress);

                if (autoProgress >= 1f)
                {
                    EndExtraction();
                }
            }
        }

        /// <summary>
        /// Generate simplified mesh for WebGL (lower vertex count)
        /// </summary>
        private void GenerateSimplifiedMesh()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Color> colors = new List<Color>();

            int resolution = meshResolution; // 32x32 for WebGL
            float maxRadius = extractionDiscRadius;

            // Generate top surface (same algorithm as native but lower res)
            for (int y = 0; y <= resolution; y++)
            {
                for (int x = 0; x <= resolution; x++)
                {
                    float u = (x / (float)resolution) * 2f - 1f;
                    float v = (y / (float)resolution) * 2f - 1f;

                    float radius = Mathf.Sqrt(u * u + v * v) * maxRadius;

                    if (radius > maxRadius)
                    {
                        vertices.Add(new Vector3(u * maxRadius, 0, v * maxRadius));
                        // Fully transparent - no color data at all
                        colors.Add(new Color(0, 0, 0, 0));
                    }
                    else
                    {
                        // Calculate height and color based on gaussian rings
                        float height = CalculateHeightAtRadius(radius, currentSamples);
                        Color vertexColor = CalculateColorAtRadius(radius, currentSamples);

                        // Fade alpha smoothly - start fading earlier for cleaner edge
                        float fadeStart = maxRadius * 0.85f; // Start fading at 85% of max radius (17 units)
                        if (radius > fadeStart)
                        {
                            float fadeAmount = (radius - fadeStart) / (maxRadius - fadeStart);
                            vertexColor.a = 1f - fadeAmount;
                        }
                        else
                        {
                            vertexColor.a = 1f;
                        }

                        vertices.Add(new Vector3(u * maxRadius, height, v * maxRadius));
                        colors.Add(vertexColor);
                    }
                }
            }

            // Generate bottom surface (mirror)
            int topVertexCount = vertices.Count;
            for (int i = 0; i < topVertexCount; i++)
            {
                Vector3 topVertex = vertices[i];
                vertices.Add(new Vector3(topVertex.x, -topVertex.y, topVertex.z));
                colors.Add(colors[i]);
            }

            // Generate triangles for top
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int i = y * (resolution + 1) + x;

                    triangles.Add(i);
                    triangles.Add(i + resolution + 1);
                    triangles.Add(i + 1);

                    triangles.Add(i + 1);
                    triangles.Add(i + resolution + 1);
                    triangles.Add(i + resolution + 2);
                }
            }

            // Generate triangles for bottom (reversed winding)
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int i = topVertexCount + y * (resolution + 1) + x;

                    triangles.Add(i);
                    triangles.Add(i + 1);
                    triangles.Add(i + resolution + 1);

                    triangles.Add(i + 1);
                    triangles.Add(i + resolution + 2);
                    triangles.Add(i + resolution + 1);
                }
            }

            // Create mesh (WebGL optimized)
            extractionMesh = new Mesh();
            extractionMesh.name = "ExtractionDisc_WebGL";
            extractionMesh.vertices = vertices.ToArray();
            extractionMesh.triangles = triangles.ToArray();
            extractionMesh.colors = colors.ToArray();
            extractionMesh.RecalculateNormals();

            // Optimize mesh for WebGL
            extractionMesh.Optimize();

            extractionMeshFilter.mesh = extractionMesh;

            UnityEngine.Debug.Log($"[WebGL Renderer] Generated mesh with {vertices.Count} vertices, {triangles.Count / 3} triangles");
        }
        private Color GetColorForFrequency(float frequency)
        {
            if (Mathf.Abs(frequency - 0.0f) < 0.1f) return colorRed;
            if (Mathf.Abs(frequency - 1.047f) < 0.1f) return colorYellow;
            if (Mathf.Abs(frequency - 2.094f) < 0.1f) return colorGreen;
            if (Mathf.Abs(frequency - 3.142f) < 0.1f) return colorCyan;
            if (Mathf.Abs(frequency - 4.189f) < 0.1f) return colorBlue;
            if (Mathf.Abs(frequency - 5.236f) < 0.1f) return colorMagenta;
            return colorGrey;
        }

        private int GetRingIndexForFrequency(float frequency)
        {
            if (Mathf.Abs(frequency - 0.0f) < 0.1f) return 0;
            if (Mathf.Abs(frequency - 1.047f) < 0.1f) return 1;
            if (Mathf.Abs(frequency - 2.094f) < 0.1f) return 2;
            if (Mathf.Abs(frequency - 3.142f) < 0.1f) return 3;
            if (Mathf.Abs(frequency - 4.189f) < 0.1f) return 4;
            if (Mathf.Abs(frequency - 5.236f) < 0.1f) return 5;
            return -1;
        }

        private Color GetDominantColor(WavePacketSample[] samples)
        {
            if (samples == null || samples.Length == 0) return colorGrey;
            Color result = Color.black;
            float totalWeight = 0f;
            foreach (var sample in samples)
            {
                Color sampleColor = GetColorForFrequency(sample.Frequency);
                float weight = sample.Count;
                result += sampleColor * weight;
                totalWeight += weight;
            }
            if (totalWeight > 0) result /= totalWeight;
            return result;
        }

        private float CalculateHeightAtRadius(float radius, WavePacketSample[] samples)
        {
            float height = 0f;
            foreach (var sample in samples)
            {
                int ringIndex = GetRingIndexForFrequency(sample.Frequency);
                if (ringIndex < 0 || ringIndex >= ringRadii.Length) continue;
                float ringRadius = ringRadii[ringIndex];
                float distanceFromRing = radius - ringRadius;
                float gaussian = Mathf.Exp(-(distanceFromRing * distanceFromRing) / (2f * ringWidth * ringWidth));
                height += sample.Count * heightScale * gaussian;
            }
            return height;
        }

        private Color CalculateColorAtRadius(float radius, WavePacketSample[] samples)
        {
            if (samples == null || samples.Length == 0) return new Color(0, 0, 0, 0);
            float closestDistance = float.MaxValue;
            Color closestColor = new Color(0, 0, 0, 0);
            float closestContribution = 0f;
            foreach (var sample in samples)
            {
                int ringIndex = GetRingIndexForFrequency(sample.Frequency);
                if (ringIndex < 0 || ringIndex >= ringRadii.Length) continue;
                float ringRadius = ringRadii[ringIndex];
                float distanceFromRing = Mathf.Abs(radius - ringRadius);
                if (distanceFromRing < closestDistance)
                {
                    closestDistance = distanceFromRing;
                    closestColor = GetColorForFrequency(sample.Frequency);
                    float gaussian = Mathf.Exp(-(distanceFromRing * distanceFromRing) / (2f * ringWidth * ringWidth));
                    closestContribution = sample.Count * gaussian;
                }
            }
            float brightness = Mathf.Clamp01(closestContribution / 5f);
            brightness = Mathf.Max(0.5f, brightness);
            return closestColor * brightness;
        }


        void OnDestroy()
        {
            EndExtraction();
        }
    }
}
