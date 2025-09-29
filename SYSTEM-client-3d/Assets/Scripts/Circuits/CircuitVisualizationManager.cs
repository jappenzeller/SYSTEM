using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;
using SYSTEM.Circuits;
using SYSTEM.Game;

namespace SYSTEM.Circuits
{
    /// <summary>
    /// Manages the spawning, updating, and removal of circuit visualizations based on SpacetimeDB data.
    /// Integrates with the existing game systems and ensures consistent world radius usage.
    /// </summary>
    public class CircuitVisualizationManager : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject circuitSpirePrefab;
        [SerializeField] private GameObject primaryCircuitPrefab;
        [SerializeField] private GameObject secondaryCircuitPrefab;
        [SerializeField] private GameObject tertiaryCircuitPrefab;
        [SerializeField] private GameObject tunnelRendererPrefab;

        [Header("World Configuration")]
        [SerializeField] private float worldRadiusOverride = 0f; // 0 = use CircuitConstants.WORLD_RADIUS
        [SerializeField] private bool validateWorldRadius = true;

        [Header("Circuit Settings")]
        [SerializeField] private int maxCircuitsPerWorld = CircuitConstants.MAX_CIRCUITS_PER_WORLD;
        [SerializeField] private float circuitSpawnDelay = 0.1f;
        [SerializeField] private bool autoConnectTunnels = true;

        [Header("Performance")]
        [SerializeField] private bool useObjectPooling = true;
        [SerializeField] private int poolSizePerType = 50;
        [SerializeField] private float cullingDistance = 1000f;
        [SerializeField] private bool useLOD = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool showWorldGrid = false;
        [SerializeField] private bool highlightActiveCircuits = false;

        // Circuit tracking
        private Dictionary<ulong, GameObject> circuitObjects = new Dictionary<ulong, GameObject>();
        private Dictionary<WorldCoords, List<GameObject>> worldCircuits = new Dictionary<WorldCoords, List<GameObject>>();
        private Dictionary<WorldCoords, GameObject> worldObjects = new Dictionary<WorldCoords, GameObject>();
        private Dictionary<(ulong, ulong), GameObject> tunnelConnections = new Dictionary<(ulong, ulong), GameObject>();

        // Object pools
        private Queue<GameObject> primaryCircuitPool = new Queue<GameObject>();
        private Queue<GameObject> secondaryCircuitPool = new Queue<GameObject>();
        private Queue<GameObject> tertiaryCircuitPool = new Queue<GameObject>();
        private Queue<GameObject> tunnelRendererPool = new Queue<GameObject>();

        // References
        private DbConnection conn;
        private Transform cameraTransform;
        private WorldManager worldManager;
        private GameEventBus eventBus;

        // State
        private bool isInitialized = false;
        private float worldRadius;

        #region Initialization

        void Awake()
        {
            // Determine world radius
            worldRadius = worldRadiusOverride > 0 ? worldRadiusOverride : CircuitConstants.WORLD_RADIUS;

            if (validateWorldRadius && !CircuitConstants.ValidateWorldRadius(worldRadius))
            {
                SystemDebug.LogWarning(SystemDebug.Category.OrbSystem,
                    $"[CircuitVisualizationManager] World radius {worldRadius} doesn't match expected {CircuitConstants.WORLD_RADIUS}");
            }

            // Get references
            worldManager = FindFirstObjectByType<WorldManager>();
            eventBus = GameEventBus.Instance;

            // Initialize object pools if enabled
            if (useObjectPooling)
            {
                InitializeObjectPools();
            }
        }

        void Start()
        {
            // Get camera reference
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            // Wait for game manager to be ready
            StartCoroutine(InitializeWhenReady());
        }

        private System.Collections.IEnumerator InitializeWhenReady()
        {
            // Wait for GameManager connection
            while (!GameManager.IsConnected())
            {
                yield return new WaitForSeconds(0.5f);
            }

            conn = GameManager.Conn;
            isInitialized = true;

            SystemDebug.Log(SystemDebug.Category.OrbSystem,
                "[CircuitVisualizationManager] Initialized and ready");

            // Subscribe to events
            SubscribeToEvents();

            // Load existing circuits
            LoadExistingCircuits();
        }

        void OnDestroy()
        {
            UnsubscribeFromEvents();
            CleanupAllCircuits();
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeToEvents()
        {
            if (eventBus == null) return;

            // Subscribe to circuit database events
            eventBus.Subscribe<CircuitInsertedEvent>(OnCircuitInserted);
            eventBus.Subscribe<CircuitUpdatedEvent>(OnCircuitUpdated);
            eventBus.Subscribe<CircuitDeletedEvent>(OnCircuitDeleted);

            // Subscribe to world events
            eventBus.Subscribe<WorldLoadedEvent>(OnWorldLoaded);
            eventBus.Subscribe<WorldTransitionStartedEvent>(OnWorldTransition);

            // Subscribe to tunnel events
            eventBus.Subscribe<TunnelFormedEvent>(OnTunnelFormed);
            eventBus.Subscribe<TunnelBrokenEvent>(OnTunnelBroken);
        }

        private void UnsubscribeFromEvents()
        {
            if (eventBus == null) return;

            eventBus.Unsubscribe<CircuitInsertedEvent>(OnCircuitInserted);
            eventBus.Unsubscribe<CircuitUpdatedEvent>(OnCircuitUpdated);
            eventBus.Unsubscribe<CircuitDeletedEvent>(OnCircuitDeleted);
            eventBus.Unsubscribe<WorldLoadedEvent>(OnWorldLoaded);
            eventBus.Unsubscribe<WorldTransitionStartedEvent>(OnWorldTransition);
            eventBus.Unsubscribe<TunnelFormedEvent>(OnTunnelFormed);
            eventBus.Unsubscribe<TunnelBrokenEvent>(OnTunnelBroken);
        }

        #endregion

        #region Circuit Loading

        /// <summary>
        /// Loads all existing circuits from the database.
        /// </summary>
        private void LoadExistingCircuits()
        {
            if (conn == null) return;

            SystemDebug.Log(SystemDebug.Category.OrbSystem,
                "[CircuitVisualizationManager] Loading existing circuits from database");

            int circuitCount = 0;
            foreach (var circuit in conn.Db.WorldCircuit.Iter())
            {
                CreateCircuitVisualization(circuit);
                circuitCount++;
            }

            SystemDebug.Log(SystemDebug.Category.OrbSystem,
                $"[CircuitVisualizationManager] Loaded {circuitCount} circuits");
        }

        #endregion

        #region Circuit Management

        /// <summary>
        /// Creates a circuit visualization for the given circuit data.
        /// </summary>
        private void CreateCircuitVisualization(WorldCircuit circuit)
        {
            // Check if circuit already exists
            if (circuitObjects.ContainsKey(circuit.CircuitId))
            {
                SystemDebug.LogWarning(SystemDebug.Category.OrbSystem,
                    $"[CircuitVisualizationManager] Circuit {circuit.CircuitId} already exists");
                return;
            }

            // Get or create world object
            GameObject worldObject = GetOrCreateWorld(circuit.WorldCoords);
            if (worldObject == null)
            {
                SystemDebug.LogError(SystemDebug.Category.OrbSystem,
                    $"[CircuitVisualizationManager] Failed to create world for circuit {circuit.CircuitId}");
                return;
            }

            // Determine circuit type and get appropriate prefab
            GameObject prefab = GetCircuitPrefab(circuit.CircuitType);
            if (prefab == null)
            {
                SystemDebug.LogError(SystemDebug.Category.OrbSystem,
                    $"[CircuitVisualizationManager] No prefab for circuit type: {circuit.CircuitType}");
                return;
            }

            // Spawn circuit
            GameObject circuitObject = SpawnCircuit(prefab, worldObject.transform, circuit);
            if (circuitObject == null)
            {
                SystemDebug.LogError(SystemDebug.Category.OrbSystem,
                    $"[CircuitVisualizationManager] Failed to spawn circuit {circuit.CircuitId}");
                return;
            }

            // Track circuit
            circuitObjects[circuit.CircuitId] = circuitObject;

            // Track by world
            if (!worldCircuits.ContainsKey(circuit.WorldCoords))
            {
                worldCircuits[circuit.WorldCoords] = new List<GameObject>();
            }
            worldCircuits[circuit.WorldCoords].Add(circuitObject);

            SystemDebug.Log(SystemDebug.Category.OrbSystem,
                $"[CircuitVisualizationManager] Created circuit {circuit.CircuitId} at world {circuit.WorldCoords}");
        }

        /// <summary>
        /// Updates an existing circuit visualization.
        /// </summary>
        private void UpdateCircuitVisualization(WorldCircuit circuit)
        {
            if (!circuitObjects.TryGetValue(circuit.CircuitId, out GameObject circuitObject))
            {
                // Circuit doesn't exist, create it
                CreateCircuitVisualization(circuit);
                return;
            }

            // Update circuit components
            var circuitBase = circuitObject.GetComponentInChildren<CircuitBase>();
            if (circuitBase != null)
            {
                // Update charge or other properties
                // This would be based on the specific circuit data
            }

            SystemDebug.Log(SystemDebug.Category.OrbSystem,
                $"[CircuitVisualizationManager] Updated circuit {circuit.CircuitId}");
        }

        /// <summary>
        /// Removes a circuit visualization.
        /// </summary>
        private void RemoveCircuitVisualization(ulong circuitId)
        {
            if (!circuitObjects.TryGetValue(circuitId, out GameObject circuitObject))
            {
                return;
            }

            // Remove from world tracking
            foreach (var kvp in worldCircuits)
            {
                kvp.Value.Remove(circuitObject);
            }

            // Return to pool or destroy
            if (useObjectPooling)
            {
                ReturnToPool(circuitObject);
            }
            else
            {
                Destroy(circuitObject);
            }

            circuitObjects.Remove(circuitId);

            SystemDebug.Log(SystemDebug.Category.OrbSystem,
                $"[CircuitVisualizationManager] Removed circuit {circuitId}");
        }

        #endregion

        #region World Management

        /// <summary>
        /// Gets or creates a world object at the specified coordinates.
        /// </summary>
        private GameObject GetOrCreateWorld(WorldCoords coords)
        {
            if (worldObjects.TryGetValue(coords, out GameObject worldObject))
            {
                return worldObject;
            }

            // Calculate world position using the corrected lattice spacing
            Vector3 worldPosition = CircuitConstants.LogicalToWorldPosition(
                coords.X, coords.Y, coords.Z, CircuitConstants.WorldType.MainGrid);

            // Create world container
            worldObject = new GameObject($"World_{coords.X}_{coords.Y}_{coords.Z}");
            worldObject.transform.position = worldPosition;
            worldObject.transform.SetParent(transform);

            // Add world sphere visualization (optional)
            if (showWorldGrid)
            {
                CreateWorldSphere(worldObject);
            }

            worldObjects[coords] = worldObject;

            SystemDebug.Log(SystemDebug.Category.WorldSystem,
                $"[CircuitVisualizationManager] Created world at {coords} (position: {worldPosition})");

            return worldObject;
        }

        /// <summary>
        /// Creates a visual sphere for a world (for debugging).
        /// </summary>
        private void CreateWorldSphere(GameObject worldObject)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(worldObject.transform);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale = Vector3.one * worldRadius * 2f; // Diameter = 2 * radius

            // Make it semi-transparent
            var renderer = sphere.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetFloat("_Surface", 1); // Transparent
                material.SetFloat("_Blend", 0); // Alpha blend
                Color color = new Color(0.2f, 0.5f, 1f, 0.1f);
                material.SetColor("_BaseColor", color);
                renderer.material = material;
            }

            // Remove collider for performance
            Destroy(sphere.GetComponent<Collider>());
        }

        #endregion

        #region Circuit Spawning

        /// <summary>
        /// Spawns a circuit from the appropriate pool or instantiates a new one.
        /// </summary>
        private GameObject SpawnCircuit(GameObject prefab, Transform worldTransform, WorldCircuit circuitData)
        {
            GameObject circuitObject = null;

            // Get from pool if available
            if (useObjectPooling)
            {
                circuitObject = GetFromPool(circuitData.CircuitType);
            }

            // Instantiate if not from pool
            if (circuitObject == null)
            {
                if (prefab == null)
                {
                    prefab = circuitSpirePrefab; // Fallback to default
                }

                circuitObject = Instantiate(prefab, worldTransform);
            }

            // Configure circuit
            circuitObject.transform.SetParent(worldTransform);
            circuitObject.name = $"Circuit_{circuitData.CircuitId}";

            // Initialize circuit components
            var circuitBase = circuitObject.GetComponentInChildren<CircuitBase>();
            if (circuitBase != null)
            {
                // Map circuit position based on its index or type
                CardinalDirection direction = GetDirectionForCircuit(circuitData);
                circuitBase.Initialize(circuitData.CircuitId, circuitData.WorldCoords, direction, worldTransform);
            }

            // Set active
            circuitObject.SetActive(true);

            return circuitObject;
        }

        /// <summary>
        /// Determines the cardinal direction for a circuit based on its properties.
        /// </summary>
        private CardinalDirection GetDirectionForCircuit(WorldCircuit circuit)
        {
            // This could be based on circuit ID, type, or other properties
            // For now, distribute evenly around the sphere

            int index = (int)(circuit.CircuitId % 26); // Max 26 positions

            if (index < 6)
            {
                // Primary face positions
                CardinalDirection[] faces = {
                    CardinalDirection.NorthPole,
                    CardinalDirection.SouthPole,
                    CardinalDirection.East,
                    CardinalDirection.West,
                    CardinalDirection.Front,
                    CardinalDirection.Back
                };
                return faces[index];
            }
            else if (index < 14)
            {
                // Secondary vertex positions
                CardinalDirection[] vertices = {
                    CardinalDirection.NorthEastFront,
                    CardinalDirection.NorthEastBack,
                    CardinalDirection.NorthWestFront,
                    CardinalDirection.NorthWestBack,
                    CardinalDirection.SouthEastFront,
                    CardinalDirection.SouthEastBack,
                    CardinalDirection.SouthWestFront,
                    CardinalDirection.SouthWestBack
                };
                return vertices[index - 6];
            }
            else
            {
                // Tertiary edge positions
                CardinalDirection[] edges = {
                    CardinalDirection.NorthEast,
                    CardinalDirection.NorthWest,
                    CardinalDirection.NorthFront,
                    CardinalDirection.NorthBack,
                    CardinalDirection.SouthEast,
                    CardinalDirection.SouthWest,
                    CardinalDirection.SouthFront,
                    CardinalDirection.SouthBack,
                    CardinalDirection.EastFront,
                    CardinalDirection.EastBack,
                    CardinalDirection.WestFront,
                    CardinalDirection.WestBack
                };
                return edges[index - 14];
            }
        }

        /// <summary>
        /// Gets the appropriate prefab for a circuit type.
        /// </summary>
        private GameObject GetCircuitPrefab(string circuitType)
        {
            switch (circuitType?.ToLower())
            {
                case "primary":
                    return primaryCircuitPrefab ?? circuitSpirePrefab;
                case "secondary":
                    return secondaryCircuitPrefab ?? circuitSpirePrefab;
                case "tertiary":
                    return tertiaryCircuitPrefab ?? circuitSpirePrefab;
                default:
                    return circuitSpirePrefab;
            }
        }

        #endregion

        #region Tunnel Management

        /// <summary>
        /// Creates a tunnel connection between two circuits.
        /// </summary>
        private void CreateTunnel(ulong sourceCircuitId, ulong targetCircuitId, TunnelType tunnelType)
        {
            if (!circuitObjects.TryGetValue(sourceCircuitId, out GameObject sourceCircuit) ||
                !circuitObjects.TryGetValue(targetCircuitId, out GameObject targetCircuit))
            {
                SystemDebug.LogWarning(SystemDebug.Category.OrbSystem,
                    $"[CircuitVisualizationManager] Cannot create tunnel - circuits not found");
                return;
            }

            // Get ring assemblies
            var sourceRing = sourceCircuit.GetComponentInChildren<RingAssemblyController>();
            var targetRing = targetCircuit.GetComponentInChildren<RingAssemblyController>();

            if (sourceRing == null || targetRing == null)
            {
                SystemDebug.LogWarning(SystemDebug.Category.OrbSystem,
                    $"[CircuitVisualizationManager] Cannot create tunnel - ring assemblies not found");
                return;
            }

            // Create tunnel renderer
            GameObject tunnelObject = useObjectPooling ? GetFromPool("tunnel") : null;
            if (tunnelObject == null)
            {
                tunnelObject = Instantiate(tunnelRendererPrefab ?? new GameObject("TunnelRenderer"));
            }

            // Configure tunnel renderer
            var tunnelRenderer = tunnelObject.GetComponent<QuantumTunnelRenderer>();
            if (tunnelRenderer == null)
            {
                tunnelRenderer = tunnelObject.AddComponent<QuantumTunnelRenderer>();
            }

            tunnelRenderer.Initialize(sourceRing, targetRing, tunnelType, true);
            tunnelRenderer.FormTunnel(1f);

            // Connect rings
            sourceRing.ConnectToWorld(targetRing.transform.position, tunnelType, 1f);
            targetRing.ConnectToWorld(sourceRing.transform.position, tunnelType, 1f);

            // Track tunnel
            var key = sourceCircuitId < targetCircuitId ?
                (sourceCircuitId, targetCircuitId) : (targetCircuitId, sourceCircuitId);
            tunnelConnections[key] = tunnelObject;

            SystemDebug.Log(SystemDebug.Category.OrbSystem,
                $"[CircuitVisualizationManager] Created {tunnelType} tunnel between circuits {sourceCircuitId} and {targetCircuitId}");
        }

        /// <summary>
        /// Removes a tunnel connection.
        /// </summary>
        private void RemoveTunnel(ulong circuit1Id, ulong circuit2Id)
        {
            var key = circuit1Id < circuit2Id ?
                (circuit1Id, circuit2Id) : (circuit2Id, circuit1Id);

            if (!tunnelConnections.TryGetValue(key, out GameObject tunnelObject))
            {
                return;
            }

            // Break the tunnel
            var tunnelRenderer = tunnelObject.GetComponent<QuantumTunnelRenderer>();
            if (tunnelRenderer != null)
            {
                tunnelRenderer.BreakTunnel();
            }

            // Disconnect rings
            if (circuitObjects.TryGetValue(circuit1Id, out GameObject circuit1))
            {
                var ring1 = circuit1.GetComponentInChildren<RingAssemblyController>();
                ring1?.Disconnect();
            }

            if (circuitObjects.TryGetValue(circuit2Id, out GameObject circuit2))
            {
                var ring2 = circuit2.GetComponentInChildren<RingAssemblyController>();
                ring2?.Disconnect();
            }

            // Return to pool or destroy
            if (useObjectPooling)
            {
                ReturnToPool(tunnelObject);
            }
            else
            {
                Destroy(tunnelObject);
            }

            tunnelConnections.Remove(key);

            SystemDebug.Log(SystemDebug.Category.OrbSystem,
                $"[CircuitVisualizationManager] Removed tunnel between circuits {circuit1Id} and {circuit2Id}");
        }

        #endregion

        #region Object Pooling

        private void InitializeObjectPools()
        {
            // Pre-spawn objects for each pool
            for (int i = 0; i < poolSizePerType; i++)
            {
                if (primaryCircuitPrefab != null)
                {
                    var obj = Instantiate(primaryCircuitPrefab, transform);
                    obj.SetActive(false);
                    primaryCircuitPool.Enqueue(obj);
                }

                if (secondaryCircuitPrefab != null)
                {
                    var obj = Instantiate(secondaryCircuitPrefab, transform);
                    obj.SetActive(false);
                    secondaryCircuitPool.Enqueue(obj);
                }

                if (tertiaryCircuitPrefab != null)
                {
                    var obj = Instantiate(tertiaryCircuitPrefab, transform);
                    obj.SetActive(false);
                    tertiaryCircuitPool.Enqueue(obj);
                }

                if (tunnelRendererPrefab != null)
                {
                    var obj = Instantiate(tunnelRendererPrefab, transform);
                    obj.SetActive(false);
                    tunnelRendererPool.Enqueue(obj);
                }
            }

            SystemDebug.Log(SystemDebug.Category.Performance,
                $"[CircuitVisualizationManager] Initialized object pools with {poolSizePerType} objects per type");
        }

        private GameObject GetFromPool(string type)
        {
            Queue<GameObject> pool = null;

            switch (type?.ToLower())
            {
                case "primary":
                    pool = primaryCircuitPool;
                    break;
                case "secondary":
                    pool = secondaryCircuitPool;
                    break;
                case "tertiary":
                    pool = tertiaryCircuitPool;
                    break;
                case "tunnel":
                    pool = tunnelRendererPool;
                    break;
            }

            if (pool != null && pool.Count > 0)
            {
                return pool.Dequeue();
            }

            return null;
        }

        private void ReturnToPool(GameObject obj)
        {
            if (obj == null) return;

            obj.SetActive(false);
            obj.transform.SetParent(transform);

            // Determine which pool to return to based on components
            if (obj.GetComponent<QuantumTunnelRenderer>() != null)
            {
                tunnelRendererPool.Enqueue(obj);
            }
            else
            {
                // Could check for specific circuit types
                // For now, just put in primary pool
                primaryCircuitPool.Enqueue(obj);
            }
        }

        #endregion

        #region Event Handlers

        private void OnCircuitInserted(CircuitInsertedEvent evt)
        {
            if (evt.Circuit != null)
            {
                CreateCircuitVisualization(evt.Circuit);
            }
        }

        private void OnCircuitUpdated(CircuitUpdatedEvent evt)
        {
            if (evt.Circuit != null)
            {
                UpdateCircuitVisualization(evt.Circuit);
            }
        }

        private void OnCircuitDeleted(CircuitDeletedEvent evt)
        {
            RemoveCircuitVisualization(evt.CircuitId);
        }

        private void OnWorldLoaded(WorldLoadedEvent evt)
        {
            // Could load circuits for a specific world
            SystemDebug.Log(SystemDebug.Category.WorldSystem,
                $"[CircuitVisualizationManager] World loaded: {evt.World?.WorldName ?? "Unknown"}");
        }

        private void OnWorldTransition(WorldTransitionStartedEvent evt)
        {
            // Could unload circuits when transitioning away from world
            SystemDebug.Log(SystemDebug.Category.WorldSystem,
                $"[CircuitVisualizationManager] World transition started from {evt.FromWorld} to {evt.ToWorld}");
        }

        private void OnTunnelFormed(TunnelFormedEvent evt)
        {
            CreateTunnel(evt.SourceCircuitId, evt.TargetCircuitId, evt.TunnelType);
        }

        private void OnTunnelBroken(TunnelBrokenEvent evt)
        {
            RemoveTunnel(evt.Circuit1Id, evt.Circuit2Id);
        }

        #endregion

        #region Performance

        void Update()
        {
            if (!isInitialized) return;

            // Update LOD and culling
            if (useLOD && Time.frameCount % 10 == 0) // Check every 10 frames
            {
                UpdateLODAndCulling();
            }
        }

        private void UpdateLODAndCulling()
        {
            if (cameraTransform == null) return;

            foreach (var kvp in circuitObjects)
            {
                GameObject circuit = kvp.Value;
                if (circuit == null) continue;

                float distance = Vector3.Distance(circuit.transform.position, cameraTransform.position);

                // Culling
                if (distance > cullingDistance)
                {
                    if (circuit.activeSelf)
                    {
                        circuit.SetActive(false);
                    }
                }
                else
                {
                    if (!circuit.activeSelf)
                    {
                        circuit.SetActive(true);
                    }
                }
            }
        }

        #endregion

        #region Cleanup

        private void CleanupAllCircuits()
        {
            foreach (var circuit in circuitObjects.Values)
            {
                if (circuit != null)
                {
                    Destroy(circuit);
                }
            }
            circuitObjects.Clear();

            foreach (var world in worldObjects.Values)
            {
                if (world != null)
                {
                    Destroy(world);
                }
            }
            worldObjects.Clear();

            foreach (var tunnel in tunnelConnections.Values)
            {
                if (tunnel != null)
                {
                    Destroy(tunnel);
                }
            }
            tunnelConnections.Clear();

            worldCircuits.Clear();
        }

        #endregion

        #region Debug

        void OnDrawGizmos()
        {
            if (!showDebugInfo) return;

            // Draw world grid
            if (showWorldGrid)
            {
                Gizmos.color = new Color(0, 1, 1, 0.2f);
                foreach (var kvp in worldObjects)
                {
                    if (kvp.Value != null)
                    {
                        Gizmos.DrawWireSphere(kvp.Value.transform.position, worldRadius);
                    }
                }
            }

            // Highlight active circuits
            if (highlightActiveCircuits)
            {
                Gizmos.color = Color.yellow;
                foreach (var circuit in circuitObjects.Values)
                {
                    if (circuit != null && circuit.activeSelf)
                    {
                        Gizmos.DrawWireCube(circuit.transform.position, Vector3.one * 10f);
                    }
                }
            }
        }

        #endregion
    }

    #region Event Definitions

    /// <summary>
    /// Event fired when a circuit is inserted into the database.
    /// </summary>
    public class CircuitInsertedEvent : SpacetimeDB.Types.IGameEvent
    {
        public System.DateTime Timestamp { get; set; }
        public string EventName => "CircuitInserted";
        public WorldCircuit Circuit { get; }

        public CircuitInsertedEvent(WorldCircuit circuit)
        {
            Circuit = circuit;
            Timestamp = System.DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event fired when a circuit is updated in the database.
    /// </summary>
    public class CircuitUpdatedEvent : SpacetimeDB.Types.IGameEvent
    {
        public System.DateTime Timestamp { get; set; }
        public string EventName => "CircuitUpdated";
        public WorldCircuit Circuit { get; }

        public CircuitUpdatedEvent(WorldCircuit circuit)
        {
            Circuit = circuit;
            Timestamp = System.DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event fired when a circuit is deleted from the database.
    /// </summary>
    public class CircuitDeletedEvent : SpacetimeDB.Types.IGameEvent
    {
        public System.DateTime Timestamp { get; set; }
        public string EventName => "CircuitDeleted";
        public ulong CircuitId { get; }

        public CircuitDeletedEvent(ulong circuitId)
        {
            CircuitId = circuitId;
            Timestamp = System.DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event fired when a tunnel forms between circuits.
    /// </summary>
    public class TunnelFormedEvent : SpacetimeDB.Types.IGameEvent
    {
        public System.DateTime Timestamp { get; set; }
        public string EventName => "TunnelFormed";
        public ulong SourceCircuitId { get; }
        public ulong TargetCircuitId { get; }
        public TunnelType TunnelType { get; }

        public TunnelFormedEvent(ulong source, ulong target, TunnelType type)
        {
            SourceCircuitId = source;
            TargetCircuitId = target;
            TunnelType = type;
            Timestamp = System.DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event fired when a tunnel breaks.
    /// </summary>
    public class TunnelBrokenEvent : SpacetimeDB.Types.IGameEvent
    {
        public System.DateTime Timestamp { get; set; }
        public string EventName => "TunnelBroken";
        public ulong Circuit1Id { get; }
        public ulong Circuit2Id { get; }

        public TunnelBrokenEvent(ulong circuit1, ulong circuit2)
        {
            Circuit1Id = circuit1;
            Circuit2Id = circuit2;
            Timestamp = System.DateTime.UtcNow;
        }
    }

    #endregion
}