using UnityEngine;
using System;
using SYSTEM.Circuits;
using SYSTEM.Debug;
using SpacetimeDB.Types;

namespace SYSTEM.Circuits
{
    /// <summary>
    /// Manages the ground-level circuit visualization on a world's surface.
    /// This component handles the base platform, charge visualization, and positioning
    /// of circuits based on WorldCoords and CardinalDirection.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class CircuitBase : MonoBehaviour
    {
        [Header("Circuit Identity")]
        [SerializeField] private ulong circuitId;
        [SerializeField] private WorldCoords worldCoords;
        [SerializeField] private CardinalDirection placementDirection = CardinalDirection.NorthPole;

        [Header("Visual Settings")]
        [SerializeField] private Material baseMaterial;
        [SerializeField] private Material chargedMaterial;
        [SerializeField] private AnimationCurve pulseIntensityCurve;
        [SerializeField] private float pulseSpeed = 1f;
        [SerializeField] private Color baseColor = Color.cyan;
        [SerializeField] private Color chargedColor = Color.white;

        [Header("Charge Settings")]
        [SerializeField] private float currentCharge = 0f;
        [SerializeField] private float maxCharge = 100f;
        [SerializeField] private float chargeRate = 10f; // Units per second

        [Header("Particle Effects")]
        [SerializeField] private ParticleSystem baseParticles;
        [SerializeField] private ParticleSystem chargeParticles;
        [SerializeField] private int maxParticles = 50;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip chargeSound;
        [SerializeField] private AudioClip pulseSound;

        [Header("World Reference")]
        [SerializeField] private float worldRadius = CircuitConstants.WORLD_RADIUS;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = false;

        // Components
        private MeshRenderer meshRenderer;
        private Light circuitLight;
        private MaterialPropertyBlock propertyBlock;

        // State
        private float pulseTimer = 0f;
        private bool isCharging = false;
        private Vector3 surfaceNormal;
        private Transform parentWorld;

        // Events
        public event Action<float> OnChargeChanged;
        public event Action OnFullyCharged;
        public event Action OnChargeDepleted;

        #region Initialization

        void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            propertyBlock = new MaterialPropertyBlock();

            // Try to find or create circuit light
            circuitLight = GetComponentInChildren<Light>();
            if (circuitLight == null)
            {
                GameObject lightObj = new GameObject("CircuitLight");
                lightObj.transform.SetParent(transform);
                lightObj.transform.localPosition = Vector3.up * 2f;
                circuitLight = lightObj.AddComponent<Light>();
                circuitLight.type = LightType.Point;
                circuitLight.range = CircuitConstants.CIRCUIT_BASE_RADIUS * 2f;
                circuitLight.intensity = 0.5f;
                circuitLight.color = baseColor;
            }

            SetupParticles();
        }

        void Start()
        {
            UpdateChargeVisuals();
        }

        /// <summary>
        /// Initialize the circuit base with its identity and position data.
        /// </summary>
        public void Initialize(ulong id, WorldCoords coords, CardinalDirection direction, Transform world)
        {
            circuitId = id;
            worldCoords = coords;
            placementDirection = direction;
            parentWorld = world;

            // Position circuit on world surface
            PositionOnWorldSurface();

            // Set initial visual state
            UpdateChargeVisuals();
        }

        #endregion

        #region Positioning

        /// <summary>
        /// Positions the circuit base on the world surface based on its CardinalDirection.
        /// </summary>
        private void PositionOnWorldSurface()
        {
            if (parentWorld == null)
            {
                SystemDebug.LogError(SystemDebug.Category.Network, $"[CircuitBase] No parent world assigned for circuit {circuitId}");
                return;
            }

            // Get the direction vector for this cardinal position
            Vector3 directionVector = GetDirectionVector(placementDirection);
            surfaceNormal = directionVector;

            // Position at world radius distance from center
            Vector3 worldCenter = parentWorld.position;
            Vector3 surfacePosition = worldCenter + (directionVector * worldRadius);

            transform.position = surfacePosition;

            // Orient the circuit so its UP points away from world center (surface normal)
            // Calculate a tangent direction for forward
            Vector3 tangent = Vector3.Cross(surfaceNormal, Vector3.right);
            if (tangent.sqrMagnitude < 0.001f)
            {
                // Surface normal is parallel to right, use forward instead
                tangent = Vector3.Cross(surfaceNormal, Vector3.forward);
            }
            tangent.Normalize();

            transform.rotation = Quaternion.LookRotation(tangent, surfaceNormal);

            // Apply small offset to prevent z-fighting
            transform.position += surfaceNormal * 0.1f;
        }

        /// <summary>
        /// Converts CardinalDirection enum to a normalized direction vector.
        /// </summary>
        private Vector3 GetDirectionVector(CardinalDirection direction)
        {
            switch (direction)
            {
                case CardinalDirection.NorthPole: return Vector3.up;
                case CardinalDirection.SouthPole: return Vector3.down;
                case CardinalDirection.East: return Vector3.right;
                case CardinalDirection.West: return Vector3.left;
                case CardinalDirection.Front: return Vector3.forward;
                case CardinalDirection.Back: return Vector3.back;

                // Edge directions (combinations of two faces)
                case CardinalDirection.NorthEast: return (Vector3.up + Vector3.right).normalized;
                case CardinalDirection.NorthWest: return (Vector3.up + Vector3.left).normalized;
                case CardinalDirection.NorthFront: return (Vector3.up + Vector3.forward).normalized;
                case CardinalDirection.NorthBack: return (Vector3.up + Vector3.back).normalized;
                case CardinalDirection.SouthEast: return (Vector3.down + Vector3.right).normalized;
                case CardinalDirection.SouthWest: return (Vector3.down + Vector3.left).normalized;
                case CardinalDirection.SouthFront: return (Vector3.down + Vector3.forward).normalized;
                case CardinalDirection.SouthBack: return (Vector3.down + Vector3.back).normalized;
                case CardinalDirection.EastFront: return (Vector3.right + Vector3.forward).normalized;
                case CardinalDirection.EastBack: return (Vector3.right + Vector3.back).normalized;
                case CardinalDirection.WestFront: return (Vector3.left + Vector3.forward).normalized;
                case CardinalDirection.WestBack: return (Vector3.left + Vector3.back).normalized;

                // Vertex directions (combinations of three faces)
                case CardinalDirection.NorthEastFront: return (Vector3.up + Vector3.right + Vector3.forward).normalized;
                case CardinalDirection.NorthEastBack: return (Vector3.up + Vector3.right + Vector3.back).normalized;
                case CardinalDirection.NorthWestFront: return (Vector3.up + Vector3.left + Vector3.forward).normalized;
                case CardinalDirection.NorthWestBack: return (Vector3.up + Vector3.left + Vector3.back).normalized;
                case CardinalDirection.SouthEastFront: return (Vector3.down + Vector3.right + Vector3.forward).normalized;
                case CardinalDirection.SouthEastBack: return (Vector3.down + Vector3.right + Vector3.back).normalized;
                case CardinalDirection.SouthWestFront: return (Vector3.down + Vector3.left + Vector3.forward).normalized;
                case CardinalDirection.SouthWestBack: return (Vector3.down + Vector3.left + Vector3.back).normalized;

                default: return Vector3.up;
            }
        }

        #endregion

        #region Charge Management

        void Update()
        {
            // Handle charging
            if (isCharging && currentCharge < maxCharge)
            {
                float deltaCharge = chargeRate * Time.deltaTime;
                SetCharge(currentCharge + deltaCharge);
            }

            // Update pulse animation
            UpdatePulseAnimation();
        }

        /// <summary>
        /// Sets the current charge level of the circuit.
        /// </summary>
        public void SetCharge(float charge)
        {
            float previousCharge = currentCharge;
            currentCharge = Mathf.Clamp(charge, 0f, maxCharge);

            if (Mathf.Abs(previousCharge - currentCharge) > 0.01f)
            {
                UpdateChargeVisuals();
                OnChargeChanged?.Invoke(GetChargePercentage());

                // Check for full charge
                if (previousCharge < maxCharge && currentCharge >= maxCharge)
                {
                    OnFullyCharged?.Invoke();
                    PlaySound(pulseSound);
                }

                // Check for depletion
                if (previousCharge > 0 && currentCharge <= 0)
                {
                    OnChargeDepleted?.Invoke();
                }
            }
        }

        /// <summary>
        /// Gets the current charge as a percentage (0-1).
        /// </summary>
        public float GetChargePercentage()
        {
            return maxCharge > 0 ? currentCharge / maxCharge : 0f;
        }

        /// <summary>
        /// Start or stop charging the circuit.
        /// </summary>
        public void SetCharging(bool charging)
        {
            if (isCharging != charging)
            {
                isCharging = charging;
                if (charging)
                {
                    PlaySound(chargeSound);
                }
            }
        }

        #endregion

        #region Visual Updates

        /// <summary>
        /// Updates all visual elements based on current charge level.
        /// </summary>
        private void UpdateChargeVisuals()
        {
            float chargePercent = GetChargePercentage();

            // Update material properties
            if (meshRenderer != null)
            {
                meshRenderer.GetPropertyBlock(propertyBlock);

                // Lerp between base and charged colors
                Color currentColor = Color.Lerp(baseColor, chargedColor, chargePercent);
                propertyBlock.SetColor("_BaseColor", currentColor);
                propertyBlock.SetColor("_EmissionColor", currentColor * chargePercent * 2f);
                propertyBlock.SetFloat("_ChargeLevel", chargePercent);

                meshRenderer.SetPropertyBlock(propertyBlock);

                // Switch materials at high charge
                if (chargePercent > 0.8f && chargedMaterial != null)
                {
                    meshRenderer.material = chargedMaterial;
                }
                else if (baseMaterial != null)
                {
                    meshRenderer.material = baseMaterial;
                }
            }

            // Update light intensity and color
            if (circuitLight != null)
            {
                circuitLight.intensity = Mathf.Lerp(0.5f, 2f, chargePercent);
                circuitLight.color = Color.Lerp(baseColor, chargedColor, chargePercent);
                circuitLight.range = CircuitConstants.CIRCUIT_BASE_RADIUS * (1f + chargePercent);
            }

            // Update particle emission
            UpdateParticles(chargePercent);
        }

        /// <summary>
        /// Updates the pulse animation effect.
        /// </summary>
        private void UpdatePulseAnimation()
        {
            pulseTimer += Time.deltaTime * pulseSpeed;
            float pulseValue = pulseIntensityCurve.Evaluate(Mathf.Repeat(pulseTimer, 1f));

            // Apply pulse to emission
            if (meshRenderer != null)
            {
                meshRenderer.GetPropertyBlock(propertyBlock);
                float chargePercent = GetChargePercentage();
                Color emissionColor = Color.Lerp(baseColor, chargedColor, chargePercent);
                propertyBlock.SetColor("_EmissionColor", emissionColor * pulseValue * (1f + chargePercent));
                meshRenderer.SetPropertyBlock(propertyBlock);
            }

            // Pulse the light
            if (circuitLight != null && currentCharge > 0)
            {
                circuitLight.intensity = Mathf.Lerp(0.5f, 2f, GetChargePercentage()) * (0.8f + pulseValue * 0.4f);
            }
        }

        #endregion

        #region Particle Effects

        private void SetupParticles()
        {
            // Setup base particles if assigned
            if (baseParticles != null)
            {
                var main = baseParticles.main;
                main.startColor = baseColor;
                main.maxParticles = maxParticles / 2;

                var shape = baseParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = CircuitConstants.CIRCUIT_BASE_RADIUS;
            }

            // Setup charge particles if assigned
            if (chargeParticles != null)
            {
                var main = chargeParticles.main;
                main.startColor = chargedColor;
                main.maxParticles = maxParticles;

                var shape = chargeParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = CircuitConstants.CIRCUIT_BASE_RADIUS * 0.8f;
            }
        }

        private void UpdateParticles(float chargePercent)
        {
            // Base particles always emit at low rate
            if (baseParticles != null)
            {
                var emission = baseParticles.emission;
                emission.rateOverTime = 5f + chargePercent * 10f;
            }

            // Charge particles scale with charge level
            if (chargeParticles != null)
            {
                var emission = chargeParticles.emission;
                emission.rateOverTime = chargePercent * 30f;

                // Burst when fully charged
                if (chargePercent >= 1f)
                {
                    chargeParticles.Emit(20);
                }
            }
        }

        #endregion

        #region Audio

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        #endregion

        #region Debug

        void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            // Draw circuit base radius
            Gizmos.color = new Color(0, 1, 1, 0.5f);
            Gizmos.DrawWireSphere(transform.position, CircuitConstants.CIRCUIT_BASE_RADIUS);

            // Draw surface normal
            if (Application.isPlaying)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, transform.position + surfaceNormal * 10f);
            }

            // Draw charge level
            Gizmos.color = Color.Lerp(Color.red, Color.green, GetChargePercentage());
            Gizmos.DrawWireCube(
                transform.position + Vector3.up * 5f,
                new Vector3(GetChargePercentage() * 10f, 1f, 1f)
            );
        }

        #endregion
    }

    /// <summary>
    /// Defines the cardinal directions for circuit placement on a sphere.
    /// </summary>
    public enum CardinalDirection
    {
        // Primary (6 face positions)
        NorthPole,
        SouthPole,
        East,
        West,
        Front,
        Back,

        // Secondary (12 edge positions)
        NorthEast,
        NorthWest,
        NorthFront,
        NorthBack,
        SouthEast,
        SouthWest,
        SouthFront,
        SouthBack,
        EastFront,
        EastBack,
        WestFront,
        WestBack,

        // Tertiary (8 vertex positions)
        NorthEastFront,
        NorthEastBack,
        NorthWestFront,
        NorthWestBack,
        SouthEastFront,
        SouthEastBack,
        SouthWestFront,
        SouthWestBack
    }
}