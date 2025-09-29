using UnityEngine;

namespace SYSTEM.Circuits
{
    /// <summary>
    /// Central constants for the circuit visualization system.
    /// CRITICAL: World radius R = 300 units must be consistent throughout the system.
    /// </summary>
    public static class CircuitConstants
    {
        // ==================== WORLD RADIUS DEFINITION ====================
        /// <summary>
        /// The fundamental world radius in Unity units.
        /// CRITICAL: This value MUST match the actual world sphere radius (300 units).
        /// All lattice calculations and circuit placements depend on this value.
        /// </summary>
        public const float WORLD_RADIUS = 300f;  // R = 300 units

        // ==================== FCC LATTICE PARAMETERS ====================
        /// <summary>
        /// Distance between main grid vertices in the FCC lattice (10R).
        /// Main grid worlds are positioned at multiples of this value.
        /// Example: World at (1,0,0) is at position (3000,0,0) in Unity coordinates.
        /// </summary>
        public const float LATTICE_SPACING = WORLD_RADIUS * 10f;  // 10R = 3000 units

        /// <summary>
        /// Offset for face-center worlds from main grid vertices (5R).
        /// Face-center worlds are offset by this amount along one axis.
        /// </summary>
        public const float FACE_CENTER_OFFSET = WORLD_RADIUS * 5f;  // 5R = 1500 units

        /// <summary>
        /// Offset for cube-center worlds from main grid vertices (5R).
        /// Cube-center worlds are offset by this amount along all three axes.
        /// </summary>
        public const float CUBE_CENTER_OFFSET = WORLD_RADIUS * 5f;  // 5R = 1500 units

        // ==================== CIRCUIT POSITIONING ====================
        /// <summary>
        /// Base size of circuit platforms relative to world radius.
        /// </summary>
        public const float CIRCUIT_BASE_RADIUS_RATIO = 0.05f;  // 5% of R
        public const float CIRCUIT_BASE_RADIUS = WORLD_RADIUS * CIRCUIT_BASE_RADIUS_RATIO;  // 15 units

        /// <summary>
        /// Height of energy conduit above world surface relative to world radius.
        /// </summary>
        public const float CONDUIT_HEIGHT_RATIO = 0.125f;  // 12.5% of R
        public const float CONDUIT_HEIGHT = WORLD_RADIUS * CONDUIT_HEIGHT_RATIO;  // 37.5 units

        /// <summary>
        /// Distribution sphere parameters relative to world radius.
        /// </summary>
        public const float DISTRIBUTION_SPHERE_RADIUS_RATIO = 0.133f;  // 13.3% of R
        public const float DISTRIBUTION_SPHERE_RADIUS = WORLD_RADIUS * DISTRIBUTION_SPHERE_RADIUS_RATIO;  // 40 units
        public const float DISTRIBUTION_SPHERE_HEIGHT_RATIO = 0.15f;  // 15% of R above surface
        public const float DISTRIBUTION_SPHERE_HEIGHT = WORLD_RADIUS * DISTRIBUTION_SPHERE_HEIGHT_RATIO;  // 45 units

        /// <summary>
        /// Ring assembly positioning relative to world radius.
        /// </summary>
        public const float RING_ASSEMBLY_HEIGHT_RATIO = 0.225f;  // 22.5% of R above surface
        public const float RING_ASSEMBLY_HEIGHT = WORLD_RADIUS * RING_ASSEMBLY_HEIGHT_RATIO;  // 67.5 units

        /// <summary>
        /// Ring sizes for different connection types.
        /// </summary>
        public const float PRIMARY_RING_RADIUS = WORLD_RADIUS * 0.08f;    // 24 units
        public const float SECONDARY_RING_RADIUS = WORLD_RADIUS * 0.10f;  // 30 units
        public const float TERTIARY_RING_RADIUS = WORLD_RADIUS * 0.12f;   // 36 units

        // ==================== PERFORMANCE PARAMETERS ====================
        /// <summary>
        /// Level of detail distances for circuit rendering.
        /// </summary>
        public const float LOD_FULL_DETAIL_DISTANCE = 100f;
        public const float LOD_SIMPLIFIED_DISTANCE = 500f;
        public const float LOD_HIDDEN_DISTANCE = 1000f;

        /// <summary>
        /// Maximum number of circuits per world.
        /// </summary>
        public const int MAX_CIRCUITS_PER_WORLD = 26;  // 6 faces + 8 vertices + 12 edges

        // ==================== VISUAL PARAMETERS ====================
        /// <summary>
        /// Animation and effect timing parameters.
        /// </summary>
        public const float CHARGE_PULSE_FREQUENCY = 1f;  // Pulses per second
        public const float RING_ROTATION_SPEED = 10f;   // Degrees per second
        public const float TUNNEL_FORMATION_TIME = 2f;  // Seconds to form tunnel
        public const float ENERGY_FLOW_SPEED = 50f;      // Units per second

        /// <summary>
        /// Color definitions for different frequency bands.
        /// </summary>
        public static readonly Color FREQUENCY_RED = new Color(1f, 0.2f, 0.2f, 1f);
        public static readonly Color FREQUENCY_GREEN = new Color(0.2f, 1f, 0.2f, 1f);
        public static readonly Color FREQUENCY_BLUE = new Color(0.2f, 0.2f, 1f, 1f);
        public static readonly Color FREQUENCY_YELLOW = new Color(1f, 1f, 0.2f, 1f);
        public static readonly Color FREQUENCY_CYAN = new Color(0.2f, 1f, 1f, 1f);
        public static readonly Color FREQUENCY_MAGENTA = new Color(1f, 0.2f, 1f, 1f);

        // ==================== VALIDATION METHODS ====================
        /// <summary>
        /// Validates that a given world radius matches the expected value.
        /// </summary>
        public static bool ValidateWorldRadius(float radius)
        {
            return Mathf.Approximately(radius, WORLD_RADIUS);
        }

        /// <summary>
        /// Gets the expected player spawn distance from world center (at north pole).
        /// </summary>
        public static float GetExpectedPlayerSpawnDistance()
        {
            return WORLD_RADIUS;  // Players spawn at radius R from center
        }

        /// <summary>
        /// Converts logical world coordinates to Unity world position based on world type.
        /// </summary>
        public static Vector3 LogicalToWorldPosition(int x, int y, int z, WorldType type = WorldType.MainGrid)
        {
            switch (type)
            {
                case WorldType.MainGrid:
                    // Main grid vertices at 10R spacing
                    // Example: (1,0,0) → (3000,0,0)
                    return new Vector3(
                        x * LATTICE_SPACING,
                        y * LATTICE_SPACING,
                        z * LATTICE_SPACING
                    );

                case WorldType.FaceCenter:
                    // Face centers offset by 5R along one axis
                    // This is simplified - actual implementation needs to know which face
                    return new Vector3(
                        x * LATTICE_SPACING + FACE_CENTER_OFFSET,
                        y * LATTICE_SPACING,
                        z * LATTICE_SPACING
                    );

                case WorldType.CubeCenter:
                    // Cube centers offset by 5R along all axes
                    // Example: Cube center at logical (0,0,0) → Unity (1500,1500,1500)
                    return new Vector3(
                        x * LATTICE_SPACING + CUBE_CENTER_OFFSET,
                        y * LATTICE_SPACING + CUBE_CENTER_OFFSET,
                        z * LATTICE_SPACING + CUBE_CENTER_OFFSET
                    );

                default:
                    return Vector3.zero;
            }
        }

        /// <summary>
        /// World types in the FCC lattice.
        /// </summary>
        public enum WorldType
        {
            MainGrid,    // Vertices of the cubic grid
            FaceCenter,  // Centers of cube faces
            CubeCenter   // Centers of cubes
        }
    }
}