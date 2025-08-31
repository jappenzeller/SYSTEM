using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

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
    
    private static WebGLExceptionDebugger _instance;
    
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
}