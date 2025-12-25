using UnityEngine;
using System;
using System.IO;

/// <summary>
/// Centralized debug logging system with category-based filtering
/// Logs to both Unity console and file (Logs directory)
/// </summary>
public static class SystemDebug
{
    [System.Flags]
    public enum Category
    {
        None = 0,
        Connection = 1 << 0,        // [Connection] SpacetimeDB connection events
        EventBus = 1 << 1,          // [EventBus] Event publishing/subscription
        WavePacketSystem = 1 << 2,         // [WavePacketSystem] Wave packet source database events and loading
        PlayerSystem = 1 << 3,       // [PlayerSystem] Player events and tracking
        WorldSystem = 1 << 4,        // [WorldSystem] World loading and transitions
        Mining = 1 << 5,             // [Mining] Mining system events
        Session = 1 << 6,            // [Session] Login/logout/session management
        Subscription = 1 << 7,       // [Subscription] Table subscriptions
        Reducer = 1 << 8,            // [Reducer] Reducer calls and responses
        Network = 1 << 9,            // [Network] Network traffic and sync
        Performance = 1 << 10,       // [Performance] Performance metrics
        SourceVisualization = 1 << 11,  // [SourceVisualization] Source GameObject creation and rendering
        SpireSystem = 1 << 12,       // [SpireSystem] Energy spire database events and loading
        SpireVisualization = 1 << 13, // [SpireVisualization] Spire GameObject creation and rendering
        Input = 1 << 14,             // [Input] Cursor control and input system events
        StorageSystem = 1 << 15,     // [StorageSystem] Storage device placement and logic
        StorageVisualization = 1 << 16, // [StorageVisualization] Storage device GameObject creation and rendering
        All = ~0                     // All categories
    }

    // Global debug settings - can be overridden per component
    private static Category enabledCategories = Category.None;
    private static bool initialized = false;

    // File logging
    private static StreamWriter logFile;
    private static string logFilePath;
    private static readonly object fileLock = new object();

    /// <summary>
    /// Initialize with default settings
    /// </summary>
    public static void Initialize(Category defaultCategories = Category.None)
    {
        enabledCategories = defaultCategories;
        initialized = true;
        InitializeFileLogging();
    }

    private static void InitializeFileLogging()
    {
        try
        {
            // Create Logs directory in project root
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string logsDir = Path.Combine(projectRoot, "Logs");

            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }

            // Create log file with timestamp
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            logFilePath = Path.Combine(logsDir, $"SystemLog_{timestamp}.txt");

            logFile = new StreamWriter(logFilePath, false); // false = overwrite if exists
            logFile.AutoFlush = true; // Flush immediately for crash safety

            UnityEngine.Debug.Log($"[SystemDebug] Logging to: {logFilePath}");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[SystemDebug] Failed to initialize file logging: {e.Message}");
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Cleanup()
    {
        if (logFile != null)
        {
            lock (fileLock)
            {
                logFile.Close();
                logFile = null;
            }
        }
        initialized = false;  // Reset so next Log() call reinitializes file logging
    }

    /// <summary>
    /// Enable specific debug categories globally
    /// </summary>
    public static void EnableCategories(Category categories)
    {
        enabledCategories |= categories;
    }

    /// <summary>
    /// Disable specific debug categories globally
    /// </summary>
    public static void DisableCategories(Category categories)
    {
        enabledCategories &= ~categories;
    }

    /// <summary>
    /// Set exact categories to enable
    /// </summary>
    public static void SetCategories(Category categories)
    {
        enabledCategories = categories;
    }

    /// <summary>
    /// Log a debug message. Always writes to file, only writes to console if category enabled.
    /// </summary>
    public static void Log(Category category, string message, UnityEngine.Object context = null)
    {
        if (!initialized) Initialize();

        string prefix = GetCategoryPrefix(category);
        string formattedMessage = $"{prefix} {message}";

        // ALWAYS write to file (captures everything for post-hoc debugging)
        WriteToFile("LOG", formattedMessage);

        // Only log to console if category enabled (DebugController controls this)
        if ((enabledCategories & category) != 0)
        {
            if (context != null)
                UnityEngine.Debug.Log(formattedMessage, context);
            else
                UnityEngine.Debug.Log(formattedMessage);
        }
    }

    /// <summary>
    /// Log a warning. Always writes to file, only writes to console if category enabled.
    /// </summary>
    public static void LogWarning(Category category, string message, UnityEngine.Object context = null)
    {
        if (!initialized) Initialize();

        string prefix = GetCategoryPrefix(category);
        string formattedMessage = $"{prefix} {message}";

        // ALWAYS write to file (captures everything for post-hoc debugging)
        WriteToFile("WARN", formattedMessage);

        // Only log to console if category enabled (DebugController controls this)
        if ((enabledCategories & category) != 0)
        {
            if (context != null)
                UnityEngine.Debug.LogWarning(formattedMessage, context);
            else
                UnityEngine.Debug.LogWarning(formattedMessage);
        }
    }

    /// <summary>
    /// Log an error. Always writes to file, only writes to console if category enabled.
    /// </summary>
    public static void LogError(Category category, string message, UnityEngine.Object context = null)
    {
        if (!initialized) Initialize();

        string prefix = GetCategoryPrefix(category);
        string formattedMessage = $"{prefix} {message}";

        // ALWAYS write to file (captures everything for post-hoc debugging)
        WriteToFile("ERROR", formattedMessage);

        // Only log to console if category enabled (DebugController controls this)
        if ((enabledCategories & category) != 0)
        {
            if (context != null)
                UnityEngine.Debug.LogError(formattedMessage, context);
            else
                UnityEngine.Debug.LogError(formattedMessage);
        }
    }

    /// <summary>
    /// Check if a category is enabled
    /// </summary>
    public static bool IsCategoryEnabled(Category category)
    {
        return (enabledCategories & category) != 0;
    }

    private static void WriteToFile(string level, string message)
    {
        if (logFile == null) return;

        try
        {
            lock (fileLock)
            {
                // Human-readable timestamp
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                // Unix timestamp in milliseconds (matches server ctx.timestamp format)
                long unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                logFile.WriteLine($"[{timestamp}] [{unixMs}] [{level}] {message}");
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[SystemDebug] Failed to write to log file: {e.Message}");
        }
    }

    private static string GetCategoryPrefix(Category category)
    {
        // Handle single category (most common case)
        switch (category)
        {
            case Category.Connection: return "[Connection]";
            case Category.EventBus: return "[EventBus]";
            case Category.WavePacketSystem: return "[WavePacketSystem]";
            case Category.PlayerSystem: return "[PlayerSystem]";
            case Category.WorldSystem: return "[WorldSystem]";
            case Category.Mining: return "[Mining]";
            case Category.Session: return "[Session]";
            case Category.Subscription: return "[Subscription]";
            case Category.Reducer: return "[Reducer]";
            case Category.Network: return "[Network]";
            case Category.Performance: return "[Performance]";
            case Category.SourceVisualization: return "[SourceVisualization]";
            case Category.SpireSystem: return "[SpireSystem]";
            case Category.Input: return "[Input]";
            case Category.SpireVisualization: return "[SpireVisualization]";
            case Category.StorageSystem: return "[StorageSystem]";
            case Category.StorageVisualization: return "[StorageVisualization]";
            default:
                // Handle multiple categories
                if ((category & Category.All) == Category.All)
                    return "[All]";
                return "[System]";
        }
    }
}