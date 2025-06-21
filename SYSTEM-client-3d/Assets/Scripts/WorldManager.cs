using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;
using System.Linq;

public class WorldManager : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("Prefab for energy puddles")]
    public GameObject energyPuddlePrefab;
    
    [Tooltip("Prefab for falling energy orbs")]
    public GameObject energyOrbPrefab;
    
    [Tooltip("Prefab for distribution spheres")]
    public GameObject distributionSpherePrefab;
    
    [Tooltip("Prefab for players")]
    public GameObject playerPrefab;

    [Header("World Prefabs")]
    [Tooltip("Prefab for the center world (0,0,0)")]
    public GameObject centerWorldPrefab;
    
    [Tooltip("Prefab for shell worlds (non-center)")]
    public GameObject shellWorldPrefab;
    
    [Header("Circuit Prefab")]
    [SerializeField] private GameObject worldCircuitPrefab;
    [SerializeField] private Vector3 circuitSpawnPosition = new Vector3(0f, 310f, 0f);

    [Header("Energy Materials")]
    [Tooltip("Materials for different energy types")]
    public Material redEnergyMaterial;
    public Material greenEnergyMaterial;
    public Material blueEnergyMaterial;
    public Material cyanEnergyMaterial;
    public Material magentaEnergyMaterial;
    public Material yellowEnergyMaterial;

    [Header("World Settings")]
    [Tooltip("Radius of the spherical world")]
    public float worldRadius = 300f;
    
    [Tooltip("How thick the world surface should be")]
    public float surfaceThickness = 2f;
    
    [Header("Update Settings")]
    [Tooltip("How often to check for table changes (seconds)")]
    public float updateInterval = 0.5f;

    // Private fields
    private GameObject worldSphere;
    private GameObject spawnedCircuit;
    private Dictionary<ulong, GameObject> energyPuddles = new Dictionary<ulong, GameObject>();
    private Dictionary<ulong, GameObject> energyOrbs = new Dictionary<ulong, GameObject>();
    private Dictionary<ulong, GameObject> distributionSpheres = new Dictionary<ulong, GameObject>();
    private Dictionary<uint, GameObject> playerObjects = new Dictionary<uint, GameObject>();
    
    // Tracking for updates
    private HashSet<ulong> knownPuddles = new HashSet<ulong>();
    private HashSet<ulong> knownOrbs = new HashSet<ulong>();
    private HashSet<ulong> knownSpheres = new HashSet<ulong>();
    private HashSet<uint> knownPlayers = new HashSet<uint>();
    private WorldCircuit currentCircuit = null;
    
    // Current world coordinates
    private WorldCoords currentWorldCoords;
    private bool isInitialized = false;
    private Coroutine updateCoroutine;

    void Start()
    {
        // Get current world from GameData
        if (GameData.Instance != null)
        {
            currentWorldCoords = GameData.Instance.GetCurrentWorldCoords();
            Debug.Log($"[WorldManager] Starting in world ({currentWorldCoords.X},{currentWorldCoords.Y},{currentWorldCoords.Z})");
            
            // Adapt world generation based on coordinates
            SetupWorldForCoordinates(currentWorldCoords);
        }
        else
        {
            Debug.LogError("[WorldManager] GameData.Instance is null! Cannot determine current world.");
            currentWorldCoords = new WorldCoords { X = 0, Y = 0, Z = 0 };
            SetupWorldForCoordinates(currentWorldCoords);
        }
        
        // Start checking for connection
        StartCoroutine(WaitForConnectionAndSubscribe());
        
        isInitialized = true;
    }

    void OnDestroy()
    {
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
        }
    }

    private IEnumerator WaitForConnectionAndSubscribe()
    {
        // Wait for connection to be established
        while (GameManager.Conn == null || !GameManager.Conn.IsActive)
        {
            yield return new WaitForSeconds(0.5f);
        }
        
        Debug.Log("[WorldManager] Connection established, setting up subscriptions");
        
        // Set up subscriptions for our world
        SetupSubscriptions();
        
        // Start periodic update checks
        updateCoroutine = StartCoroutine(PeriodicUpdateCheck());
    }

    private void SetupSubscriptions()
    {
        // Subscribe to all relevant tables for this world
        // Note: SpacetimeDB SQL uses = for comparison, not ==
        var queries = new List<string>
        {
            $"SELECT * FROM WorldCircuit WHERE world_coords = {{{currentWorldCoords.X}, {currentWorldCoords.Y}, {currentWorldCoords.Z}}}",
            $"SELECT * FROM EnergyPuddle WHERE world_coords = {{{currentWorldCoords.X}, {currentWorldCoords.Y}, {currentWorldCoords.Z}}}",
            $"SELECT * FROM EnergyOrb WHERE world_coords = {{{currentWorldCoords.X}, {currentWorldCoords.Y}, {currentWorldCoords.Z}}}",
            $"SELECT * FROM DistributionSphere WHERE world_coords = {{{currentWorldCoords.X}, {currentWorldCoords.Y}, {currentWorldCoords.Z}}}",
            $"SELECT * FROM Player WHERE world_coords = {{{currentWorldCoords.X}, {currentWorldCoords.Y}, {currentWorldCoords.Z}}}"
        };
        
        GameManager.Conn.SubscriptionBuilder()
            .OnApplied(_ => {
                Debug.Log("[WorldManager] Subscriptions applied successfully");
                // Do initial sync of all data
                SyncAllWorldData();
            })
            .Subscribe(queries.ToArray());
    }

    private IEnumerator PeriodicUpdateCheck()
    {
        while (true)
        {
            yield return new WaitForSeconds(updateInterval);
            
            if (GameManager.Conn != null && GameManager.Conn.IsActive)
            {
                SyncAllWorldData();
            }
        }
    }

    private void SyncAllWorldData()
    {
        // Sync World Circuit
        SyncWorldCircuit();
        
        // Sync Energy Puddles
        SyncEnergyPuddles();
        
        // Sync Energy Orbs
        SyncEnergyOrbs();
        
        // Sync Distribution Spheres
        SyncDistributionSpheres();
        
        // Sync Players
        SyncPlayers();
    }

    private void SyncWorldCircuit()
    {
        // Try getting all rows from the table
        var allCircuits = GameManager.Conn.Db.WorldCircuit.Iter(); // or .GetAll() or .All
        
        foreach (var circuit in allCircuits)
        {
            if (circuit.WorldCoords.X == currentWorldCoords.X && 
                circuit.WorldCoords.Y == currentWorldCoords.Y && 
                circuit.WorldCoords.Z == currentWorldCoords.Z)
            {
                if (currentCircuit == null || !CircuitEquals(currentCircuit, circuit))
                {
                    // Circuit is new or changed
                    if (spawnedCircuit == null)
                    {
                        Debug.Log($"[WorldManager] Spawning new world circuit with {circuit.QubitCount} qubits");
                        SpawnWorldCircuit(circuit);
                    }
                    else
                    {
                        // Update existing circuit
                        var controller = spawnedCircuit.GetComponent<WorldCircuitController>();
                        if (controller != null)
                        {
                            controller.UpdateCircuit(circuit);
                        }
                    }
                    currentCircuit = circuit;
                }
                break; // Only one circuit per world
            }
        }
        
        // Check if circuit was deleted
        if (currentCircuit != null && spawnedCircuit != null)
        {
            bool found = false;
            foreach (var circuit in GameManager.Conn.Db.WorldCircuit.Iter())
            {
                if (circuit.WorldCoords.X == currentWorldCoords.X && 
                    circuit.WorldCoords.Y == currentWorldCoords.Y && 
                    circuit.WorldCoords.Z == currentWorldCoords.Z)
                {
                    found = true;
                    break;
                }
            }
            
            if (!found)
            {
                Debug.Log("[WorldManager] World circuit was deleted");
                Destroy(spawnedCircuit);
                spawnedCircuit = null;
                currentCircuit = null;
            }
        }
    }

    private bool CircuitEquals(WorldCircuit a, WorldCircuit b)
    {
        return a.QubitCount == b.QubitCount &&
               a.EmissionIntervalMs == b.EmissionIntervalMs &&
               a.OrbsPerEmission == b.OrbsPerEmission;
    }

    private void SyncEnergyPuddles()
    {
        var currentPuddleIds = new HashSet<ulong>();
        
        // Iterate through all puddles and filter manually
        foreach (var puddle in GameManager.Conn.Db.EnergyPuddle.Iter())
        {
            if (puddle.WorldCoords.X == currentWorldCoords.X && 
                puddle.WorldCoords.Y == currentWorldCoords.Y && 
                puddle.WorldCoords.Z == currentWorldCoords.Z)
            {
                currentPuddleIds.Add(puddle.PuddleId);
                
                if (!knownPuddles.Contains(puddle.PuddleId))
                {
                    SpawnEnergyPuddle(puddle);
                    knownPuddles.Add(puddle.PuddleId);
                }
                else if (energyPuddles.TryGetValue(puddle.PuddleId, out GameObject puddleObj))
                {
                    // Update existing puddle position if it moved
                    puddleObj.transform.position = new Vector3(puddle.Position.X, puddle.Position.Y, puddle.Position.Z);
                }
            }
        }
        
        // Remove deleted puddles
        var toRemove = knownPuddles.Where(id => !currentPuddleIds.Contains(id)).ToList();
        foreach (var puddleId in toRemove)
        {
            if (energyPuddles.TryGetValue(puddleId, out GameObject puddleObj))
            {
                Destroy(puddleObj);
                energyPuddles.Remove(puddleId);
            }
            knownPuddles.Remove(puddleId);
        }
    }

    private void SyncEnergyOrbs()
    {
        var currentOrbIds = new HashSet<ulong>();
        
        foreach (var orb in GameManager.Conn.Db.EnergyOrb.Iter())
        {
            if (orb.WorldCoords.X == currentWorldCoords.X && 
                orb.WorldCoords.Y == currentWorldCoords.Y && 
                orb.WorldCoords.Z == currentWorldCoords.Z)
            {
                currentOrbIds.Add(orb.OrbId);
                
                if (!knownOrbs.Contains(orb.OrbId))
                {
                    SpawnEnergyOrb(orb);
                    knownOrbs.Add(orb.OrbId);
                }
                else if (energyOrbs.TryGetValue(orb.OrbId, out GameObject orbObj))
                {
                    // Update existing orb position
                    orbObj.transform.position = new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z);
                    
                    var rb = orbObj.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = new Vector3(orb.Velocity.X, orb.Velocity.Y, orb.Velocity.Z);
                    }
                }
            }
        }
        
        // Remove deleted orbs
        var toRemove = knownOrbs.Where(id => !currentOrbIds.Contains(id)).ToList();
        foreach (var orbId in toRemove)
        {
            if (energyOrbs.TryGetValue(orbId, out GameObject orbObj))
            {
                Destroy(orbObj);
                energyOrbs.Remove(orbId);
            }
            knownOrbs.Remove(orbId);
        }
    }

    private void SyncDistributionSpheres()
    {
        var currentSphereIds = new HashSet<ulong>();
        
        foreach (var sphere in GameManager.Conn.Db.DistributionSphere.Iter())
        {
            if (sphere.WorldCoords.X == currentWorldCoords.X && 
                sphere.WorldCoords.Y == currentWorldCoords.Y && 
                sphere.WorldCoords.Z == currentWorldCoords.Z)
            {
                currentSphereIds.Add(sphere.SphereId);
                
                if (!knownSpheres.Contains(sphere.SphereId))
                {
                    SpawnDistributionSphere(sphere);
                    knownSpheres.Add(sphere.SphereId);
                }
                else if (distributionSpheres.TryGetValue(sphere.SphereId, out GameObject sphereObj))
                {
                    // Update existing sphere position if it moved
                    sphereObj.transform.position = new Vector3(sphere.Position.X, sphere.Position.Y, sphere.Position.Z);
                }
            }
        }
        
        // Remove deleted spheres
        var toRemove = knownSpheres.Where(id => !currentSphereIds.Contains(id)).ToList();
        foreach (var sphereId in toRemove)
        {
            if (distributionSpheres.TryGetValue(sphereId, out GameObject sphereObj))
            {
                Destroy(sphereObj);
                distributionSpheres.Remove(sphereId);
            }
            knownSpheres.Remove(sphereId);
        }
    }

    private void SyncPlayers()
    {
        var currentPlayerIds = new HashSet<uint>();
        
        foreach (var player in GameManager.Conn.Db.Player.Iter())
        {
            if (player.CurrentWorld.X == currentWorldCoords.X && 
                player.CurrentWorld.Y == currentWorldCoords.Y && 
                player.CurrentWorld.Z == currentWorldCoords.Z &&
                player.Identity != GameManager.LocalIdentity)
            {
                currentPlayerIds.Add(player.PlayerId);
                
                if (!knownPlayers.Contains(player.PlayerId))
                {
                    SpawnPlayer(player);
                    knownPlayers.Add(player.PlayerId);
                }
                else if (playerObjects.TryGetValue(player.PlayerId, out GameObject playerObj))
                {
                    // Update existing player
                    playerObj.transform.position = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);
                    playerObj.transform.rotation = new Quaternion(player.Rotation.X, player.Rotation.Y, player.Rotation.Z, player.Rotation.W);
                }
            }
        }
        
        // Remove disconnected players
        var toRemove = knownPlayers.Where(id => !currentPlayerIds.Contains(id)).ToList();
        foreach (var playerId in toRemove)
        {
            if (playerObjects.TryGetValue(playerId, out GameObject playerObj))
            {
                Destroy(playerObj);
                playerObjects.Remove(playerId);
            }
            knownPlayers.Remove(playerId);
        }
    }

    // Spawn methods
    private void SpawnWorldCircuit(WorldCircuit circuit)
    {
        if (worldCircuitPrefab == null)
        {
            Debug.LogWarning("[WorldManager] World Circuit Prefab is not assigned!");
            return;
        }
        
        if (spawnedCircuit != null)
        {
            Destroy(spawnedCircuit);
        }
        
        spawnedCircuit = Instantiate(worldCircuitPrefab, circuitSpawnPosition, Quaternion.identity);
        spawnedCircuit.name = $"WorldCircuit_{circuit.WorldCoords.X}_{circuit.WorldCoords.Y}_{circuit.WorldCoords.Z}";
        
        var controller = spawnedCircuit.GetComponent<WorldCircuitController>();
        if (controller != null)
        {
            controller.Initialize(circuit);
        }
        
        Debug.Log($"[WorldManager] Spawned World Circuit at {circuitSpawnPosition} with {circuit.QubitCount} qubits");
        Debug.Log($"[WorldManager] Circuit emits {circuit.OrbsPerEmission} orbs every {circuit.EmissionIntervalMs}ms");
    }

    private void SpawnEnergyPuddle(EnergyPuddle puddle)
    {
        if (energyPuddlePrefab == null) return;
        
        Vector3 pos = new Vector3(puddle.Position.X, puddle.Position.Y, puddle.Position.Z);
        GameObject puddleObj = Instantiate(energyPuddlePrefab, pos, Quaternion.identity);
        puddleObj.name = $"EnergyPuddle_{puddle.PuddleId}";
        
        SetEnergyMaterial(puddleObj, puddle.EnergyType);
        
        energyPuddles[puddle.PuddleId] = puddleObj;
    }

    private void SpawnEnergyOrb(EnergyOrb orb)
    {
        if (energyOrbPrefab == null) return;
        
        Vector3 pos = new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z);
        GameObject orbObj = Instantiate(energyOrbPrefab, pos, Quaternion.identity);
        orbObj.name = $"EnergyOrb_{orb.OrbId}";
        
        SetEnergyMaterial(orbObj, orb.EnergyType);
        
        var rb = orbObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = new Vector3(orb.Velocity.X, orb.Velocity.Y, orb.Velocity.Z);
        }
        
        energyOrbs[orb.OrbId] = orbObj;
    }

    private void SpawnDistributionSphere(DistributionSphere sphere)
    {
        if (distributionSpherePrefab == null) return;
        
        Vector3 pos = new Vector3(sphere.Position.X, sphere.Position.Y, sphere.Position.Z);
        GameObject sphereObj = Instantiate(distributionSpherePrefab, pos, Quaternion.identity);
        sphereObj.name = $"DistributionSphere_{sphere.SphereId}";
        
        distributionSpheres[sphere.SphereId] = sphereObj;
    }

    private void SpawnPlayer(Player player)
    {
        if (playerPrefab == null) return;
        
        Vector3 pos = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);
        GameObject playerObj = Instantiate(playerPrefab, pos, Quaternion.identity);
        
        playerObj.transform.rotation = new Quaternion(player.Rotation.X, player.Rotation.Y, player.Rotation.Z, player.Rotation.W);
        playerObj.name = $"Player_{player.Name}";
        
        playerObjects[player.PlayerId] = playerObj;
    }

    // Helper methods
    void SetupWorldForCoordinates(WorldCoords coords)
    {
        bool isCenterWorld = (coords.X == 0 && coords.Y == 0 && coords.Z == 0);
        
        GameObject worldPrefab = isCenterWorld ? centerWorldPrefab : shellWorldPrefab;
        
        if (worldPrefab != null)
        {
            worldSphere = Instantiate(worldPrefab, Vector3.zero, Quaternion.identity);
            worldSphere.name = $"World_{coords.X}_{coords.Y}_{coords.Z}";
        }
        else
        {
            CreateBasicWorldSphere(isCenterWorld);
        }
        
        worldRadius = isCenterWorld ? 300f : 250f;
        
        CustomizeWorldAppearance(coords);
    }

    void CreateBasicWorldSphere(bool isCenterWorld)
    {
        worldSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        worldSphere.name = $"World_{currentWorldCoords.X}_{currentWorldCoords.Y}_{currentWorldCoords.Z}";
        worldSphere.transform.localScale = Vector3.one * worldRadius * 2f;
        
        var renderer = worldSphere.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = isCenterWorld ? Color.cyan : Color.gray;
        }
    }

    void CustomizeWorldAppearance(WorldCoords coords)
    {
        int shellLevel = Mathf.Max(Mathf.Abs(coords.X), Mathf.Abs(coords.Y), Mathf.Abs(coords.Z));
        
        if (worldSphere != null)
        {
            var renderer = worldSphere.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                float brightness = 1f - (shellLevel * 0.1f);
                brightness = Mathf.Clamp(brightness, 0.3f, 1f);
                renderer.material.color *= brightness;
            }
        }
    }

    void SetEnergyMaterial(GameObject obj, EnergyType energyType)
    {
        var renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return;
        
        Material material = energyType switch
        {
            EnergyType.Red => redEnergyMaterial,
            EnergyType.Green => greenEnergyMaterial,
            EnergyType.Blue => blueEnergyMaterial,
            EnergyType.Cyan => cyanEnergyMaterial,
            EnergyType.Magenta => magentaEnergyMaterial,
            EnergyType.Yellow => yellowEnergyMaterial,
            _ => redEnergyMaterial
        };
        
        if (material != null)
        {
            renderer.material = material;
        }
    }

    // Public API
    public GameObject GetSpawnedCircuit()
    {
        return spawnedCircuit;
    }
}