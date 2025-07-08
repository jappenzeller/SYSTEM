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
    private TextField loginPasswordField;
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
    
    // Session Management
    private string pendingUsername;
    private bool isWaitingForSession;
    
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
            return;
        }
        
        // Subscribe to connection events
        GameManager.OnConnected += HandleConnect;
        GameManager.OnConnectionError += HandleConnectionError;
        GameManager.OnDisconnected += HandleDisconnect;
        
        // Register with GameManager - this will trigger ShowLoginPanel if already connected
        GameManager.RegisterLoginUI(this);
        
        // Add a fallback timeout
        Debug.Log("[LoginUI] Starting connection timeout coroutine");
        StartCoroutine(ConnectionTimeout());
        
        // Also try a direct invoke as backup
        Invoke(nameof(ForceShowLoginAfterTimeout), 3f);
    }
    
    private void ForceShowLoginAfterTimeout()
    {
        Debug.Log("[LoginUI] ForceShowLoginAfterTimeout called");
        if (loadingOverlay != null && !loadingOverlay.ClassListContains("hidden"))
        {
            Debug.LogWarning("[LoginUI] Forcing login panel display via Invoke");
            HideLoadingOverlay();
            ShowLoginPanel();
            ShowError("Unable to connect to server. Please check your connection.");
        }
    }
    
    private IEnumerator ConnectionTimeout()
    {
        Debug.Log("[LoginUI] ConnectionTimeout coroutine started");
        yield return new WaitForSeconds(3f);
        
        Debug.Log("[LoginUI] ConnectionTimeout timer elapsed");
        
        // If we're still showing loading after 3 seconds, force show login
        if (loadingOverlay != null && !loadingOverlay.ClassListContains("hidden"))
        {
            Debug.LogWarning("[LoginUI] Connection timeout - forcing login panel display");
            HideLoadingOverlay();
            ShowLoginPanel();
            
            // Show connection error message
            ShowError("Unable to connect to server. Please check your connection.");
        }
        else
        {
            Debug.Log("[LoginUI] Loading overlay already hidden, no action needed");
        }
    }
    
    void OnEnable()
    {
        Debug.Log("[LoginUI] OnEnable called");
        root = uiDocument.rootVisualElement;
        SetupElements();
        SetupEventHandlers();
        
        // Subscribe to SpacetimeDB events
        var conn = GameManager.Conn;
        if (conn != null)
        {
            conn.Reducers.OnRegisterAccount += OnRegisterAccountResponse;
            conn.Reducers.OnLoginWithSession += OnLoginWithSessionResponse;
            conn.Reducers.OnRestoreSession += OnRestoreSessionResponse;
            conn.Reducers.OnLogout += OnLogoutResponse;
            
            // Subscribe to SessionResult table changes
            conn.Db.SessionResult.OnInsert += OnSessionResultInsert;
            conn.Db.SessionResult.OnUpdate += OnSessionResultUpdate;
        }
        
        // Check for saved session
        CheckSavedSession();
    }
    
    void OnDestroy()
    {
        // Unsubscribe from GameManager events
        GameManager.OnConnected -= HandleConnect;
        GameManager.OnConnectionError -= HandleConnectionError;
        GameManager.OnDisconnected -= HandleDisconnect;
    }
    
    #region Setup
    
    private void SetupElements()
    {
        Debug.Log("[LoginUI] SetupElements called");
        
        // Get panels
        authPanel = root.Q<VisualElement>("auth-panel");
        loginForm = root.Q<VisualElement>("login-form");
        registerForm = root.Q<VisualElement>("register-form");
        loadingOverlay = root.Q<VisualElement>("loading-overlay");
        
        Debug.Log($"[LoginUI] Elements found - authPanel: {authPanel != null}, loginForm: {loginForm != null}, loadingOverlay: {loadingOverlay != null}");
        
        // Login form elements
        loginUsernameField = root.Q<TextField>("login-username");
        loginPasswordField = root.Q<TextField>("login-password");
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
        
        // Get error text label
        var errorLabel = root.Q<Label>("error-text");
        if (errorLabel != null)
        {
            errorLabel.RemoveFromClassList("hidden");
            messageLabel = errorLabel;
        }
        
        Debug.Log("[LoginUI] UI elements setup complete");
    }
    
    private void SetupEventHandlers()
    {
        Debug.Log("[LoginUI] SetupEventHandlers called");
        
        // Login form
        loginButton?.RegisterCallback<ClickEvent>(evt => HandleLogin());
        showRegisterButton?.RegisterCallback<ClickEvent>(evt => ShowRegisterForm());
        
        // Register form
        registerButton?.RegisterCallback<ClickEvent>(evt => HandleRegister());
        showLoginButton?.RegisterCallback<ClickEvent>(evt => ShowLoginForm());
        
        // Enter key handlers
        loginPasswordField?.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return)
                HandleLogin();
        });
        
        loginPinField?.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return)
                HandleLogin();
        });
        
        registerConfirmPinField?.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return)
                HandleRegister();
        });
        
        Debug.Log("[LoginUI] Event handlers setup complete");
    }
    
    #endregion
    
    #region SpacetimeDB Event Handlers
    
    private void OnRegisterAccountResponse(ReducerEventContext ctx, string username, string displayName, string pin)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            HideLoadingOverlay();
            ShowMessage("Registration successful! Please login.");
            
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
            // Login succeeded - wait for SessionResult table update
            pendingUsername = username;
            isWaitingForSession = true;
            
            Debug.Log($"[LoginUI] Login committed for {username}, waiting for session token...");
            
            // Start a timeout in case we don't get the session
            StartCoroutine(SessionTimeout());
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            HideLoadingOverlay();
            ShowError(reason ?? "Login failed");
            isWaitingForSession = false;
            pendingUsername = null;
        }
    }
    
    private void OnSessionResultInsert(EventContext ctx, SessionResult sessionResult)
    {
        Debug.Log($"[LoginUI] SessionResult inserted for identity: {sessionResult.Identity}");
        
        // Check if this is our session
        if (isWaitingForSession && GameManager.LocalIdentity != null && 
            sessionResult.Identity == GameManager.LocalIdentity)
        {
            ProcessSessionResult(sessionResult);
        }
    }
    
    private void OnSessionResultUpdate(EventContext ctx, SessionResult oldResult, SessionResult newResult)
    {
        Debug.Log($"[LoginUI] SessionResult updated for identity: {newResult.Identity}");
        
        // Check if this is our session
        if (isWaitingForSession && GameManager.LocalIdentity != null && 
            newResult.Identity == GameManager.LocalIdentity)
        {
            ProcessSessionResult(newResult);
        }
    }
    
    private void ProcessSessionResult(SessionResult sessionResult)
    {
        isWaitingForSession = false;
        
        // Save session
        AuthToken.SaveSession(sessionResult.SessionToken, pendingUsername);
        
        HideLoadingOverlay();
        Debug.Log($"[LoginUI] Login successful for {pendingUsername}, session token saved");
        
        // The GameManager will handle the scene transition when player is created
        pendingUsername = null;
    }
    
    private IEnumerator SessionTimeout()
    {
        yield return new WaitForSeconds(5f); // Wait 5 seconds for session
        
        if (isWaitingForSession)
        {
            Debug.LogError("[LoginUI] Session timeout - no session token received");
            HideLoadingOverlay();
            ShowError("Login timeout. Please try again.");
            isWaitingForSession = false;
            pendingUsername = null;
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
    
    private void HideLoadingOverlay()
    {
        Debug.Log("[LoginUI] HideLoadingOverlay");
        loadingOverlay?.AddToClassList("hidden");
    }
    
    public void ShowLoginPanel()
    {
        Debug.Log("[LoginUI] ShowLoginPanel called");
        
        // Make sure we have the root element
        if (root == null && uiDocument != null)
        {
            root = uiDocument.rootVisualElement;
            SetupElements();
            SetupEventHandlers();
        }
        
        if (authPanel != null)
        {
            authPanel.RemoveFromClassList("hidden");
            ShowLoginForm();
        }
        else
        {
            Debug.LogError("[LoginUI] authPanel is null! Re-initializing...");
            if (uiDocument != null && uiDocument.rootVisualElement != null)
            {
                root = uiDocument.rootVisualElement;
                SetupElements();
                SetupEventHandlers();
                
                // Try again
                if (authPanel != null)
                {
                    authPanel.RemoveFromClassList("hidden");
                    ShowLoginForm();
                }
            }
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
        ClearErrors();
        loginUsernameField?.Focus();
    }
    
    private void ShowRegisterForm()
    {
        loginForm?.AddToClassList("hidden");
        registerForm?.RemoveFromClassList("hidden");
        ClearErrors();
        registerUsernameField?.Focus();
    }
    
    private void ShowLoadingOverlay(string text = "Loading...")
    {
        Debug.Log($"[LoginUI] ShowLoadingOverlay: {text}");
        if (loadingOverlay != null)
        {
            loadingOverlay.RemoveFromClassList("hidden");
            if (loadingText != null)
            {
                loadingText.text = text;
            }
        }
    }
    
    public void HideLoading()
    {
        Debug.Log("[LoginUI] HideLoading called");
        HideLoadingOverlay();
    }
    
    public void ShowError(string message)
    {
        Debug.LogError($"[LoginUI] Error: {message}");
        if (messageLabel != null)
        {
            messageLabel.text = message;
            messageLabel.RemoveFromClassList("hidden");
            messageLabel.AddToClassList("error-text");
            messageLabel.RemoveFromClassList("success-text");
        }
    }
    
    public void ShowMessage(string message)
    {
        Debug.Log($"[LoginUI] Message: {message}");
        if (messageLabel != null)
        {
            messageLabel.text = message;
            messageLabel.RemoveFromClassList("hidden");
            messageLabel.RemoveFromClassList("error-text");
            messageLabel.AddToClassList("success-text");
        }
    }
    
    private void ClearErrors()
    {
        messageLabel?.AddToClassList("hidden");
    }
    
    public void UpdateLoadingText(string text)
    {
        if (loadingText != null)
        {
            loadingText.text = text;
        }
    }
    
    #endregion
    
    #region Connection Event Handlers
    
    private void HandleConnect()
    {
        Debug.Log("[LoginUI] Connected to server");
        
        // Always hide loading and show login when connected (unless we have a player)
        if (GameManager.GetLocalPlayer() == null)
        {
            HideLoadingOverlay();
            ShowLoginPanel();
        }
    }
    
    private void HandleConnectionError(string error)
    {
        Debug.LogError($"[LoginUI] Connection error: {error}");
        HideLoadingOverlay();
        ShowError($"Connection failed: {error}");
        ShowLoginPanel();
    }
    
    private void HandleDisconnect()
    {
        Debug.Log("[LoginUI] Disconnected from server");
        ShowError("Disconnected from server");
        ShowLoginPanel();
    }
    
    #endregion
    
    #region Session Management
    
    private void CheckSavedSession()
    {
        string savedToken = AuthToken.LoadSessionToken();
        if (!string.IsNullOrEmpty(savedToken))
        {
            ShowLoadingOverlay("Restoring session...");
            
            var conn = GameManager.Conn;
            if (conn != null)
            {
                conn.Reducers.RestoreSession(savedToken);
            }
        }
    }
    
    private string GenerateDeviceInfo()
    {
        return $"{SystemInfo.deviceModel}|{SystemInfo.operatingSystem}|{SystemInfo.deviceUniqueIdentifier}";
    }
    
    #endregion
    
    #region UI Actions
    
    private void HandleLogin()
    {
        string username = loginUsernameField?.value;
        string password = loginPasswordField?.value;
        string pin = loginPinField?.value;
        
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(pin))
        {
            ShowError("Please enter username and PIN");
            return;
        }
        
        ShowLoadingOverlay("Logging in...");
        
        // Use session login with PIN
        var conn = GameManager.Conn;
        conn?.Reducers.LoginWithSession(username, pin, deviceInfo);
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
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(displayName) || 
            string.IsNullOrEmpty(pin) || string.IsNullOrEmpty(confirmPin))
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
            ShowError("PIN must be exactly 4 digits");
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
            Debug.Log($"[LoginUI] Calling RegisterAccount with username: {username}, displayName: {displayName}");
            conn.Reducers.RegisterAccount(username, displayName, pin);
        }
        else
        {
            Debug.LogError("[LoginUI] Connection is null!");
            ShowError("Not connected to server");
        }
    }
    
    #endregion
}