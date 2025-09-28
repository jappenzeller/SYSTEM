using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System.Collections;

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
    private static BuildConfigData _config = new BuildConfigData(); // Initialize with default to avoid null
    private static bool _isLoaded = false;
    private static bool _isLoading = false;
    
    public static BuildConfigData Config
    {
        get
        {
            if (!_isLoaded && !_isLoading)
            {
                // Debug.LogWarning("[BuildConfiguration] Config accessed before loading completed, using defaults");
                UseDefaultConfiguration();
            }
            return _config;
        }
    }
    
    public static void LoadConfiguration()
    {
        // WebGL Debug
        #if UNITY_WEBGL && !UNITY_EDITOR
        // Debug.Log($"[BuildConfiguration] WebGL: LoadConfiguration called, _isLoading={_isLoading}, _isLoaded={_isLoaded}");
        #endif

        if (_isLoading || _isLoaded)
        {
            return;
        }
        
        _isLoading = true;
        
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL requires using UnityWebRequest for StreamingAssets
        // Debug.Log("[BuildConfiguration] WebGL: Using WebGL loading method");
        LoadConfigurationWebGL();
#else
        // Standalone and Editor can use direct file access
        LoadConfigurationStandalone();
#endif
    }
    
    private static void LoadConfigurationStandalone()
    {
        string configPath = Path.Combine(Application.streamingAssetsPath, "build-config.json");
        // Debug.Log($"[BuildConfiguration] Loading config from: {configPath}");
        
        if (File.Exists(configPath))
        {
            try
            {
                string json = File.ReadAllText(configPath);
                // Debug.Log($"[BuildConfiguration] Read JSON: {json}");
                
                _config = JsonUtility.FromJson<BuildConfigData>(json);
                _isLoaded = true;
                _isLoading = false;
                
                LogConfiguration("Loaded");
            }
            catch (System.Exception e)
            {
                // Debug.LogError($"[BuildConfiguration] Failed to load build configuration: {e.Message}");
                UseDefaultConfiguration();
            }
        }
        else
        {
            // Debug.LogWarning($"[BuildConfiguration] No build configuration found at {configPath}, using defaults");
            UseDefaultConfiguration();
        }
    }
    
    private static void LoadConfigurationWebGL()
    {
        try
        {
            // In WebGL, we need to use a coroutine helper
            // Debug.Log("[BuildConfiguration] WebGL: Creating BuildConfigLoader GameObject");
            var go = new GameObject("BuildConfigLoader");
            var loader = go.AddComponent<BuildConfigLoader>();
            
            if (loader == null)
            {
                // Debug.LogError("[BuildConfiguration] WebGL: Failed to add BuildConfigLoader component!");
                UseDefaultConfiguration();
                return;
            }
            
            // Debug.Log("[BuildConfiguration] WebGL: Starting coroutine");
            loader.StartCoroutine(LoadConfigurationWebGLCoroutine(loader));
        }
        catch (System.Exception e)
        {
            // Debug.LogError($"[BuildConfiguration] WebGL: Exception in LoadConfigurationWebGL: {e.Message}");
            // Debug.LogError($"[BuildConfiguration] WebGL: Stack trace: {e.StackTrace}");
            UseDefaultConfiguration();
        }
    }
    
    private static IEnumerator LoadConfigurationWebGLCoroutine(BuildConfigLoader loader)
    {
        // In WebGL, Application.streamingAssetsPath returns the full URL path
        // We need to use a relative path from the web root instead
        string configPath = "StreamingAssets/build-config.json";
        
        // If streamingAssetsPath contains http, use it as base URL
        if (Application.streamingAssetsPath.StartsWith("http"))
        {
            // Application.streamingAssetsPath might include extra path segments
            // Just use relative path from web root
            configPath = "StreamingAssets/build-config.json";
        }
        
        // Debug.Log($"[BuildConfiguration] Loading WebGL config from: {configPath}");
        // Debug.Log($"[BuildConfiguration] StreamingAssets path: {Application.streamingAssetsPath}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(configPath))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    // Debug.Log($"[BuildConfiguration] WebGL loaded JSON: {json}");
                    
                    _config = JsonUtility.FromJson<BuildConfigData>(json);
                    _isLoaded = true;
                    _isLoading = false;
                    
                    LogConfiguration("WebGL Loaded");
                }
                catch (System.Exception e)
                {
                    // Debug.LogError($"[BuildConfiguration] Failed to parse WebGL config: {e.Message}");
                    UseDefaultConfiguration();
                }
            }
            else
            {
                // Debug.LogWarning($"[BuildConfiguration] WebGL config load failed: {request.error}");
                // Debug.LogWarning($"[BuildConfiguration] Attempted URL: {request.url}");
                // Debug.LogWarning($"[BuildConfiguration] Response Code: {request.responseCode}");
                UseDefaultConfiguration();
            }
        }
        
        // Clean up the temporary GameObject
        if (loader != null && loader.gameObject != null)
        {
            Object.Destroy(loader.gameObject);
        }
    }
    
    private static void UseDefaultConfiguration()
    {
        // Debug.Log("[BuildConfiguration] Using default configuration");
        
        if (_config == null)
        {
            _config = new BuildConfigData();
        }
        
        // Fallback to runtime detection for backwards compatibility
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            // For WebGL without config, check if we're running locally
            string currentUrl = Application.absoluteURL;
            // Debug.Log($"[BuildConfiguration] WebGL URL: {currentUrl}");
            
            if (!string.IsNullOrEmpty(currentUrl) && 
                (currentUrl.Contains("localhost") || currentUrl.Contains("127.0.0.1")))
            {
                // Running locally
                _config.environment = "local";
                _config.serverUrl = "http://127.0.0.1:3000";
                _config.moduleName = "system";
                _config.enableDebugLogging = true;
                _config.developmentBuild = true;
            }
            else
            {
                // Default to test for WebGL
                _config.environment = "test";
                _config.serverUrl = "https://maincloud.spacetimedb.com";
                _config.moduleName = "system-test";
            }
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
        _isLoading = false;
        LogConfiguration("Using defaults");
    }
    
    private static void LogConfiguration(string source)
    {
        // Debug.Log($"[BuildConfiguration] {source} - Environment: {_config.environment}");
        // Debug.Log($"[BuildConfiguration] Server URL: {_config.serverUrl}");
        // Debug.Log($"[BuildConfiguration] Module: {_config.moduleName}");
        // Debug.Log($"[BuildConfiguration] Debug Logging: {_config.enableDebugLogging}");
        // Debug.Log($"[BuildConfiguration] Development Build: {_config.developmentBuild}");
    }
    
    // Helper MonoBehaviour for WebGL coroutine
    private class BuildConfigLoader : MonoBehaviour { }
}