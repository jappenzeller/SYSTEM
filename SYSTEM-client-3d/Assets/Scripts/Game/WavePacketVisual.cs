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
    public class WavePacketVisual : MonoBehaviour
    {
        [Header("Wave Packet Visualization")]
        private WavePacketRenderer wavePacketRenderer;
        private WavePacketSample[] currentComposition;

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

        void Awake()
        {
            // Initialization happens via Initialize() call from manager
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
                SystemDebug.Log(SystemDebug.Category.WavePacketSystem, $"Initialize: Source {sourceId}, composition: {currentComposition.Length} frequencies, {totalCount} total packets");
            }
            else
            {
                currentComposition = CreateDefaultComposition(color);
                SystemDebug.Log(SystemDebug.Category.WavePacketSystem, $"Initialize: Source {sourceId}, using default composition (no composition provided)");
            }

            // Create wave packet renderer
            if (settings == null)
            {
                SystemDebug.LogError(SystemDebug.Category.WavePacketSystem, $"Initialize called with null settings for source {sourceId}! Cannot create renderer.");
                return;
            }

            Stopwatch createTimer = Stopwatch.StartNew();

            GameObject rendererObj = new GameObject("WavePacketRenderer");
            rendererObj.transform.SetParent(transform);
            rendererObj.transform.localPosition = Vector3.zero;
            rendererObj.transform.localRotation = Quaternion.identity;
            rendererObj.transform.localScale = Vector3.one;

            wavePacketRenderer = rendererObj.AddComponent<WavePacketRenderer>();

            createTimer.Stop();
            Stopwatch settingsTimer = Stopwatch.StartNew();

            wavePacketRenderer.Initialize(settings);

            settingsTimer.Stop();
            initTimer.Stop();
            SystemDebug.Log(SystemDebug.Category.WavePacketSystem, $"Initialize: {initTimer.ElapsedMilliseconds}ms | Create: {createTimer.ElapsedMilliseconds}ms | Settings: {settingsTimer.ElapsedMilliseconds}ms");

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
            UpdateEffects();
        }

        public void UpdateComposition(List<WavePacketSample> composition)
        {
            if (composition != null && composition.Count > 0)
            {
                currentComposition = composition.ToArray();

                // Update the mesh
                if (wavePacketRenderer != null)
                {
                    wavePacketRenderer.SetComposition(currentComposition);
                    SystemDebug.Log(SystemDebug.Category.WavePacketSystem, $"UpdateComposition: Regenerating mesh with {currentComposition.Length} samples");
                }
            }
        }

        /// <summary>
        /// Set the alpha transparency of the source visual.
        /// Used for state-based visual feedback (moving sources are more transparent).
        /// </summary>
        public void SetAlpha(float alpha)
        {
            if (wavePacketRenderer != null)
            {
                wavePacketRenderer.SetAlpha(alpha);
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
            if (wavePacketRenderer != null && currentComposition != null && currentComposition.Length > 0)
            {
                wavePacketRenderer.SetComposition(currentComposition);
                SystemDebug.Log(SystemDebug.Category.WavePacketSystem, $"UpdateVisuals: Calling SetComposition with {currentComposition.Length} samples");
            }

            UpdateUI();
            UpdateEffects();
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
            // Placeholder for future visual effects (light, particles, etc.)
        }

        void OnDrawGizmos()
        {
            // Draw mining range wireframe
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, 30f);
        }
    }
}
