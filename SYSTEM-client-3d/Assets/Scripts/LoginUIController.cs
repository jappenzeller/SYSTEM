using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using SpacetimeDB;
using SpacetimeDB.Types;

/// <summary>
/// Handles the login UI functionality through EventBus events only.
/// No direct database interactions - all handled through EventBridge.
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
    private Label messageLabel;  // Changed from VisualElement to Label
    
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
        
        // Register with GameManager
        GameManager.RegisterLoginUI(this);
        
        // Subscribe to EventBus events
        SubscribeToEvents();
        
        // Subscribe to GameManager connection events (these are still direct)
        GameManager.OnConnected += HandleConnected;
        GameManager.OnConnectionError += HandleConnectionError;
        GameManager.OnDisconnected += HandleDisconnected;
    }
    
    void Start()
    {
        SetupUI();
        
        // Check initial connection state
        if (GameManager.IsConnected())
        {
            HandleConnected();
        }
        else
        {
            ShowConnecting();
        }
    }
    
    void OnDestroy()
    {
        UnsubscribeFromEvents();
        
        // Unsubscribe from GameManager events
        GameManager.OnConnected -= HandleConnected;
        GameManager.OnConnectionError -= HandleConnectionError;
        GameManager.OnDisconnected -= HandleDisconnected;
    }
    
    #endregion
    
    #region Event Subscriptions
    
    private void SubscribeToEvents()
    {
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
        
        // Connection events
        GameEventBus.Instance.Subscribe<ConnectionEstablishedEvent>(OnConnectionEstablished);
        GameEventBus.Instance.Subscribe<SubscriptionReadyEvent>(OnSubscriptionReady);
    }
    
    private void UnsubscribeFromEvents()
    {
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
        GameEventBus.Instance.Unsubscribe<ConnectionEstablishedEvent>(OnConnectionEstablished);
        GameEventBus.Instance.Unsubscribe<SubscriptionReadyEvent>(OnSubscriptionReady);
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
        messageLabel = root.Q<Label>("MessageLabel");  // Changed to Label
        
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
        
        // Initial state
        ShowLoginForm();
        HideLoadingOverlay();
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
        Debug.Log("[LoginUI] HandleLogin called");
        
        string username = loginUsernameField?.value;
        string pin = loginPinField?.value;
        
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(pin))
        {
            ShowError("Please enter username and PIN");
            return;
        }
        
        if (pin.Length != 4 || !int.TryParse(pin, out _))
        {
            ShowError("PIN must be exactly 4 digits");
            return;
        }
        
        if (GameManager.IsConnected())
        {
            ShowLoadingOverlay("Logging in...");
            HideMessage();
            
            // Store username for later
            pendingUsername = username;
            isProcessingLogin = true;
            
            // Publish login started event
            GameEventBus.Instance.Publish(new LoginStartedEvent
            {
                Username = username
            });
            
            // Call the login reducer
            GameManager.LoginWithSession(username, pin);
        }
        else
        {
            ShowError("Not connected to server");
        }
    }
    
    private void HandleRegister()
    {
        Debug.Log("[LoginUI] HandleRegister called");
        
        string username = registerUsernameField?.value;
        string displayName = registerDisplayNameField?.value;
        string pin = registerPinField?.value;
        string confirmPin = registerConfirmPinField?.value;
        
        // Validation
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(displayName) || 
            string.IsNullOrEmpty(pin) || string.IsNullOrEmpty(confirmPin))
        {
            ShowError("Please fill in all fields");
            return;
        }
        
        if (pin != confirmPin)
        {
            ShowError("PINs do not match");
            return;
        }
        
        if (pin.Length != 4 || !int.TryParse(pin, out _))
        {
            ShowError("PIN must be exactly 4 digits");
            return;
        }
        
        if (GameManager.IsConnected())
        {
            ShowLoadingOverlay("Creating account...");
            HideMessage();
            
            // Store username for later
            pendingUsername = username;
            
            // Call the register reducer
            if (GameManager.Conn != null)
            {
                GameManager.Conn.Reducers.RegisterAccount(username, displayName, pin);
            }
        }
        else
        {
            ShowError("Not connected to server");
        }
    }
    
    #endregion
    
    #region GameManager Event Handlers
    
    private void HandleConnected()
    {
        Debug.Log("[LoginUI] Connected to server");
        HideLoadingOverlay();
        ShowSuccess("Connected to server");
    }
    
    private void HandleConnectionError(string error)
    {
        Debug.LogError($"[LoginUI] Connection error: {error}");
        HideLoadingOverlay();
        ShowError($"Connection error: {error}");
        ShowLoginPanel();
    }
    
    private void HandleDisconnected()
    {
        Debug.Log("[LoginUI] Disconnected from server");
        ShowConnecting();
    }
    
    #endregion
    
    #region EventBus Event Handlers
    
    private void OnConnectionEstablished(ConnectionEstablishedEvent evt)
    {
        Debug.Log("[LoginUI] Connection established via EventBus");
    }
    
    private void OnSubscriptionReady(SubscriptionReadyEvent evt)
    {
        Debug.Log("[LoginUI] Subscription ready - waiting for player check");
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
        ShowError(evt.Reason ?? "Login failed");
        isProcessingLogin = false;
        pendingUsername = null;
    }
    
    private void OnSessionCreated(SessionCreatedEvent evt)
    {
        Debug.Log($"[LoginUI] Session created for: {evt.Username}");
        // Session is created, now wait for player check
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
        Debug.Log("[LoginUI] No player found - need to create");
        
        if (!string.IsNullOrEmpty(pendingUsername))
        {
            ShowLoadingOverlay("Creating character...");
            GameManager.CreatePlayer(pendingUsername);
        }
    }
    
    private void OnPlayerCreated(LocalPlayerCreatedEvent evt)
    {
        Debug.Log($"[LoginUI] Player created: {evt.Player.Name}");
        // Player is created, game will transition automatically
    }
    
    private void OnPlayerRestored(LocalPlayerRestoredEvent evt)
    {
        Debug.Log($"[LoginUI] Player restored: {evt.Player.Name}");
        // Player is restored, game will transition automatically
    }
    
    private void OnPlayerReady(LocalPlayerReadyEvent evt)
    {
        Debug.Log($"[LoginUI] Player ready: {evt.Player.Name}");
        ShowSuccess("Loading game...");
        // GameManager will handle scene transition
    }
    
    private void OnPlayerCreationFailed(PlayerCreationFailedEvent evt)
    {
        Debug.LogError($"[LoginUI] Player creation failed: {evt.Reason}");
        HideLoadingOverlay();
        ShowError($"Failed to create character: {evt.Reason}");
    }
    
    #endregion
    
    #region Public Methods for GameManager
    
    public void ShowLoginPanel()
    {
        if (authPanel != null)
        {
            authPanel.style.display = DisplayStyle.Flex;
        }
        ShowLoginForm();
        HideLoadingOverlay();
    }
    
    public void HideLoading()
    {
        HideLoadingOverlay();
    }
    
    public void ShowErrorMessage(string message)
    {
        ShowError(message);
    }
    
    private void ShowConnecting()
    {
        ShowLoadingOverlay("Connecting to server...");
        if (authPanel != null)
        {
            authPanel.style.display = DisplayStyle.None;
        }
    }
    
    #endregion
}