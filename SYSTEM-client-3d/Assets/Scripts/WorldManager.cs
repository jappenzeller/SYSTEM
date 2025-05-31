using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;

public class WorldManager : MonoBehaviour
{
    [Header("World Visualization")]
    [Tooltip("Material for the spherical world surface")]
    public Material worldSurfaceMaterial;
    
    [Tooltip("Prefab for energy puddles")]
    public GameObject energyPuddlePrefab;
    
    [Tooltip("Prefab for falling energy orbs")]
    public GameObject energyOrbPrefab;
    
    [Tooltip("Prefab for distribution spheres")]
    public GameObject distributionSpherePrefab;
    
    [Tooltip("Material for red energy")]
    public Material redEnergyMaterial;
    
    [Tooltip("Material for green energy")]
    public Material greenEnergyMaterial;
    
    [Tooltip("Material for blue energy")]
    public Material blueEnergyMaterial;
    
    [Tooltip("Material for cyan energy")]
    public Material cyanEnergyMaterial;
    
    [Tooltip("Material for magenta energy")]
    public Material magentaEnergyMaterial;
    
    [Tooltip("Material for yellow energy")]
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
        // Create the world sphere
        CreateWorldSphere();
        
        // Subscribe to SpacetimeDB events if connected
        if (GameManager.IsConnected())
        {
            SubscribeToWorldEvents();
        }
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
        if (GameManager.Conn == null) return;

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
    void OnEnergyPuddleCreated(EnergyPuddle puddle, EventContext ctx)
    {
        if (!IsCenterWorld(puddle.WorldCoords)) return; // Only show center world for now

        Vector3 position = DbVectorToUnity(puddle.Position);
        
        GameObject puddleObj = Instantiate(energyPuddlePrefab, position, Quaternion.identity);
        puddleObj.name = $"Energy Puddle {puddle.PuddleId}";
        
        // Orient the puddle to the sphere surface (point outward from center)
        puddleObj.transform.LookAt(Vector3.zero);
        puddleObj.transform.Rotate(180f, 0f, 0f); // Flip to face outward
        
        // Apply color material based on energy type
        ApplyEnergyMaterial(puddleObj, puddle.EnergyType);
        
        // Scale based on energy amount
        float scale = Mathf.Lerp(0.5f, 2.0f, puddle.CurrentAmount / puddle.MaxAmount);
        puddleObj.transform.localScale = Vector3.one * scale;
        
        energyPuddles[puddle.PuddleId] = puddleObj;
        
        Debug.Log($"Created energy puddle {puddle.PuddleId} at {position} with {puddle.CurrentAmount} {puddle.EnergyType} energy");
    }

    void OnEnergyPuddleDeleted(EnergyPuddle puddle, EventContext ctx)
    {
        if (energyPuddles.TryGetValue(puddle.PuddleId, out GameObject puddleObj))
        {
            energyPuddles.Remove(puddle.PuddleId);
            Destroy(puddleObj);
        }
    }

    // Energy Orb Event Handlers
    void OnEnergyOrbCreated(EnergyOrb orb, EventContext ctx)
    {
        if (!IsCenterWorld(orb.WorldCoords)) return; // Only show center world for now

        Vector3 position = DbVectorToUnity(orb.Position);
        
        GameObject orbObj = Instantiate(energyOrbPrefab, position, Quaternion.identity);
        orbObj.name = $"Energy Orb {orb.OrbId}";
        
        // Apply color material
        ApplyEnergyMaterial(orbObj, orb.EnergyType);
        
        // Add a simple glow effect
        var renderer = orbObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.EnableKeyword("_EMISSION");
        }
        
        energyOrbs[orb.OrbId] = orbObj;
        
        Debug.Log($"Created energy orb {orb.OrbId} at {position}");
    }

    void OnEnergyOrbUpdated(EnergyOrb oldOrb, EnergyOrb newOrb, EventContext ctx)
    {
        if (energyOrbs.TryGetValue(newOrb.OrbId, out GameObject orbObj))
        {
            Vector3 newPosition = DbVectorToUnity(newOrb.Position);
            orbObj.transform.position = newPosition;
        }
    }

    void OnEnergyOrbDeleted(EnergyOrb orb, EventContext ctx)
    {
        if (energyOrbs.TryGetValue(orb.OrbId, out GameObject orbObj))
        {
            energyOrbs.Remove(orb.OrbId);
            Destroy(orbObj);
        }
    }

    // Distribution Sphere Event Handlers
    void OnDistributionSphereCreated(DistributionSphere sphere, EventContext ctx)
    {
        if (!IsCenterWorld(sphere.WorldCoords)) return; // Only show center world for now

        Vector3 position = DbVectorToUnity(sphere.Position);
        
        GameObject sphereObj = Instantiate(distributionSpherePrefab, position, Quaternion.identity);
        sphereObj.name = $"Distribution Sphere {sphere.SphereId}";
        
        // Scale based on coverage radius
        float scale = sphere.CoverageRadius / 50f; // Normalize for visual size
        sphereObj.transform.localScale = Vector3.one * scale;
        
        // Make it semi-transparent and glowing
        var renderer = sphereObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0.5f, 0.8f, 1.0f, 0.3f); // Light blue, transparent
            renderer.material.EnableKeyword("_EMISSION");
        }
        
        distributionSpheres[sphere.SphereId] = sphereObj;
        
        Debug.Log($"Created distribution sphere {sphere.SphereId} at {position} with radius {sphere.CoverageRadius}");
    }

    void OnDistributionSphereDeleted(DistributionSphere sphere, EventContext ctx)
    {
        if (distributionSpheres.TryGetValue(sphere.SphereId, out GameObject sphereObj))
        {
            distributionSpheres.Remove(sphere.SphereId);
            Destroy(sphereObj);
        }
    }

    // Player Event Handlers
    void OnPlayerJoined(Player player, EventContext ctx)
    {
        if (!IsCenterWorld(player.CurrentWorld)) return; // Only show center world for now

        Vector3 position = DbVectorToUnity(player.Position);
        
        // Create a simple capsule for the player
        GameObject playerObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        playerObj.name = $"Player {player.Name}";
        playerObj.transform.position = position;
        
        // Orient player to stand on sphere surface
        playerObj.transform.LookAt(Vector3.zero);
        playerObj.transform.Rotate(-90f, 0f, 0f); // Stand upright on surface
        
        // Different color for local player vs others
        var renderer = playerObj.GetComponent<Renderer>();
        if (player.Identity == GameManager.LocalIdentity)
        {
            renderer.material.color = Color.yellow; // Local player
        }
        else
        {
            renderer.material.color = Color.white; // Other players
        }
        
        playerObjects[player.PlayerId] = playerObj;
        
        Debug.Log($"Player {player.Name} joined at {position}");
    }

    void OnPlayerUpdated(Player oldPlayer, Player newPlayer, EventContext ctx)
    {
        if (playerObjects.TryGetValue(newPlayer.PlayerId, out GameObject playerObj))
        {
            Vector3 newPosition = DbVectorToUnity(newPlayer.Position);
            playerObj.transform.position = newPosition;
            
            // Update orientation to sphere surface
            playerObj.transform.LookAt(Vector3.zero);
            playerObj.transform.Rotate(-90f, 0f, 0f);
        }
    }

    void OnPlayerLeft(Player player, EventContext ctx)
    {
        if (playerObjects.TryGetValue(player.PlayerId, out GameObject playerObj))
        {
            playerObjects.Remove(player.PlayerId);
            Destroy(playerObj);
            Debug.Log($"Player {player.Name} left the game");
        }
    }

    // Helper method to apply energy type materials
    void ApplyEnergyMaterial(GameObject obj, EnergyType energyType)
    {
        var renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return;

        switch (energyType)
        {
            case EnergyType.Red:
                if (redEnergyMaterial != null)
                    renderer.material = redEnergyMaterial;
                else
                    renderer.material.color = Color.red;
                break;
            case EnergyType.Green:
                if (greenEnergyMaterial != null)
                    renderer.material = greenEnergyMaterial;
                else
                    renderer.material.color = Color.green;
                break;
            case EnergyType.Blue:
                if (blueEnergyMaterial != null)
                    renderer.material = blueEnergyMaterial;
                else
                    renderer.material.color = Color.blue;
                break;
            case EnergyType.Cyan:
                if (cyanEnergyMaterial != null)
                    renderer.material = cyanEnergyMaterial;
                else
                    renderer.material.color = Color.cyan;
                break;
            case EnergyType.Magenta:
                if (magentaEnergyMaterial != null)
                    renderer.material = magentaEnergyMaterial;
                else
                    renderer.material.color = Color.magenta;
                break;
            case EnergyType.Yellow:
                if (yellowEnergyMaterial != null)
                    renderer.material = yellowEnergyMaterial;
                else
                    renderer.material.color = Color.yellow;
                break;
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