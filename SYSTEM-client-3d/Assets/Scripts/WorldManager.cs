using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// WorldManager now uses the modular subscription system
/// </summary>
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
    
    [Tooltip("Prefab for world circuit (volcano)")]
    public GameObject worldCircuitPrefab;

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

    // Visual object tracking
    private GameObject worldSphere;
    private GameObject worldCircuitObject;
    private Dictionary<ulong, GameObject> energyPuddleObjects = new Dictionary<ulong, GameObject>();
    private Dictionary<ulong, GameObject> energyOrbObjects = new Dictionary<ulong, GameObject>();
    private Dictionary<ulong, GameObject> distributionSphereObjects = new Dictionary<ulong, GameObject>();
    private Dictionary<uint, GameObject> playerObjects = new Dictionary<uint, GameObject>();
    
    // Current world state
    private WorldCoords currentWorldCoords;
    private bool isInitialized = false;
    
    // Subscription controllers
    private WorldCircuitSubscriptionController circuitController;
    private EnergySubscriptionController energyController;
    private PlayerSubscriptionController playerController;

    void Awake()
    {
        // Ensure this component is attached to a valid GameObject
        if (this == null || gameObject == null)
        {
            Debug.LogError("WorldManager is not properly attached to a GameObject!");
            return;
        }
        
        // Add subscription controllers as separate components
        try
        {
            circuitController = gameObject.AddComponent<WorldCircuitSubscriptionController>();
            energyController = gameObject.AddComponent<EnergySubscriptionController>();
            playerController = gameObject.AddComponent<PlayerSubscriptionController>();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to add subscription controllers: {e.Message}");
            return;
        }
        
        // Setup Unity Events only if controllers were created successfully
        if (circuitController != null)
        {
            circuitController.OnCircuitLoaded.AddListener(OnCircuitLoaded);
            circuitController.OnCircuitUpdated.AddListener(OnCircuitUpdated);
        }
    }

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
            return;
        }

        // Subscribe to events via EventBus
        SubscribeToEvents();
        
        // Create world immediately
        CreateWorldSphere();
        isInitialized = true;
    }

    void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    void SubscribeToEvents()
    {
        // Energy events
        EventBus.Subscribe<EnergyOrbCreatedEvent>(OnEnergyOrbCreated);
        EventBus.Subscribe<EnergyOrbUpdatedEvent>(OnEnergyOrbUpdated);
        EventBus.Subscribe<EnergyOrbDeletedEvent>(OnEnergyOrbDeleted);
        
        EventBus.Subscribe<EnergyPuddleCreatedEvent>(OnEnergyPuddleCreated);
        EventBus.Subscribe<EnergyPuddleUpdatedEvent>(OnEnergyPuddleUpdated);
        EventBus.Subscribe<EnergyPuddleDeletedEvent>(OnEnergyPuddleDeleted);
        
        // Player events
        EventBus.Subscribe<LocalPlayerSpawnedEvent>(OnLocalPlayerSpawned);
        EventBus.Subscribe<RemotePlayerJoinedEvent>(OnRemotePlayerJoined);
        EventBus.Subscribe<RemotePlayerUpdatedEvent>(OnRemotePlayerUpdated);
        EventBus.Subscribe<RemotePlayerLeftEvent>(OnRemotePlayerLeft);
        
        // Distribution sphere events (if you have them)
        if (GameManager.IsConnected())
        {
            GameManager.Conn.Db.DistributionSphere.OnInsert += OnDistributionSphereCreated;
            GameManager.Conn.Db.DistributionSphere.OnDelete += OnDistributionSphereDeleted;
        }
    }

    void UnsubscribeFromEvents()
    {
        // Energy events
        EventBus.Unsubscribe<EnergyOrbCreatedEvent>(OnEnergyOrbCreated);
        EventBus.Unsubscribe<EnergyOrbUpdatedEvent>(OnEnergyOrbUpdated);
        EventBus.Unsubscribe<EnergyOrbDeletedEvent>(OnEnergyOrbDeleted);
        
        EventBus.Unsubscribe<EnergyPuddleCreatedEvent>(OnEnergyPuddleCreated);
        EventBus.Unsubscribe<EnergyPuddleUpdatedEvent>(OnEnergyPuddleUpdated);
        EventBus.Unsubscribe<EnergyPuddleDeletedEvent>(OnEnergyPuddleDeleted);
        
        // Player events
        EventBus.Unsubscribe<LocalPlayerSpawnedEvent>(OnLocalPlayerSpawned);
        EventBus.Unsubscribe<RemotePlayerJoinedEvent>(OnRemotePlayerJoined);
        EventBus.Unsubscribe<RemotePlayerUpdatedEvent>(OnRemotePlayerUpdated);
        EventBus.Unsubscribe<RemotePlayerLeftEvent>(OnRemotePlayerLeft);
        
        // Distribution sphere events
        if (GameManager.Conn != null)
        {
            GameManager.Conn.Db.DistributionSphere.OnInsert -= OnDistributionSphereCreated;
            GameManager.Conn.Db.DistributionSphere.OnDelete -= OnDistributionSphereDeleted;
        }
    }

    void SetupWorldForCoordinates(WorldCoords coords)
    {
        // Customize world appearance/behavior based on coordinates
        if (IsCenter(coords))
        {
            // Center world specific setup
            worldRadius = 300f;
        }
        else
        {
            // Shell world setup
            int shellLevel = CalculateShellLevel(coords);
            worldRadius = 300f - (shellLevel * 50f); // Smaller worlds as you go out
        }
    }

    void CreateWorldSphere()
    {
        // Determine which prefab to use
        GameObject worldPrefab = IsCenter(currentWorldCoords) ? centerWorldPrefab : shellWorldPrefab;
        
        if (worldPrefab != null)
        {
            worldSphere = Instantiate(worldPrefab, Vector3.zero, Quaternion.identity);
            worldSphere.name = $"World_{currentWorldCoords.X}_{currentWorldCoords.Y}_{currentWorldCoords.Z}";
            
            // Try to find mesh filter on the object or its children
            MeshFilter meshFilter = worldSphere.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = worldSphere.GetComponentInChildren<MeshFilter>();
            }
            
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                // Get the actual size of the mesh
                Bounds meshBounds = meshFilter.sharedMesh.bounds;
                float maxExtent = Mathf.Max(meshBounds.extents.x, meshBounds.extents.y, meshBounds.extents.z);
                
                // Calculate scale factor to achieve desired world radius
                float scaleFactor = worldRadius / maxExtent;
                worldSphere.transform.localScale = Vector3.one * scaleFactor;
                
                Debug.Log($"[WorldManager] Found mesh on: {meshFilter.gameObject.name}");
                Debug.Log($"[WorldManager] Mesh bounds: {meshBounds.size}, Max extent: {maxExtent}");
                Debug.Log($"[WorldManager] Scaling world sphere by {scaleFactor} to achieve radius {worldRadius}");
            }
            else
            {
                Debug.LogWarning("[WorldManager] No mesh filter found on world sphere or children, using known scale factor");
                // Use the known working scale factor
                worldSphere.transform.localScale = Vector3.one * 14.8f;
            }
        }
        else
        {
            Debug.LogError("No world prefab assigned!");
        }
    }

    // Circuit event handlers
    void OnCircuitLoaded(WorldCircuit circuit)
    {
        if (worldCircuitObject == null && worldCircuitPrefab != null)
        {
            // Create circuit at north pole
            Vector3 northPole = Vector3.up * worldRadius;
            worldCircuitObject = Instantiate(worldCircuitPrefab, northPole, Quaternion.identity);
            worldCircuitObject.name = $"WorldCircuit_{circuit.QubitCount}Qubit";
            
            // Initialize the circuit controller
            var controller = worldCircuitObject.GetComponent<WorldCircuitController>();
            if (controller != null)
            {
                controller.Initialize(circuit);
            }
        }
    }

    void OnCircuitUpdated(WorldCircuit circuit)
    {
        if (worldCircuitObject != null)
        {
            var controller = worldCircuitObject.GetComponent<WorldCircuitController>();
            if (controller != null)
            {
                controller.UpdateCircuit(circuit);
            }
        }
    }

    // Energy orb event handlers
    void OnEnergyOrbCreated(EnergyOrbCreatedEvent evt)
    {
        var orb = evt.Orb;
        if (!energyOrbObjects.ContainsKey(orb.OrbId) && energyOrbPrefab != null)
        {
            GameObject orbObj = Instantiate(energyOrbPrefab, orb.Position.ToUnity(), Quaternion.identity);
            orbObj.name = $"EnergyOrb_{orb.OrbId}";
            
            // Apply energy material
            ApplyEnergyMaterial(orbObj, orb.EnergyType);
            
            // Add to tracking
            energyOrbObjects[orb.OrbId] = orbObj;
            
            // Initialize orb controller if present
            var orbController = orbObj.GetComponent<EnergyOrbController>();
            if (orbController != null)
            {
                orbController.Initialize(orb, GetEnergyMaterial(orb.EnergyType), worldRadius);
            }
        }
    }

    void OnEnergyOrbUpdated(EnergyOrbUpdatedEvent evt)
    {
        if (energyOrbObjects.TryGetValue(evt.NewOrb.OrbId, out GameObject orbObj))
        {
            // Update position
            orbObj.transform.position = evt.NewOrb.Position.ToUnity();
            
            // Update controller if present
            var orbController = orbObj.GetComponent<EnergyOrbController>();
            if (orbController != null)
            {
                orbController.UpdateData(evt.NewOrb);
            }
        }
    }

    void OnEnergyOrbDeleted(EnergyOrbDeletedEvent evt)
    {
        if (energyOrbObjects.TryGetValue(evt.Orb.OrbId, out GameObject orbObj))
        {
            energyOrbObjects.Remove(evt.Orb.OrbId);
            Destroy(orbObj);
        }
    }

    // Energy puddle event handlers
    void OnEnergyPuddleCreated(EnergyPuddleCreatedEvent evt)
    {
        var puddle = evt.Puddle;
        if (!energyPuddleObjects.ContainsKey(puddle.PuddleId) && energyPuddlePrefab != null)
        {
            // Place puddle on world surface
            Vector3 surfacePos = GetSurfacePosition(puddle.Position.ToUnity());
            GameObject puddleObj = Instantiate(energyPuddlePrefab, surfacePos, Quaternion.identity);
            puddleObj.name = $"EnergyPuddle_{puddle.PuddleId}";
            
            // Apply energy material
            ApplyEnergyMaterial(puddleObj, puddle.EnergyType);
            
            // Scale based on amount
            float scale = Mathf.Lerp(0.5f, 3f, puddle.CurrentAmount / 100f);
            puddleObj.transform.localScale = Vector3.one * scale;
            
            energyPuddleObjects[puddle.PuddleId] = puddleObj;
        }
    }

    void OnEnergyPuddleUpdated(EnergyPuddleUpdatedEvent evt)
    {
        if (energyPuddleObjects.TryGetValue(evt.NewPuddle.PuddleId, out GameObject puddleObj))
        {
            // Update scale based on amount
            float scale = Mathf.Lerp(0.5f, 3f, evt.NewPuddle.CurrentAmount / 100f);
            puddleObj.transform.localScale = Vector3.one * scale;
        }
    }

    void OnEnergyPuddleDeleted(EnergyPuddleDeletedEvent evt)
    {
        if (energyPuddleObjects.TryGetValue(evt.Puddle.PuddleId, out GameObject puddleObj))
        {
            energyPuddleObjects.Remove(evt.Puddle.PuddleId);
            Destroy(puddleObj);
        }
    }

    // Player event handlers
    void OnLocalPlayerSpawned(LocalPlayerSpawnedEvent evt)
    {
        CreatePlayerObject(evt.Player, true);
    }

    void OnRemotePlayerJoined(RemotePlayerJoinedEvent evt)
    {
        CreatePlayerObject(evt.Player, false);
    }

    void OnRemotePlayerUpdated(RemotePlayerUpdatedEvent evt)
    {
        if (playerObjects.TryGetValue(evt.NewPlayer.PlayerId, out GameObject playerObj))
        {
            var playerScript = playerObj.GetComponent<PlayerController>();
            if (playerScript != null)
            {
                playerScript.UpdateData(evt.NewPlayer, worldRadius);
            }
        }
    }

    void OnRemotePlayerLeft(RemotePlayerLeftEvent evt)
    {
        if (playerObjects.TryGetValue(evt.Player.PlayerId, out GameObject playerObj))
        {
            playerObjects.Remove(evt.Player.PlayerId);
            Destroy(playerObj);
        }
    }

    void CreatePlayerObject(Player player, bool isLocal)
    {
        if (!playerObjects.ContainsKey(player.PlayerId) && playerPrefab != null)
        {
            GameObject playerObj = Instantiate(playerPrefab, player.Position.ToUnity(), 
                Quaternion.Euler(player.Rotation.ToUnity()));
            playerObj.name = isLocal ? "LocalPlayer" : $"Player_{player.Name}";
            
            var playerScript = playerObj.GetComponent<PlayerController>();
            if (playerScript != null)
            {
                playerScript.Initialize(player, isLocal, worldRadius);
            }
            
            playerObjects[player.PlayerId] = playerObj;
        }
    }

    // Distribution sphere handlers (legacy - not using EventBus yet)
    void OnDistributionSphereCreated(EventContext ctx, DistributionSphere sphere)
    {
        if (IsInCurrentWorld(sphere.WorldCoords) && !distributionSphereObjects.ContainsKey(sphere.SphereId))
        {
            GameObject sphereObj = Instantiate(distributionSpherePrefab, sphere.Position.ToUnity(), Quaternion.identity);
            sphereObj.name = $"DistributionSphere_{sphere.SphereId}";
            distributionSphereObjects[sphere.SphereId] = sphereObj;
        }
    }

    void OnDistributionSphereDeleted(EventContext ctx, DistributionSphere sphere)
    {
        if (distributionSphereObjects.TryGetValue(sphere.SphereId, out GameObject sphereObj))
        {
            distributionSphereObjects.Remove(sphere.SphereId);
            Destroy(sphereObj);
        }
    }

    // Helper methods
    bool IsCenter(WorldCoords coords) => coords.X == 0 && coords.Y == 0 && coords.Z == 0;
    
    int CalculateShellLevel(WorldCoords coords)
    {
        return Mathf.Max(Mathf.Abs(coords.X), Mathf.Abs(coords.Y), Mathf.Abs(coords.Z));
    }

    bool IsInCurrentWorld(WorldCoords coords)
    {
        return coords.X == currentWorldCoords.X && 
               coords.Y == currentWorldCoords.Y && 
               coords.Z == currentWorldCoords.Z;
    }

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

    void ApplyEnergyMaterial(GameObject obj, EnergyType energyType)
    {
        var renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return;

        Material energyMaterial = GetEnergyMaterial(energyType);
        if (energyMaterial != null)
        {
            renderer.material = energyMaterial;
        }
    }

    public Vector3 GetSurfacePosition(Vector3 worldPosition)
    {
        return worldPosition.normalized * worldRadius;
    }

    public bool IsOnSurface(Vector3 position, float tolerance = 1f)
    {
        float distance = Vector3.Distance(position, Vector3.zero);
        return Mathf.Abs(distance - worldRadius) <= tolerance;
    }

    public float GetWorldRadius() => worldRadius;

    // Debug info
    void OnGUI()
    {
        if (!isInitialized) return;
        
        var style = new GUIStyle(GUI.skin.label);
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        
        GUI.Label(new Rect(10, 10, 400, 200), 
            $"World Manager Status:\n" +
            $"Current World: ({currentWorldCoords.X},{currentWorldCoords.Y},{currentWorldCoords.Z})\n" +
            $"World Type: {(IsCenter(currentWorldCoords) ? "Center" : $"Shell {CalculateShellLevel(currentWorldCoords)}")}\n" +
            $"World Radius: {worldRadius}\n" +
            $"Circuit: {(circuitController.HasCircuit() ? $"{circuitController.GetCurrentCircuit()?.QubitCount ?? 0} qubits" : "None")}\n" +
            $"Players: {playerController.PlayerCount}\n" +
            $"Energy: {energyController.PuddleCount} puddles, {energyController.OrbCount} orbs\n" +
            $"Distribution Spheres: {distributionSphereObjects.Count}",
            style);
    }
}

// Extension method for DbVector3 to Unity Vector3
public static class DbVector3Extensions
{
    public static Vector3 ToUnity(this DbVector3 dbVec)
    {
        return new Vector3(dbVec.X, dbVec.Y, dbVec.Z);
    }
    
    public static Vector3 ToUnity(this DbQuaternion dbQuat)
    {
        // Convert quaternion to euler angles
        var quat = new Quaternion(dbQuat.X, dbQuat.Y, dbQuat.Z, dbQuat.W);
        return quat.eulerAngles;
    }
}