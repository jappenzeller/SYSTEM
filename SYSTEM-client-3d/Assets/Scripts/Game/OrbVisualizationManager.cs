using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;

namespace SYSTEM.Game
{
    /// <summary>
    /// Simple visualization manager for WavePacketOrbs
    /// Shows orbs as colored spheres in the world
    /// </summary>
    public class OrbVisualizationManager : MonoBehaviour
    {
        [Header("Visualization Settings")]
        [SerializeField] private GameObject orbPrefab;
        [SerializeField] private float orbVisualScale = 2f; // Visual size of orb

        [Header("Frequency Colors")]
        [SerializeField] private Color redColor = new Color(1f, 0f, 0f, 0.7f);      // 0.0
        [SerializeField] private Color yellowColor = new Color(1f, 1f, 0f, 0.7f);   // 1/6
        [SerializeField] private Color greenColor = new Color(0f, 1f, 0f, 0.7f);    // 1/3
        [SerializeField] private Color cyanColor = new Color(0f, 1f, 1f, 0.7f);     // 1/2
        [SerializeField] private Color blueColor = new Color(0f, 0f, 1f, 0.7f);     // 2/3
        [SerializeField] private Color magentaColor = new Color(1f, 0f, 1f, 0.7f);  // 5/6

        // Tracking
        private Dictionary<ulong, GameObject> activeOrbs = new Dictionary<ulong, GameObject>();
        private Material orbMaterial;

        void Awake()
        {
            SystemDebug.Log(SystemDebug.Category.OrbVisualization, "OrbVisualizationManager Awake - Component initialized");

            // Create material for orbs
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                SystemDebug.LogError(SystemDebug.Category.OrbVisualization, "Could not find URP Lit shader! Using fallback shader.");
                shader = Shader.Find("Standard");
                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                }
            }

            if (shader != null)
            {
                orbMaterial = new Material(shader);
                orbMaterial.SetFloat("_Surface", 1); // Transparent
                orbMaterial.SetFloat("_Blend", 0); // Alpha blending
                orbMaterial.EnableKeyword("_ALPHABLEND_ON");
            }
            else
            {
                SystemDebug.LogError(SystemDebug.Category.OrbVisualization, "Could not find any shader for orb material!");
            }
        }

        void OnEnable()
        {
            // ONLY subscribe to GameEventBus events, no direct database subscriptions
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.Subscribe<OrbInsertedEvent>(OnOrbInsertedEvent);
                GameEventBus.Instance.Subscribe<OrbUpdatedEvent>(OnOrbUpdatedEvent);
                GameEventBus.Instance.Subscribe<OrbDeletedEvent>(OnOrbDeletedEvent);
                GameEventBus.Instance.Subscribe<InitialOrbsLoadedEvent>(OnInitialOrbsLoadedEvent);
                GameEventBus.Instance.Subscribe<WorldTransitionStartedEvent>(OnWorldTransitionEvent);

            SystemDebug.Log(SystemDebug.Category.OrbVisualization, "OrbVisualizationManager subscribed to GameEventBus orb events");
            }
            else
            {
                SystemDebug.LogError(SystemDebug.Category.OrbVisualization, "OrbVisualizationManager: GameEventBus.Instance is null!");
            }
        }

        void OnDisable()
        {
            // Unsubscribe from GameEventBus
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.Unsubscribe<OrbInsertedEvent>(OnOrbInsertedEvent);
                GameEventBus.Instance.Unsubscribe<OrbUpdatedEvent>(OnOrbUpdatedEvent);
                GameEventBus.Instance.Unsubscribe<OrbDeletedEvent>(OnOrbDeletedEvent);
                GameEventBus.Instance.Unsubscribe<InitialOrbsLoadedEvent>(OnInitialOrbsLoadedEvent);
                GameEventBus.Instance.Unsubscribe<WorldTransitionStartedEvent>(OnWorldTransitionEvent);
            }

            // Clean up all visualizations
            foreach (var orb in activeOrbs.Values)
            {
                if (orb != null)
                    Destroy(orb);
            }
            activeOrbs.Clear();
        }

        #region GameEventBus Event Handlers

        private void OnOrbInsertedEvent(OrbInsertedEvent evt)
        {
            SystemDebug.Log(SystemDebug.Category.OrbVisualization, $"Orb inserted: ID={evt.Orb.OrbId}, Pos=({evt.Orb.Position.X}, {evt.Orb.Position.Y}, {evt.Orb.Position.Z}), Packets={evt.Orb.TotalWavePackets}");

            CreateOrbVisualization(evt.Orb);
        }

        private void OnOrbUpdatedEvent(OrbUpdatedEvent evt)
        {
            SystemDebug.Log(SystemDebug.Category.OrbVisualization, $"Orb updated: ID={evt.NewOrb.OrbId}, Packets={evt.NewOrb.TotalWavePackets}, Miners={evt.NewOrb.ActiveMinerCount}");

            UpdateOrbVisualization(evt.NewOrb);
        }

        private void OnOrbDeletedEvent(OrbDeletedEvent evt)
        {
            SystemDebug.Log(SystemDebug.Category.OrbVisualization, $"Orb deleted: ID={evt.Orb.OrbId}");

            RemoveOrbVisualization(evt.Orb.OrbId);
        }

        private void OnInitialOrbsLoadedEvent(InitialOrbsLoadedEvent evt)
        {
            SystemDebug.Log(SystemDebug.Category.OrbVisualization, $"Loading {evt.Orbs.Count} initial orbs");

            foreach (var orb in evt.Orbs)
            {
                CreateOrbVisualization(orb);
            }
        }

        private void OnWorldTransitionEvent(WorldTransitionStartedEvent evt)
        {
            SystemDebug.Log(SystemDebug.Category.OrbVisualization, "World transition, clearing all orbs");

            // Clear all orbs when transitioning worlds
            foreach (var orb in activeOrbs.Values)
            {
                if (orb != null)
                    Destroy(orb);
            }
            activeOrbs.Clear();
        }

        #endregion

        #region Visualization Methods


        private void CreateOrbVisualization(WavePacketOrb orb)
        {
            SystemDebug.Log(SystemDebug.Category.OrbVisualization, $"Creating orb visualization for orb {orb.OrbId} at position ({orb.Position.X}, {orb.Position.Y}, {orb.Position.Z})");

            // Don't create duplicate
            if (activeOrbs.ContainsKey(orb.OrbId))
            {
                SystemDebug.LogWarning(SystemDebug.Category.OrbVisualization, $"Orb {orb.OrbId} already exists, skipping");
                return;
            }

            GameObject orbObj;

            // Create orb GameObject
            if (orbPrefab != null)
            {
                SystemDebug.Log(SystemDebug.Category.OrbVisualization, $"Creating orb from prefab for orb {orb.OrbId}");
                orbObj = Instantiate(orbPrefab);
            }
            else
            {
                SystemDebug.Log(SystemDebug.Category.OrbVisualization, $"No orbPrefab assigned, creating primitive sphere for orb {orb.OrbId}");
                // Fallback: create simple sphere
                orbObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orbObj.name = $"Orb_{orb.OrbId}";

                // Apply material
                var renderer = orbObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = new Material(orbMaterial);
                }
            }

            // Set position
            orbObj.transform.position = new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z);
            orbObj.transform.localScale = Vector3.one * orbVisualScale;

            // Set color based on frequency
            Color orbColor = GetOrbColor(orb);

            // Try to use WavePacketOrbVisual component if prefab has it
            var orbVisual = orbObj.GetComponent<WavePacketOrbVisual>();
            if (orbVisual != null)
            {
                orbVisual.Initialize(orb.OrbId, orbColor, orb.TotalWavePackets, orb.ActiveMinerCount, orb.WavePacketComposition);
            }
            else
            {
                // Fallback: set color directly on renderer
                var rend = orbObj.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.material.color = orbColor;
                }
            }

            // Add to tracking
            activeOrbs[orb.OrbId] = orbObj;

            SystemDebug.Log(SystemDebug.Category.OrbVisualization, $"Successfully created orb GameObject '{orbObj.name}' for orb {orb.OrbId} at world position ({orbObj.transform.position.x}, {orbObj.transform.position.y}, {orbObj.transform.position.z})");
        }

        private void UpdateOrbVisualization(WavePacketOrb orb)
        {
            if (!activeOrbs.TryGetValue(orb.OrbId, out GameObject orbObj))
            {
                // Orb visualization doesn't exist, create it
                CreateOrbVisualization(orb);
                return;
            }

            if (orbObj == null)
            {
                activeOrbs.Remove(orb.OrbId);
                CreateOrbVisualization(orb);
                return;
            }

            // Update position
            orbObj.transform.position = new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z);

            // Try to use WavePacketOrbVisual component if available
            var orbVisual = orbObj.GetComponent<WavePacketOrbVisual>();
            if (orbVisual != null)
            {
                orbVisual.UpdatePacketCount(orb.TotalWavePackets);
                orbVisual.UpdateMinerCount(orb.ActiveMinerCount);

                Color orbColor = GetOrbColor(orb);
                orbVisual.UpdateColor(orbColor);
                orbVisual.UpdateComposition(orb.WavePacketComposition);
            }
            else
            {
                // Fallback: update color directly on renderer
                Color orbColor = GetOrbColor(orb);
                var rend = orbObj.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.material.color = orbColor;
                }
            }

            // Log active mining
            if (orb.ActiveMinerCount > 0)
                SystemDebug.Log(SystemDebug.Category.OrbVisualization, $"Orb {orb.OrbId} has {orb.ActiveMinerCount} active miners");
        }

        private void RemoveOrbVisualization(ulong orbId)
        {
            if (activeOrbs.TryGetValue(orbId, out GameObject orbObj))
            {
                if (orbObj != null)
                    Destroy(orbObj);

                activeOrbs.Remove(orbId);

                SystemDebug.Log(SystemDebug.Category.OrbVisualization, $"Removed visualization for orb {orbId}");
            }
        }

        private Color GetOrbColor(WavePacketOrb orb)
        {
            // Get first frequency from composition
            if (orb.WavePacketComposition != null && orb.WavePacketComposition.Count > 0)
            {
                float frequency = orb.WavePacketComposition[0].Frequency;
                return GetColorFromFrequency(frequency);
            }

            // Default to white if no composition
            return Color.white;
        }

        private Color GetColorFromFrequency(float frequency)
        {
            // Frequency bands:
            // Red: 0.0
            // Yellow: 1/6 ≈ 0.166
            // Green: 1/3 ≈ 0.333
            // Cyan: 1/2 = 0.5
            // Blue: 2/3 ≈ 0.666
            // Magenta: 5/6 ≈ 0.833

            if (frequency < 0.08f) return redColor;
            else if (frequency < 0.25f) return yellowColor;
            else if (frequency < 0.42f) return greenColor;
            else if (frequency < 0.58f) return cyanColor;
            else if (frequency < 0.75f) return blueColor;
            else return magentaColor;
        }

        #endregion
    }
}