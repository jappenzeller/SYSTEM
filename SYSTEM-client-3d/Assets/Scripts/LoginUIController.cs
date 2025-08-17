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
        // State changes
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
        
        // Get UI elements
        authPanel = root.Q<VisualElement>("AuthPanel");
        loginForm = root.Q<VisualElement>("LoginForm");
        registerForm = root.Q<VisualElement>("RegisterForm");
        loadingOverlay = root.Q<VisualElement>("LoadingOverlay");
        messageLabel = root.Q<Label>("MessageLabel");
        
        // Login form
        loginUsernameField = root.Q<TextField>("LoginUsername");
        loginPasswordField = root.Q<TextField>("LoginPassword");
        loginPinField = root.Q<TextField>("LoginPin");
        loginButton = root.Q<Button>("LoginButton");
        showRegisterButton = root.Q<Button>("ShowRegisterButton");
        
        // Register form
        registerUsernameField = root.Q<TextField>("RegisterUsername");
        registerDisplayNameField = root.Q<TextField>("RegisterDisplayName");
        registerPinField = root.Q<TextField>("RegisterPin");
        registerConfirmPinField = root.Q<TextField>("RegisterConfirmPin");
        registerButton = root.Q<Button>("RegisterButton");
        showLoginButton = root.Q<Button>("ShowLoginButton");
        
        // Loading
        loadingText = root.Q<Label>("LoadingText");
        
        // Hide password field if it exists (we only use PIN)
        if (loginPasswordField != null)
        {
            loginPasswordField.style.display = DisplayStyle.None;
        }
        
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
            authPanel.RemoveFromClassList("hidden");  // <-- ADD THIS LINE
            Debug.Log($"[LoginUI] Auth panel display: {authPanel.style.display.value}");
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
    
    private void ShowLoadingOverlay(string message = "Loading...")
    {
        if (loadingOverlay != null)
        {
            loadingOverlay.style.display = DisplayStyle.Flex;
        }
        
        if (loadingText != null)
        {
            loadingText.text = message;
        }
    }
    
    private void HideLoadingOverlay()
    {
        if (loadingOverlay != null)
        {
            loadingOverlay.style.display = DisplayStyle.None;
        }
    }
    
    private void ShowError(string message)
    {
        if (messageLabel != null)
        {
            messageLabel.text = message;
            messageLabel.style.display = DisplayStyle.Flex;
            messageLabel.AddToClassList("error");
            messageLabel.RemoveFromClassList("success");
        }
    }
    
    private void ShowSuccess(string message)
    {
        if (messageLabel != null)
        {
            messageLabel.text = message;
            messageLabel.style.display = DisplayStyle.Flex;
            messageLabel.AddToClassList("success");
            messageLabel.RemoveFromClassList("error");
        }
    }
    
    private void HideMessage()
    {
        if (messageLabel != null)
        {
            messageLabel.style.display = DisplayStyle.None;
        }
    }
    
    #endregion
    
    #region UI Actions
    
    private void HandleLogin()
    {
        if (isProcessingLogin) return;
        
        string username = loginUsernameField?.value?.Trim() ?? "";
        string pin = loginPinField?.value?.Trim() ?? "";
        
        if (string.IsNullOrEmpty(username))
        {
            ShowError("Please enter your username");
            return;
        }
        
        if (string.IsNullOrEmpty(pin))
        {
            ShowError("Please enter your PIN");
            return;
        }
        
        // Validate PIN format
        if (pin.Length != 4 || !int.TryParse(pin, out _))
        {
            ShowError("PIN must be 4 digits");
            return;
        }
        
        if (GameManager.IsConnected())
        {
            isProcessingLogin = true;
            ShowLoadingOverlay("Logging in...");
            HideMessage();
            
            // Store username for later
            pendingUsername = username;
            
            // Get device info
            string deviceInfo = SystemInfo.deviceUniqueIdentifier;
            
            // Call the login reducer
            GameManager.Conn.Reducers.LoginWithSession(username, pin, deviceInfo);
        }
        else
        {
            ShowError("Not connected to server");
        }
    }
    
    private void HandleRegister()
    {
        string username = registerUsernameField?.value?.Trim() ?? "";
        string displayName = registerDisplayNameField?.value?.Trim() ?? "";
        string pin = registerPinField?.value?.Trim() ?? "";
        string confirmPin = registerConfirmPinField?.value?.Trim() ?? "";
        
        // Validation
        if (string.IsNullOrEmpty(username))
        {
            ShowError("Please enter a username");
            return;
        }
        
        if (string.IsNullOrEmpty(displayName))
        {
            ShowError("Please enter a display name");
            return;
        }
        
        if (string.IsNullOrEmpty(pin))
        {
            ShowError("Please enter a PIN");
            return;
        }
        
        if (pin != confirmPin)
        {
            ShowError("PINs do not match");
            return;
        }
        
        if (pin.Length != 4 || !int.TryParse(pin, out _))
        {
            ShowError("PIN must be 4 digits");
            return;
        }
        
        if (GameManager.IsConnected())
        {
            ShowLoadingOverlay("Creating account...");
            HideMessage();
            
            // Store username for later
            pendingUsername = username;
            
            // Call the register reducer
            GameManager.Conn.Reducers.RegisterAccount(username, displayName, pin);
        }
        else
        {
            ShowError("Not connected to server");
        }
    }
    
    #endregion
    
    #region EventBus Event Handlers
    
    private void OnStateChanged(StateChangedEvent evt)
    {
        UpdateUIForState(evt.NewState);
    }
    
    private void OnConnectionEstablished(ConnectionEstablishedEvent evt)
    {
        Debug.Log("[LoginUI] Connection established");
        ShowSuccess("Connected to server");
    }
    
    private void OnConnectionLost(ConnectionLostEvent evt)
    {
        Debug.Log("[LoginUI] Connection lost");
        ShowConnectingUI("Reconnecting...");
    }
    
    private void OnConnectionFailed(ConnectionFailedEvent evt)
    {
        Debug.LogError($"[LoginUI] Connection failed: {evt.Error}");
        HideLoadingOverlay();
        ShowLoginUI();
        ShowError($"Connection failed: {evt.Error}");
    }
    
    private void OnLoginSuccessful(LoginSuccessfulEvent evt)
    {
        Debug.Log($"[LoginUI] Login successful: {evt.Username}");
        
        // Update GameData
        GameData.Instance.SetUsername(evt.Username);
        
        // Save session locally
        if (!string.IsNullOrEmpty(evt.SessionToken))
        {
            AuthToken.SaveSession(evt.SessionToken, evt.Username);
        }
        
        ShowLoadingOverlay("Loading character...");
    }
    
    private void OnLoginFailed(LoginFailedEvent evt)
    {
        Debug.LogError($"[LoginUI] Login failed: {evt.Reason}");
        HideLoadingOverlay();
        ShowLoginUI();
        ShowError(evt.Reason ?? "Login failed");
        isProcessingLogin = false;
        pendingUsername = null;
    }
    
    private void OnSessionCreated(SessionCreatedEvent evt)
    {
        Debug.Log($"[LoginUI] Session created for: {evt.Username}");
    }
    
    private void OnSessionRestored(SessionRestoredEvent evt)
    {
        Debug.Log($"[LoginUI] Session restored for: {evt.Username}");
    }
    
    private void OnPlayerCheckStarted(LocalPlayerCheckStartedEvent evt)
    {
        Debug.Log($"[LoginUI] Checking for player: {evt.Username}");
        ShowLoadingOverlay("Checking character...");
    }
    
    private void OnPlayerNotFound(LocalPlayerNotFoundEvent evt)
    {
        Debug.Log("[LoginUI] No player found - showing login");
        ShowLoginUI();
    }
    
    private void OnPlayerCreated(LocalPlayerCreatedEvent evt)
    {
        Debug.Log($"[LoginUI] Player created: {evt.Player.Name}");
        ShowSuccess("Character created!");
    }
    
    private void OnPlayerRestored(LocalPlayerRestoredEvent evt)
    {
        Debug.Log($"[LoginUI] Player restored: {evt.Player.Name}");
    }
    
    private void OnPlayerReady(LocalPlayerReadyEvent evt)
    {
        Debug.Log($"[LoginUI] Player ready: {evt.Player.Name}");
        ShowSuccess("Loading game...");
    }
    
    private void OnPlayerCreationFailed(PlayerCreationFailedEvent evt)
    {
        Debug.LogError($"[LoginUI] Player creation failed: {evt.Reason}");
        HideLoadingOverlay();
        ShowError($"Failed to create character: {evt.Reason}");
    }
    
    #endregion
}