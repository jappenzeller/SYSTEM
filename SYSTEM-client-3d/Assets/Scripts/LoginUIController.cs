using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using SpacetimeDB;
using SpacetimeDB.Types;

public class LoginUIController : MonoBehaviour
{
    private UIDocument uiDocument;
    private VisualElement root;
    
    // Panels
    private VisualElement authPanel;
    private VisualElement loginForm;
    private VisualElement registerForm;
    private VisualElement loadingOverlay;
    
    // Login Form Elements
    private TextField loginUsernameField;
    private TextField loginPinField;
    private Button loginButton;
    private Button showRegisterButton;
    
    // Register Form Elements  
    private TextField registerUsernameField;
    private TextField registerDisplayNameField;
    private TextField registerPinField;
    private TextField registerConfirmPinField;
    private Button registerButton;
    private Button showLoginButton;
    
    // Loading Elements
    private Label loadingText;
    private Label messageLabel;
    
    // Device Info
    private string deviceInfo;
    
    // Store username for session saving
    public string lastLoginUsername { get; private set; }
    
    // Events
    public event Action<string, string> OnLoginRequested;
    public event Action<string, string> OnRegisterRequested;
    
    void Awake()
    {
        Debug.Log("[LoginUI] Awake called");
        
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
            Debug.Log($"[LoginUI] UIDocument found: {uiDocument != null}");
        }
        
        deviceInfo = GenerateDeviceInfo();
    }
    
    void Start()
    {
        Debug.Log("[LoginUI] Start called");
        
        // Register with GameManager
        GameManager.RegisterLoginUI(this);
        
        // Setup UI elements first
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            root = uiDocument.rootVisualElement;
            SetupElements();
            SetupEventHandlers();
            
            // Show loading initially
            ShowLoadingOverlay("Connecting to server...");
        }
        else
        {
            Debug.LogError("[LoginUI] UIDocument or root element is null!");
        }
    }
    
    void OnDestroy()
    {
        // Clean up event subscriptions
        UnsubscribeFromReducerEvents();
    }
    
    #region Setup
    
    private void SetupElements()
    {
        Debug.Log("[LoginUI] Setting up elements");
        
        // Panels
        authPanel = root.Q<VisualElement>("auth-panel");
        loginForm = root.Q<VisualElement>("login-form");
        registerForm = root.Q<VisualElement>("register-form");
        loadingOverlay = root.Q<VisualElement>("loading-overlay");
        
        // Login form elements
        loginUsernameField = root.Q<TextField>("login-username");
        loginPinField = root.Q<TextField>("login-pin");
        loginButton = root.Q<Button>("login-button");
        showRegisterButton = root.Q<Button>("show-register-button");
        
        // Register form elements
        registerUsernameField = root.Q<TextField>("register-username");
        registerDisplayNameField = root.Q<TextField>("register-display-name");
        registerPinField = root.Q<TextField>("register-pin");
        registerConfirmPinField = root.Q<TextField>("register-confirm-pin");
        registerButton = root.Q<Button>("register-button");
        showLoginButton = root.Q<Button>("show-login-button");
        
        // Loading elements
        loadingText = root.Q<Label>("loading-text");
        messageLabel = root.Q<Label>("message-label");
        
        // Set initial state
        ShowLoginForm();
        HideLoadingOverlay();
    }
    
    private void SetupEventHandlers()
    {
        loginButton?.RegisterCallback<ClickEvent>(evt => HandleLogin());
        registerButton?.RegisterCallback<ClickEvent>(evt => HandleRegister());
        showRegisterButton?.RegisterCallback<ClickEvent>(evt => ShowRegisterForm());
        showLoginButton?.RegisterCallback<ClickEvent>(evt => ShowLoginForm());
        
        // Enter key submits forms
        loginPinField?.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                HandleLogin();
        });
        
        registerConfirmPinField?.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                HandleRegister();
        });
    }
    
    #endregion
    
    #region Connection Handling
    
    public void OnConnectionEstablished()
    {
        Debug.Log("[LoginUI] Connection established");
        
        // Subscribe to reducer events
        SubscribeToReducerEvents();
        
        // Check for saved session
        string savedToken = AuthToken.LoadSession();
        string savedUsername = AuthToken.LoadLastUsername();
        
        if (!string.IsNullOrEmpty(savedToken) && !string.IsNullOrEmpty(savedUsername))
        {
            ShowLoadingOverlay("Restoring session...");
            
            // Pre-fill username field
            if (loginUsernameField != null)
            {
                loginUsernameField.value = savedUsername;
            }
            
            // Attempt to restore session
            var conn = GameManager.Conn;
            conn?.Reducers.RestoreSession(savedToken);
        }
        else
        {
            HideLoading();
            ShowLoginPanel();
        }
    }
    
    public void OnConnectionFailed(string error)
    {
        Debug.LogError($"[LoginUI] Connection failed: {error}");
        HideLoadingOverlay();
        ShowError($"Connection failed: {error}");
    }
    
    #endregion
    
    #region Reducer Event Subscriptions
    
    private void SubscribeToReducerEvents()
    {
        var conn = GameManager.Conn;
        if (conn != null)
        {
            conn.Reducers.OnRegisterAccount += OnRegisterAccountResponse;
            conn.Reducers.OnLoginWithSession += OnLoginWithSessionResponse;
            conn.Reducers.OnRestoreSession += OnRestoreSessionResponse;
            conn.Reducers.OnLogout += OnLogoutResponse;
        }
    }
    
    private void UnsubscribeFromReducerEvents()
    {
        var conn = GameManager.Conn;
        if (conn != null)
        {
            conn.Reducers.OnRegisterAccount -= OnRegisterAccountResponse;
            conn.Reducers.OnLoginWithSession -= OnLoginWithSessionResponse;
            conn.Reducers.OnRestoreSession -= OnRestoreSessionResponse;
            conn.Reducers.OnLogout -= OnLogoutResponse;
        }
    }
    
    #endregion
    
    #region Reducer Response Handlers
    
    private void OnRegisterAccountResponse(ReducerEventContext ctx, string username, string displayName, string pin)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            HideLoadingOverlay();
            ShowMessage($"Registration successful! Please login.");
            
            // Switch to login form and pre-fill username
            ShowLoginForm();
            if (loginUsernameField != null)
            {
                loginUsernameField.value = username;
            }
            loginPinField?.Focus();
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            HideLoadingOverlay();
            ShowError(reason ?? "Registration failed");
        }
    }
    
    private void OnLoginWithSessionResponse(ReducerEventContext ctx, string username, string pin, string deviceInfo)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            // Login succeeded - session token will be in SessionResult table
            Debug.Log($"[LoginUI] Login successful for {username}");
            
            // The GameManager will handle the session token retrieval and scene transition
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            HideLoadingOverlay();
            ShowError(reason ?? "Login failed");
        }
    }
    
    private void OnRestoreSessionResponse(ReducerEventContext ctx, string sessionToken)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            HideLoadingOverlay();
            Debug.Log("[LoginUI] Session restored successfully");
            
            // The GameManager will handle the scene transition when player is ready
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            HideLoadingOverlay();
            ShowError("Session expired. Please login again.");
            AuthToken.ClearSession();
            ShowLoginPanel();
        }
    }
    
    private void OnLogoutResponse(ReducerEventContext ctx)
    {
        HideLoadingOverlay();
        ShowMessage("Logged out successfully");
        ShowLoginPanel();
    }
    
    #endregion
    
    #region UI State Management
    
    public void ShowLoginPanel()
    {
        Debug.Log("[LoginUI] ShowLoginPanel called");
        if (authPanel != null)
        {
            authPanel.RemoveFromClassList("hidden");
            ShowLoginForm();
        }
        else
        {
            Debug.LogError("[LoginUI] authPanel is null!");
        }
    }
    
    public void HideLoginPanel()
    {
        Debug.Log("[LoginUI] HideLoginPanel called");
        if (authPanel != null)
        {
            authPanel.AddToClassList("hidden");
        }
    }
    
    private void ShowLoginForm()
    {
        loginForm?.RemoveFromClassList("hidden");
        registerForm?.AddToClassList("hidden");
        ClearError();
        
        // Pre-fill username if available
        string savedUsername = AuthToken.LoadLastUsername();
        if (!string.IsNullOrEmpty(savedUsername) && loginUsernameField != null)
        {
            loginUsernameField.value = savedUsername;
            loginPinField?.Focus();
        }
        else
        {
            loginUsernameField?.Focus();
        }
    }
    
    private void ShowRegisterForm()
    {
        loginForm?.AddToClassList("hidden");
        registerForm?.RemoveFromClassList("hidden");
        ClearError();
        registerUsernameField?.Focus();
    }
    
    #endregion
    
    #region Loading & Messages
    
    public void ShowLoadingOverlay(string text = "Loading...")
    {
        loadingOverlay?.RemoveFromClassList("hidden");
        if (loadingText != null)
        {
            loadingText.text = text;
        }
    }
    
    public void HideLoadingOverlay()
    {
        loadingOverlay?.AddToClassList("hidden");
    }
    
    public void HideLoading()
    {
        HideLoadingOverlay();
    }
    
    public void ShowError(string message)
    {
        Debug.LogError($"[LoginUI] Error: {message}");
        
        var errorLabel = root.Q<Label>("error-text");
        if (errorLabel != null)
        {
            errorLabel.text = message;
            errorLabel.RemoveFromClassList("hidden");
        }
        
        if (messageLabel != null)
        {
            messageLabel.text = message;
            messageLabel.RemoveFromClassList("hidden");
            messageLabel.AddToClassList("error");
        }
    }
    
    public void ShowMessage(string message)
    {
        Debug.Log($"[LoginUI] Message: {message}");
        
        if (messageLabel != null)
        {
            messageLabel.text = message;
            messageLabel.RemoveFromClassList("hidden");
            messageLabel.RemoveFromClassList("error");
        }
    }
    
    private void ClearError()
    {
        var errorLabel = root.Q<Label>("error-text");
        errorLabel?.AddToClassList("hidden");
        
        messageLabel?.AddToClassList("hidden");
    }
    
    #endregion
    
    #region Session Management
    
    private string GenerateDeviceInfo()
    {
        return $"{SystemInfo.deviceModel}|{SystemInfo.operatingSystem}|{SystemInfo.deviceUniqueIdentifier}";
    }
    
    #endregion
    
    #region UI Actions
    
    private void HandleLogin()
    {
        string username = loginUsernameField?.value;
        string pin = loginPinField?.value;
        
        // Validate input
        if (string.IsNullOrEmpty(username))
        {
            ShowError("Please enter username");
            return;
        }
        
        if (string.IsNullOrEmpty(pin))
        {
            ShowError("Please enter PIN");
            return;
        }
        
        // Validate PIN format
        if (pin.Length != 4 || !int.TryParse(pin, out _))
        {
            ShowError("PIN must be 4 digits");
            return;
        }
        
        ShowLoadingOverlay("Logging in...");
        
        // Store username for session saving
        lastLoginUsername = username;
        
        // Call the login reducer
        var conn = GameManager.Conn;
        if (conn != null)
        {
            Debug.Log($"[LoginUI] Calling LoginWithSession - Username: {username}");
            conn.Reducers.LoginWithSession(username, pin, deviceInfo);
        }
        else
        {
            HideLoadingOverlay();
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
        
        Debug.Log($"[LoginUI] Register attempt - Username: {username}, DisplayName: {displayName}, PIN: {pin}");
        
        // Validation
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(displayName))
        {
            ShowError("Please fill in all fields");
            return;
        }
        
        if (username.Length < 3 || username.Length > 20)
        {
            ShowError("Username must be 3-20 characters");
            return;
        }
        
        if (displayName.Length < 3 || displayName.Length > 20)
        {
            ShowError("Display name must be 3-20 characters");
            return;
        }
        
        if (pin.Length != 4 || !int.TryParse(pin, out _))
        {
            ShowError("PIN must be 4 digits");
            return;
        }
        
        if (pin != confirmPin)
        {
            ShowError("PINs do not match");
            return;
        }
        
        ShowLoadingOverlay("Creating account...");
        
        var conn = GameManager.Conn;
        if (conn != null)
        {
            Debug.Log("[LoginUI] Calling RegisterAccount reducer");
            conn.Reducers.RegisterAccount(username, displayName, pin);
        }
        else
        {
            HideLoadingOverlay();
            ShowError("Not connected to server");
        }
    }
    
    #endregion
}