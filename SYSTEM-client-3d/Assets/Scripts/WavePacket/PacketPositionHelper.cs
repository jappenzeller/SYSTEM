using UnityEngine;
using SYSTEM.Circuits;

namespace SYSTEM.WavePacket
{
    /// <summary>
    /// Helper methods for calculating packet positions and orientations on spherical worlds.
    /// Ensures consistent surface-normal orientation and height adjustments for all packet types.
    /// </summary>
    public static class PacketPositionHelper
    {
        /// <summary>
        /// Get surface normal at a position (direction away from world center).
        /// </summary>
        public static Vector3 GetSurfaceNormal(Vector3 position)
        {
            return position.normalized;
        }

        /// <summary>
        /// Get rotation that aligns object's up vector with surface normal.
        /// </summary>
        public static Quaternion GetOrientationForSurface(Vector3 normal)
        {
            return Quaternion.FromToRotation(Vector3.up, normal);
        }

        /// <summary>
        /// Adjust position to be at specified height above sphere surface.
        /// </summary>
        public static Vector3 AdjustPositionHeight(Vector3 position, float height)
        {
            return position.normalized * (CircuitConstants.WORLD_RADIUS + height);
        }

        /// <summary>
        /// Get rotation and position for packet at surface location with specified height.
        /// </summary>
        public static void GetPacketTransform(Vector3 basePosition, float height, out Vector3 position, out Quaternion rotation)
        {
            position = AdjustPositionHeight(basePosition, height);
            Vector3 normal = GetSurfaceNormal(basePosition);
            rotation = GetOrientationForSurface(normal);
        }
    }
}
