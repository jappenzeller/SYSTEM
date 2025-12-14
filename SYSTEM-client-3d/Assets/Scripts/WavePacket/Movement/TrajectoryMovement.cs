using UnityEngine;
using System;
using SYSTEM.Circuits;
using SYSTEM.Debug;

namespace SYSTEM.WavePacket.Movement
{
    /// <summary>
    /// Client-driven trajectory movement for transfers, mining, and distribution packets.
    /// Supports single-phase and two-phase (vertical then horizontal or vice versa) movement.
    /// </summary>
    public class TrajectoryMovement : PacketMovementBase
    {
        /// <summary>
        /// Type of trajectory movement pattern.
        /// </summary>
        public enum TrajectoryType
        {
            /// <summary>Direct A->B at constant height (mining, sphere-to-sphere).</summary>
            Direct,

            /// <summary>Rise vertically, then travel horizontally (Object -> Sphere).</summary>
            VerticalThenHorizontal,

            /// <summary>Travel horizontally, then descend vertically (Sphere -> Object).</summary>
            HorizontalThenVertical
        }

        [Header("Trajectory")]
        [SerializeField] private TrajectoryType trajectoryType = TrajectoryType.Direct;

        // Timing
        private float journeyStartTime;
        private float phase1Duration;
        private float phase2Duration;

        // Phase tracking
        private Vector3 intermediatePosition;
        private Quaternion intermediateRotation;
        private bool inPhase2 = false;

        // Callback
        private Action onArrivalCallback;

        /// <summary>Current trajectory type.</summary>
        public TrajectoryType CurrentTrajectoryType => trajectoryType;

        #region Initialization Methods

        /// <summary>
        /// Initialize for direct A->B movement at constant height.
        /// Used for mining packets and sphere-to-sphere distribution.
        /// </summary>
        public void InitializeDirect(Vector3 target, float moveSpeed, float height, Action onArrival = null)
        {
            startPosition = transform.position;
            targetPosition = target;
            startRotation = transform.rotation;
            targetRotation = GetSurfaceOrientation(target);
            startHeight = height;
            targetHeight = height;
            speed = moveSpeed;
            onArrivalCallback = onArrival;
            trajectoryType = TrajectoryType.Direct;
            completionBehavior = CompletionBehavior.Destroy;

            CalculateDirectTrajectory();
            StartMovement();

            SystemDebug.Log(SystemDebug.Category.WavePacketSystem,
                $"[TrajectoryMovement] Direct: {startPosition} -> {targetPosition}, height={height}, speed={speed}");
        }

        /// <summary>
        /// Initialize for two-phase movement with height change.
        /// Used for Object->Sphere (rise then horizontal) or Sphere->Object (horizontal then descend).
        /// </summary>
        public void InitializeTwoPhase(
            Vector3 target,
            Quaternion targetRot,
            float moveSpeed,
            float heightStart,
            float heightEnd,
            Action onArrival = null)
        {
            startPosition = transform.position;
            targetPosition = target;
            startRotation = transform.rotation;
            targetRotation = targetRot;
            startHeight = heightStart;
            targetHeight = heightEnd;
            speed = moveSpeed;
            onArrivalCallback = onArrival;
            completionBehavior = CompletionBehavior.Destroy;

            CalculateTwoPhaseTrajectory();
            StartMovement();

            SystemDebug.Log(SystemDebug.Category.WavePacketSystem,
                $"[TrajectoryMovement] TwoPhase: {startPosition} -> {targetPosition}, height={heightStart}->{heightEnd}, type={trajectoryType}");
        }

        #endregion

        #region Trajectory Calculation

        private void CalculateDirectTrajectory()
        {
            trajectoryType = TrajectoryType.Direct;
            float distance = Vector3.Distance(startPosition, targetPosition);
            phase1Duration = distance / speed;
            phase2Duration = 0f;
            journeyStartTime = Time.time;
        }

        private void CalculateTwoPhaseTrajectory()
        {
            float horizontalDistance = Vector3.Distance(startPosition, targetPosition);
            float verticalDistance = Mathf.Abs(targetHeight - startHeight);

            if (verticalDistance < 0.01f)
            {
                // Same height - use direct movement
                trajectoryType = TrajectoryType.Direct;
                phase1Duration = horizontalDistance / speed;
                phase2Duration = 0f;
            }
            else if (targetHeight > startHeight)
            {
                // Object -> Sphere: Vertical rise THEN horizontal travel
                trajectoryType = TrajectoryType.VerticalThenHorizontal;
                phase1Duration = verticalDistance / speed;
                phase2Duration = horizontalDistance / speed;

                // After vertical rise, stay at same angular position but at new height
                intermediatePosition = startPosition;
                intermediateRotation = startRotation;
            }
            else
            {
                // Sphere -> Object: Horizontal travel THEN vertical descent
                trajectoryType = TrajectoryType.HorizontalThenVertical;
                phase1Duration = horizontalDistance / speed;
                phase2Duration = verticalDistance / speed;

                // After horizontal travel, at target angular position but still at start height
                intermediatePosition = targetPosition;
                intermediateRotation = targetRotation;
            }

            journeyStartTime = Time.time;
        }

        #endregion

        #region Movement Implementation

        protected override void OnStartMovement()
        {
            journeyStartTime = Time.time;
            inPhase2 = false;

            // Set initial movement state based on trajectory type
            switch (trajectoryType)
            {
                case TrajectoryType.Direct:
                    SetState(MovementState.MovingHorizontal);
                    break;

                case TrajectoryType.VerticalThenHorizontal:
                    SetState(MovementState.MovingVertical);
                    break;

                case TrajectoryType.HorizontalThenVertical:
                    SetState(MovementState.MovingHorizontal);
                    break;
            }
        }

        protected override void OnTickMovement(float deltaTime)
        {
            float elapsed = Time.time - journeyStartTime;

            switch (trajectoryType)
            {
                case TrajectoryType.Direct:
                    ProcessDirectMovement(elapsed);
                    break;

                case TrajectoryType.VerticalThenHorizontal:
                    ProcessVerticalFirst(elapsed);
                    break;

                case TrajectoryType.HorizontalThenVertical:
                    ProcessHorizontalFirst(elapsed);
                    break;
            }
        }

        private void ProcessDirectMovement(float elapsed)
        {
            float progress = Mathf.Clamp01(elapsed / phase1Duration);

            // Interpolate angular position on sphere
            Vector3 basePos = Vector3.Slerp(startPosition.normalized, targetPosition.normalized, progress);
            basePos *= CircuitConstants.WORLD_RADIUS;

            // Apply constant height
            ApplyPositionAndRotation(basePos, startHeight);

            // Interpolate rotation
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, progress);

            if (progress >= 1f)
            {
                CompleteMovement();
            }
        }

        private void ProcessVerticalFirst(float elapsed)
        {
            if (!inPhase2 && elapsed < phase1Duration)
            {
                // Phase 1: Rising vertically
                SetState(MovementState.MovingVertical);
                float progress = Mathf.Clamp01(elapsed / phase1Duration);
                float currentHeight = Mathf.Lerp(startHeight, targetHeight, progress);

                // Stay at start angular position while rising
                ApplyPositionAndRotation(startPosition, currentHeight);
                transform.rotation = startRotation;
            }
            else if (!inPhase2)
            {
                // Transition to phase 2
                inPhase2 = true;
                journeyStartTime = Time.time;
                SetState(MovementState.MovingHorizontal);
            }
            else
            {
                // Phase 2: Horizontal movement at target height
                float phase2Elapsed = Time.time - journeyStartTime;
                float progress = Mathf.Clamp01(phase2Elapsed / phase2Duration);

                // Interpolate angular position on sphere
                Vector3 basePos = Vector3.Slerp(startPosition.normalized, targetPosition.normalized, progress);
                basePos *= CircuitConstants.WORLD_RADIUS;

                ApplyPositionAndRotation(basePos, targetHeight);
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, progress);

                if (progress >= 1f)
                {
                    CompleteMovement();
                }
            }
        }

        private void ProcessHorizontalFirst(float elapsed)
        {
            if (!inPhase2 && elapsed < phase1Duration)
            {
                // Phase 1: Horizontal movement at start height
                SetState(MovementState.MovingHorizontal);
                float progress = Mathf.Clamp01(elapsed / phase1Duration);

                // Interpolate angular position on sphere
                Vector3 basePos = Vector3.Slerp(startPosition.normalized, targetPosition.normalized, progress);
                basePos *= CircuitConstants.WORLD_RADIUS;

                ApplyPositionAndRotation(basePos, startHeight);
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, progress);
            }
            else if (!inPhase2)
            {
                // Transition to phase 2
                inPhase2 = true;
                journeyStartTime = Time.time;
                SetState(MovementState.MovingVertical);
            }
            else
            {
                // Phase 2: Descending vertically
                float phase2Elapsed = Time.time - journeyStartTime;
                float progress = Mathf.Clamp01(phase2Elapsed / phase2Duration);
                float currentHeight = Mathf.Lerp(startHeight, targetHeight, progress);

                // Stay at target angular position while descending
                ApplyPositionAndRotation(targetPosition, currentHeight);
                transform.rotation = targetRotation;

                if (progress >= 1f)
                {
                    CompleteMovement();
                }
            }
        }

        protected override void OnCompleteMovement()
        {
            // Snap to final position with correct height and rotation
            ApplyPositionAndRotation(targetPosition, targetHeight);
            transform.rotation = targetRotation;

            // Invoke callback before destruction
            onArrivalCallback?.Invoke();

            SystemDebug.Log(SystemDebug.Category.WavePacketSystem,
                $"[TrajectoryMovement] Completed at {transform.position}");
        }

        #endregion

        #region Utility

        /// <summary>
        /// Get total journey duration in seconds.
        /// </summary>
        public float GetTotalDuration()
        {
            return phase1Duration + phase2Duration;
        }

        /// <summary>
        /// Get progress through journey (0-1).
        /// </summary>
        public float GetProgress()
        {
            float elapsed = Time.time - journeyStartTime;
            float totalDuration = phase1Duration + phase2Duration;

            if (totalDuration <= 0) return 1f;

            if (inPhase2)
            {
                float phase2Elapsed = elapsed;
                return (phase1Duration + Mathf.Clamp01(phase2Elapsed / phase2Duration) * phase2Duration) / totalDuration;
            }
            else
            {
                return Mathf.Clamp01(elapsed / phase1Duration) * phase1Duration / totalDuration;
            }
        }

        #endregion
    }
}
