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

    private void SetupLoginUI()
    {
        if (loginUI == null)
        {
            loginUI = FindObjectOfType<LoginUIController>();
            if (loginUI != null)
            {
                Debug.Log("[GameManager] Found LoginUIController in scene");
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
            loginUI?.ShowLoadingOverlay("Connecting to server...");
        }

        // Build the connection using the builder pattern
        string protocol = useSSL ? "wss" : "ws";
        string uri = $"{protocol}://{moduleAddress}";
        
        // Load saved auth token if available
        string token = AuthToken.LoadToken();

        // Build the connection
        var builder = DbConnection.Builder()
            .WithUri(uri)
            .WithModuleName(moduleName)
            .OnConnect(OnConnectedCallback)
            .OnConnectError(OnConnectErrorCallback)
            .OnDisconnect(OnDisconnectedCallback);

        // Add token if we have one
        if (!string.IsNullOrEmpty(token))
        {
            builder = builder.WithToken(token);
        }

        // Build and establish connection
        conn = builder.Build();

        // Setup database and reducers
        SetupDatabaseSubscriptions();
        SetupReducerCallbacks();

        // The connection happens automatically after Build()
        // Wait for connection result
        float timeout = 10f;
        float elapsed = 0f;

        while (isConnecting && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (isConnecting)
        {
            // Timeout
            isConnecting = false;
            Debug.LogError("Connection timeout");
            OnConnectionError?.Invoke("Connection timeout");
            loginUI?.OnConnectionFailed("Connection timeout");
        }
    }

    private IEnumerator ReconnectToServer()
    {
        isReconnecting = true;
        Debug.Log("Attempting to reconnect...");

        // Disconnect if still partially connected
        if (conn != null)
        {
            conn.Disconnect();
            yield return new WaitForSeconds(1f);
        }

        // Try to reconnect
        yield return ConnectToServer();
        
        isReconnecting = false;
    }

    #endregion

    #region Database Setup

    private void SetupDatabaseSubscriptions()
    {
        // Subscribe to all tables we need
        conn.SubscriptionBuilder()
            .OnApplied((ctx) => {
                Debug.Log("Subscriptions applied");
            })
            .OnError((ctx, error) => {
                Debug.LogError($"Subscription error: {error}");
            })
            .Subscribe(new string[] { "SELECT * FROM *" });

        // Subscribe to specific table events
        conn.Db.Player.OnInsert += OnPlayerInserted;
        conn.Db.Player.OnUpdate += OnPlayerUpdated;
        conn.Db.Player.OnDelete += OnPlayerDeleted;
        
        // Subscribe to session result table for login
        conn.Db.SessionResult.OnInsert += OnSessionResultReceived;
        // Note: OnUpdate has different signature - needs old and new values
        conn.Db.SessionResult.OnUpdate += OnSessionResultUpdated;
    }

    private void SetupReducerCallbacks()
    {
        // Authentication reducers
        conn.Reducers.OnRegisterAccount += HandleRegisterAccount;
        conn.Reducers.OnLoginWithSession += HandleLoginWithSession;
        conn.Reducers.OnLogin += HandleLogin;
        conn.Reducers.OnCreatePlayer += HandleCreatePlayer;
        
        // Debug reducers
        conn.Reducers.OnDebugMiningStatus += HandleDebugMiningStatus;
        conn.Reducers.OnDebugWavePacketStatus += HandleDebugWavePacketStatus;
        conn.Reducers.OnDebugGiveCrystal += HandleDebugGiveCrystal;

        // Error handling
        conn.OnUnhandledReducerError += OnUnhandledReducerError;
    }

    #endregion

    #region Connection Callbacks

    private void OnConnectedCallback(DbConnection connection, Identity identity, string token)
    {
        Debug.Log($"Connected to SpacetimeDB! Identity: {identity}, Token: {token?.Substring(0, Math.Min(10, token?.Length ?? 0))}...");
        
        isConnecting = false;
        OnConnected?.Invoke();

        // Store the SpacetimeDB token
        if (!string.IsNullOrEmpty(token))
        {
            AuthToken.SaveToken(token);
        }

        // Check if we have an existing player
        StartCoroutine(WaitForInitialData());
    }

    private void OnConnectErrorCallback(Exception error)
    {
        Debug.LogError($"Connection error: {error.Message}");
        isConnecting = false;
        
        OnConnectionError?.Invoke(error.Message);
        loginUI?.OnConnectionFailed(error.Message);
    }

    private void OnDisconnectedCallback(DbConnection connection, Exception error)
    {
        Debug.Log($"Disconnected from SpacetimeDB{(error != null ? $": {error.Message}" : "")}");
        OnDisconnected?.Invoke();

        // If we're in game scene, return to login
        if (SceneManager.GetActiveScene().name == gameSceneName)
        {
            LoadLoginScene();
        }
    }

    private IEnumerator WaitForInitialData()
    {
        // Wait a moment for initial data sync
        yield return new WaitForSeconds(0.5f);

        // Check if login UI is waiting
        if (loginUI != null)
        {
            loginUI.OnConnectionEstablished();
        }
        else
        {
            // Check if we have a player
            CheckLocalPlayer();
        }
    }

    #endregion

    #region Table Event Handlers

    private void OnPlayerInserted(EventContext ctx, Player player)
    {
        Debug.Log($"Player inserted: {player.Name} (Identity: {player.Identity})");

        if (player.Identity == LocalIdentity)
        {
            Debug.Log("Our player was created!");
            gameData?.SetUsername(player.Name);
            OnLocalPlayerReady?.Invoke(player);

            // Transition to game scene
            if (SceneManager.GetActiveScene().name != gameSceneName)
            {
                LoadGameScene();
            }
        }
    }

    private void OnPlayerUpdated(EventContext ctx, Player oldPlayer, Player newPlayer)
    {
        if (newPlayer.Identity == LocalIdentity)
        {
            Debug.Log($"Our player updated - World: {newPlayer.CurrentWorld}, Pos: {newPlayer.Position}");
        }
    }

    private void OnPlayerDeleted(EventContext ctx, Player player)
    {
        if (player.Identity == LocalIdentity)
        {
            Debug.Log("Our player was deleted!");
            LoadLoginScene();
        }
    }

    private void OnSessionResultReceived(EventContext ctx, SessionResult result)
    {
        if (result.Identity == conn.Identity)
        {
            Debug.Log($"[GameManager] Session token received: {result.SessionToken.Substring(0, 10)}...");
            
            // Get username from login UI
            if (loginUI != null && !string.IsNullOrEmpty(loginUI.lastLoginUsername))
            {
                // Save the session token
                AuthToken.SaveSession(result.SessionToken, loginUI.lastLoginUsername);
                
                Debug.Log($"[GameManager] Session saved for user: {loginUI.lastLoginUsername}");
            }
            
            // Request cleanup of the session result
            conn.Reducers.ClearSessionResult();
        }
    }

    private void OnSessionResultUpdated(EventContext ctx, SessionResult oldResult, SessionResult newResult)
    {
        // Handle updates to session result if needed
        if (newResult.Identity == conn.Identity)
        {
            Debug.Log($"[GameManager] Session token updated: {newResult.SessionToken.Substring(0, 10)}...");
            
            // Get username from login UI
            if (loginUI != null && !string.IsNullOrEmpty(loginUI.lastLoginUsername))
            {
                // Save the updated session token
                AuthToken.SaveSession(newResult.SessionToken, loginUI.lastLoginUsername);
                
                Debug.Log($"[GameManager] Updated session saved for user: {loginUI.lastLoginUsername}");
            }
            
            // Request cleanup of the session result
            conn.Reducers.ClearSessionResult();
        }
    }

    #endregion

    #region Reducer Response Handlers

    private void HandleRegisterAccount(ReducerEventContext ctx, string username, string displayName, string pin)
    {
        Debug.Log($"Register account reducer response for: {username}");
        if (ctx.Event.Status is Status.Failed(var reason))
        {
            loginUI?.ShowError(reason);
        }
        else
        {
            gameData?.SetUsername(displayName);
            loginUI?.ShowMessage("Registration successful! Please login.");
        }
    }

    private void HandleLoginWithSession(ReducerEventContext ctx, string username, string pin, string deviceInfo)
    {
        Debug.Log($"Login with session reducer response for: {username}");
        if (ctx.Event.Status is Status.Failed(var reason))
        {
            loginUI?.ShowError(reason);
        }
        else
        {
            // Session token will be in SessionResult table
            Debug.Log("Login successful, waiting for session token...");
        }
    }

    private void HandleLogin(ReducerEventContext ctx, string username, string password)
    {
        Debug.Log($"Login reducer response for: {username}");
        if (ctx.Event.Status is Status.Failed(var reason))
        {
            loginUI?.ShowError(reason);
        }
        else
        {
            gameData?.SetUsername(username);
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