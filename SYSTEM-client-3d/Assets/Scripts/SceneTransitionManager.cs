using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpacetimeDB.Types;

/// <summary>
/// Manages scene transitions and world navigation in the quantum metaverse.
/// Handles smooth transitions between login, center world, and other worlds.
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Scene Names")]
    [Tooltip("Login scene name")]
    public string loginSceneName = "LoginScene";
    
    [Tooltip("Center world scene name")]
    public string centerWorldSceneName = "CenterWorldScene";
    
    [Tooltip("Generic world scene name for shell worlds")]
    public string worldSceneName = "WorldScene";

    [Header("Transition Settings")]
    [Tooltip("Fade duration for scene transitions")]
    public float fadeDuration = 1.0f;
    
    [Tooltip("Canvas for fade overlay")]
    public Canvas fadeCanvas;
    
    [Tooltip("Image component for fade effect")]
    public UnityEngine.UI.Image fadeImage;

    // Current world state
    public SpacetimeDB.Types.WorldCoords CurrentWorldCoords { get; private set; }
    public bool IsInCenterWorld => IsCenter(CurrentWorldCoords);
    
    // Transition state
    private bool isTransitioning = false;
    private Coroutine currentTransition;

    void Awake()
    {
        // Singleton pattern with DontDestroyOnLoad
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Initialize fade overlay
        SetupFadeOverlay();
        
        Debug.Log("SceneTransitionManager created");
    }

    void Start()
    {
        // Set initial world coordinates based on current scene
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == centerWorldSceneName)
        {
            CurrentWorldCoords = new SpacetimeDB.Types.WorldCoords { X = 0, Y = 0, Z = 0 };
        }
        else if (currentScene == worldSceneName)
        {
            // Try to get world coords from GameData or default to center
            CurrentWorldCoords = GameData.Instance?.GetCurrentWorldCoords() ?? new SpacetimeDB.Types.WorldCoords { X = 0, Y = 0, Z = 0 };
        }
        
        Debug.Log($"SceneTransitionManager ready in scene {currentScene}");
    }

    void SetupFadeOverlay()
    {
        if (fadeCanvas == null || fadeImage == null)
        {
            // Create fade overlay if not assigned
            GameObject fadeObj = new GameObject("FadeOverlay");
            DontDestroyOnLoad(fadeObj);
            
            fadeCanvas = fadeObj.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = 1000; // Ensure it's on top
            
            GameObject imageObj = new GameObject("FadeImage");
            imageObj.transform.SetParent(fadeCanvas.transform);
            
            fadeImage = imageObj.AddComponent<UnityEngine.UI.Image>();
            fadeImage.color = Color.black;
            
            // Set to full screen
            RectTransform rectTransform = fadeImage.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
        }
        
        // Start with transparent
        SetFadeAlpha(0f);
    }

    #region Public Transition Methods

    /// <summary>
    /// Transition to login scene
    /// </summary>
    public void TransitionToLogin()
    {
        if (isTransitioning) return;
        
        Debug.Log("Transitioning to login scene");
        StartTransition(loginSceneName, new SpacetimeDB.Types.WorldCoords { X = sbyte.MaxValue, Y = sbyte.MaxValue, Z = sbyte.MaxValue });
    }

    /// <summary>
    /// Transition to center world after successful login
    /// </summary>
    public void TransitionToCenterWorld()
    {
        if (isTransitioning) return;
        
        Debug.Log("Transitioning to center world");
        SpacetimeDB.Types.WorldCoords centerCoords = new SpacetimeDB.Types.WorldCoords { X = 0, Y = 0, Z = 0 };
        
        // Store in GameData for persistence
        if (GameData.Instance != null)
        {
            GameData.Instance.SetCurrentWorldCoords(centerCoords);
        }
        
        StartTransition(centerWorldSceneName, centerCoords);
    }

    /// <summary>
    /// Transition to a specific world (shell worlds or other special worlds)
    /// </summary>
    public void TransitionToWorld(SpacetimeDB.Types.WorldCoords targetCoords)
    {
        if (isTransitioning) return;
        
        Debug.Log($"Transitioning to world {targetCoords.X},{targetCoords.Y},{targetCoords.Z}");
        
        // Store in GameData for persistence
        if (GameData.Instance != null)
        {
            GameData.Instance.SetCurrentWorldCoords(targetCoords);
        }
        
        string targetScene = IsCenter(targetCoords) ? centerWorldSceneName : worldSceneName;
        StartTransition(targetScene, targetCoords);
    }

    /// <summary>
    /// Transition through a tunnel to another world
    /// </summary>
    public void TransitionThroughTunnel(ulong tunnelId)
    {
        if (isTransitioning) return;
        
        // Find the tunnel in the database
        if (GameManager.Conn?.Db?.Tunnel != null)
        {
            var tunnel = GameManager.Conn.Db.Tunnel.TunnelId.Find(tunnelId);
            if (tunnel != null && tunnel.Status == "Active")
            {
                Debug.Log($"Transitioning through tunnel {tunnelId} to world {tunnel.ToWorld.X},{tunnel.ToWorld.Y},{tunnel.ToWorld.Z}");
                
                // Play tunnel transition effect
                StartTunnelTransition(tunnel.ToWorld);
                return;
            }
        }
        
        Debug.LogWarning($"Cannot transition through tunnel {tunnelId} - tunnel not found or not active");
    }

    #endregion

    #region Transition Implementation

    void StartTransition(string targetScene, SpacetimeDB.Types.WorldCoords targetCoords)
    {
        if (currentTransition != null)
        {
            StopCoroutine(currentTransition);
        }
        currentTransition = StartCoroutine(TransitionCoroutine(targetScene, targetCoords));
    }

    void StartTunnelTransition(SpacetimeDB.Types.WorldCoords targetCoords)
    {
        if (currentTransition != null)
        {
            StopCoroutine(currentTransition);
        }
        currentTransition = StartCoroutine(TunnelTransitionCoroutine(targetCoords));
    }

    IEnumerator TransitionCoroutine(string targetScene, SpacetimeDB.Types.WorldCoords targetCoords)
    {
        isTransitioning = true;
        
        // Fade out
        yield return StartCoroutine(FadeOut());
        
        // Update world coordinates
        CurrentWorldCoords = targetCoords;
        
        // Load new scene
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetScene);
        loadOperation.allowSceneActivation = false;
        
        // Wait for scene to be ready
        while (loadOperation.progress < 0.9f)
        {
            yield return null;
        }
        
        // Small delay to ensure everything is ready
        yield return new WaitForSeconds(0.1f);
        
        // Activate the new scene
        loadOperation.allowSceneActivation = true;
        
        // Wait for scene activation
        yield return new WaitUntil(() => loadOperation.isDone);
        
        // Small delay for scene initialization
        yield return new WaitForSeconds(0.2f);
        
        // Fade in
        yield return StartCoroutine(FadeIn());
        
        isTransitioning = false;
        currentTransition = null;
        
        Debug.Log($"Scene transition complete: {targetScene}");
    }

    IEnumerator TunnelTransitionCoroutine(SpacetimeDB.Types.WorldCoords targetCoords)
    {
        isTransitioning = true;
        
        // Special tunnel transition effect
        yield return StartCoroutine(TunnelFadeOut());
        
        // Update world coordinates
        CurrentWorldCoords = targetCoords;
        
        // Store in GameData
        if (GameData.Instance != null)
        {
            GameData.Instance.SetCurrentWorldCoords(targetCoords);
        }
        
        // Determine target scene
        string targetScene = IsCenter(targetCoords) ? centerWorldSceneName : worldSceneName;
        
        // Load new scene
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetScene);
        loadOperation.allowSceneActivation = false;
        
        // Wait for scene to be ready
        while (loadOperation.progress < 0.9f)
        {
            yield return null;
        }
        
        yield return new WaitForSeconds(0.1f);
        
        // Activate the new scene
        loadOperation.allowSceneActivation = true;
        yield return new WaitUntil(() => loadOperation.isDone);
        yield return new WaitForSeconds(0.2f);
        
        // Special tunnel fade in
        yield return StartCoroutine(TunnelFadeIn());
        
        isTransitioning = false;
        currentTransition = null;
        
        Debug.Log($"Tunnel transition complete to world {targetCoords.X},{targetCoords.Y},{targetCoords.Z}");
    }

    #endregion

    #region Fade Effects

    IEnumerator FadeOut()
    {
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);
            SetFadeAlpha(alpha);
            yield return null;
        }
        SetFadeAlpha(1f);
    }

// In SceneTransitionManager.cs, make sure FadeIn completes properly:
    IEnumerator FadeIn()
    {
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            SetFadeAlpha(alpha);
            yield return null;
        }
        SetFadeAlpha(0f);
        
        // IMPORTANT: Ensure time is restored
        Time.timeScale = 1f;
        
        // If there's a player, ensure cursor is locked
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    IEnumerator TunnelFadeOut()
    {
        // More dramatic tunnel effect - could add swirl or quantum effects
        float timer = 0f;
        while (timer < fadeDuration * 1.5f)
        {
            timer += Time.deltaTime;
            float progress = timer / (fadeDuration * 1.5f);
            
            // Create a swirling tunnel effect
            float alpha = Mathf.Lerp(0f, 1f, progress);
            Color tunnelColor = Color.Lerp(Color.black, Color.cyan, Mathf.Sin(progress * Mathf.PI * 3f) * 0.3f + 0.7f);
            tunnelColor.a = alpha;
            SetFadeColor(tunnelColor);
            yield return null;
        }
        SetFadeColor(Color.cyan);
    }

    IEnumerator TunnelFadeIn()
    {
        float timer = 0f;
        while (timer < fadeDuration * 1.5f)
        {
            timer += Time.deltaTime;
            float progress = timer / (fadeDuration * 1.5f);
            
            // Fade from cyan tunnel effect to transparent
            float alpha = Mathf.Lerp(1f, 0f, progress);
            Color tunnelColor = Color.Lerp(Color.cyan, Color.black, progress);
            tunnelColor.a = alpha;
            SetFadeColor(tunnelColor);
            yield return null;
        }
        SetFadeColor(Color.clear);
    }

    void SetFadeAlpha(float alpha)
    {
        if (fadeImage != null)
        {
            Color color = fadeImage.color;
            color.a = alpha;
            fadeImage.color = color;
        }
    }

    void SetFadeColor(Color color)
    {
        if (fadeImage != null)
        {
            fadeImage.color = color;
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Check if world coordinates represent the center world
    /// </summary>
    public static bool IsCenter(SpacetimeDB.Types.WorldCoords coords)
    {
        return coords.X == 0 && coords.Y == 0 && coords.Z == 0;
    }

    /// <summary>
    /// Get the current scene type
    /// </summary>
    public SceneType GetCurrentSceneType()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        
        if (currentScene == loginSceneName)
            return SceneType.Login;
        else if (currentScene == centerWorldSceneName)
            return SceneType.CenterWorld;
        else if (currentScene == worldSceneName)
            return SceneType.World;
        else
            return SceneType.Unknown;
    }

    /// <summary>
    /// Check if we can transition (not already transitioning and connected)
    /// </summary>
    public bool CanTransition()
    {
        return !isTransitioning && GameManager.IsConnected();
    }

    #endregion

    #region Events

    /// <summary>
    /// Called when player enters a new world (from SpacetimeDB events)
    /// </summary>
    public void OnPlayerWorldChanged(SpacetimeDB.Types.WorldCoords newWorldCoords)
    {
        // Safety check - make sure we exist and are initialized
        if (Instance == null || this == null)
        {
            Debug.LogWarning("SceneTransitionManager instance is null, cannot handle world change");
            return;
        }

        // Check if we're already transitioning
        if (isTransitioning)
        {
            Debug.Log($"Already transitioning, ignoring world change to {newWorldCoords.X},{newWorldCoords.Y},{newWorldCoords.Z}");
            return;
        }

        // Check if we're already in this world
        if (CurrentWorldCoords.X == newWorldCoords.X && 
            CurrentWorldCoords.Y == newWorldCoords.Y && 
            CurrentWorldCoords.Z == newWorldCoords.Z)
        {
            Debug.Log($"Already in world {newWorldCoords.X},{newWorldCoords.Y},{newWorldCoords.Z}");
            return;
        }
        
        Debug.Log($"Player world changed to {newWorldCoords.X},{newWorldCoords.Y},{newWorldCoords.Z}");
        
        // Update our current coordinates
        CurrentWorldCoords = newWorldCoords;
        
        // Only auto-transition if the scene doesn't match the world
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string expectedScene = IsCenter(newWorldCoords) ? centerWorldSceneName : worldSceneName;
        
        if (currentScene != expectedScene)
        {
            Debug.Log($"Scene mismatch: current={currentScene}, expected={expectedScene}. Starting transition.");
            TransitionToWorld(newWorldCoords);
        }
        else
        {
            Debug.Log($"Already in correct scene {currentScene} for world {newWorldCoords.X},{newWorldCoords.Y},{newWorldCoords.Z}");
        }
    }

    #endregion

    void OnDestroy()
    {
        if (currentTransition != null)
        {
            StopCoroutine(currentTransition);
        }
        
        if (Instance == this)
        {
            Instance = null;
        }
    }
}

/// <summary>
/// Scene types for the quantum metaverse
/// </summary>
public enum SceneType
{
    Unknown,
    Login,
    CenterWorld,
    World
}