using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SpacetimeDB;
using SpacetimeDB.Types;

namespace SYSTEM.Game
{
    /// <summary>
    /// Manages visualization of energy packet transfers between player inventory and storage devices.
    /// Subscribes to PacketTransfer table and uses TransferVisualController for wave packet rendering.
    /// </summary>
    public class TransferVisualizationManager : MonoBehaviour
    {
        [Header("Visualization Settings")]
        [SerializeField] private TransferVisualController transferVisualController;
        [SerializeField] private float packetTravelSpeed = 5f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private GameManager gameManager;
        private Dictionary<ulong, bool> activeTransfers = new Dictionary<ulong, bool>();

        private void Awake()
        {
            gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                UnityEngine.Debug.LogError("[TransferVisualization] GameManager instance not found");
            }

            // Auto-create TransferVisualController if not assigned
            if (transferVisualController == null)
            {
                GameObject controllerObj = new GameObject("TransferVisualController");
                controllerObj.transform.SetParent(transform);
                transferVisualController = controllerObj.AddComponent<TransferVisualController>();

                if (showDebugLogs)
                {
                    UnityEngine.Debug.Log("[TransferVisualization] Auto-created TransferVisualController");
                }
            }
        }

        private void OnEnable()
        {
            if (gameManager != null && GameManager.Conn != null)
            {
                SubscribeToTransfers();
            }
        }

        private void OnDisable()
        {
            if (gameManager != null && GameManager.Conn != null)
            {
                UnsubscribeFromTransfers();
            }

            // Stop all active transfer animations
            if (transferVisualController != null)
            {
                foreach (var transferId in activeTransfers.Keys)
                {
                    transferVisualController.StopTransfer(transferId);
                }
            }
            activeTransfers.Clear();
        }

        private void SubscribeToTransfers()
        {
            GameManager.Conn.Db.PacketTransfer.OnInsert += OnTransferInserted;
            GameManager.Conn.Db.PacketTransfer.OnUpdate += OnTransferUpdated;
            GameManager.Conn.Db.PacketTransfer.OnDelete += OnTransferDeleted;

            if (showDebugLogs)
            {
                UnityEngine.Debug.Log("[TransferVisualization] Subscribed to PacketTransfer table");
            }
        }

        private void UnsubscribeFromTransfers()
        {
            GameManager.Conn.Db.PacketTransfer.OnInsert -= OnTransferInserted;
            GameManager.Conn.Db.PacketTransfer.OnUpdate -= OnTransferUpdated;
            GameManager.Conn.Db.PacketTransfer.OnDelete -= OnTransferDeleted;

            if (showDebugLogs)
            {
                UnityEngine.Debug.Log("[TransferVisualization] Unsubscribed from PacketTransfer table");
            }
        }

        private void OnTransferInserted(EventContext ctx, PacketTransfer transfer)
        {
            if (showDebugLogs)
            {
                UnityEngine.Debug.Log($"[TransferVisualization] Transfer {transfer.TransferId} inserted: {transfer.PacketCount} packets");
            }

            // Only visualize transfers for local player
            var localPlayer = GetLocalPlayer();
            if (localPlayer != null && transfer.PlayerId == localPlayer.PlayerId)
            {
                StartTransferAnimation(transfer);
            }
        }

        private void OnTransferUpdated(EventContext ctx, PacketTransfer oldTransfer, PacketTransfer newTransfer)
        {
            if (showDebugLogs)
            {
                UnityEngine.Debug.Log($"[TransferVisualization] Transfer {newTransfer.TransferId} updated - Completed: {newTransfer.Completed}");
            }

            // If transfer completed, clean up
            if (newTransfer.Completed && activeTransfers.ContainsKey(newTransfer.TransferId))
            {
                if (transferVisualController != null)
                {
                    transferVisualController.StopTransfer(newTransfer.TransferId);
                }
                activeTransfers.Remove(newTransfer.TransferId);
            }
        }

        private void OnTransferDeleted(EventContext ctx, PacketTransfer transfer)
        {
            if (showDebugLogs)
            {
                UnityEngine.Debug.Log($"[TransferVisualization] Transfer {transfer.TransferId} deleted");
            }

            // Stop animation if running
            if (activeTransfers.ContainsKey(transfer.TransferId))
            {
                if (transferVisualController != null)
                {
                    transferVisualController.StopTransfer(transfer.TransferId);
                }
                activeTransfers.Remove(transfer.TransferId);
            }
        }

        private void StartTransferAnimation(PacketTransfer transfer)
        {
            if (activeTransfers.ContainsKey(transfer.TransferId))
            {
                UnityEngine.Debug.LogWarning($"[TransferVisualization] Transfer {transfer.TransferId} already animating");
                return;
            }

            if (transferVisualController == null)
            {
                UnityEngine.Debug.LogError("[TransferVisualization] TransferVisualController is null!");
                return;
            }

            if (showDebugLogs)
            {
                UnityEngine.Debug.Log($"[TransferVisualization] Starting animation for transfer {transfer.TransferId}");
                UnityEngine.Debug.Log($"[TransferVisualization] Route has {transfer.RouteWaypoints.Count} waypoints");
            }

            // Convert route waypoints to Unity Vector3 array
            Vector3[] waypoints = new Vector3[transfer.RouteWaypoints.Count];
            for (int i = 0; i < transfer.RouteWaypoints.Count; i++)
            {
                waypoints[i] = DbVector3ToUnity(transfer.RouteWaypoints[i]);

                if (showDebugLogs)
                {
                    UnityEngine.Debug.Log($"[TransferVisualization] Waypoint {i}: {waypoints[i]}");
                }
            }

            // Convert composition to array
            WavePacketSample[] composition = transfer.Composition.ToArray();

            // Start the transfer animation using TransferVisualController
            transferVisualController.StartTransferAnimation(
                transfer.TransferId,
                composition,
                waypoints,
                packetTravelSpeed,
                () => OnTransferAnimationComplete(transfer.TransferId)
            );

            // Mark as active
            activeTransfers[transfer.TransferId] = true;
        }

        private void OnTransferAnimationComplete(ulong transferId)
        {
            if (showDebugLogs)
            {
                UnityEngine.Debug.Log($"[TransferVisualization] Transfer {transferId} animation complete");
            }

            // Call server reducer to complete the transfer
            if (gameManager != null && GameManager.Conn != null)
            {
                if (showDebugLogs)
                {
                    UnityEngine.Debug.Log($"[TransferVisualization] Calling CompleteTransfer reducer for {transferId}");
                }

                GameManager.Conn.Reducers.CompleteTransfer(transferId);
            }

            // Remove from active transfers
            activeTransfers.Remove(transferId);
        }

        private Vector3 DbVector3ToUnity(DbVector3 dbVec)
        {
            return new Vector3(dbVec.X, dbVec.Y, dbVec.Z);
        }

        private Player GetLocalPlayer()
        {
            if (gameManager == null || GameManager.Conn == null) return null;

            foreach (var player in GameManager.Conn.Db.Player.Iter())
            {
                if (player.Identity == GameManager.Conn.Identity)
                {
                    return player;
                }
            }
            return null;
        }
    }
}

