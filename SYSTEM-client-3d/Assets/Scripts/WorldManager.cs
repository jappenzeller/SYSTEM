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

    [Header("World Visualization")]
    [Tooltip("Material for the spherical world surface")]
    public Material worldSurfaceMaterial;

    [Header("Earth Appearance (Center World Only)")]
    [Tooltip("Material to use for the Center World to make it look like Earth. Assign a material with an Earth texture here.")]
    public Material earthMaterial;

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
            // Default to center world
            currentWorldCoords = new WorldCoords { X = 0, Y = 0, Z = 0 };
            SetupWorldForCoordinates(currentWorldCoords);
        }
        
        // ADD THIS CHECK:
        // Log any PlayerController instances found in the scene before WorldManager starts its own loading.
        // This helps identify if a PlayerController was accidentally left in the scene via the editor.
        PlayerController[] preExistingControllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        if (preExistingControllers.Length > 0)
        {
            Debug.LogWarning($"[WorldManager.Start] Found {preExistingControllers.Length} PlayerController instances already in the scene BEFORE player loading logic executed:");
            foreach (var pc in preExistingControllers)
            {
                Debug.LogWarning($"  - Pre-existing PlayerController: Name='{pc.name}', Position={pc.transform.position}, InstanceID={pc.GetInstanceID()}");
                // Aggressively clean up pre-existing controllers.
                Destroy(pc.gameObject);
                Debug.LogWarning($"    Destroyed pre-existing PlayerController: {pc.name}");
            }
        }
        
        // Validate prefabs
        ValidatePrefabs();
        
        // Create the world sphere
        CreateWorldSphere();
        
        // Subscribe to database events
        SubscribeToWorldEvents();
        
        // Load existing players for this world
        LoadExistingPlayers();
        
        // Load existing world objects
        LoadExistingWorldObjects();
        
        // World Manager initialization complete
        isInitialized = true;
    }

    void SetupWorldForCoordinates(WorldCoords coords)
    {
        // Customize world appearance/behavior based on coordinates
        if (IsCenter(coords))
        {
            // Center world specific setup
            worldRadius = 300f; 
            Debug.Log("Setting up CENTER WORLD");
        }
        else
        {
            // Shell world setup
            worldRadius = 240f; // Maintaining 80% of center world radius (300 * 0.8)
            Debug.Log($"Setting up SHELL WORLD at ({coords.X},{coords.Y},{coords.Z})");
        }
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
    }

    void CreateWorldSphere()
    {
        // Create the main world sphere
        worldSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        worldSphere.name = "World Sphere";
        worldSphere.transform.position = Vector3.zero;
        worldSphere.transform.localScale = Vector3.one * worldRadius * 2f; // Scale is diameter
        
        var renderer = worldSphere.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Check if this is the Center World and if an Earth material is assigned
            if (IsCenter(currentWorldCoords) && earthMaterial != null)
            {
                renderer.material = earthMaterial;
                Debug.Log("Applied Earth material to Center World sphere.");
            }
            // Otherwise, use the default worldSurfaceMaterial if available
            else if (worldSurfaceMaterial != null)
            {
                renderer.material = worldSurfaceMaterial;
                Debug.Log($"Applied default world surface material to { (IsCenter(currentWorldCoords) ? "Center World (Earth material not set)" : "Shell World") } sphere.");
            }
            else
            {
                Debug.LogWarning("No material assigned for the world sphere.");
            }
        }
        
        // Remove collider since we'll handle sphere physics ourselves
        var collider = worldSphere.GetComponent<SphereCollider>();
        if (collider != null)
        {
            DestroyImmediate(collider);
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

        // Subscribe to energy puddle changes
        GameManager.Conn.Db.EnergyPuddle.OnInsert += OnEnergyPuddleCreated;
        GameManager.Conn.Db.EnergyPuddle.OnDelete += OnEnergyPuddleDeleted;
        
        // Subscribe to energy orb changes
        GameManager.Conn.Db.EnergyOrb.OnInsert += OnEnergyOrbCreated;
        GameManager.Conn.Db.EnergyOrb.OnUpdate += OnEnergyOrbUpdated;
        GameManager.Conn.Db.EnergyOrb.OnDelete += OnEnergyOrbDeleted;

        // Subscribe to distribution sphere changes
        GameManager.Conn.Db.DistributionSphere.OnInsert += OnDistributionSphereCreated;
        GameManager.Conn.Db.DistributionSphere.OnDelete += OnDistributionSphereDeleted;

        // Subscribe to player changes
        GameManager.Conn.Db.Player.OnInsert += OnPlayerJoined;
        GameManager.Conn.Db.Player.OnUpdate += OnPlayerUpdated;
        GameManager.Conn.Db.Player.OnDelete += OnPlayerLeft;

        Debug.Log("Subscribed to world events");
    }

    void LoadExistingPlayers()
    {
        if (!GameManager.IsConnected()) return;

        // Load all players in the current world
        var allPlayers = GameManager.Conn.Db.Player.Iter();
        foreach (var player in allPlayers)
        {
            Debug.Log($"[WorldManager.LoadExistingPlayers] Checking player {player.Name} (ID: {player.PlayerId}), World: ({player.CurrentWorld.X},{player.CurrentWorld.Y},{player.CurrentWorld.Z})");
            if (IsInCurrentWorld(player.CurrentWorld))
            {
                CreatePlayerObject(player, "LoadExistingPlayers");
            }
        }
        
        Debug.Log($"Loaded {playerObjects.Count} existing players");
    }

    void LoadExistingWorldObjects()
    {
        if (!GameManager.IsConnected()) return;

        // Load energy puddles
        var puddles = GameManager.Conn.Db.EnergyPuddle.Iter();
        foreach (var puddle in puddles)
        {
            if (IsInCurrentWorld(puddle.WorldCoords))
            {
                CreateEnergyPuddleObject(puddle);
            }
        }

        // Load energy orbs
        var orbs = GameManager.Conn.Db.EnergyOrb.Iter();
        foreach (var orb in orbs)
        {
            if (IsInCurrentWorld(orb.WorldCoords))
            {
                CreateEnergyOrbObject(orb);
            }
        }

        // Load distribution spheres
        var spheres = GameManager.Conn.Db.DistributionSphere.Iter();
        foreach (var sphere in spheres)
        {
            if (IsInCurrentWorld(sphere.WorldCoords))
            {
                CreateDistributionSphereObject(sphere);
            }
        }

        Debug.Log($"Loaded world objects - Puddles: {energyPuddles.Count}, Orbs: {energyOrbs.Count}, Spheres: {distributionSpheres.Count}");
    }

    // Helper to check if coordinates match current world
    bool IsInCurrentWorld(WorldCoords coords)
    {
        return coords.X == currentWorldCoords.X && 
               coords.Y == currentWorldCoords.Y && 
               coords.Z == currentWorldCoords.Z;
    }

    // Helper to check if coordinates are center world
    bool IsCenter(WorldCoords coords)
    {
        return coords.X == 0 && coords.Y == 0 && coords.Z == 0;
    }

    // Convert SpacetimeDB DbVector3 to Unity Vector3
    Vector3 DbVectorToUnity(SpacetimeDB.Types.DbVector3 dbVec)
    {
        return new Vector3(dbVec.X, dbVec.Y, dbVec.Z);
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
        
        // Orient the puddle to the sphere surface (point outward from center)
        puddleObj.transform.LookAt(Vector3.zero);
        puddleObj.transform.Rotate(180f, 0f, 0f); // Flip to face outward
        
        // Configure the puddle using its script component
        var puddleScript = puddleObj.GetComponent<EnergyPuddleController>();
        if (puddleScript != null)
        {
            puddleScript.Initialize(puddle, GetEnergyMaterial(puddle.EnergyType));
        }
        else
        {
            // Fallback to direct material assignment
            ApplyEnergyMaterial(puddleObj, puddle.EnergyType);
            
            // Scale based on energy amount
            float scale = Mathf.Lerp(0.5f, 2.0f, puddle.CurrentAmount / puddle.MaxAmount);
            puddleObj.transform.localScale = Vector3.one * scale;
        }
        
        energyPuddles[puddle.PuddleId] = puddleObj;
        
  //      Debug.Log($"Created energy puddle {puddle.PuddleId} at {position} with {puddle.CurrentAmount} {puddle.EnergyType} energy");
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
        
        // Configure the orb using its script component
        var orbScript = orbObj.GetComponent<EnergyOrbController>();
        if (orbScript != null)
        {
            orbScript.Initialize(orb, GetEnergyMaterial(orb.EnergyType), this.worldRadius);
        }
        else
        {
            // Fallback to direct material assignment
            ApplyEnergyMaterial(orbObj, orb.EnergyType);
        }
        
        energyOrbs[orb.OrbId] = orbObj;
        
       // Debug.Log($"Created energy orb {orb.OrbId} at {position}");
    }

    void OnEnergyOrbUpdated(EventContext ctx, EnergyOrb oldOrb, EnergyOrb newOrb)
    {
        if (energyOrbs.TryGetValue(newOrb.OrbId, out GameObject orbObj))
        {
            Vector3 newPosition = DbVectorToUnity(newOrb.Position);
            orbObj.transform.position = newPosition;
            
            // Update orb controller if it exists
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
            
            // Trigger destruction animation if orb controller exists
            var orbScript = orbObj.GetComponent<EnergyOrbController>();
            if (orbScript != null)
            {
                orbScript.DestroyWithEffect();
            }
            else
            {
                Destroy(orbObj);
            }
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
                 // The dictionary had an entry, but the GameObject was destroyed.
                Debug.LogWarning($"[WorldManager.CreatePlayerObject FROM: {callSite}] Player object for {player.Name} (ID: {player.PlayerId}) was in dictionary but the GameObject is NULL/DESTROYED. Removing stale entry for PlayerId {player.PlayerId} before recreating.");
                playerObjects.Remove(player.PlayerId); // Clean up stale entry
            }
        }

        Debug.Log($"[WorldManager.CreatePlayerObject FROM: {callSite}] Proceeding to create new player object for {player.Name} (ID: {player.PlayerId}). Current playerObjects count: {playerObjects.Count}");

        if (playerPrefab == null)
        {
            Debug.LogError($"[WorldManager.CreatePlayerObject FROM: {callSite}] Player prefab is null! Cannot create player object for {player.Name}.");
            return;
        }
        
        Debug.Log($"[WorldManager.CreatePlayerObject FROM: {callSite}] Instantiating player prefab for {player.Name} (ID: {player.PlayerId}).");
        // Vector3 position = DbVectorToUnity(player.Position); // Define and initialize the 'position' variable
        // GameObject playerObj = Instantiate(playerPrefab, position, Quaternion.identity);
        Vector3 position = DbVectorToUnity(player.Position);
        Quaternion initialRotation = new Quaternion(
            player.Rotation.X,
            player.Rotation.Y,
            player.Rotation.Z,
            player.Rotation.W
        );
        initialRotation.Normalize(); // Ensure the quaternion is valid

        // Instantiate with the player's saved position and rotation
        GameObject playerObj = Instantiate(playerPrefab, position, initialRotation);
        playerObj.name = $"Player {player.Name}";
        
        // Add to dictionary immediately after instantiation and before Initialize.
        // This makes the ContainsKey check more robust for subsequent rapid calls.
        playerObjects[player.PlayerId] = playerObj;
        Debug.Log($"[WorldManager.CreatePlayerObject FROM: {callSite}] Added NEW player object {playerObj.name} (InstanceID: {playerObj.GetInstanceID()}) to playerObjects for ID {player.PlayerId}. playerObjects count: {playerObjects.Count}");

     //   // Orient player to stand on sphere surface
      //  playerObj.transform.LookAt(Vector3.zero);
      //  playerObj.transform.Rotate(-90f, 0f, 0f); // Stand upright on surface
                // Correct the 'up' vector to align with the sphere normal,
        // while preserving the forward direction from the initialRotation.
        Vector3 sphereNormal = position.normalized; // Normal from sphere center to player position
        Vector3 currentForward = playerObj.transform.forward;

        // Project the current forward onto the plane defined by the sphere normal.
        Vector3 targetForwardOnTangentPlane = Vector3.ProjectOnPlane(currentForward, sphereNormal);

        if (targetForwardOnTangentPlane.sqrMagnitude > 0.001f) // Ensure targetForward is not zero
        {
            playerObj.transform.rotation = Quaternion.LookRotation(targetForwardOnTangentPlane.normalized, sphereNormal);
        }
        else // Fallback if currentForward is (anti-)parallel to sphereNormal
        {
            // If looking straight up/down, just ensure 'up' is correct.
            // PlayerController input will quickly sort out forward.
            playerObj.transform.up = sphereNormal;
            Debug.LogWarning($"[WorldManager.CreatePlayerObject FROM: {callSite}] Player {player.Name}'s initial forward was aligned with sphere normal. Corrected 'up' vector. Initial yaw might be less precise if looking straight up/down.");
        }

        // Configure the player using its script component
        var playerScript = playerObj.GetComponent<PlayerController>();
        if (playerScript != null)
        {
            bool isLocalPlayer = (GameManager.LocalIdentity != null && player.Identity == GameManager.LocalIdentity);
            Debug.Log($"[WorldManager.CreatePlayerObject FROM: {callSite}] Calling Initialize on PlayerController for {player.Name} (ID: {player.PlayerId}), IsLocal: {isLocalPlayer}, WorldRadius: {this.worldRadius}.");
            playerScript.Initialize(player, isLocalPlayer, this.worldRadius); // Pass the current worldRadius
            Debug.Log($"[WorldManager.CreatePlayerObject FROM: {callSite}] PlayerController Initialize completed for {player.Name} (ID: {player.PlayerId}).");
        }
        else
        {
            // Critical error if PlayerController script is missing
            Debug.LogError($"[WorldManager.CreatePlayerObject FROM: {callSite}] PlayerController script not found on instantiated prefab for {player.Name}. This is a critical error. Removing from playerObjects and destroying.");
            playerObjects.Remove(player.PlayerId); // Remove if script is missing, log instance ID if possible
            Debug.LogWarning($"[WorldManager.CreatePlayerObject FROM: {callSite}] Removed {player.Name} (ID: {player.PlayerId}) from playerObjects due to missing PlayerController script. playerObjects count: {playerObjects.Count}");
            Destroy(playerObj); // Destroy the problematic object
            return;
        }
        
        Debug.Log($"[WorldManager.CreatePlayerObject FROM: {callSite}] " +
        $" Successfully created and initialized player object {playerObj.name} "+
        $" (InstanceID: {playerObj.GetInstanceID()}) for {player.Name} " +
        $" (ID: {player.PlayerId}). Position: {player.Position}, Rotation: {player.Rotation}, IsLocal: {(GameManager.LocalIdentity != null && player.Identity == GameManager.LocalIdentity)}. playerObjects count: {playerObjects.Count}");
    }

    void OnPlayerUpdated(EventContext ctx, Player oldPlayer, Player newPlayer)
    {
        // Check if player moved to a different world
        if (!IsInCurrentWorld(oldPlayer.CurrentWorld) && IsInCurrentWorld(newPlayer.CurrentWorld))
        {
            CreatePlayerObject(newPlayer, "OnPlayerUpdated_EnteredWorld");
        }
        else if (IsInCurrentWorld(oldPlayer.CurrentWorld) && !IsInCurrentWorld(newPlayer.CurrentWorld))
        {
            // Player left our world
            if (playerObjects.TryGetValue(newPlayer.PlayerId, out GameObject playerObj))
            {
                Debug.Log($"[WorldManager.OnPlayerUpdated_LeftWorld] Removing player {newPlayer.Name} (ID: {newPlayer.PlayerId}, InstanceID: {playerObj?.GetInstanceID()}) from playerObjects. Destroying GameObject.");
                playerObjects.Remove(newPlayer.PlayerId);
                Destroy(playerObj);
            }
        }
        else if (IsInCurrentWorld(newPlayer.CurrentWorld) && playerObjects.TryGetValue(newPlayer.PlayerId, out GameObject playerObj))
        {
            // Player is still in our world, update position
            // Update player controller if it exists
            var playerScript = playerObj.GetComponent<PlayerController>();
            if (playerScript != null)
            {
                bool isActuallyLocalPlayer = (GameManager.LocalIdentity != null && newPlayer.Identity == GameManager.LocalIdentity);

                if (!isActuallyLocalPlayer)
                {
                    // For remote players, we can set their position and a base orientation here.
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