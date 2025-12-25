using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SpacetimeDB;
using SpacetimeDB.Types;
using SYSTEM.WavePacket;
using SYSTEM.WavePacket.Movement;
using SYSTEM.Debug;

namespace SYSTEM.Game
{
    /// <summary>
    /// Manages energy packet distribution visualization through the spire network.
    /// Handles SphereToSphere leg types only (sphere-to-sphere routing at high altitude).
    /// SERVER-DRIVEN: Visualizes ALL players' transfers based on server state changes (multiplayer-safe).
    /// Uses WavePacketDistribution prefab for packet rendering.
    /// BATCHING SYSTEM: Combines multiple transfers departing together into single visual GameObject.
    /// </summary>
    public class DistributionManager : MonoBehaviour
    {
        // Singleton
        private static DistributionManager instance;
        public static DistributionManager Instance => instance;

        [Header("Visualization Settings")]
        [SerializeField] private GameObject wavePacketDistributionPrefab;
        [SerializeField] private float packetTravelSpeed = 5f;
        [SerializeField] private float batchWindowSeconds = 0.1f; // Time window to collect batches

        [Header("Prefab Manager")]
        [SerializeField] private WavePacketPrefabManager prefabManager;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        // Cached settings from WavePacketPrefabManager
        private WavePacketSettings distributionPacketSettings;

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
            public float Height; // Constant height for sphere-to-sphere
            public bool Spawned = false;
        }

        private void Awake()
        {
            // Singleton pattern
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                SystemDebug.LogError(SystemDebug.Category.Network, "GameManager instance not found");
            }

            // Load prefab manager from Resources if not assigned
            if (prefabManager == null)
            {
                prefabManager = Resources.Load<WavePacketPrefabManager>("WavePacketPrefabManager");
            }

            // Load prefab and settings from WavePacketPrefabManager
            if (prefabManager != null)
            {
                var (dPrefab, dSettings) = prefabManager.GetPrefabAndSettings(WavePacketPrefabManager.PacketType.Distribution);
                if (wavePacketDistributionPrefab == null)
                {
                    wavePacketDistributionPrefab = dPrefab;
                }
                distributionPacketSettings = dSettings;
            }

            // Validate WavePacketDistribution prefab assignment
            if (wavePacketDistributionPrefab == null)
            {
                SystemDebug.LogError(SystemDebug.Category.Network, "WavePacketDistribution prefab not assigned and not found in WavePacketPrefabManager!");
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
        /// Server state change handler - triggers visualization for SphereToSphere leg only.
        /// ObjectToSphere and SphereToObject are handled by TransferManager.
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
            // Only handle SphereToSphere - skip ObjectToSphere and SphereToObject (handled by TransferManager)
            if (newTransfer.CurrentLegType != "SphereToSphere" &&
                newTransfer.CurrentLegType != "ArrivedAtSphere")
            {
                return;
            }

            SystemDebug.Log(SystemDebug.Category.Network,
                $"[DistributionManager] OnTransferUpdated: Transfer {newTransfer.TransferId} | Old: {(oldTransfer != null ? $"{oldTransfer.CurrentLegType} leg {oldTransfer.CurrentLeg}" : "NULL")} | New: {newTransfer.CurrentLegType} leg {newTransfer.CurrentLeg} | Completed: {newTransfer.Completed}");

            // Check if this is a meaningful state change (ignore duplicate events)
            string lastState = lastProcessedState.TryGetValue(newTransfer.TransferId, out string prev) ? prev : "";
            bool stateChanged = newTransfer.CurrentLegType != lastState;

            if (stateChanged)
            {
                // Update last processed state
                lastProcessedState[newTransfer.TransferId] = newTransfer.CurrentLegType;

                // Trigger visualization when entering SphereToSphere state
                if (newTransfer.CurrentLegType == "SphereToSphere")
                {
                    // Diagnostic: Log route structure to debug pass-through issue
                    SystemDebug.Log(SystemDebug.Category.Network,
                        $"[DistributionManager] SphereToSphere: transfer={newTransfer.TransferId}, leg={newTransfer.CurrentLeg}, " +
                        $"waypointCount={newTransfer.RouteWaypoints.Count}, " +
                        $"routeSpireIds=[{string.Join(",", newTransfer.RouteSpireIds)}]");

                    SystemDebug.Log(SystemDebug.Category.Network,
                        $"[DistributionManager] DEPARTURE: Transfer {newTransfer.TransferId} sphere-to-sphere");
                    StartLegVisualization(newTransfer);
                }
                else if (newTransfer.CurrentLegType == "ArrivedAtSphere")
                {
                    // Diagnostic: Log batch lookup status to debug pass-through issue
                    bool hasBatch = transferToBatch.TryGetValue(newTransfer.TransferId, out BatchKey batchKey);
                    bool hasVisual = hasBatch && batchVisuals.TryGetValue(batchKey, out GameObject existingVisual) && existingVisual != null;
                    SystemDebug.Log(SystemDebug.Category.Network,
                        $"[DistributionManager] ArrivedAtSphere: transfer={newTransfer.TransferId}, leg={newTransfer.CurrentLeg}, " +
                        $"hasBatch={hasBatch}, hasVisual={hasVisual}");

                    SystemDebug.Log(SystemDebug.Category.Network,
                        $"[DistributionManager] ARRIVAL: Transfer {newTransfer.TransferId} reached sphere - destroying visual");

                    // Find and destroy the batch visual for this transfer
                    if (hasBatch)
                    {
                        if (batchVisuals.TryGetValue(batchKey, out GameObject visual) && visual != null)
                        {
                            Destroy(visual);
                            batchVisuals.Remove(batchKey);
                            SystemDebug.Log(SystemDebug.Category.Network,
                                $"[DistributionManager] Destroyed visual for transfer {newTransfer.TransferId}");
                        }
                        transferToBatch.Remove(newTransfer.TransferId);
                    }
                }
            }

            // Mark transfer as completed
            if (newTransfer.Completed && !completedTransfers.ContainsKey(newTransfer.TransferId))
            {
                completedTransfers[newTransfer.TransferId] = true;
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[DistributionManager] MARKED COMPLETE: Transfer {newTransfer.TransferId}");
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
        /// Starts visualization for SphereToSphere leg using batching system.
        /// BATCHING: Transfers with same source/destination/time are batched into single visual.
        /// </summary>
        private void StartLegVisualization(PacketTransfer transfer)
        {
            SystemDebug.Log(SystemDebug.Category.Network,
                $"[DistributionManager] StartLegVisualization: Transfer {transfer.TransferId} leg {transfer.CurrentLeg}");

            // Clean up old visual if exists
            if (activePacketVisuals.ContainsKey(transfer.TransferId))
            {
                CleanupPacketVisual(transfer.TransferId);
            }

            int currentLeg = (int)transfer.CurrentLeg;

            // For SphereToSphere, we need current and next sphere waypoints
            if (currentLeg >= transfer.RouteWaypoints.Count || currentLeg + 1 >= transfer.RouteWaypoints.Count)
            {
                SystemDebug.LogWarning(SystemDebug.Category.Network,
                    $"[DistributionManager] Transfer {transfer.TransferId} leg {currentLeg} out of waypoints range");
                return;
            }

            Vector3 startPos = DbVector3ToUnity(transfer.RouteWaypoints[currentLeg]);
            Vector3 endPos = DbVector3ToUnity(transfer.RouteWaypoints[currentLeg + 1]);

            // DEBUG: Log ALL waypoints to trace visual targeting
            SystemDebug.Log(SystemDebug.Category.Network,
                $"[DistributionManager] SPAWN VISUAL: Transfer {transfer.TransferId} leg {currentLeg}/{transfer.RouteWaypoints.Count - 1}");

            // Log all waypoints for debugging
            for (int i = 0; i < transfer.RouteWaypoints.Count; i++)
            {
                var wp = transfer.RouteWaypoints[i];
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[DistributionManager] Waypoint[{i}]: ({wp.X:F1}, {wp.Y:F1}, {wp.Z:F1})");
            }

            SystemDebug.Log(SystemDebug.Category.Network,
                $"[DistributionManager] Using: START[{currentLeg}]={startPos} -> END[{currentLeg + 1}]={endPos}");

            // SphereToSphere always uses constant sphere height
            float height = SYSTEM.Circuits.CircuitConstants.SPHERE_PACKET_HEIGHT;

            // Calculate rotations for surface orientation
            Vector3 startNormal = PacketPositionHelper.GetSurfaceNormal(startPos);
            Vector3 endNormal = PacketPositionHelper.GetSurfaceNormal(endPos);
            Quaternion startRotation = PacketPositionHelper.GetOrientationForSurface(startNormal);
            Quaternion endRotation = PacketPositionHelper.GetOrientationForSurface(endNormal);

            // BATCHING SYSTEM
            BatchKey batchKey = new BatchKey(startPos, endPos, Time.time);

            if (!pendingBatches.TryGetValue(batchKey, out TransferBatch batch))
            {
                batch = new TransferBatch
                {
                    StartPos = startPos,
                    EndPos = endPos,
                    StartRotation = startRotation,
                    EndRotation = endRotation,
                    Height = height
                };
                pendingBatches[batchKey] = batch;
                StartCoroutine(SpawnBatchAfterWindow(batchKey, batchWindowSeconds));

                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[DistributionManager] Created new batch for {startPos} -> {endPos}");
            }

            batch.TransferIds.Add(transfer.TransferId);
            transferToBatch[transfer.TransferId] = batchKey;
            MergeCompositionIntoBatch(batch, transfer.Composition.ToArray());

            SystemDebug.Log(SystemDebug.Category.Network,
                $"[DistributionManager] Added transfer {transfer.TransferId} to batch ({batch.TransferIds.Count} transfers)");
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
            yield return new WaitForSeconds(windowSeconds);

            if (!pendingBatches.TryGetValue(batchKey, out TransferBatch batch))
            {
                yield break;
            }

            batch.Spawned = true;

            GameObject packet = SpawnDistributionPacket(
                batch.MergedComposition.ToArray(),
                batch.StartPos,
                batch.StartRotation,
                batch.EndPos,
                batch.EndRotation,
                batch.Height,
                () => OnBatchArrivedAtDestination(batchKey)
            );

            if (packet != null)
            {
                batchVisuals[batchKey] = packet;
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[DistributionManager] Spawned batch visual for transfers: [{string.Join(",", batch.TransferIds)}]");
            }

            pendingBatches.Remove(batchKey);
        }

        /// <summary>
        /// Called when a batched packet visual arrives at destination.
        /// Triggers arrival callbacks for ALL transfers in the batch.
        /// </summary>
        private void OnBatchArrivedAtDestination(BatchKey batchKey)
        {
            SystemDebug.Log(SystemDebug.Category.Network,
                $"[DistributionManager] BATCH CALLBACK FIRED: Visual reached destination at {batchKey.Destination}");

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
                $"[DistributionManager] BATCH ARRIVED: Destroying visual, {batchTransferIds.Count} transfers complete");

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
        /// Spawns a distribution packet visual using WavePacketDistribution prefab.
        /// Uses constant height for sphere-to-sphere movement.
        /// </summary>
        private GameObject SpawnDistributionPacket(WavePacketSample[] composition,
                                                   Vector3 startPos, Quaternion startRotation,
                                                   Vector3 endPos, Quaternion endRotation,
                                                   float height,
                                                   System.Action onArrival)
        {
            if (wavePacketDistributionPrefab == null)
            {
                SystemDebug.LogError(SystemDebug.Category.Network, "[DistributionManager] Distribution prefab is null!");
                return null;
            }

            GameObject packet = Instantiate(wavePacketDistributionPrefab, startPos, startRotation);
            packet.name = $"DistributionPacket_{Time.frameCount}";

            // Initialize WavePacketVisual component
            var visual = packet.GetComponent<WavePacketVisual>();
            if (visual != null)
            {
                var sampleList = new List<WavePacketSample>(composition);
                uint totalPackets = 0;
                foreach (var sample in composition) totalPackets += sample.Count;

                Color packetColor = FrequencyConstants.GetColorForFrequency(composition[0].Frequency);
                visual.Initialize(distributionPacketSettings, 0, packetColor, totalPackets, 0, sampleList);
            }

            // Add trajectory component for constant-height horizontal movement
            PacketMovementFactory.CreateDistributionTrajectory(
                packet,
                endPos,
                packetTravelSpeed,
                onArrival
            );

            if (showDebugLogs)
            {
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[DistributionManager] Spawned distribution packet: {startPos} -> {endPos} at height {height}");
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

        /// <summary>
        /// Ensures the DistributionManager exists in the scene.
        /// Logs error if not found - component must be added to WorldScene manually.
        /// </summary>
        public static void EnsureDistributionManager()
        {
            if (instance == null)
            {
                SystemDebug.LogError(SystemDebug.Category.Network, "[DistributionManager] DistributionManager not found in scene! Add DistributionManager component to WorldScene.");
            }
        }
    }
}

