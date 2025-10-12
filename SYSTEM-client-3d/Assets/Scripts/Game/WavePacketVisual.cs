using UnityEngine;
using TMPro;
using SpacetimeDB.Types;
using SYSTEM.WavePacket;
using System.Collections.Generic;
using System.Diagnostics;

namespace SYSTEM.Game
{
    public class WavePacketVisual : MonoBehaviour
    {
        [Header("Wave Packet Visualization")]
        [SerializeField] private bool useWavePacketDisplay = true;
        [SerializeField] private WavePacketSettings wavePacketSettings;
        private WavePacketDisplay wavePacketDisplay;
        private WavePacketSample[] currentComposition;

        [Header("Fallback Visual Components")]
        [SerializeField] private Renderer orbRenderer;
        [SerializeField] private ParticleSystem particleEffect;
        [SerializeField] private Light orbLight;

        [Header("UI Components")]
        [SerializeField] private TextMeshPro packetCountText;
        [SerializeField] private TextMeshPro minerCountText;
        [SerializeField] private GameObject infoPanel;

        [Header("Animation")]
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseAmount = 0.05f;
        [SerializeField] private float rotationSpeed = 20f;

        private ulong orbId;
        private uint totalPackets;
        private uint activeMinerCount;
        private Color baseColor;
        private Vector3 baseScale;
        private Material orbMaterial;

        void Awake()
        {
            Stopwatch awakeTimer = Stopwatch.StartNew();

            if (useWavePacketDisplay)
            {
                Stopwatch loadTimer = Stopwatch.StartNew();

                // Settings should be assigned in prefab - fail loudly if not
                if (wavePacketSettings == null)
                {
                    UnityEngine.Debug.LogError($"[WavePacketVisual] No WavePacketSettings assigned on {gameObject.name}! Prefab configuration error.");
                    return;
                }

                loadTimer.Stop();
                Stopwatch createTimer = Stopwatch.StartNew();

                // Create wave packet display
                GameObject displayObj = new GameObject("WavePacketDisplay");
                displayObj.transform.SetParent(transform);
                displayObj.transform.localPosition = Vector3.zero;
                displayObj.transform.localRotation = Quaternion.identity;
                displayObj.transform.localScale = Vector3.one;

                wavePacketDisplay = displayObj.AddComponent<WavePacketDisplay>();

                createTimer.Stop();
                Stopwatch reflectionTimer = Stopwatch.StartNew();

                // Assign settings - display configuration comes from the settings asset
                var settingsField = typeof(WavePacketDisplay).GetField("settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (settingsField != null) settingsField.SetValue(wavePacketDisplay, wavePacketSettings);

                reflectionTimer.Stop();

                if (orbRenderer != null)
                    orbRenderer.enabled = false;

                awakeTimer.Stop();
                UnityEngine.Debug.Log($"[WavePacketVisual] Awake: {awakeTimer.ElapsedMilliseconds}ms | Load: {loadTimer.ElapsedMilliseconds}ms | Create: {createTimer.ElapsedMilliseconds}ms | Reflection: {reflectionTimer.ElapsedMilliseconds}ms");
            }
            else
            {
                if (orbRenderer == null)
                    orbRenderer = GetComponentInChildren<Renderer>();

                if (orbRenderer != null)
                {
                    orbMaterial = new Material(orbRenderer.sharedMaterial);
                    orbRenderer.material = orbMaterial;
                }
            }

            baseScale = transform.localScale;
        }

        void Update()
        {
            // No need to update the wave packet renderer every frame for static orbs
            // The mesh is created once and stays visible

            float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = baseScale * (1f + pulse);
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

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

        public void Initialize(ulong orbId, Color color, uint packets, uint miners, List<WavePacketSample> composition = null)
        {
            this.orbId = orbId;
            this.baseColor = color;
            this.totalPackets = packets;
            this.activeMinerCount = miners;

            gameObject.name = "Orb_" + orbId;

            if (composition != null && composition.Count > 0)
            {
                currentComposition = composition.ToArray();
            }
            else
            {
                currentComposition = CreateDefaultComposition(color);
            }

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

        public void UpdateComposition(List<WavePacketSample> composition)
        {
            if (composition != null && composition.Count > 0)
            {
                currentComposition = composition.ToArray();
                UpdateVisuals();
            }
        }

        private WavePacketSample[] CreateDefaultComposition(Color color)
        {
            float frequency = 0.0f;

            if (color.r > 0.9f && color.g < 0.1f && color.b < 0.1f) frequency = 0.0f;
            else if (color.r > 0.9f && color.g > 0.9f && color.b < 0.1f) frequency = 1.047f;
            else if (color.r < 0.1f && color.g > 0.9f && color.b < 0.1f) frequency = 2.094f;
            else if (color.r < 0.1f && color.g > 0.9f && color.b > 0.9f) frequency = 3.142f;
            else if (color.r < 0.1f && color.g < 0.1f && color.b > 0.9f) frequency = 4.189f;
            else if (color.r > 0.9f && color.g < 0.1f && color.b > 0.9f) frequency = 5.236f;

            return new WavePacketSample[]
            {
                new WavePacketSample { Frequency = frequency, Amplitude = 1.0f, Phase = 0.0f, Count = 20 }
            };
        }

        private void UpdateVisuals()
        {
            if (useWavePacketDisplay && wavePacketDisplay != null)
            {
                if (currentComposition != null && currentComposition.Length > 0)
                {
                    wavePacketDisplay.SetComposition(currentComposition);
                }
            }
            else
            {
                UpdateFallbackVisuals();
            }

            UpdateUI();
            UpdateEffects();
        }

        private void UpdateFallbackVisuals()
        {
            if (orbMaterial != null)
            {
                orbMaterial.color = baseColor;
                orbMaterial.EnableKeyword("_EMISSION");
                orbMaterial.SetColor("_EmissionColor", baseColor * 0.5f);
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
            if (orbLight != null)
            {
                orbLight.color = baseColor;
                orbLight.intensity = Mathf.Lerp(1f, 3f, activeMinerCount / 5f);
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
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, 30f);
        }
    }
}
