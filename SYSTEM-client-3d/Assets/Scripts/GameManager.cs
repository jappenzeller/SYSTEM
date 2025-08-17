using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpacetimeDB;
using SpacetimeDB.Types;

/// <summary>
/// Central manager for SpacetimeDB connection and game state.
/// Now only responds to EventBus events - all SpacetimeDB interactions go through EventBridge.
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
    private LoginUIController loginUI;
    
    // Properties
    public static bool IsConnected() => instance?.conn != null && instance.conn.IsActive;
    public static DbConnection Conn => instance?.conn;
    public static Identity? LocalIdentity => instance?.conn?.Identity;

    // Events (for systems that need to know about connection state)
    public static event Action OnConnected;
    public static event Action OnDisconnected;
    public static event Action<string> OnConnectionError;
    public static event Action<Player> OnLocalPlayerReady;

    // Add a method for LoginUIController to register itself
    public static void RegisterLoginUI(LoginUIController controller)
    {
        if (instance != null)
        {
            instance.loginUI = controller;
        }
    }

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
        
        // Notify any listeners
        OnLocalPlayerReady?.Invoke(evt.Player);
        
        // Load the game scene
        LoadGameScene();
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
        Debug.Log("[GameManager] No local player found - showing login");
        
        if (loginUI != null)
        {
            loginUI.HideLoading();
            loginUI.ShowLoginPanel();
        }
    }

    private void OnSubscriptionReadyEvent(SubscriptionReadyEvent evt)
    {
        Debug.Log("[GameManager] Subscription ready via EventBus");
        
        // If we're in login scene and need to show login
        if (SceneManager.GetActiveScene().name == loginSceneName && loginUI != null)
        {
            // EventBridge will check for player and publish appropriate event
        }
    }

    #endregion

    #region Frame Tick Management
    
    private void InitializeFrameTicking()
    {
        // Initialize the frame tick manager when connection is established
        FrameTickManager.Instance.Initialize(conn);
        
        // Optionally subscribe to performance events
        FrameTickManager.Instance.OnTickCompleted += OnFrameTickCompleted;
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

    #region Connection Management

    private IEnumerator ConnectToServer()
    {
        if (isConnecting || (conn != null && conn.IsActive))
        {
            yield break;
        }

        isConnecting = true;
        Debug.Log($"Connecting to SpacetimeDB at {moduleAddress}...");
        
        // Publish connection started event
        GameEventBus.Instance.Publish(new ConnectionStartedEvent
        {
            ServerUrl = moduleAddress
        });

        // Build the connection
        string protocol = useSSL ? "https://" : "http://";
        string url = $"{protocol}{moduleAddress}";

        conn = DbConnection.Builder()
            .WithUri(url)
            .WithModuleName(moduleName)
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
        
        // Show error in login UI if available
        if (loginUI != null)
        {
            loginUI.HideLoading();
            loginUI.ShowLoginPanel();
            loginUI.ShowErrorMessage($"Connection failed: {error.Message}");
        }
    }

    private void HandleDisconnected(DbConnection connection, Exception error)
    {
        Debug.Log($"Disconnected from server. Error: {error?.Message}");
        OnDisconnected?.Invoke();

        // Try to reconnect after a delay
        if (!isReconnecting)
        {
            StartCoroutine(ReconnectToServer());
        }
    }

    #endregion

    #region Reducer Event Handlers

    private void SetupReducerHandlers()
    {
        // Login/Account reducers
        conn.Reducers.OnRegisterAccount += HandleRegisterAccount;
        conn.Reducers.OnLogin += HandleLogin;
        conn.Reducers.OnLoginWithSession += HandleLoginWithSession;
        conn.Reducers.OnCreatePlayer += HandleCreatePlayer;

        // Debug reducers
        conn.Reducers.OnDebugMiningStatus += HandleDebugMiningStatus;
        conn.Reducers.OnDebugWavePacketStatus += HandleDebugWavePacketStatus;
        conn.Reducers.OnDebugGiveCrystal += HandleDebugGiveCrystal;
    }

    private void HandleRegisterAccount(ReducerEventContext ctx, string username, string displayName, string pin)
    {
        if (ctx.Event.Status is Status.Failed(var reason))
        {
            Debug.LogError($"[GameManager] Registration failed: {reason}");
        }
        else if (ctx.Event.Status is Status.Committed)
        {
            Debug.Log($"[GameManager] Registration successful for {username}");
        }
    }

    private void HandleLogin(ReducerEventContext ctx, string username, string pin, string deviceInfo)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            Debug.Log($"[GameManager] Login successful for {username}");
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            Debug.LogError($"[GameManager] Login failed: {reason}");
        }
    }
    
    private void HandleLoginWithSession(ReducerEventContext ctx, string username, string pin, string deviceInfo)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            Debug.Log($"[GameManager] LoginWithSession successful for {username}");
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            Debug.LogError($"[GameManager] LoginWithSession failed: {reason}");
        }
    }

    private void HandleCreatePlayer(ReducerEventContext ctx, string playerName)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            Debug.Log($"[GameManager] Player creation/restoration successful");
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            if (reason != null && reason.Contains("already has a player"))
            {
                Debug.Log($"[GameManager] Account already has player, waiting for restoration...");
            }
            else
            {
                Debug.LogError($"[GameManager] Player creation failed: {reason}");
                OnConnectionError?.Invoke(reason ?? "Failed to create player");
            }
        }
    }

    private void HandleDebugMiningStatus(ReducerEventContext ctx)
    {
        Debug.Log("[GameManager] Debug mining status executed");
    }

    private void HandleDebugWavePacketStatus(ReducerEventContext ctx)
    {
        Debug.Log("[GameManager] Debug wave packet status executed");
    }

    private void HandleDebugGiveCrystal(ReducerEventContext ctx, CrystalType crystalType)
    {
        Debug.Log($"[GameManager] Debug give crystal executed for type: {crystalType}");
    }

    #endregion

    #region Public Static Methods - Core Functionality

    public static Player GetLocalPlayer()
    {
        if (!IsConnected()) return null;

        foreach (var player in instance.conn.Db.Player.Iter())
        {
            if (player.Identity == instance.conn.Identity)
            {
                return player;
            }
        }
        return null;
    }

    public static void LoadLoginScene()
    {
        if (instance != null)
        {
            SceneManager.LoadScene(instance.loginSceneName);
        }
    }

    public static void LoadGameScene()
    {
        if (instance != null)
        {
            SceneManager.LoadScene(instance.gameSceneName);
        }
    }

    #endregion

    #region Public Static Methods - SpacetimeDB Reducers

    public static void CreatePlayer(string playerName)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot create player - not connected");
            return;
        }

        Conn.Reducers.CreatePlayer(playerName);
    }

    public static void ChooseCrystal(CrystalType crystalType)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot choose crystal - not connected");
            return;
        }

        Conn.Reducers.ChooseCrystal(crystalType);
    }

    public static void UpdatePlayerPosition(DbVector3 position, DbQuaternion rotation)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot update position - not connected");
            return;
        }

        Conn.Reducers.UpdatePlayerPosition(position, rotation);
    }

    public static void TravelToWorld(WorldCoords worldCoords)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot travel - not connected");
            return;
        }

        Conn.Reducers.TravelToWorld(worldCoords);
    }

    public static void Logout()
    {
        if (IsConnected())
        {
            instance.conn.Reducers.Logout();
            
            // Clear local session
            AuthToken.ClearSession();
            GameData.Instance?.SetUsername("");
            
            // Return to login scene
            LoadLoginScene();
        }
    }

    public static void LoginWithSession(string username, string pin, string deviceInfo = null)
    {
        if (IsConnected())
        {
            // Generate device info if not provided
            if (string.IsNullOrEmpty(deviceInfo))
            {
                deviceInfo = GenerateDeviceInfo();
            }
            
            instance.conn.Reducers.LoginWithSession(username, pin, deviceInfo);
        }
        else
        {
            Debug.LogError("Cannot call Login reducer - not connected");
        }
    }

    #region Mining Reducers

    public static void StartMining(ulong orbId, CrystalType crystalType)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot start mining - not connected");
            return;
        }

        Conn.Reducers.StartMining(orbId, crystalType);
    }

    public static void StopMining()
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot stop mining - not connected");
            return;
        }

        Conn.Reducers.StopMining();
    }

    public static void CaptureWavePacket(ulong wavePacketId)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot capture wave packet - not connected");
            return;
        }

        Conn.Reducers.CaptureWavePacket(wavePacketId);
    }

    #endregion

    #region Debug Reducers

    public static void DebugGiveCrystal(CrystalType crystalType)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot give crystal - not connected");
            return;
        }

        Conn.Reducers.DebugGiveCrystal(crystalType);
    }

    public static void DebugMiningStatus()
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot debug mining status - not connected");
            return;
        }

        Conn.Reducers.DebugMiningStatus();
    }

    public static void DebugWavePacketStatus()
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot debug wave packet status - not connected");
            return;
        }

        Conn.Reducers.DebugWavePacketStatus();
    }

    #endregion

    #endregion

    #region Private Helper Methods

    private static string GenerateDeviceInfo()
    {
        return $"{SystemInfo.deviceModel}|{SystemInfo.operatingSystem}|{SystemInfo.deviceUniqueIdentifier}";
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == loginSceneName)
        {
            // Login scene loaded - find the LoginUIController
            loginUI = FindFirstObjectByType<LoginUIController>();
            if (loginUI != null)
            {
                Debug.Log("Found LoginUIController in scene");
            }
        }
        else if (scene.name == gameSceneName)
        {
            // Game scene loaded
            loginUI = null; // Clear reference since we're not in login scene
        }
    }

    #endregion
}