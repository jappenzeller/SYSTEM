using UnityEngine;
using SpacetimeDB.Types;

namespace SYSTEM.WavePacket
{
    /// <summary>
    /// Native (PC/Mac/Linux) renderer with high-quality procedural mesh generation
    /// Uses full resolution meshes and GPU instancing for flying packets
    /// </summary>
    public class WavePacketRenderer_Native : WavePacketRenderer
    {
        [Header("Native Settings")]
        [SerializeField] private Material flyingPacketMaterial;

        // Colors for flying packet visualization
        private Color colorRed = new Color(1f, 0f, 0f);
        private Color colorYellow = new Color(1f, 1f, 0f);
        private Color colorGreen = new Color(0f, 1f, 0f);
        private Color colorCyan = new Color(0f, 1f, 1f);
        private Color colorBlue = new Color(0f, 0f, 1f);
        private Color colorMagenta = new Color(1f, 0f, 1f);
        private Color colorGrey = new Color(0.5f, 0.5f, 0.5f);

        void Awake()
        {
            // Create default material for flying packets if not assigned
            if (flyingPacketMaterial == null)
            {
                Shader shader = Shader.Find("SYSTEM/WavePacketDisc");
                if (shader != null)
                {
                    flyingPacketMaterial = new Material(shader);
                    flyingPacketMaterial.SetFloat("_EmissionStrength", 1.0f);
                    flyingPacketMaterial.SetFloat("_Alpha", 1.0f);
                }
                else
                {
                    flyingPacketMaterial = new Material(Shader.Find("Unlit/Color"));
                }
            }
        }

        public override GameObject CreateFlyingPacket(WavePacketSample[] samples, Vector3 startPosition, Vector3 targetPosition, float speed)
        {
            // Create a simple glowing sphere with dominant color
            GameObject packet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            packet.transform.position = startPosition;
            packet.transform.localScale = Vector3.one * 0.5f;
            packet.name = "FlyingPacket";

            Color dominantColor = GetDominantColor(samples);

            // Set up material with vertex colors
            var renderer = packet.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(flyingPacketMaterial);
                renderer.material = mat;
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
