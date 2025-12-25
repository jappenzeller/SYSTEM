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
    /// Manages energy packet transfer visualization between objects and spheres.
    /// Handles ObjectToSphere and SphereToObject leg types.
    /// SERVER-DRIVEN: Visualizes ALL players' transfers based on server state changes (multiplayer-safe).
    /// Uses WavePacketTransfer prefab for packet rendering.
    /// BATCHING SYSTEM: Combines multiple transfers departing together into single visual GameObject.
    /// </summary>
    public class TransferManager : MonoBehaviour
    {
        // Singleton
        private static TransferManager instance;
        public static TransferManager Instance => instance;

        [Header("Visualization Settings")]
        [SerializeField] private GameObject wavePacketTransferPrefab;
        [SerializeField] private float packetTravelSpeed = 5f;
        [SerializeField] private float batchWindowSeconds = 0.1f; // Time window to collect batches

        [Header("Prefab Manager")]
        [SerializeField] private WavePacketPrefabManager prefabManager;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        // Cached settings from WavePacketPrefabManager
        private WavePacketSettings transferPacketSettings;

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
            public string LegType; // ObjectToSphere or SphereToObject
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
                var (tPrefab, tSettings) = prefabManager.GetPrefabAndSettings(WavePacketPrefabManager.PacketType.Transfer);
                if (wavePacketTransferPrefab == null)
                {
                    wavePacketTransferPrefab = tPrefab;
                }
                transferPacketSettings = tSettings;
            }

            // Validate WavePacketTransfer prefab assignment
            if (wavePacketTransferPrefab == null)
            {
                SystemDebug.LogError(SystemDebug.Category.Network, "WavePacketTransfer prefab not assigned and not found in WavePacketPrefabManager!");
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
                SystemDebug.Log(SystemDebug.Category.Network, "[TransferManager] Subscribed to PacketTransfer events");
            }
        }

        private void UnsubscribeFromTransfers()
        {
            GameEventBus.Instance.Unsubscribe<PacketTransferInsertedEvent>(OnTransferInsertedEvent);
            GameEventBus.Instance.Unsubscribe<PacketTransferDeletedEvent>(OnTransferDeletedEvent);
            GameEventBus.Instance.Unsubscribe<PacketTransferUpdatedEvent>(OnTransferUpdatedEvent);

            if (showDebugLogs)
            {
                SystemDebug.Log(SystemDebug.Category.Network, "[TransferManager] Unsubscribed from PacketTransfer events");
            }
        }

        /// <summary>
        /// Server state change handler - triggers visualization for ObjectToSphere and SphereToObject legs.
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
            // Only handle ObjectToSphere and SphereToObject - skip SphereToSphere (handled by DistributionManager)
            if (newTransfer.CurrentLegType == "SphereToSphere")
            {
                // Still update state tracking to prevent stale state issues
                lastProcessedState[newTransfer.TransferId] = newTransfer.CurrentLegType;
                return;
            }

            SystemDebug.Log(SystemDebug.Category.Network,
                $"[TransferManager] OnTransferUpdated: Transfer {newTransfer.TransferId} | Old: {(oldTransfer != null ? $"{oldTransfer.CurrentLegType} leg {oldTransfer.CurrentLeg}" : "NULL")} | New: {newTransfer.CurrentLegType} leg {newTransfer.CurrentLeg} | Completed: {newTransfer.Completed}");

            // Check if this is a meaningful state change (ignore duplicate events)
            string lastState = lastProcessedState.TryGetValue(newTransfer.TransferId, out string prev) ? prev : "";
            bool stateChanged = newTransfer.CurrentLegType != lastState;

            if (stateChanged)
            {
                // Update last processed state
                lastProcessedState[newTransfer.TransferId] = newTransfer.CurrentLegType;

                // Trigger visualization when entering ObjectToSphere or SphereToObject
                bool isNewDeparture =
                    newTransfer.CurrentLegType == "ObjectToSphere" ||
                    newTransfer.CurrentLegType == "SphereToObject";

                if (isNewDeparture)
                {
                    SystemDebug.Log(SystemDebug.Category.Network,
                        $"[TransferManager] DEPARTURE DETECTED: Transfer {newTransfer.TransferId} entering {newTransfer.CurrentLegType}");
                    StartLegVisualization(newTransfer);
                }
                else if (newTransfer.CurrentLegType == "ArrivedAtSphere")
                {
                    SystemDebug.Log(SystemDebug.Category.Network,
                        $"[TransferManager] ARRIVAL: Transfer {newTransfer.TransferId} reached sphere - destroying visual");

                    // Find and destroy the batch visual for this transfer
                    if (transferToBatch.TryGetValue(newTransfer.TransferId, out BatchKey batchKey))
                    {
                        if (batchVisuals.TryGetValue(batchKey, out GameObject visual) && visual != null)
                        {
                            Destroy(visual);
                            batchVisuals.Remove(batchKey);
                        }
                    }
                }
                else if (newTransfer.CurrentLegType == "PendingAtObject")
                {
                    SystemDebug.Log(SystemDebug.Category.Network,
                        $"[TransferManager] PENDING: Transfer {newTransfer.TransferId} waiting at source");
                }
            }

            // Mark transfer as completed
            if (newTransfer.Completed && !completedTransfers.ContainsKey(newTransfer.TransferId))
            {
                completedTransfers[newTransfer.TransferId] = true;
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[TransferManager] MARKED COMPLETE: Transfer {newTransfer.TransferId}");
            }
        }

        private void OnTransferDeletedEvent(PacketTransferDeletedEvent evt)
        {
            // Only handle transfers we're tracking
            if (!lastProcessedState.ContainsKey(evt.Transfer.TransferId) &&
                !transferToBatch.ContainsKey(evt.Transfer.TransferId))
            {
                return;
            }

            OnTransferDeleted(evt.Transfer);
        }

        private void OnTransferDeleted(PacketTransfer transfer)
        {
            if (showDebugLogs)
            {
                SystemDebug.Log(SystemDebug.Category.Network, $"[TransferManager] Transfer {transfer.TransferId} deleted");
            }

            // Check if this transfer is part of a batch
            if (transferToBatch.TryGetValue(transfer.TransferId, out BatchKey batchKey))
            {
                transferToBatch.Remove(transfer.TransferId);

                if (pendingBatches.TryGetValue(batchKey, out TransferBatch batch) && !batch.Spawned)
                {
                    batch.TransferIds.Remove(transfer.TransferId);
                }
            }
            else
            {
                CleanupPacketVisual(transfer.TransferId);
            }
        }

        /// <summary>
        /// Starts visualization for ObjectToSphere or SphereToObject leg.
        /// </summary>
        private void StartLegVisualization(PacketTransfer transfer)
        {
            SystemDebug.Log(SystemDebug.Category.Network,
                $"[TransferManager] StartLegVisualization: Transfer {transfer.TransferId} leg {transfer.CurrentLeg}");

            // Clean up old visual if exists
            if (activePacketVisuals.ContainsKey(transfer.TransferId))
            {
                CleanupPacketVisual(transfer.TransferId);
            }

            Vector3 startPos;
            Vector3 endPos;
            int currentLeg = (int)transfer.CurrentLeg;

            // Determine start and end positions based on current leg
            if (currentLeg == 0)
            {
                // Leg 0: Object -> First Sphere
                if (transfer.RouteWaypoints.Count < 2)
                {
                    SystemDebug.LogWarning(SystemDebug.Category.Network,
                        $"[TransferManager] Transfer {transfer.TransferId} has insufficient waypoints");
                    return;
                }
                startPos = DbVector3ToUnity(transfer.RouteWaypoints[0]);
                endPos = DbVector3ToUnity(transfer.RouteWaypoints[1]);

                // DEBUG: Log waypoint positions
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[TransferManager] SPAWN VISUAL: Transfer {transfer.TransferId} ObjectToSphere");
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[TransferManager] START: {startPos} -> END: {endPos}");
            }
            else
            {
                // Final leg: Last Sphere -> Object
                if (currentLeg >= transfer.RouteWaypoints.Count)
                {
                    SystemDebug.LogWarning(SystemDebug.Category.Network,
                        $"[TransferManager] Transfer {transfer.TransferId} leg {currentLeg} out of range");
                    return;
                }
                startPos = DbVector3ToUnity(transfer.RouteWaypoints[currentLeg]);

                // Get storage device position
                var storage = GameManager.Conn.Db.StorageDevice.DeviceId.Find(transfer.DestinationDeviceId);
                if (storage == null)
                {
                    SystemDebug.LogWarning(SystemDebug.Category.Network,
                        $"[TransferManager] Storage device {transfer.DestinationDeviceId} not found");
                    return;
                }
                endPos = DbVector3ToUnity(storage.Position);

                // DEBUG: Log waypoint positions
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[TransferManager] SPAWN VISUAL: Transfer {transfer.TransferId} SphereToObject (final leg)");
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[TransferManager] START: {startPos} -> END: {endPos}");
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
                    break;

                case "SphereToObject":
                    // Sphere → Object: Descend from height 10 to height 1
                    startHeight = SYSTEM.Circuits.CircuitConstants.SPHERE_PACKET_HEIGHT;
                    endHeight = SYSTEM.Circuits.CircuitConstants.OBJECT_PACKET_HEIGHT;
                    break;
            }

            // Calculate rotations for surface orientation
            Vector3 startNormal = PacketPositionHelper.GetSurfaceNormal(startPos);
            Vector3 endNormal = PacketPositionHelper.GetSurfaceNormal(endPos);
            Quaternion startRotation = PacketPositionHelper.GetOrientationForSurface(startNormal);
            Quaternion endRotation = PacketPositionHelper.GetOrientationForSurface(endNormal);

            // Log batch creation with full context for debugging
            SystemDebug.Log(SystemDebug.Category.Network,
                $"[TransferManager] Creating batch for transfer {transfer.TransferId} | {transfer.CurrentLegType} | leg {transfer.CurrentLeg}");

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
                    StartHeight = startHeight,
                    EndHeight = endHeight,
                    LegType = transfer.CurrentLegType
                };
                pendingBatches[batchKey] = batch;
                StartCoroutine(SpawnBatchAfterWindow(batchKey, batchWindowSeconds));
            }

            batch.TransferIds.Add(transfer.TransferId);
            transferToBatch[transfer.TransferId] = batchKey;
            MergeCompositionIntoBatch(batch, transfer.Composition.ToArray());
        }

        private void OnPacketArrivedAtDestination(ulong transferId)
        {
            if (completedTransfers.TryGetValue(transferId, out bool isComplete) && isComplete)
            {
                completedTransfers.Remove(transferId);
            }
            CleanupPacketVisual(transferId);
        }

        private void MergeCompositionIntoBatch(TransferBatch batch, WavePacketSample[] newSamples)
        {
            foreach (var newSample in newSamples)
            {
                var existingSample = batch.MergedComposition.Find(s => s.Frequency == newSample.Frequency);
                if (existingSample != null)
                {
                    existingSample.Count += newSample.Count;
                }
                else
                {
                    batch.MergedComposition.Add(new WavePacketSample
                    {
                        Frequency = newSample.Frequency,
                        Count = newSample.Count
                    });
                }
            }
        }

        private IEnumerator SpawnBatchAfterWindow(BatchKey batchKey, float windowSeconds)
        {
            yield return new WaitForSeconds(windowSeconds);

            if (!pendingBatches.TryGetValue(batchKey, out TransferBatch batch))
            {
                yield break;
            }

            batch.Spawned = true;

            GameObject packet = SpawnTransferPacket(
                batch.MergedComposition.ToArray(),
                batch.StartPos,
                batch.StartRotation,
                batch.EndPos,
                batch.EndRotation,
                batch.StartHeight,
                batch.EndHeight,
                batch.LegType,
                () => OnBatchArrivedAtDestination(batchKey)
            );

            if (packet != null)
            {
                batchVisuals[batchKey] = packet;
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[TransferManager] Spawned batch visual for transfers: [{string.Join(",", batch.TransferIds)}] type={batch.LegType}");
            }

            pendingBatches.Remove(batchKey);
        }

        private void OnBatchArrivedAtDestination(BatchKey batchKey)
        {
            List<ulong> batchTransferIds = new List<ulong>();
            foreach (var kvp in transferToBatch)
            {
                if (kvp.Value.Equals(batchKey))
                {
                    batchTransferIds.Add(kvp.Key);
                }
            }

            foreach (ulong transferId in batchTransferIds)
            {
                OnPacketArrivedAtDestination(transferId);
            }

            if (batchVisuals.TryGetValue(batchKey, out GameObject packet))
            {
                if (packet != null)
                {
                    Destroy(packet);
                }
                batchVisuals.Remove(batchKey);
            }

            foreach (ulong transferId in batchTransferIds)
            {
                transferToBatch.Remove(transferId);
            }
        }

        private GameObject SpawnTransferPacket(WavePacketSample[] composition,
                                               Vector3 startPos, Quaternion startRotation,
                                               Vector3 endPos, Quaternion endRotation,
                                               float startHeight, float endHeight,
                                               string legType,
                                               System.Action onArrival)
        {
            if (wavePacketTransferPrefab == null)
            {
                SystemDebug.LogError(SystemDebug.Category.Network, "[TransferManager] Transfer prefab is null!");
                return null;
            }

            GameObject packet = Instantiate(wavePacketTransferPrefab, startPos, startRotation);
            packet.name = $"TransferPacket_{Time.frameCount}";

            // Initialize WavePacketVisual component
            var visual = packet.GetComponent<WavePacketVisual>();
            if (visual != null)
            {
                var sampleList = new List<WavePacketSample>(composition);
                uint totalPackets = 0;
                foreach (var sample in composition) totalPackets += sample.Count;

                Color packetColor = FrequencyConstants.GetColorForFrequency(composition[0].Frequency);
                visual.Initialize(transferPacketSettings, 0, packetColor, totalPackets, 0, sampleList);
            }

            // Add trajectory component for two-phase movement
            PacketMovementFactory.CreateTransferTrajectory(
                packet,
                endPos,
                endRotation,
                packetTravelSpeed,
                startHeight,
                endHeight,
                onArrival
            );

            if (showDebugLogs)
            {
                SystemDebug.Log(SystemDebug.Category.Network,
                    $"[TransferManager] Spawned transfer packet: {legType} from {startPos} to {endPos}");
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

            lastProcessedState.Remove(transferId);
            completedTransfers.Remove(transferId);
        }

        private Vector3 DbVector3ToUnity(DbVector3 dbVec)
        {
            return new Vector3(dbVec.X, dbVec.Y, dbVec.Z);
        }

        /// <summary>
        /// Ensures the TransferManager exists in the scene.
        /// Logs error if not found - component must be added to WorldScene manually.
        /// </summary>
        public static void EnsureTransferManager()
        {
            if (instance == null)
            {
                SystemDebug.LogError(SystemDebug.Category.Network, "[TransferManager] TransferManager not found in scene! Add TransferManager component to WorldScene.");
            }
        }
    }
}
