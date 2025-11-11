using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SpacetimeDB;
using SpacetimeDB.Types;
using SYSTEM.Debug;
using SYSTEM.WavePacket;

namespace SYSTEM.Game
{
    /// <summary>
    /// Manages visualization of energy packet transfers between player inventory and storage devices.
    /// SERVER-DRIVEN: Visualizes ALL players' transfers based on server state changes (multiplayer-safe).
    /// Triggers animations when transfers move from PlayerPulse -> InTransit (server game loop controls timing).
    /// Uses ExtractionVisualController for unified wave packet rendering across mining and transfers.
    /// </summary>
    public class TransferVisualizationManager : MonoBehaviour
    {
        [Header("Visualization Settings")]
        [SerializeField] private ExtractionVisualController extractionVisualController;
        [SerializeField] private float packetTravelSpeed = 5f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private GameManager gameManager;
        private Dictionary<ulong, GameObject> activePacketVisuals = new Dictionary<ulong, GameObject>();
        private Dictionary<ulong, string> lastProcessedState = new Dictionary<ulong, string>();
        private Dictionary<ulong, bool> completedTransfers = new Dictionary<ulong, bool>();

        private void Awake()
        {
            gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                SystemDebug.LogError(SystemDebug.Category.Network, "GameManager instance not found");
            }

            // Find ExtractionVisualController if not assigned
            if (extractionVisualController == null)
            {
                extractionVisualController = FindFirstObjectByType<ExtractionVisualController>();
                if (extractionVisualController == null)
                {
                    SystemDebug.LogError(SystemDebug.Category.Network, "ExtractionVisualController not found in scene!");
                }
                else if (showDebugLogs)
                {
                    SystemDebug.Log(SystemDebug.Category.Network, "Found ExtractionVisualController for unified packet visualization");
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

            // Destroy all active packet visuals
            foreach (var packet in activePacketVisuals.Values)
            {
                if (packet != null)
                {
                    Destroy(packet);
                }
            }
            activePacketVisuals.Clear();
            lastProcessedState.Clear();
            completedTransfers.Clear();
        }

        private void SubscribeToTransfers()
        {
            GameEventBus.Instance.Subscribe<PacketTransferUpdatedEvent>(OnTransferUpdatedEvent);
            GameEventBus.Instance.Subscribe<PacketTransferDeletedEvent>(OnTransferDeletedEvent);

            if (showDebugLogs)
            {
                SystemDebug.Log(SystemDebug.Category.Network, "Subscribed to PacketTransfer events via GameEventBus");
            }
        }

        private void UnsubscribeFromTransfers()
        {
            GameEventBus.Instance.Unsubscribe<PacketTransferUpdatedEvent>(OnTransferUpdatedEvent);
            GameEventBus.Instance.Unsubscribe<PacketTransferDeletedEvent>(OnTransferDeletedEvent);

            if (showDebugLogs)
            {
                SystemDebug.Log(SystemDebug.Category.Network, "Unsubscribed from PacketTransfer events");
            }
        }

        /// <summary>
        /// Server state change handler - triggers visualization for all transfer leg changes.
        /// Fast pulse (2s): PlayerPulse -> InTransit (player -> first sphere)
        /// Slow pulse (5s): InTransit leg advances (sphere -> sphere or sphere -> storage)
        /// IMPORTANT: Visualizes ALL players' transfers, not just local player (multiplayer-safe).
        /// </summary>
        private void OnTransferUpdatedEvent(PacketTransferUpdatedEvent evt)
        {
            OnTransferUpdated(evt.OldTransfer, evt.NewTransfer);
        }

        private void OnTransferUpdated(PacketTransfer oldTransfer, PacketTransfer newTransfer)
        {
            SystemDebug.Log(SystemDebug.Category.Network,
                $"[TransferViz] OnTransferUpdated: Transfer {newTransfer.TransferId} | Old: {oldTransfer.CurrentLegType} leg {oldTransfer.CurrentLeg} | New: {newTransfer.CurrentLegType} leg {newTransfer.CurrentLeg} | Completed: {newTransfer.Completed}");

            // REFACTORED: State-based visualization triggered by CurrentLegType transitions
            // Detects when transfer enters a "traveling" state (departure), ignoring redundant updates

            // Check if this is a meaningful state change (ignore duplicate events)
            string lastState = lastProcessedState.TryGetValue(newTransfer.TransferId, out string prev) ? prev : "";
            bool stateChanged = newTransfer.CurrentLegType != lastState;

            if (stateChanged)
            {
                // Update last processed state
                lastProcessedState[newTransfer.TransferId] = newTransfer.CurrentLegType;

                // Trigger visualization when entering a traveling state (departure)
                bool isNewDeparture =
                    newTransfer.CurrentLegType == "ObjectToSphere" ||
                    newTransfer.CurrentLegType == "SphereToSphere" ||
                    newTransfer.CurrentLegType == "SphereToObject";

                if (isNewDeparture)
                {
                    SystemDebug.Log(SystemDebug.Category.Network,
                        $"[TransferViz] DEPARTURE DETECTED: Transfer {newTransfer.TransferId} entering {newTransfer.CurrentLegType}");
                    StartLegVisualization(newTransfer);
                }
                else if (newTransfer.CurrentLegType == "ArrivedAtSphere")
                {
                    // Packet arrived at sphere - no visual action needed (server handles delay)
                    SystemDebug.Log(SystemDebug.Category.Network,
                        $"[TransferViz] ARRIVAL: Transfer {newTransfer.TransferId} reached sphere, waiting for next pulse");
                }
                else if (newTransfer.CurrentLegType == "PendingAtObject")
                {
                    // Transfer created but not yet departed - no visual needed
                    SystemDebug.Log(SystemDebug.Category.Network,
                        $"[TransferViz] PENDING: Transfer {newTransfer.TransferId} waiting at source for departure pulse");
                }
            }
            else
            {
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[TransferViz] NO TRIGGER: Transfer {newTransfer.TransferId} - redundant update, state unchanged ({newTransfer.CurrentLegType})");
            }

            // Mark transfer as completed but don't destroy visual immediately
            // Visual will be cleaned up when it finishes traveling in OnPacketArrivedAtDestination
            if (newTransfer.Completed && !completedTransfers.ContainsKey(newTransfer.TransferId))
            {
                completedTransfers[newTransfer.TransferId] = true;
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[TransferViz] MARKED COMPLETE: Transfer {newTransfer.TransferId}, waiting for visual to finish");
            }
        }

        private void OnTransferDeletedEvent(PacketTransferDeletedEvent evt)
        {
            OnTransferDeleted(evt.Transfer);
        }

        private void OnTransferDeleted(PacketTransfer transfer)
        {
            if (showDebugLogs)
            {
                SystemDebug.Log(SystemDebug.Category.Network, $"Transfer {transfer.TransferId} deleted - cleaning up visual");
            }
            CleanupPacketVisual(transfer.TransferId);
        }

        /// <summary>
        /// Starts visualization for the current transfer leg using unified ExtractionVisualController.
        /// SERVER TIMING: Animation starts immediately when called (server already waited for pulse interval).
        /// Leg 0: Player -> First Sphere (waypoints[0] -> waypoints[1])
        /// Leg 1+: Sphere -> Sphere or Sphere -> Storage (waypoints[leg] -> waypoints[leg+1] or storage position)
        /// </summary>
        private void StartLegVisualization(PacketTransfer transfer)
        {
            SystemDebug.Log(SystemDebug.Category.Network,
                $"[TransferViz] StartLegVisualization: Transfer {transfer.TransferId} leg {transfer.CurrentLeg}");

            if (extractionVisualController == null)
            {
                SystemDebug.LogError(SystemDebug.Category.Network, "[TransferViz] ExtractionVisualController is null - cannot visualize transfer");
                return;
            }

            // Clean up old visual if exists (leg advanced)
            if (activePacketVisuals.ContainsKey(transfer.TransferId))
            {
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[TransferViz] Cleaning up old visual for transfer {transfer.TransferId}");
                CleanupPacketVisual(transfer.TransferId);
            }

            Vector3 startPos;
            Vector3 endPos;
            int currentLeg = (int)transfer.CurrentLeg;

            SystemDebug.Log(SystemDebug.Category.Network,
                $"[TransferViz] Transfer {transfer.TransferId}: route has {transfer.RouteSpireIds.Count} spires, {transfer.RouteWaypoints.Count} waypoints");

            // Determine start and end positions based on current leg
            if (currentLeg == 0)
            {
                // Leg 0: Player -> First Sphere
                if (transfer.RouteWaypoints.Count < 2)
                {
                    SystemDebug.LogWarning(SystemDebug.Category.Network,
                        $"[TransferViz] Transfer {transfer.TransferId} has insufficient waypoints ({transfer.RouteWaypoints.Count})");
                    return;
                }
                startPos = DbVector3ToUnity(transfer.RouteWaypoints[0]);
                endPos = DbVector3ToUnity(transfer.RouteWaypoints[1]);
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[TransferViz] Leg 0: Player -> Sphere | {startPos} -> {endPos}");
            }
            else
            {
                // Leg 1+: Sphere -> Sphere or Sphere -> Storage
                // Start from current sphere waypoint
                if (currentLeg >= transfer.RouteWaypoints.Count)
                {
                    SystemDebug.LogWarning(SystemDebug.Category.Network,
                        $"[TransferViz] Transfer {transfer.TransferId} leg {currentLeg} out of waypoints range (have {transfer.RouteWaypoints.Count} waypoints)");
                    return;
                }
                startPos = DbVector3ToUnity(transfer.RouteWaypoints[currentLeg]);

                // End at next waypoint or storage device position
                if (currentLeg + 1 < transfer.RouteWaypoints.Count)
                {
                    // Sphere -> Sphere
                    endPos = DbVector3ToUnity(transfer.RouteWaypoints[currentLeg + 1]);
                    SystemDebug.Log(SystemDebug.Category.Network,
                        $"[TransferViz] Leg {currentLeg}: Sphere -> Sphere | {startPos} -> {endPos}");
                }
                else
                {
                    // Final leg: Sphere -> Storage
                    // Get storage device position from database
                    var storage = GameManager.Conn.Db.StorageDevice.DeviceId.Find(transfer.DestinationDeviceId);
                    if (storage == null)
                    {
                        SystemDebug.LogWarning(SystemDebug.Category.Network,
                            $"[TransferViz] Transfer {transfer.TransferId} destination storage {transfer.DestinationDeviceId} not found");
                        return;
                    }
                    endPos = DbVector3ToUnity(storage.Position);
                    SystemDebug.Log(SystemDebug.Category.Network,
                        $"[TransferViz] Leg {currentLeg} (FINAL): Sphere -> Storage | {startPos} -> {endPos}");
                }
            }

            // Spawn flying packet using unified system (same as mining)
            SystemDebug.Log(SystemDebug.Category.Network,
                $"[TransferViz] Spawning packet visual for transfer {transfer.TransferId}");
            GameObject packet = extractionVisualController.SpawnFlyingPacket(
                transfer.Composition.ToArray(),
                startPos,
                endPos,
                packetTravelSpeed,
                () => OnPacketArrivedAtDestination(transfer.TransferId)
            );

            if (packet != null)
            {
                activePacketVisuals[transfer.TransferId] = packet;
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[TransferViz] Packet visual spawned successfully for transfer {transfer.TransferId}");
            }
            else
            {
                SystemDebug.LogError(SystemDebug.Category.Network,
                    $"[TransferViz] Failed to spawn packet visual for transfer {transfer.TransferId}");
            }
        }

        /// <summary>
        /// Called when packet visual reaches destination for current leg.
        /// If transfer is marked complete, performs final cleanup. Otherwise, waits for next departure pulse.
        /// </summary>
        private void OnPacketArrivedAtDestination(ulong transferId)
        {
            if (showDebugLogs)
            {
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"Transfer {transferId} packet visual arrived at destination");
            }

            // Check if this transfer is marked as completed by server
            if (completedTransfers.TryGetValue(transferId, out bool isComplete) && isComplete)
            {
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[TransferViz] Transfer {transferId} visual completed, performing final cleanup");
                completedTransfers.Remove(transferId);
            }

            CleanupPacketVisual(transferId);
        }

        private void CleanupPacketVisual(ulong transferId)
        {
            if (activePacketVisuals.TryGetValue(transferId, out GameObject packet))
            {
                if (packet != null)
                {
                    Destroy(packet);
                }
                activePacketVisuals.Remove(transferId);
            }

            // Clean up tracking dictionaries
            lastProcessedState.Remove(transferId);
            completedTransfers.Remove(transferId);
        }

        private Vector3 DbVector3ToUnity(DbVector3 dbVec)
        {
            return new Vector3(dbVec.X, dbVec.Y, dbVec.Z);
        }
    }
}

