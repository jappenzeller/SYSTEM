using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpacetimeDB;
using SpacetimeDB.Types;

// NOTE: This GameManager uses the SpacetimeDB Builder pattern for connections.
// The SpacetimeDBNetworkManager component on the GameObject handles message processing.

public partial class GameManager : MonoBehaviour
{
    private static GameManager instance;
    public static GameManager Instance => instance;

    [Header("Connection Settings")]
    [SerializeField] private string moduleAddress = "localhost:3000";
    [SerializeField] private string moduleName = "system";
    [SerializeField] private bool useSSL = false;

    [Header("Scene References")]
    [SerializeField] private string loginSceneName = "LoginScene";
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("UI References")]
    [SerializeField] private LoginUIController loginUI;

    // Connection
    private DbConnection conn;
    private bool isConnecting = false;
    private bool isReconnecting = false;

    // References
    private GameData gameData => GameData.Instance;

    // Events
    public static event Action OnConnected;
    public static event Action OnDisconnected;
    public static event Action<Player> OnLocalPlayerReady;
    public static event Action<WorldCoords> OnWorldChanged;
    public static event Action<string> OnConnectionError;

    // Properties
    public static DbConnection Conn => instance?.conn;
    public static Identity? LocalIdentity => instance?.conn?.Identity;

    #region Unity Lifecycle

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("GameManager initialized");
    }

    private void Start()
    {
        // Start connection immediately
        StartCoroutine(ConnectToServer());
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        // Cleanup subscriptions
        if (conn != null)
        {
            conn.Db.Player.OnInsert -= OnPlayerInsert;
            conn.Db.Player.OnUpdate -= OnPlayerUpdate;
            conn.Db.Player.OnDelete -= OnPlayerDelete;
            conn.Db.World.OnInsert -= OnWorldInsert;
            conn.Db.World.OnUpdate -= OnWorldUpdate;
        }
    }

    private void OnDestroy()
    {
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

    #region Scene Management

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"Scene loaded: {scene.name}");

        if (scene.name == loginSceneName)
        {
            // Find and setup login UI
            SetupLoginUI();

            // If we're already connected and have a player, go to game
            if (IsConnected() && GetLocalPlayer() != null)
            {
                LoadGameScene();
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
        }

        if (loginUI != null)
        {
            loginUI.OnLoginRequested += HandleLoginRequest;
            loginUI.OnRegisterRequested += HandleRegisterRequest;
            loginUI.OnCreateCharacterRequested += HandleCreateCharacterRequest;
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
            // Call the register_account reducer
            conn.Reducers.RegisterAccount(username, password);
        }
        else
        {
            loginUI?.ShowError("Not connected to server");
        }
    }

    private void HandleCreateCharacterRequest(string characterName)
    {
        Debug.Log($"Create character requested: {characterName}");

        if (conn != null && conn.IsActive)
        {
            // Call the create_player reducer
            conn.Reducers.CreatePlayer(characterName);
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
        Debug.Log($"Connected to SpacetimeDB! Identity: {identity}");
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
            Debug.Log("Local player deleted");
            // Return to login
            LoadLoginScene();
        }
    }

    private void OnWorldInsert(EventContext ctx, World world)
    {
        Debug.Log($"World inserted: {world.WorldName}");
    }

    private void OnWorldUpdate(EventContext ctx, World oldWorld, World newWorld)
    {
        Debug.Log($"World updated: {newWorld.WorldName}");
        if (gameData != null && gameData.GetCurrentWorldCoords() == oldWorld.WorldCoords)
        {
            OnWorldChanged?.Invoke(newWorld.WorldCoords);
        }
    }

    private void CheckLocalPlayer()
    {
        foreach (var player in conn.Db.Player.Iter())
        {
            if (player.Identity == conn.Identity)
            {
                Debug.Log($"Found existing player: {player.Name}");
                OnLocalPlayerReady?.Invoke(player);
                LoadGameScene();
                return;
            }
        }

        Debug.Log("No existing player found");

        // Show the login UI since no player exists
        loginUI?.HideLoading();
        loginUI?.ShowAuthPanel();
    }

    #endregion

    #region Reducer Handlers

    private void SetupReducerHandlers()
    {
        if (conn == null) return;
        
        // Login/Account reducers
        conn.Reducers.OnLogin += HandleLogin;
        conn.Reducers.OnRegisterAccount += HandleRegisterAccount;
        conn.Reducers.OnCreatePlayer += HandleCreatePlayer;

        // Debug reducers
        conn.Reducers.OnDebugMiningStatus += HandleDebugMiningStatus;
        conn.Reducers.OnDebugWavePacketStatus += HandleDebugWavePacketStatus;
        conn.Reducers.OnDebugGiveCrystal += HandleDebugGiveCrystal;

        // Logout handler
        conn.Reducers.OnLogout += (ctx) => {
            if (ctx.Event.Status == Status.Committed)
            {
                Debug.Log("Logout successful");
            }
        };
    }

    private void HandleLogin(ReducerEventContext ctx, string username, string password)
    {
        Debug.Log($"Login reducer response for: {username}");

        // Store username
        if (gameData != null)
        {
            gameData.SetUsername(username);
        }

        // Check if we have a player after login
        CheckLocalPlayer();

        // If no player exists, show character creation
        if (GetLocalPlayer() == null)
        {
            loginUI?.ShowCharacterCreation();
        }
    }

    private void HandleRegisterAccount(ReducerEventContext ctx, string username, string password)
    {
        Debug.Log($"Register account reducer response for: {username}");

        // Registration successful, show message and allow login
        loginUI?.ShowError("Registration successful! Please login.");
        // Note: ShowSuccess and ShowLogin methods don't exist in LoginUIController
        // You may need to add these methods or use alternative UI feedback
    }

    private void HandleCreatePlayer(ReducerEventContext ctx, string playerName)
    {
        Debug.Log($"Create player reducer response for: {playerName}");
        // Player creation is handled by OnPlayerInsert event
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
            // Generic error display for login-related failures
            // The error message from the server should be descriptive enough
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

    #endregion

    #region Logout Handling

    /// <summary>
    /// Initiate logout process
    /// </summary>
    public void Logout()
    {
        if (conn != null && conn.IsActive)
        {
            // Call logout reducer
            conn.Reducers.Logout();

            // Clear local session data
            AuthToken.ClearSession();
            GameData.Instance.ClearSession();

            // Disconnect
            StartCoroutine(LogoutSequence());
        }
    }

    private IEnumerator LogoutSequence()
    {
        // Wait a moment for logout to process
        yield return new WaitForSeconds(0.5f);

        // Disconnect from server
        if (conn != null && conn.IsActive)
        {
            conn.Disconnect();
        }

        // Wait for disconnection
        yield return new WaitForSeconds(0.5f);

        // Return to login scene
        LoadLoginScene();
    }

    #endregion
    
}