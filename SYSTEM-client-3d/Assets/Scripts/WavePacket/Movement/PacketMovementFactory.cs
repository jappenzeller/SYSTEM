using UnityEngine;
using System;
using SYSTEM.Circuits;

namespace SYSTEM.WavePacket.Movement
{
    /// <summary>
    /// Factory for creating appropriate movement components for different packet types.
    /// Provides a unified API for all wave packet movement needs.
    /// </summary>
    public static class PacketMovementFactory
    {
        #region Source Movement (Server-Driven)

        /// <summary>
        /// Create server-driven movement for wave packet sources.
        /// Sources persist after movement completes (CompletionBehavior.Persist).
        /// </summary>
        /// <param name="target">GameObject to add movement component to.</param>
        /// <param name="position">Initial position from server.</param>
        /// <param name="velocity">Current velocity from server.</param>
        /// <param name="destination">Target destination from server.</param>
        /// <param name="state">Current server state (0=MOVING_H, 1=ARRIVED_H0, 2=RISING, 3=STATIONARY).</param>
        /// <returns>The created ServerDrivenMovement component.</returns>
        public static ServerDrivenMovement CreateSourceMovement(
            GameObject target,
            Vector3 position,
            Vector3 velocity,
            Vector3 destination,
            byte state)
        {
            var movement = target.AddComponent<ServerDrivenMovement>();
            movement.Initialize(position, velocity, destination, state);
            return movement;
        }

        #endregion

        #region Mining Movement (Direct Trajectory)

        /// <summary>
        /// Create direct A->B trajectory for mining extraction packets.
        /// Packets travel at constant height and destroy on arrival.
        /// </summary>
        /// <param name="target">GameObject to add movement component to.</param>
        /// <param name="endPosition">Target position (player location).</param>
        /// <param name="speed">Travel speed in units/second.</param>
        /// <param name="onArrival">Callback when packet reaches destination.</param>
        /// <returns>The created TrajectoryMovement component.</returns>
        public static TrajectoryMovement CreateMiningTrajectory(
            GameObject target,
            Vector3 endPosition,
            float speed,
            Action onArrival = null)
        {
            var movement = target.AddComponent<TrajectoryMovement>();
            movement.InitializeDirect(
                endPosition,
                speed,
                CircuitConstants.MINING_PACKET_HEIGHT,
                onArrival
            );
            return movement;
        }

        #endregion

        #region Transfer Movement (Two-Phase Trajectory)

        /// <summary>
        /// Create two-phase trajectory for Object->Sphere transfers.
        /// Packets rise vertically, then travel horizontally.
        /// </summary>
        /// <param name="target">GameObject to add movement component to.</param>
        /// <param name="endPosition">Target position (sphere location).</param>
        /// <param name="endRotation">Target rotation.</param>
        /// <param name="speed">Travel speed in units/second.</param>
        /// <param name="onArrival">Callback when packet reaches destination.</param>
        /// <returns>The created TrajectoryMovement component.</returns>
        public static TrajectoryMovement CreateObjectToSphereTrajectory(
            GameObject target,
            Vector3 endPosition,
            Quaternion endRotation,
            float speed,
            Action onArrival = null)
        {
            var movement = target.AddComponent<TrajectoryMovement>();
            movement.InitializeTwoPhase(
                endPosition,
                endRotation,
                speed,
                CircuitConstants.OBJECT_PACKET_HEIGHT,   // Start at object height (1)
                CircuitConstants.SPHERE_PACKET_HEIGHT,   // End at sphere height (10)
                onArrival
            );
            return movement;
        }

        /// <summary>
        /// Create two-phase trajectory for Sphere->Object transfers.
        /// Packets travel horizontally, then descend vertically.
        /// </summary>
        /// <param name="target">GameObject to add movement component to.</param>
        /// <param name="endPosition">Target position (object location).</param>
        /// <param name="endRotation">Target rotation.</param>
        /// <param name="speed">Travel speed in units/second.</param>
        /// <param name="onArrival">Callback when packet reaches destination.</param>
        /// <returns>The created TrajectoryMovement component.</returns>
        public static TrajectoryMovement CreateSphereToObjectTrajectory(
            GameObject target,
            Vector3 endPosition,
            Quaternion endRotation,
            float speed,
            Action onArrival = null)
        {
            var movement = target.AddComponent<TrajectoryMovement>();
            movement.InitializeTwoPhase(
                endPosition,
                endRotation,
                speed,
                CircuitConstants.SPHERE_PACKET_HEIGHT,   // Start at sphere height (10)
                CircuitConstants.OBJECT_PACKET_HEIGHT,   // End at object height (1)
                onArrival
            );
            return movement;
        }

        /// <summary>
        /// Create transfer trajectory with explicit heights.
        /// Automatically determines trajectory type based on height change.
        /// </summary>
        /// <param name="target">GameObject to add movement component to.</param>
        /// <param name="endPosition">Target position.</param>
        /// <param name="endRotation">Target rotation.</param>
        /// <param name="speed">Travel speed in units/second.</param>
        /// <param name="startHeight">Starting height above surface.</param>
        /// <param name="endHeight">Ending height above surface.</param>
        /// <param name="onArrival">Callback when packet reaches destination.</param>
        /// <returns>The created TrajectoryMovement component.</returns>
        public static TrajectoryMovement CreateTransferTrajectory(
            GameObject target,
            Vector3 endPosition,
            Quaternion endRotation,
            float speed,
            float startHeight,
            float endHeight,
            Action onArrival = null)
        {
            var movement = target.AddComponent<TrajectoryMovement>();
            movement.InitializeTwoPhase(
                endPosition,
                endRotation,
                speed,
                startHeight,
                endHeight,
                onArrival
            );
            return movement;
        }

        #endregion

        #region Distribution Movement (Constant Height Trajectory)

        /// <summary>
        /// Create constant-height trajectory for Sphere->Sphere distribution.
        /// Packets travel at sphere height throughout journey.
        /// </summary>
        /// <param name="target">GameObject to add movement component to.</param>
        /// <param name="endPosition">Target position (destination sphere).</param>
        /// <param name="speed">Travel speed in units/second.</param>
        /// <param name="onArrival">Callback when packet reaches destination.</param>
        /// <returns>The created TrajectoryMovement component.</returns>
        public static TrajectoryMovement CreateDistributionTrajectory(
            GameObject target,
            Vector3 endPosition,
            float speed,
            Action onArrival = null)
        {
            var movement = target.AddComponent<TrajectoryMovement>();
            movement.InitializeDirect(
                endPosition,
                speed,
                CircuitConstants.SPHERE_PACKET_HEIGHT,  // Constant sphere height (10)
                onArrival
            );
            return movement;
        }

        #endregion

        #region Generic Movement

        /// <summary>
        /// Create direct trajectory with custom height.
        /// General-purpose for any constant-height movement.
        /// </summary>
        /// <param name="target">GameObject to add movement component to.</param>
        /// <param name="endPosition">Target position.</param>
        /// <param name="speed">Travel speed in units/second.</param>
        /// <param name="height">Height above surface.</param>
        /// <param name="onArrival">Callback when packet reaches destination.</param>
        /// <returns>The created TrajectoryMovement component.</returns>
        public static TrajectoryMovement CreateDirectTrajectory(
            GameObject target,
            Vector3 endPosition,
            float speed,
            float height,
            Action onArrival = null)
        {
            var movement = target.AddComponent<TrajectoryMovement>();
            movement.InitializeDirect(
                endPosition,
                speed,
                height,
                onArrival
            );
            return movement;
        }

        #endregion
    }
}
