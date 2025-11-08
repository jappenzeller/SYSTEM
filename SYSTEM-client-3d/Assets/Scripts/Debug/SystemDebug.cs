using UnityEngine;
using System;

/// <summary>
/// Centralized debug logging system with category-based filtering
/// </summary>
public static class SystemDebug
{
    [System.Flags]
    public enum Category
    {
        None = 0,
        Connection = 1 << 0,        // [Connection] SpacetimeDB connection events
        EventBus = 1 << 1,          // [EventBus] Event publishing/subscription
        OrbSystem = 1 << 2,         // [OrbSystem] Orb database events and loading
        PlayerSystem = 1 << 3,       // [PlayerSystem] Player events and tracking
        WorldSystem = 1 << 4,        // [WorldSystem] World loading and transitions
        Mining = 1 << 5,             // [Mining] Mining system events
        Session = 1 << 6,            // [Session] Login/logout/session management
        Subscription = 1 << 7,       // [Subscription] Table subscriptions
        Reducer = 1 << 8,            // [Reducer] Reducer calls and responses
        Network = 1 << 9,            // [Network] Network traffic and sync
        Performance = 1 << 10,       // [Performance] Performance metrics
        OrbVisualization = 1 << 11,  // [OrbVisualization] Orb GameObject creation and rendering
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

    /// <summary>
    /// Initialize with default settings
    /// </summary>
    public static void Initialize(Category defaultCategories = Category.None)
    {
        enabledCategories = defaultCategories;
        initialized = true;
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
    /// Log a debug message if the category is enabled
    /// </summary>
    public static void Log(Category category, string message, UnityEngine.Object context = null)
    {
        if (!initialized) Initialize();

        if ((enabledCategories & category) != 0)
        {
            string prefix = GetCategoryPrefix(category);
            if (context != null)
                UnityEngine.Debug.Log($"{prefix} {message}", context);
            else
                UnityEngine.Debug.Log($"{prefix} {message}");
        }
    }

    /// <summary>
    /// Log a warning if the category is enabled
    /// </summary>
    public static void LogWarning(Category category, string message, UnityEngine.Object context = null)
    {
        if (!initialized) Initialize();

        if ((enabledCategories & category) != 0)
        {
            string prefix = GetCategoryPrefix(category);
            if (context != null)
                UnityEngine.Debug.LogWarning($"{prefix} {message}", context);
            else
                UnityEngine.Debug.LogWarning($"{prefix} {message}");
        }
    }

    /// <summary>
    /// Log an error if the category is enabled
    /// </summary>
    public static void LogError(Category category, string message, UnityEngine.Object context = null)
    {
        if (!initialized) Initialize();

        if ((enabledCategories & category) != 0)
        {
            string prefix = GetCategoryPrefix(category);
            if (context != null)
                UnityEngine.Debug.LogError($"{prefix} {message}", context);
            else
                UnityEngine.Debug.LogError($"{prefix} {message}");
        }
    }

    /// <summary>
    /// Check if a category is enabled
    /// </summary>
    public static bool IsCategoryEnabled(Category category)
    {
        return (enabledCategories & category) != 0;
    }

    private static string GetCategoryPrefix(Category category)
    {
        // Handle single category (most common case)
        switch (category)
        {
            case Category.Connection: return "[Connection]";
            case Category.EventBus: return "[EventBus]";
            case Category.OrbSystem: return "[OrbSystem]";
            case Category.PlayerSystem: return "[PlayerSystem]";
            case Category.WorldSystem: return "[WorldSystem]";
            case Category.Mining: return "[Mining]";
            case Category.Session: return "[Session]";
            case Category.Subscription: return "[Subscription]";
            case Category.Reducer: return "[Reducer]";
            case Category.Network: return "[Network]";
            case Category.Performance: return "[Performance]";
            case Category.OrbVisualization: return "[OrbVisualization]";
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