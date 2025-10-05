using UnityEngine;
using SpacetimeDB.Types;

namespace SYSTEM.WavePacket
{
    /// <summary>
    /// Abstract base class for platform-specific wave packet rendering
    /// Handles both flying packets and detailed extraction visuals
    /// </summary>
    public abstract class WavePacketRenderer : MonoBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] protected float extractionDiscRadius = 1f; // Scaled down 1/20 for orb size
        [SerializeField] protected float extractionDuration = 4f; // Slowed down from 2s to 4s
        [SerializeField] protected float rotationSpeed = 90f; // Slowed down from 180 to 90 degrees per second

        [Header("Ring Configuration")]
        [SerializeField] protected float[] ringRadii = new float[] { 0.75f, 0.625f, 0.5f, 0.375f, 0.25f, 0.125f }; // Scaled down 1/20
        [SerializeField] protected float ringWidth = 0.03f; // Scaled down 1/20 from 0.6f
        [SerializeField] protected float heightScale = 0.00625f; // Scaled down 1/20 from 0.125f

        [Header("Colors")]
        [SerializeField] protected Color colorRed = new Color(1f, 0f, 0f);
        [SerializeField] protected Color colorYellow = new Color(1f, 1f, 0f);
        [SerializeField] protected Color colorGreen = new Color(0f, 1f, 0f);
        [SerializeField] protected Color colorCyan = new Color(0f, 1f, 1f);
        [SerializeField] protected Color colorBlue = new Color(0f, 0f, 1f);
        [SerializeField] protected Color colorMagenta = new Color(1f, 0f, 1f);
        [SerializeField] protected Color colorGrey = new Color(0.5f, 0.5f, 0.5f);

        protected bool isExtracting = false;
        protected float extractionProgress = 0f;

        /// <summary>
        /// Create a flying packet with trajectory animation
        /// Returns the GameObject so caller can manage it
        /// </summary>
        public abstract GameObject CreateFlyingPacket(WavePacketSample[] samples, Vector3 startPosition, Vector3 targetPosition, float speed);

        /// <summary>
        /// Start the detailed extraction visualization
        /// </summary>
        public abstract void StartExtraction(WavePacketSample[] samples, Vector3 orbPosition);

        /// <summary>
        /// Update extraction animation progress (0-1)
        /// </summary>
        public abstract void UpdateExtraction(float progress);

        /// <summary>
        /// End extraction and clean up
        /// </summary>
        public abstract void EndExtraction();

        /// <summary>
        /// Map frequency to color
        /// </summary>
        protected Color GetColorForFrequency(float frequency)
        {
            // Frequency mapping: 0.0=Red, 1.047=Yellow, 2.094=Green, 3.142=Cyan, 4.189=Blue, 5.236=Magenta
            if (Mathf.Abs(frequency - 0.0f) < 0.1f) return colorRed;
            if (Mathf.Abs(frequency - 1.047f) < 0.1f) return colorYellow;
            if (Mathf.Abs(frequency - 2.094f) < 0.1f) return colorGreen;
            if (Mathf.Abs(frequency - 3.142f) < 0.1f) return colorCyan;
            if (Mathf.Abs(frequency - 4.189f) < 0.1f) return colorBlue;
            if (Mathf.Abs(frequency - 5.236f) < 0.1f) return colorMagenta;
            return colorGrey;
        }

        /// <summary>
        /// Map radius to color for ring visualization
        /// Rings: Red(0.75), Yellow(0.625), Green(0.5), Cyan(0.375), Blue(0.25), Magenta(0.125)
        /// </summary>
        protected Color GetColorForRadius(float radius)
        {
            // Match each radius range to its ring color (scaled 1/20)
            if (radius > 0.6875f) return colorRed;      // Outer ring: 0.6875-0.75+
            if (radius > 0.5625f) return colorYellow;   // 0.5625-0.6875
            if (radius > 0.4375f) return colorGreen;    // 0.4375-0.5625
            if (radius > 0.3125f) return colorCyan;     // 0.3125-0.4375
            if (radius > 0.1875f) return colorBlue;     // 0.1875-0.3125
            if (radius > 0.0625f) return colorMagenta;  // 0.0625-0.1875
            return colorMagenta;                        // Center: 0-0.0625
        }

        /// <summary>
        /// Get ring index (0-5) for a given frequency
        /// </summary>
        protected int GetRingIndexForFrequency(float frequency)
        {
            // Red=0, Yellow=1, Green=2, Cyan=3, Blue=4, Magenta=5
            if (Mathf.Abs(frequency - 0.0f) < 0.1f) return 0;
            if (Mathf.Abs(frequency - 1.047f) < 0.1f) return 1;
            if (Mathf.Abs(frequency - 2.094f) < 0.1f) return 2;
            if (Mathf.Abs(frequency - 3.142f) < 0.1f) return 3;
            if (Mathf.Abs(frequency - 4.189f) < 0.1f) return 4;
            if (Mathf.Abs(frequency - 5.236f) < 0.1f) return 5;
            return -1; // Unknown frequency
        }

        /// <summary>
        /// Calculate weighted average color for mixed composition
        /// </summary>
        protected Color GetDominantColor(WavePacketSample[] samples)
        {
            if (samples == null || samples.Length == 0)
                return colorGrey;

            Color result = Color.black;
            float totalWeight = 0f;

            foreach (var sample in samples)
            {
                Color sampleColor = GetColorForFrequency(sample.Frequency);
                float weight = sample.Count;
                result += sampleColor * weight;
                totalWeight += weight;
            }

            if (totalWeight > 0)
                result /= totalWeight;

            return result;
        }

        /// <summary>
        /// Calculate height at a given radius based on gaussian rings
        /// </summary>
        protected float CalculateHeightAtRadius(float radius, WavePacketSample[] samples)
        {
            float height = 0f;

            foreach (var sample in samples)
            {
                int ringIndex = GetRingIndexForFrequency(sample.Frequency);
                if (ringIndex < 0 || ringIndex >= ringRadii.Length)
                    continue;

                float ringRadius = ringRadii[ringIndex];
                float distanceFromRing = radius - ringRadius;

                // Gaussian falloff
                float gaussian = Mathf.Exp(-(distanceFromRing * distanceFromRing) / (2f * ringWidth * ringWidth));
                height += sample.Count * heightScale * gaussian;
            }

            return height;
        }

        /// <summary>
        /// Calculate color at a given radius using closest ring's color
        /// Prevents color mixing that causes Z-order issues with multiple rings
        /// </summary>
        protected Color CalculateColorAtRadius(float radius, WavePacketSample[] samples)
        {
            if (samples == null || samples.Length == 0)
                return new Color(0, 0, 0, 0); // Transparent, not grey

            // Find the closest ring to this radius
            float closestDistance = float.MaxValue;
            Color closestColor = new Color(0, 0, 0, 0); // Start with transparent
            float closestContribution = 0f;

            foreach (var sample in samples)
            {
                int ringIndex = GetRingIndexForFrequency(sample.Frequency);
                if (ringIndex < 0 || ringIndex >= ringRadii.Length)
                    continue;

                float ringRadius = ringRadii[ringIndex];
                float distanceFromRing = Mathf.Abs(radius - ringRadius);

                // Find closest ring
                if (distanceFromRing < closestDistance)
                {
                    closestDistance = distanceFromRing;
                    closestColor = GetColorForFrequency(sample.Frequency);

                    // Calculate brightness based on gaussian
                    float gaussian = Mathf.Exp(-(distanceFromRing * distanceFromRing) / (2f * ringWidth * ringWidth));
                    closestContribution = sample.Count * gaussian;
                }
            }

            // Use the closest ring's color, modulated by gaussian brightness
            // Normalize brightness (assuming max count of ~20)
            float brightness = Mathf.Clamp01(closestContribution / 5f);
            brightness = Mathf.Max(0.5f, brightness); // Minimum 50% brightness to avoid black holes

            // Always return the closest color (extends ring color to center)
            // Transparency is handled by alpha fadeout in mesh generation, not here
            return closestColor * brightness;
        }
    }

    /// <summary>
    /// Factory for creating platform-appropriate renderer
    /// </summary>
    public static class WavePacketFactory
    {
        public static WavePacketRenderer CreateRenderer(GameObject parent)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return parent.AddComponent<WavePacketRenderer_WebGL>();
#else
            return parent.AddComponent<WavePacketRenderer_Native>();
#endif
        }
    }
}
