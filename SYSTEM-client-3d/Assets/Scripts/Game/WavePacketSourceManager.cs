using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;
using SYSTEM.WavePacket;

namespace SYSTEM.Game
{
    /// <summary>
    /// Visualization manager for WavePacketSources (mineable wave packet sources).
    /// Handles both stationary sources and moving sources with smooth interpolation.
    /// Sources spawn at height 0, travel along sphere surface, then rise to height 1.
    /// </summary>
    public class WavePacketSourceManager : MonoBehaviour
    {
        // Source state constants (must match server SOURCE_STATE_* constants)
        private const byte STATE_MOVING_H = 0;    // Moving horizontally on sphere surface
        private const byte STATE_ARRIVED_H0 = 1;  // Arrived at destination, height 0
        private const byte STATE_RISING = 2;      // Rising from height 0 to height 1
        private const byte STATE_STATIONARY = 3;  // At final position, height 1, mineable

        [Header("Prefab Configuration")]
        [SerializeField] private WavePacketPrefabManager prefabManager;
        [SerializeField] private float sourceVisualScale = 2f; // Visual size of source

        [Header("Movement Settings")]
        [SerializeField] private float interpolationSpeed = 10f; // How fast to smooth toward server position
        [SerializeField] private float worldRadius = 300f; // Must match server WORLD_RADIUS

        [Header("State Visual Settings")]
        [SerializeField] private float movingAlpha = 0.6f;   // Transparency when moving (states 0-1)
        [SerializeField] private float risingAlpha = 0.8f;   // Transparency when rising (state 2)
        [SerializeField] private float stationaryAlpha = 1f; // Full opacity when stationary (state 3)

        [Header("Frequency Colors")]
        [SerializeField] private Color redColor = new Color(1f, 0f, 0f, 0.7f);      // 0.0
        [SerializeField] private Color yellowColor = new Color(1f, 1f, 0f, 0.7f);   // 1/6
        [SerializeField] private Color greenColor = new Color(0f, 1f, 0f, 0.7f);    // 1/3
        [SerializeField] private Color cyanColor = new Color(0f, 1f, 1f, 0.7f);     // 1/2
        [SerializeField] private Color blueColor = new Color(0f, 0f, 1f, 0.7f);     // 2/3
        [SerializeField] private Color magentaColor = new Color(1f, 0f, 1f, 0.7f);  // 5/6

        // Tracking
        private Dictionary<ulong, GameObject> activeSources = new Dictionary<ulong, GameObject>();

        // Movement interpolation state
        private struct SourceMovementState
        {
            public Vector3 lastServerPosition;
            public Vector3 velocity;
            public Vector3 destination;
            public byte state;
            public float timeSinceUpdate;
        }
        private Dictionary<ulong, SourceMovementState> sourceMovementStates = new Dictionary<ulong, SourceMovementState>();

        void Awake()
        {
            SystemDebug.Log(SystemDebug.Category.SourceVisualization, "WavePacketSourceManager Awake - Component initialized");

            if (prefabManager == null)
            {
                SystemDebug.LogError(SystemDebug.Category.SourceVisualization, "WavePacketPrefabManager not assigned! Sources will fail to render.");
            }
        }

        void OnEnable()
        {
            // ONLY subscribe to GameEventBus events, no direct database subscriptions
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.Subscribe<WavePacketSourceInsertedEvent>(OnSourceInsertedEvent);
                GameEventBus.Instance.Subscribe<WavePacketSourceUpdatedEvent>(OnSourceUpdatedEvent);
                GameEventBus.Instance.Subscribe<WavePacketSourceDeletedEvent>(OnSourceDeletedEvent);
                GameEventBus.Instance.Subscribe<InitialSourcesLoadedEvent>(OnInitialSourcesLoadedEvent);
                GameEventBus.Instance.Subscribe<WorldTransitionStartedEvent>(OnWorldTransitionEvent);

                SystemDebug.Log(SystemDebug.Category.SourceVisualization, $"[DEBUG] WavePacketSourceManager OnEnable called - Instance ID: {GetInstanceID()}");
            }
            else
            {
                SystemDebug.LogError(SystemDebug.Category.SourceVisualization, "WavePacketSourceManager: GameEventBus.Instance is null!");
            }
        }

        void OnDisable()
        {
            // Unsubscribe from GameEventBus
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.Unsubscribe<WavePacketSourceInsertedEvent>(OnSourceInsertedEvent);
                GameEventBus.Instance.Unsubscribe<WavePacketSourceUpdatedEvent>(OnSourceUpdatedEvent);
                GameEventBus.Instance.Unsubscribe<WavePacketSourceDeletedEvent>(OnSourceDeletedEvent);
                GameEventBus.Instance.Unsubscribe<InitialSourcesLoadedEvent>(OnInitialSourcesLoadedEvent);
                GameEventBus.Instance.Unsubscribe<WorldTransitionStartedEvent>(OnWorldTransitionEvent);
            }

            // Clean up all visualizations
            foreach (var source in activeSources.Values)
            {
                if (source != null)
                    Destroy(source);
            }
            activeSources.Clear();
            sourceMovementStates.Clear();
        }

        void Update()
        {
            // Process smooth movement for all non-stationary sources
            var keysToProcess = new List<ulong>(sourceMovementStates.Keys);
            foreach (var sourceId in keysToProcess)
            {
                if (!activeSources.TryGetValue(sourceId, out GameObject sourceObj) || sourceObj == null)
                    continue;

                var movementState = sourceMovementStates[sourceId];

                // Only process movement for non-stationary sources
                if (movementState.state >= STATE_STATIONARY)
                    continue;

                movementState.timeSinceUpdate += Time.deltaTime;

                // Calculate predicted position based on velocity
                Vector3 predictedPos = movementState.lastServerPosition + movementState.velocity * movementState.timeSinceUpdate;

                // For horizontal movement (state 0), constrain to sphere surface
                if (movementState.state == STATE_MOVING_H)
                {
                    // Project back onto sphere surface at height 0
                    predictedPos = predictedPos.normalized * worldRadius;
                }

                // Smooth interpolation toward predicted position
                Vector3 currentPos = sourceObj.transform.position;
                Vector3 newPos = Vector3.Lerp(currentPos, predictedPos, Time.deltaTime * interpolationSpeed);
                sourceObj.transform.position = newPos;

                // Orient "up" vector to point away from world center (sphere surface normal)
                Vector3 surfaceNormal = newPos.normalized;
                sourceObj.transform.rotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);

                // Update the state back
                sourceMovementStates[sourceId] = movementState;
            }
        }

        #region GameEventBus Event Handlers
        private void OnSourceInsertedEvent(WavePacketSourceInsertedEvent evt)
        {
            // INSERT event only creates new sources (server uses .update() for changes)
            SystemDebug.Log(SystemDebug.Category.SourceVisualization,
                $"Source inserted: ID={evt.Source.SourceId}, State={evt.Source.State}, Pos=({evt.Source.Position.X:F1}, {evt.Source.Position.Y:F1}, {evt.Source.Position.Z:F1}), Packets={evt.Source.TotalWavePackets}");
            CreateSourceVisualization(evt.Source);
            InitializeMovementState(evt.Source);
        }

        private void OnSourceUpdatedEvent(WavePacketSourceUpdatedEvent evt)
        {
            // UPDATE event modifies existing sources (server calls .update())
            SystemDebug.Log(SystemDebug.Category.SourceVisualization,
                $"Source updated: ID={evt.NewSource.SourceId}, State={evt.NewSource.State}, Packets={evt.NewSource.TotalWavePackets}");
            UpdateSourceVisualization(evt.OldSource, evt.NewSource);
            UpdateMovementState(evt.NewSource);
        }

        private void OnSourceDeletedEvent(WavePacketSourceDeletedEvent evt)
        {
            SystemDebug.Log(SystemDebug.Category.SourceVisualization, $"Source deleted: ID={evt.Source.SourceId}");

            RemoveSourceVisualization(evt.Source.SourceId);
            RemoveMovementState(evt.Source.SourceId);
        }

        private void OnInitialSourcesLoadedEvent(InitialSourcesLoadedEvent evt)
        {
            SystemDebug.Log(SystemDebug.Category.SourceVisualization, $"=== INITIAL SOURCE LOAD START ===");
            SystemDebug.Log(SystemDebug.Category.SourceVisualization, $"Event contains {evt.Sources.Count} sources");
            SystemDebug.Log(SystemDebug.Category.SourceVisualization, $"activeSources.Count BEFORE load: {activeSources.Count}");

            int successCount = 0;
            int skippedCount = 0;

            foreach (var source in evt.Sources)
            {
                SystemDebug.Log(SystemDebug.Category.SourceVisualization, $"Processing source {source.SourceId}");

                bool existedBefore = activeSources.ContainsKey(source.SourceId);
                CreateSourceVisualization(source);
                InitializeMovementState(source);  // Also initialize movement state
                bool existsAfter = activeSources.ContainsKey(source.SourceId);

                if (!existedBefore && existsAfter)
                {
                    successCount++;
                    SystemDebug.Log(SystemDebug.Category.SourceVisualization, $"✓ Source {source.SourceId} added to dictionary");
                }
                else if (existedBefore)
                {
                    skippedCount++;
                    SystemDebug.LogWarning(SystemDebug.Category.SourceVisualization, $"✗ Source {source.SourceId} ALREADY in dictionary (skipped)");
                }
                else
                {
                    SystemDebug.LogError(SystemDebug.Category.SourceVisualization, $"✗ Source {source.SourceId} NOT added to dictionary!");
                }
            }

            SystemDebug.Log(SystemDebug.Category.SourceVisualization, $"activeSources.Count AFTER load: {activeSources.Count}");
            SystemDebug.Log(SystemDebug.Category.SourceVisualization, $"Summary: {successCount} added, {skippedCount} skipped");
            SystemDebug.Log(SystemDebug.Category.SourceVisualization, $"=== INITIAL SOURCE LOAD END ===");
        }

        private void OnWorldTransitionEvent(WorldTransitionStartedEvent evt)
        {
            SystemDebug.Log(SystemDebug.Category.SourceVisualization, "World transition, clearing all sources");

            // Clear all sources when transitioning worlds
            foreach (var source in activeSources.Values)
            {
                if (source != null)
                    Destroy(source);
            }
            activeSources.Clear();
        }

        #endregion

        #region Visualization Methods

        private void CreateSourceVisualization(WavePacketSource source)
        {
            SystemDebug.Log(SystemDebug.Category.SourceVisualization, $"Creating source visualization for source {source.SourceId} at position ({source.Position.X}, {source.Position.Y}, {source.Position.Z})");

            // Don't create duplicate
            if (activeSources.ContainsKey(source.SourceId))
            {
                SystemDebug.LogWarning(SystemDebug.Category.SourceVisualization,
                    $"Source {source.SourceId} already exists in activeSources, skipping (GameObject: {activeSources[source.SourceId]?.name ?? "NULL"})");
                return;
            }

            // Get prefab and settings from manager
            if (prefabManager == null)
            {
                SystemDebug.LogError(SystemDebug.Category.SourceVisualization, "PrefabManager is null! Cannot create source visualization.");
                return;
            }

            var (prefab, settings) = prefabManager.GetPrefabAndSettings(WavePacketPrefabManager.PacketType.Source);

            if (prefab == null || settings == null)
            {
                SystemDebug.LogError(SystemDebug.Category.SourceVisualization, $"Failed to get prefab/settings for source {source.SourceId}. Prefab={prefab != null}, Settings={settings != null}");
                return;
            }

            SystemDebug.Log(SystemDebug.Category.SourceVisualization, $"Creating source from prefab '{prefab.name}' for source {source.SourceId}");

            // Create source GameObject from prefab
            GameObject sourceObj = Instantiate(prefab);
            sourceObj.name = $"WavePacketSource_{source.SourceId}";

            // Set position
            Vector3 position = new Vector3(source.Position.X, source.Position.Y, source.Position.Z);
            sourceObj.transform.position = position;
            sourceObj.transform.localScale = Vector3.one * sourceVisualScale;

            // Orient source to align with sphere surface (up vector points away from world center)
            Vector3 surfaceNormal = position.normalized;
            sourceObj.transform.rotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);

            // Set color based on frequency
            Color sourceColor = GetSourceColor(source);

            // Initialize renderer component with settings
            var sourceRenderer = sourceObj.GetComponent<WavePacketSourceRenderer>();
            if (sourceRenderer != null)
            {
                sourceRenderer.Initialize(settings, source.SourceId, sourceColor, source.TotalWavePackets, source.ActiveMinerCount, source.WavePacketComposition);
            }
            else
            {
                SystemDebug.LogError(SystemDebug.Category.SourceVisualization, $"Prefab '{prefab.name}' does not have WavePacketSourceRenderer component!");
            }

            // Add to tracking
            activeSources[source.SourceId] = sourceObj;

            SystemDebug.Log(SystemDebug.Category.SourceVisualization, $"Successfully created source GameObject '{sourceObj.name}' for source {source.SourceId} at world position ({sourceObj.transform.position.x}, {sourceObj.transform.position.y}, {sourceObj.transform.position.z})");
        }

        private void UpdateSourceVisualization(WavePacketSource oldSource, WavePacketSource newSource)
        {
            if (!activeSources.TryGetValue(newSource.SourceId, out GameObject sourceObj))
            {
                // Source visualization doesn't exist, create it
                CreateSourceVisualization(newSource);
                return;
            }

            if (sourceObj == null)
            {
                activeSources.Remove(newSource.SourceId);
                CreateSourceVisualization(newSource);
                return;
            }

            // Update position and orientation
            Vector3 position = new Vector3(newSource.Position.X, newSource.Position.Y, newSource.Position.Z);
            sourceObj.transform.position = position;

            // Orient source to align with sphere surface
            Vector3 surfaceNormal = position.normalized;
            sourceObj.transform.rotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);

            // Update renderer component
            var sourceRenderer = sourceObj.GetComponent<WavePacketSourceRenderer>();
            if (sourceRenderer != null)
            {
                sourceRenderer.UpdatePacketCount(newSource.TotalWavePackets);
                sourceRenderer.UpdateMinerCount(newSource.ActiveMinerCount);

                Color sourceColor = GetSourceColor(newSource);
                sourceRenderer.UpdateColor(sourceColor);

                // Only update composition if it actually changed (avoids unnecessary mesh regeneration)
                SystemDebug.Log(SystemDebug.Category.SourceVisualization,
                    $"[DEBUG] Source {newSource.SourceId}: oldSource={(oldSource == null ? "NULL" : "exists")}, " +
                    $"oldComp={(oldSource?.WavePacketComposition == null ? "NULL" : oldSource.WavePacketComposition.Count.ToString())}, " +
                    $"newComp={newSource.WavePacketComposition?.Count}");

                bool compositionChanged = HasCompositionChanged(oldSource?.WavePacketComposition, newSource.WavePacketComposition);
                SystemDebug.Log(SystemDebug.Category.SourceVisualization,
                    $"[DEBUG] Source {newSource.SourceId}: HasCompositionChanged={compositionChanged}");

                if (compositionChanged)
                {
                    SystemDebug.Log(SystemDebug.Category.SourceVisualization, $"Source {newSource.SourceId} composition changed, updating mesh");
                    sourceRenderer.UpdateComposition(newSource.WavePacketComposition);
                }
            }

            // Log active mining
            if (newSource.ActiveMinerCount > 0)
                SystemDebug.Log(SystemDebug.Category.SourceVisualization, $"Source {newSource.SourceId} has {newSource.ActiveMinerCount} active miners");
        }

        private void RemoveSourceVisualization(ulong sourceId)
        {
            if (activeSources.TryGetValue(sourceId, out GameObject sourceObj))
            {
                if (sourceObj != null)
                    Destroy(sourceObj);

                activeSources.Remove(sourceId);

                SystemDebug.Log(SystemDebug.Category.SourceVisualization, $"Removed visualization for source {sourceId}");
            }
        }

        private Color GetSourceColor(WavePacketSource source)
        {
            // Blend colors based on wave packet composition (weighted by packet counts)
            if (source.WavePacketComposition != null && source.WavePacketComposition.Count > 0)
            {
                Color blendedColor = Color.black;
                uint totalPackets = 0;

                // Calculate weighted color blend
                foreach (var sample in source.WavePacketComposition)
                {
                    Color frequencyColor = GetColorFromFrequency(sample.Frequency);
                    blendedColor += frequencyColor * sample.Count;
                    totalPackets += sample.Count;
                }

                // Normalize by total packet count
                if (totalPackets > 0)
                {
                    blendedColor /= totalPackets;
                    blendedColor.a = 1f; // Ensure full opacity
                    return blendedColor;
                }
            }

            // Default to white if no composition
            return Color.white;
        }

        private Color GetColorFromFrequency(float frequency)
        {
            // Frequency bands:
            // Red: 0.0
            // Yellow: 1/6 ≈ 0.166
            // Green: 1/3 ≈ 0.333
            // Cyan: 1/2 = 0.5
            // Blue: 2/3 ≈ 0.666
            // Magenta: 5/6 ≈ 0.833

            if (frequency < 0.08f) return redColor;
            else if (frequency < 0.25f) return yellowColor;
            else if (frequency < 0.42f) return greenColor;
            else if (frequency < 0.58f) return cyanColor;
            else if (frequency < 0.75f) return blueColor;
            else return magentaColor;
        }

        private bool HasCompositionChanged(List<WavePacketSample> oldComp, List<WavePacketSample> newComp)
        {
            // Null checks
            if (oldComp == null && newComp == null) return false;
            if (oldComp == null || newComp == null) return true;

            // Length check
            if (oldComp.Count != newComp.Count) return true;

            // Content comparison
            for (int i = 0; i < oldComp.Count; i++)
            {
                if (oldComp[i].Frequency != newComp[i].Frequency ||
                    oldComp[i].Count != newComp[i].Count)
                {
                    SystemDebug.Log(SystemDebug.Category.SourceVisualization,
                        $"[DEBUG] Composition diff at index {i}: OLD[freq={oldComp[i].Frequency:F3}, count={oldComp[i].Count}] vs NEW[freq={newComp[i].Frequency:F3}, count={newComp[i].Count}]");
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Movement State Management

        /// <summary>
        /// Initialize movement state for a newly created source.
        /// Called when a source is inserted or loaded.
        /// </summary>
        private void InitializeMovementState(WavePacketSource source)
        {
            Vector3 position = new Vector3(source.Position.X, source.Position.Y, source.Position.Z);
            Vector3 velocity = new Vector3(source.Velocity.X, source.Velocity.Y, source.Velocity.Z);
            Vector3 destination = new Vector3(source.Destination.X, source.Destination.Y, source.Destination.Z);

            var state = new SourceMovementState
            {
                lastServerPosition = position,
                velocity = velocity,
                destination = destination,
                state = source.State,
                timeSinceUpdate = 0f
            };

            sourceMovementStates[source.SourceId] = state;

            SystemDebug.Log(SystemDebug.Category.SourceVisualization,
                $"Initialized movement state for source {source.SourceId}: state={source.State}, vel=({velocity.x:F2}, {velocity.y:F2}, {velocity.z:F2})");
        }

        /// <summary>
        /// Update movement state when server sends an update.
        /// Resets interpolation timer and updates target position/velocity.
        /// </summary>
        private void UpdateMovementState(WavePacketSource source)
        {
            Vector3 position = new Vector3(source.Position.X, source.Position.Y, source.Position.Z);
            Vector3 velocity = new Vector3(source.Velocity.X, source.Velocity.Y, source.Velocity.Z);
            Vector3 destination = new Vector3(source.Destination.X, source.Destination.Y, source.Destination.Z);

            // Get existing state or create new one
            SourceMovementState movementState;
            if (!sourceMovementStates.TryGetValue(source.SourceId, out movementState))
            {
                // No existing state, create one
                InitializeMovementState(source);
                return;
            }

            // Log state transitions
            if (movementState.state != source.State)
            {
                SystemDebug.Log(SystemDebug.Category.SourceVisualization,
                    $"Source {source.SourceId} state transition: {movementState.state} → {source.State}");
            }

            // Update state with new server data
            movementState.lastServerPosition = position;
            movementState.velocity = velocity;
            movementState.destination = destination;
            movementState.state = source.State;
            movementState.timeSinceUpdate = 0f; // Reset interpolation timer

            sourceMovementStates[source.SourceId] = movementState;

            // Update visual alpha based on state
            UpdateSourceVisualAlpha(source.SourceId, source.State);
        }

        /// <summary>
        /// Update source visual transparency based on movement state.
        /// Moving/arrived sources are more transparent, stationary sources are fully opaque.
        /// </summary>
        private void UpdateSourceVisualAlpha(ulong sourceId, byte state)
        {
            if (!activeSources.TryGetValue(sourceId, out GameObject sourceObj) || sourceObj == null)
                return;

            float targetAlpha;
            switch (state)
            {
                case STATE_MOVING_H:
                case STATE_ARRIVED_H0:
                    targetAlpha = movingAlpha;
                    break;
                case STATE_RISING:
                    targetAlpha = risingAlpha;
                    break;
                case STATE_STATIONARY:
                default:
                    targetAlpha = stationaryAlpha;
                    break;
            }

            // Apply alpha to renderer
            var sourceRenderer = sourceObj.GetComponent<WavePacketSourceRenderer>();
            if (sourceRenderer != null)
            {
                sourceRenderer.SetAlpha(targetAlpha);
            }
        }

        /// <summary>
        /// Remove movement state when source is deleted.
        /// </summary>
        private void RemoveMovementState(ulong sourceId)
        {
            sourceMovementStates.Remove(sourceId);
        }

        #endregion
    }
}
