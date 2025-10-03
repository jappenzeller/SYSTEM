using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB.Types;

namespace SYSTEM.Game
{
    /// <summary>
    /// Manages enhanced wave packet visuals with concentric rings and grid distortion
    /// Based on the wave packet concept sketch showing layered frequency bands
    /// </summary>
    public class WavePacketVisualizer : MonoBehaviour
    {
        [Header("Wave Visual Components")]
        [SerializeField] private GameObject concentricRingsPrefab;
        [SerializeField] private Material gridDistortionMaterial;
        [SerializeField] private GameObject gridPlanePrefab;

        [Header("Ring Configuration")]
        [SerializeField] private Color[] frequencyColors = new Color[]
        {
            new Color(1f, 0f, 0f, 0.8f),    // Red (innermost) - Base frequency
            new Color(1f, 1f, 0f, 0.8f),    // Yellow - RG mix
            new Color(0f, 1f, 0f, 0.8f),    // Green - Phase
            new Color(0f, 1f, 1f, 0.8f),    // Cyan - GB mix
            new Color(0f, 0f, 1f, 0.8f),    // Blue - Computation
            new Color(1f, 0f, 1f, 0.8f)     // Magenta (outermost) - BR mix
        };

        [Header("Animation")]
        [SerializeField] private float ringExpansionRate = 2f;
        [SerializeField] private float ringRotationSpeed = 30f;
        [SerializeField] private AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0, 1, 1, 1);
        [SerializeField] private float pulseAmplitude = 0.2f;
        [SerializeField] private float gridDistortionStrength = 0.5f;

        [Header("Performance")]
        [SerializeField] private int maxActivePackets = 32;
        [SerializeField] private bool useObjectPooling = true;

        // Static list for shader access
        private static List<WavePacketData> activePackets = new();
        private static RenderTexture distortionMap;
        private GameObject gridPlane;
        private Queue<GameObject> ringPool = new Queue<GameObject>();

        // Shader property IDs
        private static readonly int PacketPositionsID = Shader.PropertyToID("_PacketPositions");
        private static readonly int PacketCountID = Shader.PropertyToID("_ActivePacketCount");
        private static readonly int DistortionMapID = Shader.PropertyToID("_DistortionMap");
        private static readonly int DistortionStrengthID = Shader.PropertyToID("_DistortionStrength");

        private struct WavePacketData
        {
            public ulong PacketId;
            public Vector3 Position;
            public float Frequency;
            public float Amplitude;
            public Dictionary<FrequencyBand, uint> Composition; // Multi-frequency composition
            public uint TotalCount;
            public float CreationTime;
            public GameObject VisualObject;
            public Vector3 StartPosition;
            public Vector3 TargetPosition;
        }

        void Awake()
        {
            // Initialize pulse curve if not set
            if (pulseCurve == null || pulseCurve.keys.Length == 0)
            {
                pulseCurve = AnimationCurve.EaseInOut(0, 1, 1, 1);
                pulseCurve.AddKey(0.5f, 1.2f);
            }
        }

        void Start()
        {
            InitializeGrid();
            InitializeDistortionMap();

            if (useObjectPooling)
            {
                InitializeObjectPool();
            }

            // Subscribe to mining events if WavePacketMiningSystem exists
            var miningSystem = GetComponent<WavePacketMiningSystem>();
            if (miningSystem != null)
            {
                SystemDebug.Log(SystemDebug.Category.Mining, "[WavePacketVisualizer] Connected to mining system");
            }
        }

        void OnDestroy()
        {
            if (distortionMap != null)
            {
                distortionMap.Release();
                distortionMap = null;
            }

            // Clean up all active visuals
            foreach (var packet in activePackets)
            {
                if (packet.VisualObject != null)
                {
                    Destroy(packet.VisualObject);
                }
            }
            activePackets.Clear();
        }

        private void InitializeGrid()
        {
            // Create grid plane for distortion effect
            if (gridPlanePrefab != null)
            {
                gridPlane = Instantiate(gridPlanePrefab);
                gridPlane.name = "WavePacketDistortionGrid";
                gridPlane.transform.position = Vector3.zero;
                gridPlane.transform.localScale = new Vector3(100f, 1f, 100f);

                var renderer = gridPlane.GetComponent<MeshRenderer>();
                if (renderer != null && gridDistortionMaterial != null)
                {
                    renderer.material = gridDistortionMaterial;
                    gridDistortionMaterial.SetFloat(DistortionStrengthID, gridDistortionStrength);
                }
            }
            else
            {
                // Create procedural grid if no prefab
                CreateProceduralGrid();
            }
        }

        private void CreateProceduralGrid()
        {
            gridPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            gridPlane.name = "WavePacketDistortionGrid";
            gridPlane.transform.position = new Vector3(0, -0.5f, 0); // Slightly below ground
            gridPlane.transform.localScale = new Vector3(10f, 1f, 10f);

            // Remove collider to avoid interference
            var collider = gridPlane.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            // Apply material if available
            if (gridDistortionMaterial != null)
            {
                var renderer = gridPlane.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.material = gridDistortionMaterial;
                }
            }
        }

        private void InitializeDistortionMap()
        {
            // Create render texture for complex distortion effects
            distortionMap = new RenderTexture(512, 512, 0, RenderTextureFormat.RFloat);
            distortionMap.filterMode = FilterMode.Bilinear;
            distortionMap.wrapMode = TextureWrapMode.Clamp;
            distortionMap.name = "WavePacketDistortionMap";

            if (gridDistortionMaterial != null)
            {
                gridDistortionMaterial.SetTexture(DistortionMapID, distortionMap);
            }
        }

        private void InitializeObjectPool()
        {
            // Pre-create ring objects for performance
            for (int i = 0; i < 10; i++)
            {
                GameObject rings = CreateConcentricRings();
                rings.SetActive(false);
                ringPool.Enqueue(rings);
            }
        }

        /// <summary>
        /// Called by WavePacketMiningSystem when a packet is extracted
        /// </summary>
        public void OnPacketExtracted(ulong packetId, Vector3 sourcePosition, Vector3 targetPosition, float frequency)
        {
            CreateEnhancedWaveVisual(packetId, sourcePosition, targetPosition, frequency);
        }

        public GameObject CreateEnhancedWaveVisual(ulong packetId, Vector3 sourcePos, Vector3 targetPos, float frequency)
        {
            // Limit active packets for performance
            if (activePackets.Count >= maxActivePackets)
            {
                RemoveOldestPacket();
            }

            // Get or create visual object
            GameObject waveVisual;
            if (useObjectPooling && ringPool.Count > 0)
            {
                waveVisual = ringPool.Dequeue();
                waveVisual.SetActive(true);
            }
            else if (concentricRingsPrefab != null)
            {
                waveVisual = Instantiate(concentricRingsPrefab);
            }
            else
            {
                waveVisual = CreateConcentricRings();
            }

            waveVisual.name = $"WavePacket_{packetId}_Rings";
            waveVisual.transform.position = sourcePos;

            // Configure rings based on frequency
            ConfigureRings(waveVisual, frequency);

            // Add to active packets for distortion and tracking
            var packetData = new WavePacketData
            {
                PacketId = packetId,
                Position = sourcePos,
                Frequency = frequency,
                Amplitude = 1f,
                CreationTime = Time.time,
                VisualObject = waveVisual,
                StartPosition = sourcePos,
                TargetPosition = targetPos
            };

            activePackets.Add(packetData);
            SystemDebug.Log(SystemDebug.Category.Mining, $"[WavePacketVisualizer] Created enhanced visual for packet {packetId}");

            return waveVisual;
        }

        private GameObject CreateConcentricRings()
        {
            GameObject container = new GameObject("ConcentricRings");

            // Create 6 rings matching the frequency spectrum in the sketch
            for (int i = 0; i < 6; i++)
            {
                GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = $"Ring_{GetFrequencyName(i)}";
                ring.transform.parent = container.transform;

                // Scale rings from inner to outer (matching sketch proportions)
                float radius = 0.5f + (i * 0.3f);
                ring.transform.localScale = new Vector3(radius, 0.02f, radius);
                ring.transform.localPosition = Vector3.zero;
                ring.transform.localRotation = Quaternion.identity;

                // Remove collider to avoid physics interference
                var collider = ring.GetComponent<Collider>();
                if (collider != null) Destroy(collider);

                // Apply frequency-specific color
                var renderer = ring.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.name = $"RingMaterial_{GetFrequencyName(i)}";

                    // Configure for transparency
                    mat.SetFloat("_Surface", 1); // Transparent
                    mat.SetFloat("_Blend", 0);   // Alpha blend
                    mat.SetFloat("_AlphaClip", 0);
                    mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_ZWrite", 0);
                    mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000; // Transparent queue

                    // Set color with transparency
                    Color ringColor = frequencyColors[i];
                    mat.color = ringColor;

                    // Add emission for glow effect
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", ringColor * 0.5f);

                    renderer.material = mat;
                }
            }

            return container;
        }

        private string GetFrequencyName(int index)
        {
            string[] names = { "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta" };
            return index < names.Length ? names[index] : $"Frequency{index}";
        }

        private void ConfigureRings(GameObject waveVisual, float frequency)
        {
            // Get all ring children
            var rings = waveVisual.GetComponentsInChildren<MeshRenderer>();

            // Normalize frequency to 0-1 range
            float normalizedFreq = Mathf.Clamp01(frequency / (2f * Mathf.PI));

            // Configure each ring based on the packet's frequency
            for (int i = 0; i < rings.Length && i < 6; i++)
            {
                if (rings[i].material != null)
                {
                    // Modulate ring colors based on packet frequency
                    Color baseColor = frequencyColors[i];

                    // Create interference pattern based on frequency
                    float phaseShift = i * Mathf.PI / 3f; // 60 degree phase shifts
                    float intensity = 0.5f + 0.5f * Mathf.Sin((normalizedFreq * 2f * Mathf.PI) + phaseShift);

                    // Adjust alpha for layering effect
                    baseColor.a *= (0.4f + 0.6f * intensity);

                    rings[i].material.color = baseColor;
                    rings[i].material.SetColor("_EmissionColor", baseColor * intensity * 0.5f);
                }
            }
        }

        void Update()
        {
            if (activePackets.Count == 0) return;

            UpdatePacketPositions();
            UpdateShaderData();
            AnimateRings();
            CleanupArrivedPackets();
        }

        private void UpdatePacketPositions()
        {
            // Update packet positions for movement tracking
            for (int i = 0; i < activePackets.Count; i++)
            {
                var packet = activePackets[i];
                if (packet.VisualObject != null)
                {
                    packet.Position = packet.VisualObject.transform.position;
                    activePackets[i] = packet;
                }
            }
        }

        private void UpdateShaderData()
        {
            // Update shader with packet positions for grid distortion
            if (gridDistortionMaterial != null && activePackets.Count > 0)
            {
                int count = Mathf.Min(activePackets.Count, maxActivePackets);
                Vector4[] positions = new Vector4[count];

                for (int i = 0; i < count; i++)
                {
                    var packet = activePackets[i];
                    positions[i] = new Vector4(
                        packet.Position.x,
                        packet.Position.y,
                        packet.Position.z,
                        packet.Amplitude
                    );
                }

                gridDistortionMaterial.SetVectorArray(PacketPositionsID, positions);
                gridDistortionMaterial.SetInt(PacketCountID, count);
            }
        }

        private void AnimateRings()
        {
            for (int i = 0; i < activePackets.Count; i++)
            {
                var packet = activePackets[i];
                if (packet.VisualObject == null) continue;

                float age = Time.time - packet.CreationTime;

                // Rotate rings for dynamic effect
                packet.VisualObject.transform.Rotate(Vector3.up, ringRotationSpeed * Time.deltaTime);

                // Pulse effect using animation curve
                float pulseValue = pulseCurve.Evaluate((age * 2f) % 1f);
                float pulseScale = 1f + (pulseValue - 1f) * pulseAmplitude;

                // Slight expansion over time
                float expansionScale = 1f + (age * ringExpansionRate * 0.05f);

                // Apply combined scale
                packet.VisualObject.transform.localScale = Vector3.one * pulseScale * expansionScale;

                // Update amplitude for distortion based on distance traveled
                float distanceRatio = Vector3.Distance(packet.Position, packet.StartPosition) /
                                    Vector3.Distance(packet.TargetPosition, packet.StartPosition);
                packet.Amplitude = Mathf.Lerp(1f, 0.2f, distanceRatio); // Fade as it travels

                activePackets[i] = packet;
            }
        }

        private void CleanupArrivedPackets()
        {
            // Remove packets with null visual objects
            for (int i = activePackets.Count - 1; i >= 0; i--)
            {
                if (activePackets[i].VisualObject == null)
                {
                    activePackets.RemoveAt(i);
                }
            }
        }

        private void RemoveOldestPacket()
        {
            if (activePackets.Count > 0)
            {
                var oldest = activePackets[0];
                RemovePacketVisual(oldest.PacketId);
            }
        }

        public void RemovePacketVisual(ulong packetId)
        {
            var packetIndex = activePackets.FindIndex(p => p.PacketId == packetId);
            if (packetIndex >= 0)
            {
                var packet = activePackets[packetIndex];

                if (packet.VisualObject != null)
                {
                    if (useObjectPooling)
                    {
                        // Return to pool
                        packet.VisualObject.SetActive(false);
                        packet.VisualObject.transform.position = Vector3.zero;
                        packet.VisualObject.transform.rotation = Quaternion.identity;
                        ringPool.Enqueue(packet.VisualObject);
                    }
                    else
                    {
                        Destroy(packet.VisualObject);
                    }
                }

                activePackets.RemoveAt(packetIndex);
                SystemDebug.Log(SystemDebug.Category.Mining, $"[WavePacketVisualizer] Removed visual for packet {packetId}");
            }
        }

        /// <summary>
        /// Clear all active wave packet visuals
        /// </summary>
        public void ClearAllVisuals()
        {
            foreach (var packet in activePackets)
            {
                if (packet.VisualObject != null)
                {
                    if (useObjectPooling)
                    {
                        packet.VisualObject.SetActive(false);
                        ringPool.Enqueue(packet.VisualObject);
                    }
                    else
                    {
                        Destroy(packet.VisualObject);
                    }
                }
            }
            activePackets.Clear();
        }

        /// <summary>
        /// Creates a composite wave visual with multiple frequency bands
        /// </summary>
        public GameObject CreateCompositeWaveVisual(
            ulong packetId,
            Vector3 sourcePos,
            Vector3 targetPos,
            List<WavePacketSample> composition,
            uint totalCount)
        {
            // Limit active packets for performance
            if (activePackets.Count >= maxActivePackets)
            {
                RemoveOldestPacket();
            }

            // Get or create visual object
            GameObject waveVisual;
            if (useObjectPooling && ringPool.Count > 0)
            {
                waveVisual = ringPool.Dequeue();
                waveVisual.SetActive(true);
            }
            else if (concentricRingsPrefab != null)
            {
                waveVisual = Instantiate(concentricRingsPrefab);
            }
            else
            {
                waveVisual = CreateConcentricRings();
            }

            waveVisual.name = $"CompositePacket_{packetId}";
            waveVisual.transform.position = sourcePos;

            // Convert composition to frequency band dictionary
            Dictionary<FrequencyBand, uint> compDict = new Dictionary<FrequencyBand, uint>();
            foreach (var sample in composition)
            {
                FrequencyBand band = GetFrequencyBand(sample.Frequency);
                if (compDict.ContainsKey(band))
                {
                    compDict[band] += sample.Count;
                }
                else
                {
                    compDict[band] = sample.Count;
                }
            }

            // Configure rings based on composition
            ConfigureCompositeRings(waveVisual, compDict, totalCount);

            // Add to active packets for tracking
            var packetData = new WavePacketData
            {
                PacketId = packetId,
                Position = sourcePos,
                Composition = compDict,
                TotalCount = totalCount,
                CreationTime = Time.time,
                VisualObject = waveVisual,
                StartPosition = sourcePos,
                TargetPosition = targetPos
            };

            activePackets.Add(packetData);

            SystemDebug.Log(SystemDebug.Category.Mining,
                $"[WavePacketVisualizer] Created composite visual for packet {packetId} with {compDict.Count} frequencies, total: {totalCount}");

            return waveVisual;
        }

        /// <summary>
        /// Configures rings based on composition proportions
        /// </summary>
        private void ConfigureCompositeRings(GameObject waveVisual,
            Dictionary<FrequencyBand, uint> composition, uint totalCount)
        {
            var rings = waveVisual.GetComponentsInChildren<MeshRenderer>();

            // Calculate total for proportions
            float total = (float)totalCount;
            if (total == 0) total = 1; // Avoid division by zero

            // Configure each ring based on its proportion in the composition
            for (int i = 0; i < rings.Length && i < 6; i++)
            {
                FrequencyBand band = (FrequencyBand)i;
                uint count = composition.ContainsKey(band) ? composition[band] : 0;
                float proportion = count / total;

                if (rings[i] != null && rings[i].material != null)
                {
                    // Scale ring based on proportion
                    float baseScale = 0.5f + (i * 0.3f); // Base ring sizes
                    float proportionalScale = baseScale * (0.3f + proportion * 1.7f); // Scale by content
                    rings[i].transform.localScale = new Vector3(proportionalScale, 0.02f, proportionalScale);

                    // Adjust opacity based on presence
                    Color baseColor = frequencyColors[i];
                    if (count == 0)
                    {
                        // Make absent frequencies very faint
                        baseColor.a = 0.1f;
                    }
                    else
                    {
                        // Scale alpha by proportion (min 0.3, max 0.9)
                        baseColor.a = 0.3f + (proportion * 0.6f);
                    }

                    rings[i].material.color = baseColor;

                    // Set emission based on count
                    float emissionIntensity = proportion * 0.5f;
                    rings[i].material.SetColor("_EmissionColor", baseColor * emissionIntensity);

                    // Log for debugging
                    if (count > 0)
                    {
                        SystemDebug.Log(SystemDebug.Category.Mining,
                            $"  Ring {i} ({band}): {count} packets, proportion: {proportion:F2}, scale: {proportionalScale:F2}");
                    }
                }
            }

            // Add overall glow effect based on total packet count
            Light light = waveVisual.GetComponentInChildren<Light>();
            if (light != null)
            {
                light.intensity = 1f + (totalCount / 100f); // Brighter for larger packets

                // Set light color to dominant frequency
                FrequencyBand dominant = GetDominantFrequency(composition);
                light.color = frequencyColors[(int)dominant];
            }
        }

        /// <summary>
        /// Finds the dominant frequency in a composition
        /// </summary>
        private FrequencyBand GetDominantFrequency(Dictionary<FrequencyBand, uint> composition)
        {
            FrequencyBand dominant = FrequencyBand.Red;
            uint maxCount = 0;

            foreach (var kvp in composition)
            {
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    dominant = kvp.Key;
                }
            }

            return dominant;
        }

        /// <summary>
        /// Converts a frequency value (0-1) to a frequency band
        /// </summary>
        public FrequencyBand GetFrequencyBand(float frequency)
        {
            // Map frequency (0-1) to bands
            float radian = frequency * 2f * Mathf.PI;

            if (radian < Mathf.PI / 6f || radian > 11f * Mathf.PI / 6f) return FrequencyBand.Red;
            else if (radian < Mathf.PI / 2f) return FrequencyBand.Yellow;
            else if (radian < 5f * Mathf.PI / 6f) return FrequencyBand.Green;
            else if (radian < 7f * Mathf.PI / 6f) return FrequencyBand.Cyan;
            else if (radian < 3f * Mathf.PI / 2f) return FrequencyBand.Blue;
            else return FrequencyBand.Magenta;
        }

        // Debug visualization
        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            // Draw active packet positions
            Gizmos.color = Color.yellow;
            foreach (var packet in activePackets)
            {
                Gizmos.DrawWireSphere(packet.Position, 0.5f);
                Gizmos.DrawLine(packet.Position, packet.TargetPosition);
            }
        }
    }
}