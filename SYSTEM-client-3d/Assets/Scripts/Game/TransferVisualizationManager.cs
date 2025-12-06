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
    /// Uses WavePacketTransfer prefab directly for transfer packet rendering.
    /// BATCHING SYSTEM: Combines multiple transfers departing together into single visual GameObject.
    /// </summary>
    public class TransferVisualizationManager : MonoBehaviour
    {
        [Header("Visualization Settings")]
        [SerializeField] private GameObject wavePacketTransferPrefab;
        [SerializeField] private float packetTravelSpeed = 5f;
        [SerializeField] private float batchWindowSeconds = 0.1f; // Time window to collect batches

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private GameManager gameManager;
        private Dictionary<ulong, GameObject> activePacketVisuals = new Dictionary<ulong, GameObject>();
        private Dictionary<ulong, string> lastProcessedState = new Dictionary<ulong, string>();
        private Dictionary<ulong, bool> completedTransfers = new Dictionary<ulong, bool>();

        // Batching system
        private Dictionary<BatchKey, TransferBatch> pendingBatches = new Dictionary<BatchKey, TransferBatch>();
        private Dictionary<ulong, BatchKey> transferToBatch = new Dictionary<ulong, BatchKey>();
        private Dictionary<BatchKey, GameObject> batchVisuals = new Dictionary<BatchKey, GameObject>();

        /// <summary>
        /// Key for identifying batches of transfers that depart together
        /// </summary>
        private struct BatchKey
        {
            public Vector3 Source;
            public Vector3 Destination;
            public float DepartureTime;

            public BatchKey(Vector3 source, Vector3 destination, float departureTime)
            {
                Source = source;
                Destination = destination;
                DepartureTime = departureTime;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is BatchKey)) return false;
                BatchKey other = (BatchKey)obj;
                return Vector3.Distance(Source, other.Source) < 0.1f &&
                       Vector3.Distance(Destination, other.Destination) < 0.1f &&
                       Mathf.Abs(DepartureTime - other.DepartureTime) < 0.1f;
            }

            public override int GetHashCode()
            {
                return Source.GetHashCode() ^ Destination.GetHashCode();
            }
        }

        /// <summary>
        /// Batch of transfers departing together from same source to same destination
        /// </summary>
        private class TransferBatch
        {
            public List<ulong> TransferIds = new List<ulong>();
            public List<WavePacketSample> MergedComposition = new List<WavePacketSample>();
            public Vector3 StartPos;
            public Vector3 EndPos;
            public Quaternion StartRotation;
            public Quaternion EndRotation;
            public float StartHeight;
            public float EndHeight;
            public bool Spawned = false;
        }

        private void Awake()
        {
            gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                SystemDebug.LogError(SystemDebug.Category.Network, "GameManager instance not found");
            }

            // Validate WavePacketTransfer prefab assignment
            if (wavePacketTransferPrefab == null)
            {
                SystemDebug.LogError(SystemDebug.Category.Network, "WavePacketTransfer prefab not assigned!");
            }
            else if (showDebugLogs)
            {
                SystemDebug.Log(SystemDebug.Category.Network, "WavePacketTransfer prefab assigned for transfer visualization");
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

            // Destroy all batch visuals
            foreach (var packet in batchVisuals.Values)
            {
                if (packet != null)
                {
                    Destroy(packet);
                }
            }
            batchVisuals.Clear();
            pendingBatches.Clear();
            transferToBatch.Clear();
        }

        private void SubscribeToTransfers()
        {
            GameEventBus.Instance.Subscribe<PacketTransferInsertedEvent>(OnTransferInsertedEvent);
            GameEventBus.Instance.Subscribe<PacketTransferDeletedEvent>(OnTransferDeletedEvent);
            GameEventBus.Instance.Subscribe<PacketTransferUpdatedEvent>(OnTransferUpdatedEvent);

            if (showDebugLogs)
            {
                SystemDebug.Log(SystemDebug.Category.Network, "Subscribed to PacketTransfer events via GameEventBus");
            }
        }

        private void UnsubscribeFromTransfers()
        {
            GameEventBus.Instance.Unsubscribe<PacketTransferInsertedEvent>(OnTransferInsertedEvent);
            GameEventBus.Instance.Unsubscribe<PacketTransferDeletedEvent>(OnTransferDeletedEvent);

            GameEventBus.Instance.Unsubscribe<PacketTransferUpdatedEvent>(OnTransferUpdatedEvent);
            if (showDebugLogs)
            {
                SystemDebug.Log(SystemDebug.Category.Network, "Unsubscribed from PacketTransfer events");
            }
        }

        /// <summary>
        /// Server state change handler - triggers visualization for all transfer leg changes.
        /// INSERT event handles both creation and updates (due to DELETE+INSERT pattern).
        /// Fast pulse (2s): PlayerPulse -> InTransit (player -> first sphere)
        /// Slow pulse (5s): InTransit leg advances (sphere -> sphere or sphere -> storage)
        /// IMPORTANT: Visualizes ALL players' transfers, not just local player (multiplayer-safe).
        /// </summary>
        private void OnTransferInsertedEvent(PacketTransferInsertedEvent evt)
        {
            OnTransferUpdated(null, evt.Transfer);
        }
        private void OnTransferUpdatedEvent(PacketTransferUpdatedEvent evt)
        {
            OnTransferUpdated(evt.OldTransfer, evt.NewTransfer);
        }

        private void OnTransferUpdated(PacketTransfer oldTransfer, PacketTransfer newTransfer)
        {
            SystemDebug.Log(SystemDebug.Category.Network,
                $"[TransferViz] OnTransferUpdated: Transfer {newTransfer.TransferId} | Old: {(oldTransfer != null ? $"{oldTransfer.CurrentLegType} leg {oldTransfer.CurrentLeg}" : "NULL")} | New: {newTransfer.CurrentLegType} leg {newTransfer.CurrentLeg} | Completed: {newTransfer.Completed}");

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

            // Check if this transfer is part of a batch
            if (transferToBatch.TryGetValue(transfer.TransferId, out BatchKey batchKey))
            {
                // Transfer is batched - remove from batch tracking
                transferToBatch.Remove(transfer.TransferId);

                // If batch hasn't spawned yet, remove from pending batch
                if (pendingBatches.TryGetValue(batchKey, out TransferBatch batch) && !batch.Spawned)
                {
                    batch.TransferIds.Remove(transfer.TransferId);
                    SystemDebug.Log(SystemDebug.Category.Network,
                        $"[TransferViz] Removed deleted transfer {transfer.TransferId} from pending batch (now {batch.TransferIds.Count} transfers)");
                }
            }
            else
            {
                // Not batched - clean up individual visual (legacy path, shouldn't happen with batching enabled)
                CleanupPacketVisual(transfer.TransferId);
            }
        }

        /// <summary>
        /// Starts visualization for the current transfer leg using batching system.
        /// SERVER TIMING: Animation starts after batch window (server already waited for pulse interval).
        /// Leg 0: Player -> First Sphere (waypoints[0] -> waypoints[1])
        /// Leg 1+: Sphere -> Sphere or Sphere -> Storage (waypoints[leg] -> waypoints[leg+1] or storage position)
        /// BATCHING: Transfers with same source/destination/time are batched into single visual.
        /// </summary>
        private void StartLegVisualization(PacketTransfer transfer)
        {
            SystemDebug.Log(SystemDebug.Category.Network,
                $"[TransferViz] StartLegVisualization: Transfer {transfer.TransferId} leg {transfer.CurrentLeg}");

            if (wavePacketTransferPrefab == null)
            {
                SystemDebug.LogError(SystemDebug.Category.Network, "[TransferViz] WavePacketTransfer prefab is null - cannot visualize transfer");
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

            // Determine heights based on leg type
            float startHeight = 0f;
            float endHeight = 0f;

            switch (transfer.CurrentLegType)
            {
                case "ObjectToSphere":
                    // Object → Sphere: Rise from height 1 to height 10
                    startHeight = SYSTEM.Circuits.CircuitConstants.OBJECT_PACKET_HEIGHT;
                    endHeight = SYSTEM.Circuits.CircuitConstants.SPHERE_PACKET_HEIGHT;
                    SystemDebug.Log(SystemDebug.Category.Network,
                        $"[TransferViz] ObjectToSphere: height {startHeight} -> {endHeight}");
                    break;

                case "SphereToSphere":
                    // Sphere → Sphere: Constant height 10
                    startHeight = SYSTEM.Circuits.CircuitConstants.SPHERE_PACKET_HEIGHT;
                    endHeight = SYSTEM.Circuits.CircuitConstants.SPHERE_PACKET_HEIGHT;
                    SystemDebug.Log(SystemDebug.Category.Network,
                        $"[TransferViz] SphereToSphere: constant height {startHeight}");
                    break;

                case "SphereToObject":
                    // Sphere → Object: Descend from height 10 to height 1
                    startHeight = SYSTEM.Circuits.CircuitConstants.SPHERE_PACKET_HEIGHT;
                    endHeight = SYSTEM.Circuits.CircuitConstants.OBJECT_PACKET_HEIGHT;
                    SystemDebug.Log(SystemDebug.Category.Network,
                        $"[TransferViz] SphereToObject: height {startHeight} -> {endHeight}");
                    break;

                default:
                    // Fallback: use mining packet height
                    startHeight = SYSTEM.Circuits.CircuitConstants.MINING_PACKET_HEIGHT;
                    endHeight = SYSTEM.Circuits.CircuitConstants.MINING_PACKET_HEIGHT;
                    SystemDebug.LogWarning(SystemDebug.Category.Network,
                        $"[TransferViz] Unknown leg type '{transfer.CurrentLegType}', using default height {startHeight}");
                    break;
            }

            // Calculate rotations for surface orientation (but use base positions, not height-adjusted)
            Vector3 startNormal = SYSTEM.WavePacket.PacketPositionHelper.GetSurfaceNormal(startPos);
            Vector3 endNormal = SYSTEM.WavePacket.PacketPositionHelper.GetSurfaceNormal(endPos);
            Quaternion startRotation = SYSTEM.WavePacket.PacketPositionHelper.GetOrientationForSurface(startNormal);
            Quaternion endRotation = SYSTEM.WavePacket.PacketPositionHelper.GetOrientationForSurface(endNormal);

            // BATCHING SYSTEM: Check if this transfer can be batched with others
            BatchKey batchKey = new BatchKey(startPos, endPos, Time.time);

            // Get or create batch for this departure
            if (!pendingBatches.TryGetValue(batchKey, out TransferBatch batch))
            {
                // New batch - create and start spawn window
                batch = new TransferBatch
                {
                    StartPos = startPos,
                    EndPos = endPos,
                    StartRotation = startRotation,
                    EndRotation = endRotation,
                    StartHeight = startHeight,
                    EndHeight = endHeight
                };
                pendingBatches[batchKey] = batch;

                // Start batch window coroutine
                StartCoroutine(SpawnBatchAfterWindow(batchKey, batchWindowSeconds));

                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[TransferViz] Created new batch for route {startPos} -> {endPos}");
            }

            // Add transfer to batch
            batch.TransferIds.Add(transfer.TransferId);
            transferToBatch[transfer.TransferId] = batchKey;

            // Merge composition into batch
            MergeCompositionIntoBatch(batch, transfer.Composition.ToArray());

            SystemDebug.Log(SystemDebug.Category.Network,
                $"[TransferViz] Added transfer {transfer.TransferId} to batch (now {batch.TransferIds.Count} transfers)");
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

        /// <summary>
        /// Merges wave packet composition into a batch's merged composition.
        /// Combines frequencies by adding counts for matching frequencies.
        /// </summary>
        private void MergeCompositionIntoBatch(TransferBatch batch, WavePacketSample[] newSamples)
        {
            foreach (var newSample in newSamples)
            {
                // Find existing sample with same frequency
                var existingSample = batch.MergedComposition.Find(s => s.Frequency == newSample.Frequency);
                if (existingSample != null)
                {
                    // Frequency exists - add to count
                    existingSample.Count += newSample.Count;
                }
                else
                {
                    // New frequency - add to batch
                    batch.MergedComposition.Add(new WavePacketSample
                    {
                        Frequency = newSample.Frequency,
                        Count = newSample.Count
                    });
                }
            }
        }

        /// <summary>
        /// Coroutine that waits for batch window to close, then spawns the batched packet visual.
        /// </summary>
        private IEnumerator SpawnBatchAfterWindow(BatchKey batchKey, float windowSeconds)
        {
            // Wait for batch window to collect all transfers
            yield return new WaitForSeconds(windowSeconds);

            // Get the batch
            if (!pendingBatches.TryGetValue(batchKey, out TransferBatch batch))
            {
                SystemDebug.LogWarning(SystemDebug.Category.Network,
                    "[TransferViz] Batch disappeared during spawn window");
                yield break;
            }

            // Mark as spawned
            batch.Spawned = true;

            // Spawn single visual for entire batch
            GameObject packet = SpawnTransferPacket(
                batch.MergedComposition.ToArray(),
                batch.StartPos,
                batch.StartRotation,
                batch.EndPos,
                batch.EndRotation,
                batch.StartHeight,
                batch.EndHeight,
                () => OnBatchArrivedAtDestination(batchKey)
            );

            if (packet != null)
            {
                batchVisuals[batchKey] = packet;
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[TransferViz] Spawned BATCH visual for {batch.TransferIds.Count} transfers with {batch.MergedComposition.Count} frequencies");
            }
            else
            {
                SystemDebug.LogError(SystemDebug.Category.Network,
                    $"[TransferViz] Failed to spawn batch visual");
            }

            // Remove from pending batches (but keep transferToBatch mapping for arrival handling)
            pendingBatches.Remove(batchKey);
        }

        /// <summary>
        /// Called when a batched packet visual arrives at destination.
        /// Triggers arrival callbacks for ALL transfers in the batch.
        /// </summary>
        private void OnBatchArrivedAtDestination(BatchKey batchKey)
        {
            // Find all transfers in this batch
            List<ulong> batchTransferIds = new List<ulong>();
            foreach (var kvp in transferToBatch)
            {
                if (kvp.Value.Equals(batchKey))
                {
                    batchTransferIds.Add(kvp.Key);
                }
            }

            SystemDebug.Log(SystemDebug.Category.Network,
                $"[TransferViz] Batch arrived with {batchTransferIds.Count} transfers");

            // Trigger arrival for each transfer in batch
            foreach (ulong transferId in batchTransferIds)
            {
                OnPacketArrivedAtDestination(transferId);
            }

            // Clean up batch visual
            if (batchVisuals.TryGetValue(batchKey, out GameObject packet))
            {
                if (packet != null)
                {
                    Destroy(packet);
                }
                batchVisuals.Remove(batchKey);
            }

            // Clean up batch tracking
            foreach (ulong transferId in batchTransferIds)
            {
                transferToBatch.Remove(transferId);
            }
        }

        /// <summary>
        /// Spawns a transfer packet visual using WavePacketTransfer prefab.
        /// Direct instantiation with full control over composition, rotation, and height.
        /// </summary>
        private GameObject SpawnTransferPacket(WavePacketSample[] composition,
                                               Vector3 startPos, Quaternion startRotation,
                                               Vector3 endPos, Quaternion endRotation,
                                               float startHeight, float endHeight,
                                               System.Action onArrival)
        {
            if (wavePacketTransferPrefab == null)
            {
                SystemDebug.LogError(SystemDebug.Category.Network, "[TransferViz] WavePacketTransfer prefab is null!");
                return null;
            }

            // Instantiate prefab at start position with start rotation
            GameObject packet = Instantiate(wavePacketTransferPrefab, startPos, startRotation);
            packet.name = $"TransferPacket_{Time.frameCount}";

            // Initialize WavePacketSourceRenderer component
            var visual = packet.GetComponent<SYSTEM.Game.WavePacketSourceRenderer>();
            if (visual != null)
            {
                var sampleList = new List<WavePacketSample>(composition);
                uint totalPackets = 0;
                foreach (var sample in composition) totalPackets += sample.Count;

                // Use first frequency for primary color
                Color packetColor = FrequencyConstants.GetColorForFrequency(composition[0].Frequency);
                visual.Initialize(null, 0, packetColor, totalPackets, 0, sampleList);

                if (showDebugLogs)
                {
                    SystemDebug.Log(SystemDebug.Category.Network,
                        $"[TransferViz] Initialized WavePacketSourceRenderer with {composition.Length} frequencies, {totalPackets} total packets");
                }
            }
            else
            {
                SystemDebug.LogWarning(SystemDebug.Category.Network,
                    "[TransferViz] WavePacketTransfer prefab missing WavePacketSourceRenderer component");
            }

            // Add trajectory component for movement
            var trajectory = packet.AddComponent<SYSTEM.WavePacket.PacketTrajectory>();
            trajectory.Initialize(endPos, endRotation, packetTravelSpeed, startHeight, endHeight, onArrival);

            if (showDebugLogs)
            {
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[TransferViz] Spawned transfer packet from {startPos} to {endPos} with height {startHeight}->{endHeight}");
            }

            return packet;
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

