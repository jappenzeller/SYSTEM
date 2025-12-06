using UnityEngine;
using TMPro;
using SpacetimeDB.Types;
using SYSTEM.WavePacket;
using System.Collections.Generic;
using System.Diagnostics;

namespace SYSTEM.Game
{
    /// <summary>
    /// Renders a wave packet source using the parameterized mesh renderer.
    /// Handles composition updates, visual effects, and UI display for stationary sources.
    /// </summary>
    public class WavePacketSourceRenderer : MonoBehaviour
    {
        [Header("Wave Packet Visualization")]
        [SerializeField] private bool useWavePacketRenderer = true;
        private WavePacketRenderer wavePacketRenderer;
        private WavePacketSample[] currentComposition;

        [Header("Fallback Visual Components")]
        [SerializeField] private Renderer sourceRenderer;
        [SerializeField] private ParticleSystem particleEffect;
        [SerializeField] private Light sourceLight;

        [Header("UI Components")]
        [SerializeField] private TextMeshPro packetCountText;
        [SerializeField] private TextMeshPro minerCountText;
        [SerializeField] private GameObject infoPanel;

        [Header("Animation")]
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseAmount = 0.05f;
        [SerializeField] private float rotationSpeed = 0f;

        private ulong sourceId;
        private uint totalPackets;
        private uint activeMinerCount;
        private Color baseColor;
        private Vector3 baseScale;
        private Material sourceMaterial;

        void Awake()
        {
            // Initialization happens via Initialize() call from manager
            // Don't create renderer here - wait for explicit initialization with settings

            if (!useWavePacketRenderer)
            {
                // Fallback mode setup
                if (sourceRenderer == null)
                    sourceRenderer = GetComponentInChildren<Renderer>();

                if (sourceRenderer != null)
                {
                    sourceMaterial = new Material(sourceRenderer.sharedMaterial);
                    sourceRenderer.material = sourceMaterial;
                }
            }

            baseScale = transform.localScale;
        }

        void Update()
        {
            // Pulsing animation
            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = baseScale * (1f + pulse);
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

            // Billboard UI panel
            if (infoPanel != null && Camera.main != null)
            {
                infoPanel.transform.LookAt(Camera.main.transform);
                infoPanel.transform.Rotate(0, 180, 0);
            }
        }

        void OnDestroy()
        {
            // Cleanup handled automatically by Unity parent-child destruction
        }

        /// <summary>
        /// Initialize the source renderer with settings and source data.
        /// Called by WavePacketSourceManager after instantiation.
        /// </summary>
        public void Initialize(WavePacketSettings settings, ulong sourceId, Color color, uint packets, uint miners, List<WavePacketSample> composition = null)
        {
            Stopwatch initTimer = Stopwatch.StartNew();

            this.sourceId = sourceId;
            this.baseColor = color;
            this.totalPackets = packets;
            this.activeMinerCount = miners;

            gameObject.name = "WavePacketSource_" + sourceId;

            if (composition != null && composition.Count > 0)
            {
                currentComposition = composition.ToArray();
                uint totalCount = 0;
                foreach (var sample in currentComposition)
                    totalCount += sample.Count;
                UnityEngine.Debug.Log($"[WavePacketSourceRenderer] Initialize: Source {sourceId}, composition: {currentComposition.Length} frequencies, {totalCount} total packets");
            }
            else
            {
                currentComposition = CreateDefaultComposition(color);
                UnityEngine.Debug.Log($"[WavePacketSourceRenderer] Initialize: Source {sourceId}, using default composition (no composition provided)");
            }

            // Create wave packet renderer if enabled
            if (useWavePacketRenderer)
            {
                if (settings == null)
                {
                    UnityEngine.Debug.LogError($"[WavePacketSourceRenderer] Initialize called with null settings for source {sourceId}! Cannot create renderer.");
                    return;
                }

                Stopwatch createTimer = Stopwatch.StartNew();

                // Create wave packet renderer
                GameObject rendererObj = new GameObject("WavePacketRenderer");
                rendererObj.transform.SetParent(transform);
                rendererObj.transform.localPosition = Vector3.zero;
                rendererObj.transform.localRotation = Quaternion.identity;
                rendererObj.transform.localScale = Vector3.one;

                wavePacketRenderer = rendererObj.AddComponent<WavePacketRenderer>();

                createTimer.Stop();
                Stopwatch settingsTimer = Stopwatch.StartNew();

                // Initialize renderer with settings
                wavePacketRenderer.Initialize(settings);

                settingsTimer.Stop();

                // Disable fallback renderer if present
                if (sourceRenderer != null)
                    sourceRenderer.enabled = false;

                initTimer.Stop();
                UnityEngine.Debug.Log($"[WavePacketSourceRenderer] Initialize: {initTimer.ElapsedMilliseconds}ms | Create: {createTimer.ElapsedMilliseconds}ms | Settings: {settingsTimer.ElapsedMilliseconds}ms");
            }

            UpdateVisuals();
        }

        public void UpdatePacketCount(uint packets)
        {
            totalPackets = packets;
            // Only update UI, not mesh
            UpdateUI();
            UpdateEffects();
        }

        public void UpdateMinerCount(uint miners)
        {
            activeMinerCount = miners;
            // Only update UI and effects, not mesh
            UpdateUI();
            UpdateEffects();
        }

        public void UpdateColor(Color color)
        {
            baseColor = color;
            // Only update fallback material color, not mesh
            UpdateFallbackVisuals();
            UpdateEffects();
        }

        public void UpdateComposition(List<WavePacketSample> composition)
        {
            if (composition != null && composition.Count > 0)
            {
                currentComposition = composition.ToArray();

                // Update the mesh - this is the ONLY method that should regenerate mesh
                if (useWavePacketRenderer && wavePacketRenderer != null)
                {
                    wavePacketRenderer.SetComposition(currentComposition);
                    UnityEngine.Debug.Log($"[WavePacketSourceRenderer] UpdateComposition: Regenerating mesh with {currentComposition.Length} samples");
                }
                else
                {
                    UpdateFallbackVisuals();
                }
            }
        }

        /// <summary>
        /// Set the alpha transparency of the source visual.
        /// Used for state-based visual feedback (moving sources are more transparent).
        /// </summary>
        public void SetAlpha(float alpha)
        {
            if (useWavePacketRenderer && wavePacketRenderer != null)
            {
                wavePacketRenderer.SetAlpha(alpha);
            }
            else if (sourceMaterial != null)
            {
                Color c = sourceMaterial.color;
                c.a = alpha;
                sourceMaterial.color = c;

                // Also update emission alpha if using emission
                if (sourceMaterial.HasProperty("_EmissionColor"))
                {
                    Color emissionColor = sourceMaterial.GetColor("_EmissionColor");
                    emissionColor.a = alpha;
                    sourceMaterial.SetColor("_EmissionColor", emissionColor);
                }
            }
        }

        private WavePacketSample[] CreateDefaultComposition(Color color)
        {
            float frequency = 0.0f;

            // Map color to frequency
            if (color.r > 0.9f && color.g < 0.1f && color.b < 0.1f) frequency = 0.0f;       // Red
            else if (color.r > 0.9f && color.g > 0.9f && color.b < 0.1f) frequency = 1.047f; // Yellow
            else if (color.r < 0.1f && color.g > 0.9f && color.b < 0.1f) frequency = 2.094f; // Green
            else if (color.r < 0.1f && color.g > 0.9f && color.b > 0.9f) frequency = 3.142f; // Cyan
            else if (color.r < 0.1f && color.g < 0.1f && color.b > 0.9f) frequency = 4.189f; // Blue
            else if (color.r > 0.9f && color.g < 0.1f && color.b > 0.9f) frequency = 5.236f; // Magenta

            return new WavePacketSample[]
            {
                new WavePacketSample { Frequency = frequency, Amplitude = 1.0f, Phase = 0.0f, Count = 20 }
            };
        }

        private void UpdateVisuals()
        {
            if (useWavePacketRenderer && wavePacketRenderer != null)
            {
                if (currentComposition != null && currentComposition.Length > 0)
                {
                    wavePacketRenderer.SetComposition(currentComposition);
                    UnityEngine.Debug.Log($"[WavePacketSourceRenderer] UpdateVisuals: Calling SetComposition with {currentComposition.Length} samples");
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[WavePacketSourceRenderer] UpdateVisuals: Using fallback (useWavePacketRenderer={useWavePacketRenderer}, wavePacketRenderer={wavePacketRenderer != null})");
                UpdateFallbackVisuals();
            }

            UpdateUI();
            UpdateEffects();
        }

        private void UpdateFallbackVisuals()
        {
            if (sourceMaterial != null)
            {
                sourceMaterial.color = baseColor;
                sourceMaterial.EnableKeyword("_EMISSION");
                sourceMaterial.SetColor("_EmissionColor", baseColor * 0.5f);
            }
        }

        private void UpdateUI()
        {
            if (packetCountText != null)
            {
                packetCountText.text = "Packets: " + totalPackets;
                packetCountText.color = totalPackets > 0 ? Color.white : Color.gray;
            }

            if (minerCountText != null)
            {
                if (activeMinerCount > 0)
                {
                    minerCountText.text = "Miners: " + activeMinerCount;
                    minerCountText.color = Color.yellow;
                    minerCountText.gameObject.SetActive(true);
                }
                else
                {
                    minerCountText.gameObject.SetActive(false);
                }
            }
        }

        private void UpdateEffects()
        {
            if (sourceLight != null)
            {
                sourceLight.color = baseColor;
                sourceLight.intensity = Mathf.Lerp(1f, 3f, activeMinerCount / 5f);
            }

            if (particleEffect != null)
            {
                var main = particleEffect.main;
                main.startColor = new ParticleSystem.MinMaxGradient(baseColor);
            }

            if (particleEffect != null)
            {
                if (activeMinerCount > 0 && !particleEffect.isPlaying)
                    particleEffect.Play();
                else if (activeMinerCount == 0 && particleEffect.isPlaying)
                    particleEffect.Stop();
            }
        }

        void OnDrawGizmos()
        {
            // Draw mining range wireframe
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, 30f);
        }
    }
}
