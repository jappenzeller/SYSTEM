using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using SpacetimeDB.Types;
using SpacetimeDB;

/// <summary>
/// Comprehensive spawn debugging component for WebGL builds
/// Tracks and displays all spawn-related events and state
/// </summary>
public class WebGLSpawnDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugUI = true;
    [SerializeField] private bool logToConsole = true;
    [SerializeField] private bool verboseLogging = false;
    [SerializeField] private KeyCode toggleKey = KeyCode.F7;
    [SerializeField] private KeyCode testSpawnResetKey = KeyCode.F8;
    
    [Header("UI Settings")]
    [SerializeField] private int fontSize = 14;
    [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.8f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color errorColor = Color.red;
    [SerializeField] private Color successColor = Color.green;
    
    [Header("Monitoring")]
    [SerializeField] private float updateInterval = 0.5f;
    
    // Spawn state tracking
    private class SpawnEvent
    {
        public float Time;
        public string Type;
        public string Message;
        public Vector3? Position;
        public bool IsError;
        
        public SpawnEvent(string type, string message, Vector3? pos = null, bool error = false)
        {
            Time = UnityEngine.Time.time;
            Type = type;
            Message = message;
            Position = pos;
            IsError = error;
        }
    }
    
    private Queue<SpawnEvent> spawnEvents = new Queue<SpawnEvent>();
    private const int MAX_EVENTS = 20;
    
    // Current state
    private GameObject localPlayer;
    private PlayerController playerController;
    private WorldManager worldManager;
    private Vector3 lastValidPosition;
    private Vector3 serverReportedPosition;
    private float worldRadius = 3000f;
    private bool isShowingUI = true;
    private float lastUpdateTime;
    
    // Spawn validation results
    private class ValidationResult
    {
        public bool IsValid;
        public string Issue;
        public float DistanceFromSurface;
        public float Magnitude;
        public bool IsNaN;
        public bool IsInfinity;
        public bool TooCloseToOrigin;
        
        public ValidationResult(Vector3 pos, float expectedRadius)
        {
            IsNaN = float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z);
            IsInfinity = float.IsInfinity(pos.x) || float.IsInfinity(pos.y) || float.IsInfinity(pos.z);
            Magnitude = pos.magnitude;
            TooCloseToOrigin = Magnitude < 10f;
            
            float expectedDistance = expectedRadius + 1f; // Surface offset
            DistanceFromSurface = Mathf.Abs(Magnitude - expectedDistance);
            
            IsValid = !IsNaN && !IsInfinity && !TooCloseToOrigin && DistanceFromSurface < 5f;
            
            if (IsNaN) Issue = "Position contains NaN";
            else if (IsInfinity) Issue = "Position contains Infinity";
            else if (TooCloseToOrigin) Issue = $"Too close to origin (mag: {Magnitude:F2})";
            else if (DistanceFromSurface > 5f) Issue = $"Not on surface (error: {DistanceFromSurface:F2})";
            else Issue = "Valid";
        }
    }
    
    private ValidationResult lastValidation;
    
    // Statistics
    private int totalSpawnAttempts = 0;
    private int successfulSpawns = 0;
    private int failedSpawns = 0;
    private int positionCorrections = 0;
    private float averageSpawnTime = 0f;
    private float lastSpawnStartTime = 0f;
    
    void Start()
    {
        LogEvent("INIT", "WebGLSpawnDebugger started");
        
        if (Application.platform != RuntimePlatform.WebGLPlayer && !Application.isEditor)
        {
            LogEvent("INIT", "Not running in WebGL or Editor, disabling debugger");
            enabled = false;
            return;
        }
        
        // Subscribe to game events
        SubscribeToEvents();
        
        // Find references
        StartCoroutine(FindReferences());
    }
    
    System.Collections.IEnumerator FindReferences()
    {
        yield return new WaitForSeconds(0.5f);
        
        worldManager = FindObjectOfType<WorldManager>();
        if (worldManager != null)
        {
            worldRadius = worldManager.GetWorldRadius();
            LogEvent("INIT", $"Found WorldManager, radius: {worldRadius}");
        }
        
        // Keep looking for local player
        StartCoroutine(MonitorLocalPlayer());
    }
    
    System.Collections.IEnumerator MonitorLocalPlayer()
    {
        while (true)
        {
            if (localPlayer == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player == null && worldManager != null)
                {
                    player = worldManager.GetLocalPlayerObject();
                }
                
                if (player != null)
                {
                    localPlayer = player;
                    playerController = player.GetComponent<PlayerController>();
                    LogEvent("PLAYER", $"Local player found: {player.name}", player.transform.position);
                    
                    // Validate initial spawn
                    ValidateSpawnPosition(player.transform.position);
                }
            }
            else
            {
                // Monitor position changes
                if (Time.time - lastUpdateTime > updateInterval)
                {
                    lastUpdateTime = Time.time;
                    MonitorPosition();
                }
            }
            
            yield return new WaitForSeconds(0.1f);
        }
    }
    
    void SubscribeToEvents()
    {
        // Subscribe to GameEventBus if available
        if (GameEventBus.Instance != null)
        {
            GameEventBus.Instance.Subscribe<LocalPlayerReadyEvent>(OnLocalPlayerReady);
            LogEvent("INIT", "Subscribed to GameEventBus");
        }
    }
    
    void OnLocalPlayerReady(LocalPlayerReadyEvent evt)
    {
        lastSpawnStartTime = Time.time;
        totalSpawnAttempts++;
        
        // Get player name from the Player object
        string playerName = evt.Player != null ? evt.Player.Name : "Unknown";
        LogEvent("SPAWN", $"Local player ready: {playerName}");
        
        // Mark as successful spawn
        float spawnTime = Time.time - lastSpawnStartTime;
        averageSpawnTime = (averageSpawnTime * (successfulSpawns) + spawnTime) / (successfulSpawns + 1);
        successfulSpawns++;
        
        // Get position from player data
        if (evt.Player != null)
        {
            Vector3 pos = new Vector3(evt.Player.Position.X, evt.Player.Position.Y, evt.Player.Position.Z);
            LogEvent("SPAWN", $"Player spawned (time: {spawnTime:F2}s)", pos);
        }
    }
    
    void MonitorPosition()
    {
        if (localPlayer == null) return;
        
        Vector3 currentPos = localPlayer.transform.position;
        
        // Check for position changes
        if (Vector3.Distance(currentPos, lastValidPosition) > 0.1f)
        {
            // Validate new position
            var validation = ValidateSpawnPosition(currentPos);
            
            if (!validation.IsValid)
            {
                LogEvent("WARNING", $"Invalid position detected: {validation.Issue}", currentPos, true);
                positionCorrections++;
            }
            
            lastValidPosition = currentPos;
        }
    }
    
    ValidationResult ValidateSpawnPosition(Vector3 position)
    {
        lastValidation = new ValidationResult(position, worldRadius);
        
        if (verboseLogging || !lastValidation.IsValid)
        {
            LogEvent("VALIDATE", 
                $"Position validation: {(lastValidation.IsValid ? "PASS" : "FAIL")} - {lastValidation.Issue}", 
                position, 
                !lastValidation.IsValid);
        }
        
        return lastValidation;
    }
    
    void Update()
    {
        // Toggle UI
        if (Input.GetKeyDown(toggleKey))
        {
            isShowingUI = !isShowingUI;
            LogEvent("UI", $"Debug UI {(isShowingUI ? "enabled" : "disabled")}");
        }
        
        // Test spawn reset
        if (Input.GetKeyDown(testSpawnResetKey))
        {
            RequestSpawnReset();
        }
        
        // Get server position from player data
        if (playerController != null)
        {
            var playerData = playerController.GetPlayerData();
            if (playerData != null)
            {
                serverReportedPosition = new Vector3(
                    playerData.Position.X,
                    playerData.Position.Y,
                    playerData.Position.Z
                );
            }
        }
    }
    
    void RequestSpawnReset()
    {
        LogEvent("TEST", "Requesting spawn position reset");
        
        if (GameManager.Instance != null && GameManager.Conn != null)
        {
            try
            {
                // Call debug reducer on server
                GameManager.Conn.Reducers.DebugResetSpawnPosition();
                LogEvent("TEST", "Spawn reset request sent to server");
            }
            catch (Exception e)
            {
                LogEvent("ERROR", $"Failed to send spawn reset: {e.Message}", null, true);
            }
        }
        else
        {
            LogEvent("ERROR", "GameManager not available for spawn reset", null, true);
        }
    }
    
    void LogEvent(string type, string message, Vector3? position = null, bool isError = false)
    {
        var evt = new SpawnEvent(type, message, position, isError);
        
        spawnEvents.Enqueue(evt);
        if (spawnEvents.Count > MAX_EVENTS)
        {
            spawnEvents.Dequeue();
        }
        
        if (logToConsole)
        {
            string posStr = position.HasValue ? $" at {position.Value:F2}" : "";
            if (isError)
                Debug.LogError($"[SpawnDebug:{type}] {message}{posStr}");
            else if (type == "WARNING")
                Debug.LogWarning($"[SpawnDebug:{type}] {message}{posStr}");
            else
                Debug.Log($"[SpawnDebug:{type}] {message}{posStr}");
        }
    }
    
    void OnGUI()
    {
        if (!enableDebugUI || !isShowingUI) return;
        
        // Create style
        GUIStyle bgStyle = new GUIStyle(GUI.skin.box);
        bgStyle.normal.background = MakeTexture(1, 1, backgroundColor);
        
        GUIStyle textStyle = new GUIStyle(GUI.skin.label);
        textStyle.fontSize = fontSize;
        textStyle.normal.textColor = textColor;
        textStyle.wordWrap = true;
        
        // Calculate size
        float width = 450;
        float height = 600;
        float x = Screen.width - width - 10;
        float y = 10;
        
        // Background
        GUI.Box(new Rect(x, y, width, height), "", bgStyle);
        
        // Title
        GUIStyle titleStyle = new GUIStyle(textStyle);
        titleStyle.fontSize = fontSize + 2;
        titleStyle.fontStyle = FontStyle.Bold;
        GUI.Label(new Rect(x + 10, y + 10, width - 20, 25), "Spawn Debugger", titleStyle);
        
        y += 35;
        
        // Current state
        GUI.Label(new Rect(x + 10, y, width - 20, 20), "=== Current State ===", textStyle);
        y += 25;
        
        if (localPlayer != null)
        {
            Vector3 pos = localPlayer.transform.position;
            GUI.Label(new Rect(x + 10, y, width - 20, 20), 
                $"Position: ({pos.x:F2}, {pos.y:F2}, {pos.z:F2})", textStyle);
            y += 20;
            
            GUI.Label(new Rect(x + 10, y, width - 20, 20), 
                $"Magnitude: {pos.magnitude:F2} (Expected: {worldRadius + 1:F0})", textStyle);
            y += 20;
            
            if (lastValidation != null)
            {
                GUIStyle validStyle = new GUIStyle(textStyle);
                validStyle.normal.textColor = lastValidation.IsValid ? successColor : errorColor;
                GUI.Label(new Rect(x + 10, y, width - 20, 20), 
                    $"Validation: {lastValidation.Issue}", validStyle);
                y += 20;
                
                GUI.Label(new Rect(x + 10, y, width - 20, 20), 
                    $"Surface Error: {lastValidation.DistanceFromSurface:F2} units", textStyle);
                y += 20;
            }
            
            GUI.Label(new Rect(x + 10, y, width - 20, 20), 
                $"Server Position: ({serverReportedPosition.x:F2}, {serverReportedPosition.y:F2}, {serverReportedPosition.z:F2})", 
                textStyle);
            y += 20;
            
            float serverDiff = Vector3.Distance(pos, serverReportedPosition);
            GUIStyle diffStyle = new GUIStyle(textStyle);
            diffStyle.normal.textColor = serverDiff > 1f ? warningColor : textColor;
            GUI.Label(new Rect(x + 10, y, width - 20, 20), 
                $"Server Diff: {serverDiff:F2} units", diffStyle);
            y += 25;
        }
        else
        {
            GUIStyle waitStyle = new GUIStyle(textStyle);
            waitStyle.normal.textColor = warningColor;
            GUI.Label(new Rect(x + 10, y, width - 20, 20), "Waiting for local player...", waitStyle);
            y += 25;
        }
        
        // Statistics
        GUI.Label(new Rect(x + 10, y, width - 20, 20), "=== Statistics ===", textStyle);
        y += 25;
        
        GUI.Label(new Rect(x + 10, y, width - 20, 20), 
            $"Spawn Attempts: {totalSpawnAttempts}", textStyle);
        y += 20;
        
        GUI.Label(new Rect(x + 10, y, width - 20, 20), 
            $"Successful: {successfulSpawns} / Failed: {failedSpawns}", textStyle);
        y += 20;
        
        GUI.Label(new Rect(x + 10, y, width - 20, 20), 
            $"Position Corrections: {positionCorrections}", textStyle);
        y += 20;
        
        GUI.Label(new Rect(x + 10, y, width - 20, 20), 
            $"Avg Spawn Time: {averageSpawnTime:F2}s", textStyle);
        y += 25;
        
        // Recent events
        GUI.Label(new Rect(x + 10, y, width - 20, 20), "=== Recent Events ===", textStyle);
        y += 25;
        
        float eventHeight = height - (y - 10) - 10;
        float eventY = 0;
        
        GUI.BeginScrollView(new Rect(x + 10, y, width - 20, eventHeight), 
            Vector2.zero, 
            new Rect(0, 0, width - 40, spawnEvents.Count * 20));
        
        foreach (var evt in spawnEvents)
        {
            GUIStyle eventStyle = new GUIStyle(textStyle);
            eventStyle.fontSize = fontSize - 2;
            
            if (evt.IsError)
                eventStyle.normal.textColor = errorColor;
            else if (evt.Type == "WARNING")
                eventStyle.normal.textColor = warningColor;
            else if (evt.Type == "SPAWN")
                eventStyle.normal.textColor = successColor;
            
            string timeStr = $"[{evt.Time:F1}]";
            string posStr = evt.Position.HasValue ? $" @{evt.Position.Value.y:F0}" : "";
            GUI.Label(new Rect(0, eventY, width - 40, 20), 
                $"{timeStr} {evt.Type}: {evt.Message}{posStr}", eventStyle);
            eventY += 20;
        }
        
        GUI.EndScrollView();
        
        // Controls hint
        GUIStyle hintStyle = new GUIStyle(textStyle);
        hintStyle.fontSize = fontSize - 2;
        hintStyle.normal.textColor = Color.gray;
        GUI.Label(new Rect(x + 10, Screen.height - 30, width - 20, 20), 
            $"Press {toggleKey} to toggle | {testSpawnResetKey} to test spawn reset", hintStyle);
    }
    
    Texture2D MakeTexture(int width, int height, Color color)
    {
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
        
        Texture2D texture = new Texture2D(width, height);
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
    
    void OnDestroy()
    {
        // Log final statistics
        LogEvent("SHUTDOWN", $"Final stats - Attempts: {totalSpawnAttempts}, Success: {successfulSpawns}, Corrections: {positionCorrections}");
    }
}