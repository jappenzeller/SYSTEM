using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SpacetimeDB;
using SpacetimeDB.Types;

namespace SYSTEM.Game
{
    /// <summary>
    /// Manages visualization of energy packet transfers between player inventory and storage devices.
    /// Subscribes to PacketTransfer table and animates wave packets traveling along routes through spires.
    /// </summary>
    public class TransferVisualizationManager : MonoBehaviour
    {
        [Header("Visualization Settings")]
        [SerializeField] private GameObject wavePacketDisplayPrefab;
        [SerializeField] private float packetTravelSpeed = 5f;
        [SerializeField] private float spireFlashDuration = 0.5f;
        [SerializeField] private Color spireFlashColor = Color.cyan;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private GameManager gameManager;
        private Dictionary<ulong, Coroutine> activeTransfers = new Dictionary<ulong, Coroutine>();

        private void Awake()
        {
            gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                UnityEngine.Debug.LogError("[TransferVisualization] GameManager instance not found");
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

            // Stop all active animations
            foreach (var coroutine in activeTransfers.Values)
            {
                if (coroutine != null)
                {
                    StopCoroutine(coroutine);
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

            // If transfer completed, stop animation
            if (newTransfer.Completed && activeTransfers.ContainsKey(newTransfer.TransferId))
            {
                StopCoroutine(activeTransfers[newTransfer.TransferId]);
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
                StopCoroutine(activeTransfers[transfer.TransferId]);
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

            var coroutine = StartCoroutine(AnimateTransfer(transfer));
            activeTransfers[transfer.TransferId] = coroutine;
        }

        private IEnumerator AnimateTransfer(PacketTransfer transfer)
        {
            if (showDebugLogs)
            {
                UnityEngine.Debug.Log($"[TransferVisualization] Starting animation for transfer {transfer.TransferId}");
                UnityEngine.Debug.Log($"[TransferVisualization] Route has {transfer.RouteWaypoints.Count} waypoints");
            }

            // Create simple sphere for visualization (WavePacketDisplay not yet available)
            GameObject packetDisplay = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            packetDisplay.transform.localScale = Vector3.one * 2f;

            // Color based on composition
            var renderer = packetDisplay.GetComponent<Renderer>();
            if (renderer != null && transfer.Composition.Count > 0)
            {
                // Use first frequency as color
                renderer.material.color = GetColorForFrequency(transfer.Composition[0].Frequency);
            }

            // Animate along route waypoints
            for (int i = 0; i < transfer.RouteWaypoints.Count - 1; i++)
            {
                Vector3 startPos = DbVector3ToUnity(transfer.RouteWaypoints[i]);
                Vector3 endPos = DbVector3ToUnity(transfer.RouteWaypoints[i + 1]);

                if (showDebugLogs)
                {
                    UnityEngine.Debug.Log($"[TransferVisualization] Waypoint {i} -> {i + 1}: {startPos} -> {endPos}");
                }

                // Flash spire at start of segment (if not first waypoint)
                if (i > 0 && i - 1 < transfer.RouteSpireIds.Count)
                {
                    FlashSpire(transfer.RouteSpireIds[i - 1]);
                }

                // Animate packet movement
                float distance = Vector3.Distance(startPos, endPos);
                float duration = distance / packetTravelSpeed;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    packetDisplay.transform.position = Vector3.Lerp(startPos, endPos, t);
                    yield return null;
                }

                packetDisplay.transform.position = endPos;
            }

            // Flash final spire
            if (transfer.RouteSpireIds.Count > 0)
            {
                FlashSpire(transfer.RouteSpireIds[transfer.RouteSpireIds.Count - 1]);
            }

            // Destroy display
            Destroy(packetDisplay);

            // Call complete_transfer reducer
            if (gameManager != null && GameManager.Conn != null)
            {
                if (showDebugLogs)
                {
                    UnityEngine.Debug.Log($"[TransferVisualization] Calling complete_transfer for {transfer.TransferId}");
                }

                GameManager.Conn.Reducers.CompleteTransfer(transfer.TransferId);
            }

            // Remove from active transfers
            activeTransfers.Remove(transfer.TransferId);

            if (showDebugLogs)
            {
                UnityEngine.Debug.Log($"[TransferVisualization] Transfer {transfer.TransferId} animation complete");
            }
        }

        private void FlashSpire(ulong spireId)
        {
            // Find spire in database
            var spire = GameManager.Conn.Db.EnergySpire.SpireId.Find(spireId);
            if (spire == null)
            {
                UnityEngine.Debug.LogWarning($"[TransferVisualization] Spire {spireId} not found");
                return;
            }

            // TODO: Find corresponding GameObject and flash it
            // For now, just log
            if (showDebugLogs)
            {
                UnityEngine.Debug.Log($"[TransferVisualization] Flashing spire {spireId} at {spire.SpherePosition}");
            }

            // Future implementation: Find spire GameObject by position and apply flash effect
            // This would require a SpireVisualizationManager similar to OrbVisualizationManager
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
        private Color GetColorForFrequency(float frequency)
        {
            // Simple color mapping based on frequency
            if (frequency < 0.5f) return Color.red;
            if (frequency < 1.5f) return Color.yellow;
            if (frequency < 2.5f) return Color.green;
            if (frequency < 3.5f) return Color.cyan;
            if (frequency < 4.5f) return Color.blue;
            return Color.magenta;
        }
    }
}

