using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpacetimeDB;
using SpacetimeDB.Types;

/// <summary>
/// Central game manager that handles SpaceTimeDB connection and scene transitions.
/// Coordinates between UI and SpaceTimeDB, following the new event architecture.
/// </summary>
public partial class GameManager : MonoBehaviour
{
    private static GameManager instance;
    public static GameManager Instance => instance;

    [Header("Connection Settings")]
    [SerializeField] private string moduleAddress = "http://localhost:3000";
    [SerializeField] private string databaseName = "SYSTEM-24e";
    [SerializeField] private bool autoConnect = true;
    [SerializeField] private float connectionTimeout = 10f;

    [Header("Scene References")]
    [SerializeField] private string loginSceneName = "LoginScene";
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("UI References")]
    [SerializeField] private LoginUIController loginUI;

    // Connection state
    private static DbConnection conn;
    private bool isConnecting = false;
    private Coroutine connectionTimeoutCoroutine;

    // Properties
    public static DbConnection Conn => conn;
    public bool IsConnecting => isConnecting;

    // Events
    public static event Action<Player> OnLocalPlayerReady;
    public static event Action<WorldCoords> OnWorldChanged;
    public static event Action<string> OnConnectionError;
    public static event Action OnConnectionLost;

    // Cached data
    private GameData gameData;

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

        gameData = GameData.Instance;
    }

    void Start()
    {
        if (autoConnect)
        {
            Connect();
        }

        SetupUIEventHandlers();
    }

    void OnDestroy()
    {
        if (conn != null && conn.IsOpen)
        {
            conn.Disconnect();
        }

        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region Connection Management

    public void Connect()
    {
        if (conn != null && conn.IsOpen)
        {
            Debug.Log("Already connected to SpaceTimeDB");
            return;
        }

        if (isConnecting)
        {
            Debug.Log("Connection already in progress");
            return;
        }

        StartCoroutine(ConnectAsync());
    }

    private IEnumerator ConnectAsync()
    {
        isConnecting = true;
        UpdateConnectionStatus("Connecting...");

        // Try to load saved token
        string savedToken = AuthToken.LoadToken();
        Identity? savedIdentity = null;

        if (!string.IsNullOrEmpty(savedToken))
        {
            try
            {
                savedIdentity = Identity.FromHexString(savedToken);
                Debug.Log("Using saved identity token");
            }
            catch
            {
                Debug.LogWarning("Invalid saved token, will create new identity");
                savedIdentity = null;
            }
        }

        // Create connection
        Action<DbConnection, Identity, string> onConnect = OnConnected;
        Action<Exception, string> onConnectError = OnConnectError;
        Action onDisconnect = OnDisconnected;

        if (savedIdentity.HasValue)
        {
            conn = DbConnection.Connect(savedToken, moduleAddress, databaseName, onConnect, onConnectError, onDisconnect);
        }
        else
        {
            conn = DbConnection.Connect(moduleAddress, databaseName, onConnect, onConnectError, onDisconnect);
        }

        // Start timeout
        connectionTimeoutCoroutine = StartCoroutine(ConnectionTimeout());

        yield return null;
    }

    private IEnumerator ConnectionTimeout()
    {
        yield return new WaitForSeconds(connectionTimeout);

        if (isConnecting)
        {
            Debug.LogError("Connection timeout!");
            OnConnectError(null, "Connection timeout");
        }
    }

    public void Disconnect()
    {
        if (conn != null && conn.IsOpen)
        {
            conn.Disconnect();
        }
    }

    #endregion

    #region Connection Callbacks

    private void OnConnected(DbConnection connection, Identity identity, string authToken)
    {
        isConnecting = false;
        if (connectionTimeoutCoroutine != null)
        {
            StopCoroutine(connectionTimeoutCoroutine);
            connectionTimeoutCoroutine = null;
        }

        Debug.Log($"Connected to SpaceTimeDB! Identity: {identity}");

        // Save token for future connections
        AuthToken.SaveToken(identity.ToHexString());

        // Store identity in GameData
        if (gameData != null)
        {
            gameData.SetPlayerIdentity(identity);
        }

        // Set up event subscriptions
        SubscribeToSpaceTimeDBEvents();
        SetupReducerHandlers();

        UpdateConnectionStatus("Connected");

        // Check for existing player
        CheckLocalPlayer();
    }

    private void OnConnectError(Exception error, string message)
    {
        isConnecting = false;
        if (connectionTimeoutCoroutine != null)
        {
            StopCoroutine(connectionTimeoutCoroutine);
            connectionTimeoutCoroutine = null;
        }

        string errorMsg = error?.Message ?? message ?? "Unknown connection error";
        Debug.LogError($"Connection failed: {errorMsg}");

        UpdateConnectionStatus($"Connection failed: {errorMsg}");
        OnConnectionError?.Invoke(errorMsg);

        // Clear saved token if connection failed
        AuthToken.ClearToken();
    }

    private void OnDisconnected()
    {
        Debug.Log("Disconnected from SpaceTimeDB");
        UpdateConnectionStatus("Disconnected");
        OnConnectionLost?.Invoke();
    }

    #endregion

    #region UI Event Handlers

    private void SetupUIEventHandlers()
    {
        // Find LoginUIController if not assigned
        if (loginUI == null)
        {
            loginUI = FindFirstObjectByType<LoginUIController>();
        }

        if (loginUI != null)
        {
            // Subscribe to UI events
            // Note: LoginUIController handles its own internal events
        }
    }

    private void UpdateConnectionStatus(string status)
    {
        Debug.Log($"Connection Status: {status}");
        
        // Update UI if available
        if (loginUI != null)
        {
            loginUI.UpdateLoadingText(status);
        }
    }

    #endregion

    #region SpaceTimeDB Event Subscriptions

    private void SubscribeToSpaceTimeDBEvents()
    {
        if (conn == null) return;

        // Player events
        conn.Db.Player.OnInsert += OnPlayerInsert;
        conn.Db.Player.OnUpdate += OnPlayerUpdate;
        conn.Db.Player.OnDelete += OnPlayerDelete;

        // World events
        conn.Db.World.OnInsert += OnWorldInsert;
        conn.Db.World.OnUpdate += OnWorldUpdate;

        // Session result events (for retrieving session tokens)
        conn.Db.SessionResult.OnInsert += OnSessionResultInsert;
    }

    private void OnPlayerInsert(EventContext ctx, Player player)
    {
        if (player.Identity == conn.Identity)
        {
            Debug.Log($"Local player created: {player.Name}");
            OnLocalPlayerReady?.Invoke(player);
            LoadGameScene();
        }
    }

    private void OnPlayerUpdate(EventContext ctx, Player oldPlayer, Player newPlayer)
    {
        if (newPlayer.Identity == conn.Identity)
        {
            // Check for world change
            if (!IsSameWorldCoords(oldPlayer.CurrentWorld, newPlayer.CurrentWorld))
            {
                Debug.Log($"Player moved to world ({newPlayer.CurrentWorld.X}, {newPlayer.CurrentWorld.Y}, {newPlayer.CurrentWorld.Z})");
                OnWorldChanged?.Invoke(newPlayer.CurrentWorld);
            }
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

    private void OnSessionResultInsert(EventContext ctx, SessionResult result)
    {
        if (result.Identity == conn.Identity)
        {
            Debug.Log("Session token received");
            // Save the session token
            string username = gameData?.Username ?? "";
            AuthToken.SaveSession(result.SessionToken, username);
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
        // Player will be created after login
    }

    #endregion

    #region Reducer Handlers

    private void SetupReducerHandlers()
    {
        if (conn == null) return;
        
        // Authentication reducers
        conn.Reducers.OnLoginWithSession += HandleLoginWithSession;
        conn.Reducers.OnRegisterAccount += HandleRegisterAccount;
        conn.Reducers.OnRestoreSession += HandleRestoreSession;
        conn.Reducers.OnLogout += HandleLogout;
        
        // Player reducers
        conn.Reducers.OnCreatePlayer += HandleCreatePlayer;

        // Debug reducers
        conn.Reducers.OnDebugMiningStatus += HandleDebugMiningStatus;
        conn.Reducers.OnDebugWavePacketStatus += HandleDebugWavePacketStatus;
        conn.Reducers.OnDebugGiveCrystal += HandleDebugGiveCrystal;
    }

    private void HandleLoginWithSession(ReducerEventContext ctx, string username, string pin, string deviceInfo)
    {
        Debug.Log($"Login reducer response for: {username}");

        if (ctx.Event.Status is Status.Committed)
        {
            // Store username
            if (gameData != null)
            {
                gameData.SetUsername(username);
            }
            
            // Session token will be received via SessionResult table insert
        }
        else if (ctx.Event.Status is Status.Failed failed)
        {
            Debug.LogError($"Login failed: {failed.Reason}");
        }
    }

    private void HandleRegisterAccount(ReducerEventContext ctx, string username, string displayName, string pin)
    {
        Debug.Log($"Register account reducer response for: {username}");

        if (ctx.Event.Status is Status.Committed)
        {
            Debug.Log("Registration successful");
        }
        else if (ctx.Event.Status is Status.Failed failed)
        {
            Debug.LogError($"Registration failed: {failed.Reason}");
        }
    }

    private void HandleRestoreSession(ReducerEventContext ctx, string sessionToken)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            Debug.Log("Session restored successfully");
        }
        else if (ctx.Event.Status is Status.Failed failed)
        {
            Debug.LogError($"Session restore failed: {failed.Reason}");
            AuthToken.ClearSession();
        }
    }

    private void HandleLogout(ReducerEventContext ctx)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            Debug.Log("Logout successful");
            AuthToken.ClearSession();
            GameData.Instance.ClearSession();
        }
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

    #endregion

    #region Public Methods for UI

    /// <summary>
    /// Called by LoginUIController to perform login
    /// </summary>
    public void Login(string username, string pin)
    {
        if (conn == null || !conn.IsOpen)
        {
            Debug.LogError("Not connected to SpaceTimeDB");
            return;
        }

        string deviceInfo = AuthToken.GetDeviceInfo();
        conn.Reducers.LoginWithSession(username, pin, deviceInfo);
    }

    /// <summary>
    /// Called by LoginUIController to perform registration
    /// </summary>
    public void Register(string username, string displayName, string pin)
    {
        if (conn == null || !conn.IsOpen)
        {
            Debug.LogError("Not connected to SpaceTimeDB");
            return;
        }

        conn.Reducers.RegisterAccount(username, displayName, pin);
    }

    /// <summary>
    /// Called by LoginUIController to restore session
    /// </summary>
    public void RestoreSession(string sessionToken)
    {
        if (conn == null || !conn.IsOpen)
        {
            Debug.LogError("Not connected to SpaceTimeDB");
            return;
        }

        conn.Reducers.RestoreSession(sessionToken);
    }

    /// <summary>
    /// Initiate logout process
    /// </summary>
    public void Logout()
    {
        if (conn != null && conn.IsOpen)
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
        if (conn != null && conn.IsOpen)
        {
            conn.Disconnect();
        }

        // Wait for disconnection
        yield return new WaitForSeconds(0.5f);

        // Return to login scene
        LoadLoginScene();
    }

    public static Player GetLocalPlayer()
    {
        if (!IsConnected()) return null;

        foreach (var player in conn.Db.Player.Iter())
        {
            if (player.Identity == conn.Identity)
            {
                return player;
            }
        }

        return null;
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
        return instance != null && conn != null && conn.IsOpen;
    }

    private bool IsSameWorldCoords(WorldCoords w1, WorldCoords w2)
    {
        return w1.X == w2.X && w1.Y == w2.Y && w1.Z == w2.Z;
    }

    #endregion
}