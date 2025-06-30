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
        
        // SpacetimeDB doesn't have connection events - connection is handled differently
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
        
        // The connection URL needs to be set on the module
        // For now, we'll just subscribe and the connection will be established
        
        // Subscribe to all tables - this establishes the connection
        conn.SubscriptionBuilder()
            .OnApplied((ctx) => 
            {
                HandleConnected();
                HandleSubscriptionApplied();
            })
            .OnError(HandleSubscriptionError)
            .Subscribe(new string[] { "SELECT * FROM *" });
        
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
            // Call the login reducer
            conn.Reducers.Login(username, password);
        }
        else
        {
            // Connect first
            gameData.SetUsername(username);
            Connect(username);
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
            ShowError("Not connected to server");
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
            ShowError("Not connected to server");
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
        
        // Store identity if we don't have it
        if (conn.Identity.HasValue && !gameData.PlayerIdentity.HasValue)
        {
            gameData.SetPlayerIdentity(conn.Identity.Value);
        }
        
        // Check if player exists
        CheckExistingPlayer();
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