using UnityEngine;
using UnityEngine.InputSystem;
using SpacetimeDB.Types;

/// <summary>
/// Debug helper to diagnose world scene issues - Updated for new Input System
/// </summary>
public class WorldDebugHelper : MonoBehaviour
{
    [Header("Debug Settings")]
    public bool showDebugInfo = true;
    public bool createTestPlayer = false;
    public GameObject testPlayerPrefab;
    
    private GameObject debugPlayer;
    private string debugLog = "";
    
    // Input System actions
    private InputAction refreshAction;
    private InputAction createPlayerAction;
    private InputAction teleportAction;
    
    void Start()
    {
        DebugWorldState();
        SetupInputActions();
    }
    
    void SetupInputActions()
    {
        // Create input actions for debug controls
        refreshAction = new InputAction("Refresh", InputActionType.Button, "<Keyboard>/r");
        createPlayerAction = new InputAction("CreatePlayer", InputActionType.Button, "<Keyboard>/p");
        teleportAction = new InputAction("Teleport", InputActionType.Button, "<Keyboard>/t");
        
        // Enable all actions
        refreshAction.Enable();
        createPlayerAction.Enable();
        teleportAction.Enable();
    }
    
    void OnEnable()
    {
        refreshAction?.Enable();
        createPlayerAction?.Enable();
        teleportAction?.Enable();
    }
    
    void OnDisable()
    {
        refreshAction?.Disable();
        createPlayerAction?.Disable();
        teleportAction?.Disable();
    }
    
    void DebugWorldState()
    {
        debugLog = "=== WORLD DEBUG INFO ===\n";
        
        // Check GameData
        if (GameData.Instance != null)
        {
            var coords = GameData.Instance.GetCurrentWorldCoords();
            debugLog += $"GameData Present - World: ({coords.X},{coords.Y},{coords.Z})\n";
            debugLog += $"Username: {GameData.Instance.Username}\n";
            debugLog += $"IsLoggedIn: {GameData.Instance.IsLoggedIn}\n";
        }
        else
        {
            debugLog += "ERROR: GameData.Instance is NULL!\n";
        }
        
        // Check GameManager connection
        if (GameManager.Instance != null)
        {
            debugLog += $"GameManager Present - Connected: {GameManager.IsConnected()}\n";
            if (GameManager.LocalIdentity != null)
            {
                debugLog += $"Local Identity: {GameManager.LocalIdentity}\n";
            }
            
            // Check for player in database
            if (GameManager.IsConnected())
            {
                var player = GameManager.Instance.GetCurrentPlayer();
                if (player != null)
                {
                    debugLog += $"Player Found: {player.Name} at ({player.Position.X},{player.Position.Y},{player.Position.Z})\n";
                    debugLog += $"Player World: ({player.CurrentWorld.X},{player.CurrentWorld.Y},{player.CurrentWorld.Z})\n";
                }
                else
                {
                    debugLog += "ERROR: No player found in database!\n";
                }
            }
        }
        else
        {
            debugLog += "ERROR: GameManager.Instance is NULL!\n";
        }
        
        // Check WorldManager
        var worldManager = FindFirstObjectByType<WorldManager>();
        if (worldManager != null)
        {
            debugLog += "WorldManager Found\n";
        }
        else
        {
            debugLog += "ERROR: No WorldManager in scene!\n";
        }
        
        // Check for existing players
        var playerControllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        debugLog += $"PlayerController count in scene: {playerControllers.Length}\n";
        foreach (var pc in playerControllers)
        {
            debugLog += $"  - Player: {pc.name} at {pc.transform.position}\n";
        }
        
        // Check camera
        if (Camera.main != null)
        {
            debugLog += $"Main Camera Found at: {Camera.main.transform.position}\n";
        }
        else
        {
            debugLog += "ERROR: No main camera found!\n";
        }
        
        Debug.Log(debugLog);
    }
    
    void Update()
    {
        // Check for input using new Input System
        if (refreshAction != null && refreshAction.WasPressedThisFrame())
        {
            DebugWorldState();
        }
        
        if (createPlayerAction != null && createPlayerAction.WasPressedThisFrame())
        {
            createTestPlayer = true;
        }
        
        if (teleportAction != null && teleportAction.WasPressedThisFrame() && debugPlayer != null)
        {
            Vector3 randomPos = Random.onUnitSphere * 100f;
            debugPlayer.transform.position = randomPos;
            debugPlayer.transform.LookAt(Vector3.zero);
            debugPlayer.transform.Rotate(-90f, 0f, 0f);
        }
        
        // Test player creation
        if (createTestPlayer && debugPlayer == null)
        {
            CreateDebugPlayer();
            createTestPlayer = false;
        }
        
        // Update camera to follow debug player if exists
        if (debugPlayer != null && Camera.main != null)
        {
            UpdateCameraForPlayer(debugPlayer);
        }
    }
    
    void CreateDebugPlayer()
    {
        GameObject prefab = testPlayerPrefab;
        if (prefab == null)
        {
            // Create a simple capsule
            prefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var mat = new Material(Shader.Find("Standard"));
            mat.color = Color.yellow;
            prefab.GetComponent<Renderer>().material = mat;
        }
        
        // Create at random position on sphere
        Vector3 randomDir = Random.onUnitSphere;
        float worldRadius = 100f; // Default radius
        
        var worldManager = FindFirstObjectByType<WorldManager>();
        if (worldManager != null)
        {
            worldRadius = worldManager.worldRadius;
        }
        
        Vector3 spawnPos = randomDir * worldRadius;
        
        debugPlayer = Instantiate(prefab, spawnPos, Quaternion.identity);
        debugPlayer.name = "DEBUG_PLAYER";
        
        // Orient to sphere
        debugPlayer.transform.LookAt(Vector3.zero);
        debugPlayer.transform.Rotate(-90f, 0f, 0f);
        
        // Add simple movement for debug purposes
        // No controller needed for debug player - just visual representation
        
        Debug.Log($"Created debug player at {spawnPos}");
    }
    
    void UpdateCameraForPlayer(GameObject player)
    {
        var camera = Camera.main;
        Vector3 playerPos = player.transform.position;
        Vector3 playerUp = playerPos.normalized;
        
        // Position camera behind and above player
        Vector3 cameraOffset = -player.transform.forward * 10f + playerUp * 5f;
        camera.transform.position = playerPos + cameraOffset;
        camera.transform.LookAt(playerPos + playerUp * 2f);
    }
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        // Show debug info
        GUI.Box(new Rect(10, 10, 400, 300), "World Debug Info");
        GUI.Label(new Rect(20, 30, 380, 270), debugLog);
        
        // Debug controls
        GUI.Label(new Rect(10, 315, 380, 20), "Controls: R=Refresh, P=Create Player, T=Teleport");
        
        if (GUI.Button(new Rect(10, 340, 150, 30), "Refresh Debug Info"))
        {
            DebugWorldState();
        }
        
        if (GUI.Button(new Rect(170, 340, 150, 30), "Create Test Player"))
        {
            createTestPlayer = true;
        }
        
        if (debugPlayer != null && GUI.Button(new Rect(10, 380, 150, 30), "Teleport Debug Player"))
        {
            Vector3 randomPos = Random.onUnitSphere * 100f;
            debugPlayer.transform.position = randomPos;
            debugPlayer.transform.LookAt(Vector3.zero);
            debugPlayer.transform.Rotate(-90f, 0f, 0f);
        }
    }
    
    void OnDestroy()
    {
        // Clean up input actions
        refreshAction?.Disable();
        createPlayerAction?.Disable();
        teleportAction?.Disable();
        
        refreshAction?.Dispose();
        createPlayerAction?.Dispose();
        teleportAction?.Dispose();
    }
}