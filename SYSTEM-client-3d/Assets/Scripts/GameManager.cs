using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpacetimeDB;
using SpacetimeDB.Types;

/// <summary>
/// Central manager for SpacetimeDB connection and game state.
/// Persists across scenes and handles connection lifecycle.
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

    // Events
    public static event Action OnConnected;
    public static event Action OnDisconnected;
    public static event Action<string> OnConnectionError;
    public static event Action<Player> OnLocalPlayerReady;
    public static event Action OnSubscriptionReady; // New event for when tables are ready

    // Add a method for LoginUIController to register itself
    public static void RegisterLoginUI(LoginUIController controller)
    {
        if (instance != null)
        {
            instance.loginUI = controller;
            // // Debug.Log("[GameManager] LoginUIController registered");
            
            // If we're already connected and subscription is ready, notify the controller
            if (IsConnected() && instance.conn.Db != null)
            {
                OnSubscriptionReady?.Invoke();
                
                // If no player exists, show login
                if (GetLocalPlayer() == null)
                {
                    controller.HideLoading();
                    controller.ShowLoginPanel();
                }
            }
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
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus && conn != null && !conn.IsActive && !isReconnecting)
        {
            // // Debug.Log("Resuming from pause, attempting to reconnect...");
            StartCoroutine(ReconnectToServer());
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && conn != null && !conn.IsActive && !isReconnecting)
        {
            // Debug.Log("Regained focus, checking connection...");
            StartCoroutine(ReconnectToServer());
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
        
        // Debug.Log("[GameManager] FrameTickManager initialized");
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
            // Debug.Log("Already connected or connecting");
            yield break;
        }

        isConnecting = true;
        // Debug.Log($"Connecting to SpacetimeDB at {moduleAddress}...");

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
        // Debug.Log("Attempting to reconnect to server...");

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
        // Debug.Log($"Connected to SpacetimeDB! Identity: {identity}");
        
        // Initialize FrameTickManager with the connection
        InitializeFrameTicking();
        
        // Notify connected listeners first
        OnConnected?.Invoke();
        
        // Subscribe to all tables
        connection.SubscriptionBuilder()
            .OnApplied(HandleSubscriptionApplied)
            .OnError(HandleSubscriptionError)
            .SubscribeToAllTables();
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
        // Debug.Log($"Disconnected from server. Error: {error?.Message}");
        OnDisconnected?.Invoke();

        // Try to reconnect after a delay
        if (!isReconnecting)
        {
            StartCoroutine(ReconnectToServer());
        }
    }

    private void HandleSubscriptionApplied(SubscriptionEventContext ctx)
    {
        // Debug.Log("Table subscriptions applied successfully");

        // Set up table event handlers AFTER subscription is applied
        SetupTableSubscriptions();
        
        // Notify that subscription is ready - this is when tables can be accessed
        OnSubscriptionReady?.Invoke();

        // Check if we already have a player
        CheckLocalPlayer();
    }

    private void HandleSubscriptionError(ErrorContext ctx, Exception error)
    {
        Debug.LogError($"Subscription error: {error.Message}");
        isConnecting = false;
        OnConnectionError?.Invoke(error.Message);
        
        if (loginUI != null)
        {
            loginUI.HideLoading();
            loginUI.ShowLoginPanel();
            loginUI.ShowErrorMessage($"Connection error: {error.Message}");
        }
    }

    #endregion

    #region Table Subscriptions

    private void SetupTableSubscriptions()
    {
        // Player table events
        conn.Db.Player.OnInsert += OnPlayerInsert;
        conn.Db.Player.OnUpdate += OnPlayerUpdate;
        conn.Db.Player.OnDelete += OnPlayerDelete;

        // World table events
        conn.Db.World.OnInsert += OnWorldInsert;
        conn.Db.World.OnUpdate += OnWorldUpdate;
        
        // SessionResult table events - Set these up here so they're ready
        conn.Db.SessionResult.OnInsert += OnSessionResultInsert;
        conn.Db.SessionResult.OnUpdate += OnSessionResultUpdate;
    }

    private void OnPlayerInsert(EventContext ctx, Player player)
    {
        if (player.Identity == conn.Identity)
        {
            // Debug.Log($"Local player created: {player.Name}");
            OnLocalPlayerReady?.Invoke(player);

            // Player exists, transition to game
            LoadGameScene();
        }
    }

    private void OnPlayerUpdate(EventContext ctx, Player oldPlayer, Player newPlayer)
    {
        if (newPlayer.Identity == conn.Identity)
        {
            // Debug.Log($"Local player updated: {newPlayer.Name}");
        }
    }

    private void OnPlayerDelete(EventContext ctx, Player player)
    {
        if (player.Identity == conn.Identity)
        {
            // Debug.Log("Local player deleted!");
            // Return to login
            LoadLoginScene();
        }
    }

    private void OnWorldInsert(EventContext ctx, World world)
    {
        // Debug.Log($"World registered: {world.WorldName} at {world.WorldCoords}");
    }

    private void OnWorldUpdate(EventContext ctx, World oldWorld, World newWorld)
    {
        // Debug.Log($"World updated: {newWorld.WorldName}");
    }
    
    // Forward SessionResult events to LoginUIController if it exists
    private void OnSessionResultInsert(EventContext ctx, SessionResult sessionResult)
    {
        // Debug.Log($"[GameManager] SessionResult inserted for identity: {sessionResult.Identity}");
        // Let LoginUIController handle this via its own subscription
    }
    
    private void OnSessionResultUpdate(EventContext ctx, SessionResult oldResult, SessionResult newResult)
    {
        // Debug.Log($"[GameManager] SessionResult updated for identity: {newResult.Identity}");
        // Let LoginUIController handle this via its own subscription
    }

    #endregion

    #region Reducer Event Handlers

    private void SetupReducerHandlers()
    {
        // Login/Account reducers
        conn.Reducers.OnRegisterAccount += HandleRegisterAccount;
        conn.Reducers.OnLogin += HandleLogin;
        conn.Reducers.OnLoginWithSession += HandleLoginWithSession; // Add this handler
        conn.Reducers.OnCreatePlayer += HandleCreatePlayer;

        // Debug reducers
        conn.Reducers.OnDebugMiningStatus += HandleDebugMiningStatus;
        conn.Reducers.OnDebugWavePacketStatus += HandleDebugWavePacketStatus;
        conn.Reducers.OnDebugGiveCrystal += HandleDebugGiveCrystal;
    }

    private void HandleRegisterAccount(ReducerEventContext ctx, string username, string displayName, string pin)
    {
        // Debug.Log($"[GameManager] Register account reducer response for: {username}, Status: {ctx.Event.Status}");
        
        // Let LoginUIController handle the UI updates
        // GameManager should only log or handle non-UI related tasks
        
        if (ctx.Event.Status is Status.Failed(var reason))
        {
            Debug.LogError($"[GameManager] Registration failed: {reason}");
        }
        else if (ctx.Event.Status is Status.Committed)
        {
            // Debug.Log($"[GameManager] Registration successful for {username}");
        }
    }

    // Handle the basic login reducer (if it exists on server)
    private void HandleLogin(ReducerEventContext ctx, string username, string pin, string deviceInfo)
    {
        // Debug.Log($"[GameManager] Login reducer response for: {username}, Status: {ctx.Event.Status}");
        
        if (ctx.Event.Status is Status.Committed)
        {
            // Debug.Log($"[GameManager] Login successful for {username}");
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            Debug.LogError($"[GameManager] Login failed: {reason}");
        }
    }
    
    // Handle the login_with_session reducer
    private void HandleLoginWithSession(ReducerEventContext ctx, string username, string pin, string deviceInfo)
    {
        // Debug.Log($"[GameManager] LoginWithSession reducer response for: {username}, Status: {ctx.Event.Status}");
        
        if (ctx.Event.Status is Status.Committed)
        {
            // Debug.Log($"[GameManager] LoginWithSession successful for {username}");
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            Debug.LogError($"[GameManager] LoginWithSession failed: {reason}");
        }
    }

    private void HandleCreatePlayer(ReducerEventContext ctx, string playerName)
    {
        // Debug.Log($"[GameManager] Create player reducer response, Status: {ctx.Event.Status}");
        
        if (ctx.Event.Status is Status.Committed)
        {
            // Debug.Log($"[GameManager] Player creation successful");
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            Debug.LogError($"[GameManager] Player creation failed: {reason}");
        }
    }

    // Debug reducer handlers
    private void HandleDebugMiningStatus(ReducerEventContext ctx)
    {
        // Debug.Log("[GameManager] Debug mining status executed");
    }

    private void HandleDebugWavePacketStatus(ReducerEventContext ctx)
    {
        // Debug.Log("[GameManager] Debug wave packet status executed");
    }

    private void HandleDebugGiveCrystal(ReducerEventContext ctx, CrystalType crystalType)
    {
        // Debug.Log($"[GameManager] Debug give crystal executed for type: {crystalType}");
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

    /// <summary>
    /// Create a new player with the given name
    /// </summary>
    public static void CreatePlayer(string playerName)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot create player - not connected");
            return;
        }

        // Debug.Log($"Calling CreatePlayer reducer with name: {playerName}");
        Conn.Reducers.CreatePlayer(playerName);
    }

    /// <summary>
    /// Choose a starting crystal for the player
    /// </summary>
    public static void ChooseCrystal(CrystalType crystalType)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot choose crystal - not connected");
            return;
        }

        // Debug.Log($"Calling ChooseCrystal reducer with type: {crystalType}");
        Conn.Reducers.ChooseCrystal(crystalType);
    }

    /// <summary>
    /// Update player position in the world
    /// </summary>
    public static void UpdatePlayerPosition(DbVector3 position, DbQuaternion rotation)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot update position - not connected");
            return;
        }

        Conn.Reducers.UpdatePlayerPosition(position, rotation);
    }

    /// <summary>
    /// Travel to a different world
    /// </summary>
    public static void TravelToWorld(WorldCoords worldCoords)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot travel - not connected");
            return;
        }

        // Debug.Log($"Calling TravelToWorld reducer for coords: ({worldCoords.X}, {worldCoords.Y}, {worldCoords.Z})");
        Conn.Reducers.TravelToWorld(worldCoords);
    }

    /// <summary>
    /// Logout the current user
    /// </summary>
    public static void Logout()
    {
        if (IsConnected())
        {
            // Debug.Log("Logging out...");
            instance.conn.Reducers.Logout();
            
            // Clear local session
            AuthToken.ClearSession();
            GameData.Instance?.SetUsername("");
            
            // Return to login scene
            LoadLoginScene();
        }
    }

    /// <summary>
    /// Login with username and PIN
    /// </summary>
    public static void LoginWithSession(string username, string pin, string deviceInfo = null)
    {
        if (IsConnected())
        {
            // Generate device info if not provided
            if (string.IsNullOrEmpty(deviceInfo))
            {
                deviceInfo = GenerateDeviceInfo();
            }
            
            // Debug.Log($"Attempting login for: {username}");
            instance.conn.Reducers.LoginWithSession(username, pin, deviceInfo);
        }
        else
        {
            Debug.LogError("Cannot call Login reducer - not connected");
        }
    }

    #region Mining Reducers

    /// <summary>
    /// Start mining a wave packet orb (requires crystal type in newer API)
    /// </summary>
    public static void StartMining(ulong orbId, CrystalType crystalType)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot start mining - not connected");
            return;
        }

        // Debug.Log($"Calling StartMining reducer for orb: {orbId} with crystal: {crystalType}");
        Conn.Reducers.StartMining(orbId, crystalType);
    }

    /// <summary>
    /// Stop current mining operation
    /// </summary>
    public static void StopMining()
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot stop mining - not connected");
            return;
        }

        // Debug.Log("Calling StopMining reducer");
        Conn.Reducers.StopMining();
    }

    /// <summary>
    /// Capture a wave packet during mining
    /// </summary>
    public static void CaptureWavePacket(ulong wavePacketId)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot capture wave packet - not connected");
            return;
        }

        // Debug.Log($"Calling CaptureWavePacket reducer for packet: {wavePacketId}");
        Conn.Reducers.CaptureWavePacket(wavePacketId);
    }

    #endregion

    #region Debug Reducers

    /// <summary>
    /// Debug: Give the player a crystal of specified type
    /// </summary>
    public static void DebugGiveCrystal(CrystalType crystalType)
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot give crystal - not connected");
            return;
        }

        // Debug.Log($"Calling DebugGiveCrystal reducer with type: {crystalType}");
        Conn.Reducers.DebugGiveCrystal(crystalType);
    }

    /// <summary>
    /// Debug: Show mining status information
    /// </summary>
    public static void DebugMiningStatus()
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot debug mining status - not connected");
            return;
        }

        // Debug.Log("Calling DebugMiningStatus reducer");
        Conn.Reducers.DebugMiningStatus();
    }

    /// <summary>
    /// Debug: Show wave packet status information
    /// </summary>
    public static void DebugWavePacketStatus()
    {
        if (!IsConnected() || Conn == null)
        {
            Debug.LogError("Cannot debug wave packet status - not connected");
            return;
        }

        // Debug.Log("Calling DebugWavePacketStatus reducer");
        Conn.Reducers.DebugWavePacketStatus();
    }

    #endregion

    #endregion

    #region Private Helper Methods

    // Helper method to generate device info
    private static string GenerateDeviceInfo()
    {
        return $"{SystemInfo.deviceModel}|{SystemInfo.operatingSystem}|{SystemInfo.deviceUniqueIdentifier}";
    }

    private void CheckLocalPlayer()
    {
        var localPlayer = GetLocalPlayer();
        if (localPlayer != null)
        {
            // Debug.Log($"Found existing player: {localPlayer.Name}");
            OnLocalPlayerReady?.Invoke(localPlayer);
            LoadGameScene();
        }
        else
        {
            // Debug.Log("No player found - showing login");
            if (loginUI != null)
            {
                loginUI.HideLoading();
                loginUI.ShowLoginPanel();
            }
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Debug.Log($"Scene loaded: {scene.name}");

        if (scene.name == loginSceneName)
        {
            // Login scene loaded - find the LoginUIController
            loginUI = FindFirstObjectByType<LoginUIController>();
            if (loginUI != null)
            {
                // Debug.Log("Found LoginUIController in scene");
                
                // Check if we need to show login
                if (IsConnected() && GetLocalPlayer() == null)
                {
                    loginUI.HideLoading();
                    loginUI.ShowLoginPanel();
                }
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