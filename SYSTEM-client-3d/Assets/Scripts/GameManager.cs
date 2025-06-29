using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpacetimeDB;
using SpacetimeDB.Types;
using SpacetimeDB.ClientApi;

public partial class GameManager : MonoBehaviour
{
    private static GameManager instance;
    public static GameManager Instance => instance;
    
    [Header("Connection Settings")]
    [SerializeField] private string moduleAddress = "stdb-website.spacetimedb.com";
    [SerializeField] private string moduleName = "quanta-mining-system";
    [SerializeField] private bool useSSL = true;
    
    [Header("Scene References")]
    [SerializeField] private string loginSceneName = "LoginScene";
    [SerializeField] private string gameSceneName = "GameScene";
    
    [Header("UI References")]
    [SerializeField] private LoginUIController loginUI;
    [SerializeField] private GameObject connectingPanel;
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TMPro.TextMeshProUGUI errorText;
    
    // Connection
    private DbConnection conn;
    private bool isConnecting = false;
    private bool isReconnecting = false;
    
    // References
    private GameData gameData => GameData.Instance;
    
    // Events
    public event Action OnConnected;
    public event Action OnDisconnected;
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
            if (conn == null || !conn.IsActive)
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
        
        // Subscribe to connection events
        conn.OnConnect += HandleConnected;
        conn.OnConnectError += HandleConnectionError;
        conn.OnDisconnect += HandleDisconnected;
    }
    
    public void Connect(string username = null)
    {
        if (isConnecting || (conn != null && conn.IsActive))
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
        
        string protocol = useSSL ? "wss" : "ws";
        string url = $"{protocol}://{moduleAddress}/{moduleName}";
        
        // Connect with stored credentials if available
        Identity? identity = gameData.PlayerIdentity;
        string authToken = null; // Add auth token support if needed
        
        conn.Connect(url, identity, authToken);
        
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
            ShowConnectingUI(false);
            ShowError("Connection timeout");
        }
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
            // Fixed: Use += instead of = for event subscription
            loginUI.OnLoginRequested += HandleLoginRequest;
            loginUI.OnRegisterRequested += HandleRegisterRequest;
        }
    }
    
    private void HandleLoginRequest(string username, string password)
    {
        Debug.Log($"Login requested for user: {username}");
        
        // For now, just connect with the username
        // In a real implementation, you'd validate credentials
        Connect(username);
    }
    
    private void HandleRegisterRequest(string username, string password)
    {
        Debug.Log($"Registration requested for user: {username}");
        
        // For now, treat registration same as login
        // In a real implementation, you'd create a new account
        Connect(username);
    }
    
    #endregion
    
    #region Connection Event Handlers
    
    private void HandleConnected()
    {
        Debug.Log("Connected to SpacetimeDB!");
        isConnecting = false;
        ShowConnectingUI(false);
        
        // Subscribe to all tables
        conn.SubscriptionBuilder()
            .OnApplied(() => HandleSubscriptionApplied())
            .OnError(HandleSubscriptionError)
            .Subscribe("SELECT * FROM *");
    }
    
    private void HandleConnectionError(string error, string message)
    {
        Debug.LogError($"Connection error: {error} - {message}");
        isConnecting = false;
        ShowConnectingUI(false);
        ShowError($"Connection failed: {message}");
        OnConnectionError?.Invoke(message);
    }
    
    private void HandleDisconnected()
    {
        Debug.Log("Disconnected from SpacetimeDB");
        isConnecting = false;
        
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
        
        // Store connection info
        gameData.SetPlayerIdentity(conn.Identity.Value);
        
        // Fixed: Call method if it exists, or handle inline
        if (loginUI != null)
        {
            // If OnConnectionSuccess doesn't exist, handle it here
            loginUI.gameObject.SetActive(false);
        }
        
        // Check if we need to create a player
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
        Player existingPlayer = null;
        foreach (var player in conn.Db.Player.Iter())
        {
            if (player.Identity == conn.Identity)
            {
                existingPlayer = player;
                break;
            }
        }
        
        if (existingPlayer != null)
        {
            Debug.Log($"Found existing player: {existingPlayer.Name}");
            OnPlayerFound(existingPlayer);
        }
        else
        {
            // Create new player
            string playerName = gameData.Username;
            if (string.IsNullOrEmpty(playerName))
            {
                playerName = $"Player_{UnityEngine.Random.Range(1000, 9999)}";
            }
            
            Debug.Log($"Creating new player: {playerName}");
            conn.Reducers.CreatePlayer(playerName);
        }
    }
    
    private void OnPlayerFound(Player player)
    {
        OnPlayerCreated?.Invoke(player);
        
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
            worldManager.Initialize(conn);
        }
        
        var playerController = FindFirstObjectByType<PlayerController>();
        if (playerController != null)
        {
            var player = GetLocalPlayer();
            if (player != null)
            {
                playerController.Initialize(conn, player.PlayerId);
            }
        }
        
        var miningController = FindFirstObjectByType<MiningController>();
        if (miningController != null)
        {
            // Mining controller will get conn reference from inspector
        }
        
        // Notify that we've entered the game
        var player = GetLocalPlayer();
        if (player != null)
        {
            conn.Reducers.UpdatePlayerPosition(
                player.CurrentWorld,
                player.Position,
                player.Rotation
            );
        }
    }
    
    private void SubscribeToReducerEvents()
    {
        // Fixed: Use correct event names from autogenerated code
        conn.Reducers.OnCreatePlayer += HandlePlayerCreated;
        conn.Reducers.OnUpdatePlayerPosition += OnUpdatePositionResult;
        conn.Reducers.OnChooseStartingCrystal += HandleCrystalChosen;
        conn.Reducers.OnStartMining += HandleMiningStarted;
        conn.Reducers.OnStopMining += HandleMiningStopped;
        conn.Reducers.OnSendWavePacket += HandleWavePacketSent;
        conn.Reducers.OnCaptureWavePacket += HandleWavePacketCaptured;
    }
    
    #endregion
    
    #region Reducer Event Handlers
    
    private void HandlePlayerCreated(ReducerEventContext ctx, string name)
    {
        Debug.Log($"Player created successfully: {name}");
        
        // Find the newly created player
        Player newPlayer = null;
        foreach (var player in ctx.Db.Player.Iter())
        {
            if (player.Identity == ctx.Identity)
            {
                newPlayer = player;
                break;
            }
        }
        
        if (newPlayer != null)
        {
            OnPlayerFound(newPlayer);
        }
        else
        {
            Debug.LogError("Failed to find newly created player");
            ShowError("Failed to find newly created player");
        }
    }
    
    private void OnUpdatePositionResult(ReducerEventContext ctx, WorldCoords worldCoords, DbVector3 position, DbVector3 rotation)
    {
        // Position updates don't typically fail, just log for debug
        Debug.Log($"Position updated to world {worldCoords.X},{worldCoords.Y},{worldCoords.Z}");
    }
    
    private void HandleCrystalChosen(ReducerEventContext ctx, CrystalType crystalType)
    {
        Debug.Log($"Crystal chosen: {crystalType}");
    }
    
    private void HandleMiningStarted(ReducerEventContext ctx, ulong orbId)
    {
        Debug.Log($"Mining started on orb {orbId}");
    }
    
    private void HandleMiningStopped(ReducerEventContext ctx)
    {
        Debug.Log("Mining stopped");
    }
    
    private void HandleWavePacketSent(ReducerEventContext ctx, ulong playerId, ulong wavePacketId, WavePacketSignature signature, ulong expectedArrival)
    {
        Debug.Log($"Wave packet sent: {wavePacketId}");
    }
    
    private void HandleWavePacketCaptured(ReducerEventContext ctx, ulong wavePacketId)
    {
        Debug.Log($"Wave packet captured: {wavePacketId}");
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
    public bool IsConnectionActive => conn != null && conn.IsActive;
    public static bool IsConnected() => instance?.conn != null && instance.conn.IsActive;
    public static DbConnection Conn => instance?.conn;
    public static Identity? LocalIdentity => instance?.conn?.Identity;
    
    public static Player GetCurrentPlayer()
    {
        return instance?.GetLocalPlayer();
    }
    
    #endregion
}

// Extension class for LoginUIController if methods don't exist
public static class LoginUIControllerExtensions
{
    public static void OnConnectionSuccess(this LoginUIController ui)
    {
        // Hide the login UI
        if (ui != null && ui.gameObject != null)
        {
            ui.gameObject.SetActive(false);
        }
    }
    
    public static void OnConnectionLost(this LoginUIController ui)
    {
        // Show the login UI
        if (ui != null && ui.gameObject != null)
        {
            ui.gameObject.SetActive(true);
        }
    }
}