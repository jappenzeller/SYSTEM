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
    public bool IsTransitioning => isTransitioning;
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
        
        Debug.Log("[SceneTransitionManager] Created");
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
        
        // Subscribe to EventBus state changes
        SubscribeToEvents();
        
        Debug.Log($"[SceneTransitionManager] Ready in scene {currentScene}");
    }

    void OnDestroy()
    {
        UnsubscribeFromEvents();
        
        if (currentTransition != null)
        {
            StopCoroutine(currentTransition);
        }
        
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #region EventBus Integration

    void SubscribeToEvents()
    {
        // Subscribe to state changes to handle scene transitions
        GameEventBus.Instance.Subscribe<StateChangedEvent>(OnStateChanged);
        
        // Subscribe to connection lost to return to login
        GameEventBus.Instance.Subscribe<ConnectionLostEvent>(OnConnectionLost);
        
        Debug.Log("[SceneTransitionManager] Subscribed to EventBus");
    }

    void UnsubscribeFromEvents()
    {
        if (GameEventBus.Instance != null)
        {
            GameEventBus.Instance.Unsubscribe<StateChangedEvent>(OnStateChanged);
            GameEventBus.Instance.Unsubscribe<ConnectionLostEvent>(OnConnectionLost);
        }
    }

    void OnStateChanged(StateChangedEvent e)
    {
        Debug.Log($"[SceneTransitionManager] State changed: {e.OldState} → {e.NewState}");
        
        // Handle scene transitions based on state changes
        switch (e.NewState)
        {
            case GameEventBus.GameState.InGame:
                // Player is ready and authenticated, transition to game
                if (SceneManager.GetActiveScene().name != centerWorldSceneName)
                {
                    Debug.Log("[SceneTransitionManager] State is InGame, transitioning to center world");
                    TransitionToCenterWorld();
                }
                break;
                
            case GameEventBus.GameState.Disconnected:
                // Lost connection or logged out, return to login
                if (SceneManager.GetActiveScene().name != loginSceneName)
                {
                    Debug.Log("[SceneTransitionManager] State is Disconnected, returning to login");
                    TransitionToLogin();
                }
                break;
        }
    }

    void OnConnectionLost(ConnectionLostEvent e)
    {
        Debug.Log($"[SceneTransitionManager] Connection lost: {e.Reason}");
        // The state should transition to Disconnected, which will trigger scene change
    }

    #endregion

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
        
        Debug.Log("[SceneTransitionManager] Transitioning to login scene");
        StartTransition(loginSceneName, new SpacetimeDB.Types.WorldCoords { X = sbyte.MaxValue, Y = sbyte.MaxValue, Z = sbyte.MaxValue });
    }

    /// <summary>
    /// Transition to center world after successful login
    /// </summary>
    public void TransitionToCenterWorld()
    {
        if (isTransitioning) return;
        
        Debug.Log("[SceneTransitionManager] Transitioning to center world");
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
        
        Debug.Log($"[SceneTransitionManager] Transitioning to world {targetCoords.X},{targetCoords.Y},{targetCoords.Z}");
        
        string targetScene = IsCenter(targetCoords) ? centerWorldSceneName : worldSceneName;
        StartTransition(targetScene, targetCoords);
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

    IEnumerator TransitionCoroutine(string targetScene, SpacetimeDB.Types.WorldCoords targetCoords)
    {
        isTransitioning = true;
        
        // Publish scene load started event
        GameEventBus.Instance.Publish(new SceneLoadStartedEvent
        {
            SceneName = targetScene,
            TargetCoords = targetCoords
        });
        
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
        
        // Publish scene loaded event
        GameEventBus.Instance.Publish(new SceneLoadCompletedEvent
        {
            SceneName = targetScene,
            WorldCoords = targetCoords
        });
        
        Debug.Log($"[SceneTransitionManager] Scene transition complete: {targetScene}");
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
    /// Check if we can transition (not already transitioning)
    /// </summary>
    public bool CanTransition()
    {
        return !isTransitioning;
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
            Debug.LogWarning("[SceneTransitionManager] Instance is null, cannot handle world change");
            return;
        }

        // Check if we're already transitioning
        if (isTransitioning)
        {
            Debug.Log($"[SceneTransitionManager] Already transitioning, ignoring world change to {newWorldCoords.X},{newWorldCoords.Y},{newWorldCoords.Z}");
            return;
        }

        // Check if we're already in this world
        if (CurrentWorldCoords.X == newWorldCoords.X && 
            CurrentWorldCoords.Y == newWorldCoords.Y && 
            CurrentWorldCoords.Z == newWorldCoords.Z)
        {
            Debug.Log($"[SceneTransitionManager] Already in world {newWorldCoords.X},{newWorldCoords.Y},{newWorldCoords.Z}");
            return;
        }
        
        Debug.Log($"[SceneTransitionManager] Player world changed to {newWorldCoords.X},{newWorldCoords.Y},{newWorldCoords.Z}");
        
        // Update our current coordinates
        CurrentWorldCoords = newWorldCoords;
        
        // Only auto-transition if the scene doesn't match the world
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string expectedScene = IsCenter(newWorldCoords) ? centerWorldSceneName : worldSceneName;
        
        if (currentScene != expectedScene)
        {
            Debug.Log($"[SceneTransitionManager] Scene mismatch: current={currentScene}, expected={expectedScene}. Starting transition.");
            TransitionToWorld(newWorldCoords);
        }
        else
        {
            Debug.Log($"[SceneTransitionManager] Already in correct scene {currentScene} for world {newWorldCoords.X},{newWorldCoords.Y},{newWorldCoords.Z}");
        }
    }

    #endregion
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