// GameManager.cs - Complete implementation with all SpaceTimeDB best practices
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpacetimeDB.Types;
using SpacetimeDB.ClientApi;
using SpacetimeDB;
using TMPro;
using UnityEngine.UI;

public partial class GameManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Singleton Implementation
    // ─────────────────────────────────────────────────────────────────────────────
    
    private static GameManager instance;
    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GameManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("GameManager");
                    instance = go.AddComponent<GameManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Configuration
    // ─────────────────────────────────────────────────────────────────────────────
    
    [Header("Connection Settings")]
    [SerializeField] private string serverIP = "localhost";
    [SerializeField] private string serverPort = "3000";
    [SerializeField] private string dbName = "myproject-myname";
    [SerializeField] private float reconnectDelay = 5f;
    [SerializeField] private int maxReconnectAttempts = 3;
    
    [Header("Frame Tick Settings")]
    [SerializeField] private FrameTickManager.TickMode defaultTickMode = FrameTickManager.TickMode.EveryFrame;
    [SerializeField] private bool autoInitializeFrameTick = true;
    [SerializeField] private float slowTickWarningThreshold = 16.0f; // ms
    [SerializeField] private float criticalTickThreshold = 32.0f; // ms
    
    [Header("UI References")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private Button connectButton;
    [SerializeField] private TextMeshProUGUI errorText;
    [SerializeField] private TextMeshProUGUI connectionStatusText;
    [SerializeField] private bool autoTransitionToCenterWorld = true;
    
    [Header("Debug Settings")]
    [SerializeField] private bool verboseLogging = false;
    [SerializeField] private bool autoConnectInEditor = true;
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Static Properties
    // ─────────────────────────────────────────────────────────────────────────────
    
    public static DbConnection Conn { get; private set; }
    public static SpacetimeDB.Identity? LocalIdentity { get; private set; }
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Private Fields
    // ─────────────────────────────────────────────────────────────────────────────
    
    // Remove LoginUI if it doesn't exist in your project
    // private LoginUI loginUI;
    private bool isConnecting = false;
    private int reconnectAttempts = 0;
    private Coroutine reconnectCoroutine;
    private SubscriptionHandle mainSubscription;
    private Dictionary<string, SubscriptionHandle> activeSubscriptions = new Dictionary<string, SubscriptionHandle>();
    
    // Performance monitoring
    private int slowTickCount = 0;
    private float performanceCheckInterval = 5f;
    private float lastPerformanceCheck = 0f;
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Events
    // ─────────────────────────────────────────────────────────────────────────────
    
    public static event Action<DbConnection, SpacetimeDB.Identity> OnConnected;
    public static event Action<Exception> OnDisconnected;
    public static event Action<Player> OnLocalPlayerReady;
    public static event Action<WorldCoords> OnWorldChanged;
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────────
    
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        LogDebug("GameManager initialized");
    }

    void Start()
    {
        InitializeUI();
        
        // Auto-connect in editor for faster development
        #if UNITY_EDITOR
        if (autoConnectInEditor)
        {
            StartCoroutine(AutoConnectInEditor());
        }
        #endif
    }
    
    void Update()
    {
        // Monitor performance
        if (Time.time - lastPerformanceCheck > performanceCheckInterval)
        {
            lastPerformanceCheck = Time.time;
            CheckPerformanceAndAdjust();
        }
        
        // Update connection status UI
        UpdateConnectionStatusUI();
    }
    
    void OnDestroy()
    {
        Cleanup();
    }
    
    void OnApplicationPause(bool pauseStatus)
    {
        HandleApplicationPause(pauseStatus);
    }
    
    void OnApplicationFocus(bool hasFocus)
    {
        HandleApplicationFocus(hasFocus);
    }
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Initialization
    // ─────────────────────────────────────────────────────────────────────────────
    
    private void InitializeUI()
    {
        // Setup login UI component if you have one
        // loginUI = GetComponent<LoginUI>();
        // if (loginUI != null)
        // {
        //     loginUI.OnLoginRequested += OnLoginRequested;
        //     loginUI.OnCreateAccountRequested += OnCreateAccountRequested;
        //     LogDebug("LoginUI component initialized");
        // }
        
        // Setup connect button
        if (connectButton != null)
        {
            connectButton.onClick.RemoveAllListeners();
            connectButton.onClick.AddListener(() => ConnectToSpacetime());
        }
        
        // Setup username input
        if (usernameInput != null)
        {
            // Load saved username
            string savedUsername = PlayerPrefs.GetString("LastUsername", "");
            if (!string.IsNullOrEmpty(savedUsername))
            {
                usernameInput.text = savedUsername;
            }
        }
    }
    
    private IEnumerator AutoConnectInEditor()
    {
        yield return new WaitForSeconds(0.5f);
        
        if (!IsConnected())
        {
            LogDebug("Auto-connecting in editor...");
            ConnectToSpacetime();
        }
    }
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Connection Management
    // ─────────────────────────────────────────────────────────────────────────────
    
    public void ConnectToSpacetime()
    {
        if (isConnecting)
        {
            LogDebug("Already attempting to connect");
            return;
        }
        
        if (Conn != null && Conn.IsActive)
        {
            LogDebug("Already connected");
            return;
        }
        
        isConnecting = true;
        ShowError(""); // Clear any previous errors
        UpdateConnectionButton(false);
        
        string serverUrl = $"http://{serverIP}:{serverPort}";
        
        try
        {
            LogDebug($"Connecting to {serverUrl}/{dbName}...");
            
            var builder = DbConnection.Builder()
                .WithUri(serverUrl)
                .WithModuleName(dbName)
                .OnConnect(HandleConnect)
                .OnConnectError((ctx, ex) => HandleConnectError(ex))
                .OnDisconnect((ctx, ex) => HandleDisconnect(ex));
            
            // Use saved token if available
            string savedToken = AuthToken.LoadToken();
            if (!string.IsNullOrEmpty(savedToken))
            {
                builder = builder.WithToken(savedToken);
                LogDebug("Using saved authentication token");
            }
            
            Conn = builder.Build();
        }
        catch (Exception ex)
        {
            isConnecting = false;
            UpdateConnectionButton(true);
            ShowError($"Connection failed: {ex.Message}");
            LogError($"Failed to create connection: {ex}");
        }
    }
    
    private void HandleConnect(DbConnection conn, SpacetimeDB.Identity identity, string token)
    {
        isConnecting = false;
        LocalIdentity = identity;
        reconnectAttempts = 0;
        
        Log($"Connected successfully! Identity: {identity}");
        
        // Save auth token for future sessions
        if (!string.IsNullOrEmpty(token))
        {
            AuthToken.SaveToken(token);
        }
        
        // Store identity in GameData
        if (GameData.Instance != null)
        {
            GameData.Instance.SetPlayerIdentity(identity);
        }
        
        // Initialize frame tick manager
        if (autoInitializeFrameTick)
        {
            SetupFrameTicking();
        }
        
        // Fire connected event
        OnConnected?.Invoke(conn, identity);
        
        // Subscribe to tables with optimized queries
        SubscribeToGameData();
    }
    
    private void HandleConnectError(Exception ex)
    {
        isConnecting = false;
        UpdateConnectionButton(true);
        
        LogError($"Connection error: {ex}");
        
        // Check for specific error types
        if (ex.Message.Contains("invalid token"))
        {
            LogDebug("Invalid token detected, clearing and retrying...");
            AuthToken.ClearToken();
            
            // Retry without token
            if (reconnectAttempts < maxReconnectAttempts)
            {
                reconnectAttempts++;
                StartCoroutine(RetryConnection());
            }
            else
            {
                ShowError("Authentication failed. Please login again.");
            }
        }
        else
        {
            ShowError($"Connection failed: {ex.Message}");
            
            // Attempt reconnection
            if (reconnectAttempts < maxReconnectAttempts)
            {
                reconnectAttempts++;
                reconnectCoroutine = StartCoroutine(RetryConnection());
            }
        }
    }
    
    private void HandleDisconnect(Exception ex)
    {
        isConnecting = false;
        LocalIdentity = null;
        UpdateConnectionButton(true);
        
        // Stop frame ticking
        if (FrameTickManager.Instance != null)
        {
            FrameTickManager.Instance.Pause();
        }
        
        // Clear subscriptions
        ClearAllSubscriptions();
        
        Log("Disconnected from SpacetimeDB");
        
        if (ex != null)
        {
            LogError($"Disconnect error: {ex}");
            ShowError($"Disconnected: {ex.Message}");
            
            // Fire disconnected event
            OnDisconnected?.Invoke(ex);
            
            // Attempt reconnection
            if (reconnectAttempts < maxReconnectAttempts)
            {
                reconnectAttempts++;
                reconnectCoroutine = StartCoroutine(RetryConnection());
            }
        }
        else
        {
            // Clean disconnect
            OnDisconnected?.Invoke(null);
        }
        
        // Clear session
        if (GameData.Instance != null)
        {
            GameData.Instance.ClearSession();
        }
        
        // Return to login if not already there
        HandleDisconnectSceneTransition();
    }
    
    private IEnumerator RetryConnection()
    {
        ShowError($"Reconnecting in {reconnectDelay} seconds... (Attempt {reconnectAttempts}/{maxReconnectAttempts})");
        
        yield return new WaitForSeconds(reconnectDelay);
        
        ConnectToSpacetime();
    }
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Subscription Management
    // ─────────────────────────────────────────────────────────────────────────────
    
    private void SubscribeToGameData()
    {
        if (Conn == null || !Conn.IsActive)
        {
            LogError("Cannot subscribe - no active connection");
            return;
        }
        
        // Subscribe to all tables initially
        // We'll optimize later when we know the player's world
        Conn.SubscriptionBuilder()
            .OnApplied(HandleSubscriptionApplied)
            .OnError(HandleSubscriptionError)
            .SubscribeToAllTables();
    }
    
    private void HandleSubscriptionApplied(SubscriptionEventContext ctx)
    {
        Log("Subscription applied - game data synced");
        
        // Setup event handlers
        SetupSpacetimeDBEventHandlers();
        
        // Check for existing player
        if (LocalIdentity.HasValue)
        {
            var existingPlayer = ctx.Db.Player.Identity.Find(LocalIdentity.Value);
            if (existingPlayer != null)
            {
                OnExistingPlayerFound(existingPlayer);
                return;
            }
        }
        
        // Show login UI if in login scene
        if (SceneManager.GetActiveScene().name == "LoginScene")
        {
            ShowLoginUI();
        }
    }
    
    private void HandleSubscriptionError(ErrorContext ctx, Exception error)
    {
        LogError($"Subscription error: {error}");
        ShowError("Failed to sync game data");
    }
    
    private void SubscribeToWorldData(WorldCoords worldCoords)
    {
        // Unsubscribe from previous world data
        if (activeSubscriptions.TryGetValue("world_data", out var oldSub))
        {
            oldSub.UnsubscribeThen(ctx => LogDebug("Unsubscribed from previous world"));
        }
        
        var queries = new List<string>
        {
            $"SELECT * FROM world WHERE world_coords = ({worldCoords.X}, {worldCoords.Y}, {worldCoords.Z})",
            $"SELECT * FROM energy_puddle WHERE world_coords = ({worldCoords.X}, {worldCoords.Y}, {worldCoords.Z})",
            $"SELECT * FROM energy_orb WHERE world_coords = ({worldCoords.X}, {worldCoords.Y}, {worldCoords.Z})",
            $"SELECT * FROM miner_device WHERE world_coords = ({worldCoords.X}, {worldCoords.Y}, {worldCoords.Z})",
            $"SELECT * FROM storage_device WHERE world_coords = ({worldCoords.X}, {worldCoords.Y}, {worldCoords.Z})",
            $"SELECT * FROM distribution_sphere WHERE world_coords = ({worldCoords.X}, {worldCoords.Y}, {worldCoords.Z})",
            $"SELECT * FROM tunnel WHERE from_world = ({worldCoords.X}, {worldCoords.Y}, {worldCoords.Z}) OR to_world = ({worldCoords.X}, {worldCoords.Y}, {worldCoords.Z})"
        };
        
        var sub = Conn.SubscriptionBuilder()
            .OnApplied(ctx => LogDebug($"Subscribed to world ({worldCoords.X}, {worldCoords.Y}, {worldCoords.Z})"))
            .OnError((ctx, error) => LogError($"World subscription error: {error}"))
            .Subscribe(queries.ToArray());
            
        activeSubscriptions["world_data"] = sub;
    }
    
    private void SubscribeToPlayerData(SpacetimeDB.Identity playerIdentity)
    {
        if (activeSubscriptions.ContainsKey("player_data"))
            return; // Already subscribed
            
        var queries = new List<string>
        {
            $"SELECT * FROM player WHERE identity = 0x{playerIdentity}",
            $"SELECT * FROM energy_storage WHERE owner_type = 'player' AND owner_id IN (SELECT player_id FROM player WHERE identity = 0x{playerIdentity})",
            $"SELECT * FROM miner_device WHERE owner_identity = 0x{playerIdentity}",
            $"SELECT * FROM storage_device WHERE owner_identity = 0x{playerIdentity}"
        };
        
        var sub = Conn.SubscriptionBuilder()
            .OnApplied(ctx => LogDebug("Subscribed to player data"))
            .OnError((ctx, error) => LogError($"Player subscription error: {error}"))
            .Subscribe(queries.ToArray());
            
        activeSubscriptions["player_data"] = sub;
    }
    
    private void ClearAllSubscriptions()
    {
        foreach (var sub in activeSubscriptions.Values)
        {
            if (sub.IsActive)
            {
                sub.Unsubscribe();
            }
        }
        activeSubscriptions.Clear();
        
        if (mainSubscription?.IsActive == true)
        {
            mainSubscription.Unsubscribe();
            mainSubscription = null;
        }
    }
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Frame Tick Management
    // ─────────────────────────────────────────────────────────────────────────────
    
    private void SetupFrameTicking()
    {
        if (Conn == null || !Conn.IsActive)
        {
            LogWarning("Cannot initialize frame ticking - no active connection");
            return;
        }
        
        // Initialize with configured mode
        FrameTickManager.Instance.Initialize(Conn);
        FrameTickManager.Instance.SetTickMode(defaultTickMode);
        
        // Subscribe to performance monitoring
        FrameTickManager.Instance.OnTickCompleted += HandleFrameTickCompleted;
        
        Log($"Frame tick initialized with mode: {defaultTickMode}");
    }
    
    private void HandleFrameTickCompleted(float tickTimeMs)
    {
        // Monitor for performance issues
        if (tickTimeMs > slowTickWarningThreshold)
        {
            slowTickCount++;
            
            if (tickTimeMs > criticalTickThreshold)
            {
                LogWarning($"Critical frame tick: {tickTimeMs:F2}ms");
            }
            else if (verboseLogging)
            {
                LogDebug($"Slow frame tick: {tickTimeMs:F2}ms");
            }
        }
    }
    
    private void CheckPerformanceAndAdjust()
    {
        if (FrameTickManager.Instance == null || !IsConnected())
            return;
            
        var stats = FrameTickManager.Instance.GetPerformanceStats();
        
        // Auto-adjust tick mode based on performance
        if (stats.AverageTickTimeMs > criticalTickThreshold && 
            stats.CurrentMode == FrameTickManager.TickMode.EveryFrame)
        {
            Log("Switching to Adaptive tick mode due to poor performance");
            FrameTickManager.Instance.SetTickMode(FrameTickManager.TickMode.Adaptive);
        }
        else if (stats.AverageTickTimeMs < slowTickWarningThreshold * 0.5f && 
                 stats.CurrentMode == FrameTickManager.TickMode.Adaptive)
        {
            Log("Performance improved, switching back to EveryFrame mode");
            FrameTickManager.Instance.SetTickMode(FrameTickManager.TickMode.EveryFrame);
        }
        
        // Reset counter
        slowTickCount = 0;
    }
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Event Handlers
    // ─────────────────────────────────────────────────────────────────────────────
    
    private void SetupSpacetimeDBEventHandlers()
    {
        if (Conn?.Db == null) return;
        
        // Player events
        Conn.Db.Player.OnInsert += OnPlayerJoined;
        Conn.Db.Player.OnUpdate += OnPlayerUpdated;
        Conn.Db.Player.OnDelete += OnPlayerLeft;
        
        // Reducer callbacks - only add those that exist in your autogenerated code
        Conn.Reducers.OnEnterGame += OnEnterGameResult;
        Conn.Reducers.OnUpdatePlayerPosition += OnUpdatePositionResult;
        
        LogDebug("Event handlers registered");
    }
    
    private void OnPlayerJoined(EventContext ctx, Player player)
    {
        if (player.Identity == LocalIdentity)
        {
            Log($"Local player joined: {player.Name}");
            OnExistingPlayerFound(player);
        }
        else
        {
            LogDebug($"Other player joined: {player.Name}");
        }
    }
    
    private void OnPlayerUpdated(EventContext ctx, Player oldPlayer, Player newPlayer)
    {
        if (newPlayer.Identity == LocalIdentity)
        {
            // Check for world change
            var oldWorld = oldPlayer.CurrentWorld;
            
            if (GameData.Instance != null)
            {
                GameData.Instance.SyncWithPlayerData(newPlayer);
            }
            
            // Handle world transitions
            if (!oldWorld.Equals(newPlayer.CurrentWorld))
            {
                Log($"Player moved from world ({oldWorld.X},{oldWorld.Y},{oldWorld.Z}) to ({newPlayer.CurrentWorld.X},{newPlayer.CurrentWorld.Y},{newPlayer.CurrentWorld.Z})");
                OnWorldChanged?.Invoke(newPlayer.CurrentWorld);
                
                // Update subscriptions for new world
                SubscribeToWorldData(newPlayer.CurrentWorld);
            }
        }
    }
    
    private void OnPlayerLeft(EventContext ctx, Player player)
    {
        Log($"Player left: {player.Name}");
    }
    
    // Reducer result handlers
    private void OnEnterGameResult(ReducerEventContext ctx, string playerName)
    {
        var status = ctx.Event.Status;
        
        switch (status)
        {
            case Status.Committed _:
                Log($"Player entered game successfully: {playerName}");
                break;
                
            case Status.Failed(var reason):
                LogError($"Failed to enter game: {reason}");
                ShowError($"Failed to enter game: {reason}");
                break;
                
            case Status.OutOfEnergy _:
                LogError("Out of energy - cannot enter game");
                ShowError("The game is out of energy! Please try again later.");
                break;
        }
    }
    
    private void OnUpdatePositionResult(ReducerEventContext ctx, float posX, float posY, float posZ, float rotX, float rotY, float rotZ, float rotW)
    {
        // Only log failures
        if (ctx.Event.Status is Status.Failed(var error))
        {
            LogWarning($"Position update failed: {error}");
        }
    }
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Player Management
    // ─────────────────────────────────────────────────────────────────────────────
    
    private void OnExistingPlayerFound(Player player)
    {
        Log($"Found existing player: {player.Name}");
        
        // Save username
        if (usernameInput != null)
        {
            PlayerPrefs.SetString("LastUsername", player.Name);
        }
        
        // Update GameData
        if (GameData.Instance != null)
        {
            GameData.Instance.SyncWithPlayerData(player);
        }
        
        // Fire player ready event
        OnLocalPlayerReady?.Invoke(player);
        
        // Subscribe to world-specific data
        SubscribeToWorldData(player.CurrentWorld);
        
        // Transition to appropriate world
        if (autoTransitionToCenterWorld && SceneTransitionManager.Instance != null)
        {
            if (SceneTransitionManager.IsCenter(player.CurrentWorld))
            {
                SceneTransitionManager.Instance.TransitionToCenterWorld();
            }
            else
            {
                SceneTransitionManager.Instance.TransitionToWorld(player.CurrentWorld);
            }
        }
    }
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Authentication
    // ─────────────────────────────────────────────────────────────────────────────
    
    // Simple login method for basic UI (without LoginUI component)
    public void OnLoginButtonClicked()
    {
        if (usernameInput == null) return;
        
        string username = usernameInput.text.Trim();
        if (string.IsNullOrEmpty(username))
        {
            ShowError("Please enter a username");
            return;
        }
        
        if (!IsConnected())
        {
            ShowError("Not connected to server");
            return;
        }
        
        try
        {
            // Use EnterGame reducer instead of CreatePlayer
            Conn.Reducers.EnterGame(username);
            
            // Save username
            PlayerPrefs.SetString("LastUsername", username);
            
            StartCoroutine(WaitForPlayerCreation(username));
        }
        catch (Exception ex)
        {
            ShowError($"Failed to enter game: {ex.Message}");
        }
    }
    
    private IEnumerator WaitForPlayerCreation(string username)
    {
        float timeout = 5f;
        float elapsed = 0f;
        
        ShowError("Entering game...");
        
        while (elapsed < timeout)
        {
            if (LocalIdentity.HasValue)
            {
                var player = Conn.Db.Player.Identity.Find(LocalIdentity.Value);
                if (player != null)
                {
                    ShowError(""); // Clear message
                    Log($"Player created: {player.Name}");
                    yield break;
                }
            }
            
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        
        ShowError("Failed to enter game - timeout");
    }
    
    // ─────────────────────────────────────────────────────────────────────────────
    // UI Management
    // ─────────────────────────────────────────────────────────────────────────────
    
    private void ShowLoginUI()
    {
        // If you have a LoginUI component, uncomment this:
        // if (loginUI != null)
        // {
        //     loginUI.gameObject.SetActive(true);
        //     loginUI.ShowAuthPanel();
        // }
        // else 
        if (loginPanel != null)
        {
            loginPanel.SetActive(true);
        }
    }
    
    private void ShowError(string message)
    {
        if (errorText != null)
        {
            errorText.text = message;
            errorText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
        
        // If you have LoginUI, uncomment:
        // loginUI?.ShowError(message);
    }
    
    private void UpdateConnectionButton(bool interactable)
    {
        if (connectButton != null)
        {
            connectButton.interactable = interactable;
        }
    }
    
    private void UpdateConnectionStatusUI()
    {
        if (connectionStatusText == null) return;
        
        if (IsConnected())
        {
            connectionStatusText.text = "Connected";
            connectionStatusText.color = Color.green;
        }
        else if (isConnecting)
        {
            connectionStatusText.text = "Connecting...";
            connectionStatusText.color = Color.yellow;
        }
        else
        {
            connectionStatusText.text = "Disconnected";
            connectionStatusText.color = Color.red;
        }
    }
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Scene Management
    // ─────────────────────────────────────────────────────────────────────────────
    
    private void HandleDisconnectSceneTransition()
    {
        if (SceneManager.GetActiveScene().name != "LoginScene")
        {
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.TransitionToLogin();
            }
            else
            {
                SceneManager.LoadScene("LoginScene");
            }
        }
    }
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Application Lifecycle
    // ─────────────────────────────────────────────────────────────────────────────
    
    private void HandleApplicationPause(bool pauseStatus)
    {
        if (FrameTickManager.Instance != null)
        {
            if (pauseStatus)
            {
                Log("Application paused - pausing frame ticks");
                FrameTickManager.Instance.Pause();
            }
            else
            {
                Log("Application resumed - resuming frame ticks");
                FrameTickManager.Instance.Resume();
            }
        }
    }
    
    private void HandleApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && FrameTickManager.Instance != null)
        {
            var stats = FrameTickManager.Instance.GetPerformanceStats();
            if (stats.CurrentMode == FrameTickManager.TickMode.EveryFrame)
            {
                LogDebug("Lost focus - switching to fixed interval ticking");
                FrameTickManager.Instance.SetTickMode(FrameTickManager.TickMode.FixedInterval);
            }
        }
        else if (hasFocus && FrameTickManager.Instance != null)
        {
            LogDebug("Gained focus - restoring tick mode");
            FrameTickManager.Instance.SetTickMode(defaultTickMode);
        }
    }
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Cleanup
    // ─────────────────────────────────────────────────────────────────────────────
    
    private void Cleanup()
    {
        LogDebug("GameManager cleanup started");
        
        // Stop any running coroutines
        if (reconnectCoroutine != null)
        {
            StopCoroutine(reconnectCoroutine);
        }
        
        // Cleanup event handlers
        if (Conn?.Db != null)
        {
            Conn.Db.Player.OnInsert -= OnPlayerJoined;
            Conn.Db.Player.OnUpdate -= OnPlayerUpdated;
            Conn.Db.Player.OnDelete -= OnPlayerLeft;
        }
        
        if (Conn?.Reducers != null)
        {
            Conn.Reducers.OnEnterGame -= OnEnterGameResult;
            Conn.Reducers.OnUpdatePlayerPosition -= OnUpdatePositionResult;
        }
        
        // Cleanup frame tick monitoring
        if (FrameTickManager.Instance != null)
        {
            FrameTickManager.Instance.OnTickCompleted -= HandleFrameTickCompleted;
        }
        
        // Clear subscriptions
        ClearAllSubscriptions();
        
        // Disconnect if still connected
        if (Conn?.IsActive == true)
        {
            try
            {
                Conn.Disconnect();
            }
            catch (Exception ex)
            {
                LogError($"Error during disconnect: {ex}");
            }
        }
        
        LogDebug("GameManager cleanup completed");
    }
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Static Helper Methods
    // ─────────────────────────────────────────────────────────────────────────────
    
    public static bool IsConnected()
    {
        return Conn != null && Conn.IsActive && LocalIdentity.HasValue;
    }
    
    public static Player GetCurrentPlayer()
    {
        if (!IsConnected()) return null;
        return Conn.Db.Player.Identity.Find(LocalIdentity.Value);
    }
    
    public static void ActivateTunnel(ulong tunnelId, float energyAmount)
    {
        if (!IsConnected())
        {
            Instance.LogError("Cannot activate tunnel - not connected");
            return;
        }
        
        try
        {
            Conn.Reducers.ActivateTunnel(tunnelId, energyAmount);
            Instance.Log($"Activated tunnel {tunnelId} with {energyAmount} energy");
        }
        catch (Exception ex)
        {
            Instance.LogError($"Failed to activate tunnel: {ex.Message}");
        }
    }
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Logging Utilities
    // ─────────────────────────────────────────────────────────────────────────────
    
    private void Log(string message)
    {
        Debug.Log($"[GameManager] {message}");
    }
    
    private void LogDebug(string message)
    {
        if (verboseLogging)
        {
            Debug.Log($"[GameManager] {message}");
        }
    }
    
    private void LogWarning(string message)
    {
        Debug.LogWarning($"[GameManager] {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[GameManager] {message}");
    }
}