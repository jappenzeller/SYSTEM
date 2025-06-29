// GameManager.cs - Cleaned version without energy system
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
                instance = FindFirstObjectByType<GameManager>();
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
    [SerializeField] private string dbName = "system";
    [SerializeField] private float reconnectDelay = 5f;
    [SerializeField] private int maxReconnectAttempts = 3;
    
    [Header("Frame Tick Settings")]
    [SerializeField] private FrameTickManager.TickMode defaultTickMode = FrameTickManager.TickMode.EveryFrame;
    [SerializeField] private bool autoInitializeFrameTick = true;
    [SerializeField] private float slowTickWarningThreshold = 16.0f; // ms
    [SerializeField] private float criticalTickThreshold = 32.0f; // ms
    
    [Header("UI References")]
    [SerializeField] private LoginUIController loginUIController;
    [SerializeField] private bool autoTransitionToCenterWorld = true;

    // Store username when login is requested
    private string currentUsername = "";

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
        // Connect to LoginUIController if assigned
        if (loginUIController != null)
        {
            loginUIController.OnLoginRequested.AddListener(OnLoginRequested);
            loginUIController.OnRegisterRequested.AddListener(OnRegisterRequested);
        }
        
        if (autoConnectInEditor && Application.isEditor)
        {
            Log("Auto-connecting in editor mode...");
            StartCoroutine(AutoConnectRoutine());
        }
    }

    private IEnumerator AutoConnectRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        Connect();
    }

    void Update()
    {
        HandlePerformanceMonitoring();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        HandleApplicationPause(pauseStatus);
    }

    void OnApplicationFocus(bool hasFocus)
    {
        HandleApplicationFocus(hasFocus);
    }

    void OnDestroy()
    {
        Cleanup();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Connection Management
    // ─────────────────────────────────────────────────────────────────────────────
    
    public void Connect()
    {
        if (isConnecting || IsConnected())
        {
            LogWarning("Already connected or connecting");
            return;
        }
        
        isConnecting = true;
        UpdateConnectionButton(false);
        
        string connectionUri = $"http://{serverIP}:{serverPort}/{dbName}";
        
        Log($"Connecting to SpacetimeDB at {connectionUri}...");
        
        try
        {
            Conn = DbConnection.Builder()
                .WithUri(connectionUri)
                .OnConnect(OnConnectionEstablished)
                .OnConnectError(OnConnectionError)
                .OnDisconnect(OnConnectionLost)
                .Build();
        }
        catch (Exception ex)
        {
            LogError($"Failed to create connection: {ex.Message}");
            isConnecting = false;
            UpdateConnectionButton(true);
        }
    }
    
    private void OnConnectionEstablished(DbConnection conn, SpacetimeDB.Identity identity, string authToken)
    {
        Log($"Connected! Identity: {identity}");
        
        isConnecting = false;
        LocalIdentity = identity;
        reconnectAttempts = 0;
        UpdateConnectionButton(true);
        
        // Initialize frame ticking
        if (autoInitializeFrameTick && FrameTickManager.Instance != null)
        {
            FrameTickManager.Instance.Initialize(conn);
            FrameTickManager.Instance.OnTickCompleted += HandleFrameTickCompleted;
        }
        
        SetupEventHandlers();
        SetupInitialSubscriptions();
        
        OnConnected?.Invoke(conn, identity);
        
        if (GameData.Instance != null)
        {
            GameData.Instance.IsLoggedIn = true;
            GameData.Instance.Initialize();
        }
        
        if (loginUIController != null)
        {
            loginUIController.OnConnectionSuccess();
        }
    }
    
    private void OnConnectionError(string error)
    {
        LogError($"Connection error: {error}");
        isConnecting = false;
        UpdateConnectionButton(true);
        ShowError($"Connection failed: {error}");
        
        if (reconnectAttempts < maxReconnectAttempts)
        {
            reconnectCoroutine = StartCoroutine(ReconnectRoutine());
        }
    }
    
    private void OnConnectionLost(DbConnection conn, SpacetimeDB.CloseStatus closeStatus)
    {
        Log($"Disconnected from server - Status: {closeStatus?.Reason ?? "Unknown"}");
        
        LocalIdentity = null;
        isConnecting = false;
        UpdateConnectionButton(true);
        
        OnDisconnected?.Invoke(new Exception(closeStatus?.Reason ?? "Connection lost"));
        
        if (FrameTickManager.Instance != null)
        {
            FrameTickManager.Instance.OnTickCompleted -= HandleFrameTickCompleted;
        }
        
        HandleDisconnectSceneTransition();
        
        if (reconnectAttempts < maxReconnectAttempts)
        {
            reconnectCoroutine = StartCoroutine(ReconnectRoutine());
        }
    }
    
    private IEnumerator ReconnectRoutine()
    {
        reconnectAttempts++;
        Log($"Reconnecting in {reconnectDelay} seconds... (Attempt {reconnectAttempts}/{maxReconnectAttempts})");
        
        yield return new WaitForSeconds(reconnectDelay);
        
        Connect();
    }
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Subscriptions
    // ─────────────────────────────────────────────────────────────────────────────
    
    private void SetupInitialSubscriptions()
    {
        string[] mainQueries = new string[]
        {
            "SELECT * FROM player WHERE identity = 0x" + LocalIdentity,
            "SELECT * FROM world"
        };
        
        mainSubscription = Conn.SubscriptionBuilder()
            .OnApplied((ctx) => 
            {
                Log("Main subscription ready");
                CheckForExistingPlayer();
            })
            .OnError((ctx, error) => 
            {
                LogError($"Main subscription error: {error}");
            })
            .Subscribe(mainQueries);
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
            $"SELECT * FROM world_circuit WHERE world_coords = ({worldCoords.X}, {worldCoords.Y}, {worldCoords.Z})"
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
            $"SELECT * FROM player WHERE identity = 0x{playerIdentity}"
        };
        
        var sub = Conn.SubscriptionBuilder()
            .OnApplied((ctx) => LogDebug("Player data subscription ready"))
            .OnError((ctx, error) => LogError($"Player subscription error: {error}"))
            .Subscribe(queries.ToArray());
            
        activeSubscriptions["player_data"] = sub;
    }
    
    private void ClearAllSubscriptions()
    {
        mainSubscription?.Unsubscribe();
        
        foreach (var sub in activeSubscriptions.Values)
        {
            sub?.Unsubscribe();
        }
        activeSubscriptions.Clear();
    }
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Event Handlers
    // ─────────────────────────────────────────────────────────────────────────────
    
    private void SetupEventHandlers()
    {
        // Player events
        Conn.Db.Player.OnInsert += OnPlayerJoined;
        Conn.Db.Player.OnUpdate += OnPlayerUpdated;
        Conn.Db.Player.OnDelete += OnPlayerLeft;
        
        // Reducer result handlers
        Conn.Reducers.OnEnterGame += OnEnterGameResult;
        Conn.Reducers.OnUpdatePlayerPosition += OnUpdatePositionResult;
    }
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Performance Monitoring
    // ─────────────────────────────────────────────────────────────────────────────
    
    private void HandlePerformanceMonitoring()
    {
        if (Time.time - lastPerformanceCheck > performanceCheckInterval)
        {
            if (slowTickCount > 0)
            {
                LogWarning($"Performance warning: {slowTickCount} slow ticks in the last {performanceCheckInterval} seconds");
                
                if (FrameTickManager.Instance != null)
                {
                    var stats = FrameTickManager.Instance.GetPerformanceStats();
                    LogDebug($"Tick stats - Avg: {stats.AverageTickTimeMs:F2}ms, Max: {stats.MaxTickTimeMs:F2}ms, Messages/s: {stats.MessagesPerSecond}");
                }
            }
            
            slowTickCount = 0;
            lastPerformanceCheck = Time.time;
        }
    }
    
    private void HandleFrameTickCompleted(float tickTimeMs)
    {
        if (tickTimeMs > slowTickWarningThreshold)
        {
            slowTickCount++;
            
            if (tickTimeMs > criticalTickThreshold)
            {
                LogError($"Critical performance issue: Frame tick took {tickTimeMs:F2}ms!");
            }
        }
    }
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Authentication Handlers
    // ─────────────────────────────────────────────────────────────────────────────
    
    private void OnLoginRequested(string username, string password)
    {
        if (!IsConnected())
        {
            ShowError("Not connected to server");
            return;
        }
        
        Log($"Login requested for user: {username}");
        currentUsername = username;
        
        Conn.Reducers.Login(username, password);
        Conn.Reducers.OnLogin += OnLoginResult;
    }
    
    private void OnRegisterRequested(string username, string password)
    {
        if (!IsConnected())
        {
            ShowError("Not connected to server");
            return;
        }
        
        Log($"Registration requested for user: {username}");
        
        Conn.Reducers.RegisterAccount(username, password);
        Conn.Reducers.OnRegisterAccount += OnRegisterResult;
    }
    
    private void OnLoginResult(ReducerEventContext ctx, string username, string password)
    {
        Conn.Reducers.OnLogin -= OnLoginResult;
        
        var status = ctx.Event.Status;
        switch (status)
        {
            case Status.Committed _:
                Log($"Login successful for {username}");
                ShowError("");
                
                if (GameData.Instance != null)
                {
                    GameData.Instance.Username = username;
                }
                
                TryEnterGame(username);
                break;
                
            case Status.Failed(var reason):
                LogError($"Login failed: {reason}");
                ShowError($"Login failed: {reason}");
                break;
        }
    }
    
    private void OnRegisterResult(ReducerEventContext ctx, string username, string password)
    {
        Conn.Reducers.OnRegisterAccount -= OnRegisterResult;
        
        var status = ctx.Event.Status;
        switch (status)
        {
            case Status.Committed _:
                Log($"Registration successful for {username}");
                ShowError("Registration successful! Please login.");
                break;
                
            case Status.Failed(var reason):
                LogError($"Registration failed: {reason}");
                ShowError($"Registration failed: {reason}");
                break;
        }
    }
    
    // ─────────────────────────────────────────────────────────────────────────────
    // Player Entry
    // ─────────────────────────────────────────────────────────────────────────────
    
    private void CheckForExistingPlayer()
    {
        if (!LocalIdentity.HasValue) return;
        
        var existingPlayer = Conn.Db.Player.Identity.Find(LocalIdentity.Value);
        if (existingPlayer != null)
        {
            Log($"Found existing player: {existingPlayer.Name}");
            OnExistingPlayerFound(existingPlayer);
        }
        else if (!string.IsNullOrEmpty(currentUsername))
        {
            TryEnterGame(currentUsername);
        }
    }
    
    private void TryEnterGame(string playerName)
    {
        if (!IsConnected())
        {
            LogError("Cannot enter game - not connected");
            return;
        }
        
        Log($"Entering game as: {playerName}");
        
        Conn.Reducers.EnterGame(playerName);
        StartCoroutine(WaitForPlayerCreation(playerName));
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
        Log($"Welcome back, {player.Name}!");
        
        if (GameData.Instance != null)
        {
            GameData.Instance.SyncWithPlayerData(player);
        }
        
        SubscribeToWorldData(player.CurrentWorld);
        SubscribeToPlayerData(player.Identity);
        
        OnLocalPlayerReady?.Invoke(player);
        
        if (autoTransitionToCenterWorld && SceneManager.GetActiveScene().name != "CenterWorld")
        {
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.TransitionToCenterWorld();
            }
            else
            {
                SceneManager.LoadScene("CenterWorld");
            }
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
        if (loginUIController != null)
        {
            loginUIController.ShowAuthPanel();
        }
    }
    
    private void ShowError(string message)
    {
        if (loginUIController != null)
        {
            loginUIController.ShowError(message);
        }
        else
        {
            Debug.LogError($"UI Error: {message}");
        }
    }
    
    private void UpdateConnectionButton(bool interactable)
    {
        if (loginUIController != null)
        {
            loginUIController.SetLoginButtonEnabled(interactable);
        }
    }
    
    private void UpdateConnectionStatusUI()
    {
        string status = "";
        
        if (IsConnected())
        {
            status = "Connected";
        }
        else if (isConnecting)
        {
            status = "Connecting...";
        }
        else
        {
            status = "Disconnected";
        }
        
        LogDebug($"Connection Status: {status}");
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

// Extension to make GameManager use FrameTickManager
public partial class GameManager
{
    private void InitializeFrameTicking()
    {
        // Initialize the frame tick manager when connection is established
        FrameTickManager.Instance.Initialize(Conn);
        
        // Optionally subscribe to performance events
        FrameTickManager.Instance.OnTickCompleted += OnFrameTickCompleted;
    }
    
    private void OnFrameTickCompleted(float tickTimeMs)
    {
        // Log warnings for slow ticks
        if (tickTimeMs > 16.0f) // More than one frame at 60fps
        {
            Debug.LogWarning($"[GameManager] Slow frame tick detected: {tickTimeMs:F2}ms");
        }
    }
}