using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;

namespace SYSTEM.Game
{
    /// <summary>
    /// Manages visualization of Energy Spire structures (WorldCircuit + DistributionSphere + QuantumTunnel)
    /// Subscribes to GameEventBus events published by SpacetimeDBEventBridge
    /// </summary>
    public class EnergySpireManager : MonoBehaviour
    {
        [Header("Spire Visualization Settings")]
        [SerializeField] private bool enableVisualization = true;

        [Header("Prefabs (Optional - will use primitives if not set)")]
        [SerializeField] private GameObject circuitBasePrefab;
        [SerializeField] private GameObject distributionSpherePrefab;
        [SerializeField] private GameObject quantumTunnelRingPrefab;

        [Header("Primitive Settings")]
        [SerializeField] private float circuitBaseRadius = 2f;
        [SerializeField] private float circuitBaseHeight = 0.5f;
        [SerializeField] private float distributionSphereRadius = 1.5f;
        [SerializeField] private float sphereHeightOffset = 5f;
        [SerializeField] private float tunnelRingRadius = 3f;
        [SerializeField] private float tunnelRingHeight = 0.3f;
        [SerializeField] private float tunnelHeightOffset = 10f;

        [Header("Colors")]
        [SerializeField] private Color circuitBaseColor = new Color(0.3f, 0.3f, 0.3f);
        [SerializeField] private Color sphereColor = new Color(0.2f, 0.6f, 1f);
        // Cardinal colors (6)
        [SerializeField] private Color redTunnelColor = Color.red;
        [SerializeField] private Color greenTunnelColor = Color.green;
        [SerializeField] private Color blueTunnelColor = Color.blue;
        // Edge colors (12)
        [SerializeField] private Color yellowTunnelColor = Color.yellow;
        [SerializeField] private Color cyanTunnelColor = Color.cyan;
        [SerializeField] private Color magentaTunnelColor = Color.magenta;
        // Vertex colors (8)
        [SerializeField] private Color whiteTunnelColor = Color.white;

        // Track active spire components by their database IDs
        private Dictionary<ulong, GameObject> activeCircuits = new Dictionary<ulong, GameObject>();
        private Dictionary<ulong, GameObject> activeSpheres = new Dictionary<ulong, GameObject>();
        private Dictionary<ulong, GameObject> activeTunnels = new Dictionary<ulong, GameObject>();

        // Parent transform for organization
        private Transform spiresParent;

        void Awake()
        {
            // Create parent container
            GameObject parent = new GameObject("EnergySpires");
            spiresParent = parent.transform;
            spiresParent.SetParent(transform);
        }

        void OnEnable()
        {
            if (!enableVisualization) return;

            // Subscribe to GameEventBus spire events
            GameEventBus.Instance.Subscribe<InitialSpiresLoadedEvent>(OnInitialSpiresLoaded);

            GameEventBus.Instance.Subscribe<DistributionSphereInsertedEvent>(OnDistributionSphereInserted);
            GameEventBus.Instance.Subscribe<DistributionSphereUpdatedEvent>(OnDistributionSphereUpdated);
            GameEventBus.Instance.Subscribe<DistributionSphereDeletedEvent>(OnDistributionSphereDeleted);

            GameEventBus.Instance.Subscribe<QuantumTunnelInsertedEvent>(OnQuantumTunnelInserted);
            GameEventBus.Instance.Subscribe<QuantumTunnelUpdatedEvent>(OnQuantumTunnelUpdated);
            GameEventBus.Instance.Subscribe<QuantumTunnelDeletedEvent>(OnQuantumTunnelDeleted);

            GameEventBus.Instance.Subscribe<WorldCircuitInsertedEvent>(OnWorldCircuitInserted);
            GameEventBus.Instance.Subscribe<WorldCircuitUpdatedEvent>(OnWorldCircuitUpdated);
            GameEventBus.Instance.Subscribe<WorldCircuitDeletedEvent>(OnWorldCircuitDeleted);

            SystemDebug.Log(SystemDebug.Category.SpireVisualization, "EnergySpireManager subscribed to spire events");
        }

        void OnDisable()
        {
            // Unsubscribe from all events
            GameEventBus.Instance.Unsubscribe<InitialSpiresLoadedEvent>(OnInitialSpiresLoaded);

            GameEventBus.Instance.Unsubscribe<DistributionSphereInsertedEvent>(OnDistributionSphereInserted);
            GameEventBus.Instance.Unsubscribe<DistributionSphereUpdatedEvent>(OnDistributionSphereUpdated);
            GameEventBus.Instance.Unsubscribe<DistributionSphereDeletedEvent>(OnDistributionSphereDeleted);

            GameEventBus.Instance.Unsubscribe<QuantumTunnelInsertedEvent>(OnQuantumTunnelInserted);
            GameEventBus.Instance.Unsubscribe<QuantumTunnelUpdatedEvent>(OnQuantumTunnelUpdated);
            GameEventBus.Instance.Unsubscribe<QuantumTunnelDeletedEvent>(OnQuantumTunnelDeleted);

            GameEventBus.Instance.Unsubscribe<WorldCircuitInsertedEvent>(OnWorldCircuitInserted);
            GameEventBus.Instance.Unsubscribe<WorldCircuitUpdatedEvent>(OnWorldCircuitUpdated);
            GameEventBus.Instance.Unsubscribe<WorldCircuitDeletedEvent>(OnWorldCircuitDeleted);
        }

        #region Event Handlers

        void OnInitialSpiresLoaded(InitialSpiresLoadedEvent evt)
        {
            SystemDebug.Log(SystemDebug.Category.SpireVisualization,
                $"Loading initial spires: {evt.Spheres.Count} spheres, {evt.Tunnels.Count} tunnels, {evt.Circuits.Count} circuits");

            // Load all circuits first (ground level)
            foreach (var circuit in evt.Circuits)
            {
                CreateCircuitVisualization(circuit);
            }

            // Then distribution spheres (mid level)
            foreach (var sphere in evt.Spheres)
            {
                CreateSphereVisualization(sphere);
            }

            // Finally quantum tunnels (top level)
            foreach (var tunnel in evt.Tunnels)
            {
                CreateTunnelVisualization(tunnel);
            }
        }

        // Distribution Sphere events
        void OnDistributionSphereInserted(DistributionSphereInsertedEvent evt)
        {
            CreateSphereVisualization(evt.Sphere);
        }

        void OnDistributionSphereUpdated(DistributionSphereUpdatedEvent evt)
        {
            UpdateSphereVisualization(evt.NewSphere);
        }

        void OnDistributionSphereDeleted(DistributionSphereDeletedEvent evt)
        {
            DestroySphereVisualization(evt.Sphere.SphereId);
        }

        // Quantum Tunnel events
        void OnQuantumTunnelInserted(QuantumTunnelInsertedEvent evt)
        {
            CreateTunnelVisualization(evt.Tunnel);
        }

        void OnQuantumTunnelUpdated(QuantumTunnelUpdatedEvent evt)
        {
            UpdateTunnelVisualization(evt.NewTunnel);
        }

        void OnQuantumTunnelDeleted(QuantumTunnelDeletedEvent evt)
        {
            DestroyTunnelVisualization(evt.Tunnel.TunnelId);
        }

        // World Circuit events
        void OnWorldCircuitInserted(WorldCircuitInsertedEvent evt)
        {
            CreateCircuitVisualization(evt.Circuit);
        }

        void OnWorldCircuitUpdated(WorldCircuitUpdatedEvent evt)
        {
            UpdateCircuitVisualization(evt.NewCircuit);
        }

        void OnWorldCircuitDeleted(WorldCircuitDeletedEvent evt)
        {
            DestroyCircuitVisualization(evt.Circuit.CircuitId);
        }

        #endregion

        #region Visualization Creation

        void CreateCircuitVisualization(WorldCircuit circuit)
        {
            if (activeCircuits.ContainsKey(circuit.CircuitId))
            {
                SystemDebug.LogWarning(SystemDebug.Category.SpireVisualization,
                    $"Circuit {circuit.CircuitId} already exists, skipping creation");
                return;
            }

            GameObject circuitObj;

            if (circuitBasePrefab != null)
            {
                circuitObj = Instantiate(circuitBasePrefab, spiresParent);
            }
            else
            {
                // Create primitive cylinder for circuit base
                circuitObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                circuitObj.transform.SetParent(spiresParent);
                circuitObj.transform.localScale = new Vector3(circuitBaseRadius * 2, circuitBaseHeight, circuitBaseRadius * 2);

                // Set color with proper material for WebGL compatibility
                var renderer = circuitObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material mat = CreateSafeMaterial(circuitBaseColor, 0.2f, 0.5f);
                    if (mat != null)
                    {
                        renderer.material = mat;
                    }
                }
            }

            // Position on world surface using cardinal direction
            Vector3 position = GetCardinalPosition(circuit.CardinalDirection);
            circuitObj.transform.position = position;
            circuitObj.name = $"Circuit_{circuit.CircuitId}_{circuit.CardinalDirection}";

            activeCircuits[circuit.CircuitId] = circuitObj;

            SystemDebug.Log(SystemDebug.Category.SpireVisualization,
                $"Created circuit {circuit.CircuitId} at {circuit.CardinalDirection}");
        }

        void CreateSphereVisualization(DistributionSphere sphere)
        {
            if (activeSpheres.ContainsKey(sphere.SphereId))
            {
                SystemDebug.LogWarning(SystemDebug.Category.SpireVisualization,
                    $"Sphere {sphere.SphereId} already exists, skipping creation");
                return;
            }

            GameObject sphereObj;

            if (distributionSpherePrefab != null)
            {
                sphereObj = Instantiate(distributionSpherePrefab, spiresParent);
            }
            else
            {
                // Create primitive sphere
                sphereObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphereObj.transform.SetParent(spiresParent);
                sphereObj.transform.localScale = Vector3.one * (distributionSphereRadius * 2);

                // Set color with proper material for WebGL compatibility
                var renderer = sphereObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material mat = CreateSafeMaterial(sphereColor, 0.3f, 0.7f, true, sphereColor * 0.3f);
                    if (mat != null)
                    {
                        renderer.material = mat;
                    }
                }
            }

            // Position above world surface
            Vector3 basePosition = new Vector3(sphere.SpherePosition.X, sphere.SpherePosition.Y, sphere.SpherePosition.Z);
            Vector3 upDirection = basePosition.normalized;
            sphereObj.transform.position = basePosition + upDirection * sphereHeightOffset;
            sphereObj.name = $"Sphere_{sphere.SphereId}_{sphere.CardinalDirection}";

            activeSpheres[sphere.SphereId] = sphereObj;

            SystemDebug.Log(SystemDebug.Category.SpireVisualization,
                $"Created distribution sphere {sphere.SphereId} at {sphere.CardinalDirection}");
        }

        void CreateTunnelVisualization(QuantumTunnel tunnel)
        {
            if (activeTunnels.ContainsKey(tunnel.TunnelId))
            {
                SystemDebug.LogWarning(SystemDebug.Category.SpireVisualization,
                    $"Tunnel {tunnel.TunnelId} already exists, skipping creation");
                return;
            }

            GameObject tunnelObj;

            if (quantumTunnelRingPrefab != null)
            {
                tunnelObj = Instantiate(quantumTunnelRingPrefab, spiresParent);
            }
            else
            {
                // Create primitive torus-like structure (using cylinder for now)
                tunnelObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tunnelObj.transform.SetParent(spiresParent);
                tunnelObj.transform.localScale = new Vector3(tunnelRingRadius * 2, tunnelRingHeight, tunnelRingRadius * 2);

                // Set color based on tunnel color with proper material for WebGL compatibility
                var renderer = tunnelObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Color tunnelColor = GetTunnelColor(tunnel.TunnelColor);
                    bool hasCharge = tunnel.RingCharge > 0;
                    Color emissionColor = hasCharge ? tunnelColor * (tunnel.RingCharge / 100f) : Color.black;

                    Material mat = CreateSafeMaterial(tunnelColor, 0.5f, 0.8f, hasCharge, emissionColor);
                    if (mat != null)
                    {
                        renderer.material = mat;
                    }
                }
            }

            // Position at top of spire
            // Calculate position from cardinal direction
            Vector3 cardinalPos = GetCardinalPosition(tunnel.CardinalDirection);
            Vector3 upDirection = cardinalPos.normalized;
            tunnelObj.transform.position = cardinalPos + upDirection * tunnelHeightOffset;
            tunnelObj.name = $"Tunnel_{tunnel.TunnelId}_{tunnel.CardinalDirection}";

            activeTunnels[tunnel.TunnelId] = tunnelObj;

            SystemDebug.Log(SystemDebug.Category.SpireVisualization,
                $"Created quantum tunnel {tunnel.TunnelId} at {tunnel.CardinalDirection} with charge {tunnel.RingCharge}%");
        }

        #endregion

        #region Visualization Updates

        void UpdateCircuitVisualization(WorldCircuit circuit)
        {
            if (!activeCircuits.ContainsKey(circuit.CircuitId))
            {
                SystemDebug.LogWarning(SystemDebug.Category.SpireVisualization,
                    $"Cannot update circuit {circuit.CircuitId} - not found");
                return;
            }

            // For now, circuits don't have dynamic properties to update
            // This is a placeholder for future enhancements
        }

        void UpdateSphereVisualization(DistributionSphere sphere)
        {
            if (!activeSpheres.ContainsKey(sphere.SphereId))
            {
                SystemDebug.LogWarning(SystemDebug.Category.SpireVisualization,
                    $"Cannot update sphere {sphere.SphereId} - not found");
                return;
            }

            // Could update visual effects based on packets_routed
            // This is a placeholder for future enhancements
        }

        void UpdateTunnelVisualization(QuantumTunnel tunnel)
        {
            if (!activeTunnels.ContainsKey(tunnel.TunnelId))
            {
                SystemDebug.LogWarning(SystemDebug.Category.SpireVisualization,
                    $"Cannot update tunnel {tunnel.TunnelId} - not found");
                return;
            }

            GameObject tunnelObj = activeTunnels[tunnel.TunnelId];
            var renderer = tunnelObj.GetComponent<Renderer>();

            if (renderer != null && renderer.material != null)
            {
                Color tunnelColor = GetTunnelColor(tunnel.TunnelColor);

                // Update emission based on charge
                if (tunnel.RingCharge > 0)
                {
                    renderer.material.EnableKeyword("_EMISSION");
                    renderer.material.SetColor("_EmissionColor", tunnelColor * tunnel.RingCharge / 100f);
                }
                else
                {
                    renderer.material.DisableKeyword("_EMISSION");
                    renderer.material.SetColor("_EmissionColor", Color.black);
                }
            }

            SystemDebug.Log(SystemDebug.Category.SpireVisualization,
                $"Updated tunnel {tunnel.TunnelId} charge to {tunnel.RingCharge}%");
        }

        #endregion

        #region Visualization Destruction

        void DestroyCircuitVisualization(ulong circuitId)
        {
            if (activeCircuits.TryGetValue(circuitId, out GameObject circuitObj))
            {
                Destroy(circuitObj);
                activeCircuits.Remove(circuitId);
                SystemDebug.Log(SystemDebug.Category.SpireVisualization, $"Destroyed circuit {circuitId}");
            }
        }

        void DestroySphereVisualization(ulong sphereId)
        {
            if (activeSpheres.TryGetValue(sphereId, out GameObject sphereObj))
            {
                Destroy(sphereObj);
                activeSpheres.Remove(sphereId);
                SystemDebug.Log(SystemDebug.Category.SpireVisualization, $"Destroyed sphere {sphereId}");
            }
        }

        void DestroyTunnelVisualization(ulong tunnelId)
        {
            if (activeTunnels.TryGetValue(tunnelId, out GameObject tunnelObj))
            {
                Destroy(tunnelObj);
                activeTunnels.Remove(tunnelId);
                SystemDebug.Log(SystemDebug.Category.SpireVisualization, $"Destroyed tunnel {tunnelId}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Create a material safely for WebGL compatibility
        /// Falls back to primitive's existing material if Standard shader not found
        /// </summary>
        Material CreateSafeMaterial(Color color, float metallic, float glossiness, bool enableEmission = false, Color emissionColor = default(Color))
        {
            // Try to find Standard shader (URP)
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                // Fallback to built-in Standard
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                // Last resort: Unlit/Color
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                SystemDebug.LogError(SystemDebug.Category.SpireVisualization,
                    "Could not find any suitable shader for spire materials!");
                return null;
            }

            Material mat = new Material(shader);
            mat.color = color;

            // Try to set material properties (might not exist on all shaders)
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Glossiness") || mat.HasProperty("_Smoothness"))
                mat.SetFloat(mat.HasProperty("_Glossiness") ? "_Glossiness" : "_Smoothness", glossiness);

            if (enableEmission && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor);
            }

            return mat;
        }

        Color GetTunnelColor(string colorName)
        {
            switch (colorName)
            {
                // Cardinal colors (6)
                case "Red": return redTunnelColor;
                case "Green": return greenTunnelColor;
                case "Blue": return blueTunnelColor;
                // Edge colors (12)
                case "Yellow": return yellowTunnelColor;
                case "Cyan": return cyanTunnelColor;
                case "Magenta": return magentaTunnelColor;
                // Vertex colors (8)
                case "White": return whiteTunnelColor;
                default: return Color.gray;
            }
        }

        Vector3 GetCardinalPosition(string direction)
        {
            const float R = 300f;
            const float SQRT2 = 1.414213562373095f;
            const float SQRT3 = 1.732050807568877f;

            switch (direction)
            {
                // Cardinal (6 - face centers)
                case "North": return new Vector3(0, R, 0);
                case "South": return new Vector3(0, -R, 0);
                case "East": return new Vector3(R, 0, 0);
                case "West": return new Vector3(-R, 0, 0);
                case "Forward": return new Vector3(0, 0, R);
                case "Back": return new Vector3(0, 0, -R);

                // Edge centers (12 - between two cardinals)
                case "NorthEast": return new Vector3(R/SQRT2, R/SQRT2, 0);
                case "NorthWest": return new Vector3(-R/SQRT2, R/SQRT2, 0);
                case "SouthEast": return new Vector3(R/SQRT2, -R/SQRT2, 0);
                case "SouthWest": return new Vector3(-R/SQRT2, -R/SQRT2, 0);
                case "NorthForward": return new Vector3(0, R/SQRT2, R/SQRT2);
                case "NorthBack": return new Vector3(0, R/SQRT2, -R/SQRT2);
                case "SouthForward": return new Vector3(0, -R/SQRT2, R/SQRT2);
                case "SouthBack": return new Vector3(0, -R/SQRT2, -R/SQRT2);
                case "EastForward": return new Vector3(R/SQRT2, 0, R/SQRT2);
                case "EastBack": return new Vector3(R/SQRT2, 0, -R/SQRT2);
                case "WestForward": return new Vector3(-R/SQRT2, 0, R/SQRT2);
                case "WestBack": return new Vector3(-R/SQRT2, 0, -R/SQRT2);

                // Vertex corners (8 - between three cardinals)
                case "NorthEastForward": return new Vector3(R/SQRT3, R/SQRT3, R/SQRT3);
                case "NorthEastBack": return new Vector3(R/SQRT3, R/SQRT3, -R/SQRT3);
                case "NorthWestForward": return new Vector3(-R/SQRT3, R/SQRT3, R/SQRT3);
                case "NorthWestBack": return new Vector3(-R/SQRT3, R/SQRT3, -R/SQRT3);
                case "SouthEastForward": return new Vector3(R/SQRT3, -R/SQRT3, R/SQRT3);
                case "SouthEastBack": return new Vector3(R/SQRT3, -R/SQRT3, -R/SQRT3);
                case "SouthWestForward": return new Vector3(-R/SQRT3, -R/SQRT3, R/SQRT3);
                case "SouthWestBack": return new Vector3(-R/SQRT3, -R/SQRT3, -R/SQRT3);

                default: return new Vector3(0, R, 0); // Default to North
            }
        }

        /// <summary>
        /// Public API for TransferVisualizationManager to flash a sphere when packets route through it
        /// </summary>
        public void FlashSphereById(ulong sphereId)
        {
            if (activeSpheres.TryGetValue(sphereId, out GameObject sphereObj))
            {
                // TODO: Implement flash effect (coroutine that temporarily brightens the sphere)
                SystemDebug.Log(SystemDebug.Category.SpireVisualization, $"Flashing sphere {sphereId}");
            }
        }

        #endregion
    }
}
