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

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        // Tracking
        private Dictionary<ulong, GameObject> activeOrbs = new Dictionary<ulong, GameObject>();
        private DbConnection conn;
        private Material orbMaterial;

        void Awake()
        {
            // Create material for orbs
            orbMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            orbMaterial.SetFloat("_Surface", 1); // Transparent
            orbMaterial.SetFloat("_Blend", 0); // Alpha blending
            orbMaterial.EnableKeyword("_ALPHABLEND_ON");
        }

        void OnEnable()
        {
            conn = GameManager.Conn;
            if (conn == null)
            {
                UnityEngine.Debug.LogError("[OrbVisualization] No database connection!");
                enabled = false;
                return;
            }

            // Subscribe to orb table events
            conn.Db.WavePacketOrb.OnInsert += OnOrbInserted;
            conn.Db.WavePacketOrb.OnUpdate += OnOrbUpdated;
            conn.Db.WavePacketOrb.OnDelete += OnOrbDeleted;

            if (showDebugInfo)
                UnityEngine.Debug.Log("[OrbVisualization] Subscribed to WavePacketOrb events");
        }

        void OnDisable()
        {
            if (conn != null)
            {
                conn.Db.WavePacketOrb.OnInsert -= OnOrbInserted;
                conn.Db.WavePacketOrb.OnUpdate -= OnOrbUpdated;
                conn.Db.WavePacketOrb.OnDelete -= OnOrbDeleted;
            }

            // Clean up all visualizations
            foreach (var orb in activeOrbs.Values)
            {
                if (orb != null)
                    Destroy(orb);
            }
            activeOrbs.Clear();
        }

        #region SpacetimeDB Event Handlers

        private void OnOrbInserted(EventContext ctx, WavePacketOrb orb)
        {
            if (showDebugInfo)
                UnityEngine.Debug.Log($"[OrbVisualization] Orb inserted: ID={orb.OrbId}, Pos=({orb.Position.X}, {orb.Position.Y}, {orb.Position.Z}), Packets={orb.TotalWavePackets}");

            CreateOrbVisualization(orb);
        }

        private void OnOrbUpdated(EventContext ctx, WavePacketOrb oldOrb, WavePacketOrb newOrb)
        {
            if (showDebugInfo)
                UnityEngine.Debug.Log($"[OrbVisualization] Orb updated: ID={newOrb.OrbId}, Packets={newOrb.TotalWavePackets}, Miners={newOrb.ActiveMinerCount}");

            UpdateOrbVisualization(newOrb);
        }

        private void OnOrbDeleted(EventContext ctx, WavePacketOrb orb)
        {
            if (showDebugInfo)
                UnityEngine.Debug.Log($"[OrbVisualization] Orb deleted: ID={orb.OrbId}");

            RemoveOrbVisualization(orb.OrbId);
        }

        #endregion

        #region Visualization Methods

        private void CreateOrbVisualization(WavePacketOrb orb)
        {
            // Don't create duplicate
            if (activeOrbs.ContainsKey(orb.OrbId))
            {
                UpdateOrbVisualization(orb);
                return;
            }

            GameObject orbObj;

            // Create orb GameObject
            if (orbPrefab != null)
            {
                orbObj = Instantiate(orbPrefab);
            }
            else
            {
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
                orbVisual.Initialize(orb.OrbId, orbColor, orb.TotalWavePackets, orb.ActiveMinerCount);
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

            if (showDebugInfo)
                UnityEngine.Debug.Log($"[OrbVisualization] Created visualization for orb {orb.OrbId} at ({orb.Position.X}, {orb.Position.Y}, {orb.Position.Z})");
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
            if (showDebugInfo && orb.ActiveMinerCount > 0)
                UnityEngine.Debug.Log($"[OrbVisualization] Orb {orb.OrbId} has {orb.ActiveMinerCount} active miners");
        }

        private void RemoveOrbVisualization(ulong orbId)
        {
            if (activeOrbs.TryGetValue(orbId, out GameObject orbObj))
            {
                if (orbObj != null)
                    Destroy(orbObj);

                activeOrbs.Remove(orbId);

                if (showDebugInfo)
                    UnityEngine.Debug.Log($"[OrbVisualization] Removed visualization for orb {orbId}");
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