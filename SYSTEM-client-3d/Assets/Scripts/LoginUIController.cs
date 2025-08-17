using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using SpacetimeDB;
using SpacetimeDB.Types;

/// <summary>
/// Handles the login UI functionality through EventBus events only.
/// Fully event-driven with no direct coupling to GameManager.
/// </summary>
public class LoginUIController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;
    
    // UI Elements
    private VisualElement root;
    private VisualElement authPanel;
    private VisualElement loginForm;
    private VisualElement registerForm;
    private VisualElement loadingOverlay;
    private Label messageLabel;
    
    // Login form elements
    private TextField loginUsernameField;
    private TextField loginPasswordField;
    private TextField loginPinField;
    private Button loginButton;
    private Button showRegisterButton;
    
    // Register form elements
    private TextField registerUsernameField;
    private TextField registerDisplayNameField;
    private TextField registerPinField;
    private TextField registerConfirmPinField;
    private Button registerButton;
    private Button showLoginButton;
    
    // Loading elements
    private Label loadingText;
    private Label errorText;
    
    // State
    private bool isProcessingLogin = false;
    private string pendingUsername = null;
    
    #region Unity Lifecycle
    
    void Awake()
    {
        Debug.Log("[LoginUI] LoginUIController Awake");
        
        // Subscribe to EventBus events
        SubscribeToEvents();
    }
    
    void Start()
    {
        SetupUI();
        
        // Set initial UI state based on current EventBus state
        UpdateUIForState(GameEventBus.Instance.CurrentState);
    }
    
    void OnDestroy()
    {
        UnsubscribeFromEvents();
    }
    
    #endregion
    
    #region Event Subscriptions
    
    private void SubscribeToEvents()
    {
        GameEventBus.Instance.Subscribe<StateChangedEvent>(OnStateChanged);
        
        // Connection events
        GameEventBus.Instance.Subscribe<ConnectionEstablishedEvent>(OnConnectionEstablished);
        GameEventBus.Instance.Subscribe<ConnectionLostEvent>(OnConnectionLost);
        GameEventBus.Instance.Subscribe<ConnectionFailedEvent>(OnConnectionFailed);
        
        // Login/Session events
        GameEventBus.Instance.Subscribe<LoginSuccessfulEvent>(OnLoginSuccessful);
        GameEventBus.Instance.Subscribe<LoginFailedEvent>(OnLoginFailed);
        GameEventBus.Instance.Subscribe<SessionCreatedEvent>(OnSessionCreated);
        GameEventBus.Instance.Subscribe<SessionRestoredEvent>(OnSessionRestored);
        
        // Player events
        GameEventBus.Instance.Subscribe<LocalPlayerCheckStartedEvent>(OnPlayerCheckStarted);
        GameEventBus.Instance.Subscribe<LocalPlayerCreatedEvent>(OnPlayerCreated);
        GameEventBus.Instance.Subscribe<LocalPlayerRestoredEvent>(OnPlayerRestored);
        GameEventBus.Instance.Subscribe<LocalPlayerReadyEvent>(OnPlayerReady);
        GameEventBus.Instance.Subscribe<LocalPlayerNotFoundEvent>(OnPlayerNotFound);
        GameEventBus.Instance.Subscribe<PlayerCreationFailedEvent>(OnPlayerCreationFailed);
    }
    
    private void UnsubscribeFromEvents()
    {
        GameEventBus.Instance.Unsubscribe<StateChangedEvent>(OnStateChanged);
        GameEventBus.Instance.Unsubscribe<ConnectionEstablishedEvent>(OnConnectionEstablished);
        GameEventBus.Instance.Unsubscribe<ConnectionLostEvent>(OnConnectionLost);
        GameEventBus.Instance.Unsubscribe<ConnectionFailedEvent>(OnConnectionFailed);
        GameEventBus.Instance.Unsubscribe<LoginSuccessfulEvent>(OnLoginSuccessful);
        GameEventBus.Instance.Unsubscribe<LoginFailedEvent>(OnLoginFailed);
        GameEventBus.Instance.Unsubscribe<SessionCreatedEvent>(OnSessionCreated);
        GameEventBus.Instance.Unsubscribe<SessionRestoredEvent>(OnSessionRestored);
        GameEventBus.Instance.Unsubscribe<LocalPlayerCheckStartedEvent>(OnPlayerCheckStarted);
        GameEventBus.Instance.Unsubscribe<LocalPlayerCreatedEvent>(OnPlayerCreated);
        GameEventBus.Instance.Unsubscribe<LocalPlayerRestoredEvent>(OnPlayerRestored);
        GameEventBus.Instance.Unsubscribe<LocalPlayerReadyEvent>(OnPlayerReady);
        GameEventBus.Instance.Unsubscribe<LocalPlayerNotFoundEvent>(OnPlayerNotFound);
        GameEventBus.Instance.Unsubscribe<PlayerCreationFailedEvent>(OnPlayerCreationFailed);
    }
    
    #endregion
    
    #region UI Setup
    
    private void SetupUI()
    {
        if (uiDocument == null)
        {
            Debug.LogError("[LoginUI] UIDocument is not assigned!");
            return;
        }
        
        root = uiDocument.rootVisualElement;
        
        // Get UI elements - FIXED: Using exact names from UXML (kebab-case)
        authPanel = root.Q<VisualElement>("auth-panel");
        loginForm = root.Q<VisualElement>("login-form");
        registerForm = root.Q<VisualElement>("register-form");
        loadingOverlay = root.Q<VisualElement>("loading-overlay");
        
        // Login form - FIXED: Using exact names from UXML
        loginUsernameField = root.Q<TextField>("login-username");
        loginPinField = root.Q<TextField>("login-pin");
        loginButton = root.Q<Button>("login-button");
        showRegisterButton = root.Q<Button>("show-register-button");
        
        // Register form - FIXED: Using exact names from UXML
        registerUsernameField = root.Q<TextField>("register-username");
        registerDisplayNameField = root.Q<TextField>("register-display-name");
        registerPinField = root.Q<TextField>("register-pin");
        registerConfirmPinField = root.Q<TextField>("register-confirm-pin");
        registerButton = root.Q<Button>("register-button");
        showLoginButton = root.Q<Button>("show-login-button");
        
        // Loading - FIXED: Using exact names from UXML
        loadingText = root.Q<Label>("loading-text");
        errorText = root.Q<Label>("error-text");
        
        // Debug log to verify elements were found
        Debug.Log($"[LoginUI] UI Elements found - authPanel: {authPanel != null}, loginForm: {loginForm != null}, loadingOverlay: {loadingOverlay != null}");
        
        // Setup event handlers
        SetupEventHandlers();
    }
    
    private void SetupEventHandlers()
    {
        loginButton?.RegisterCallback<ClickEvent>(evt => HandleLogin());
        registerButton?.RegisterCallback<ClickEvent>(evt => HandleRegister());
        showRegisterButton?.RegisterCallback<ClickEvent>(evt => ShowRegisterForm());
        showLoginButton?.RegisterCallback<ClickEvent>(evt => ShowLoginForm());
        
        // Enter key handlers
        loginPinField?.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                HandleLogin();
            }
        });
        
        registerConfirmPinField?.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                HandleRegister();
            }
        });
    }
    
    #endregion
    
    #region UI State Management
    
    private void UpdateUIForState(GameEventBus.GameState state)
    {
        Debug.Log($"[LoginUI] Updating UI for state: {state}");
        
        switch (state)
        {
            case GameEventBus.GameState.Disconnected:
            case GameEventBus.GameState.Connecting:
                ShowConnectingUI();
                break;
                
            case GameEventBus.GameState.Connected:
                // Stay in connecting state until we know if player exists
                ShowConnectingUI("Checking account...");
                break;
                
            case GameEventBus.GameState.CheckingPlayer:
                ShowConnectingUI("Checking for existing player...");
                break;
                
            case GameEventBus.GameState.WaitingForLogin:
                ShowLoginUI();
                break;
                
            case GameEventBus.GameState.Authenticating:
                ShowLoadingOverlay("Logging in...");
                break;
                
            case GameEventBus.GameState.Authenticated:
                ShowLoadingOverlay("Loading character...");
                break;
                
            case GameEventBus.GameState.CreatingPlayer:
                ShowLoadingOverlay("Creating character...");
                break;
                
            case GameEventBus.GameState.PlayerReady:
            case GameEventBus.GameState.LoadingWorld:
            case GameEventBus.GameState.InGame:
                // Hide UI - game scene will load
                HideAll();
                break;
        }
    }
    
    private void ShowConnectingUI(string message = "Connecting to server...")
    {
        ShowLoadingOverlay(message);
        if (authPanel != null)
        {
            authPanel.style.display = DisplayStyle.None;
        }
    }
    
    private void ShowLoginUI()
    {
        Debug.Log("[LoginUI] Showing login panel");
        HideLoadingOverlay();
        
        if (authPanel != null)
        {
            authPanel.style.display = DisplayStyle.Flex;
            authPanel.RemoveFromClassList("hidden");
            Debug.Log($"[LoginUI] Auth panel display: {authPanel.style.display.value}, classes: {string.Join(", ", authPanel.GetClasses())}");
        }
        else
        {
            Debug.LogError("[LoginUI] authPanel is null! Check element name in UXML.");
        }
        
        ShowLoginForm();
        HideMessage();
    }
    
    private void HideAll()
    {
        if (authPanel != null)
        {
            authPanel.style.display = DisplayStyle.None;
        }
        HideLoadingOverlay();
    }
    
    private void ShowLoginForm()
    {
        loginForm?.RemoveFromClassList("hidden");
        registerForm?.AddToClassList("hidden");
        loginUsernameField?.Focus();
    }
    
    private void ShowRegisterForm()
    {
        loginForm?.AddToClassList("hidden");
        registerForm?.RemoveFromClassList("hidden");
        registerUsernameField?.Focus();
    }
    
    private void ShowLoadingOverlay(string text)
    {
        if (loadingOverlay != null)
        {
            loadingOverlay.RemoveFromClassList("hidden");
            loadingOverlay.style.display = DisplayStyle.Flex;
        }
        
        if (loadingText != null)
        {
            loadingText.text = text;
        }
    }
    
    private void HideLoadingOverlay()
    {
        if (loadingOverlay != null)
        {
            loadingOverlay.AddToClassList("hidden");
            loadingOverlay.style.display = DisplayStyle.None;
        }
    }
    
    private void ShowMessage(string message, bool isError = false)
    {
        if (errorText != null)
        {
            errorText.text = message;
            errorText.RemoveFromClassList("hidden");
            
            if (isError)
            {
                errorText.AddToClassList("error-text");
            }
            else
            {
                errorText.RemoveFromClassList("error-text");
            }
        }
    }
    
    private void HideMessage()
    {
        errorText?.AddToClassList("hidden");
    }
    
    #endregion
    
    #region Login/Register Handlers
    
    private void HandleLogin()
    {
        if (isProcessingLogin) return;
        
        string username = loginUsernameField?.value;
        string pin = loginPinField?.value;
        
        if (string.IsNullOrEmpty(username))
        {
            ShowMessage("Please enter a username", true);
            return;
        }
        
        if (string.IsNullOrEmpty(pin) || pin.Length != 4)
        {
            ShowMessage("Please enter a 4-digit PIN", true);
            return;
        }
        
        isProcessingLogin = true;
        pendingUsername = username;
        
        // Store username temporarily in GameData
        GameData.Instance.SetUsername(username);
        
        ShowLoadingOverlay("Logging in...");
        
        // Publish login started event
        GameEventBus.Instance.Publish(new LoginStartedEvent
        {
            Username = username
        });
        
        // Call the login reducer using static property
        if (GameManager.IsConnected() && GameManager.Conn != null)
        {
            GameManager.Conn.Reducers.LoginWithSession(username, pin, GetDeviceInfo());
        }
        else
        {
            ShowMessage("Not connected to server", true);
            isProcessingLogin = false;
        }
    }
    
    private void HandleRegister()
    {
        string username = registerUsernameField?.value;
        string displayName = registerDisplayNameField?.value;
        string pin = registerPinField?.value;
        string confirmPin = registerConfirmPinField?.value;
        
        if (string.IsNullOrEmpty(username))
        {
            ShowMessage("Please enter a username", true);
            return;
        }
        
        if (string.IsNullOrEmpty(displayName))
        {
            ShowMessage("Please enter a display name", true);
            return;
        }
        
        if (string.IsNullOrEmpty(pin) || pin.Length != 4)
        {
            ShowMessage("Please enter a 4-digit PIN", true);
            return;
        }
        
        if (pin != confirmPin)
        {
            ShowMessage("PINs do not match", true);
            return;
        }
        
        // For now, just show a message since registration isn't implemented
        ShowMessage("Registration coming soon!", false);
    }
    
    private string GetDeviceInfo()
    {
        return $"{SystemInfo.deviceModel}|{SystemInfo.operatingSystem}|{SystemInfo.deviceUniqueIdentifier}";
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnStateChanged(StateChangedEvent evt)
    {
        UpdateUIForState(evt.NewState);
    }
    
    private void OnConnectionEstablished(ConnectionEstablishedEvent evt)
    {
        Debug.Log("[LoginUI] Connection established");
    }
    
    private void OnConnectionLost(ConnectionLostEvent evt)
    {
        ShowMessage("Connection lost. Please try again.", true);
        isProcessingLogin = false;
    }
    
    private void OnConnectionFailed(ConnectionFailedEvent evt)
    {
        ShowMessage($"Connection failed: {evt.Error}", true);
        isProcessingLogin = false;
    }
    
    private void OnLoginSuccessful(LoginSuccessfulEvent evt)
    {
        Debug.Log($"[LoginUI] Login successful for {evt.Username}");
        ShowLoadingOverlay("Loading character...");
        
        // Reset the login processing flag
        isProcessingLogin = false;
    }
    
    private void OnLoginFailed(LoginFailedEvent evt)
    {
        Debug.Log($"[LoginUI] Login failed: {evt.Reason}");
        ShowMessage(evt.Reason, true);
        isProcessingLogin = false;
        HideLoadingOverlay();
    }
    
    private void OnSessionCreated(SessionCreatedEvent evt)
    {
        Debug.Log($"[LoginUI] Session created for {evt.Username}");
    }
    
    private void OnSessionRestored(SessionRestoredEvent evt)
    {
        Debug.Log($"[LoginUI] Session restored");
    }
    
    private void OnPlayerCheckStarted(LocalPlayerCheckStartedEvent evt)
    {
        ShowLoadingOverlay("Checking for existing character...");
    }
    
    private void OnPlayerCreated(LocalPlayerCreatedEvent evt)
    {
        Debug.Log($"[LoginUI] Player created: {evt.Player.Name}");
    }
    
    private void OnPlayerRestored(LocalPlayerRestoredEvent evt)
    {
        Debug.Log($"[LoginUI] Player restored: {evt.Player.Name}");
    }
    
    private void OnPlayerReady(LocalPlayerReadyEvent evt)
    {
        Debug.Log($"[LoginUI] Player ready: {evt.Player.Name}");
        HideAll();
    }
    
    private void OnPlayerNotFound(LocalPlayerNotFoundEvent evt)
    {
        Debug.Log("[LoginUI] No player found - creating player automatically (stub)");
        
        // STUB: Automatically create a player with the username
        // Later this will show a player creation UI instead
        string username = GameData.Instance.Username;
        if (!string.IsNullOrEmpty(username))
        {
            ShowLoadingOverlay("Creating character...");
            
            // Publish player creation started event
            GameEventBus.Instance.Publish(new PlayerCreationStartedEvent
            {
                Username = username
            });
            
            // Call the CreatePlayer reducer with username as the player name
            if (GameManager.IsConnected() && GameManager.Conn != null)
            {
                Debug.Log($"[LoginUI] Creating player with name: {username}");
                GameManager.Conn.Reducers.CreatePlayer(username);
            }
            else
            {
                Debug.LogError("[LoginUI] Cannot create player - not connected");
                ShowMessage("Connection lost", true);
            }
        }
        else
        {
            Debug.LogError("[LoginUI] No username available for player creation");
            ShowLoginUI();
        }
    }
    
    private void OnPlayerCreationFailed(PlayerCreationFailedEvent evt)
    {
        ShowMessage($"Failed to create character: {evt.Reason}", true);
        isProcessingLogin = false;
    }
    
    #endregion
}