using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;
using SYSTEM.WavePacket;
using SYSTEM.WavePacket.Effects;
using SYSTEM.WavePacket.Movement;

namespace SYSTEM.Game
{
    /// <summary>
    /// Visualization manager for WavePacketSources (mineable wave packet sources).
    /// Uses ServerDrivenMovement component for smooth interpolation.
    /// Sources spawn at height 0, travel along sphere surface, then rise to height 1.
    /// </summary>
    public class WavePacketSourceManager : MonoBehaviour
    {
        [Header("Prefab Configuration")]
        [SerializeField] private WavePacketPrefabManager prefabManager;
        [SerializeField] private float sourceVisualScale = 2f; // Visual size of source

        [Header("Dissipation Effect")]
        [SerializeField] private GameObject dissipationEffectPrefab;

        [Header("Frequency Colors")]
        [SerializeField] private Color redColor = new Color(1f, 0f, 0f, 0.7f);      // 0.0
        [SerializeField] private Color yellowColor = new Color(1f, 1f, 0f, 0.7f);   // 1/6
        [SerializeField] private Color greenColor = new Color(0f, 1f, 0f, 0.7f);    // 1/3
        [SerializeField] private Color cyanColor = new Color(0f, 1f, 1f, 0.7f);     // 1/2
        [SerializeField] private Color blueColor = new Color(0f, 0f, 1f, 0.7f);     // 2/3
        [SerializeField] private Color magentaColor = new Color(1f, 0f, 1f, 0.7f);  // 5/6

        // Tracking
        private Dictionary<ulong, GameObject> activeSources = new Dictionary<ulong, GameObject>();
        private Dictionary<ulong, ServerDrivenMovement> sourceMovements = new Dictionary<ulong, ServerDrivenMovement>();

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

            // Clean up all visualizations (movement components destroyed with GameObjects)
            foreach (var source in activeSources.Values)
            {
                if (source != null)
                    Destroy(source);
            }
            activeSources.Clear();
            sourceMovements.Clear();
        }

        // Note: No Update() loop needed - movement is handled by ServerDrivenMovement components

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

            // Detect dissipation (packets decreased due to natural decay, NOT mining)
            // Mining sets LastDepletion, dissipation sets LastDissipation
            if (evt.OldSource != null && evt.OldSource.TotalWavePackets > evt.NewSource.TotalWavePackets)
            {
                // Only play effect if this was actual dissipation (LastDissipation changed)
                bool isDissipation = evt.NewSource.LastDissipation != evt.OldSource.LastDissipation;

                if (isDissipation)
                {
                    // Find which frequency dissipated
                    float? dissipatedFreq = FindDissipatedFrequency(
                        evt.OldSource.WavePacketComposition,
                        evt.NewSource.WavePacketComposition);

                    if (dissipatedFreq.HasValue)
                    {
                        // Get source position for effect
                        if (activeSources.TryGetValue(evt.NewSource.SourceId, out GameObject sourceObj) && sourceObj != null)
                        {
                            Color freqColor = GetColorFromFrequency(dissipatedFreq.Value);
                            PlayDissipationEffect(sourceObj.transform.position, freqColor);
                        }
                    }
                }
            }
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
            sourceMovements.Clear();
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
            var sourceRenderer = sourceObj.GetComponent<WavePacketVisual>();
            if (sourceRenderer != null)
            {
                sourceRenderer.Initialize(settings, source.SourceId, sourceColor, source.TotalWavePackets, source.ActiveMinerCount, source.WavePacketComposition);
            }
            else
            {
                SystemDebug.LogError(SystemDebug.Category.SourceVisualization, $"Prefab '{prefab.name}' does not have WavePacketVisual component!");
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

            // NOTE: Do NOT update transform.position or transform.rotation here!
            // The ServerDrivenMovement component handles position/rotation with proper height calculation.
            // Directly setting transform here would overwrite the movement component's calculations.

            // Update renderer component
            var sourceRenderer = sourceObj.GetComponent<WavePacketVisual>();
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

        /// <summary>
        /// Find which frequency sample dissipated by comparing old and new compositions.
        /// Returns the frequency of the first sample that decreased in count.
        /// </summary>
        private float? FindDissipatedFrequency(List<WavePacketSample> oldComp, List<WavePacketSample> newComp)
        {
            if (oldComp == null || newComp == null) return null;

            // Compare each frequency sample to find which one decreased
            for (int i = 0; i < oldComp.Count && i < newComp.Count; i++)
            {
                if (oldComp[i].Count > newComp[i].Count)
                {
                    SystemDebug.Log(SystemDebug.Category.SourceVisualization,
                        $"Dissipation detected: frequency {oldComp[i].Frequency:F3} lost {oldComp[i].Count - newComp[i].Count} packet(s)");
                    return oldComp[i].Frequency;
                }
            }

            return null;
        }

        /// <summary>
        /// Play a dissipation particle effect at the specified position with the given color.
        /// </summary>
        private void PlayDissipationEffect(Vector3 position, Color color)
        {
            if (dissipationEffectPrefab == null)
            {
                // No prefab assigned - silently skip
                return;
            }

            // Instantiate effect at source position
            GameObject effectObj = Instantiate(dissipationEffectPrefab, position, Quaternion.identity);

            // Try to get DissipationEffect component
            var effect = effectObj.GetComponent<DissipationEffect>();
            if (effect != null)
            {
                effect.Play(color);
            }
            else
            {
                // Fallback: try to set color directly on ParticleSystem
                var ps = effectObj.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    main.startColor = color;
                    ps.Play();
                }
            }

            // Cleanup after particles finish (2 seconds should be enough)
            Destroy(effectObj, 2f);

            SystemDebug.Log(SystemDebug.Category.SourceVisualization,
                $"Played dissipation effect at {position} with color {color}");
        }

        #endregion

        #region Movement State Management

        /// <summary>
        /// Initialize movement for a newly created source.
        /// Creates ServerDrivenMovement component via factory.
        /// </summary>
        private void InitializeMovementState(WavePacketSource source)
        {
            if (!activeSources.TryGetValue(source.SourceId, out GameObject sourceObj) || sourceObj == null)
            {
                SystemDebug.LogWarning(SystemDebug.Category.SourceVisualization,
                    $"Cannot initialize movement for source {source.SourceId} - GameObject not found");
                return;
            }

            Vector3 position = new Vector3(source.Position.X, source.Position.Y, source.Position.Z);

            // Diagnostic logging to confirm server data
            SystemDebug.Log(SystemDebug.Category.SourceVisualization,
                $"[INIT] Source {source.SourceId}: State={source.State}, " +
                $"Pos=({source.Position.X:F1},{source.Position.Y:F1},{source.Position.Z:F1}) mag={position.magnitude:F1}, " +
                $"Dest=({source.Destination.X:F1},{source.Destination.Y:F1},{source.Destination.Z:F1})");

            // STATIONARY sources: Use server position directly (already at correct height)
            // Server is authoritative - position includes height 1.0 for completed sources
            if (source.State >= 3) // STATE_STATIONARY = 3
            {
                Vector3 surfaceNormal = position.normalized;
                sourceObj.transform.position = position;
                sourceObj.transform.rotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);

                SystemDebug.Log(SystemDebug.Category.SourceVisualization,
                    $"Source {source.SourceId}: STATIONARY - using server position {position} (no movement)");

                // No ServerDrivenMovement needed for stationary sources
                return;
            }

            // Non-stationary sources: Create movement component for local simulation
            // Remove existing movement component if present
            var existingMovement = sourceObj.GetComponent<ServerDrivenMovement>();
            if (existingMovement != null)
            {
                Destroy(existingMovement);
            }

            Vector3 velocity = new Vector3(source.Velocity.X, source.Velocity.Y, source.Velocity.Z);
            Vector3 destination = new Vector3(source.Destination.X, source.Destination.Y, source.Destination.Z);

            // Create movement component via factory
            var movement = PacketMovementFactory.CreateSourceMovement(
                sourceObj,
                position,
                velocity,
                destination,
                source.State
            );

            // Subscribe to state changes for alpha updates
            movement.OnServerStateChanged += (oldState, newState) =>
            {
                UpdateSourceVisualAlpha(source.SourceId, newState);
            };

            // Track movement component
            sourceMovements[source.SourceId] = movement;

            // Set initial alpha
            UpdateSourceVisualAlpha(source.SourceId, source.State);

            SystemDebug.Log(SystemDebug.Category.SourceVisualization,
                $"Initialized ServerDrivenMovement for source {source.SourceId}: state={source.State}, vel=({velocity.x:F2}, {velocity.y:F2}, {velocity.z:F2})");
        }

        /// <summary>
        /// Update movement when server sends an update.
        /// Delegates to ServerDrivenMovement component.
        /// </summary>
        private void UpdateMovementState(WavePacketSource source)
        {
            // Get existing movement or create new one
            if (!sourceMovements.TryGetValue(source.SourceId, out ServerDrivenMovement movement) || movement == null)
            {
                // No existing movement, create one
                InitializeMovementState(source);
                return;
            }

            Vector3 position = new Vector3(source.Position.X, source.Position.Y, source.Position.Z);
            Vector3 velocity = new Vector3(source.Velocity.X, source.Velocity.Y, source.Velocity.Z);
            Vector3 destination = new Vector3(source.Destination.X, source.Destination.Y, source.Destination.Z);

            // Update movement component from server data
            movement.UpdateFromServer(position, velocity, destination, source.State);
        }

        /// <summary>
        /// Update source visual transparency based on movement state.
        /// Uses movement component's recommended alpha.
        /// </summary>
        private void UpdateSourceVisualAlpha(ulong sourceId, byte state)
        {
            if (!activeSources.TryGetValue(sourceId, out GameObject sourceObj) || sourceObj == null)
                return;

            // Get alpha from movement component or calculate from state
            float targetAlpha = 1.0f;
            if (sourceMovements.TryGetValue(sourceId, out ServerDrivenMovement movement) && movement != null)
            {
                targetAlpha = movement.GetRecommendedAlpha();
            }

            // Apply alpha to renderer
            var sourceRenderer = sourceObj.GetComponent<WavePacketVisual>();
            if (sourceRenderer != null)
            {
                sourceRenderer.SetAlpha(targetAlpha);
            }
        }

        /// <summary>
        /// Remove movement component when source is deleted.
        /// </summary>
        private void RemoveMovementState(ulong sourceId)
        {
            sourceMovements.Remove(sourceId);
            // Note: Component is destroyed automatically when GameObject is destroyed
        }

        #endregion
    }
}
