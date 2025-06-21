using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;

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
    public float worldRadius = 300f; // Default, will be overridden by SetupWorldForCoordinates
    
    [Tooltip("How thick the world surface should be")]
    public float surfaceThickness = 2f;

    // Private fields
    private GameObject worldSphere;
    private Dictionary<ulong, GameObject> energyPuddles = new Dictionary<ulong, GameObject>();
    private Dictionary<ulong, GameObject> energyOrbs = new Dictionary<ulong, GameObject>();
    private Dictionary<ulong, GameObject> distributionSpheres = new Dictionary<ulong, GameObject>();
    private Dictionary<uint, GameObject> playerObjects = new Dictionary<uint, GameObject>();
    
    // Current world coordinates
    private WorldCoords currentWorldCoords;
    private bool isInitialized = false;

    void Start()
    {
        // Get current world from GameData
        if (GameData.Instance != null)
        {
            currentWorldCoords = GameData.Instance.GetCurrentWorldCoords();
            Debug.Log($"WorldManager starting in world ({currentWorldCoords.X},{currentWorldCoords.Y},{currentWorldCoords.Z})");
            
            // Adapt world generation based on coordinates
            SetupWorldForCoordinates(currentWorldCoords);
        }
        else
        {
            Debug.LogError("GameData.Instance is null! Cannot determine current world.");
        }
        
        // Validate prefabs
        ValidatePrefabs();
        
        // Subscribe to SpacetimeDB events
        SubscribeToWorldEvents();
        
        isInitialized = true;
    }

    void SetupWorldForCoordinates(WorldCoords coords)
    {
        // Set radius based on world type
        if (IsCenter(coords))
        {
            worldRadius = 300f; // Center world radius
        }
        else
        {
            worldRadius = 80f; // Shell world radius
        }
        
        // Create the world
        CreateWorldSphere();
    }

    bool IsCenter(WorldCoords coords)
    {
        return coords.X == 0 && coords.Y == 0 && coords.Z == 0;
    }

    void ValidatePrefabs()
    {
        if (energyPuddlePrefab == null)
            Debug.LogError("Energy Puddle Prefab is not assigned in WorldManager!");
        
        if (energyOrbPrefab == null)
            Debug.LogError("Energy Orb Prefab is not assigned in WorldManager!");
        
        if (distributionSpherePrefab == null)
            Debug.LogError("Distribution Sphere Prefab is not assigned in WorldManager!");
        
        if (playerPrefab == null)
            Debug.LogError("Player Prefab is not assigned in WorldManager!");
            
        // Validate world prefabs
        if (centerWorldPrefab == null && IsCenter(currentWorldCoords))
            Debug.LogWarning("Center World Prefab is not assigned! Will fall back to primitive sphere.");
            
        if (shellWorldPrefab == null && !IsCenter(currentWorldCoords))
            Debug.LogWarning("Shell World Prefab is not assigned! Will fall back to primitive sphere.");
    }

    void CreateWorldSphere()
    {
        GameObject prefabToUse = null;
        
        // Determine which prefab to use based on world coordinates
        if (IsCenter(currentWorldCoords))
        {
            prefabToUse = centerWorldPrefab;
            Debug.Log("Using center world prefab.");
        }
        else
        {
            prefabToUse = shellWorldPrefab;
            Debug.Log("Using shell world prefab.");
        }
        
        // If we have a prefab, instantiate it
        if (prefabToUse != null)
        {
            worldSphere = Instantiate(prefabToUse, Vector3.zero, Quaternion.identity);
            worldSphere.name = IsCenter(currentWorldCoords) ? "Center World" : $"Shell World ({currentWorldCoords.X},{currentWorldCoords.Y},{currentWorldCoords.Z})";
            
            // Calculate scale based on mesh bounds, not renderer bounds
            // The renderer includes visual effects that extend beyond the actual surface
            float scaleFactor = 14.87f; // Default based on testing
            
            MeshFilter meshFilter = worldSphere.GetComponentInChildren<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Bounds meshBounds = meshFilter.sharedMesh.bounds;
                float meshRadius = Mathf.Max(meshBounds.extents.x, meshBounds.extents.y, meshBounds.extents.z);
                
                if (meshRadius > 0.01f)
                {
                    scaleFactor = worldRadius / meshRadius;
                    Debug.Log($"Mesh radius: {meshRadius}, target radius: {worldRadius}");
                    Debug.Log($"Calculated scale factor from mesh: {scaleFactor}");
                }
            }
            else
            {
                Debug.LogWarning("Could not find mesh, using tested scale factor of 14.87");
            }
            
            worldSphere.transform.localScale = Vector3.one * scaleFactor;
            
            // Verify the result
            var renderer = worldSphere.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                // Note: renderer bounds will be larger than the actual surface due to effects
                float rendererRadius = Mathf.Max(renderer.bounds.extents.x, renderer.bounds.extents.y, renderer.bounds.extents.z);
                Debug.Log($"Applied scale: {scaleFactor}");
                Debug.Log($"Renderer radius (includes effects): {rendererRadius}");
                Debug.Log($"Actual surface radius: ~{worldRadius}");
            }
        }
        else
        {
            // Fallback to primitive sphere if no prefab is assigned
            Debug.LogWarning("No world prefab assigned, falling back to primitive sphere.");
            
            worldSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            worldSphere.name = "World Sphere (Fallback)";
            worldSphere.transform.position = Vector3.zero;
            worldSphere.transform.localScale = Vector3.one * worldRadius * 2f; // Scale is diameter
            
            // Remove collider since we'll handle sphere physics ourselves
            var collider = worldSphere.GetComponent<SphereCollider>();
            if (collider != null)
            {
                DestroyImmediate(collider);
            }
        }

        Debug.Log($"Created world sphere with radius {worldRadius}");
    }

    void SubscribeToWorldEvents()
    {
        if (GameManager.Conn == null)
        {
            Debug.LogError("GameManager.Conn is null! Cannot subscribe to events.");
            return;
        }

        // Subscribe to energy puddle events
        GameManager.Conn.Db.EnergyPuddle.OnInsert += OnEnergyPuddleCreated;
        GameManager.Conn.Db.EnergyPuddle.OnDelete += OnEnergyPuddleDeleted;
        
        // Subscribe to energy orb events
        GameManager.Conn.Db.EnergyOrb.OnInsert += OnEnergyOrbCreated;
        GameManager.Conn.Db.EnergyOrb.OnUpdate += OnEnergyOrbUpdated;
        GameManager.Conn.Db.EnergyOrb.OnDelete += OnEnergyOrbDeleted;
        
        // Subscribe to distribution sphere events
        GameManager.Conn.Db.DistributionSphere.OnInsert += OnDistributionSphereCreated;
        GameManager.Conn.Db.DistributionSphere.OnDelete += OnDistributionSphereDeleted;
        
        // Subscribe to player events
        GameManager.Conn.Db.Player.OnInsert += OnPlayerJoined;
        GameManager.Conn.Db.Player.OnUpdate += OnPlayerUpdated;
        GameManager.Conn.Db.Player.OnDelete += OnPlayerLeft;
        
        Debug.Log("Subscribed to world events");
        
        // Load initial world state if connected
        RefreshWorldState();
    }

    void RefreshWorldState()
    {
        if (GameManager.Conn == null || GameManager.Conn.Db == null) return;
        
        Debug.Log("Loading existing world state from server...");
        
        // Load existing energy puddles
        foreach (var puddle in GameManager.Conn.Db.EnergyPuddle.Iter())
        {
            if (IsInCurrentWorld(puddle.WorldCoords))
            {
                CreateEnergyPuddleObject(puddle);
            }
        }
        
        // Load existing energy orbs
        foreach (var orb in GameManager.Conn.Db.EnergyOrb.Iter())
        {
            if (IsInCurrentWorld(orb.WorldCoords))
            {
                CreateEnergyOrbObject(orb);
            }
        }
        
        // Load existing distribution spheres
        foreach (var sphere in GameManager.Conn.Db.DistributionSphere.Iter())
        {
            if (IsInCurrentWorld(sphere.WorldCoords))
            {
                CreateDistributionSphereObject(sphere);
            }
        }
        
        // Load existing players
        foreach (var player in GameManager.Conn.Db.Player.Iter())
        {
            if (IsInCurrentWorld(player.CurrentWorld))
            {
                CreatePlayerObject(player, "RefreshWorldState");
            }
        }
        
        Debug.Log($"World state loaded: {energyPuddles.Count} puddles, {energyOrbs.Count} orbs, {distributionSpheres.Count} spheres, {playerObjects.Count} players");
    }

    bool IsInCurrentWorld(WorldCoords coords)
    {
        return coords.X == currentWorldCoords.X && 
               coords.Y == currentWorldCoords.Y && 
               coords.Z == currentWorldCoords.Z;
    }

    // Coordinate conversion helpers
    Vector3 DbVectorToUnity(DbVector3 dbVector)
    {
        return new Vector3(dbVector.X, dbVector.Y, dbVector.Z);
    }

    DbVector3 UnityToDbVector(Vector3 unityVector)
    {
        return new DbVector3 { X = unityVector.x, Y = unityVector.y, Z = unityVector.z };
    }

    // Energy Puddle Event Handlers
    void OnEnergyPuddleCreated(EventContext ctx, EnergyPuddle puddle)
    {
        if (!IsInCurrentWorld(puddle.WorldCoords)) return;
        CreateEnergyPuddleObject(puddle);
    }

    void CreateEnergyPuddleObject(EnergyPuddle puddle)
    {
        if (energyPuddlePrefab == null) return;

        Vector3 position = DbVectorToUnity(puddle.Position);
        
        GameObject puddleObj = Instantiate(energyPuddlePrefab, position, Quaternion.identity);
        puddleObj.name = $"Energy Puddle {puddle.PuddleId}";
        
        // Set puddle properties
        var puddleScript = puddleObj.GetComponent<EnergyPuddleController>();
        if (puddleScript != null)
        {
            puddleScript.Initialize(puddle, GetEnergyMaterial(puddle.EnergyType));
        }
        else
        {
            // Fallback: just apply energy type visuals
            ApplyEnergyMaterial(puddleObj, puddle.EnergyType);
        }
        
        energyPuddles[puddle.PuddleId] = puddleObj;
    }

    void OnEnergyPuddleDeleted(EventContext ctx, EnergyPuddle puddle)
    {
        if (energyPuddles.TryGetValue(puddle.PuddleId, out GameObject puddleObj))
        {
            energyPuddles.Remove(puddle.PuddleId);
            Destroy(puddleObj);
        }
    }

    // Energy Orb Event Handlers
    void OnEnergyOrbCreated(EventContext ctx, EnergyOrb orb)
    {
        if (!IsInCurrentWorld(orb.WorldCoords)) return;
        CreateEnergyOrbObject(orb);
    }

    void CreateEnergyOrbObject(EnergyOrb orb)
    {
        if (energyOrbPrefab == null) return;

        Vector3 position = DbVectorToUnity(orb.Position);
        
        GameObject orbObj = Instantiate(energyOrbPrefab, position, Quaternion.identity);
        orbObj.name = $"Energy Orb {orb.OrbId}";
        
        // Set orb properties
        var orbScript = orbObj.GetComponent<EnergyOrbController>();
        if (orbScript != null)
        {
            orbScript.Initialize(orb, GetEnergyMaterial(orb.EnergyType), worldRadius);
        }
        else
        {
            // Fallback: just apply energy type visuals
            ApplyEnergyMaterial(orbObj, orb.EnergyType);
        }
        
        energyOrbs[orb.OrbId] = orbObj;
    }

    void OnEnergyOrbUpdated(EventContext ctx, EnergyOrb oldOrb, EnergyOrb newOrb)
    {
        if (!IsInCurrentWorld(newOrb.WorldCoords)) return;
        
        if (energyOrbs.TryGetValue(newOrb.OrbId, out GameObject orbObj))
        {
            // Update position
            orbObj.transform.position = DbVectorToUnity(newOrb.Position);
            
            // Update orb data in controller
            var orbScript = orbObj.GetComponent<EnergyOrbController>();
            if (orbScript != null)
            {
                orbScript.UpdateData(newOrb);
            }
        }
    }

    void OnEnergyOrbDeleted(EventContext ctx, EnergyOrb orb)
    {
        if (energyOrbs.TryGetValue(orb.OrbId, out GameObject orbObj))
        {
            energyOrbs.Remove(orb.OrbId);
            Destroy(orbObj);
        }
    }

    // Distribution Sphere Event Handlers
    void OnDistributionSphereCreated(EventContext ctx, DistributionSphere sphere)
    {
        if (!IsInCurrentWorld(sphere.WorldCoords)) return;
        CreateDistributionSphereObject(sphere);
    }

    void CreateDistributionSphereObject(DistributionSphere sphere)
    {
        if (distributionSpherePrefab == null) return;

        Vector3 position = DbVectorToUnity(sphere.Position);
        
        GameObject sphereObj = Instantiate(distributionSpherePrefab, position, Quaternion.identity);
        sphereObj.name = $"Distribution Sphere {sphere.SphereId}";
        
        // Configure the sphere using its script component
        var sphereScript = sphereObj.GetComponent<DistributionSphereController>();
        if (sphereScript != null)
        {
            sphereScript.Initialize(sphere);
        }
        else
        {
            // Fallback configuration
            float scale = sphere.CoverageRadius / 50f; // Normalize for visual size
            sphereObj.transform.localScale = Vector3.one * scale;
            
            // Make it semi-transparent and glowing
            var renderer = sphereObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.5f, 0.8f, 1.0f, 0.3f); // Light blue, transparent
                renderer.material.EnableKeyword("_EMISSION");
            }
        }
        
        distributionSpheres[sphere.SphereId] = sphereObj;
        
    //    Debug.Log($"Created distribution sphere {sphere.SphereId} at {position} with radius {sphere.CoverageRadius}");
    }

    void OnDistributionSphereDeleted(EventContext ctx, DistributionSphere sphere)
    {
        if (distributionSpheres.TryGetValue(sphere.SphereId, out GameObject sphereObj))
        {
            distributionSpheres.Remove(sphere.SphereId);
            Destroy(sphereObj);
        }
    }

    // Player Event Handlers
    void OnPlayerJoined(EventContext ctx, Player player)
    {
        if (!IsInCurrentWorld(player.CurrentWorld)) return;
        Debug.Log($"[WorldManager.OnPlayerJoined] Player {player.Name} (ID: {player.PlayerId}) joined this world. Attempting to create object.");
        CreatePlayerObject(player, "OnPlayerJoined");
    }

    void CreatePlayerObject(Player player, string callSite)
    {
        Debug.Log($"[WorldManager.CreatePlayerObject CALLED FROM: {callSite}] For player {player.Name} (ID: {player.PlayerId})");

        // Don't create duplicate players
        if (playerObjects.ContainsKey(player.PlayerId))
        {
            GameObject existingPlayerObj = playerObjects[player.PlayerId];
            if (existingPlayerObj != null) // Check if the GameObject reference is valid (not destroyed)
            {
                Debug.LogWarning($"[WorldManager.CreatePlayerObject FROM: {callSite}] Player object for {player.Name} (ID: {player.PlayerId}) already in dictionary. Existing Object Name: {existingPlayerObj.name}, InstanceID: {existingPlayerObj.GetInstanceID()}, IsNull: {existingPlayerObj == null}. Skipping creation.");
                return;
            }
            else
            {
                 // The dictionary had an entry, but the GameObject was destroyed. Clean it up.
                Debug.LogWarning($"[WorldManager.CreatePlayerObject FROM: {callSite}] Found destroyed player object for {player.Name} (ID: {player.PlayerId}) in dictionary. Cleaning up.");
                playerObjects.Remove(player.PlayerId);
            }
        }

        if (playerPrefab == null) return;

        Vector3 position = DbVectorToUnity(player.Position);
        
        // Align rotation with planet normal
        Vector3 upDirection = position.normalized;
        Quaternion rotation = Quaternion.LookRotation(Vector3.forward, upDirection);
        
        GameObject playerObj = Instantiate(playerPrefab, position, rotation);
        playerObj.name = $"Player_{player.Name}";
        
        // Configure player controller
        var playerScript = playerObj.GetComponent<PlayerController>();
        if (playerScript != null)
        {
            bool isLocal = GameManager.Conn != null && GameManager.Conn.Identity == player.Identity;
            playerScript.Initialize(player, isLocal, worldRadius);
        }
        
        // Check if this is the local player
        if (GameManager.Conn != null && GameManager.Conn.Identity == player.Identity)
        {
            // This is our local player
            playerObj.tag = "Player";
        }
        
        playerObjects[player.PlayerId] = playerObj;
        Debug.Log($"[WorldManager.CreatePlayerObject FROM: {callSite}] Created player object for {player.Name} (ID: {player.PlayerId}, InstanceID: {playerObj.GetInstanceID()}).");
    }

    void OnPlayerUpdated(EventContext ctx, Player oldPlayer, Player newPlayer)
    {
        if (!IsInCurrentWorld(newPlayer.CurrentWorld))
        {
            // Player moved to different world, remove from this world
            if (playerObjects.TryGetValue(newPlayer.PlayerId, out GameObject leavingPlayerObj))
            {
                Debug.Log($"[WorldManager.OnPlayerUpdated] Player {newPlayer.Name} left this world");
                playerObjects.Remove(newPlayer.PlayerId);
                Destroy(leavingPlayerObj);
            }
            return;
        }
        
        if (playerObjects.TryGetValue(newPlayer.PlayerId, out GameObject playerObj))
        {
            // Update player data
            var playerScript = playerObj.GetComponent<PlayerController>();
            if (playerScript != null)
            {
                // If this is NOT the local player, update the transform
                bool isLocalPlayer = GameManager.Conn != null && GameManager.Conn.Identity == newPlayer.Identity;
                if (!isLocalPlayer)
                {
                    // PlayerController.UpdateData will handle smooth interpolation to the exact server rotation.
                    Vector3 newPosition = DbVectorToUnity(newPlayer.Position);
                    playerObj.transform.position = newPosition;
                    playerObj.transform.LookAt(Vector3.zero); // Orient towards center
                    playerObj.transform.Rotate(-90f, 0f, 0f); // Stand upright
                }
                // For the local player, we do not set transform.position or transform.rotation here.
                // PlayerController.HandleMovementAndRotation is authoritative for local player.
                playerScript.UpdateData(newPlayer, this.worldRadius);
            }
        }
    }

    void OnPlayerLeft(EventContext ctx, Player player)
    {
        if (playerObjects.TryGetValue(player.PlayerId, out GameObject playerObj))
        {
            Debug.Log($"[WorldManager.OnPlayerLeft] Removing player {player.Name} (ID: {player.PlayerId}, InstanceID: {playerObj?.GetInstanceID()}) from playerObjects. Destroying GameObject.");
            playerObjects.Remove(player.PlayerId);
            Destroy(playerObj);
        }
    }

    // Helper method to get energy material
    Material GetEnergyMaterial(EnergyType energyType)
    {
        return energyType switch
        {
            EnergyType.Red => redEnergyMaterial,
            EnergyType.Green => greenEnergyMaterial,
            EnergyType.Blue => blueEnergyMaterial,
            EnergyType.Cyan => cyanEnergyMaterial,
            EnergyType.Magenta => magentaEnergyMaterial,
            EnergyType.Yellow => yellowEnergyMaterial,
            _ => redEnergyMaterial
        };
    }

    // Fallback helper method to apply energy type materials
    void ApplyEnergyMaterial(GameObject obj, EnergyType energyType)
    {
        var renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return;

        Material energyMaterial = GetEnergyMaterial(energyType);
        if (energyMaterial != null)
        {
            renderer.material = energyMaterial;
        }
        else
        {
            // Fallback to color if no material is assigned
            renderer.material.color = energyType switch
            {
                EnergyType.Red => Color.red,
                EnergyType.Green => Color.green,
                EnergyType.Blue => Color.blue,
                EnergyType.Cyan => Color.cyan,
                EnergyType.Magenta => Color.magenta,
                EnergyType.Yellow => Color.yellow,
                _ => Color.white
            };
        }
    }

    // Public method to get world surface position from any 3D point
    public Vector3 GetSurfacePosition(Vector3 worldPosition)
    {
        return worldPosition.normalized * worldRadius;
    }

    // Check if a position is on or near the world surface
    public bool IsOnSurface(Vector3 position, float tolerance = 1f)
    {
        float distance = Vector3.Distance(position, Vector3.zero);
        return Mathf.Abs(distance - worldRadius) <= tolerance;
    }

    // Public getter for the current world radius
    public float GetWorldRadius()
    {
        return worldRadius;
    }

    void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (GameManager.Conn != null)
        {
            GameManager.Conn.Db.EnergyPuddle.OnInsert -= OnEnergyPuddleCreated;
            GameManager.Conn.Db.EnergyPuddle.OnDelete -= OnEnergyPuddleDeleted;
            GameManager.Conn.Db.EnergyOrb.OnInsert -= OnEnergyOrbCreated;
            GameManager.Conn.Db.EnergyOrb.OnUpdate -= OnEnergyOrbUpdated;
            GameManager.Conn.Db.EnergyOrb.OnDelete -= OnEnergyOrbDeleted;
            GameManager.Conn.Db.DistributionSphere.OnInsert -= OnDistributionSphereCreated;
            GameManager.Conn.Db.DistributionSphere.OnDelete -= OnDistributionSphereDeleted;
            GameManager.Conn.Db.Player.OnInsert -= OnPlayerJoined;
            GameManager.Conn.Db.Player.OnUpdate -= OnPlayerUpdated;
            GameManager.Conn.Db.Player.OnDelete -= OnPlayerLeft;
        }
    }

    // Debug helper to show world info
    void OnGUI()
    {
        if (!isInitialized) return;
        
    /*    GUI.Label(new Rect(10, 10, 400, 100), 
            $"World Manager Status:\n" +
            $"Current World: ({currentWorldCoords.X},{currentWorldCoords.Y},{currentWorldCoords.Z})\n" +
            $"World Type: {(IsCenter(currentWorldCoords) ? "Center" : "Shell")}\n" +
            $"World Radius: {worldRadius}\n" +
            $"Distribution Spheres: {distributionSpheres.Count}\n" +
            $"Players: {playerObjects.Count}, Puddles: {energyPuddles.Count}, Orbs: {energyOrbs.Count}");*/
    }
}