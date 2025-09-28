using UnityEngine;
using TMPro;

namespace SYSTEM.Game
{
    /// <summary>
    /// Visual representation of a WavePacketOrb
    /// Attach this to the orb prefab
    /// </summary>
    public class WavePacketOrbVisual : MonoBehaviour
    {
        [Header("Visual Components")]
        [SerializeField] private Renderer orbRenderer;
        [SerializeField] private ParticleSystem particleEffect;
        [SerializeField] private Light orbLight;

        [Header("UI Components")]
        [SerializeField] private TextMeshPro packetCountText;
        [SerializeField] private TextMeshPro minerCountText;
        [SerializeField] private GameObject infoPanel;

        [Header("Animation")]
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseAmount = 0.1f;
        [SerializeField] private float rotationSpeed = 20f;

        // State
        private ulong orbId;
        private uint totalPackets;
        private uint activeMinerCount;
        private Color baseColor;
        private Vector3 baseScale;
        private Material orbMaterial;

        void Awake()
        {
            // Cache references
            if (orbRenderer == null)
                orbRenderer = GetComponentInChildren<Renderer>();

            if (orbRenderer != null)
            {
                // Create instance material so each orb can have unique color
                orbMaterial = new Material(orbRenderer.sharedMaterial);
                orbRenderer.material = orbMaterial;
            }

            baseScale = transform.localScale;
        }

        void Update()
        {
            // Gentle pulsing animation
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = baseScale * (1f + pulse);

            // Slow rotation
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

            // Make info panel face camera if it exists
            if (infoPanel != null && Camera.main != null)
            {
                infoPanel.transform.LookAt(Camera.main.transform);
                infoPanel.transform.Rotate(0, 180, 0); // Flip to face camera
            }
        }

        #region Public Methods

        public void Initialize(ulong orbId, Color color, uint packets, uint miners)
        {
            this.orbId = orbId;
            this.baseColor = color;
            this.totalPackets = packets;
            this.activeMinerCount = miners;

            gameObject.name = $"Orb_{orbId}";

            UpdateVisuals();
        }

        public void UpdatePacketCount(uint packets)
        {
            totalPackets = packets;
            UpdateVisuals();
        }

        public void UpdateMinerCount(uint miners)
        {
            activeMinerCount = miners;
            UpdateVisuals();
        }

        public void UpdateColor(Color color)
        {
            baseColor = color;
            UpdateVisuals();
        }

        #endregion

        #region Private Methods

        private void UpdateVisuals()
        {
            // Update material color
            if (orbMaterial != null)
            {
                orbMaterial.color = baseColor;

                // Add emission for glow effect
                orbMaterial.EnableKeyword("_EMISSION");
                orbMaterial.SetColor("_EmissionColor", baseColor * 0.5f);
            }

            // Update light color
            if (orbLight != null)
            {
                orbLight.color = baseColor;
                orbLight.intensity = Mathf.Lerp(1f, 3f, activeMinerCount / 5f); // Brighter with more miners
            }

            // Update particle color
            if (particleEffect != null)
            {
                var main = particleEffect.main;
                main.startColor = new ParticleSystem.MinMaxGradient(baseColor);
            }

            // Update packet count text
            if (packetCountText != null)
            {
                packetCountText.text = $"Packets: {totalPackets}";
                packetCountText.color = totalPackets > 0 ? Color.white : Color.gray;
            }

            // Update miner count text
            if (minerCountText != null)
            {
                if (activeMinerCount > 0)
                {
                    minerCountText.text = $"Miners: {activeMinerCount}";
                    minerCountText.color = Color.yellow;
                    minerCountText.gameObject.SetActive(true);
                }
                else
                {
                    minerCountText.gameObject.SetActive(false);
                }
            }

            // Visual feedback when being mined
            if (particleEffect != null)
            {
                if (activeMinerCount > 0 && !particleEffect.isPlaying)
                    particleEffect.Play();
                else if (activeMinerCount == 0 && particleEffect.isPlaying)
                    particleEffect.Stop();
            }
        }

        #endregion

        #region Gizmos

        void OnDrawGizmos()
        {
            // Draw mining range sphere
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, 30f); // MAX_MINING_RANGE
        }

        #endregion
    }
}