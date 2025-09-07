using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using SpacetimeDB.Types;
using UnityEngine.InputSystem;
using SYSTEM.Game;

public class WebGLDebugOverlay : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool showDebugUI = true;
    [SerializeField] private bool minimalMode = true;
    
    private string debugText = "";
    private GUIStyle boxStyle;
    private GUIStyle textStyle;
    private bool isVisible = true;
    
    void Start()
    {
        InitializeStyles();
        StartCoroutine(UpdateDebugInfo());
    }
    
    void InitializeStyles()
    {
        boxStyle = new GUIStyle();
        boxStyle.normal.background = CreateTexture(new Color(0, 0, 0, 0.7f));
        boxStyle.padding = new RectOffset(5, 5, 5, 5);
        
        textStyle = new GUIStyle();
        textStyle.fontSize = 12;
        textStyle.normal.textColor = Color.white;
        textStyle.fontStyle = FontStyle.Normal;
    }
    
    Texture2D CreateTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
    
    IEnumerator UpdateDebugInfo()
    {
        while (true)
        {
            if (showDebugUI && isVisible)
            {
                UpdateDebugText();
            }
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    void UpdateDebugText()
    {
        if (minimalMode)
        {
            // Minimal mode - single line with essential info
            BuildMinimalDebugText();
        }
        else
        {
            // Normal mode - more detailed but still concise
            BuildNormalDebugText();
        }
    }
    
    void BuildMinimalDebugText()
    {
        string connection = GameManager.IsConnected() ? "Connected" : "Disconnected";
        string env = GetEnvironmentName();
        string player = GameManager.GetLocalPlayer()?.Name ?? "None";
        string state = GameEventBus.Instance?.CurrentState.ToString() ?? "Unknown";
        
        debugText = $"{connection} | {env} | {player} | {state}";
    }
    
    void BuildNormalDebugText()
    {
        debugText = "=== SYSTEM ===\n";
        
        // State and connection on same line
        string state = GameEventBus.Instance?.CurrentState.ToString() ?? "Unknown";
        string connection = GameManager.IsConnected() ? "Connected" : "Disconnected";
        debugText += $"State: {state} | {connection}\n";
        
        // Environment (simplified)
        debugText += $"Env: {GetEnvironmentName()}\n";
        
        // Player info (only if connected)
        var player = GameManager.GetLocalPlayer();
        if (player != null)
        {
            debugText += $"Player: {player.Name}\n";
            
            // World info (only if player exists)
            var worldManager = FindFirstObjectByType<WorldManager>();
            if (worldManager != null && worldManager.GetCurrentWorldData() != null)
            {
                var world = worldManager.GetCurrentWorldData();
                var coords = worldManager.GetCurrentWorldCoords();
                debugText += $"World: {world.WorldName} ({coords.X},{coords.Y},{coords.Z})\n";
            }
        }
        
        // Scene (only if different from expected)
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName != "CenterWorldScene" && sceneName != "Login")
        {
            debugText += $"Scene: {sceneName}\n";
        }
    }
    
    string GetEnvironmentName()
    {
        if (Application.platform == RuntimePlatform.WebGLPlayer)
            return "Test";
        else if (Application.isEditor)
            return "Local";
        else
            return "Prod";
    }
    
    void Update()
    {
        // Toggle visibility with F3 using new Input System
        if (Keyboard.current?.f3Key.wasPressedThisFrame ?? false)
        {
            isVisible = !isVisible;
        }
        
        // Toggle minimal mode with F4 using new Input System
        if (Keyboard.current?.f4Key.wasPressedThisFrame ?? false)
        {
            minimalMode = !minimalMode;
        }
    }
    
    void OnGUI()
    {
        if (!showDebugUI || !isVisible) return;
        
        if (minimalMode)
        {
            // Minimal mode - top center, small
            float width = 250;
            float height = 20;
            float x = (Screen.width - width) / 2;
            float y = 5;
            
            GUI.Box(new Rect(x, y, width, height), "", boxStyle);
            GUI.Label(new Rect(x + 5, y + 2, width - 10, height - 4), debugText, textStyle);
        }
        else
        {
            // Normal mode - top left corner
            float width = 200;
            float height = 80;
            
            GUI.Box(new Rect(10, 10, width, height), "", boxStyle);
            GUI.Label(new Rect(15, 12, width - 10, height - 5), debugText, textStyle);
        }
    }
}