using UnityEngine;
using System;
using SYSTEM.Circuits;
using SYSTEM.Debug;

namespace SYSTEM.WavePacket.Movement
{
    /// <summary>
    /// Abstract base for all packet movement implementations.
    /// Provides shared spherical coordinate math and state management.
    /// </summary>
    public abstract class PacketMovementBase : MonoBehaviour, IPacketMovement
    {
        [Header("Movement Settings")]
        [SerializeField] protected float speed = 5f;
        [SerializeField] protected float interpolationSpeed = 10f;
        [SerializeField] protected CompletionBehavior completionBehavior = CompletionBehavior.Destroy;

        // State
        protected MovementState currentState = MovementState.Inactive;
        protected bool isActive = false;

        // Position tracking
        protected Vector3 startPosition;
        protected Vector3 targetPosition;
        protected Quaternion startRotation;
        protected Quaternion targetRotation;
        protected float startHeight;
        protected float targetHeight;

        // Events
        public event Action<IPacketMovement> OnStateChanged;
        public event Action<IPacketMovement> OnMovementComplete;

        // Interface implementation
        public MovementState CurrentState => currentState;
        public bool IsComplete => currentState == MovementState.Complete;
        public bool IsActive => isActive;
        public Vector3 CurrentPosition => transform.position;
        public Quaternion CurrentRotation => transform.rotation;
        public CompletionBehavior CompletionBehavior => completionBehavior;

        // Abstract methods for derived classes
        protected abstract void OnStartMovement();
        protected abstract void OnTickMovement(float deltaTime);
        protected abstract void OnCompleteMovement();

        #region State Management

        /// <summary>
        /// Set movement state and fire event if changed.
        /// </summary>
        protected void SetState(MovementState newState)
        {
            if (currentState != newState)
            {
                var oldState = currentState;
                currentState = newState;

                SystemDebug.Log(SystemDebug.Category.WavePacketSystem,
                    $"[Movement] {gameObject.name} State: {oldState} -> {newState}");

                OnStateChanged?.Invoke(this);
            }
        }

        #endregion

        #region Spherical Coordinate Math

        /// <summary>
        /// Get surface normal (direction from world center) for a position.
        /// </summary>
        protected Vector3 GetSurfaceNormal(Vector3 position)
        {
            return position.normalized;
        }

        /// <summary>
        /// Get rotation to align object's up vector with sphere surface normal.
        /// </summary>
        protected Quaternion GetSurfaceOrientation(Vector3 position)
        {
            return Quaternion.FromToRotation(Vector3.up, GetSurfaceNormal(position));
        }

        /// <summary>
        /// Adjust position to be at specified height above sphere surface.
        /// </summary>
        protected Vector3 AdjustHeightAboveSurface(Vector3 position, float height)
        {
            return position.normalized * (CircuitConstants.WORLD_RADIUS + height);
        }

        /// <summary>
        /// Apply position at height and surface-aligned rotation.
        /// </summary>
        protected void ApplyPositionAndRotation(Vector3 basePosition, float height)
        {
            transform.position = AdjustHeightAboveSurface(basePosition, height);
            transform.rotation = GetSurfaceOrientation(basePosition);
        }

        /// <summary>
        /// Get current height above sphere surface.
        /// </summary>
        protected float GetCurrentHeight()
        {
            return transform.position.magnitude - CircuitConstants.WORLD_RADIUS;
        }

        /// <summary>
        /// Constrain position to sphere surface (height = 0).
        /// </summary>
        protected Vector3 ConstrainToSphereSurface(Vector3 position)
        {
            return position.normalized * CircuitConstants.WORLD_RADIUS;
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Start movement processing.
        /// </summary>
        public virtual void StartMovement()
        {
            isActive = true;
            SetState(MovementState.Initializing);
            OnStartMovement();
        }

        /// <summary>
        /// Stop movement processing without completion.
        /// </summary>
        public virtual void StopMovement()
        {
            isActive = false;
            SetState(MovementState.Inactive);
        }

        /// <summary>
        /// Process one frame of movement.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!isActive) return;
            OnTickMovement(deltaTime);
        }

        /// <summary>
        /// Unity Update - delegates to Tick.
        /// </summary>
        protected virtual void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Complete movement and handle completion behavior.
        /// </summary>
        protected void CompleteMovement()
        {
            SetState(MovementState.Complete);
            isActive = false;

            // Invoke completion event
            OnMovementComplete?.Invoke(this);

            // Call derived class completion handler
            OnCompleteMovement();

            // Handle completion behavior
            switch (completionBehavior)
            {
                case CompletionBehavior.Destroy:
                    Destroy(gameObject);
                    break;

                case CompletionBehavior.Persist:
                    // Do nothing - object stays in scene
                    break;

                case CompletionBehavior.Callback:
                    // Callback already invoked via OnMovementComplete
                    break;
            }
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Set movement speed.
        /// </summary>
        public void SetSpeed(float newSpeed)
        {
            speed = newSpeed;
        }

        /// <summary>
        /// Set interpolation smoothing speed.
        /// </summary>
        public void SetInterpolationSpeed(float newSpeed)
        {
            interpolationSpeed = newSpeed;
        }

        /// <summary>
        /// Set completion behavior.
        /// </summary>
        public void SetCompletionBehavior(CompletionBehavior behavior)
        {
            completionBehavior = behavior;
        }

        #endregion
    }
}
