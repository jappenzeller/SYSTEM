using UnityEngine;
using System.Collections.Generic;
using SYSTEM.Circuits;

namespace SYSTEM.Circuits
{
    /// <summary>
    /// Renders quantum tunnels as curved energy beams between circuit ring assemblies.
    /// Handles beam visualization, energy packet flow animation, and bi-directional tunnels.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class QuantumTunnelRenderer : MonoBehaviour
    {
        [Header("Tunnel Configuration")]
        [SerializeField] private RingAssemblyController sourceRing;
        [SerializeField] private RingAssemblyController targetRing;
        [SerializeField] private TunnelType tunnelType = TunnelType.Blue;
        [SerializeField] private bool isBidirectional = false;
        [SerializeField] private float tunnelStrength = 1f;

        [Header("Beam Settings")]
        [SerializeField] private int beamSegments = 50;
        [SerializeField] private float beamWidth = 1f;
        [SerializeField] private float beamCurvature = 0.2f; // Height multiplier for arc
        [SerializeField] private AnimationCurve beamWidthCurve = AnimationCurve.Linear(0, 1, 1, 1);
        [SerializeField] private Material beamMaterial;

        [Header("Energy Flow")]
        [SerializeField] private bool animateEnergyFlow = true;
        [SerializeField] private float flowSpeed = 50f; // Units per second
        [SerializeField] private GameObject energyPacketPrefab;
        [SerializeField] private int maxPackets = 10;
        [SerializeField] private float packetSpawnInterval = 1f;
        [SerializeField] private float packetSize = 0.5f;

        [Header("Visual Effects")]
        [SerializeField] private bool useDistortionEffect = true;
        [SerializeField] private float distortionStrength = 0.1f;
        [SerializeField] private ParticleSystem tunnelParticles;
        [SerializeField] private float particleDensity = 10f; // Particles per unit length

        [Header("Performance")]
        [SerializeField] private bool adaptiveQuality = true;
        [SerializeField] private float lodDistance = 500f;
        [SerializeField] private int lodMinSegments = 10;

        [Header("Debug")]
        [SerializeField] private bool showDebugPath = false;
        [SerializeField] private bool showPacketTrajectories = false;

        // Components
        private LineRenderer lineRenderer;
        private MaterialPropertyBlock propertyBlock;
        private List<GameObject> energyPackets = new List<GameObject>();
        private List<float> packetProgress = new List<float>();
        private List<bool> packetDirections = new List<bool>(); // true = forward, false = backward

        // State
        private Vector3[] beamPoints;
        private float flowOffset = 0f;
        private float nextPacketTime = 0f;
        private bool isActive = false;
        private Color tunnelColor;
        private float currentWidth;
        private Transform cameraTransform;

        // Path calculation cache
        private Vector3[] cachedPath;
        private float pathLength;
        private bool pathNeedsUpdate = true;

        #region Initialization

        void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            propertyBlock = new MaterialPropertyBlock();

            // Configure line renderer
            lineRenderer.positionCount = beamSegments;
            lineRenderer.useWorldSpace = true;

            if (beamMaterial != null)
            {
                lineRenderer.material = beamMaterial;
            }

            // Initialize beam points array
            beamPoints = new Vector3[beamSegments];
            cachedPath = new Vector3[beamSegments];

            // Create energy packet pool
            CreateEnergyPacketPool();
        }

        void Start()
        {
            // Find camera for LOD
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            // Set initial color based on tunnel type
            tunnelColor = GetTunnelColor(tunnelType);
            UpdateBeamColor();

            // Start with tunnel inactive
            SetActive(false);
        }

        void OnDestroy()
        {
            // Clean up energy packets
            foreach (var packet in energyPackets)
            {
                if (packet != null)
                {
                    Destroy(packet);
                }
            }
        }

        #endregion

        #region Tunnel Initialization

        /// <summary>
        /// Initializes the tunnel between two ring assemblies.
        /// </summary>
        public void Initialize(RingAssemblyController source, RingAssemblyController target, TunnelType type, bool bidirectional = false)
        {
            sourceRing = source;
            targetRing = target;
            tunnelType = type;
            isBidirectional = bidirectional;

            tunnelColor = GetTunnelColor(type);
            pathNeedsUpdate = true;

            UpdateBeamColor();
        }

        /// <summary>
        /// Forms the tunnel and begins rendering.
        /// </summary>
        public void FormTunnel(float strength = 1f)
        {
            tunnelStrength = Mathf.Clamp01(strength);
            isActive = true;
            SetActive(true);

            // Start particle effects
            if (tunnelParticles != null)
            {
                tunnelParticles.Play();
            }

            // Reset flow
            flowOffset = 0f;
            nextPacketTime = 0f;
        }

        /// <summary>
        /// Breaks the tunnel connection.
        /// </summary>
        public void BreakTunnel()
        {
            isActive = false;
            SetActive(false);

            // Stop particles
            if (tunnelParticles != null)
            {
                tunnelParticles.Stop();
            }

            // Hide all energy packets
            HideAllPackets();
        }

        #endregion

        #region Beam Rendering

        void Update()
        {
            if (!isActive) return;

            // Check if we need to update the path
            if (pathNeedsUpdate || HasEndpointsMoved())
            {
                UpdateBeamPath();
            }

            // Update LOD if needed
            if (adaptiveQuality)
            {
                UpdateLOD();
            }

            // Animate energy flow
            if (animateEnergyFlow)
            {
                UpdateEnergyFlow();
            }

            // Update energy packets
            UpdateEnergyPackets();

            // Update visual properties
            UpdateBeamProperties();
        }

        /// <summary>
        /// Calculates and updates the curved beam path between rings.
        /// </summary>
        private void UpdateBeamPath()
        {
            if (sourceRing == null || targetRing == null) return;

            Vector3 start = sourceRing.transform.position;
            Vector3 end = targetRing.transform.position;
            Vector3 direction = end - start;
            float distance = direction.magnitude;

            // Calculate control points for curve
            Vector3 midPoint = (start + end) * 0.5f;

            // Add curvature perpendicular to the connection
            Vector3 perpendicular = Vector3.Cross(direction.normalized, Vector3.up);
            if (perpendicular.magnitude < 0.001f)
            {
                perpendicular = Vector3.Cross(direction.normalized, Vector3.right);
            }
            perpendicular.Normalize();

            // Height of the arc based on distance
            float arcHeight = distance * beamCurvature;
            Vector3 controlPoint = midPoint + (Vector3.up * arcHeight) + (perpendicular * arcHeight * 0.3f);

            // Generate curved path using quadratic Bezier curve
            pathLength = 0f;
            for (int i = 0; i < beamSegments; i++)
            {
                float t = (float)i / (beamSegments - 1);

                // Quadratic Bezier curve formula
                Vector3 point = Mathf.Pow(1 - t, 2) * start +
                               2 * (1 - t) * t * controlPoint +
                               Mathf.Pow(t, 2) * end;

                beamPoints[i] = point;
                cachedPath[i] = point;

                // Calculate path length
                if (i > 0)
                {
                    pathLength += Vector3.Distance(beamPoints[i], beamPoints[i - 1]);
                }
            }

            // Apply to line renderer
            lineRenderer.SetPositions(beamPoints);

            // Update particle system if present
            UpdateParticleSystem();

            pathNeedsUpdate = false;
        }

        /// <summary>
        /// Checks if the tunnel endpoints have moved.
        /// </summary>
        private bool HasEndpointsMoved()
        {
            if (sourceRing == null || targetRing == null) return false;

            float moveThreshold = 0.1f;
            return Vector3.Distance(sourceRing.transform.position, cachedPath[0]) > moveThreshold ||
                   Vector3.Distance(targetRing.transform.position, cachedPath[cachedPath.Length - 1]) > moveThreshold;
        }

        #endregion

        #region Energy Flow Animation

        /// <summary>
        /// Updates the energy flow animation along the tunnel.
        /// </summary>
        private void UpdateEnergyFlow()
        {
            flowOffset += flowSpeed * Time.deltaTime;

            // Update material texture offset for scrolling effect
            if (lineRenderer.material != null)
            {
                lineRenderer.material.SetFloat("_FlowOffset", flowOffset);
                lineRenderer.material.SetFloat("_FlowSpeed", flowSpeed);
            }

            // Spawn new energy packets
            if (Time.time >= nextPacketTime)
            {
                SpawnEnergyPacket();
                nextPacketTime = Time.time + packetSpawnInterval;
            }
        }

        /// <summary>
        /// Creates and initializes the energy packet object pool.
        /// </summary>
        private void CreateEnergyPacketPool()
        {
            if (energyPacketPrefab == null)
            {
                // Create default packet if no prefab assigned
                energyPacketPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                energyPacketPrefab.transform.localScale = Vector3.one * packetSize;

                // Add emission
                var renderer = energyPacketPrefab.GetComponent<MeshRenderer>();
                if (renderer != null && beamMaterial != null)
                {
                    renderer.material = beamMaterial;
                }

                Destroy(energyPacketPrefab.GetComponent<Collider>());
                energyPacketPrefab.SetActive(false);
            }

            // Create packet pool
            for (int i = 0; i < maxPackets; i++)
            {
                GameObject packet = Instantiate(energyPacketPrefab, transform);
                packet.name = $"EnergyPacket_{i}";
                packet.SetActive(false);
                energyPackets.Add(packet);
                packetProgress.Add(-1f); // -1 means inactive
                packetDirections.Add(true);
            }
        }

        /// <summary>
        /// Spawns a new energy packet at the tunnel start.
        /// </summary>
        private void SpawnEnergyPacket()
        {
            // Find an inactive packet
            for (int i = 0; i < energyPackets.Count; i++)
            {
                if (packetProgress[i] < 0f)
                {
                    // Activate packet
                    packetProgress[i] = 0f;
                    packetDirections[i] = !isBidirectional || Random.value > 0.5f; // Random direction if bidirectional

                    if (!packetDirections[i])
                    {
                        packetProgress[i] = 1f; // Start at the end if going backward
                    }

                    energyPackets[i].SetActive(true);

                    // Set packet color
                    var renderer = energyPackets[i].GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        renderer.material.SetColor("_EmissionColor", tunnelColor * 2f);
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// Updates positions of all active energy packets.
        /// </summary>
        private void UpdateEnergyPackets()
        {
            for (int i = 0; i < energyPackets.Count; i++)
            {
                if (packetProgress[i] < 0f) continue;

                // Update progress
                float speed = flowSpeed / pathLength; // Normalized speed
                if (packetDirections[i])
                {
                    packetProgress[i] += speed * Time.deltaTime;
                }
                else
                {
                    packetProgress[i] -= speed * Time.deltaTime;
                }

                // Check if packet reached the end
                if (packetProgress[i] > 1f || packetProgress[i] < 0f)
                {
                    // Deactivate packet
                    packetProgress[i] = -1f;
                    energyPackets[i].SetActive(false);
                    continue;
                }

                // Update packet position along the path
                Vector3 position = GetPositionOnPath(packetProgress[i]);
                energyPackets[i].transform.position = position;

                // Add some oscillation for visual interest
                float oscillation = Mathf.Sin(Time.time * 10f + i) * 0.2f;
                energyPackets[i].transform.position += transform.up * oscillation;

                // Update packet size based on position (smaller at edges)
                float sizeCurve = beamWidthCurve.Evaluate(packetProgress[i]);
                energyPackets[i].transform.localScale = Vector3.one * (packetSize * sizeCurve * tunnelStrength);
            }
        }

        /// <summary>
        /// Gets a position along the beam path at normalized distance t.
        /// </summary>
        private Vector3 GetPositionOnPath(float t)
        {
            if (cachedPath == null || cachedPath.Length == 0)
                return Vector3.zero;

            // Find the segment
            float segment = t * (cachedPath.Length - 1);
            int index = Mathf.FloorToInt(segment);
            float localT = segment - index;

            // Clamp indices
            index = Mathf.Clamp(index, 0, cachedPath.Length - 2);

            // Interpolate between points
            return Vector3.Lerp(cachedPath[index], cachedPath[index + 1], localT);
        }

        /// <summary>
        /// Hides all energy packets.
        /// </summary>
        private void HideAllPackets()
        {
            for (int i = 0; i < energyPackets.Count; i++)
            {
                packetProgress[i] = -1f;
                energyPackets[i].SetActive(false);
            }
        }

        #endregion

        #region Visual Properties

        /// <summary>
        /// Updates beam visual properties based on current state.
        /// </summary>
        private void UpdateBeamProperties()
        {
            // Update width based on strength
            currentWidth = beamWidth * tunnelStrength;

            // Apply width curve along the beam
            AnimationCurve widthCurve = new AnimationCurve();
            for (int i = 0; i < beamSegments; i++)
            {
                float t = (float)i / (beamSegments - 1);
                float width = currentWidth * beamWidthCurve.Evaluate(t);
                widthCurve.AddKey(t, width);
            }
            lineRenderer.widthCurve = widthCurve;

            // Update material properties
            if (lineRenderer.material != null)
            {
                lineRenderer.material.SetFloat("_Strength", tunnelStrength);
                lineRenderer.material.SetFloat("_DistortionStrength", useDistortionEffect ? distortionStrength : 0f);
            }
        }

        /// <summary>
        /// Updates the beam color based on tunnel type.
        /// </summary>
        private void UpdateBeamColor()
        {
            lineRenderer.startColor = tunnelColor;
            lineRenderer.endColor = tunnelColor * 0.7f; // Slightly darker at the end

            // Update material emission
            if (lineRenderer.material != null)
            {
                lineRenderer.material.SetColor("_EmissionColor", tunnelColor * tunnelStrength * 2f);
            }
        }

        /// <summary>
        /// Updates the particle system along the beam.
        /// </summary>
        private void UpdateParticleSystem()
        {
            if (tunnelParticles == null) return;

            // Configure particle system shape to follow the beam
            var shape = tunnelParticles.shape;
            shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;

            // Set emission rate based on path length
            var emission = tunnelParticles.emission;
            emission.rateOverTime = pathLength * particleDensity;

            // Set particle color
            var main = tunnelParticles.main;
            main.startColor = tunnelColor;
        }

        #endregion

        #region LOD System

        /// <summary>
        /// Updates level of detail based on camera distance.
        /// </summary>
        private void UpdateLOD()
        {
            if (cameraTransform == null) return;

            // Calculate distance to camera
            Vector3 midPoint = GetPositionOnPath(0.5f);
            float distance = Vector3.Distance(midPoint, cameraTransform.position);

            if (distance > lodDistance)
            {
                // Reduce quality for distant tunnels
                int reducedSegments = Mathf.Max(lodMinSegments, beamSegments / 3);
                if (lineRenderer.positionCount != reducedSegments)
                {
                    lineRenderer.positionCount = reducedSegments;
                    ResamplePath(reducedSegments);
                }

                // Hide energy packets
                HideAllPackets();
            }
            else
            {
                // Full quality for nearby tunnels
                if (lineRenderer.positionCount != beamSegments)
                {
                    lineRenderer.positionCount = beamSegments;
                    pathNeedsUpdate = true;
                }
            }
        }

        /// <summary>
        /// Resamples the beam path with a different number of segments.
        /// </summary>
        private void ResamplePath(int newSegments)
        {
            Vector3[] resampledPoints = new Vector3[newSegments];

            for (int i = 0; i < newSegments; i++)
            {
                float t = (float)i / (newSegments - 1);
                resampledPoints[i] = GetPositionOnPath(t);
            }

            lineRenderer.SetPositions(resampledPoints);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets the color for a tunnel type.
        /// </summary>
        private Color GetTunnelColor(TunnelType type)
        {
            switch (type)
            {
                case TunnelType.Blue: return CircuitConstants.FREQUENCY_BLUE;
                case TunnelType.Red: return CircuitConstants.FREQUENCY_RED;
                case TunnelType.Green: return CircuitConstants.FREQUENCY_GREEN;
                case TunnelType.Yellow: return CircuitConstants.FREQUENCY_YELLOW;
                case TunnelType.Cyan: return CircuitConstants.FREQUENCY_CYAN;
                case TunnelType.Magenta: return CircuitConstants.FREQUENCY_MAGENTA;
                default: return Color.white;
            }
        }

        /// <summary>
        /// Enables or disables the tunnel renderer.
        /// </summary>
        private void SetActive(bool active)
        {
            lineRenderer.enabled = active;

            if (!active)
            {
                HideAllPackets();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the tunnel strength (0-1).
        /// </summary>
        public void SetStrength(float strength)
        {
            tunnelStrength = Mathf.Clamp01(strength);
        }

        /// <summary>
        /// Gets the current tunnel strength.
        /// </summary>
        public float GetStrength()
        {
            return tunnelStrength;
        }

        /// <summary>
        /// Checks if the tunnel is currently active.
        /// </summary>
        public bool IsActive()
        {
            return isActive;
        }

        /// <summary>
        /// Gets the total length of the tunnel path.
        /// </summary>
        public float GetPathLength()
        {
            return pathLength;
        }

        #endregion

        #region Debug

        void OnDrawGizmos()
        {
            if (!showDebugPath) return;

            // Draw beam path
            if (cachedPath != null && cachedPath.Length > 1)
            {
                Gizmos.color = tunnelColor;
                for (int i = 0; i < cachedPath.Length - 1; i++)
                {
                    Gizmos.DrawLine(cachedPath[i], cachedPath[i + 1]);
                }
            }

            // Draw packet trajectories
            if (showPacketTrajectories)
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < energyPackets.Count; i++)
                {
                    if (packetProgress[i] >= 0f)
                    {
                        Gizmos.DrawWireSphere(energyPackets[i].transform.position, packetSize);
                    }
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            // Draw connection between rings
            if (sourceRing != null && targetRing != null)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
                Gizmos.DrawLine(sourceRing.transform.position, targetRing.transform.position);
            }
        }

        #endregion
    }
}