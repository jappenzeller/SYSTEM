using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpacetimeDB;
using SpacetimeDB.Types;

public partial class GameManager : MonoBehaviour
{
    private static GameManager instance;
    public static GameManager Instance => instance;

    [Header("SpacetimeDB Configuration")]
    [SerializeField] private string moduleAddress = "localhost:3000";
    [SerializeField] private string moduleName = "system";
    [SerializeField] private bool useSSL = false;

    [Header("Scene Configuration")]
    [SerializeField] private string loginSceneName = "LoginScene";
    [SerializeField] private string gameSceneName = "GameScene";

    // Connection
    private DbConnection conn;
    private bool isConnecting = false;
    private bool isReconnecting = false;

    // Components
    private LoginUIController loginUI;
    private GameData gameData;

    // Properties
    public static DbConnection Conn => instance?.conn;
    public static Identity? LocalIdentity => instance?.conn?.Identity;

    // Events
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
            Debug.Log("[GameManager] LoginUIController registered");
            
            // If we're already connected and waiting to show login, do it now
            if (IsConnected() && GetLocalPlayer() == null)
            {
                controller.HideLoading();
                controller.ShowLoginPanel();
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

        if (conn != null && conn.IsActive)
        {
            conn.Disconnect();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus && conn != null && !conn.IsActive && !isReconnecting)
        {
            Debug.Log("Resuming from pause, attempting to reconnect...");
            StartCoroutine(ReconnectToServer());
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && conn != null && !conn.IsActive && !isReconnecting)
        {
            Debug.Log("Regained focus, checking connection...");
            StartCoroutine(ReconnectToServer());
        }
    }

    #endregion

    #region Connection Management

    private IEnumerator ConnectToServer()
    {
        if (isConnecting || (conn != null && conn.IsActive))
        {
            Debug.Log("Already connected or connecting");
            yield break;
        }

        isConnecting = true;
        Debug.Log($"Connecting to SpacetimeDB at {moduleAddress}...");

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

        // Set up reducer event handlers
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
        Debug.Log("Attempting to reconnect to server...");

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
        
        // Subscribe to all tables
        connection.SubscriptionBuilder()
            .OnApplied(HandleSubscriptionApplied)
            .OnError(HandleSubscriptionError)
            .SubscribeToAllTables();

        OnConnected?.Invoke();
    }

    private void HandleConnectError(Exception error)
    {
        Debug.LogError($"Failed to connect: {error.Message}");
        isConnecting = false;
        OnConnectionError?.Invoke(error.Message);
        
        // Show error in login UI if available
        loginUI?.ShowError($"Connection failed: {error.Message}");
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

    private void HandleSubscriptionApplied(SubscriptionEventContext ctx)
    {
        Debug.Log("Table subscriptions applied successfully");

        // Set up table subscriptions
        SetupTableSubscriptions();
    }

    private void HandleSubscriptionError(ErrorContext ctx, Exception error)
    {
        Debug.LogError($"Subscription error: {error.Message}");
        isConnecting = false;
        OnConnectionError?.Invoke(error.Message);
        loginUI?.ShowError($"Connection error: {error.Message}");
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

        // Check if we already have a player
        CheckLocalPlayer();
    }

    private void OnPlayerInsert(EventContext ctx, Player player)
    {
        if (player.Identity == conn.Identity)
        {
            Debug.Log($"Local player created: {player.Name}");
            OnLocalPlayerReady?.Invoke(player);

            // Player exists, transition to game
            LoadGameScene();
        }
    }

    private void OnPlayerUpdate(EventContext ctx, Player oldPlayer, Player newPlayer)
    {
        if (newPlayer.Identity == conn.Identity)
        {
            Debug.Log($"Local player updated: {newPlayer.Name}");
        }
    }

    private void OnPlayerDelete(EventContext ctx, Player player)
    {
        if (player.Identity == conn.Identity)
        {
            Debug.Log("Local player deleted!");
            // Return to login
            LoadLoginScene();
        }
    }

    private void OnWorldInsert(EventContext ctx, World world)
    {
        Debug.Log($"World registered: {world.WorldName} at {world.WorldCoords}");
    }

    private void OnWorldUpdate(EventContext ctx, World oldWorld, World newWorld)
    {
        Debug.Log($"World updated: {newWorld.WorldName}");
    }

    #endregion

    #region Reducer Event Handlers

    private void SetupReducerHandlers()
    {
        // Login/Account reducers
        conn.Reducers.OnRegisterAccount += HandleRegisterAccount;
        conn.Reducers.OnLogin += HandleLogin;
        conn.Reducers.OnCreatePlayer += HandleCreatePlayer;

        // Debug reducers
        conn.Reducers.OnDebugMiningStatus += HandleDebugMiningStatus;
        conn.Reducers.OnDebugWavePacketStatus += HandleDebugWavePacketStatus;
        conn.Reducers.OnDebugGiveCrystal += HandleDebugGiveCrystal;
    }

    private void HandleRegisterAccount(ReducerEventContext ctx, string username, string displayName, string pin)
    {
        Debug.Log($"Register account reducer response for: {username}");
        if (ctx.Event.Status is Status.Failed(var reason))
        {
            loginUI?.ShowError($"Registration failed: {reason}");
        }
        else
        {
            loginUI?.ShowMessage("Registration successful! Please login.");
        }
    }

    // FIXED: Added deviceInfo parameter to match server reducer signature
    private void HandleLogin(ReducerEventContext ctx, string username, string pin, string deviceInfo)
    {
        Debug.Log($"Login reducer response for: {username}");
        if (ctx.Event.Status is Status.Failed(var reason))
        {
            loginUI?.ShowError($"Login failed: {reason}");
        }
        else
        {
            gameData.SetUsername(username);
        }
    }

    private void HandleCreatePlayer(ReducerEventContext ctx, string playerName)
    {
        Debug.Log($"Create player reducer response for: {playerName}");
        if (ctx.Event.Status is Status.Failed(var reason))
        {
            loginUI?.ShowError($"Failed to create character: {reason}");
        }
    }

    private void HandleDebugMiningStatus(ReducerEventContext ctx)
    {
        Debug.Log("Debug mining status completed");
    }

    private void HandleDebugWavePacketStatus(ReducerEventContext ctx)
    {
        Debug.Log("Debug wave packet status completed");
    }

    // FIXED: Removed playerId parameter to match server reducer signature
    private void HandleDebugGiveCrystal(ReducerEventContext ctx, CrystalType crystalType)
    {
        Debug.Log($"Debug give crystal completed with crystal type {crystalType}");
    }

    private void OnUnhandledReducerError(ReducerEventContext ctx, Exception error)
    {
        Debug.LogError($"Unhandled reducer error: {error.Message}");

        // Check if we're in the login scene to show UI errors
        if (loginUI != null)
        {
            loginUI.ShowError($"Operation failed: {error.Message}");
        }

        // Log additional context for debugging
        Debug.LogError($"Error type: {error.GetType().Name}");

        // If this is a connection-related error, notify listeners
        if (error.Message.Contains("disconnected") || error.Message.Contains("connection"))
        {
            OnConnectionError?.Invoke(error.Message);
        }
    }

    #endregion

    #region Scene Management

    private void LoadLoginScene()
    {
        StartCoroutine(LoadSceneAsync(loginSceneName));
    }

    private void LoadGameScene()
    {
        StartCoroutine(LoadSceneAsync(gameSceneName));
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        Debug.Log($"Loading scene: {sceneName}");

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        Debug.Log($"Scene {sceneName} loaded");
    }

    #endregion

    #region Static Helper Methods

    public static bool IsConnected()
    {
        return instance != null && instance.conn != null && instance.conn.IsActive;
    }

    public static Player GetLocalPlayer()
    {
        if (!IsConnected() || LocalIdentity == null)
            return null;

        foreach (var player in Conn.Db.Player.Iter())
        {
            if (player.Identity == LocalIdentity)
                return player;
        }

        return null;
    }

    public static void TryConnectToServer()
    {
        if (instance != null && !IsConnected())
        {
            instance.StartCoroutine(instance.ConnectToServer());
        }
    }

    public static void CallLoginReducer(string username, string pin)
    {
        if (IsConnected() && Conn != null)
        {
            // FIXED: Added deviceInfo parameter
            string deviceInfo = GenerateDeviceInfo();
            Conn.Reducers.Login(username, pin, deviceInfo);
        }
        else
        {
            Debug.LogError("Cannot call Login reducer - not connected");
        }
    }

    // Helper method to generate device info
    private static string GenerateDeviceInfo()
    {
        return $"{SystemInfo.deviceModel}|{SystemInfo.operatingSystem}|{SystemInfo.deviceUniqueIdentifier}";
    }

    #endregion

    #region Private Helper Methods

    private void CheckLocalPlayer()
    {
        var localPlayer = GetLocalPlayer();
        if (localPlayer != null)
        {
            Debug.Log($"Found existing player: {localPlayer.Name}");
            OnLocalPlayerReady?.Invoke(localPlayer);
            LoadGameScene();
        }
        else
        {
            Debug.Log("No player found - showing login");
            if (loginUI != null)
            {
                loginUI.HideLoading();
                loginUI.ShowLoginPanel();
            }
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"Scene loaded: {scene.name}");

        if (scene.name == loginSceneName)
        {
            // Login scene loaded - find the LoginUIController
            loginUI = FindObjectOfType<LoginUIController>();
            if (loginUI != null)
            {
                Debug.Log("Found LoginUIController in scene");
                
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