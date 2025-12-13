using UnityEngine;
using SpacetimeDB.Types;

namespace SYSTEM.WavePacket
{
    /// <summary>
    /// WebGL-optimized renderer with lower vertex counts and simplified rendering
    /// Uses pre-made ring prefabs scaled instead of procedural mesh generation
    /// </summary>
    public class WavePacketRenderer_WebGL : WavePacketRenderer
    {
        [Header("WebGL Settings")]
        [SerializeField] private GameObject flyingPacketPrefab;

        // Colors for flying packet visualization
        private Color colorRed = new Color(1f, 0f, 0f);
        private Color colorYellow = new Color(1f, 1f, 0f);
        private Color colorGreen = new Color(0f, 1f, 0f);
        private Color colorCyan = new Color(0f, 1f, 1f);
        private Color colorBlue = new Color(0f, 0f, 1f);
        private Color colorMagenta = new Color(1f, 0f, 1f);
        private Color colorGrey = new Color(0.5f, 0.5f, 0.5f);

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
    }
}
