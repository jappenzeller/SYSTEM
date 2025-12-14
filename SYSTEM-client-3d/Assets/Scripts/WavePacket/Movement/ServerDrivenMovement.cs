using UnityEngine;
using System;
using SYSTEM.Circuits;
using SYSTEM.Debug;

namespace SYSTEM.WavePacket.Movement
{
    /// <summary>
    /// Server-driven movement for wave packet sources.
    /// Receives state updates from server, interpolates locally between updates.
    /// 4-state machine: MOVING_H -> ARRIVED_H0 -> RISING -> STATIONARY
    /// </summary>
    public class ServerDrivenMovement : PacketMovementBase
    {
        #region Server State Constants (must match lib.rs)

        public const byte STATE_MOVING_H = 0;      // Moving horizontally on sphere surface
        public const byte STATE_ARRIVED_H0 = 1;    // Arrived at destination, at height 0
        public const byte STATE_RISING = 2;        // Rising from height 0 to height 1
        public const byte STATE_STATIONARY = 3;    // Final position, mineable

        #endregion

        #region Movement Constants (must match lib.rs)

        public const float SOURCE_MOVE_SPEED = 6.0f;    // Horizontal movement speed
        public const float SOURCE_RISE_SPEED = 2.0f;    // Vertical rise speed
        public const float SOURCE_HEIGHT_START = 0.0f;  // Starting height (sphere surface)
        public const float SOURCE_HEIGHT_FINAL = 1.0f;  // Final height above surface

        #endregion

        [Header("Server State")]
        [SerializeField] private byte serverState = STATE_MOVING_H;

        // Movement interpolation
        private Vector3 velocity;
        private Vector3 destination;
        private float stateStartTime;

        // Events specific to source movement
        public event Action<byte, byte> OnServerStateChanged;  // (oldState, newState)

        /// <summary>Current server-assigned state.</summary>
        public byte ServerState => serverState;

        /// <summary>
        /// Initialize source movement with initial server data.
        /// </summary>
        public void Initialize(Vector3 position, Vector3 velocity, Vector3 destination, byte state)
        {
            this.startPosition = position;
            this.velocity = velocity;
            this.destination = destination;
            this.serverState = state;
            this.stateStartTime = Time.time;

            // Sources persist after movement completes
            completionBehavior = CompletionBehavior.Persist;
            speed = SOURCE_MOVE_SPEED;

            // Set initial transform
            transform.position = position;
            transform.rotation = GetSurfaceOrientation(position);

            // Map server state to movement state
            MapServerStateToMovementState();

            // Start movement if not already stationary
            if (state < STATE_STATIONARY)
            {
                StartMovement();
            }
            else
            {
                isActive = false;
                SetState(MovementState.Complete);
            }

            SystemDebug.Log(SystemDebug.Category.WavePacketSystem,
                $"[ServerDrivenMovement] Initialized: pos={position}, vel={velocity}, dest={destination}, state={state}");
        }

        /// <summary>
        /// Update from server state change.
        /// Only resets position on state TRANSITIONS to prevent visual jitter.
        /// </summary>
        public void UpdateFromServer(Vector3 position, Vector3 velocity, Vector3 destination, byte newState)
        {
            byte oldState = serverState;
            bool stateTransition = serverState != newState;

            if (stateTransition)
            {
                // State transition - reset position and timing
                startPosition = position;
                stateStartTime = Time.time;

                SystemDebug.Log(SystemDebug.Category.WavePacketSystem,
                    $"[ServerDrivenMovement] State transition: {oldState} -> {newState}, new pos={position}");

                // Fire state change event
                OnServerStateChanged?.Invoke(oldState, newState);
            }

            // Always update velocity/destination (doesn't cause visual snapping)
            this.velocity = velocity;
            this.destination = destination;
            this.serverState = newState;

            // Map to movement state
            MapServerStateToMovementState();

            // Handle completion
            if (newState >= STATE_STATIONARY && isActive)
            {
                CompleteMovement();
            }
            else if (newState < STATE_STATIONARY && !isActive)
            {
                StartMovement();
            }
        }

        /// <summary>
        /// Map server state byte to MovementState enum.
        /// </summary>
        private void MapServerStateToMovementState()
        {
            switch (serverState)
            {
                case STATE_MOVING_H:
                    SetState(MovementState.MovingHorizontal);
                    break;

                case STATE_ARRIVED_H0:
                    SetState(MovementState.Arriving);
                    break;

                case STATE_RISING:
                    SetState(MovementState.MovingVertical);
                    break;

                case STATE_STATIONARY:
                    SetState(MovementState.Complete);
                    break;

                default:
                    SystemDebug.LogWarning(SystemDebug.Category.WavePacketSystem,
                        $"[ServerDrivenMovement] Unknown server state: {serverState}");
                    break;
            }
        }

        #region Movement Implementation

        protected override void OnStartMovement()
        {
            isActive = true;
            stateStartTime = Time.time;
        }

        protected override void OnTickMovement(float deltaTime)
        {
            // Don't process if stationary
            if (serverState >= STATE_STATIONARY)
                return;

            float timeSinceStateStart = Time.time - stateStartTime;
            Vector3 predictedPos;

            // Apply state-specific movement
            switch (serverState)
            {
                case STATE_MOVING_H:
                    // Spherical movement - rotate around world center
                    // Angular velocity: ω = v / r (linear speed / radius)
                    float speed = velocity.magnitude;
                    float angularVelocity = speed / CircuitConstants.WORLD_RADIUS;
                    float angle = angularVelocity * timeSinceStateStart;

                    // Rotation axis: perpendicular to position and velocity (cross product)
                    Vector3 posNormal = startPosition.normalized;
                    Vector3 velNormal = velocity.normalized;
                    Vector3 rotationAxis = Vector3.Cross(posNormal, velNormal).normalized;

                    // Rotate start position around axis by angle
                    Quaternion rotation = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, rotationAxis);
                    predictedPos = rotation * startPosition;

                    // Clamp to destination to prevent overshoot
                    float distanceToDestination = Vector3.Distance(startPosition, destination);
                    float arcDistance = angle * CircuitConstants.WORLD_RADIUS;
                    if (arcDistance >= distanceToDestination && distanceToDestination > 0.01f)
                    {
                        predictedPos = destination;
                    }
                    break;

                case STATE_ARRIVED_H0:
                    // At destination, height 0 - snap to destination on surface
                    predictedPos = ConstrainToSphereSurface(destination);
                    break;

                case STATE_RISING:
                    // Rising - calculate radial position based on elapsed time
                    float currentHeight = Mathf.Min(SOURCE_RISE_SPEED * timeSinceStateStart, SOURCE_HEIGHT_FINAL);
                    Vector3 surfaceNormal = startPosition.normalized;
                    predictedPos = surfaceNormal * (CircuitConstants.WORLD_RADIUS + currentHeight);
                    break;

                default:
                    predictedPos = transform.position;
                    break;
            }

            // Smooth interpolation toward predicted position
            transform.position = Vector3.Lerp(transform.position, predictedPos, deltaTime * interpolationSpeed);

            // Update rotation to align with sphere surface
            transform.rotation = GetSurfaceOrientation(transform.position);
        }

        protected override void OnCompleteMovement()
        {
            // Snap to final position at correct height
            Vector3 finalNormal = destination.normalized;
            transform.position = finalNormal * (CircuitConstants.WORLD_RADIUS + SOURCE_HEIGHT_FINAL);
            transform.rotation = GetSurfaceOrientation(transform.position);

            SystemDebug.Log(SystemDebug.Category.WavePacketSystem,
                $"[ServerDrivenMovement] Completed at {transform.position}");
        }

        #endregion

        #region Alpha Support

        /// <summary>
        /// Get recommended alpha for current state.
        /// Moving sources are more transparent than stationary ones.
        /// </summary>
        public float GetRecommendedAlpha()
        {
            switch (serverState)
            {
                case STATE_MOVING_H:
                case STATE_ARRIVED_H0:
                    return 0.6f;  // Moving - more transparent

                case STATE_RISING:
                    return 0.8f;  // Rising - medium transparency

                case STATE_STATIONARY:
                default:
                    return 1.0f;  // Stationary - fully opaque
            }
        }

        #endregion
    }
}
