using UnityEngine;
using System;
using SYSTEM.Circuits;

namespace SYSTEM.WavePacket
{
    /// <summary>
    /// Trajectory controller for flying wave packets with spherical movement.
    /// Supports two-phase movement: vertical then horizontal, or horizontal then vertical.
    /// </summary>
    public class PacketTrajectory : MonoBehaviour
    {
        private enum MovementPhase
        {
            SinglePhase,      // Constant height movement (mining, sphere-to-sphere)
            VerticalPhase,    // Object→Sphere phase 1: Rise vertically
            HorizontalPhase,  // All horizontal movement phases
            Complete          // Journey finished
        }

        private Vector3 startPosition;
        private Vector3 targetPosition;
        private Quaternion startRotation;
        private Quaternion endRotation;
        private float startHeight;
        private float endHeight;
        private float speed = 5f;
        private bool isActive = false;
        private Action onArrival;

        // Progress tracking
        private float journeyLength;
        private float journeyStartTime;

        // Phase system
        private MovementPhase currentPhase;
        private float phase1Duration;           // Duration of first phase
        private float phase2Duration;           // Duration of second phase
        private Vector3 intermediatePosition;   // Position at phase transition
        private Quaternion intermediateRotation; // Rotation at phase transition

        /// <summary>
        /// Initialize trajectory with simple position movement (legacy compatibility).
        /// </summary>
        public void Initialize(Vector3 target, float moveSpeed, Action arrivalCallback = null)
        {
            startPosition = transform.position;
            targetPosition = target;
            startRotation = transform.rotation;
            endRotation = transform.rotation;
            startHeight = 0f;
            endHeight = 0f;
            speed = moveSpeed;
            isActive = true;
            onArrival = arrivalCallback;

            journeyLength = Vector3.Distance(startPosition, targetPosition);
            journeyStartTime = Time.time;
        }

        /// <summary>
        /// Initialize trajectory with rotation and height support.
        /// Automatically detects movement phases based on height change.
        /// </summary>
        public void Initialize(Vector3 target, Quaternion targetRotation, float moveSpeed,
                             float heightStart, float heightEnd, Action arrivalCallback = null)
        {
            startPosition = transform.position;
            targetPosition = target;
            startRotation = transform.rotation;
            endRotation = targetRotation;
            startHeight = heightStart;
            endHeight = heightEnd;
            speed = moveSpeed;
            isActive = true;
            onArrival = arrivalCallback;
            journeyStartTime = Time.time;

            // Calculate distances
            float horizontalDistance = Vector3.Distance(startPosition, targetPosition);
            float verticalDistance = Mathf.Abs(endHeight - startHeight);

            // Determine movement phases based on height change
            if (verticalDistance < 0.01f)
            {
                // Same height - single phase horizontal movement
                currentPhase = MovementPhase.SinglePhase;
                journeyLength = horizontalDistance;
                phase1Duration = 0f;
                phase2Duration = 0f;
            }
            else if (endHeight > startHeight)
            {
                // Object→Sphere: Vertical rise THEN horizontal travel
                currentPhase = MovementPhase.VerticalPhase;
                phase1Duration = verticalDistance / speed;
                phase2Duration = horizontalDistance / speed;
                journeyLength = verticalDistance + horizontalDistance;

                // After vertical rise, stay at same angular position
                intermediatePosition = startPosition;
                intermediateRotation = startRotation;
            }
            else
            {
                // Sphere→Object: Horizontal travel THEN vertical descent
                currentPhase = MovementPhase.HorizontalPhase;
                phase1Duration = horizontalDistance / speed;
                phase2Duration = verticalDistance / speed;
                journeyLength = horizontalDistance + verticalDistance;

                // After horizontal travel, arrive at target angular position
                intermediatePosition = targetPosition;
                intermediateRotation = endRotation;
            }
        }

        void Update()
        {
            if (!isActive) return;

            float elapsedTime = Time.time - journeyStartTime;

            // Handle movement based on current phase
            if (currentPhase == MovementPhase.SinglePhase)
            {
                // Single phase: constant height horizontal movement
                float progress = Mathf.Clamp01(elapsedTime * speed / journeyLength);

                // Interpolate angular position
                Vector3 basePosition = Vector3.Lerp(startPosition, targetPosition, progress);
                transform.rotation = Quaternion.Slerp(startRotation, endRotation, progress);

                // Apply constant height
                Vector3 direction = basePosition.normalized;
                transform.position = direction * (CircuitConstants.WORLD_RADIUS + startHeight);

                // Check completion
                if (progress >= 1f)
                {
                    CompleteJourney();
                }
            }
            else if (currentPhase == MovementPhase.VerticalPhase)
            {
                // Vertical phase: could be rise (Object→Sphere phase 1) or descent (Sphere→Object phase 2)
                bool isRisingPhase = (endHeight > startHeight);
                float phaseDuration = isRisingPhase ? phase1Duration : phase2Duration;
                float phaseProgress = Mathf.Clamp01(elapsedTime / phaseDuration);

                if (phaseProgress >= 1f)
                {
                    if (isRisingPhase)
                    {
                        // Object→Sphere: after rising, transition to horizontal phase
                        currentPhase = MovementPhase.HorizontalPhase;
                        journeyStartTime = Time.time;  // Reset timer for phase 2
                    }
                    else
                    {
                        // Sphere→Object: descent complete, journey done
                        CompleteJourney();
                    }
                }
                else
                {
                    // Interpolate height only, keep angular position constant
                    // Always lerp from startHeight to endHeight (works for both rising and descending)
                    float currentHeight = Mathf.Lerp(startHeight, endHeight, phaseProgress);
                    Vector3 direction = (isRisingPhase ? startPosition : targetPosition).normalized;
                    transform.position = direction * (CircuitConstants.WORLD_RADIUS + currentHeight);
                    transform.rotation = isRisingPhase ? startRotation : endRotation;
                }
            }
            else if (currentPhase == MovementPhase.HorizontalPhase)
            {
                // Horizontal phase: could be phase 1 (Sphere→Object) or phase 2 (Object→Sphere)
                bool isSphereToObject = (endHeight < startHeight);
                bool isPhase1 = isSphereToObject;  // Sphere→Object does horizontal first
                float phaseDuration = isPhase1 ? phase1Duration : phase2Duration;
                float phaseProgress = Mathf.Clamp01(elapsedTime / phaseDuration);

                if (phaseProgress >= 1f)
                {
                    if (isSphereToObject)
                    {
                        // Sphere→Object: after horizontal, now descend
                        currentPhase = MovementPhase.VerticalPhase;
                        journeyStartTime = Time.time;
                    }
                    else
                    {
                        // Object→Sphere: horizontal complete, journey done
                        CompleteJourney();
                    }
                }
                else
                {
                    // Interpolate position horizontally at constant height
                    Vector3 phaseStart = isPhase1 ? startPosition : intermediatePosition;
                    Vector3 phaseEnd = isPhase1 ? intermediatePosition : targetPosition;
                    Quaternion rotStart = isPhase1 ? startRotation : intermediateRotation;
                    Quaternion rotEnd = isPhase1 ? intermediateRotation : endRotation;
                    float currentHeight = isPhase1 ? startHeight : endHeight;

                    Vector3 basePosition = Vector3.Lerp(phaseStart, phaseEnd, phaseProgress);
                    transform.rotation = Quaternion.Slerp(rotStart, rotEnd, phaseProgress);
                    Vector3 direction = basePosition.normalized;
                    transform.position = direction * (CircuitConstants.WORLD_RADIUS + currentHeight);
                }
            }
            else if (currentPhase == MovementPhase.Complete)
            {
                // Already completed, do nothing
            }
        }

        private void CompleteJourney()
        {
            isActive = false;
            currentPhase = MovementPhase.Complete;

            // Snap to final position with correct height and rotation
            Vector3 finalDirection = targetPosition.normalized;
            transform.position = finalDirection * (CircuitConstants.WORLD_RADIUS + endHeight);
            transform.rotation = endRotation;

            // Call arrival callback before destroying
            onArrival?.Invoke();

            Destroy(gameObject);
        }
    }
}
