using UnityEngine;
using System.Collections.Generic;
using SYSTEM.Circuits;
using SpacetimeDB.Types;

namespace SYSTEM.Circuits
{
    /// <summary>
    /// Controls the ring assembly above a circuit that visualizes quantum tunnel connections.
    /// A single ring per circuit can rotate to point toward connected worlds and shows
    /// directional tunnel information through color and orientation.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class RingAssemblyController : MonoBehaviour
    {
        [Header("Ring Configuration")]
        [SerializeField] private float ringRadius = CircuitConstants.PRIMARY_RING_RADIUS;
        [SerializeField] private float heightAboveSurface = CircuitConstants.RING_ASSEMBLY_HEIGHT;
        [SerializeField] private float ringThickness = 2f;
        [SerializeField] private int ringSegments = 32;

        [Header("Tunnel Connections")]
        [SerializeField] private bool isConnected = false;
        [SerializeField] private Vector3 targetWorldPosition;
        [SerializeField] private TunnelType tunnelType = TunnelType.Blue;
        [SerializeField] private float connectionStrength = 0f;

        [Header("Visual Settings")]
        [SerializeField] private Material ringMaterial;
        [SerializeField] private Material connectedRingMaterial;
        [SerializeField] private AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private float rotationSpeed = 90f; // Degrees per second
        [SerializeField] private bool autoAlign = true;

        [Header("Colors by Tunnel Type")]
        [SerializeField] private Color blueConnection = CircuitConstants.FREQUENCY_BLUE;
        [SerializeField] private Color redConnection = CircuitConstants.FREQUENCY_RED;
        [SerializeField] private Color greenConnection = CircuitConstants.FREQUENCY_GREEN;
        [SerializeField] private Color yellowConnection = CircuitConstants.FREQUENCY_YELLOW;
        [SerializeField] private Color cyanConnection = CircuitConstants.FREQUENCY_CYAN;
        [SerializeField] private Color magentaConnection = CircuitConstants.FREQUENCY_MAGENTA;

        [Header("Animation")]
        [SerializeField] private bool enableRotation = true;
        [SerializeField] private float idleRotationSpeed = 10f;
        [SerializeField] private float alignmentDuration = 2f;
        [SerializeField] private bool pulseWhenConnected = true;
        [SerializeField] private float pulseFrequency = 1f;
        [SerializeField] private float pulseAmplitude = 0.1f;

        [Header("Effects")]
        [SerializeField] private ParticleSystem connectionParticles;
        [SerializeField] private LineRenderer energyBeam;
        [SerializeField] private Light ringLight;
        [SerializeField] private float lightIntensityMultiplier = 2f;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = false;
        [SerializeField] private bool showTargetDirection = false;

        // Components
        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private MaterialPropertyBlock propertyBlock;
        private CircuitBase circuitBase;
        private DistributionSphere distributionSphere;

        // State
        private Quaternion targetRotation;
        private Quaternion currentRotation;
        private float alignmentProgress = 0f;
        private bool isAligning = false;
        private float pulseTimer = 0f;
        private Vector3 surfaceNormal = Vector3.up;
        private Color currentColor;
        private Mesh ringMesh;

        // Connection data
        private ulong connectedCircuitId;
        private WorldCoords connectedWorldCoords;
        private List<Vector3> potentialTargets = new List<Vector3>();

        #region Initialization

        void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            meshFilter = GetComponent<MeshFilter>();
            propertyBlock = new MaterialPropertyBlock();

            // Get references to other circuit components
            circuitBase = GetComponentInParent<CircuitBase>();
            distributionSphere = GetComponentInParent<DistributionSphere>();

            // Create ring mesh
            CreateRingMesh();

            // Setup light if not assigned
            if (ringLight == null)
            {
                GameObject lightObj = new GameObject("RingLight");
                lightObj.transform.SetParent(transform);
                lightObj.transform.localPosition = Vector3.zero;
                ringLight = lightObj.AddComponent<Light>();
                ringLight.type = LightType.Point;
                ringLight.range = ringRadius * 3f;
                ringLight.intensity = 0f;
            }

            // Apply initial material
            if (ringMaterial != null)
            {
                meshRenderer.material = ringMaterial;
            }
        }

        void Start()
        {
            // Get surface normal from parent
            if (transform.parent != null)
            {
                surfaceNormal = transform.parent.up;
            }

            // Position ring at correct height
            PositionAboveSurface();

            // Set initial rotation
            currentRotation = transform.rotation;
            targetRotation = currentRotation;

            // Subscribe to circuit events if available
            if (circuitBase != null)
            {
                circuitBase.OnFullyCharged += OnCircuitFullyCharged;
                circuitBase.OnChargeDepleted += OnCircuitChargeDepleted;
            }

            UpdateVisuals();
        }

        void OnDestroy()
        {
            // Unsubscribe from events
            if (circuitBase != null)
            {
                circuitBase.OnFullyCharged -= OnCircuitFullyCharged;
                circuitBase.OnChargeDepleted -= OnCircuitChargeDepleted;
            }
        }

        #endregion

        #region Mesh Generation

        /// <summary>
        /// Creates a torus mesh for the ring.
        /// </summary>
        private void CreateRingMesh()
        {
            ringMesh = new Mesh();
            ringMesh.name = "RingMesh";

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            int radialSegments = 16;
            float tubeRadius = ringThickness * 0.5f;

            for (int i = 0; i <= ringSegments; i++)
            {
                float u = (float)i / ringSegments;
                float mainAngle = u * Mathf.PI * 2f;
                Vector3 ringCenter = new Vector3(
                    Mathf.Cos(mainAngle) * ringRadius,
                    0,
                    Mathf.Sin(mainAngle) * ringRadius
                );

                for (int j = 0; j <= radialSegments; j++)
                {
                    float v = (float)j / radialSegments;
                    float tubeAngle = v * Mathf.PI * 2f;

                    Vector3 normal = new Vector3(
                        Mathf.Cos(mainAngle) * Mathf.Cos(tubeAngle),
                        Mathf.Sin(tubeAngle),
                        Mathf.Sin(mainAngle) * Mathf.Cos(tubeAngle)
                    );

                    Vector3 vertex = ringCenter + normal * tubeRadius;

                    vertices.Add(vertex);
                    normals.Add(normal);
                    uvs.Add(new Vector2(u, v));
                }
            }

            // Generate triangles
            for (int i = 0; i < ringSegments; i++)
            {
                for (int j = 0; j < radialSegments; j++)
                {
                    int current = i * (radialSegments + 1) + j;
                    int next = current + radialSegments + 1;

                    triangles.Add(current);
                    triangles.Add(next + 1);
                    triangles.Add(current + 1);

                    triangles.Add(current);
                    triangles.Add(next);
                    triangles.Add(next + 1);
                }
            }

            ringMesh.vertices = vertices.ToArray();
            ringMesh.normals = normals.ToArray();
            ringMesh.uv = uvs.ToArray();
            ringMesh.triangles = triangles.ToArray();
            ringMesh.RecalculateBounds();

            if (meshFilter != null)
            {
                meshFilter.mesh = ringMesh;
            }
        }

        #endregion

        #region Positioning

        /// <summary>
        /// Positions the ring assembly at the correct height above the circuit.
        /// </summary>
        private void PositionAboveSurface()
        {
            transform.localPosition = surfaceNormal * heightAboveSurface;
        }

        #endregion

        #region Tunnel Connection

        /// <summary>
        /// Establishes a tunnel connection to another world.
        /// </summary>
        public void ConnectToWorld(Vector3 worldPosition, TunnelType type, float strength = 1f)
        {
            targetWorldPosition = worldPosition;
            tunnelType = type;
            connectionStrength = Mathf.Clamp01(strength);
            isConnected = true;

            // Start alignment process
            if (autoAlign)
            {
                StartAlignment();
            }

            // Update visuals for connection
            UpdateConnectionVisuals();

            // Start particle effects
            if (connectionParticles != null)
            {
                connectionParticles.Play();
            }
        }

        /// <summary>
        /// Disconnects the current tunnel connection.
        /// </summary>
        public void Disconnect()
        {
            isConnected = false;
            connectionStrength = 0f;

            // Stop effects
            if (connectionParticles != null)
            {
                connectionParticles.Stop();
            }

            // Reset material
            if (ringMaterial != null)
            {
                meshRenderer.material = ringMaterial;
            }

            UpdateVisuals();
        }

        /// <summary>
        /// Sets multiple potential target worlds for visualization.
        /// </summary>
        public void SetPotentialTargets(List<Vector3> targets)
        {
            potentialTargets = new List<Vector3>(targets);
        }

        #endregion

        #region Rotation and Alignment

        /// <summary>
        /// Starts the ring alignment process toward the target world.
        /// </summary>
        private void StartAlignment()
        {
            if (!isConnected) return;

            Vector3 directionToTarget = (targetWorldPosition - transform.position).normalized;

            // Calculate rotation to face the target
            // The ring's "forward" direction points toward the connected world
            targetRotation = Quaternion.LookRotation(directionToTarget, surfaceNormal);

            isAligning = true;
            alignmentProgress = 0f;
        }

        /// <summary>
        /// Updates ring rotation and alignment.
        /// </summary>
        void Update()
        {
            // Handle alignment animation
            if (isAligning && alignmentProgress < 1f)
            {
                alignmentProgress += Time.deltaTime / alignmentDuration;
                float smoothProgress = rotationCurve.Evaluate(alignmentProgress);

                currentRotation = Quaternion.Slerp(currentRotation, targetRotation, smoothProgress);
                transform.rotation = currentRotation;

                if (alignmentProgress >= 1f)
                {
                    isAligning = false;
                    OnAlignmentComplete();
                }
            }
            // Idle rotation when not connected
            else if (!isConnected && enableRotation)
            {
                transform.Rotate(surfaceNormal, idleRotationSpeed * Time.deltaTime);
            }
            // Maintain alignment when connected
            else if (isConnected && autoAlign && !isAligning)
            {
                // Continuously adjust to track moving targets
                Vector3 directionToTarget = (targetWorldPosition - transform.position).normalized;
                Quaternion desiredRotation = Quaternion.LookRotation(directionToTarget, surfaceNormal);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Time.deltaTime * 2f);
            }

            // Update pulse effect
            if (pulseWhenConnected && isConnected)
            {
                UpdatePulseEffect();
            }

            // Update visual effects
            UpdateEffects();
        }

        /// <summary>
        /// Called when ring alignment is complete.
        /// </summary>
        private void OnAlignmentComplete()
        {
            // Could trigger additional effects or notifications
            if (energyBeam != null)
            {
                energyBeam.enabled = true;
            }
        }

        #endregion

        #region Visual Updates

        /// <summary>
        /// Updates the ring's visual appearance.
        /// </summary>
        private void UpdateVisuals()
        {
            if (meshRenderer == null) return;

            // Determine color based on tunnel type
            currentColor = GetColorForTunnelType(tunnelType);

            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", currentColor);
            propertyBlock.SetColor("_EmissionColor", currentColor * connectionStrength * 2f);
            propertyBlock.SetFloat("_Metallic", 0.8f);
            propertyBlock.SetFloat("_Smoothness", 0.9f);
            meshRenderer.SetPropertyBlock(propertyBlock);

            // Update light
            if (ringLight != null)
            {
                ringLight.color = currentColor;
                ringLight.intensity = connectionStrength * lightIntensityMultiplier;
            }
        }

        /// <summary>
        /// Updates visuals specifically for connection state.
        /// </summary>
        private void UpdateConnectionVisuals()
        {
            // Switch to connected material if available
            if (connectedRingMaterial != null && isConnected)
            {
                meshRenderer.material = connectedRingMaterial;
            }

            UpdateVisuals();

            // Configure particle system colors
            if (connectionParticles != null)
            {
                var main = connectionParticles.main;
                main.startColor = currentColor;
            }
        }

        /// <summary>
        /// Updates the pulsing effect when connected.
        /// </summary>
        private void UpdatePulseEffect()
        {
            pulseTimer += Time.deltaTime * pulseFrequency;
            float pulseValue = Mathf.Sin(pulseTimer * Mathf.PI * 2f) * pulseAmplitude;

            // Apply pulse to scale
            float scale = 1f + (pulseValue * connectionStrength);
            transform.localScale = Vector3.one * scale;

            // Apply pulse to emission
            meshRenderer.GetPropertyBlock(propertyBlock);
            Color emissionColor = currentColor * connectionStrength * (2f + pulseValue * 2f);
            propertyBlock.SetColor("_EmissionColor", emissionColor);
            meshRenderer.SetPropertyBlock(propertyBlock);

            // Pulse the light
            if (ringLight != null)
            {
                ringLight.intensity = connectionStrength * lightIntensityMultiplier * (1f + pulseValue);
            }
        }

        /// <summary>
        /// Updates particle and beam effects.
        /// </summary>
        private void UpdateEffects()
        {
            // Update energy beam if present
            if (energyBeam != null && isConnected)
            {
                energyBeam.SetPosition(0, transform.position);

                // Set intermediate control point for curve
                Vector3 midPoint = (transform.position + targetWorldPosition) * 0.5f;
                midPoint += Vector3.up * Vector3.Distance(transform.position, targetWorldPosition) * 0.1f;

                if (energyBeam.positionCount > 2)
                {
                    energyBeam.SetPosition(1, midPoint);
                    energyBeam.SetPosition(2, targetWorldPosition);
                }
                else
                {
                    energyBeam.SetPosition(1, targetWorldPosition);
                }

                // Update beam color
                energyBeam.startColor = currentColor;
                energyBeam.endColor = currentColor * 0.5f;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets the color associated with a tunnel type.
        /// </summary>
        private Color GetColorForTunnelType(TunnelType type)
        {
            switch (type)
            {
                case TunnelType.Blue: return blueConnection;
                case TunnelType.Red: return redConnection;
                case TunnelType.Green: return greenConnection;
                case TunnelType.Yellow: return yellowConnection;
                case TunnelType.Cyan: return cyanConnection;
                case TunnelType.Magenta: return magentaConnection;
                default: return Color.white;
            }
        }

        /// <summary>
        /// Responds to circuit reaching full charge.
        /// </summary>
        private void OnCircuitFullyCharged()
        {
            // Could trigger automatic connection attempts
            connectionStrength = 1f;
            UpdateVisuals();
        }

        /// <summary>
        /// Responds to circuit charge depletion.
        /// </summary>
        private void OnCircuitChargeDepleted()
        {
            if (isConnected)
            {
                connectionStrength = 0f;
                UpdateVisuals();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the current connection strength (0-1).
        /// </summary>
        public float GetConnectionStrength()
        {
            return connectionStrength;
        }

        /// <summary>
        /// Gets the current tunnel type.
        /// </summary>
        public TunnelType GetTunnelType()
        {
            return tunnelType;
        }

        /// <summary>
        /// Checks if the ring is currently connected.
        /// </summary>
        public bool IsConnected()
        {
            return isConnected;
        }

        /// <summary>
        /// Gets the world position this ring is connected to.
        /// </summary>
        public Vector3 GetTargetWorldPosition()
        {
            return targetWorldPosition;
        }

        /// <summary>
        /// Forces immediate alignment to target.
        /// </summary>
        public void ForceAlignment()
        {
            if (!isConnected) return;

            Vector3 directionToTarget = (targetWorldPosition - transform.position).normalized;
            transform.rotation = Quaternion.LookRotation(directionToTarget, surfaceNormal);
            isAligning = false;
            alignmentProgress = 1f;
        }

        #endregion

        #region Debug

        void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            // Draw ring radius
            Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.5f);
            DrawCircleGizmo(transform.position, surfaceNormal, ringRadius);

            // Draw connection line
            if (isConnected && showTargetDirection)
            {
                Gizmos.color = GetColorForTunnelType(tunnelType);
                Gizmos.DrawLine(transform.position, targetWorldPosition);

                // Draw target indicator
                Gizmos.DrawWireSphere(targetWorldPosition, 10f);
            }

            // Draw potential targets
            if (potentialTargets.Count > 0)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                foreach (var target in potentialTargets)
                {
                    Gizmos.DrawLine(transform.position, target);
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;

            // Draw detailed ring visualization
            Gizmos.color = Color.cyan;
            DrawCircleGizmo(transform.position, surfaceNormal, ringRadius);

            // Draw ring forward direction
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * ringRadius * 2f);

            // Draw height above surface
            if (transform.parent != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.parent.position, transform.position);
            }
        }

        /// <summary>
        /// Helper to draw a circle gizmo.
        /// </summary>
        private void DrawCircleGizmo(Vector3 center, Vector3 normal, float radius)
        {
            Vector3 tangent = Vector3.Cross(normal, Vector3.forward);
            if (tangent.magnitude < 0.001f)
                tangent = Vector3.Cross(normal, Vector3.right);
            tangent.Normalize();

            Vector3 bitangent = Vector3.Cross(normal, tangent);

            int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = (float)i / segments * Mathf.PI * 2f;
                float angle2 = (float)(i + 1) / segments * Mathf.PI * 2f;

                Vector3 p1 = center + (tangent * Mathf.Cos(angle1) + bitangent * Mathf.Sin(angle1)) * radius;
                Vector3 p2 = center + (tangent * Mathf.Cos(angle2) + bitangent * Mathf.Sin(angle2)) * radius;

                Gizmos.DrawLine(p1, p2);
            }
        }

        #endregion
    }

    /// <summary>
    /// Types of quantum tunnels that can form between worlds.
    /// </summary>
    public enum TunnelType
    {
        Blue,
        Red,
        Green,
        Yellow,
        Cyan,
        Magenta
    }
}