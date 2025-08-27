using UnityEngine;
using System;

/// <summary>
/// Loads runtime configuration that was saved during the build process
/// </summary>
public static class RuntimeConfig
{
    [Serializable]
    public class Configuration
    {
        public string environment = "local";
        public string moduleAddress = "127.0.0.1:3000";
        public string moduleName = "system";
        public bool enableDebugLogging = true;
        public bool useSSL = false;
    }

    private static Configuration _config;
    private static bool _loaded = false;

    public static Configuration Config
    {
        get
        {
            if (!_loaded)
            {
                LoadConfiguration();
            }
            return _config;
        }
    }

    private static void LoadConfiguration()
    {
        // Try to load runtime config from Resources
        var configAsset = Resources.Load<TextAsset>("runtime_config");
        
        if (configAsset != null)
        {
            try
            {
                _config = JsonUtility.FromJson<Configuration>(configAsset.text);
                Debug.Log($"[RuntimeConfig] Loaded {_config.environment} environment configuration");
                Debug.Log($"[RuntimeConfig] Server: {_config.moduleAddress}/{_config.moduleName}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[RuntimeConfig] Failed to parse runtime_config.json: {e.Message}");
                _config = GetDefaultConfig();
            }
        }
        else
        {
            Debug.LogWarning("[RuntimeConfig] No runtime_config.json found in Resources, using defaults");
            _config = GetDefaultConfig();
        }

        // Determine if SSL should be used based on address
        if (_config.moduleAddress.Contains("spacetimedb.com"))
        {
            _config.useSSL = true;
        }

        _loaded = true;
    }

    private static Configuration GetDefaultConfig()
    {
        return new Configuration
        {
            environment = "local",
            moduleAddress = "127.0.0.1:3000",
            moduleName = "system",
            enableDebugLogging = true,
            useSSL = false
        };
    }

    /// <summary>
    /// Get the full connection URL including protocol
    /// </summary>
    public static string GetConnectionUrl()
    {
        var cfg = Config;
        string protocol = cfg.useSSL ? "https://" : "http://";
        return $"{protocol}{cfg.moduleAddress}";
    }
}