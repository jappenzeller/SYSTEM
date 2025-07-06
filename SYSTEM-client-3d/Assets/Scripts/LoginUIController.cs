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
    private TextField registerPasswordField;
    private TextField registerPinField;
    private Button registerButton;
    private Button showLoginButton;
    
    // Loading Elements
    private Label loadingText;
    private Label messageLabel;
    
    // Device Info
    private string deviceInfo;
    
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
        
        // Add a fallback check after a delay
        StartCoroutine(CheckConnectionAfterDelay());
    }
    
    private IEnumerator CheckConnectionAfterDelay()
    {
        yield return new WaitForSeconds(3f);
        
        // If we're connected but still showing loading, force show login
        if (GameManager.IsConnected() && GameManager.GetLocalPlayer() == null)
        {
            Debug.Log("[LoginUI] Fallback: Connected but no player, showing login");
            ForceShowLogin();
        }
    }
    
    [ContextMenu("Force Show Login")]
    public void ForceShowLogin()
    {
        Debug.Log("[LoginUI] ForceShowLogin called");
        
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }
        
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            root = uiDocument.rootVisualElement;
            
            // Hide loading
            var overlay = root.Q<VisualElement>("loading-overlay");
            if (overlay != null)
            {
                overlay.AddToClassList("hidden");
                Debug.Log("[LoginUI] Loading overlay hidden");
            }
            
            // Show auth panel
            var authPanel = root.Q<VisualElement>("auth-panel");
            if (authPanel != null)
            {
                authPanel.RemoveFromClassList("hidden");
                Debug.Log("[LoginUI] Auth panel shown");
            }
            
            // Setup elements if not already done
            if (loginUsernameField == null)
            {
                SetupElements();
                SetupEventHandlers();
            }
            
            // Subscribe to SpacetimeDB events if not already done
            var conn = GameManager.Conn;
            if (conn != null)
            {
                conn.Reducers.OnRegisterAccount -= OnRegisterAccountResponse;
                conn.Reducers.OnRegisterAccount += OnRegisterAccountResponse;
            }
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
        }
        
        // Check for saved session
        CheckSavedSession();
    }
    
    void OnDisable()
    {
        // Unsubscribe from events
        var conn = GameManager.Conn;
        if (conn != null)
        {
            conn.Reducers.OnRegisterAccount -= OnRegisterAccountResponse;
            conn.Reducers.OnLoginWithSession -= OnLoginWithSessionResponse;
            conn.Reducers.OnRestoreSession -= OnRestoreSessionResponse;
            conn.Reducers.OnLogout -= OnLogoutResponse;
        }
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
        registerPasswordField = root.Q<TextField>("register-password");
        registerPinField = root.Q<TextField>("register-pin");
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
        loginPasswordField?.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                HandleLogin();
        });
        
        loginPinField?.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                HandleLogin();
        });
        
        registerPinField?.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                HandleRegister();
        });
    }
    
    #endregion
    
    #region Session Management
    
    private void CheckSavedSession()
    {
        string savedToken = AuthToken.LoadToken();
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
        
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowError("Please enter username and password");
            return;
        }
        
        ShowLoadingOverlay("Logging in...");
        
        // First try password login
        OnLoginRequested?.Invoke(username, password);
        
        // If PIN is provided, we'll use session login instead
        if (!string.IsNullOrEmpty(pin))
        {
            var conn = GameManager.Conn;
            conn?.Reducers.LoginWithSession(username, pin, deviceInfo);
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
        
        if (string.IsNullOrEmpty(username))
        {
            ShowError("Please enter a username");
            return;
        }
        
        if (string.IsNullOrEmpty(pin) || pin.Length != 4)
        {
            ShowError("Please enter a 4-digit PIN");
            return;
        }
        
        if (pin != confirmPin)
        {
            ShowError("PINs do not match");
            return;
        }
        
        ShowLoadingOverlay("Creating account...");
        
        // The RegisterAccount reducer expects (username, password)
        // We'll use the PIN as the password for now
        var conn = GameManager.Conn;
        if (conn != null)
        {
            Debug.Log("[LoginUI] Calling RegisterAccount reducer");
            conn.Reducers.RegisterAccount(username, pin); // Using PIN as password
        }
        else
        {
            Debug.LogError("[LoginUI] No connection to call RegisterAccount");
            ShowError("Not connected to server");
        }
    }
    
    #endregion
    
    #region UI Updates
    
    public void ShowLoading(string message)
    {
        Debug.Log($"[LoginUI] ShowLoading called with message: {message}");
        ShowLoadingOverlay(message);
    }
    
    public void HideLoading()
    {
        HideLoadingOverlay();
    }
    
    public void UpdateLoadingText(string text)
    {
        if (loadingText != null)
        {
            loadingText.text = text;
        }
    }
    
    public void ShowError(string error)
    {
        HideLoadingOverlay();
        ShowMessage(error, true);
    }
    
    public void ShowMessage(string message, bool isError = false)
    {
        if (messageLabel != null)
        {
            messageLabel.text = message;
            messageLabel.RemoveFromClassList("success");
            messageLabel.RemoveFromClassList("error");
            messageLabel.AddToClassList(isError ? "error" : "success");
            messageLabel.RemoveFromClassList("hidden");
            
            // Hide message after 3 seconds
            StartCoroutine(HideMessageAfterDelay(3f));
        }
    }
    
    private IEnumerator HideMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        messageLabel?.AddToClassList("hidden");
    }
    
    #endregion
    
    #region SpacetimeDB Event Handlers
    
    private void OnRegisterAccountResponse(ReducerEventContext ctx, string username, string displayName, string pin)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            HideLoadingOverlay();
            ShowMessage("Account created successfully! Please login.");
            
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
            // Login succeeded - session token should be in the event data
            // For now, we'll generate a placeholder token
            string sessionToken = $"session_{username}_{System.DateTime.Now.Ticks}";
            
            // Save session
            AuthToken.SaveSession(sessionToken, username);
            
            HideLoadingOverlay();
            Debug.Log($"[LoginUI] Login successful for {username}");
            
            // The GameManager will handle the scene transition when player is created
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
            Debug.LogError("[LoginUI] authPanel is null! Re-initializing UI...");
            if (uiDocument != null && uiDocument.rootVisualElement != null)
            {
                root = uiDocument.rootVisualElement;
                SetupElements();
                // Try again
                if (authPanel != null)
                {
                    authPanel.RemoveFromClassList("hidden");
                    ShowLoginForm();
                }
            }
        }
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
            loadingOverlay.RemoveFromClassList("hidden");
            loadingText.text = message;
        }
    }
    
    private void HideLoadingOverlay()
    {
        loadingOverlay?.AddToClassList("hidden");
    }
    
    #endregion
}