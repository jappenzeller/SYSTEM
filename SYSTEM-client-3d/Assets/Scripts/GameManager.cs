using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpacetimeDB;
using SpacetimeDB.Types;
using SpacetimeDB.ClientApi;
using TMPro;

public partial class GameManager : MonoBehaviour
{
    private static GameManager instance;
    public static GameManager Instance => instance;
    
    [Header("Connection Settings")]
    [SerializeField] private string moduleAddress = "your-module.spacetimedb.com";
    [SerializeField] private string moduleName = "your-module-name";
    [SerializeField] private bool useSSL = true;
    
    [Header("Scene References")]
    [SerializeField] private string loginSceneName = "LoginScene";
    [SerializeField] private string gameSceneName = "GameScene";
    
    [Header("UI References")]
    [SerializeField] private LoginUIController loginUI;
    [SerializeField] private GameObject connectingPanel;
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TextMeshProUGUI errorText;
    
    // Connection
    private DbConnection conn;
    private bool isConnecting = false;
    private bool isConnected = false;
    
    // References
    private GameData gameData => GameData.Instance;
    
    // Events - made static for backward compatibility
    public static event Action OnConnected;
    public static event Action OnDisconnected;
    public static event Action<WorldCoords> OnWorldChanged;
    public static event Action<Player> OnLocalPlayerReady;
    
    // Instance events
    public event Action<Player> OnPlayerCreated;
    public event Action<string> OnConnectionError;
    
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    void Start()
    {
        // Initialize connection
        InitializeConnection();
        
        // Check current scene
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == loginSceneName)
        {
            SetupLoginUI();
        }
        else if (currentScene == gameSceneName)
        {
            // If we're in game scene without connection, go back to login
            if (!isConnected)
            {
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
    
    #region Connection Management
    
    private void InitializeConnection()
    {
        conn = new DbConnection();
    }
    
    public void Connect(string username = null)
    {
        if (isConnecting || isConnected)
        {
            Debug.LogWarning("Already connected or connecting");
            return;
        }
        
        isConnecting = true;
        ShowConnectingUI(true);
        
        // Store username for later use
        if (!string.IsNullOrEmpty(username))
        {
            gameData.SetUsername(username);
        }
        
        StartCoroutine(ConnectToServer());
    }
    
    private IEnumerator ConnectToServer()
    {
        Debug.Log($"Connecting to {moduleName} at {moduleAddress}...");
        
        // The actual connection happens synchronously when we first call a reducer
        // or subscribe. For now, we'll consider ourselves "connecting"
        // and the actual connection will happen when we subscribe
        
        yield return null; // Small delay
        
        // Consider connection successful and proceed to subscription
        HandleConnected();
    }
    
    public void Disconnect()
    {
        if (conn != null && conn.IsActive)
        {
            conn.Disconnect();
        }
        isConnected = false;
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
        }
    }
    
    private void HandleLoginRequest(string username, string password)
    {
        Debug.Log($"Login requested for user: {username}");
        
        // Store credentials in GameData
        gameData.SetUsername(username);
        
        // First connect, then login
        StartCoroutine(ConnectAndLogin(username, password));
    }
    
    private void HandleRegisterRequest(string username, string password)
    {
        Debug.Log($"Registration requested for user: {username}");
        
        // Store credentials in GameData
        gameData.SetUsername(username);
        
        // First connect, then register
        StartCoroutine(ConnectAndRegister(username, password));
    }
    
    private IEnumerator ConnectAndLogin(string username, string password)
    {
        // Connect first
        yield return ConnectToServer();
        
        if (isConnected)
        {
            // Now login
            conn.Reducers.Login(username, password);
        }
    }
    
    private IEnumerator ConnectAndRegister(string username, string password)
    {
        // Connect first
        yield return ConnectToServer();
        
        if (isConnected)
        {
            // Now register
            conn.Reducers.RegisterAccount(username, password);
        }
    }
    
    #endregion
    
    #region Connection Event Handlers
    
    private void HandleConnected()
    {
        Debug.Log("Preparing to connect to SpacetimeDB...");
        isConnecting = false;
        isConnected = true;
        ShowConnectingUI(false);
        
        // Subscribe to all tables with proper event context
        // The actual connection happens here when we subscribe
        conn.SubscriptionBuilder()
            .OnApplied((ctx) => HandleSubscriptionApplied(ctx))
            .OnError((ctx, error) => HandleSubscriptionError(ctx, error))
            .Subscribe(new string[] { "SELECT * FROM *" });
    }
    
    private void HandleSubscriptionApplied(SubscriptionEventContext ctx)
    {
        Debug.Log("Subscription successful!");
        
        // Now we have a connection identity
        if (conn.Identity.HasValue)
        {
            gameData.SetPlayerIdentity(conn.Identity.Value);
        }
        
        // Hide login UI if it exists
        if (loginUI != null)
        {
            loginUI.gameObject.SetActive(false);
        }
        
        // Fire connected event
        OnConnected?.Invoke();
        
        // Check if we need to create a player or we already have one
        CheckOrCreatePlayer();
    }
    
    private void HandleSubscriptionError(ErrorContext ctx, Exception error)
    {
        Debug.LogError($"Subscription error: {error.Message}");
        ShowError($"Failed to sync data: {error.Message}");
    }
    
    #endregion
    
    #region Player Management
    
    private void CheckOrCreatePlayer()
    {
        // Check if player exists
        Player existingPlayer = GetLocalPlayer();
        
        if (existingPlayer != null)
        {
            Debug.Log($"Found existing player: {existingPlayer.Name}");
            OnPlayerFound(existingPlayer);
        }
        else
        {
            // Need to create player - but check if we're logged in first
            if (string.IsNullOrEmpty(gameData.Username))
            {
                Debug.Log("No username set, waiting for login/register");
                return;
            }
            
            // If we have a username but no player, create one
            Debug.Log($"Creating new player: {gameData.Username}");
            conn.Reducers.CreatePlayer(gameData.Username);
        }
    }
    
    private void OnPlayerFound(Player player)
    {
        OnPlayerCreated?.Invoke(player);
        OnLocalPlayerReady?.Invoke(player);
        
        // Load game scene
        LoadGameScene();
    }
    
    #endregion
    
    #region Scene Management
    
    private void LoadLoginScene()
    {
        if (SceneManager.GetActiveScene().name != loginSceneName)
        {
            SceneManager.LoadScene(loginSceneName);
        }
    }
    
    private void LoadGameScene()
    {
        if (SceneManager.GetActiveScene().name != gameSceneName)
        {
            StartCoroutine(LoadGameSceneAsync());
        }
    }
    
    private IEnumerator LoadGameSceneAsync()
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(gameSceneName);
        
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
        
        // Setup game scene
        SetupGameScene();
    }
    
    private void SetupGameScene()
    {
        // Subscribe to reducer events
        SubscribeToReducerEvents();
        
        // Find and setup game components
        var worldManager = FindFirstObjectByType<WorldManager>();
        if (worldManager != null)
        {
            // WorldManager doesn't have Initialize method, it self-initializes
            // Just ensure it has reference to connection if needed
        }
        
        var playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null)
        {
            var localPlayer = GetLocalPlayer();
            if (localPlayer != null)
            {
                // Initialize with proper parameters (PlayerId, bool, float)
                playerController.Initialize(localPlayer, true, 50f);
            }
        }
        
        // Notify that we've entered the game
        var currentPlayer = GetLocalPlayer();
        if (currentPlayer != null)
        {
            conn.Reducers.UpdatePlayerPosition(
                currentPlayer.CurrentWorld,
                currentPlayer.Position,
                currentPlayer.Rotation
            );
        }
    }
    
    private void SubscribeToReducerEvents()
    {
        // Player events
        conn.Reducers.OnCreatePlayer += HandleCreatePlayerResult;
        conn.Reducers.OnLogin += HandleLoginResult;
        conn.Reducers.OnRegisterAccount += HandleRegisterResult;
        conn.Reducers.OnUpdatePlayerPosition += HandleUpdatePositionResult;
        
        // Mining events
        conn.Reducers.OnStartMining += HandleStartMiningResult;
        conn.Reducers.OnStopMining += HandleStopMiningResult;
        conn.Reducers.OnExtractWavePacket += HandleExtractWavePacketResult;
        conn.Reducers.OnCaptureWavePacket += HandleWavePacketCaptured;
        
        // Connection events
        conn.Reducers.OnConnect += HandleConnectReducer;
        conn.Reducers.OnDisconnect += HandleDisconnectReducer;
    }
    
    #endregion
    
    #region Reducer Event Handlers
    
    private void HandleCreatePlayerResult(ReducerEventContext ctx, string name)
    {
        Debug.Log($"Player created: {name}");
        
        // Player should now exist, find and setup
        var player = GetLocalPlayer();
        if (player != null)
        {
            OnPlayerFound(player);
        }
    }
    
    private void HandleLoginResult(ReducerEventContext ctx, string username, string password)
    {
        Debug.Log($"Login successful for: {username}");
        // IsLoggedIn is set automatically by GameData.SetPlayerIdentity
        
        // Check for player
        CheckOrCreatePlayer();
    }
    
    private void HandleRegisterResult(ReducerEventContext ctx, string username, string password)
    {
        Debug.Log($"Registration successful for: {username}");
        
        // Auto-login after registration
        conn.Reducers.Login(username, password);
    }
    
    private void HandleUpdatePositionResult(ReducerEventContext ctx, WorldCoords worldCoords, DbVector3 position, DbQuaternion rotation)
    {
        // Check if world changed
        var currentCoords = gameData.GetCurrentWorldCoords();
        if (worldCoords.X != currentCoords.X || worldCoords.Y != currentCoords.Y || worldCoords.Z != currentCoords.Z)
        {
            gameData.SetCurrentWorldCoords(worldCoords);
            OnWorldChanged?.Invoke(worldCoords);
        }
    }
    
    private void HandleStartMiningResult(ReducerEventContext ctx, ulong orbId)
    {
        Debug.Log($"Mining started on orb: {orbId}");
    }
    
    private void HandleStopMiningResult(ReducerEventContext ctx)
    {
        Debug.Log("Mining stopped");
    }
    
    private void HandleExtractWavePacketResult(ReducerEventContext ctx, ulong orbId)
    {
        Debug.Log($"Wave packet extracted from orb: {orbId}");
    }
    
    private void HandleWavePacketCaptured(ReducerEventContext ctx, ulong wavePacketId)
    {
        Debug.Log($"Wave packet captured: {wavePacketId}");
    }
    
    private void HandleConnectReducer(ReducerEventContext ctx)
    {
        Debug.Log("Connect reducer called");
    }
    
    private void HandleDisconnectReducer(ReducerEventContext ctx)
    {
        Debug.Log("Disconnect reducer called");
        HandleDisconnected();
    }
    
    private void HandleDisconnected()
    {
        Debug.Log("Disconnected from SpacetimeDB");
        isConnected = false;
        
        gameData.ClearSession();
        
        OnDisconnected?.Invoke();
        
        // If we're in game scene, go back to login
        if (SceneManager.GetActiveScene().name == gameSceneName)
        {
            LoadLoginScene();
        }
    }
    
    #endregion
    
    #region UI Helpers
    
    private void ShowConnectingUI(bool show)
    {
        if (connectingPanel != null)
        {
            connectingPanel.SetActive(show);
        }
    }
    
    private void ShowError(string error)
    {
        if (errorPanel != null && errorText != null)
        {
            errorText.text = error;
            errorPanel.SetActive(true);
            
            // Auto-hide after 5 seconds
            StartCoroutine(HideErrorAfterDelay(5f));
        }
        else
        {
            Debug.LogError($"UI Error: {error}");
        }
    }
    
    private IEnumerator HideErrorAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (errorPanel != null)
        {
            errorPanel.SetActive(false);
        }
    }
    
    #endregion
    
    #region Helper Methods
    
    private Player GetLocalPlayer()
    {
        if (conn?.Identity == null) return null;
        
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
    
    #region Public Properties
    
    public DbConnection Connection => conn;
    public bool IsConnectionActive => isConnected;
    public static bool IsConnected() => instance?.isConnected ?? false;
    public static DbConnection Conn => instance?.conn;
    public static Identity? LocalIdentity => instance?.conn?.Identity;
    
    public static Player GetCurrentPlayer()
    {
        return instance?.GetLocalPlayer();
    }
    
    #endregion
}