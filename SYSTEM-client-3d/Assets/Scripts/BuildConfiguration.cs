using UnityEngine;
using System.IO;

[System.Serializable]
public class BuildConfigData
{
    public string environment = "local";
    public string serverUrl = "http://127.0.0.1:3000";
    public string moduleName = "system";
    public bool enableDebugLogging = true;
    public bool developmentBuild = true;
}

public static class BuildConfiguration
{
    private static BuildConfigData _config;
    private static bool _isLoaded = false;
    
    public static BuildConfigData Config
    {
        get
        {
            if (!_isLoaded)
            {
                LoadConfiguration();
            }
            return _config;
        }
    }
    
    public static void LoadConfiguration()
    {
        string configPath = Path.Combine(Application.streamingAssetsPath, "build-config.json");
        
        if (File.Exists(configPath))
        {
            try
            {
                string json = File.ReadAllText(configPath);
                _config = JsonUtility.FromJson<BuildConfigData>(json);
                _isLoaded = true;
                
                Debug.Log($"[BuildConfiguration] Loaded configuration for {_config.environment} environment");
                Debug.Log($"[BuildConfiguration] Server: {_config.serverUrl}");
                Debug.Log($"[BuildConfiguration] Module: {_config.moduleName}");
                Debug.Log($"[BuildConfiguration] Debug Logging: {_config.enableDebugLogging}");
                Debug.Log($"[BuildConfiguration] Development Build: {_config.developmentBuild}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BuildConfiguration] Failed to load build configuration: {e.Message}");
                UseDefaultConfiguration();
            }
        }
        else
        {
            Debug.LogWarning($"[BuildConfiguration] No build configuration found at {configPath}, using defaults");
            UseDefaultConfiguration();
        }
    }
    
    private static void UseDefaultConfiguration()
    {
        _config = new BuildConfigData();
        
        // Fallback to runtime detection for backwards compatibility
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            _config.environment = "test";
            _config.serverUrl = "https://maincloud.spacetimedb.com";
            _config.moduleName = "system-test";
        }
        else if (Application.isEditor)
        {
            _config.environment = "local";
            _config.serverUrl = "http://127.0.0.1:3000";
            _config.moduleName = "system";
            _config.enableDebugLogging = true;
            _config.developmentBuild = true;
        }
        else
        {
            _config.environment = "production";
            _config.serverUrl = "https://maincloud.spacetimedb.com";
            _config.moduleName = "system";
            _config.enableDebugLogging = false;
            _config.developmentBuild = false;
        }
        
        _isLoaded = true;
        Debug.Log($"[BuildConfiguration] Using default configuration for {_config.environment} environment");
    }
}