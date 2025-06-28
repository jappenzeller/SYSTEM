// SimpleEnergyIntegration.cs - Unity integration for Simple Energy System
using System;
using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;
using System.Linq;
using System.Collections;

public class SimpleEnergyIntegration : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject simpleEnergyOrbPrefab;  // Assign your energy orb prefab
    
    [Header("World Settings")]
    public Transform worldCenter;
    public float worldRadius = 300f;
    
    // Track active orbs
    private Dictionary<ulong, GameObject> activeSimpleOrbs = new Dictionary<ulong, GameObject>();
    
    // Reference to GameManager's connection
    private DbConnection conn => GameManager.Conn;
    
    void Start()
    {
        // Subscribe to simple energy orb events when connection is ready
        if (GameManager.IsConnected())
        {
            SubscribeToSimpleEnergyEvents();
        }
        else
        {
            // Wait for connection
            StartCoroutine(WaitForConnection());
        }
        
        // Also subscribe to connection events if we're on the GameManager
        var gameManager = GetComponent<GameManager>();
        if (gameManager != null)
        {
            GameManager.OnConnected += OnGameManagerConnected;
            GameManager.OnDisconnected += OnGameManagerDisconnected;
        }
    }
    
    void OnGameManagerConnected(DbConnection conn, SpacetimeDB.Identity identity)
    {
        Debug.Log("[SimpleEnergyIntegration] GameManager connected, subscribing to energy events");
        SubscribeToSimpleEnergyEvents();
    }
    
    void OnGameManagerDisconnected(Exception ex)
    {
        Debug.Log("[SimpleEnergyIntegration] GameManager disconnected, cleaning up");
        // Clean up any active orbs
        foreach (var orb in activeSimpleOrbs.Values)
        {
            if (orb != null) Destroy(orb);
        }
        activeSimpleOrbs.Clear();
    }
    
    IEnumerator WaitForConnection()
    {
        while (!GameManager.IsConnected())
        {
            yield return new WaitForSeconds(0.5f);
        }
        SubscribeToSimpleEnergyEvents();
    }
    
    void SubscribeToSimpleEnergyEvents()
    {
        if (conn == null) return;
        
        // Subscribe to simple energy orb table changes
        conn.Db.SimpleEnergyOrb.OnInsert += OnSimpleEnergyOrbInsert;
        conn.Db.SimpleEnergyOrb.OnUpdate += OnSimpleEnergyOrbUpdate;
        conn.Db.SimpleEnergyOrb.OnDelete += OnSimpleEnergyOrbDelete;
        
        // Subscribe to simple energy storage changes (for inventory)
        conn.Db.SimpleEnergyStorage.OnInsert += OnSimpleEnergyStorageInsert;
        conn.Db.SimpleEnergyStorage.OnUpdate += OnSimpleEnergyStorageUpdate;
        conn.Db.SimpleEnergyStorage.OnDelete += OnSimpleEnergyStorageDelete;
        
        Debug.Log("Subscribed to Simple Energy System events");
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        if (conn != null)
        {
            conn.Db.SimpleEnergyOrb.OnInsert -= OnSimpleEnergyOrbInsert;
            conn.Db.SimpleEnergyOrb.OnUpdate -= OnSimpleEnergyOrbUpdate;
            conn.Db.SimpleEnergyOrb.OnDelete -= OnSimpleEnergyOrbDelete;
            
            conn.Db.SimpleEnergyStorage.OnInsert -= OnSimpleEnergyStorageInsert;
            conn.Db.SimpleEnergyStorage.OnUpdate -= OnSimpleEnergyStorageUpdate;
            conn.Db.SimpleEnergyStorage.OnDelete -= OnSimpleEnergyStorageDelete;
        }
        
        // Unsubscribe from GameManager events
        var gameManager = GetComponent<GameManager>();
        if (gameManager != null)
        {
            GameManager.OnConnected -= OnGameManagerConnected;
            GameManager.OnDisconnected -= OnGameManagerDisconnected;
        }
    }
    
    // ============================================================================
    // Simple Energy Orb Event Handlers
    // ============================================================================
    
    void OnSimpleEnergyOrbInsert(EventContext ctx, SimpleEnergyOrb orbData)
    {
        CreateSimpleEnergyOrbInWorld(orbData);
    }
    
    void OnSimpleEnergyOrbUpdate(EventContext ctx, SimpleEnergyOrb oldOrb, SimpleEnergyOrb newOrb)
    {
        // Update existing orb position/velocity
        if (activeSimpleOrbs.TryGetValue(newOrb.OrbId, out GameObject orbObj))
        {
            // Update position
            orbObj.transform.position = new Vector3(
                newOrb.Position.X, 
                newOrb.Position.Y, 
                newOrb.Position.Z
            );
            
            // Update velocity if rigidbody exists
            var rb = orbObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = new Vector3(
                    newOrb.Velocity.X, 
                    newOrb.Velocity.Y, 
                    newOrb.Velocity.Z
                );
            }
        }
    }
    
    void OnSimpleEnergyOrbDelete(EventContext ctx, SimpleEnergyOrb orbData)
    {
        // Remove orb from world
        if (activeSimpleOrbs.TryGetValue(orbData.OrbId, out GameObject orbObj))
        {
            if (orbObj != null)
            {
                Destroy(orbObj);
            }
            activeSimpleOrbs.Remove(orbData.OrbId);
        }
    }
    
    // ============================================================================
    // Simple Energy Storage Event Handlers (for inventory)
    // ============================================================================
    
    void OnSimpleEnergyStorageInsert(EventContext ctx, SimpleEnergyStorage storageData)
    {
        // Handle new energy storage (player collected energy)
        if (IsCurrentPlayerStorage(storageData))
        {
            Debug.Log($"Player gained {storageData.QuantumCount} {GetColorName(storageData.EnergySignature.Frequency)} energy");
            
            // Update UI or inventory display here
            UpdatePlayerEnergyDisplay();
        }
    }
    
    void OnSimpleEnergyStorageUpdate(EventContext ctx, SimpleEnergyStorage oldStorage, SimpleEnergyStorage newStorage)
    {
        // Handle energy amount changes
        if (IsCurrentPlayerStorage(newStorage))
        {
            Debug.Log($"Player energy updated: {newStorage.QuantumCount} {GetColorName(newStorage.EnergySignature.Frequency)} energy");
            UpdatePlayerEnergyDisplay();
        }
    }
    
    void OnSimpleEnergyStorageDelete(EventContext ctx, SimpleEnergyStorage storageData)
    {
        // Handle energy removal
        if (IsCurrentPlayerStorage(storageData))
        {
            Debug.Log($"Player lost {GetColorName(storageData.EnergySignature.Frequency)} energy");
            UpdatePlayerEnergyDisplay();
        }
    }
    
    // ============================================================================
    // Orb Creation and Visualization
    // ============================================================================
    
    void CreateSimpleEnergyOrbInWorld(SimpleEnergyOrb orbData)
    {
        if (simpleEnergyOrbPrefab == null)
        {
            Debug.LogError("Simple Energy Orb Prefab not assigned!");
            return;
        }
        
        // Create orb GameObject
        Vector3 position = new Vector3(
            orbData.Position.X, 
            orbData.Position.Y, 
            orbData.Position.Z
        );
        GameObject orbObj = Instantiate(simpleEnergyOrbPrefab, position, Quaternion.identity);
        
        // Set orb color based on frequency
        SetOrbColor(orbObj, orbData.EnergySignature.Frequency);
        
        // Set orb velocity
        var rb = orbObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = new Vector3(
                orbData.Velocity.X, 
                orbData.Velocity.Y, 
                orbData.Velocity.Z
            );
        }
        
        // Store reference
        activeSimpleOrbs[orbData.OrbId] = orbObj;
        
        // Add collection script
        var collector = orbObj.GetComponent<SimpleEnergyOrbCollector>();
        if (collector == null)
        {
            collector = orbObj.AddComponent<SimpleEnergyOrbCollector>();
        }
        collector.Initialize(orbData.OrbId, orbData.QuantumCount);
        
        Debug.Log($"Created {GetColorName(orbData.EnergySignature.Frequency)} energy orb (ID: {orbData.OrbId})");
    }
    
    void SetOrbColor(GameObject orbObj, float frequency)
    {
        Color orbColor = GetColorFromFrequency(frequency);
        
        // Try to find and color the renderer
        var renderer = orbObj.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            renderer.material.color = orbColor;
            
            // Set emission for glow effect
            if (renderer.material.HasProperty("_EmissionColor"))
            {
                renderer.material.SetColor("_EmissionColor", orbColor * 0.5f);
            }
        }
        
        // Color any particle systems
        var particles = orbObj.GetComponent<ParticleSystem>();
        if (particles != null)
        {
            var main = particles.main;
            main.startColor = orbColor;
        }
        
        // Color any lights
        var light = orbObj.GetComponent<Light>();
        if (light != null)
        {
            light.color = orbColor;
        }
    }
    
    // ============================================================================
    // Color and Frequency Helpers
    // ============================================================================
    
    Color GetColorFromFrequency(float frequency)
    {
        return frequency switch
        {
            < 0.1f  => new Color(0.6f, 0.1f, 0.1f),  // Deep Red
            < 0.25f => new Color(1.0f, 0.2f, 0.2f),  // Red
            < 0.4f  => new Color(1.0f, 0.6f, 0.2f),  // Orange
            < 0.55f => new Color(1.0f, 1.0f, 0.2f),  // Yellow
            < 0.7f  => new Color(0.2f, 1.0f, 0.2f),  // Green
            < 0.85f => new Color(0.2f, 0.2f, 1.0f),  // Blue
            _       => new Color(0.8f, 0.2f, 1.0f)   // Violet
        };
    }
    
    string GetColorName(float frequency)
    {
        return frequency switch
        {
            < 0.1f  => "Deep Red",
            < 0.25f => "Red", 
            < 0.4f  => "Orange",
            < 0.55f => "Yellow",
            < 0.7f  => "Green",
            < 0.85f => "Blue",
            _       => "Violet"
        };
    }
    
    // ============================================================================
    // Player Helpers
    // ============================================================================
    
    bool IsCurrentPlayerStorage(SimpleEnergyStorage storage)
    {
        // Check if this storage belongs to the current player
        var currentPlayer = GetCurrentPlayer();
        return storage.OwnerType == "player" && 
               currentPlayer != null && 
               storage.OwnerId == currentPlayer.PlayerId;
    }
    
    Player GetCurrentPlayer()
    {
        // Get current player from GameManager
        if (conn != null && GameManager.LocalIdentity.HasValue)
        {
            return conn.Db.Player.Identity.Find(GameManager.LocalIdentity.Value);
        }
        return null;
    }
    
    void UpdatePlayerEnergyDisplay()
    {
        // Update your UI here - this is a placeholder
        // You might want to update energy counters, inventory UI, etc.
        Debug.Log("Player energy display updated");
        
        // Example: Update UI if you have a UI manager
        // var uiManager = FindFirstObjectByType<UIManager>();
        // if (uiManager != null)
        // {
        //     uiManager.UpdateEnergyDisplay();
        // }
    }
    
    // ============================================================================
    // Public Interface for Testing
    // ============================================================================
    
    public void TestSpawnSimpleEnergyOrb()
    {
        // Test function to spawn an orb
        if (GameManager.IsConnected() && conn != null)
        {
            conn.Reducers.DebugTestSimpleEnergyEmission();
        }
    }
    
    public void CheckSimpleEnergyStatus()
    {
        // Test function to check system status
        if (GameManager.IsConnected() && conn != null)
        {
            conn.Reducers.DebugSimpleEnergyStatus();
        }
    }
    
    // ============================================================================
    // Debug Info
    // ============================================================================
    
    void OnGUI()
    {
        if (!Application.isEditor) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"Active Simple Orbs: {activeSimpleOrbs.Count}");
        
        if (GUILayout.Button("Test Spawn Orbs"))
        {
            TestSpawnSimpleEnergyOrb();
        }
        
        if (GUILayout.Button("Check System Status"))
        {
            CheckSimpleEnergyStatus();
        }
        
        GUILayout.EndArea();
    }
}

// ============================================================================
// Simple Energy Orb Collector Component
// ============================================================================

public class SimpleEnergyOrbCollector : MonoBehaviour
{
    private ulong orbId;
    private uint quantumCount;
    
    public void Initialize(ulong id, uint count)
    {
        orbId = id;
        quantumCount = count;
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Check if player touched the orb
        if (other.CompareTag("Player"))
        {
            // Get the current local player from the database
            if (GameManager.IsConnected() && GameManager.Conn != null && GameManager.LocalIdentity.HasValue)
            {
                var localPlayer = GameManager.Conn.Db.Player.Identity.Find(GameManager.LocalIdentity.Value);
                if (localPlayer != null)
                {
                    // We found the local player, now collect the orb
                    CollectOrb(localPlayer.PlayerId);
                }
            }
        }
    }
    
    void CollectOrb(uint playerId)
    {
        // Call server to collect the orb
        if (GameManager.IsConnected() && GameManager.Conn != null)
        {
            GameManager.Conn.Reducers.CollectSimpleEnergyOrb(orbId, playerId);
            Debug.Log($"Collecting simple energy orb {orbId} for player {playerId}");
        }
    }
}