using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using SpacetimeDB;
using SpacetimeDB.Types;

/// <summary>
/// Handles the login UI functionality including registration, login, and session management.
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
    private VisualElement messageLabel;
    
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
    
    // State tracking for session handling
    private bool isWaitingForSession = false;
    private string pendingUsername = null;
    
    #region Unity Lifecycle
    
    void Awake()
    {
        // Debug.Log("[LoginUI] LoginUIController Awake");
        
        // Register with GameManager
        GameManager.RegisterLoginUI(this);
        
        // Hide the password field if it exists (we only use PIN)
        if (loginPasswordField != null)
        {
            loginPasswordField.style.display = DisplayStyle.None;
        }
        
        // Subscribe to GameManager events
        GameManager.OnConnected += HandleConnect;
        GameManager.OnConnectionError += HandleConnectionError;
        GameManager.OnDisconnected += HandleDisconnect;
        GameManager.OnSubscriptionReady += HandleSubscriptionReady; // New subscription
        
        // Check if already connected
        if (GameManager.IsConnected())
        {
            // Debug.Log("[LoginUI] Already connected");
            HandleConnect();
            
            // If subscription is also ready, set up events
            if (GameManager.Conn?.Db != null)
            {
                HandleSubscriptionReady();
            }
        }
    }
    
    void OnEnable()
    {
        // Debug.Log("[LoginUI] OnEnable called");
        root = uiDocument.rootVisualElement;
        SetupElements();
        SetupEventHandlers();
    }
    
    void OnDestroy()
    {
        // Unsubscribe from GameManager events
        GameManager.OnConnected -= HandleConnect;
        GameManager.OnConnectionError -= HandleConnectionError;
        GameManager.OnDisconnected -= HandleDisconnect;
        GameManager.OnSubscriptionReady -= HandleSubscriptionReady;
        
        // Unsubscribe from SpacetimeDB events
        UnsubscribeFromSpacetimeDBEvents();
    }
    
    #endregion
    
    #region Setup
    
    private void SetupElements()
    {
        // Debug.Log("[LoginUI] SetupElements called");
        
        if (root == null)
        {
            Debug.LogError("[LoginUI] Root is null in SetupElements!");
            return;
        }
        
        // Panels
        authPanel = root.Q<VisualElement>("auth-panel");
        loginForm = root.Q<VisualElement>("login-form");
        registerForm = root.Q<VisualElement>("register-form");
        loadingOverlay = root.Q<VisualElement>("loading-overlay");
        
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
        
        // Debug.Log("[LoginUI] UI elements setup complete");
    }
    
    private void SetupEventHandlers()
    {
        // Debug.Log("[LoginUI] SetupEventHandlers called");
        
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
        
        // Debug.Log("[LoginUI] Event handlers setup complete");
    }
    
    private void SetupSpacetimeDBEvents()
    {
        var conn = GameManager.Conn;
        if (conn?.Db == null)
        {
            Debug.LogWarning("[LoginUI] Cannot setup SpacetimeDB events - connection or Db is null");
            return;
        }
        
        // Debug.Log("[LoginUI] Setting up SpacetimeDB events");
        
        // Reducer event handlers
        conn.Reducers.OnRegisterAccount += OnRegisterAccountResponse;
        conn.Reducers.OnLoginWithSession += OnLoginWithSessionResponse;
        conn.Reducers.OnRestoreSession += OnRestoreSessionResponse;
        conn.Reducers.OnLogout += OnLogoutResponse;
        
        // Table event handlers - SessionResult
        conn.Db.SessionResult.OnInsert += OnSessionResultInsert;
        conn.Db.SessionResult.OnUpdate += OnSessionResultUpdate;
        
        // Unhandled error handler
        conn.OnUnhandledReducerError += OnUnhandledReducerError;
        
        // Debug.Log("[LoginUI] SpacetimeDB events setup complete");
        
        // Check for any existing SessionResult that might have been missed
        CheckExistingSessionResult();
    }
    
    private void UnsubscribeFromSpacetimeDBEvents()
    {
        var conn = GameManager.Conn;
        if (conn != null)
        {
            conn.Reducers.OnRegisterAccount -= OnRegisterAccountResponse;
            conn.Reducers.OnLoginWithSession -= OnLoginWithSessionResponse;
            conn.Reducers.OnRestoreSession -= OnRestoreSessionResponse;
            conn.Reducers.OnLogout -= OnLogoutResponse;
            
            if (conn.Db != null)
            {
                conn.Db.SessionResult.OnInsert -= OnSessionResultInsert;
                conn.Db.SessionResult.OnUpdate -= OnSessionResultUpdate;
            }
            
            conn.OnUnhandledReducerError -= OnUnhandledReducerError;
        }
    }
    
    private void CheckExistingSessionResult()
    {
        if (!isWaitingForSession || GameManager.Conn?.Identity == null) return;
        
        var conn = GameManager.Conn;
        
        // Check if there's already a SessionResult for our identity
        var existingResult = conn.Db.SessionResult.Identity.Find(conn.Identity.Value);
        if (existingResult != null)
        {
            // Debug.Log("[LoginUI] Found existing SessionResult, processing it");
            ProcessSessionResult(existingResult);
        }
    }
    
    #endregion
    
    #region UI Actions
    
    private void HandleLogin()
    {
        // Debug.Log("[LoginUI] HandleLogin called");
        
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
            
            // Store username for session processing
            pendingUsername = username;
            isWaitingForSession = true;
            
            // Call the login reducer
            GameManager.LoginWithSession(username, pin);
        }
        else
        {
            HideLoadingOverlay();
            ShowError("Not connected to server");
        }
    }
    
    private void HandleRegister()
    {
        // Debug.Log("[LoginUI] HandleRegister called");
        
        string username = registerUsernameField?.value;
        string displayName = registerDisplayNameField?.value;
        string pin = registerPinField?.value;
        string confirmPin = registerConfirmPinField?.value;
        
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
        
        ShowLoadingOverlay("Creating account...");
        
        var conn = GameManager.Conn;
        conn?.Reducers.RegisterAccount(username, displayName, pin);
    }
    
    #endregion
    
    #region SpacetimeDB Event Handlers
    
    private void OnRegisterAccountResponse(ReducerEventContext ctx, string username, string displayName, string pin)
    {
        // Debug.Log($"[LoginUI] OnRegisterAccountResponse - Status: {ctx.Event.Status}");
        
        if (ctx.Event.Status is Status.Committed)
        {
            // Debug.Log("[LoginUI] Registration successful");
            
            HideLoadingOverlay();
            ShowLoginForm();
            SetLoginUsername(username);
            
            // Focus on PIN field since username is pre-filled
            loginPinField?.Focus();
            
            ShowMessage("Account created! Please login.");
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            Debug.LogError($"[LoginUI] Registration failed: {reason}");
            HideLoadingOverlay();
            ShowError(reason ?? "Registration failed");
        }
        else if (ctx.Event.Status is Status.OutOfEnergy)
        {
            Debug.LogError("[LoginUI] Registration failed: Out of energy");
            HideLoadingOverlay();
            ShowError("Registration failed: Out of energy");
        }
    }
    
    private void OnLoginWithSessionResponse(ReducerEventContext ctx, string username, string pin, string deviceInfo)
    {
        // Debug.Log($"[LoginUI] OnLoginWithSessionResponse - Status: {ctx.Event.Status}");
        
        if (ctx.Event.Status is Status.Committed)
        {
            // Debug.Log($"[LoginUI] Login committed for {username}");
            // Keep loading overlay - wait for SessionResult
            // The server will create a SessionResult which will trigger OnSessionResultInsert
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            Debug.LogError($"[LoginUI] Login failed: {reason}");
            HideLoadingOverlay();
            ShowError(reason ?? "Login failed");
            isWaitingForSession = false;
            pendingUsername = null;
        }
        else if (ctx.Event.Status is Status.OutOfEnergy)
        {
            Debug.LogError("[LoginUI] Login failed: Out of energy");
            HideLoadingOverlay();
            ShowError("Login failed: Out of energy");
            isWaitingForSession = false;
            pendingUsername = null;
        }
    }
    
    private void OnSessionResultInsert(EventContext ctx, SessionResult sessionResult)
    {
        // Debug.Log($"[LoginUI] SessionResult inserted for identity: {sessionResult.Identity}");
        
        var ourIdentity = GameManager.Conn?.Identity;
        
        if (isWaitingForSession && ourIdentity != null && sessionResult.Identity == ourIdentity)
        {
            // Debug.Log("[LoginUI] This is our session! Processing...");
            ProcessSessionResult(sessionResult);
        }
    }
    
    private void OnSessionResultUpdate(EventContext ctx, SessionResult oldResult, SessionResult newResult)
    {
        // Debug.Log($"[LoginUI] SessionResult updated for identity: {newResult.Identity}");
        
        var ourIdentity = GameManager.Conn?.Identity;
        
        if (isWaitingForSession && ourIdentity != null && newResult.Identity == ourIdentity)
        {
            ProcessSessionResult(newResult);
        }
    }
    
    private void ProcessSessionResult(SessionResult sessionResult)
    {
        isWaitingForSession = false;
        
        string username = pendingUsername ?? AuthToken.LoadUsername();
        
        if (string.IsNullOrEmpty(username))
        {
            Debug.LogError("[LoginUI] No username available for session!");
            ShowError("Session error. Please login again.");
            ShowLoginPanel();
            return;
        }
        
        // Save session
        AuthToken.SaveSession(sessionResult.SessionToken, username);
        
        HideLoadingOverlay();
        // Debug.Log($"[LoginUI] Login successful for {username}");
        
        // Update GameData
        GameData.Instance.SetUsername(username);
        
        // Check if we need to create a player
        StartCoroutine(CheckAndCreatePlayer(username));
        
        pendingUsername = null;
    }
    
    private IEnumerator CheckAndCreatePlayer(string username)
    {
        // Debug.Log($"[LoginUI] Checking for existing player: {username}");
        
        // Wait a frame to ensure all table data is synced
        yield return null;
        
        if (!GameManager.IsConnected())
        {
            Debug.LogError("[LoginUI] Lost connection during login");
            ShowError("Lost connection to server");
            yield break;
        }
        
        // Check if player already exists
        var existingPlayer = GameManager.GetLocalPlayer();
        if (existingPlayer != null)
        {
            // Debug.Log($"[LoginUI] Player already exists: {existingPlayer.Name}");
            // GameManager will handle the scene transition via OnPlayerInsert
        }
        else
        {
            // Debug.Log($"[LoginUI] No player found, creating new player: {username}");
            
            ShowLoadingOverlay("Creating character...");
            GameManager.CreatePlayer(username);
            
            // GameManager will handle the scene transition when player is created
        }
    }
    
    private void OnRestoreSessionResponse(ReducerEventContext ctx, string sessionToken)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            // Debug.Log("[LoginUI] Session restore successful");
            isWaitingForSession = true;
            // Wait for SessionResult
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            HideLoadingOverlay();
            
            if (reason != null && reason.Contains("expired"))
            {
                ShowError("Session expired. Please login again.");
                AuthToken.ClearSession();
            }
            else
            {
                ShowError(reason ?? "Failed to restore session");
            }
            
            ShowLoginPanel();
        }
    }
    
    private void OnLogoutResponse(ReducerEventContext ctx)
    {
        HideLoadingOverlay();
        ShowMessage("Logged out successfully");
        ShowLoginPanel();
    }
    
    private void OnUnhandledReducerError(ReducerEventContext ctx, Exception error)
    {
        Debug.LogError($"[LoginUI] Unhandled reducer error: {error.Message}");
        HideLoadingOverlay();
        ShowError($"Error: {error.Message}");
    }
    
    #endregion
    
    #region Connection Event Handlers
    
    private void HandleConnect()
    {
        // Debug.Log("[LoginUI] HandleConnect called");
        
        HideLoadingOverlay();
        
        // Don't setup events here - wait for subscription ready
    }
    
    private void HandleSubscriptionReady()
    {
        // Debug.Log("[LoginUI] HandleSubscriptionReady called");
        
        // Now it's safe to setup SpacetimeDB events
        SetupSpacetimeDBEvents();
        
        // Show login panel if we don't have a player
        if (GameManager.GetLocalPlayer() == null)
        {
            ShowLoginPanel();
        }
    }
    
    private void HandleConnectionError(string error)
    {
        Debug.LogError($"[LoginUI] Connection error: {error}");
        HideLoadingOverlay();
        ShowError("Connection failed. Please check your internet connection.");
    }
    
    private void HandleDisconnect()
    {
        // Debug.Log("[LoginUI] Disconnected from server");
        HideLoadingOverlay();
        ShowError("Lost connection to server");
    }
    
    #endregion
    
    #region UI State Management
    
    public void HideLoading()
    {
        HideLoadingOverlay();
    }
    
    private void ShowLoadingOverlay(string text = "Loading...")
    {
        if (loadingOverlay != null)
        {
            loadingOverlay.RemoveFromClassList("hidden");
            if (loadingText != null)
                loadingText.text = text;
        }
    }
    
    private void HideLoadingOverlay()
    {
        // Debug.Log("[LoginUI] HideLoadingOverlay");
        loadingOverlay?.AddToClassList("hidden");
    }
    
    public void ShowLoginPanel()
    {
        // Debug.Log("[LoginUI] ShowLoginPanel called");
        
        HideLoadingOverlay();
        
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
            Debug.LogError("[LoginUI] authPanel is null!");
        }
    }
    
    public void ShowLoginForm()
    {
        // Debug.Log("[LoginUI] ShowLoginForm");
        loginForm?.RemoveFromClassList("hidden");
        registerForm?.AddToClassList("hidden");
        ClearMessages();
    }
    
    public void ShowRegisterForm()
    {
        // Debug.Log("[LoginUI] ShowRegisterForm");
        loginForm?.AddToClassList("hidden");
        registerForm?.RemoveFromClassList("hidden");
        ClearMessages();
    }
    
    private void SetLoginUsername(string username)
    {
        if (loginUsernameField != null)
        {
            loginUsernameField.value = username;
        }
    }
    
    public void ShowErrorMessage(string message)
    {
        ShowError(message);
    }
    
    private void ShowError(string message)
    {
        Debug.LogError($"[LoginUI] Error: {message}");
        
        // Find error label in the currently visible form
        Label errorLabel = null;
        
        if (loginForm != null && !loginForm.ClassListContains("hidden"))
        {
            errorLabel = loginForm.Q<Label>("error-text");
        }
        else if (registerForm != null && !registerForm.ClassListContains("hidden"))
        {
            errorLabel = registerForm.Q<Label>("error-text");
        }
        
        if (errorLabel != null)
        {
            errorLabel.text = message;
            errorLabel.RemoveFromClassList("hidden");
            errorLabel.style.color = Color.red;
        }
    }
    
    private void ShowMessage(string message)
    {
        // Debug.Log($"[LoginUI] Message: {message}");
        
        // Find message label in the currently visible form
        Label messageLabel = null;
        
        if (loginForm != null && !loginForm.ClassListContains("hidden"))
        {
            messageLabel = loginForm.Q<Label>("error-text");
        }
        else if (registerForm != null && !registerForm.ClassListContains("hidden"))
        {
            messageLabel = registerForm.Q<Label>("error-text");
        }
        
        if (messageLabel != null)
        {
            messageLabel.text = message;
            messageLabel.RemoveFromClassList("hidden");
            messageLabel.style.color = Color.green;
        }
    }
    
    private void ClearMessages()
    {
        // Clear error messages in both forms
        var loginError = loginForm?.Q<Label>("error-text");
        if (loginError != null)
        {
            loginError.text = "";
            loginError.AddToClassList("hidden");
        }
        
        var registerError = registerForm?.Q<Label>("error-text");
        if (registerError != null)
        {
            registerError.text = "";
            registerError.AddToClassList("hidden");
        }
    }
    
    #endregion
}