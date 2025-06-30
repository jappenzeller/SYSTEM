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
    [SerializeField] private string moduleAddress = "localhost:3000";
    [SerializeField] private string moduleName = "system";
    [SerializeField] private bool useSSL = false;
    
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
    
    // Pending operations
    private string pendingRegisterUsername;
    private string pendingLoginUsername;
    
    // Events
    public static event Action OnConnected;
    public static event Action OnDisconnected;
    public static event Action<Player> OnLocalPlayerReady;
    public static event Action<WorldCoords> OnWorldChanged;
    public static event Action<string> OnConnectionError;
    public static event Action OnLoginUIReady;  // New event for UI display
    
    // Properties
    public static DbConnection Conn => instance?.conn;
    public static Identity? LocalIdentity => instance?.conn?.Identity;
    
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
            
            // Don't auto-connect - wait for user interaction
            // This gives time for any SDK initialization
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
        
        // Actually connect after creating the connection
        if (!conn.IsActive)
        {
            Debug.Log("Initializing SpacetimeDB connection...");
            StartCoroutine(ConnectToServer());
        }
    }
    
    public void Connect(string username = null)
    {
        if (isConnecting)
        {
            Debug.LogWarning("Already connecting...");
            return;
        }
        
        if (conn != null && conn.IsActive)
        {
            Debug.LogWarning("Already connected");
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
        
        // Try to establish connection with auth token if available
        string token = AuthToken.LoadToken();
        
        // The SpacetimeDB Unity SDK might need configuration before subscribing
        // Since we can't find a Connect method, let's try subscribing with a delay
        yield return new WaitForSeconds(0.5f);
        
        // Subscribe to all tables - this should establish the connection
        try
        {
            conn.SubscriptionBuilder()
                .OnApplied((ctx) => 
                {
                    HandleConnected();
                    HandleSubscriptionApplied();
                })
                .OnError(HandleSubscriptionError)
                .Subscribe(new string[] { "SELECT * FROM *" });
            
            // After subscription, call the connect reducer
            if (conn.IsActive)
            {
                conn.Reducers.Connect();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to subscribe: {e.Message}");
            isConnecting = false;
            ShowConnectingUI(false);
            ShowError($"Connection failed: {e.Message}");
            yield break;
        }
        
        // Give it some time to connect
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
            loginUI.OnLoginRequested += HandleLoginRequest;
            loginUI.OnRegisterRequested += HandleRegisterRequest;
            loginUI.OnCreateCharacterRequested += HandleCreateCharacterRequest;
            
            // Fire event to signal UI can be shown
            StartCoroutine(SignalLoginUIReady());
        }
    }
    
    private IEnumerator SignalLoginUIReady()
    {
        // Wait one frame to ensure all components are initialized
        yield return null;
        
        Debug.Log("Signaling login UI ready");
        OnLoginUIReady?.Invoke();
    }
    
    private void HandleLoginRequest(string username, string password)
    {
        Debug.Log($"Login requested for user: {username}");
        
        if (conn != null && conn.IsActive)
        {
            // Store pending username
            pendingLoginUsername = username;
            
            // Call the login reducer
            conn.Reducers.Login(username, password);
            
            // Start monitoring for login result
            StartCoroutine(WaitForLoginResult(username));
        }
        else
        {
            // Should not happen with auto-connect, but just in case
            loginUI?.ShowError("Not connected to server - please wait and try again");
        }
    }
    
    private void HandleRegisterRequest(string username, string password)
    {
        Debug.Log($"Registration requested for user: {username}");
        
        // First ensure we're connected
        if (conn == null || !conn.IsActive)
        {
            // Store registration info for after connection
            pendingRegisterUsername = username;
            
            // Connect first, then register will happen after connection
            gameData.SetUsername(username);
            Connect(username);
            
            // Store password temporarily (you may want a more secure approach)
            StartCoroutine(RetryRegisterAfterConnection(username, password));
        }
        else
        {
            // Already connected, proceed with registration
            pendingRegisterUsername = username;
            
            // Call the register_account reducer
            conn.Reducers.RegisterAccount(username, password);
            
            // Start monitoring for registration result
            StartCoroutine(WaitForRegistrationResult(username));
        }
    }
    
    private IEnumerator RetryRegisterAfterConnection(string username, string password, float timeout = 10f)
    {
        float elapsed = 0f;
        
        // Wait for connection to be established
        while (elapsed < timeout && (conn == null || !conn.IsActive || isConnecting))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        if (conn != null && conn.IsActive)
        {
            // Now we're connected, try registration
            pendingRegisterUsername = username;
            conn.Reducers.RegisterAccount(username, password);
            StartCoroutine(WaitForRegistrationResult(username));
        }
        else
        {
            // Connection failed
            loginUI?.HandleRegisterError("Failed to connect to server");
        }
    }
    
    private void HandleCreateCharacterRequest(string characterName)
    {
        Debug.Log($"Create character requested: {characterName}");
        
        if (conn != null && conn.IsActive)
        {
            // Call the create_player reducer
            conn.Reducers.CreatePlayer(characterName);
            
            // Start monitoring for player creation
            StartCoroutine(WaitForPlayerCreation());
        }
        else
        {
            ShowError("Not connected to server");
            loginUI?.HandleCreateCharacterError("Not connected to server");
        }
    }
    
    #endregion
    
    #region Table Monitoring Coroutines
    
    private IEnumerator WaitForRegistrationResult(string username, float timeout = 5f)
    {
        float elapsed = 0f;
        bool accountFound = false;
        
        // Set up temporary handler using lambda
        RemoteTableHandle<EventContext, Account>.RowEventHandler onAccountInsert = null;
        onAccountInsert = (ctx, account) =>
        {
            if (account.Username == username)
            {
                accountFound = true;
                Debug.Log($"Account created successfully for {username}");
            }
        };
        
        // Subscribe to account insert events
        conn.Db.Account.OnInsert += onAccountInsert;
        
        // Wait for account creation or timeout
        while (elapsed < timeout && !accountFound)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Unsubscribe
        conn.Db.Account.OnInsert -= onAccountInsert;
        
        // Handle result
        if (accountFound)
        {
            loginUI?.HandleRegisterSuccess();
            pendingRegisterUsername = null;
        }
        else
        {
            Debug.LogWarning($"Registration timeout for {username}");
            loginUI?.HandleRegisterError("Registration failed - please try again");
        }
    }
    
    private IEnumerator WaitForLoginResult(string username, float timeout = 5f)
    {
        float elapsed = 0f;
        bool loginSuccess = false;
        
        // Check if we already have a player (login succeeded)
        while (elapsed < timeout && !loginSuccess)
        {
            // Check if our identity now has a player with matching account
            foreach (var player in conn.Db.Player.Iter())
            {
                if (player.Identity == conn.Identity && player.Name == username)
                {
                    loginSuccess = true;
                    Debug.Log($"Login successful for {username}");
                    break;
                }
            }
            
            if (!loginSuccess)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        
        // Handle result
        if (loginSuccess)
        {
            loginUI?.HandleLoginSuccess();
            pendingLoginUsername = null;
            
            // Check for existing player
            CheckExistingPlayer();
        }
        else
        {
            Debug.LogWarning($"Login timeout for {username}");
            loginUI?.HandleLoginError("Login failed - invalid credentials");
        }
    }
    
    private IEnumerator WaitForPlayerCreation(float timeout = 5f)
    {
        float elapsed = 0f;
        bool playerCreated = false;
        Player newPlayer = null;
        
        // Set up temporary handler using proper delegate type
        RemoteTableHandle<EventContext, Player>.RowEventHandler onPlayerInsert = null;
        onPlayerInsert = (ctx, player) =>
        {
            if (player.Identity == conn.Identity)
            {
                playerCreated = true;
                newPlayer = player;
                Debug.Log($"Player created successfully: {player.Name}");
            }
        };
        
        // Subscribe to player insert events
        conn.Db.Player.OnInsert += onPlayerInsert;
        
        // Wait for player creation or timeout
        while (elapsed < timeout && !playerCreated)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Unsubscribe
        conn.Db.Player.OnInsert -= onPlayerInsert;
        
        // Handle result
        if (playerCreated && newPlayer != null)
        {
            loginUI?.HandleCreateCharacterSuccess();
            HandlePlayerReady(newPlayer);
        }
        else
        {
            Debug.LogWarning("Player creation timeout");
            loginUI?.HandleCreateCharacterError("Failed to create character");
        }
    }
    
    #endregion
    
    #region Connection Event Handlers
    
    private void HandleConnected()
    {
        Debug.Log("Connected to SpacetimeDB!");
        isConnecting = false;
        ShowConnectingUI(false);
        OnConnected?.Invoke();
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
        Debug.Log("Subscription successful! Checking for existing player...");
        
        // Hide connecting UI since we're now connected
        ShowConnectingUI(false);
        
        // Store identity if we don't have it
        if (conn.Identity.HasValue && !gameData.PlayerIdentity.HasValue)
        {
            gameData.SetPlayerIdentity(conn.Identity.Value);
        }
        
        // Only check for existing player if we have a username
        // (Don't auto-create player on initial connection)
        if (!string.IsNullOrEmpty(gameData.Username))
        {
            CheckExistingPlayer();
        }
    }
    
    private void HandleSubscriptionError(ErrorContext ctx, Exception error)
    {
        Debug.LogError($"Subscription error: {error.Message}");
        isConnecting = false;
        ShowConnectingUI(false);
        ShowError($"Failed to connect: {error.Message}");
        OnConnectionError?.Invoke(error.Message);
    }
    
    #endregion
    
    #region Player Management
    
    private void CheckExistingPlayer()
    {
        if (!conn.Identity.HasValue) return;
        
        Player existingPlayer = null;
        foreach (var player in conn.Db.Player.Iter())
        {
            if (player.Identity == conn.Identity.Value)
            {
                existingPlayer = player;
                break;
            }
        }
        
        if (existingPlayer != null)
        {
            Debug.Log($"Found existing player: {existingPlayer.Name}");
            HandlePlayerReady(existingPlayer);
        }
        else
        {
            // Check for logged out player
            var loggedOutPlayer = conn.Db.LoggedOutPlayer.Identity.Find(conn.Identity.Value);
            if (loggedOutPlayer != null)
            {
                Debug.Log($"Found logged out player: {loggedOutPlayer.Name}, recreating...");
                conn.Reducers.CreatePlayer(loggedOutPlayer.Name);
            }
            else
            {
                Debug.Log("No existing player found");
                if (loginUI != null)
                {
                    loginUI.ShowCharacterCreation(gameData.Username);
                }
            }
        }
    }
    
    private void HandlePlayerReady(Player player)
    {
        Debug.Log($"Player ready: {player.Name} at world ({player.CurrentWorld.X},{player.CurrentWorld.Y},{player.CurrentWorld.Z})");
        
        // Update game data
        gameData.SetCurrentWorldCoords(player.CurrentWorld);
        gameData.SetPlayerIdentity(player.Identity);
        
        // Fire event
        OnLocalPlayerReady?.Invoke(player);
        
        // Load game scene if we're still in login
        if (SceneManager.GetActiveScene().name == loginSceneName)
        {
            LoadGameScene();
        }
    }
    
    #endregion
    
    #region Scene Management
    
    public void LoadGameScene()
    {
        Debug.Log("Loading game scene...");
        StartCoroutine(LoadSceneAsync(gameSceneName));
    }
    
    public void LoadLoginScene()
    {
        Debug.Log("Loading login scene...");
        StartCoroutine(LoadSceneAsync(loginSceneName));
    }
    
    private IEnumerator LoadSceneAsync(string sceneName)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
        
        Debug.Log($"Scene {sceneName} loaded");
    }
    
    #endregion
    
    #region UI Management
    
    private void ShowConnectingUI(bool show)
    {
        if (connectingPanel != null)
        {
            connectingPanel.SetActive(show);
        }
    }
    
    private void ShowError(string message)
    {
        if (errorPanel != null && errorText != null)
        {
            errorText.text = message;
            errorPanel.SetActive(true);
            
            // Auto-hide after 5 seconds
            StartCoroutine(HideErrorAfterDelay(5f));
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
}