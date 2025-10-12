using UnityEngine;

namespace SYSTEM.WavePacket
{
    /// <summary>
    /// Standard frequency values in radians (0 to 2π)
    /// Used throughout the system for wave packet frequencies
    /// </summary>
    public static class FrequencyConstants
    {
        // Standard frequencies in radians
        public const float RED = 0.0f;          // 0°
        public const float YELLOW = 1.047f;     // 60° = π/3
        public const float GREEN = 2.094f;      // 120° = 2π/3
        public const float CYAN = 3.142f;       // 180° = π
        public const float BLUE = 4.189f;       // 240° = 4π/3
        public const float MAGENTA = 5.236f;    // 300° = 5π/3

        // Conversion constant
        public const float TWO_PI = 6.283185f;  // 2π

        /// <summary>
        /// Normalize a radian frequency to 0-1 range
        /// Used for color mapping, UI sliders, etc.
        /// </summary>
        public static float NormalizeFrequency(float radianFrequency)
        {
            return radianFrequency / TWO_PI;
        }

        /// <summary>
        /// Convert normalized frequency (0-1) to radians (0-2π)
        /// </summary>
        public static float DenormalizeFrequency(float normalized)
        {
            return normalized * TWO_PI;
        }

        /// <summary>
        /// Get color for a radian frequency
        /// </summary>
        public static Color GetColorForFrequency(float radianFrequency)
        {
            // Normalize to 0-1 for color mapping
            float normalized = NormalizeFrequency(radianFrequency);

            if (normalized < 0.1f) return Color.red;
            if (normalized < 0.25f) return Color.yellow;
            if (normalized < 0.4f) return Color.green;
            if (normalized < 0.55f) return Color.cyan;
            if (normalized < 0.75f) return Color.blue;
            return Color.magenta;
        }

        /// <summary>
        /// Find the closest standard frequency to a given radian value
        /// </summary>
        public static float GetNearestStandardFrequency(float radianFrequency)
        {
            float[] standards = { RED, YELLOW, GREEN, CYAN, BLUE, MAGENTA };
            float nearest = RED;
            float minDist = Mathf.Abs(radianFrequency - RED);

            foreach (float freq in standards)
            {
                float dist = Mathf.Abs(radianFrequency - freq);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = freq;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Get frequency name for debugging
        /// </summary>
        public static string GetFrequencyName(float radianFrequency)
        {
            if (Mathf.Abs(radianFrequency - RED) < 0.1f) return "Red";
            if (Mathf.Abs(radianFrequency - YELLOW) < 0.1f) return "Yellow";
            if (Mathf.Abs(radianFrequency - GREEN) < 0.1f) return "Green";
            if (Mathf.Abs(radianFrequency - CYAN) < 0.1f) return "Cyan";
            if (Mathf.Abs(radianFrequency - BLUE) < 0.1f) return "Blue";
            if (Mathf.Abs(radianFrequency - MAGENTA) < 0.1f) return "Magenta";
            return $"Custom({radianFrequency:F3})";
        }
    }
}
