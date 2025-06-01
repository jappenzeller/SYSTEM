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
    public float worldRadius = 100f;
    
    [Tooltip("How thick the world surface should be")]
    public float surfaceThickness = 2f;

    // Private fields
    private GameObject worldSphere;
    private Dictionary<ulong, GameObject> energyPuddles = new Dictionary<ulong, GameObject>();
    private Dictionary<ulong, GameObject> energyOrbs = new Dictionary<ulong, GameObject>();
    private Dictionary<ulong, GameObject> distributionSpheres = new Dictionary<ulong, GameObject>();
    private Dictionary<uint, GameObject> playerObjects = new Dictionary<uint, GameObject>();

    void Start()
    {
        // Validate prefabs first
        ValidatePrefabs();
        
        // Get current world from GameData
        if (GameData.Instance != null)
        {
            var currentWorld = GameData.Instance.GetCurrentWorldCoords();
            Debug.Log($"WorldManager starting in world ({currentWorld.X},{currentWorld.Y},{currentWorld.Z})");
            
            // Adapt world generation based on coordinates
            SetupWorldForCoordinates(currentWorld);
        }
        else
        {
            // Default to center world
            SetupWorldForCoordinates(new WorldCoords { X = 0, Y = 0, Z = 0 });
        }
        
        // Create the world sphere
        CreateWorldSphere();
        
        // Subscribe to SpacetimeDB events
        if (GameManager.IsConnected())
        {
            SubscribeToWorldEvents();
        }
        else
        {
            // Wait for connection and try again
            Invoke(nameof(TrySubscribeToEvents), 1f);
        }
    }

    void TrySubscribeToEvents()
    {
        if (GameManager.IsConnected())
        {
            SubscribeToWorldEvents();
        }
        else
        {
            // Try again in 1 second
            Invoke(nameof(TrySubscribeToEvents), 1f);
        }
    }

    void SetupWorldForCoordinates(WorldCoords coords)
    {
        // Customize world appearance/behavior based on coordinates
        if (SceneTransitionManager.IsCenter(coords))
        {
            // Center world specific setup
            worldRadius = 100f;
            Debug.Log("Setting up CENTER WORLD");
        }
        else
        {
            // Shell world setup
            worldRadius = 80f; // Smaller than center world
            Debug.Log($"Setting up SHELL WORLD at ({coords.X},{coords.Y},{coords.Z})");
        }
    }

    void ValidatePrefabs()
    {
        // Create simple fallback prefabs if none are assigned
        if (energyPuddlePrefab == null)
        {
            Debug.LogWarning("Energy Puddle Prefab not assigned - creating fallback");
            energyPuddlePrefab = CreateFallbackPuddle();
        }
        
        if (energyOrbPrefab == null)
        {
            Debug.LogWarning("Energy Orb Prefab not assigned - creating fallback");
            energyOrbPrefab = CreateFallbackOrb();
        }
        
        if (distributionSpherePrefab == null)
        {
            Debug.LogWarning("Distribution Sphere Prefab not assigned - creating fallback");
            distributionSpherePrefab = CreateFallbackSphere();
        }
        
        if (playerPrefab == null)
        {
            Debug.LogWarning("Player Prefab not assigned - creating fallback");
            playerPrefab = CreateFallbackPlayer();
        }
    }

    GameObject CreateFallbackPuddle()
    {
        GameObject puddle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        puddle.transform.localScale = new Vector3(2f, 0.1f, 2f);
        puddle.name = "Fallback Energy Puddle";
        
        // Add EnergyPuddleController if it exists
        var puddleController = puddle.GetComponent<EnergyPuddleController>();
        if (puddleController == null)
        {
            // Just make it visually distinctive
            var renderer = puddle.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Standard"));
                material.color = Color.red;
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.red * 0.5f);
                renderer.material = material;
            }
        }
        
        return puddle;
    }

    GameObject CreateFallbackOrb()
    {
        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.transform.localScale = Vector3.one * 0.5f;
        orb.name = "Fallback Energy Orb";
        
        var renderer = orb.GetComponent<Renderer>();
        if (renderer != null)
        {
            var material = new Material(Shader.Find("Standard"));
            material.color = Color.yellow;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.yellow);
            renderer.material = material;
        }
        
        return orb;
    }

    GameObject CreateFallbackSphere()
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Fallback Distribution Sphere";
        
        var renderer = sphere.GetComponent<Renderer>();
        if (renderer != null)
        {
            var material = new Material(Shader.Find("Standard"));
            material.color = new Color(0.5f, 0.8f, 1f, 0.3f);
            material.SetFloat("_Mode", 3); // Transparent
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            renderer.material = material;
        }
        
        return sphere;
    }

    GameObject CreateFallbackPlayer()
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Fallback Player";
        
        var renderer = player.GetComponent<Renderer>();
        if (renderer != null)
        {
            var material = new Material(Shader.Find("Standard"));
            material.color = Color.yellow;
            renderer.material = material;
        }
        
        return player;
    }

    void CreateWorldSphere()
    {
        // Create the main world sphere
        worldSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        worldSphere.name = "World Sphere";
        worldSphere.transform.position = Vector3.zero;
        worldSphere.transform.localScale = Vector3.one * worldRadius * 2f; // Scale is diameter
        
        // Apply material if provided
        if (worldSurfaceMaterial != null)
        {
            var renderer = worldSphere.GetComponent<Renderer>();
            renderer.material = worldSurfaceMaterial;
        }
        else
        {
            // Create basic world material
            var renderer = worldSphere.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Standard"));
            material.color = new Color(0.6f, 0.4f, 0.2f); // Brown
            renderer.material = material;
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
            Debug.LogWarning("Cannot subscribe to world events - no connection");
            return;
        }

        Debug.Log("Subscribing to SpacetimeDB world events...");

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

        Debug.Log("Successfully subscribed to world events");
        
        // Load any existing data
        LoadExistingWorldData();
    }

    void LoadExistingWorldData()
    {
        Debug.Log("Loading existing world data...");
        
        // Load existing energy puddles
        foreach (var puddle in GameManager.Conn.Db.EnergyPuddle.Iter())
        {
            if (IsCenterWorld(puddle.WorldCoords))
            {
                OnEnergyPuddleCreated(null, puddle);
            }
        }
        
        // Load existing energy orbs
        foreach (var orb in GameManager.Conn.Db.EnergyOrb.Iter())
        {
            if (IsCenterWorld(orb.WorldCoords))
            {
                OnEnergyOrbCreated(null, orb);
            }
        }
        
        // Load existing distribution spheres
        foreach (var sphere in GameManager.Conn.Db.DistributionSphere.Iter())
        {
            if (IsCenterWorld(sphere.WorldCoords))
            {
                OnDistributionSphereCreated(null, sphere);
            }
        }
        
        // Load existing players
        foreach (var player in GameManager.Conn.Db.Player.Iter())
        {
            if (IsCenterWorld(player.CurrentWorld))
            {
                OnPlayerJoined(null, player);
            }
        }
        
        Debug.Log($"Loaded existing data: {energyPuddles.Count} puddles, {energyOrbs.Count} orbs, {distributionSpheres.Count} spheres, {playerObjects.Count} players");
    }

    // Convert SpacetimeDB DbVector3 to Unity Vector3
    Vector3 DbVectorToUnity(SpacetimeDB.Types.DbVector3 dbVec)
    {
        return new Vector3(dbVec.X, dbVec.Y, dbVec.Z);
    }

    // Convert SpacetimeDB WorldCoords to check if it's center world
    bool IsCenterWorld(SpacetimeDB.Types.WorldCoords coords)
    {
        return coords.X == 0 && coords.Y == 0 && coords.Z == 0;
    }

    // Energy Puddle Event Handlers
    void OnEnergyPuddleCreated(EventContext ctx, EnergyPuddle puddle)
    {
        if (!IsCenterWorld(puddle.WorldCoords)) return; // Only show center world for now

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
        
        Debug.Log($"Created energy puddle {puddle.PuddleId} at {position} with {puddle.CurrentAmount} {puddle.EnergyType} energy");
    }

    void OnEnergyPuddleDeleted(EventContext ctx, EnergyPuddle puddle)
    {
        if (energyPuddles.TryGetValue(puddle.PuddleId, out GameObject puddleObj))
        {
            energyPuddles.Remove(puddle.PuddleId);
            Destroy(puddleObj);
            Debug.Log($"Deleted energy puddle {puddle.PuddleId}");
        }
    }

    // Energy Orb Event Handlers
    void OnEnergyOrbCreated(EventContext ctx, EnergyOrb orb)
    {
        if (!IsCenterWorld(orb.WorldCoords)) return; // Only show center world for now

        Vector3 position = DbVectorToUnity(orb.Position);
        
        GameObject orbObj = Instantiate(energyOrbPrefab, position, Quaternion.identity);
        orbObj.name = $"Energy Orb {orb.OrbId}";
        
        // Configure the orb using its script component
        var orbScript = orbObj.GetComponent<EnergyOrbController>();
        if (orbScript != null)
        {
            orbScript.Initialize(orb, GetEnergyMaterial(orb.EnergyType));
        }
        else
        {
            // Fallback to direct material assignment
            ApplyEnergyMaterial(orbObj, orb.EnergyType);
        }
        
        energyOrbs[orb.OrbId] = orbObj;
        
        Debug.Log($"Created energy orb {orb.OrbId} at {position}");
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
            
            Debug.Log($"Deleted energy orb {orb.OrbId}");
        }
    }

    // Distribution Sphere Event Handlers
    void OnDistributionSphereCreated(EventContext ctx, DistributionSphere sphere)
    {
        if (!IsCenterWorld(sphere.WorldCoords)) return; // Only show center world for now

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
                var material = new Material(Shader.Find("Standard"));
                material.color = new Color(0.5f, 0.8f, 1.0f, 0.3f); // Light blue, transparent
                material.SetFloat("_Mode", 3); // Transparent
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 3000;
                renderer.material = material;
            }
        }
        
        distributionSpheres[sphere.SphereId] = sphereObj;
        
        Debug.Log($"Created distribution sphere {sphere.SphereId} at {position} with radius {sphere.CoverageRadius}");
    }

    void OnDistributionSphereDeleted(EventContext ctx, DistributionSphere sphere)
    {
        if (distributionSpheres.TryGetValue(sphere.SphereId, out GameObject sphereObj))
        {
            distributionSpheres.Remove(sphere.SphereId);
            Destroy(sphereObj);
            Debug.Log($"Deleted distribution sphere {sphere.SphereId}");
        }
    }

    // Player Event Handlers
    void OnPlayerJoined(EventContext ctx, Player player)
    {
        if (!IsCenterWorld(player.CurrentWorld)) return; // Only show center world for now

        Vector3 position = DbVectorToUnity(player.Position);
        
        GameObject playerObj = Instantiate(playerPrefab, position, Quaternion.identity);
        playerObj.name = $"Player {player.Name}";
        
        // Orient player to stand on sphere surface
        playerObj.transform.LookAt(Vector3.zero);
        playerObj.transform.Rotate(-90f, 0f, 0f); // Stand upright on surface
        
        // Configure the player using its script component
        var playerScript = playerObj.GetComponent<PlayerController>();
        if (playerScript != null)
        {
            bool isLocalPlayer = player.Identity == GameManager.LocalIdentity;
            playerScript.Initialize(player, isLocalPlayer);
        }
        else
        {
            // Fallback configuration
            var renderer = playerObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (player.Identity == GameManager.LocalIdentity)
                {
                    renderer.material.color = Color.yellow; // Local player
                }
                else
                {
                    renderer.material.color = Color.white; // Other players
                }
            }
        }
        
        playerObjects[player.PlayerId] = playerObj;
        
        Debug.Log($"Player {player.Name} joined at {position}");
    }

    void OnPlayerUpdated(EventContext ctx, Player oldPlayer, Player newPlayer)
    {
        if (playerObjects.TryGetValue(newPlayer.PlayerId, out GameObject playerObj))
        {
            Vector3 newPosition = DbVectorToUnity(newPlayer.Position);
            playerObj.transform.position = newPosition;
            
            // Update orientation to sphere surface
            playerObj.transform.LookAt(Vector3.zero);
            playerObj.transform.Rotate(-90f, 0f, 0f);
            
            // Update player controller if it exists
            var playerScript = playerObj.GetComponent<PlayerController>();
            if (playerScript != null)
            {
                playerScript.UpdateData(newPlayer);
            }
        }
    }

    void OnPlayerLeft(EventContext ctx, Player player)
    {
        if (playerObjects.TryGetValue(player.PlayerId, out GameObject playerObj))
        {
            playerObjects.Remove(player.PlayerId);
            Destroy(playerObj);
            Debug.Log($"Player {player.Name} left the game");
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
            var material = new Material(Shader.Find("Standard"));
            material.color = energyType switch
            {
                EnergyType.Red => Color.red,
                EnergyType.Green => Color.green,
                EnergyType.Blue => Color.blue,
                EnergyType.Cyan => Color.cyan,
                EnergyType.Magenta => Color.magenta,
                EnergyType.Yellow => Color.yellow,
                _ => Color.white
            };
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", material.color * 0.5f);
            renderer.material = material;
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
}