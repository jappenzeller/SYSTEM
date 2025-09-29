using UnityEngine;
using System.Collections.Generic;
using SYSTEM.Circuits;

namespace SYSTEM.Circuits
{
    /// <summary>
    /// Manages the energy distribution sphere visualization that shows the circuit's coverage area.
    /// The sphere expands/contracts based on charge level and shows energy field effects.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class DistributionSphere : MonoBehaviour
    {
        [Header("Sphere Settings")]
        [SerializeField] private float baseRadius = CircuitConstants.DISTRIBUTION_SPHERE_RADIUS;
        [SerializeField] private float heightAboveSurface = CircuitConstants.DISTRIBUTION_SPHERE_HEIGHT;
        [SerializeField] private AnimationCurve radiusScaleCurve = AnimationCurve.Linear(0f, 0.5f, 1f, 1f);

        [Header("Visual Settings")]
        [SerializeField] private Material sphereMaterial;
        [SerializeField] private Color lowChargeColor = new Color(0.2f, 0.5f, 1f, 0.3f);
        [SerializeField] private Color highChargeColor = new Color(0.5f, 0.8f, 1f, 0.6f);
        [SerializeField] private float rimPower = 2f;
        [SerializeField] private float pulseFrequency = 1f;
        [SerializeField] private float pulseAmplitude = 0.1f;

        [Header("Particle Field")]
        [SerializeField] private GameObject particlePrefab;
        [SerializeField] private int particleCount = 20;
        [SerializeField] private float orbitRadius = 1.2f;
        [SerializeField] private float orbitSpeed = 30f;
        [SerializeField] private float particleSize = 0.5f;

        [Header("Overlap Detection")]
        [SerializeField] private bool detectOverlaps = true;
        [SerializeField] private LayerMask overlapLayer = -1;
        [SerializeField] private float overlapCheckRadius = 50f;

        [Header("Performance")]
        [SerializeField] private bool useLOD = true;
        [SerializeField] private float lodFullDistance = 100f;
        [SerializeField] private float lodSimplifiedDistance = 300f;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = false;
        [SerializeField] private bool showOverlapConnections = false;

        // Components
        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private SphereCollider triggerCollider;
        private MaterialPropertyBlock propertyBlock;
        private CircuitBase circuitBase;

        // Particle field
        private List<GameObject> orbitingParticles = new List<GameObject>();
        private List<float> particleAngles = new List<float>();
        private List<float> particleHeights = new List<float>();
        private List<float> particleSpeeds = new List<float>();

        // State
        private float currentChargeLevel = 0f;
        private float targetRadius = 0f;
        private float currentRadius = 0f;
        private float pulseTimer = 0f;
        private Vector3 surfaceNormal = Vector3.up;
        private Transform cameraTransform;

        // Overlapping circuits
        private List<DistributionSphere> overlappingSpheres = new List<DistributionSphere>();

        #region Initialization

        void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            meshFilter = GetComponent<MeshFilter>();
            propertyBlock = new MaterialPropertyBlock();

            // Get or add trigger collider for overlap detection
            triggerCollider = GetComponent<SphereCollider>();
            if (triggerCollider == null)
            {
                triggerCollider = gameObject.AddComponent<SphereCollider>();
            }
            triggerCollider.isTrigger = true;
            triggerCollider.radius = baseRadius;

            // Get circuit base reference
            circuitBase = GetComponentInParent<CircuitBase>();

            // Create or assign sphere mesh
            if (meshFilter.sharedMesh == null)
            {
                meshFilter.sharedMesh = CreateSphereMesh(2); // LOD level 2 for distribution sphere
            }

            // Apply material
            if (sphereMaterial != null)
            {
                meshRenderer.material = sphereMaterial;
            }

            // Initialize particle field
            CreateOrbitingParticles();
        }

        void Start()
        {
            // Find camera for LOD
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            // Subscribe to circuit charge events
            if (circuitBase != null)
            {
                circuitBase.OnChargeChanged += OnCircuitChargeChanged;
                surfaceNormal = transform.parent.up; // Get normal from parent circuit
            }

            // Position sphere above circuit
            PositionAboveSurface();

            // Set initial state
            UpdateVisuals(0f);
        }

        void OnDestroy()
        {
            // Unsubscribe from events
            if (circuitBase != null)
            {
                circuitBase.OnChargeChanged -= OnCircuitChargeChanged;
            }

            // Clean up particles
            foreach (var particle in orbitingParticles)
            {
                if (particle != null)
                {
                    Destroy(particle);
                }
            }
        }

        #endregion

        #region Positioning

        /// <summary>
        /// Positions the distribution sphere at the correct height above the circuit base.
        /// </summary>
        private void PositionAboveSurface()
        {
            if (transform.parent != null)
            {
                // Position relative to parent circuit
                transform.localPosition = surfaceNormal * heightAboveSurface;
            }
        }

        #endregion

        #region Charge Response

        /// <summary>
        /// Responds to charge level changes from the circuit base.
        /// </summary>
        private void OnCircuitChargeChanged(float chargePercent)
        {
            SetChargeLevel(chargePercent);
        }

        /// <summary>
        /// Sets the charge level which affects the sphere's size and appearance.
        /// </summary>
        public void SetChargeLevel(float chargeLevel)
        {
            currentChargeLevel = Mathf.Clamp01(chargeLevel);
            targetRadius = baseRadius * radiusScaleCurve.Evaluate(currentChargeLevel);
            UpdateVisuals(currentChargeLevel);
        }

        #endregion

        #region Visual Updates

        void Update()
        {
            // Smooth radius changes
            if (Mathf.Abs(currentRadius - targetRadius) > 0.001f)
            {
                currentRadius = Mathf.Lerp(currentRadius, targetRadius, Time.deltaTime * 5f);
                transform.localScale = Vector3.one * currentRadius;

                // Update collider
                if (triggerCollider != null)
                {
                    triggerCollider.radius = currentRadius;
                }
            }

            // Update pulse animation
            UpdatePulseEffect();

            // Update orbiting particles
            UpdateOrbitingParticles();

            // Handle LOD
            if (useLOD)
            {
                UpdateLOD();
            }

            // Check for overlaps periodically
            if (detectOverlaps && Time.frameCount % 60 == 0) // Check every second at 60fps
            {
                CheckForOverlaps();
            }
        }

        /// <summary>
        /// Updates the visual properties based on charge level.
        /// </summary>
        private void UpdateVisuals(float chargePercent)
        {
            if (meshRenderer == null) return;

            meshRenderer.GetPropertyBlock(propertyBlock);

            // Update colors
            Color currentColor = Color.Lerp(lowChargeColor, highChargeColor, chargePercent);
            propertyBlock.SetColor("_BaseColor", currentColor);
            propertyBlock.SetColor("_RimColor", currentColor * 1.5f);
            propertyBlock.SetFloat("_RimPower", rimPower);

            // Update transparency based on charge
            propertyBlock.SetFloat("_Alpha", currentColor.a);

            // Energy field intensity
            propertyBlock.SetFloat("_FieldIntensity", chargePercent);

            meshRenderer.SetPropertyBlock(propertyBlock);

            // Update particle visibility
            UpdateParticleVisibility(chargePercent);
        }

        /// <summary>
        /// Updates the pulsing effect of the sphere.
        /// </summary>
        private void UpdatePulseEffect()
        {
            pulseTimer += Time.deltaTime * pulseFrequency;
            float pulseValue = Mathf.Sin(pulseTimer * Mathf.PI * 2f) * pulseAmplitude;

            // Apply pulse to scale
            float pulseScale = 1f + (pulseValue * currentChargeLevel);
            transform.localScale = Vector3.one * (currentRadius * pulseScale);

            // Apply pulse to emission
            if (meshRenderer != null)
            {
                meshRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat("_PulseIntensity", 1f + pulseValue);
                meshRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        #endregion

        #region Particle Field

        /// <summary>
        /// Creates the orbiting energy particles around the sphere.
        /// </summary>
        private void CreateOrbitingParticles()
        {
            if (particlePrefab == null)
            {
                // Create default particle if no prefab assigned
                particlePrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                particlePrefab.transform.localScale = Vector3.one * particleSize;
                Destroy(particlePrefab.GetComponent<Collider>());
                particlePrefab.SetActive(false);
            }

            for (int i = 0; i < particleCount; i++)
            {
                GameObject particle = Instantiate(particlePrefab, transform);
                particle.SetActive(true);
                particle.name = $"OrbitParticle_{i}";

                // Set up particle renderer
                var renderer = particle.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.material = sphereMaterial;
                    renderer.material.SetFloat("_Mode", 3); // Transparent mode
                }

                orbitingParticles.Add(particle);

                // Random initial positions
                float angle = Random.Range(0f, 360f);
                float height = Random.Range(-0.5f, 0.5f);
                float speed = Random.Range(0.8f, 1.2f) * orbitSpeed;

                particleAngles.Add(angle);
                particleHeights.Add(height);
                particleSpeeds.Add(speed);
            }
        }

        /// <summary>
        /// Updates the position and movement of orbiting particles.
        /// </summary>
        private void UpdateOrbitingParticles()
        {
            for (int i = 0; i < orbitingParticles.Count; i++)
            {
                if (orbitingParticles[i] == null) continue;

                // Update angle
                particleAngles[i] += particleSpeeds[i] * Time.deltaTime;

                // Calculate position
                float radius = currentRadius * orbitRadius;
                float angle = particleAngles[i] * Mathf.Deg2Rad;
                float height = particleHeights[i] * currentRadius * 0.5f;

                Vector3 localPos = new Vector3(
                    Mathf.Cos(angle) * radius,
                    height,
                    Mathf.Sin(angle) * radius
                );

                orbitingParticles[i].transform.localPosition = localPos;

                // Scale particles based on charge
                float particleScale = particleSize * (0.5f + currentChargeLevel * 0.5f);
                orbitingParticles[i].transform.localScale = Vector3.one * particleScale;
            }
        }

        /// <summary>
        /// Updates particle visibility based on charge level.
        /// </summary>
        private void UpdateParticleVisibility(float chargePercent)
        {
            int visibleCount = Mathf.RoundToInt(particleCount * chargePercent);

            for (int i = 0; i < orbitingParticles.Count; i++)
            {
                if (orbitingParticles[i] != null)
                {
                    orbitingParticles[i].SetActive(i < visibleCount);
                }
            }
        }

        #endregion

        #region Overlap Detection

        /// <summary>
        /// Checks for overlapping distribution spheres from nearby circuits.
        /// </summary>
        private void CheckForOverlaps()
        {
            overlappingSpheres.Clear();

            Collider[] overlaps = Physics.OverlapSphere(
                transform.position,
                overlapCheckRadius,
                overlapLayer
            );

            foreach (var collider in overlaps)
            {
                if (collider.gameObject == gameObject) continue;

                DistributionSphere otherSphere = collider.GetComponent<DistributionSphere>();
                if (otherSphere != null && otherSphere.currentChargeLevel > 0.1f)
                {
                    float distance = Vector3.Distance(transform.position, otherSphere.transform.position);
                    float combinedRadius = currentRadius + otherSphere.currentRadius;

                    if (distance < combinedRadius)
                    {
                        overlappingSpheres.Add(otherSphere);
                    }
                }
            }

            // Visual feedback for overlaps
            if (overlappingSpheres.Count > 0)
            {
                OnOverlapDetected();
            }
        }

        /// <summary>
        /// Called when overlapping with another distribution sphere.
        /// </summary>
        private void OnOverlapDetected()
        {
            // Could trigger visual effects, efficiency bonuses, etc.
            if (meshRenderer != null)
            {
                meshRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat("_OverlapIntensity", overlappingSpheres.Count * 0.2f);
                meshRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (!detectOverlaps) return;

            DistributionSphere otherSphere = other.GetComponent<DistributionSphere>();
            if (otherSphere != null && !overlappingSpheres.Contains(otherSphere))
            {
                overlappingSpheres.Add(otherSphere);
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (!detectOverlaps) return;

            DistributionSphere otherSphere = other.GetComponent<DistributionSphere>();
            if (otherSphere != null)
            {
                overlappingSpheres.Remove(otherSphere);
            }
        }

        #endregion

        #region LOD System

        /// <summary>
        /// Updates level of detail based on camera distance.
        /// </summary>
        private void UpdateLOD()
        {
            if (cameraTransform == null) return;

            float distance = Vector3.Distance(transform.position, cameraTransform.position);

            if (distance > lodSimplifiedDistance)
            {
                // Hide particles, use simple shader
                SetLODLevel(2);
            }
            else if (distance > lodFullDistance)
            {
                // Reduced particles, medium quality
                SetLODLevel(1);
            }
            else
            {
                // Full quality
                SetLODLevel(0);
            }
        }

        /// <summary>
        /// Sets the LOD level for the distribution sphere.
        /// </summary>
        private void SetLODLevel(int level)
        {
            switch (level)
            {
                case 0: // Full detail
                    meshRenderer.enabled = true;
                    foreach (var particle in orbitingParticles)
                    {
                        if (particle != null) particle.SetActive(true);
                    }
                    break;

                case 1: // Simplified
                    meshRenderer.enabled = true;
                    // Show only half the particles
                    for (int i = 0; i < orbitingParticles.Count; i++)
                    {
                        if (orbitingParticles[i] != null)
                        {
                            orbitingParticles[i].SetActive(i % 2 == 0);
                        }
                    }
                    break;

                case 2: // Hidden or very simple
                    meshRenderer.enabled = true;
                    // Hide all particles
                    foreach (var particle in orbitingParticles)
                    {
                        if (particle != null) particle.SetActive(false);
                    }
                    break;
            }
        }

        #endregion

        #region Mesh Generation

        /// <summary>
        /// Creates a sphere mesh with specified subdivision level.
        /// </summary>
        private Mesh CreateSphereMesh(int subdivisions)
        {
            // For now, use Unity's built-in sphere
            // In production, this would generate an icosphere with proper subdivisions
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh sphereMesh = Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);
            Destroy(temp);
            return sphereMesh;
        }

        #endregion

        #region Debug

        void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            // Draw sphere radius
            Gizmos.color = new Color(0, 0.5f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, currentRadius > 0 ? currentRadius : baseRadius);

            // Draw overlap detection radius
            if (detectOverlaps)
            {
                Gizmos.color = new Color(1f, 1f, 0, 0.1f);
                Gizmos.DrawWireSphere(transform.position, overlapCheckRadius);
            }

            // Draw connections to overlapping spheres
            if (showOverlapConnections && overlappingSpheres != null)
            {
                Gizmos.color = new Color(0, 1f, 0, 0.5f);
                foreach (var otherSphere in overlappingSpheres)
                {
                    if (otherSphere != null)
                    {
                        Gizmos.DrawLine(transform.position, otherSphere.transform.position);
                    }
                }
            }

            // Draw charge level indicator
            Gizmos.color = Color.Lerp(Color.red, Color.green, currentChargeLevel);
            Vector3 barPos = transform.position + Vector3.up * (currentRadius + 5f);
            Gizmos.DrawWireCube(barPos, new Vector3(currentChargeLevel * 10f, 1f, 1f));
        }

        void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;

            // Draw detailed sphere visualization
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, baseRadius);

            // Draw height above surface
            if (transform.parent != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.parent.position, transform.position);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the current radius of the distribution sphere.
        /// </summary>
        public float GetCurrentRadius()
        {
            return currentRadius;
        }

        /// <summary>
        /// Checks if a point is within the distribution sphere.
        /// </summary>
        public bool ContainsPoint(Vector3 worldPoint)
        {
            float distance = Vector3.Distance(transform.position, worldPoint);
            return distance <= currentRadius;
        }

        /// <summary>
        /// Gets list of overlapping distribution spheres.
        /// </summary>
        public List<DistributionSphere> GetOverlappingSpheres()
        {
            return new List<DistributionSphere>(overlappingSpheres);
        }

        #endregion
    }
}