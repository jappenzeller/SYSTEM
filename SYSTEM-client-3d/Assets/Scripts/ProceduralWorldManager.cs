using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;

public class ProceduralWorldManager : MonoBehaviour
{
    [Header("World Prefabs")]
    [Tooltip("Base world prefab for center world (shell 0)")]
    public GameObject centerWorldPrefab;
    
    [Tooltip("Base world prefab for shell 1 worlds")]
    public GameObject shell1WorldPrefab;
    
    [Tooltip("Base world prefab for shell 2 worlds")]
    public GameObject shell2WorldPrefab;
    
    [Header("Procedural Generation Settings")]
    [Tooltip("Distance between world centers")]
    public float worldSpacing = 300f;
    
    [Tooltip("Random seed for procedural generation")]
    public int proceduralSeed = 12345;
    
    [Header("Biome Materials")]
    public Material[] biomeMaterials;
    public Material[] atmosphereMaterials;
    
    [Header("Feature Prefabs")]
    public GameObject[] mountainPrefabs;
    public GameObject[] craterPrefabs;
    public GameObject[] crystalFormationPrefabs;
    
    // World management
    private Dictionary<string, GameObject> loadedWorlds = new Dictionary<string, GameObject>();
    private Dictionary<string, WorldData> worldDataCache = new Dictionary<string, WorldData>();
    
    // Procedural generation data
    [System.Serializable]
    public class WorldData
    {
        public WorldCoords coords;
        public int biomeType;
        public float terrainSeed;
        public List<FeatureData> features;
        public float radius;
        public int shellLevel;
    }
    
    [System.Serializable]
    public class FeatureData
    {
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
        public int featureType; // 0=mountain, 1=crater, 2=crystal, etc.
    }

    void Start()
    {
        // Initialize procedural system
        Random.InitState(proceduralSeed);
        
        // Subscribe to world events
        SubscribeToWorldEvents();
        
        // Generate center world immediately
        var centerCoords = new WorldCoords { X = 0, Y = 0, Z = 0 };
        LoadWorld(centerCoords);
    }

    void SubscribeToWorldEvents()
    {
        if (GameManager.Conn != null)
        {
            GameManager.Conn.Db.World.OnInsert += OnWorldActivated;
            GameManager.Conn.Db.World.OnUpdate += OnWorldUpdated;
            GameManager.Conn.Db.Tunnel.OnUpdate += OnTunnelUpdated;
        }
    }

    void OnWorldActivated(EventContext ctx, World world)
    {
        if (world.Status == "Active" && !loadedWorlds.ContainsKey(WorldCoordsToString(world.WorldCoords)))
        {
            LoadWorld(world.WorldCoords);
        }
    }

    void OnWorldUpdated(EventContext ctx, World oldWorld, World newWorld)
    {
        if (oldWorld.Status != "Active" && newWorld.Status == "Active")
        {
            LoadWorld(newWorld.WorldCoords);
        }
        else if (oldWorld.Status == "Active" && newWorld.Status != "Active")
        {
            UnloadWorld(newWorld.WorldCoords);
        }
    }

    void OnTunnelUpdated(EventContext ctx, Tunnel oldTunnel, Tunnel newTunnel)
    {
        // Load target world when tunnel becomes active
        if (oldTunnel.Status != "Active" && newTunnel.Status == "Active")
        {
            LoadWorld(newTunnel.ToWorld);
        }
    }

    public void LoadWorld(WorldCoords coords)
    {
        string worldKey = WorldCoordsToString(coords);
        
        if (loadedWorlds.ContainsKey(worldKey))
        {
            //Debug.Log(($"World {worldKey} already loaded");
            return;
        }

        // Generate or load world data
        WorldData worldData = GenerateWorldData(coords);
        worldDataCache[worldKey] = worldData;

        // Choose appropriate prefab
        GameObject worldPrefab = GetWorldPrefab(worldData.shellLevel);
        if (worldPrefab == null)
        {
            Debug.LogError($"No prefab found for shell level {worldData.shellLevel}");
            return;
        }

        // Calculate world position
        Vector3 worldPosition = CalculateWorldPosition(coords);

        // Instantiate world
        GameObject worldObj = Instantiate(worldPrefab, worldPosition, Quaternion.identity);
        worldObj.name = $"World_{worldKey}";

        // Apply procedural generation
        ApplyProceduralGeneration(worldObj, worldData);

        // Register world
        loadedWorlds[worldKey] = worldObj;

        //Debug.Log(($"Loaded procedural world {worldKey} at {worldPosition}");
    }

    public void UnloadWorld(WorldCoords coords)
    {
        string worldKey = WorldCoordsToString(coords);
        
        if (loadedWorlds.TryGetValue(worldKey, out GameObject worldObj))
        {
            loadedWorlds.Remove(worldKey);
            Destroy(worldObj);
            //Debug.Log(($"Unloaded world {worldKey}");
        }
    }

    WorldData GenerateWorldData(WorldCoords coords)
    {
        // Use coordinates as seed for consistent generation
        int seed = coords.X * 1000 + coords.Y * 100 + coords.Z;
        Random.State oldState = Random.state;
        Random.InitState(seed + proceduralSeed);

        WorldData data = new WorldData
        {
            coords = coords,
            shellLevel = CalculateShellLevel(coords),
            radius = CalculateWorldRadius(coords),
            biomeType = Random.Range(0, biomeMaterials.Length),
            terrainSeed = Random.value,
            features = new List<FeatureData>()
        };

        // Generate surface features
        GenerateFeatures(data);

        Random.state = oldState;
        return data;
    }

    void GenerateFeatures(WorldData worldData)
    {
        int featureCount = Random.Range(3, 8); // 3-7 features per world
        
        for (int i = 0; i < featureCount; i++)
        {
            // Random position on sphere surface
            Vector3 randomDir = Random.onUnitSphere;
            Vector3 position = randomDir * worldData.radius;

            FeatureData feature = new FeatureData
            {
                position = position,
                rotation = Random.rotation.eulerAngles,
                scale = Vector3.one * Random.Range(0.5f, 2f),
                featureType = Random.Range(0, 3) // 0=mountain, 1=crater, 2=crystal
            };

            worldData.features.Add(feature);
        }
    }

    void ApplyProceduralGeneration(GameObject worldObj, WorldData worldData)
    {
        // Get world controller
        var worldController = worldObj.GetComponent<ProceduralWorldController>();
        if (worldController != null)
        {
            worldController.Initialize(worldData);
        }

        // Apply basic properties
        Transform worldSphere = worldObj.transform.Find("WorldSphere");
        if (worldSphere != null)
        {
            // Set radius
            worldSphere.localScale = Vector3.one * worldData.radius * 2f;

            // Apply biome material
            var renderer = worldSphere.GetComponent<Renderer>();
            if (renderer != null && biomeMaterials.Length > 0)
            {
                renderer.material = biomeMaterials[worldData.biomeType % biomeMaterials.Length];
            }
        }

        // Generate surface features
        GenerateSurfaceFeatures(worldObj, worldData);

        // Apply atmospheric effects
        ApplyAtmosphere(worldObj, worldData);
    }

    void GenerateSurfaceFeatures(GameObject worldObj, WorldData worldData)
    {
        Transform featuresContainer = worldObj.transform.Find("Features");
        if (featuresContainer == null)
        {
            GameObject container = new GameObject("Features");
            container.transform.SetParent(worldObj.transform);
            featuresContainer = container.transform;
        }

        foreach (var feature in worldData.features)
        {
            GameObject featurePrefab = GetFeaturePrefab(feature.featureType);
            if (featurePrefab != null)
            {
                GameObject featureObj = Instantiate(featurePrefab, featuresContainer);
                featureObj.transform.localPosition = feature.position;
                featureObj.transform.localRotation = Quaternion.Euler(feature.rotation);
                featureObj.transform.localScale = feature.scale;

                // Orient to sphere surface
                Vector3 surfaceNormal = feature.position.normalized;
                featureObj.transform.LookAt(featureObj.transform.position + surfaceNormal);
            }
        }
    }

    void ApplyAtmosphere(GameObject worldObj, WorldData worldData)
    {
        Transform atmosphere = worldObj.transform.Find("Atmosphere");
        if (atmosphere != null && atmosphereMaterials.Length > 0)
        {
            var renderer = atmosphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                int atmosphereType = (worldData.biomeType + worldData.shellLevel) % atmosphereMaterials.Length;
                renderer.material = atmosphereMaterials[atmosphereType];
                
                // Slightly larger than world surface
                atmosphere.localScale = Vector3.one * (worldData.radius + 5f) * 2f;
            }
        }
    }

    GameObject GetWorldPrefab(int shellLevel)
    {
        return shellLevel switch
        {
            0 => centerWorldPrefab,
            1 => shell1WorldPrefab,
            2 => shell2WorldPrefab,
            _ => shell1WorldPrefab // Fallback
        };
    }

    GameObject GetFeaturePrefab(int featureType)
    {
        return featureType switch
        {
            0 => mountainPrefabs.Length > 0 ? mountainPrefabs[Random.Range(0, mountainPrefabs.Length)] : null,
            1 => craterPrefabs.Length > 0 ? craterPrefabs[Random.Range(0, craterPrefabs.Length)] : null,
            2 => crystalFormationPrefabs.Length > 0 ? crystalFormationPrefabs[Random.Range(0, crystalFormationPrefabs.Length)] : null,
            _ => null
        };
    }

    int CalculateShellLevel(WorldCoords coords)
    {
        int maxCoord = Mathf.Max(Mathf.Abs(coords.X), Mathf.Abs(coords.Y), Mathf.Abs(coords.Z));
        return maxCoord;
    }

    float CalculateWorldRadius(WorldCoords coords)
    {
        int shellLevel = CalculateShellLevel(coords);
        return shellLevel switch
        {
            0 => 100f, // Center world
            1 => 80f,  // Shell 1
            2 => 60f,  // Shell 2
            _ => 50f   // Outer shells
        };
    }

    Vector3 CalculateWorldPosition(WorldCoords coords)
    {
        return new Vector3(
            coords.X * worldSpacing,
            coords.Y * worldSpacing,
            coords.Z * worldSpacing
        );
    }

    string WorldCoordsToString(WorldCoords coords)
    {
        return $"{coords.X}_{coords.Y}_{coords.Z}";
    }

    // Public API for world management
    public bool IsWorldLoaded(WorldCoords coords)
    {
        return loadedWorlds.ContainsKey(WorldCoordsToString(coords));
    }

    public GameObject GetLoadedWorld(WorldCoords coords)
    {
        loadedWorlds.TryGetValue(WorldCoordsToString(coords), out GameObject world);
        return world;
    }

    public WorldData GetWorldData(WorldCoords coords)
    {
        worldDataCache.TryGetValue(WorldCoordsToString(coords), out WorldData data);
        return data;
    }

    // LOD System - load/unload worlds based on distance
    public void UpdateWorldLOD(Vector3 playerPosition)
    {
        // Calculate which worlds should be loaded based on player position
        WorldCoords playerWorldCoords = CalculateWorldCoords(playerPosition);
        
        // Load nearby worlds (current + adjacent)
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    WorldCoords nearbyCoords = new WorldCoords
                    {
                        X = (sbyte)(playerWorldCoords.X + x),
                        Y = (sbyte)(playerWorldCoords.Y + y),
                        Z = (sbyte)(playerWorldCoords.Z + z)
                    };
                    
                    if (!IsWorldLoaded(nearbyCoords))
                    {
                        LoadWorld(nearbyCoords);
                    }
                }
            }
        }
        
        // Unload distant worlds (optional, for memory management)
        var worldsToUnload = new List<string>();
        foreach (var kvp in loadedWorlds)
        {
            WorldCoords worldCoords = worldDataCache[kvp.Key].coords;
            float distance = Vector3.Distance(
                CalculateWorldPosition(worldCoords),
                playerPosition
            );
            
            if (distance > worldSpacing * 3f) // Unload if more than 3 world-spacings away
            {
                worldsToUnload.Add(kvp.Key);
            }
        }
        
        foreach (string worldKey in worldsToUnload)
        {
            var worldData = worldDataCache[worldKey];
            UnloadWorld(worldData.coords);
        }
    }

    WorldCoords CalculateWorldCoords(Vector3 position)
    {
        return new WorldCoords
        {
            X = (sbyte)Mathf.RoundToInt(position.x / worldSpacing),
            Y = (sbyte)Mathf.RoundToInt(position.y / worldSpacing),
            Z = (sbyte)Mathf.RoundToInt(position.z / worldSpacing)
        };
    }

    void OnDestroy()
    {
        // Clean up event subscriptions
        if (GameManager.Conn != null)
        {
            GameManager.Conn.Db.World.OnInsert -= OnWorldActivated;
            GameManager.Conn.Db.World.OnUpdate -= OnWorldUpdated;
            GameManager.Conn.Db.Tunnel.OnUpdate -= OnTunnelUpdated;
        }
    }

    // Debug visualization
    void OnDrawGizmosSelected()
    {
        // Draw world positions
        Gizmos.color = Color.yellow;
        foreach (var kvp in loadedWorlds)
        {
            if (worldDataCache.TryGetValue(kvp.Key, out WorldData data))
            {
                Vector3 worldPos = CalculateWorldPosition(data.coords);
                Gizmos.DrawWireSphere(worldPos, data.radius);
                
#if UNITY_EDITOR
                UnityEditor.Handles.Label(worldPos + Vector3.up * (data.radius + 10f), 
                    $"World {kvp.Key}\nShell: {data.shellLevel}\nBiome: {data.biomeType}");
#endif
            }
        }
    }
}