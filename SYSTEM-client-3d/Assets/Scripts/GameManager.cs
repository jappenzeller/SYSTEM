using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpacetimeDB;
using SpacetimeDB.Types;

/// <summary>
/// Central manager for SpacetimeDB connection and game state.
/// Now only responds to EventBus events - all SpacetimeDB interactions go through EventBridge.
/// No direct UI coupling - everything through EventBus.
/// </summary>
public class GameManager : MonoBehaviour
{
    private static GameManager instance;
    public static GameManager Instance => instance;

    [Header("Connection Settings")]
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
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(ConnectToServer());

        // Subscribe to EventBus events
        GameEventBus.Instance.Subscribe<LocalPlayerReadyEvent>(OnLocalPlayerReadyEvent);
        GameEventBus.Instance.Subscribe<ConnectionLostEvent>(OnConnectionLostEvent);
        GameEventBus.Instance.Subscribe<LocalPlayerNotFoundEvent>(OnLocalPlayerNotFoundEvent);
        GameEventBus.Instance.Subscribe<SubscriptionReadyEvent>(OnSubscriptionReadyEvent);
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
        GameEventBus.Instance.Unsubscribe<LocalPlayerReadyEvent>(OnLocalPlayerReadyEvent);
        GameEventBus.Instance.Unsubscribe<ConnectionLostEvent>(OnConnectionLostEvent);
        GameEventBus.Instance.Unsubscribe<LocalPlayerNotFoundEvent>(OnLocalPlayerNotFoundEvent);
        GameEventBus.Instance.Unsubscribe<SubscriptionReadyEvent>(OnSubscriptionReadyEvent);
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
        Debug.Log($"[GameManager] Local player ready via EventBus: {evt.Player.Name}");
        
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
        Debug.Log($"[GameManager] Connection lost via EventBus: {evt.Reason}");
        
        OnDisconnected?.Invoke();
        
        // Start reconnection if needed
        if (!isReconnecting)
        {
            StartCoroutine(ReconnectToServer());
        }
    }

    private void OnLocalPlayerNotFoundEvent(LocalPlayerNotFoundEvent evt)
    {
        Debug.Log("[GameManager] No local player found");
        // LoginUIController will handle showing the login UI
    }

    private void OnSubscriptionReadyEvent(SubscriptionReadyEvent evt)
    {
        Debug.Log("[GameManager] Subscription ready via EventBus");
        // EventBridge will check for local player
    }

    #endregion

    #region Public Methods

    public static void CreatePlayer(string username)
    {
        if (!IsConnected())
        {
            Debug.LogError("[GameManager] Cannot create player - not connected");
            return;
        }

        Debug.Log($"[GameManager] Creating player: {username}");
        
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
            Debug.Log("[GameManager] LoadGameScene called - using SceneTransitionManager");
            
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
                    Debug.LogError("[GameManager] SceneTransitionManager not available!");
                    // Fallback to direct scene load
                    instance.StartCoroutine(instance.LoadSceneAsync(instance.gameSceneName));
                }
            }
            else
            {
                Debug.LogError("[GameManager] Cannot load game scene - no local player!");
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
        Debug.Log("[GameManager] Logging out...");

        // Clear local data
        if (instance != null)
        {
            instance.localPlayer = null;
            
            // Clear saved session
            AuthToken.ClearSession();
            
            // Clear game data
            GameData.Instance?.ClearSession();
            
            // Disconnect
            if (instance.conn != null && instance.conn.IsActive)
            {
                instance.conn.Disconnect();
            }
            
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
        Debug.Log($"[GameManager] Scene loaded: {scene.name}");
        
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
        if (isConnecting || (conn != null && conn.IsActive))
        {
            yield break;
        }

        isConnecting = true;
        
        // Use configuration for both URL and module
        string protocol = useSSL ? "https://" : "http://";
        string url = $"{protocol}{moduleAddress}";
        string module = moduleName;
        Debug.Log($"Connecting to SpacetimeDB at {url}/{module}...");
        
        // Publish connection started event
        GameEventBus.Instance.Publish(new ConnectionStartedEvent());

        // Get saved token if exists
        string savedToken = AuthToken.LoadToken();

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
        Debug.Log($"Connected to SpacetimeDB! Identity: {identity}");
        
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
        Debug.LogError($"Failed to connect: {error.Message}");
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
        Debug.Log($"Disconnected from server. Error: {error?.Message ?? "None"}");
        
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
        Debug.Log($"[GameManager] LoginWithSession reducer callback for {username}");
        // EventBridge handles the actual login logic
    }

    private void HandleRegisterAccount(ReducerEventContext ctx, string username, string displayName, string pin)
    {
        Debug.Log($"[GameManager] RegisterAccount reducer callback for {username}");
        // EventBridge handles the actual registration logic
    }

    private void HandleCreatePlayer(ReducerEventContext ctx, string username)
    {
        Debug.Log($"[GameManager] CreatePlayer reducer callback for {username}");
        // EventBridge handles the actual player creation logic
    }

    private void HandleUnhandledReducerError(ReducerEventContext ctx, Exception error)
    {
        Debug.LogError($"[GameManager] Unhandled reducer error: {error}");
        
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
            Debug.LogError("[GameManager] FrameTickManager not found! Frame updates will not work properly.");
        }
    }

    private void OnFrameTickCompleted(float tickTimeMs)
    {
        // Log warnings for slow ticks
        if (tickTimeMs > 16.0f) // More than one frame at 60fps
        {
            Debug.LogWarning($"[GameManager] Slow frame tick detected: {tickTimeMs:F2}ms");
        }
    }

    #endregion
}