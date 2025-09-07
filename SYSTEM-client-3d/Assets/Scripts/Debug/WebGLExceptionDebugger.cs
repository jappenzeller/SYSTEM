using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using SYSTEM.Game;

/// <summary>
/// Enhanced exception debugging for WebGL builds.
/// Provides detailed context about NullReferenceExceptions and component initialization.
/// </summary>
public class WebGLExceptionDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool autoAttachToLoginUI = true;
    [SerializeField] private bool verboseComponentLogging = true;
    [SerializeField] private bool logAllFieldValues = false;
    [SerializeField] private bool trackSpawnEvents = true;
    
    private static WebGLExceptionDebugger _instance;
    
    // Spawn tracking
    private static Vector3 lastPlayerSpawnPosition;
    private static float lastSpawnTime;
    private static int spawnAttemptCount = 0;
    private static List<string> spawnErrorLog = new List<string>();
    
    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log("[ExceptionDebugger] WebGL Exception Debugger Active");
        
        if (autoAttachToLoginUI)
        {
            AttachToLoginUIComponents();
        }
#endif
    }
    
    /// <summary>
    /// Wraps a method call with detailed exception handling
    /// </summary>
    public static void SafeExecute(Action action, string context = null)
    {
        try
        {
            action?.Invoke();
        }
        catch (NullReferenceException ex)
        {
            LogNullReferenceException(ex, context);
            throw; // Re-throw to maintain original behavior
        }
        catch (Exception ex)
        {
            LogDetailedException(ex, context);
            throw;
        }
    }
    
    /// <summary>
    /// Special handler for NullReferenceExceptions with component context
    /// </summary>
    private static void LogNullReferenceException(NullReferenceException ex, string context)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLConsoleLogger.LogError("════════════════════════════════════════");
        WebGLConsoleLogger.LogError("🔴 NULL REFERENCE EXCEPTION DETECTED");
        WebGLConsoleLogger.LogError("════════════════════════════════════════");
        
        if (!string.IsNullOrEmpty(context))
        {
            WebGLConsoleLogger.LogError($"Context: {context}");
        }
        
        WebGLConsoleLogger.LogError($"Message: {ex.Message}");
        
        // Parse stack trace to find the exact line
        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            var lines = ex.StackTrace.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("LoginUIController") || line.Contains("GameEventBus") || line.Contains("GameManager"))
                {
                    WebGLConsoleLogger.LogError($"📍 Critical Line: {line.Trim()}");
                }
            }
        }
        
        // Try to identify what was null
        AnalyzeNullReference(ex);
        
        // Check for spawn-related issues
        AnalyzeNullReferenceWithSpawnContext(ex);
        
        WebGLConsoleLogger.LogError("Full Stack Trace:");
        WebGLConsoleLogger.LogError(ex.StackTrace);
        WebGLConsoleLogger.LogError("════════════════════════════════════════");
#else
        Debug.LogError($"[NullRef] {context}: {ex.Message}");
        Debug.LogException(ex);
#endif
    }
    
    private static void LogDetailedException(Exception ex, string context)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLConsoleLogger.LogError($"[Exception] Type: {ex.GetType().Name}");
        WebGLConsoleLogger.LogError($"[Exception] Context: {context}");
        WebGLConsoleLogger.LogError($"[Exception] Message: {ex.Message}");
        WebGLConsoleLogger.LogError($"[Exception] Stack: {ex.StackTrace}");
#else
        Debug.LogError($"[Exception] {context}: {ex.Message}");
        Debug.LogException(ex);
#endif
    }
    
    /// <summary>
    /// Analyzes a NullReferenceException to identify what was null
    /// </summary>
    private static void AnalyzeNullReference(NullReferenceException ex)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLConsoleLogger.LogError("🔍 Analyzing Null Reference...");
        
        // Check common Unity null scenarios
        var stackTrace = ex.StackTrace;
        
        if (stackTrace.Contains("Subscribe"))
        {
            WebGLConsoleLogger.LogError("⚠️ Likely Cause: GameEventBus.Instance is null");
            WebGLConsoleLogger.LogError("   Fix: Ensure GameEventBus exists in scene");
        }
        else if (stackTrace.Contains("GetComponent"))
        {
            WebGLConsoleLogger.LogError("⚠️ Likely Cause: GetComponent returned null");
            WebGLConsoleLogger.LogError("   Fix: Check if component exists on GameObject");
        }
        else if (stackTrace.Contains("Instance"))
        {
            WebGLConsoleLogger.LogError("⚠️ Likely Cause: Singleton Instance is null");
            WebGLConsoleLogger.LogError("   Fix: Ensure singleton is initialized before use");
        }
        else if (stackTrace.Contains("Conn") || stackTrace.Contains("connection"))
        {
            WebGLConsoleLogger.LogError("⚠️ Likely Cause: SpacetimeDB connection is null");
            WebGLConsoleLogger.LogError("   Fix: Check GameManager.Conn initialization");
        }
#endif
    }
    
    /// <summary>
    /// Attaches debugging to LoginUI components
    /// </summary>
    private void AttachToLoginUIComponents()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        StartCoroutine(DelayedLoginUIAttachment());
#endif
    }
    
    private System.Collections.IEnumerator DelayedLoginUIAttachment()
    {
        yield return new WaitForSeconds(0.5f); // Wait for scene to load
        
        var loginUI = FindFirstObjectByType<LoginUIController>();
        if (loginUI != null)
        {
            WebGLConsoleLogger.Log("📎 Attaching debugger to LoginUIController");
            ValidateLoginUIReferences(loginUI);
        }
        else
        {
            WebGLConsoleLogger.LogError("❌ LoginUIController not found in scene!");
        }
        
        // Also check other critical components
        CheckCriticalSingletons();
    }
    
    /// <summary>
    /// Validates all references in LoginUIController
    /// </summary>
    private void ValidateLoginUIReferences(LoginUIController loginUI)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLConsoleLogger.Log("🔍 Validating LoginUIController References...");
        
        // Use reflection to check all fields
        var fields = loginUI.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        
        foreach (var field in fields)
        {
            var value = field.GetValue(loginUI);
            if (value == null)
            {
                WebGLConsoleLogger.LogError($"   ❌ NULL FIELD: {field.Name} ({field.FieldType.Name})");
            }
            else if (verboseComponentLogging)
            {
                WebGLConsoleLogger.Log($"   ✅ {field.Name}: {value.GetType().Name}");
            }
        }
        
        // Check specific Unity references
        var uiDocument = loginUI.GetComponent<UnityEngine.UIElements.UIDocument>();
        if (uiDocument == null)
        {
            WebGLConsoleLogger.LogError("   ❌ UIDocument component is missing!");
        }
        else
        {
            WebGLConsoleLogger.Log($"   ✅ UIDocument found");
            
            if (uiDocument.rootVisualElement == null)
            {
                WebGLConsoleLogger.LogError("   ❌ UIDocument.rootVisualElement is null!");
            }
            else
            {
                WebGLConsoleLogger.Log($"   ✅ Root visual element exists");
            }
        }
#endif
    }
    
    /// <summary>
    /// Checks critical singleton instances
    /// </summary>
    private void CheckCriticalSingletons()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLConsoleLogger.Log("🔍 Checking Critical Singletons...");
        
        // Check GameEventBus using reflection
        var eventBusType = Type.GetType("GameEventBus");
        if (eventBusType != null)
        {
            var instanceProperty = eventBusType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProperty != null)
            {
                var instance = instanceProperty.GetValue(null);
                if (instance == null)
                {
                    WebGLConsoleLogger.LogError("   ❌ GameEventBus.Instance is NULL!");
                }
                else
                {
                    WebGLConsoleLogger.Log($"   ✅ GameEventBus.Instance exists");
                    
                    var stateProperty = eventBusType.GetProperty("CurrentState");
                    if (stateProperty != null)
                    {
                        var state = stateProperty.GetValue(instance);
                        WebGLConsoleLogger.Log($"      State: {state}");
                    }
                }
            }
        }
        else
        {
            WebGLConsoleLogger.Log("   ℹ️ GameEventBus type not found");
        }
        
        // Check GameManager
        if (GameManager.Instance == null)
        {
            WebGLConsoleLogger.LogError("   ❌ GameManager.Instance is NULL!");
        }
        else
        {
            WebGLConsoleLogger.Log($"   ✅ GameManager.Instance exists");
            WebGLConsoleLogger.Log($"      Connected: {GameManager.IsConnected()}");
        }
        
        // Check GameData
        if (GameData.Instance == null)
        {
            WebGLConsoleLogger.LogError("   ❌ GameData.Instance is NULL!");
        }
        else
        {
            WebGLConsoleLogger.Log($"   ✅ GameData.Instance exists");
        }
#endif
    }
    
    /// <summary>
    /// Manual component inspection helper
    /// </summary>
    public static void InspectComponent(Component component)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (component == null)
        {
            WebGLConsoleLogger.LogError("[Inspect] Component is null!");
            return;
        }
        
        WebGLConsoleLogger.Log($"════════════════════════════════════════");
        WebGLConsoleLogger.Log($"📋 Inspecting: {component.GetType().Name}");
        WebGLConsoleLogger.Log($"GameObject: {component.gameObject.name}");
        WebGLConsoleLogger.Log($"Active: {component.gameObject.activeInHierarchy}");
        
        var fields = component.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        
        WebGLConsoleLogger.Log($"Fields ({fields.Length}):");
        foreach (var field in fields)
        {
            try
            {
                var value = field.GetValue(component);
                if (value == null)
                {
                    WebGLConsoleLogger.LogError($"  ❌ {field.Name}: NULL");
                }
                else
                {
                    WebGLConsoleLogger.Log($"  ✅ {field.Name}: {value}");
                }
            }
            catch (Exception ex)
            {
                WebGLConsoleLogger.LogError($"  ⚠️ {field.Name}: Error reading - {ex.Message}");
            }
        }
        WebGLConsoleLogger.Log($"════════════════════════════════════════");
#endif
    }
    
    /// <summary>
    /// Track player spawn events and positions
    /// </summary>
    public static void LogSpawnEvent(string playerName, Vector3 position, bool success = true)
    {
        spawnAttemptCount++;
        lastPlayerSpawnPosition = position;
        lastSpawnTime = Time.time;
        
        string message = $"[Spawn {spawnAttemptCount}] {playerName} at ({position.x:F2}, {position.y:F2}, {position.z:F2})";
        
        if (!success)
        {
            spawnErrorLog.Add($"[{Time.time:F1}] FAILED: {message}");
            if (spawnErrorLog.Count > 10) spawnErrorLog.RemoveAt(0);
        }
        
#if UNITY_WEBGL && !UNITY_EDITOR
        if (_instance != null && _instance.trackSpawnEvents)
        {
            if (success)
            {
                WebGLConsoleLogger.Log($"✅ {message}");
                
                // Validate spawn position
                float magnitude = position.magnitude;
                float expectedRadius = 3001f; // WORLD_RADIUS + SURFACE_OFFSET
                float error = Mathf.Abs(magnitude - expectedRadius);
                
                if (error > 5f)
                {
                    Debug.LogWarning($"⚠️ Spawn position not on sphere! Error: {error:F2} units");
                }
                
                if (position.magnitude < 10f)
                {
                    WebGLConsoleLogger.LogError($"❌ Spawn position too close to origin!");
                }
            }
            else
            {
                WebGLConsoleLogger.LogError($"❌ {message}");
            }
        }
#endif
    }
    
    /// <summary>
    /// Log spawn position validation results
    /// </summary>
    public static void LogSpawnValidation(Vector3 position, bool isValid, string issue = null)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (_instance != null && _instance.trackSpawnEvents)
        {
            if (isValid)
            {
                WebGLConsoleLogger.Log($"✅ Spawn position valid: ({position.x:F2}, {position.y:F2}, {position.z:F2})");
            }
            else
            {
                WebGLConsoleLogger.LogError($"❌ Invalid spawn position: {issue}");
                WebGLConsoleLogger.LogError($"   Position: ({position.x:F2}, {position.y:F2}, {position.z:F2})");
                WebGLConsoleLogger.LogError($"   Magnitude: {position.magnitude:F2}");
            }
        }
#endif
    }
    
    /// <summary>
    /// Get spawn debugging information
    /// </summary>
    public static string GetSpawnDebugInfo()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== Spawn Debug Info ===");
        sb.AppendLine($"Total Attempts: {spawnAttemptCount}");
        sb.AppendLine($"Last Position: {lastPlayerSpawnPosition}");
        sb.AppendLine($"Last Time: {lastSpawnTime:F1}s");
        sb.AppendLine($"Time Since Last: {(Time.time - lastSpawnTime):F1}s");
        
        if (spawnErrorLog.Count > 0)
        {
            sb.AppendLine("Recent Errors:");
            foreach (var error in spawnErrorLog)
            {
                sb.AppendLine($"  {error}");
            }
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Check if a position is valid for spawning
    /// </summary>
    public static bool IsValidSpawnPosition(Vector3 position, out string issue)
    {
        issue = null;
        
        // Check for NaN/Infinity
        if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z))
        {
            issue = "Position contains NaN";
            return false;
        }
        
        if (float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z))
        {
            issue = "Position contains Infinity";
            return false;
        }
        
        // Check magnitude
        float magnitude = position.magnitude;
        if (magnitude < 10f)
        {
            issue = $"Too close to origin (magnitude: {magnitude:F2})";
            return false;
        }
        
        // Check if on sphere surface (with tolerance)
        float expectedRadius = 3001f; // WORLD_RADIUS + SURFACE_OFFSET
        float error = Mathf.Abs(magnitude - expectedRadius);
        if (error > 10f)
        {
            issue = $"Not on sphere surface (error: {error:F2} units)";
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Enhanced exception analysis with spawn context
    /// </summary>
    private static void AnalyzeNullReferenceWithSpawnContext(NullReferenceException ex)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Check if exception is spawn-related
        if (ex.StackTrace != null && 
            (ex.StackTrace.Contains("SpawnPlayer") || 
             ex.StackTrace.Contains("PlayerController") ||
             ex.StackTrace.Contains("WorldManager")))
        {
            WebGLConsoleLogger.LogError("🎯 SPAWN-RELATED NULL REFERENCE");
            WebGLConsoleLogger.LogError(GetSpawnDebugInfo());
            
            // Check common spawn issues
            var worldManager = FindFirstObjectByType<WorldManager>();
            if (worldManager != null)
            {
                var worldRadius = worldManager.GetWorldRadius();
                WebGLConsoleLogger.Log($"World Radius: {worldRadius}");
                
                if (worldRadius <= 0)
                {
                    WebGLConsoleLogger.LogError("❌ Invalid world radius!");
                }
            }
            else
            {
                WebGLConsoleLogger.LogError("❌ WorldManager not found in scene during spawn!");
            }
            
            // Check for player prefabs
            var playerPrefab = Resources.Load("Prefabs/Player");
            if (playerPrefab == null)
            {
                WebGLConsoleLogger.LogError("❌ Player prefab not found in Resources!");
            }
        }
#endif
    }
}