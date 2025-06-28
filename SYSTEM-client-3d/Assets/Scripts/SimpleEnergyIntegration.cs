// SimpleEnergyIntegration.cs - Add this to your existing GameManager or create new script
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;
using System.Collections.Generic;

public class SimpleEnergyIntegration : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject simpleEnergyOrbPrefab;  // Assign your energy orb prefab
    
    [Header("World Settings")]
    public Transform worldCenter;
    public float worldRadius = 300f;
    
    // Track active orbs
    private Dictionary<ulong, GameObject> activeSimpleOrbs = new Dictionary<ulong, GameObject>();
    
    void Start()
    {
        // Subscribe to simple energy orb events
        SubscribeToSimpleEnergyEvents();
    }
    
    void SubscribeToSimpleEnergyEvents()
    {
        // Subscribe to simple energy orb table changes
        SpacetimeDBClient.Subscribe<SimpleEnergyOrb>(
            OnSimpleEnergyOrbInsert,
            OnSimpleEnergyOrbUpdate, 
            OnSimpleEnergyOrbDelete
        );
        
        // Subscribe to simple energy storage changes (for inventory)
        SpacetimeDBClient.Subscribe<SimpleEnergyStorage>(
            OnSimpleEnergyStorageInsert,
            OnSimpleEnergyStorageUpdate,
            OnSimpleEnergyStorageDelete
        );
        
        Debug.Log("Subscribed to Simple Energy System events");
    }
    
    // ============================================================================
    // Simple Energy Orb Event Handlers
    // ============================================================================
    
    void OnSimpleEnergyOrbInsert(SimpleEnergyOrb orbData)
    {
        CreateSimpleEnergyOrbInWorld(orbData);
    }
    
    void OnSimpleEnergyOrbUpdate(SimpleEnergyOrb oldOrb, SimpleEnergyOrb newOrb)
    {
        // Update existing orb position/velocity
        if (activeSimpleOrbs.TryGetValue(newOrb.orb_id, out GameObject orbObj))
        {
            // Update position
            orbObj.transform.position = new Vector3(newOrb.position.x, newOrb.position.y, newOrb.position.z);
            
            // Update velocity if rigidbody exists
            var rb = orbObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = new Vector3(newOrb.velocity.x, newOrb.velocity.y, newOrb.velocity.z);
            }
        }
    }
    
    void OnSimpleEnergyOrbDelete(SimpleEnergyOrb orbData)
    {
        // Remove orb from world
        if (activeSimpleOrbs.TryGetValue(orbData.orb_id, out GameObject orbObj))
        {
            if (orbObj != null)
            {
                Destroy(orbObj);
            }
            activeSimpleOrbs.Remove(orbData.orb_id);
        }
    }
    
    // ============================================================================
    // Simple Energy Storage Event Handlers (for inventory)
    // ============================================================================
    
    void OnSimpleEnergyStorageInsert(SimpleEnergyStorage storageData)
    {
        // Handle new energy storage (player collected energy)
        if (IsCurrentPlayerStorage(storageData))
        {
            Debug.Log($"Player gained {storageData.quantum_count} {GetColorName(storageData.energy_signature.frequency)} energy");
            
            // Update UI or inventory display here
            UpdatePlayerEnergyDisplay();
        }
    }
    
    void OnSimpleEnergyStorageUpdate(SimpleEnergyStorage oldStorage, SimpleEnergyStorage newStorage)
    {
        // Handle energy amount changes
        if (IsCurrentPlayerStorage(newStorage))
        {
            Debug.Log($"Player energy updated: {newStorage.quantum_count} {GetColorName(newStorage.energy_signature.frequency)} energy");
            UpdatePlayerEnergyDisplay();
        }
    }
    
    void OnSimpleEnergyStorageDelete(SimpleEnergyStorage storageData)
    {
        // Handle energy removal
        if (IsCurrentPlayerStorage(storageData))
        {
            Debug.Log($"Player lost {GetColorName(storageData.energy_signature.frequency)} energy");
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
        Vector3 position = new Vector3(orbData.position.x, orbData.position.y, orbData.position.z);
        GameObject orbObj = Instantiate(simpleEnergyOrbPrefab, position, Quaternion.identity);
        
        // Set orb color based on frequency
        SetOrbColor(orbObj, orbData.energy_signature.frequency);
        
        // Set orb velocity
        var rb = orbObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = new Vector3(orbData.velocity.x, orbData.velocity.y, orbData.velocity.z);
        }
        
        // Store reference
        activeSimpleOrbs[orbData.orb_id] = orbObj;
        
        // Add collection script
        var collector = orbObj.GetComponent<SimpleEnergyOrbCollector>();
        if (collector == null)
        {
            collector = orbObj.AddComponent<SimpleEnergyOrbCollector>();
        }
        collector.Initialize(orbData.orb_id, orbData.quantum_count);
        
        Debug.Log($"Created {GetColorName(orbData.energy_signature.frequency)} energy orb (ID: {orbData.orb_id})");
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
        var currentPlayer = GameManager.GetCurrentPlayer(); // Adjust to your player system
        return storage.owner_type == "player" && 
               currentPlayer != null && 
               storage.owner_id == currentPlayer.player_id;
    }
    
    void UpdatePlayerEnergyDisplay()
    {
        // Update your UI here - this is a placeholder
        // You might want to update energy counters, inventory UI, etc.
        
        Debug.Log("Player energy display updated");
    }
    
    // ============================================================================
    // Public Interface for Testing
    // ============================================================================
    
    public void TestSpawnSimpleEnergyOrb()
    {
        // Test function to spawn an orb
        if (GameManager.IsConnected())
        {
            SpacetimeDBClient.CallReducer("debug_test_simple_energy_emission");
        }
    }
    
    public void CheckSimpleEnergyStatus()
    {
        // Test function to check system status
        if (GameManager.IsConnected())
        {
            SpacetimeDBClient.CallReducer("debug_simple_energy_status");
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
            var playerController = other.GetComponent<PlayerController>();
            if (playerController != null && playerController.isLocalPlayer)
            {
                CollectOrb(playerController);
            }
        }
    }
    
    void CollectOrb(PlayerController player)
    {
        // Call server to collect the orb
        if (GameManager.IsConnected())
        {
            SpacetimeDBClient.CallReducer("collect_simple_energy_orb", orbId, player.playerData.player_id);
            Debug.Log($"Collecting simple energy orb {orbId}");
        }
    }
}