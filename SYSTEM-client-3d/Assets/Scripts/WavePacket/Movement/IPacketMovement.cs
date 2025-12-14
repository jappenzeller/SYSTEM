using UnityEngine;
using System;

namespace SYSTEM.WavePacket.Movement
{
    /// <summary>
    /// Unified interface for all wave packet movement types.
    /// Abstracts over server-driven (sources) and client-driven (transfers/mining) movement patterns.
    /// </summary>
    public interface IPacketMovement
    {
        /// <summary>Current movement state.</summary>
        MovementState CurrentState { get; }

        /// <summary>True when movement has reached completion.</summary>
        bool IsComplete { get; }

        /// <summary>True when movement is actively processing.</summary>
        bool IsActive { get; }

        /// <summary>Current world position of the packet.</summary>
        Vector3 CurrentPosition { get; }

        /// <summary>Current rotation of the packet.</summary>
        Quaternion CurrentRotation { get; }

        /// <summary>What happens when movement completes.</summary>
        CompletionBehavior CompletionBehavior { get; }

        /// <summary>Fired when movement state changes.</summary>
        event Action<IPacketMovement> OnStateChanged;

        /// <summary>Fired when movement reaches completion.</summary>
        event Action<IPacketMovement> OnMovementComplete;

        /// <summary>Start movement processing.</summary>
        void StartMovement();

        /// <summary>Stop movement processing.</summary>
        void StopMovement();

        /// <summary>Process one frame of movement (called automatically by base class).</summary>
        void Tick(float deltaTime);
    }

    /// <summary>
    /// Movement states for wave packets.
    /// Supports both server-driven (sources) and client-driven (trajectories) patterns.
    /// </summary>
    public enum MovementState
    {
        /// <summary>Movement not active.</summary>
        Inactive,

        /// <summary>Movement initializing.</summary>
        Initializing,

        /// <summary>Moving horizontally on sphere surface.</summary>
        MovingHorizontal,

        /// <summary>Moving vertically (rising or descending).</summary>
        MovingVertical,

        /// <summary>Final approach to destination.</summary>
        Arriving,

        /// <summary>Movement completed.</summary>
        Complete
    }

    /// <summary>
    /// What happens when movement completes.
    /// </summary>
    public enum CompletionBehavior
    {
        /// <summary>GameObject persists after movement (sources).</summary>
        Persist,

        /// <summary>GameObject is destroyed after movement (transfers/mining).</summary>
        Destroy,

        /// <summary>Custom callback decides behavior.</summary>
        Callback
    }
}
