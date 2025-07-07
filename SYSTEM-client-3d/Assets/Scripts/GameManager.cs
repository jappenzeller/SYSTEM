using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpacetimeDB;
using SpacetimeDB.Types;

public partial class GameManager : MonoBehaviour
{
    private static GameManager instance;
    public static GameManager Instance => instance;

    [Header("SpacetimeDB Configuration")]
    [SerializeField] private string host = "http://localhost:3000";
    [SerializeField] private string moduleName = "system";

    [Header("Scene Names")]
    [SerializeField] private string loginSceneName = "LoginScene";
    [SerializeField] private string gameSceneName = "GameScene";

    private DbConnection conn;
    private bool isConnecting = false;
    private LoginUIController loginUI;
    private GameData gameData;

    public static DbConnection Conn => instance?.conn;
    public static Identity? LocalIdentity => instance?.conn?.Identity;
    public static GameData Data => instance?.gameData;

    // Events
    public static event Action OnConnected;
    public static event Action OnDisconnected;
    public static event Action<string> OnConnectionError;
    public static event Action<Player> OnLocalPlayerReady;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            gameData = new GameData();
            InitializeConnection();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeConnection()
    {
        if (conn != null)
        {
            Debug.Log("Connection already exists");
            return;
        }

        // Load saved auth token
        string savedToken = AuthToken.LoadToken();

        // Create connection builder
        var builder = DbConnection.Builder()
            .WithUri(host)
            .WithModuleName(moduleName)
            .OnConnect(HandleConnect)
            .OnConnectError(HandleConnectError)
            .OnDisconnect(HandleDisconnect);

        // Add token if we have one
        if (!string.IsNullOrEmpty(savedToken))
        {
            builder = builder.WithToken(savedToken);
        }

        // Build connection
        conn = builder.Build();

        // Connection is established automatically when Build() is called
        isConnecting = true;
    }

    public static void RegisterLoginUI(LoginUIController ui)
    {
        if (instance != null)
        {
            instance.loginUI = ui;
            instance.SetupLoginEvents();
            Debug.Log($"[GameManager] LoginUIController registered. IsConnected: {IsConnected()}, LocalPlayer: {GetLocalPlayer() != null}");
            
            // If we're already connected and have no local player, show login
            if (IsConnected() && GetLocalPlayer() == null)
            {
                Debug.Log("[GameManager] Connected without player, showing login panel");
                ui.HideLoading();
                ui.ShowLoginPanel();
            }
        }
    }

    #region Login Event Setup

    private void SetupLoginEvents()
    {
        if (loginUI == null) return;

        loginUI.OnLoginRequested -= HandleLoginRequest;
        loginUI.OnRegisterRequested -= HandleRegisterRequest;

        loginUI.OnLoginRequested += HandleLoginRequest;
        loginUI.OnRegisterRequested += HandleRegisterRequest;
    }

    private void SetupReducerHandlers()
    {
        conn.Reducers.OnLogin += HandleLogin;
        conn.Reducers.OnCreatePlayer += HandleCreatePlayer;
        conn.Reducers.OnDebugMiningStatus += HandleDebugMiningStatus;
        conn.Reducers.OnDebugWavePacketStatus += HandleDebugWavePacketStatus;
        conn.Reducers.OnDebugGiveCrystal += HandleDebugGiveCrystal;
    }

    #endregion

    #region Reducer Handlers

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

        // Use the correct iteration pattern
        foreach (var player in instance.conn.Db.Player.Iter())
        {
            if (player.Identity == LocalIdentity)
                return player;
        }

        return null;
    }

    #endregion

    #region Login Handlers

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

        // Subscribe to all tables
        conn.SubscriptionBuilder()
            .OnApplied((ctx) => HandleSubscriptionApplied())
            .OnError((ctx, error) => HandleSubscriptionError(ctx, error))
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

        // Set up table event handlers
        SetupTableEventHandlers();

        // If we're in the login scene and have a session, try to restore it
        if (SceneManager.GetActiveScene().name == loginSceneName && AuthToken.HasSavedSession())
        {
            string sessionToken = AuthToken.LoadSessionToken();
            conn.Reducers.RestoreSession(sessionToken);
        }

        // Check if we already have a player
        var localPlayer = GetLocalPlayer();
        if (localPlayer != null)
        {
            Debug.Log($"Found existing player: {localPlayer.Name}");
            gameData.SetUsername(localPlayer.Name);

            // If we're in login scene with an existing player, go to game
            if (SceneManager.GetActiveScene().name == loginSceneName)
            {
                LoadGameScene();
            }
        }
        else if (SceneManager.GetActiveScene().name == gameSceneName)
        {
            // We're in game scene without a player, go back to login
            Debug.LogWarning("In game scene without a player, returning to login");
            LoadLoginScene();
        }
    }

    private void HandleSubscriptionError(ErrorContext ctx, Exception error)
    {
        Debug.LogError($"Subscription error: {error.Message}");
        loginUI?.ShowError($"Failed to sync with server: {error.Message}");
    }

    private void SetupTableEventHandlers()
    {
        // Player table events
        conn.Db.Player.OnInsert += OnPlayerInsert;
        conn.Db.Player.OnUpdate += OnPlayerUpdate;
        conn.Db.Player.OnDelete += OnPlayerDelete;
    }

    private void OnPlayerInsert(EventContext ctx, Player player)
    {
        if (player.Identity == conn.Identity)
        {
            Debug.Log($"Local player created: {player.Name}");
            gameData.SetUsername(player.Name);
            OnLocalPlayerReady?.Invoke(player);

            // If we're in login scene, transition to game
            if (SceneManager.GetActiveScene().name == loginSceneName)
            {
                LoadGameScene();
            }
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
            Debug.Log("Local player deleted");
            gameData.ClearSession();

            // Return to login
            if (SceneManager.GetActiveScene().name == gameSceneName)
            {
                LoadLoginScene();
            }
        }
    }

    #endregion

    void Update()
    {
        // Monitor player state
        if (Time.frameCount % 60 == 0) // Check every second
        {
            var currentScene = SceneManager.GetActiveScene().name;
            var localPlayer = GetLocalPlayer();

            if (currentScene == loginSceneName && localPlayer != null)
            {
                // We have a player in login scene, go to game
                Debug.Log("Player detected in login scene, transitioning to game");
                LoadGameScene();
            }
            else if (currentScene == gameSceneName && localPlayer == null && IsConnected())
            {
                // We're in game without a player, go back to login
                Debug.LogWarning("No player in game scene, returning to login");
                LoadLoginScene();
            }
        }
    }

    void OnDestroy()
    {
        if (conn != null && conn.IsActive)
        {
            conn.Disconnect();
        }
    }
}