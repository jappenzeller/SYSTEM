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

    // Note: HandleCreateCharacterRequest was removed as OnCreateCharacterRequested event
    // no longer exists in LoginUIController. Character creation is handled
    // through the CreatePlayer reducer which is setup in the partial class.

    #endregion

    #region Scene Management

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[GameManager] Scene loaded: {scene.name}");

        if (scene.name == loginSceneName)
        {
            // Find and setup login UI immediately when login scene loads
            SetupLoginUI();

            // If we're already connected and have a player, go to game
            if (IsConnected() && GetLocalPlayer() != null)
            {
                LoadGameScene();
            }
            else if (IsConnected())
            {
                // Connected but no player - show login UI
                Debug.Log("[GameManager] Connected but no player - showing login UI");
                if (loginUI != null)
                {
                    loginUI.HideLoading();
                    loginUI.ShowLoginPanel();
                }
            }
        }
        else if (scene.name == gameSceneName)
        {
            // Make sure we have a valid player
            var player = GetLocalPlayer();
            if (player == null)
            {
                Debug.LogWarning("No player found, returning to login");
                LoadLoginScene();
            }
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

        // Find login UI if we're in login scene
        if (SceneManager.GetActiveScene().name == loginSceneName)
        {
            SetupLoginUI();
            loginUI?.ShowLoading("Connecting to server...");
        }

        // Build the connection using the builder pattern
        string protocol = useSSL ? "wss" : "ws";
        string url = $"{protocol}://{moduleAddress}";

        // Load saved auth token if available
        string token = AuthToken.LoadToken();

        // Build the connection
        var builder = DbConnection.Builder()
            .WithUri(url)
            .WithModuleName(moduleName)
            .OnConnect(HandleConnect)
            .OnConnectError(HandleConnectError)
            .OnDisconnect(HandleDisconnect);

        // Add token if we have one
        if (!string.IsNullOrEmpty(token))
        {
            builder = builder.WithToken(token);
        }

        // Build and establish connection
        conn = builder.Build();

        // The connection process is asynchronous, so we wait for it
        float timeout = 10f;
        float elapsed = 0f;

        while (isConnecting && elapsed < timeout)
        {
            elapsed += Time.deltaTime;

            // Update loading animation
            if (loginUI != null)
            {
                int dots = Mathf.FloorToInt(elapsed % 3) + 1;
                string dotsText = new string('.', dots);
                loginUI.UpdateLoadingText($"Connecting to server{dotsText}");
            }

            yield return null;
        }

        if (isConnecting)
        {
            // Timeout
            isConnecting = false;
            Debug.LogError("Connection timeout");
            OnConnectionError?.Invoke("Connection timeout");
            loginUI?.ShowError("Failed to connect to server");
        }
    }

    private IEnumerator ReconnectToServer()
    {
        if (isReconnecting)
        {
            yield break;
        }

        isReconnecting = true;
        Debug.Log("Attempting to reconnect...");

        // Disconnect if still connected
        if (conn != null && conn.IsActive)
        {
            conn.Disconnect();
        }

        // Wait a moment
        yield return new WaitForSeconds(1f);

        // Try to connect again
        isReconnecting = false;
        yield return StartCoroutine(ConnectToServer());
    }

    public void Disconnect()
    {
        if (conn != null && conn.IsActive)
        {
            conn.Disconnect();
        }
    }

    #endregion

    #region Login UI Setup

    private void SetupLoginUI()
    {
        if (loginUI == null)
        {
            loginUI = FindFirstObjectByType<LoginUIController>();
            Debug.Log($"[GameManager] SetupLoginUI - LoginUI found: {loginUI != null}");
        }

        if (loginUI != null)
        {
            loginUI.OnLoginRequested += HandleLoginRequest;
            loginUI.OnRegisterRequested += HandleRegisterRequest;
            // Note: OnCreateCharacterRequested was removed from LoginUIController
            // Character creation is now handled through the CreatePlayer reducer
        }
        else
        {
            Debug.LogError("[GameManager] SetupLoginUI - Failed to find LoginUIController!");
        }
    }

    private void HandleLoginRequest(string username, string password)
    {
        Debug.Log($"Login requested for user: {username}");

        if (conn != null && conn.IsActive)
        {
            // Call the login reducer
            conn.Reducers.Login(username, password);
        }
        else
        {
            loginUI?.ShowError("Not connected to server");
        }
    }

    private void HandleRegisterRequest(string username, string password)
    {
        Debug.Log($"Registration requested for user: {username}");

        if (conn != null && conn.IsActive)
        {
            // Call the register_account reducer - now needs 3 params
            // Using username as display name and "0000" as default PIN
            conn.Reducers.RegisterAccount(username, username, "0000");
        }
        else
        {
            loginUI?.ShowError("Not connected to server");
        }
    }

    #endregion

    #region Connection Event Handlers

    private void HandleConnect(DbConnection connection, Identity identity, string token)
    {
        Debug.Log($"Connected to SpacetimeDB! Identity: {identity.ToString()}");
        isConnecting = false;

        // Save the auth token for future connections
        AuthToken.SaveToken(token);

        // Set up reducer error handler
        conn.OnUnhandledReducerError += OnUnhandledReducerError;

        // Set up reducer event handlers
        SetupReducerHandlers();

        OnConnected?.Invoke();

        // Now subscribe to all tables
        conn.SubscriptionBuilder()
            .OnApplied((ctx) => HandleSubscriptionApplied())
            .OnError(HandleSubscriptionError)
            .SubscribeToAllTables();
    }

    private void HandleConnectError(Exception error)
    {
        Debug.LogError($"Connection error: {error.Message}");
        isConnecting = false;
        OnConnectionError?.Invoke(error.Message);
        loginUI?.ShowError($"Failed to connect: {error.Message}");
    }

    private void HandleDisconnect(DbConnection connection, Exception error)
    {
        Debug.Log("Disconnected from SpacetimeDB");
        isConnecting = false;

        if (error != null)
        {
            Debug.LogError($"Disconnection error: {error.Message}");
        }

        gameData.ClearSession();

        OnDisconnected?.Invoke();

        // If we're in game scene, go back to login
        if (SceneManager.GetActiveScene().name == gameSceneName)
        {
            LoadLoginScene();
        }
    }

    private void HandleSubscriptionApplied()
    {
        Debug.Log("Subscription successful!");

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

    private void HandleLogin(ReducerEventContext ctx, string username, string password)
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

    private void HandleDebugGiveCrystal(ReducerEventContext ctx, ulong playerId, CrystalType crystalType)
    {
        Debug.Log($"Debug give crystal completed for player {playerId} with crystal type {crystalType}");
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

    public static World GetCurrentWorld()
    {
        var player = GetLocalPlayer();
        if (player == null)
            return null;

        foreach (var world in Conn.Db.World.Iter())
        {
            if (world.WorldCoords == player.CurrentWorld)
                return world;
        }

        return null;
    }

    #endregion

    #region Player Management

    private void CheckLocalPlayer()
    {
        var player = GetLocalPlayer();
        if (player != null)
        {
            Debug.Log($"Found existing player: {player.Name}");
            OnLocalPlayerReady?.Invoke(player);

            // Player exists, make sure we're in game scene
            if (SceneManager.GetActiveScene().name != gameSceneName)
            {
                LoadGameScene();
            }
        }
        else
        {
            Debug.Log("No player found for this identity");
            // Stay in login scene
        }
    }

    #endregion

    #region Utility Methods

    public void ShowDebugInfo()
    {
        if (!IsConnected())
        {
            Debug.Log("Not connected to SpacetimeDB");
            return;
        }

        Debug.Log($"Connected: {conn.IsActive}");
        Debug.Log($"Identity: {LocalIdentity?.ToString() ?? "None"}");

        var player = GetLocalPlayer();
        if (player != null)
        {
            Debug.Log($"Player: {player.Name} (ID: {player.PlayerId})");
            Debug.Log($"World: {player.CurrentWorld}");
            Debug.Log($"Position: {player.Position}");
        }
    }

    #endregion
}