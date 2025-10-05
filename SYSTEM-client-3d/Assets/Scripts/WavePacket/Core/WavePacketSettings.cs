using UnityEngine;

namespace SYSTEM.WavePacket
{
    /// <summary>
    /// Centralized settings for wave packet visualization
    /// Create via: Assets -> Create -> SYSTEM -> Wave Packet Settings
    /// </summary>
    [CreateAssetMenu(fileName = "WavePacketSettings", menuName = "SYSTEM/Wave Packet Settings", order = 1)]
    public class WavePacketSettings : ScriptableObject
    {
        [Header("Visual Scale")]
        [Tooltip("Base radius of the wave packet disc")]
        public float discRadius = 1f;
        
        [Tooltip("Duration of extraction animation (0 for static)")]
        public float extractionDuration = 4f;
        
        [Tooltip("Rotation speed in degrees per second")]
        public float rotationSpeed = 90f;

        [Header("Ring Configuration")]
        [Tooltip("Radii for each frequency ring (outer to inner)")]
        public float[] ringRadii = new float[] { 0.75f, 0.625f, 0.5f, 0.375f, 0.25f, 0.125f };
        
        [Tooltip("Gaussian width for ring falloff")]
        public float ringWidth = 0.03f;
        
        [Tooltip("Height multiplier per packet count")]
        public float heightScale = 0.00625f;

        [Header("Mesh Quality")]
        [Tooltip("Mesh resolution (higher = smoother, lower = faster)")]
        public MeshQuality meshQuality = MeshQuality.Medium;

        [Header("Colors")]
        public Color colorRed = new Color(1f, 0f, 0f);
        public Color colorYellow = new Color(1f, 1f, 0f);
        public Color colorGreen = new Color(0f, 1f, 0f);
        public Color colorCyan = new Color(0f, 1f, 1f);
        public Color colorBlue = new Color(0f, 0f, 1f);
        public Color colorMagenta = new Color(1f, 0f, 1f);

        [Header("Shader")]
        [Tooltip("Shader to use for rendering (leave empty for default)")]
        public Shader customShader;

        public enum MeshQuality
        {
            Low = 32,      // 32x32 grid
            Medium = 64,   // 64x64 grid
            High = 128     // 128x128 grid
        }

        public int GetMeshResolution()
        {
            return (int)meshQuality;
        }

        public Color GetColorForFrequency(float frequency)
        {
            if (Mathf.Abs(frequency - 0.0f) < 0.1f) return colorRed;
            if (Mathf.Abs(frequency - 1.047f) < 0.1f) return colorYellow;
            if (Mathf.Abs(frequency - 2.094f) < 0.1f) return colorGreen;
            if (Mathf.Abs(frequency - 3.142f) < 0.1f) return colorCyan;
            if (Mathf.Abs(frequency - 4.189f) < 0.1f) return colorBlue;
            if (Mathf.Abs(frequency - 5.236f) < 0.1f) return colorMagenta;
            return Color.gray;
        }

        public int GetRingIndexForFrequency(float frequency)
        {
            if (Mathf.Abs(frequency - 0.0f) < 0.1f) return 0;
            if (Mathf.Abs(frequency - 1.047f) < 0.1f) return 1;
            if (Mathf.Abs(frequency - 2.094f) < 0.1f) return 2;
            if (Mathf.Abs(frequency - 3.142f) < 0.1f) return 3;
            if (Mathf.Abs(frequency - 4.189f) < 0.1f) return 4;
            if (Mathf.Abs(frequency - 5.236f) < 0.1f) return 5;
            return -1;
        }
    }
}
