using UnityEngine;
using System.Collections;

public class BuildConfigDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    public bool autoLoadOnStart = false;  // Disabled by default
    public bool showDebugUI = false;       // Disabled by default
    
    [Header("Runtime Info")]
    [SerializeField] private string loadedEnvironment = "Not Loaded";
    [SerializeField] private string loadedServerUrl = "Not Loaded";
    [SerializeField] private string loadedModuleName = "Not Loaded";
    [SerializeField] private bool configLoaded = false;
    
    private GUIStyle boxStyle;
    
    void Start()
    {
        // Debug logging removed - enable showDebugUI to see configuration
        
        if (autoLoadOnStart)
        {
            StartCoroutine(LoadAndDisplayConfig());
        }
    }
    
    IEnumerator LoadAndDisplayConfig()
    {
        
        // Load configuration
        BuildConfiguration.LoadConfiguration();
        
        // Wait a bit for async loading in WebGL
        yield return new WaitForSeconds(1f);
        
        // Get the loaded config
        var config = BuildConfiguration.Config;
        
        if (config != null)
        {
            loadedEnvironment = config.environment;
            loadedServerUrl = config.serverUrl;
            loadedModuleName = config.moduleName;
            configLoaded = true;
            
            // Config loaded successfully
        }
        else
        {
            Debug.LogError("Failed to load build configuration!");
        }
    }
    
    void OnGUI()
    {
        if (!showDebugUI) return;
        
        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.alignment = TextAnchor.UpperLeft;
            boxStyle.fontSize = 12;
            boxStyle.normal.textColor = Color.white;
            boxStyle.normal.background = Texture2D.whiteTexture;
        }
        
        // Create semi-transparent background
        GUI.backgroundColor = new Color(0, 0, 0, 0.8f);
        
        // Display debug info
        float width = 400;
        float height = 200;
        float x = 10;
        float y = 10;
        
        GUILayout.BeginArea(new Rect(x, y, width, height));
        GUILayout.BeginVertical(boxStyle);
        
        GUILayout.Label($"<b>Build Configuration Debug</b>", new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Label($"Platform: {Application.platform}");
        GUILayout.Label($"Config Loaded: {(configLoaded ? "✅ Yes" : "❌ No")}");
        GUILayout.Space(10);
        
        if (configLoaded)
        {
            GUILayout.Label($"<b>Environment:</b> {loadedEnvironment}", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label($"<b>Server URL:</b> {loadedServerUrl}", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label($"<b>Module:</b> {loadedModuleName}", new GUIStyle(GUI.skin.label) { richText = true });
        }
        else
        {
            if (GUILayout.Button("Reload Configuration"))
            {
                StartCoroutine(LoadAndDisplayConfig());
            }
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
        
        GUI.backgroundColor = Color.white;
    }
    
    [ContextMenu("Force Reload Configuration")]
    public void ForceReloadConfiguration()
    {
        StartCoroutine(LoadAndDisplayConfig());
    }
}