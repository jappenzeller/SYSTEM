using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using SpacetimeDB.Types;

public class WebGLDebugOverlay : MonoBehaviour
{
    private string debugText = "";
    private GUIStyle style;
    
    void Start()
    {
        style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.white;
        style.normal.background = Texture2D.whiteTexture;
        
        StartCoroutine(UpdateDebugInfo());
    }
    
    IEnumerator UpdateDebugInfo()
    {
        while (true)
        {
            debugText = $"=== SYSTEM Debug ===\n";
            debugText += $"Time: {Time.time:F1}\n";
            debugText += $"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}\n";
            
            // Environment info based on runtime platform detection
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                debugText += $"Environment: Test (WebGL Runtime)\n";
                debugText += $"Server: maincloud.spacetimedb.com/system-test\n";
            }
            else if (Application.isEditor)
            {
                debugText += $"Environment: Local (Editor)\n";
                debugText += $"Server: 127.0.0.1:3000/system\n";
            }
            else
            {
                debugText += $"Environment: Production (Standalone)\n";
                debugText += $"Server: maincloud.spacetimedb.com/system\n";
            }
            debugText += $"Platform: {Application.platform}\n";
            
            // GameManager info
            if (GameManager.Instance != null)
            {
                debugText += $"GameManager: Found\n";
                debugText += $"Connected: {GameManager.IsConnected()}\n";
                debugText += $"LocalPlayer: {GameManager.GetLocalPlayer()?.Name ?? "NULL"}\n";
            }
            else
            {
                debugText += "GameManager: NULL\n";
            }
            
            // EventBus state
            if (GameEventBus.Instance != null)
            {
                debugText += $"EventBus State: {GameEventBus.Instance.CurrentState}\n";
            }
            
            // WorldManager info
            var worldManager = FindObjectOfType<WorldManager>();
            if (worldManager != null)
            {
                debugText += "WorldManager: Found\n";
            }
            else
            {
                debugText += "WorldManager: NULL\n";
            }
            
            // Player count
            var players = FindObjectsOfType<PlayerController>();
            debugText += $"Players in scene: {players.Length}\n";
            
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    void OnGUI()
    {
        GUI.backgroundColor = new Color(0, 0, 0, 0.8f);
        GUI.Box(new Rect(10, 10, 300, 200), debugText, style);
    }
}