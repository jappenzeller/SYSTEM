using UnityEngine;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// Redirects Unity Debug logs and exceptions to browser console in WebGL builds.
/// Provides full stack traces and detailed error information for debugging.
/// </summary>
public class WebGLConsoleLogger : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    
    // JavaScript interop functions
    [DllImport("__Internal")]
    private static extern void JSConsoleLog(string message);
    
    [DllImport("__Internal")]
    private static extern void JSConsoleWarn(string message);
    
    [DllImport("__Internal")]
    private static extern void JSConsoleError(string message);
    
    [DllImport("__Internal")]
    private static extern void JSConsoleGroup(string label);
    
    [DllImport("__Internal")]
    private static extern void JSConsoleGroupEnd();
    
    [DllImport("__Internal")]
    private static extern void JSConsoleTable(string jsonData);
    
#endif

    [Header("Configuration")]
    [SerializeField] private bool enableStackTraces = true;
    [SerializeField] private bool enableTimestamps = true;
    [SerializeField] private bool captureUnhandledExceptions = true;
    [SerializeField] private bool verboseMode = false;
    
    [Header("Filtering")]
    [SerializeField] private bool logDebugMessages = true;
    [SerializeField] private bool logWarnings = true;
    [SerializeField] private bool logErrors = true;
    [SerializeField] private List<string> ignoredMessages = new List<string>();
    
    private static WebGLConsoleLogger _instance;
    private Queue<LogEntry> _logQueue = new Queue<LogEntry>();
    private bool _isProcessing = false;
    
    private struct LogEntry
    {
        public string message;
        public string stackTrace;
        public LogType type;
        public DateTime timestamp;
    }
    
    void Awake()
    {
        // Singleton pattern
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
#if UNITY_WEBGL && !UNITY_EDITOR
        InitializeWebGLLogging();
#else
        if (verboseMode)
        {
            Debug.Log("[WebGLConsoleLogger] Running in Editor/Standalone - WebGL logging disabled");
        }
#endif
    }
    
#if UNITY_WEBGL && !UNITY_EDITOR
    
    private void InitializeWebGLLogging()
    {
        JSConsoleGroup("🎮 Unity WebGL Console Logger Initialized");
        JSConsoleLog($"Unity Version: {Application.unityVersion}");
        JSConsoleLog($"Platform: {Application.platform}");
        JSConsoleLog($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        JSConsoleLog($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        JSConsoleGroupEnd();
        
        // Subscribe to Unity log events
        Application.logMessageReceived += HandleLog;
        Application.logMessageReceivedThreaded += HandleLogThreaded;
        
        // Subscribe to unhandled exceptions
        if (captureUnhandledExceptions)
        {
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
        }
        
        JSConsoleLog("✅ WebGL Console Logger is now capturing Unity logs");
    }
    
    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Filter out ignored messages
        foreach (var ignored in ignoredMessages)
        {
            if (!string.IsNullOrEmpty(ignored) && logString.Contains(ignored))
                return;
        }
        
        // Check if we should log this type
        if (type == LogType.Log && !logDebugMessages) return;
        if (type == LogType.Warning && !logWarnings) return;
        if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !logErrors) return;
        
        // Format the message
        string formattedMessage = FormatLogMessage(logString, stackTrace, type);
        
        // Send to browser console
        switch (type)
        {
            case LogType.Error:
            case LogType.Exception:
            case LogType.Assert:
                JSConsoleError(formattedMessage);
                if (enableStackTraces && !string.IsNullOrEmpty(stackTrace))
                {
                    JSConsoleError(FormatStackTrace(stackTrace));
                }
                break;
                
            case LogType.Warning:
                JSConsoleWarn(formattedMessage);
                break;
                
            default:
                JSConsoleLog(formattedMessage);
                break;
        }
    }
    
    private void HandleLogThreaded(string logString, string stackTrace, LogType type)
    {
        // Queue logs from other threads to be processed on main thread
        lock (_logQueue)
        {
            _logQueue.Enqueue(new LogEntry
            {
                message = logString,
                stackTrace = stackTrace,
                type = type,
                timestamp = DateTime.Now
            });
        }
    }
    
    private void HandleUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception ex)
        {
            JSConsoleGroup("💥 UNHANDLED EXCEPTION");
            JSConsoleError($"Exception Type: {ex.GetType().FullName}");
            JSConsoleError($"Message: {ex.Message}");
            
            if (ex.InnerException != null)
            {
                JSConsoleError($"Inner Exception: {ex.InnerException.Message}");
            }
            
            JSConsoleError("Stack Trace:");
            JSConsoleError(FormatStackTrace(ex.StackTrace));
            
            // Try to provide context
            if (ex.TargetSite != null)
            {
                JSConsoleError($"Target Method: {ex.TargetSite.Name}");
                JSConsoleError($"Target Class: {ex.TargetSite.DeclaringType?.FullName}");
            }
            
            JSConsoleError($"Is Terminating: {args.IsTerminating}");
            JSConsoleGroupEnd();
        }
        else
        {
            JSConsoleError($"Unhandled exception (non-Exception type): {args.ExceptionObject}");
        }
    }
    
    private string FormatLogMessage(string message, string stackTrace, LogType type)
    {
        var sb = new StringBuilder();
        
        // Add timestamp if enabled
        if (enableTimestamps)
        {
            sb.Append($"[{DateTime.Now:HH:mm:ss.fff}] ");
        }
        
        // Add Unity prefix
        switch (type)
        {
            case LogType.Error:
                sb.Append("[Unity ERROR] ");
                break;
            case LogType.Exception:
                sb.Append("[Unity EXCEPTION] ");
                break;
            case LogType.Assert:
                sb.Append("[Unity ASSERT] ");
                break;
            case LogType.Warning:
                sb.Append("[Unity WARN] ");
                break;
            default:
                sb.Append("[Unity] ");
                break;
        }
        
        sb.Append(message);
        
        return sb.ToString();
    }
    
    private string FormatStackTrace(string stackTrace)
    {
        if (string.IsNullOrEmpty(stackTrace))
            return "";
        
        // Format Unity stack trace for better readability in browser console
        var lines = stackTrace.Split('\n');
        var sb = new StringBuilder();
        sb.AppendLine("📍 Stack Trace:");
        
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                // Clean up the line
                var cleanLine = line.Trim();
                
                // Highlight important parts
                if (cleanLine.Contains("LoginUIController"))
                {
                    sb.AppendLine($"  ⚠️ {cleanLine}");
                }
                else if (cleanLine.Contains("at "))
                {
                    sb.AppendLine($"    {cleanLine}");
                }
                else
                {
                    sb.AppendLine($"  {cleanLine}");
                }
            }
        }
        
        return sb.ToString();
    }
    
    void Update()
    {
        // Process queued logs from other threads
        if (_logQueue.Count > 0 && !_isProcessing)
        {
            _isProcessing = true;
            
            lock (_logQueue)
            {
                while (_logQueue.Count > 0)
                {
                    var entry = _logQueue.Dequeue();
                    HandleLog(entry.message, entry.stackTrace, entry.type);
                }
            }
            
            _isProcessing = false;
        }
    }
    
    void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
        Application.logMessageReceivedThreaded -= HandleLogThreaded;
        
        if (captureUnhandledExceptions)
        {
            AppDomain.CurrentDomain.UnhandledException -= HandleUnhandledException;
        }
    }
    
#endif

    /// <summary>
    /// Manual log method that works in all platforms
    /// </summary>
    public static void Log(string message)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (_instance != null)
        {
            JSConsoleLog($"[Manual] {message}");
        }
#else
        Debug.Log(message);
#endif
    }
    
    /// <summary>
    /// Manual error log method
    /// </summary>
    public static void LogError(string message)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (_instance != null)
        {
            JSConsoleError($"[Manual] {message}");
        }
#else
        Debug.LogError(message);
#endif
    }
    
    /// <summary>
    /// Log exception with context
    /// </summary>
    public static void LogException(Exception ex, UnityEngine.Object context = null)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (_instance != null)
        {
            JSConsoleGroup($"🔴 Exception: {ex.GetType().Name}");
            JSConsoleError($"Message: {ex.Message}");
            
            if (context != null)
            {
                JSConsoleError($"Context: {context.name} ({context.GetType().Name})");
            }
            
            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                JSConsoleError(_instance.FormatStackTrace(ex.StackTrace));
            }
            
            JSConsoleGroupEnd();
        }
#else
        if (context != null)
            Debug.LogException(ex, context);
        else
            Debug.LogException(ex);
#endif
    }
}