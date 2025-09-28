using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpacetimeDB;
using SpacetimeDB.Types;

namespace SYSTEM.Game
{
    /// <summary>
    /// Central manager for SpacetimeDB connection and game state.
    /// Now only responds to EventBus events - all SpacetimeDB interactions go through EventBridge.
    /// No direct UI coupling - everything through EventBus.
    /// </summary>
    public class GameManager : MonoBehaviour
{
    private static GameManager instance;
    public static GameManager Instance => instance;

    [Header("Connection Settings (Editor Override Only)")]
    [Tooltip("These values are only used in Unity Editor. WebGL builds always use system-test, Standalone uses production.")]
    [SerializeField] private string moduleAddress = "127.0.0.1:3000";
    [SerializeField] private string moduleName = "system";
    [SerializeField] private bool useSSL = false;

    [Header("Scene Settings")]
    [SerializeField] private string loginSceneName = "LoginScene";
    [SerializeField] private string gameSceneName = "GameScene";

    // Connection state
    private DbConnection conn;
    private bool isConnecting = false;
    private bool isReconnecting = false;

    // References
    private GameData gameData;
    
    // Properties
    public static bool IsConnected() => instance?.conn != null && instance.conn.IsActive;
    public static DbConnection Conn => instance?.conn;
    public static Identity? LocalIdentity => instance?.conn?.Identity;

    // Events (for systems that need to know about connection state)
    public static event Action OnConnected;
    public static event Action OnDisconnected;
    public static event Action<string> OnConnectionError;
    public static event Action<Player> OnLocalPlayerReady;
    
    // Local player cache
    private Player localPlayer;
    
    // System readiness
    private bool isInitialized = false;

    #region Unity Lifecycle

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // Get GameData reference
        gameData = GameData.Instance;
    }

    void Start()
    {
        // UnityEngine.Debug.Log("[GameManager] Start() called");
        
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // GameEventBus guaranteed to exist via RuntimeInitializeOnLoadMethod
        SubscribeToEvents();
        
        // Start connection coroutine
        StartCoroutine(ConnectToServer());
    }

    private void SubscribeToEvents()
    {
        if (GameEventBus.Instance != null)
        {
            GameEventBus.Instance.Subscribe<LocalPlayerReadyEvent>(OnLocalPlayerReadyEvent);
            GameEventBus.Instance.Subscribe<ConnectionLostEvent>(OnConnectionLostEvent);
            GameEventBus.Instance.Subscribe<LocalPlayerNotFoundEvent>(OnLocalPlayerNotFoundEvent);
            GameEventBus.Instance.Subscribe<SubscriptionReadyEvent>(OnSubscriptionReadyEvent);
            
            // Mark as initialized
            isInitialized = true;
        }
        else
        {
            // UnityEngine.Debug.LogError("[GameManager] GameEventBus.Instance is null when trying to subscribe!");
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        // Unsubscribe from FrameTickManager events
        if (FrameTickManager.Instance != null)
        {
            FrameTickManager.Instance.OnTickCompleted -= OnFrameTickCompleted;
        }

        if (conn != null && conn.IsActive)
        {
            conn.Disconnect();
        }

        // Unsubscribe from EventBus
        if (GameEventBus.Instance != null)
        {
            GameEventBus.Instance.Unsubscribe<LocalPlayerReadyEvent>(OnLocalPlayerReadyEvent);
            GameEventBus.Instance.Unsubscribe<ConnectionLostEvent>(OnConnectionLostEvent);
            GameEventBus.Instance.Unsubscribe<LocalPlayerNotFoundEvent>(OnLocalPlayerNotFoundEvent);
            GameEventBus.Instance.Unsubscribe<SubscriptionReadyEvent>(OnSubscriptionReadyEvent);
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus && conn != null && !conn.IsActive && !isReconnecting)
        {
            StartCoroutine(ReconnectToServer());
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && conn != null && !conn.IsActive && !isReconnecting)
        {
            StartCoroutine(ReconnectToServer());
        }
    }

    #endregion

    #region EventBus Event Handlers

    private void OnLocalPlayerReadyEvent(LocalPlayerReadyEvent evt)
    {
        // UnityEngine.Debug.Log($"[GameManager] Local player ready via EventBus: {evt.Player.Name}");
        
        // Cache the local player
        localPlayer = evt.Player;
        
        // Update GameData with current world coordinates
        if (GameData.Instance != null)
        {
            GameData.Instance.SetCurrentWorldCoords(evt.Player.CurrentWorld);
        }
        
        // Notify any listeners
        OnLocalPlayerReady?.Invoke(evt.Player);
        
        // Don't load scene here - let LoginUIController handle it
        // This avoids duplicate scene loading
    }

    private void OnConnectionLostEvent(ConnectionLostEvent evt)
    {
        // UnityEngine.Debug.Log($"[GameManager] Connection lost via EventBus: {evt.Reason}");
        
        OnDisconnected?.Invoke();
        
        // Start reconnection if needed
        if (!isReconnecting)
        {
            StartCoroutine(ReconnectToServer());
        }
    }

    private void OnLocalPlayerNotFoundEvent(LocalPlayerNotFoundEvent evt)
    {
        // UnityEngine.Debug.Log("[GameManager] No local player found");
        // LoginUIController will handle showing the login UI
    }

    private void OnSubscriptionReadyEvent(SubscriptionReadyEvent evt)
    {
        // UnityEngine.Debug.Log("[GameManager] Subscription ready via EventBus");
        // EventBridge will check for local player
    }

    #endregion

    #region Public Methods

    public static void CreatePlayer(string username)
    {
        if (!IsConnected())
        {
            // UnityEngine.Debug.LogError("[GameManager] Cannot create player - not connected");
            return;
        }

        // UnityEngine.Debug.Log($"[GameManager] Creating player: {username}");
        
        // Publish player creation started event
        GameEventBus.Instance.Publish(new PlayerCreationStartedEvent
        {
            Username = username
        });

        // Call the CreatePlayer reducer
        instance.conn.Reducers.CreatePlayer(username);
    }

    public static void LoadGameScene()
    {
        if (instance != null)
        {
            // UnityEngine.Debug.Log("[GameManager] LoadGameScene called - using SceneTransitionManager");
            
            // Get player's current world
            var player = GetLocalPlayer();
            if (player != null)
            {
                // Use SceneTransitionManager for proper transition
                if (SceneTransitionManager.Instance != null)
                {
                    SceneTransitionManager.Instance.TransitionToWorld(player.CurrentWorld);
                }
                else
                {
                    // UnityEngine.Debug.LogError("[GameManager] SceneTransitionManager not available!");
                    // Fallback to direct scene load
                    instance.StartCoroutine(instance.LoadSceneAsync(instance.gameSceneName));
                }
            }
            else
            {
                // UnityEngine.Debug.LogError("[GameManager] Cannot load game scene - no local player!");
            }
        }
    }

    public static void LoadLoginScene()
    {
        if (instance != null)
        {
            instance.StartCoroutine(instance.LoadSceneAsync(instance.loginSceneName));
        }
    }

    public static Player GetLocalPlayer()
    {
        // Return cached local player if available
        if (instance?.localPlayer != null)
        {
            return instance.localPlayer;
        }

        // Otherwise try to find it
        if (!IsConnected() || !LocalIdentity.HasValue)
        {
            return null;
        }

        foreach (var player in instance.conn.Db.Player.Iter())
        {
            if (player.Identity == LocalIdentity.Value)
            {
                instance.localPlayer = player;
                return player;
            }
        }

        return null;
    }

    public static void Logout()
    {
        // UnityEngine.Debug.Log("[GameManager] Logging out...");

        // Clear local data
        if (instance != null)
        {
            // Call logout reducer first if connected
            if (instance.conn != null && instance.conn.IsActive)
            {
                try
                {
                    // UnityEngine.Debug.Log("[GameManager] Calling logout reducer...");
                    instance.conn.Reducers.Logout();
                    
                    // Give it a moment to process
                    System.Threading.Thread.Sleep(100);
                }
                catch (Exception e)
                {
                    // UnityEngine.Debug.LogWarning($"[GameManager] Failed to call logout reducer: {e.Message}");
                }
                
                // Then disconnect
                instance.conn.Disconnect();
            }
            
            // Log position before clearing
            if (instance.localPlayer != null)
            {
                UnityEngine.Debug.Log($"[GameManager] Logging out player '{instance.localPlayer.Name}' from position: " +
                    $"World({instance.localPlayer.CurrentWorld.X},{instance.localPlayer.CurrentWorld.Y},{instance.localPlayer.CurrentWorld.Z}), " +
                    $"Pos({instance.localPlayer.Position.X:F2},{instance.localPlayer.Position.Y:F2},{instance.localPlayer.Position.Z:F2})");
            }
            
            // Clear local player reference
            instance.localPlayer = null;
            
            // Clear saved session
            AuthToken.ClearSession();
            
            // Clear game data
            GameData.Instance?.ClearSession();
            
            // Publish logout event
            GameEventBus.Instance.Publish(new LogoutEvent());
            
            // Load login scene
            LoadLoginScene();
        }
    }

    #endregion

    #region Scene Management

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // UnityEngine.Debug.Log($"[GameManager] Scene loaded: {scene.name}");
        
        if (scene.name == gameSceneName)
        {
            // Game scene loaded
            GameEventBus.Instance.Publish(new SceneLoadedEvent
            {
                SceneName = scene.name,
                IsGameScene = true
            });
        }
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }

    #endregion

    #region Connection Management

    private IEnumerator ConnectToServer()
    {
        // WebGL Debug: Comprehensive null checking at start
        #if UNITY_WEBGL && !UNITY_EDITOR
        // UnityEngine.Debug.Log("[GameManager] WebGL: ConnectToServer started");
        
        try
        {
            // Check if this method is even running on the correct instance
            // UnityEngine.Debug.Log($"[GameManager] WebGL: this == null? {this == null}");
            // UnityEngine.Debug.Log($"[GameManager] WebGL: this == instance? {this == instance}");
            // UnityEngine.Debug.Log($"[GameManager] WebGL: instance is null? {instance == null}");
            
            // These might be the issue - accessing member variables
            if (instance != null)
            {
                // UnityEngine.Debug.Log($"[GameManager] WebGL: isConnecting? {isConnecting}");
                // UnityEngine.Debug.Log($"[GameManager] WebGL: conn is null? {conn == null}");
            }
            else
            {
                // UnityEngine.Debug.LogError("[GameManager] WebGL: instance is null! Cannot access member variables!");
                yield break;
            }
        }
        catch (Exception e)
        {
            // UnityEngine.Debug.LogError($"[GameManager] WebGL: Error checking initial state: {e.Message}");
            // UnityEngine.Debug.LogError($"[GameManager] WebGL: Stack trace: {e.StackTrace}");
        }
        #endif

        // Additional safety check for instance
        if (instance == null)
        {
            // UnityEngine.Debug.LogError("[GameManager] Instance is null in ConnectToServer! This should never happen!");
            yield break;
        }

        if (isConnecting || (conn != null && conn.IsActive))
        {
            // UnityEngine.Debug.Log("[GameManager] Already connecting or connected, skipping");
            yield break;
        }

        isConnecting = true;
        
        // WebGL Debug: Check BuildConfiguration before loading
        #if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            // UnityEngine.Debug.Log("[GameManager] WebGL: About to load BuildConfiguration");
            // UnityEngine.Debug.Log($"[GameManager] WebGL: Application.streamingAssetsPath = {Application.streamingAssetsPath}");
        }
        catch (Exception e)
        {
            // UnityEngine.Debug.LogError($"[GameManager] WebGL: Error accessing streamingAssetsPath: {e.Message}");
        }
        #endif
        
        // Load build configuration with error handling
        try
        {
            BuildConfiguration.LoadConfiguration();
        }
        catch (Exception e)
        {
            // UnityEngine.Debug.LogError($"[GameManager] Failed to load BuildConfiguration: {e.Message}");
            // Use fallback configuration
        }
        
        // WebGL: Wait a bit for async config loading
        #if UNITY_WEBGL && !UNITY_EDITOR
        yield return new WaitForSeconds(0.5f);
        // UnityEngine.Debug.Log("[GameManager] WebGL: Waited for BuildConfiguration to load");
        #endif
        
        var config = BuildConfiguration.Config;
        
        // WebGL Debug: Check config
        #if UNITY_WEBGL && !UNITY_EDITOR
        // UnityEngine.Debug.Log($"[GameManager] WebGL: config is null? {config == null}");
        if (config != null)
        {
            // UnityEngine.Debug.Log($"[GameManager] WebGL: config.serverUrl = {config.serverUrl}");
            // UnityEngine.Debug.Log($"[GameManager] WebGL: config.moduleName = {config.moduleName}");
        }
        #endif
        
        string url;
        string module;
        string environment;
        
        // Use build-time configuration if available, otherwise fall back to editor settings
        if (Application.isEditor && !System.IO.File.Exists(System.IO.Path.Combine(Application.streamingAssetsPath, "build-config.json")))
        {
            // Editor without build config - use inspector values
            if (moduleAddress != "127.0.0.1:3000" || moduleName != "system")
            {
                // Use inspector overrides if they've been changed
                string protocol = useSSL ? "https://" : "http://";
                url = $"{protocol}{moduleAddress}";
                module = moduleName;
                environment = "Custom (Editor Override)";
            }
            else
            {
                // Default local development
                url = "http://127.0.0.1:3000";
                module = "system";
                environment = "Local (Editor)";
            }
            
            // UnityEngine.Debug.Log($"[GameManager] Using editor settings (no build config found)");
        }
        else
        {
            // Use build-time configuration
            if (config != null)
            {
                url = config.serverUrl ?? "http://127.0.0.1:3000";
                module = config.moduleName ?? "system";
                string env = config.environment ?? "local";
                environment = $"{char.ToUpper(env[0])}{env.Substring(1)} (Build Config)";
            }
            else
            {
                // Fallback if config is null
                // UnityEngine.Debug.LogError("[GameManager] BuildConfiguration.Config is null! Using hardcoded defaults");
                url = "http://127.0.0.1:3000";
                module = "system";
                environment = "Local (Fallback)";
            }
            
            // UnityEngine.Debug.Log($"[GameManager] Using build configuration from StreamingAssets");
        }
        
        // UnityEngine.Debug.Log($"[GameManager] Environment: {environment}");
        // UnityEngine.Debug.Log($"[GameManager] Connecting to SpacetimeDB at {url}/{module}...");
        
        // WebGL Debug: Check GameEventBus before using it
        #if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            // UnityEngine.Debug.Log($"[GameManager] WebGL: GameEventBus.Instance is null? {GameEventBus.Instance == null}");
            if (GameEventBus.Instance != null)
            {
                // UnityEngine.Debug.Log($"[GameManager] WebGL: GameEventBus.CurrentState = {GameEventBus.Instance.CurrentState}");
            }
        }
        catch (Exception e)
        {
            // UnityEngine.Debug.LogError($"[GameManager] WebGL: Error accessing GameEventBus: {e.Message}");
        }
        #endif
        
        // Check GameEventBus exists before using it
        if (GameEventBus.Instance != null)
        {
            // UnityEngine.Debug.Log($"[GameManager] Current EventBus state: {GameEventBus.Instance.CurrentState}");
            
            // Publish connection started event
            // UnityEngine.Debug.Log("[GameManager] Publishing ConnectionStartedEvent");
            GameEventBus.Instance.Publish(new ConnectionStartedEvent());
        }
        else
        {
            // UnityEngine.Debug.LogError("[GameManager] GameEventBus.Instance is null! Cannot publish events!");
        }

        // Get saved token if exists
        string savedToken = null;
        try
        {
            savedToken = AuthToken.LoadToken();
        }
        catch (Exception e)
        {
            // UnityEngine.Debug.LogError($"[GameManager] Error loading auth token: {e.Message}");
        }

        conn = DbConnection.Builder()
            .WithUri(url)
            .WithModuleName(module)
            .OnConnect(HandleConnected)
            .OnConnectError(HandleConnectError)
            .OnDisconnect(HandleDisconnected)
            .Build();

        // Set up reducer event handlers BEFORE connecting
        SetupReducerHandlers();

        // Wait a frame to ensure everything is initialized
        yield return null;

        isConnecting = false;
    }

    private IEnumerator ReconnectToServer()
    {
        if (isReconnecting || isConnecting || (conn != null && conn.IsActive))
        {
            yield break;
        }

        isReconnecting = true;

        // Wait a moment before reconnecting
        yield return new WaitForSeconds(2f);

        // Disconnect if needed
        if (conn != null && !conn.IsActive)
        {
            conn.Disconnect();
            conn = null;
        }

        // Try to connect again
        yield return StartCoroutine(ConnectToServer());

        isReconnecting = false;
    }

    #endregion

    #region Connection Event Handlers

    private void HandleConnected(DbConnection connection, Identity identity, string token)
    {
        // UnityEngine.Debug.Log($"Connected to SpacetimeDB! Identity: {identity}");
        
        // Initialize FrameTickManager with the connection
        InitializeFrameTicking();
        
        // Save token if needed
        if (!string.IsNullOrEmpty(token))
        {
            AuthToken.SaveToken(token);
        }
        
        // Notify connected listeners
        OnConnected?.Invoke();
        
        // SpacetimeDBEventBridge will handle everything else
    }

    private void HandleConnectError(Exception error)
    {
        // UnityEngine.Debug.LogError($"Failed to connect: {error.Message}");
        isConnecting = false;
        OnConnectionError?.Invoke(error.Message);
        
        // Publish connection failed event
        GameEventBus.Instance.Publish(new ConnectionFailedEvent
        {
            Error = error.Message
        });
    }

    private void HandleDisconnected(DbConnection connection, Exception error)
    {
        // UnityEngine.Debug.Log($"Disconnected from server. Error: {error?.Message ?? "None"}");
        
        OnDisconnected?.Invoke();
        
        // Publish connection lost event will be handled by EventBridge
    }

    #endregion

    #region Reducer Handlers

    private void SetupReducerHandlers()
    {
        // Login handlers
        conn.Reducers.OnLoginWithSession += HandleLoginWithSession;
        conn.Reducers.OnRegisterAccount += HandleRegisterAccount;
        
        // Player handlers
        conn.Reducers.OnCreatePlayer += HandleCreatePlayer;
        
        // Error handler
        conn.OnUnhandledReducerError += HandleUnhandledReducerError;
    }

    private void HandleLoginWithSession(ReducerEventContext ctx, string username, string pin, string deviceInfo)
    {
        // UnityEngine.Debug.Log($"[GameManager] LoginWithSession reducer callback for {username}");
        // EventBridge handles the actual login logic
    }

    private void HandleRegisterAccount(ReducerEventContext ctx, string username, string displayName, string pin)
    {
        // UnityEngine.Debug.Log($"[GameManager] RegisterAccount reducer callback for {username}");
        // EventBridge handles the actual registration logic
    }

    private void HandleCreatePlayer(ReducerEventContext ctx, string username)
    {
        // UnityEngine.Debug.Log($"[GameManager] CreatePlayer reducer callback for {username}");
        // EventBridge handles the actual player creation logic
    }

    private void HandleUnhandledReducerError(ReducerEventContext ctx, Exception error)
    {
        // UnityEngine.Debug.LogError($"[GameManager] Unhandled reducer error: {error}");
        
        // Get reducer name from the event
        string reducerName = "Unknown";
        if (ctx != null && ctx.Event != null)
        {
            // The reducer name is part of the event type
            reducerName = ctx.Event.GetType().Name;
        }
        
        // Publish a generic reducer error event
        GameEventBus.Instance.Publish(new ReducerErrorEvent
        {
            ReducerName = reducerName,
            Error = error.Message
        });
    }

    #endregion

    #region Frame Ticking

    private void InitializeFrameTicking()
    {
        if (FrameTickManager.Instance != null)
        {
            FrameTickManager.Instance.Initialize(conn);
            FrameTickManager.Instance.OnTickCompleted += OnFrameTickCompleted;
        }
        else
        {
            // UnityEngine.Debug.LogError("[GameManager] FrameTickManager not found! Frame updates will not work properly.");
        }
    }

    private void OnFrameTickCompleted(float tickTimeMs)
    {
        // Log warnings for slow ticks
        if (tickTimeMs > 16.0f) // More than one frame at 60fps
        {
            // UnityEngine.Debug.LogWarning($"[GameManager] Slow frame tick detected: {tickTimeMs:F2}ms");
        }
    }

    #endregion
    }
}
