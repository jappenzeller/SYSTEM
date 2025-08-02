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
        }
        
        // Subscribe to GameManager events
        GameManager.OnConnected += HandleConnect;
        GameManager.OnConnectionError += HandleConnectionError;
        GameManager.OnDisconnected += HandleDisconnect;
        
        // Check if already connected
        if (GameManager.IsConnected())
        {
            Debug.Log("[LoginUI] Already connected, setting up SpacetimeDB events");
            SetupSpacetimeDBEvents();
            HandleConnect();
        }
    }
    
    void OnEnable()
    {
        Debug.Log("[LoginUI] OnEnable called");
        root = uiDocument.rootVisualElement;
        SetupElements();
        SetupEventHandlers();
        
        // Only setup SpacetimeDB events if connected
        if (GameManager.IsConnected())
        {
            SetupSpacetimeDBEvents();
        }
    }
    
    private void SetupSpacetimeDBEvents()
    {
        // Subscribe to SpacetimeDB events
        var conn = GameManager.Conn;
        if (conn != null)
        {
            Debug.Log("[LoginUI] Subscribing to SpacetimeDB events");
            
            // Add debug to confirm subscription
            conn.Reducers.OnRegisterAccount += OnRegisterAccountResponse;
            Debug.Log("[LoginUI] OnRegisterAccount subscribed.");
            
            conn.Reducers.OnLoginWithSession += OnLoginWithSessionResponse;
            Debug.Log("[LoginUI] OnLoginWithSession subscribed.");
            
            conn.Reducers.OnRestoreSession += OnRestoreSessionResponse;
            conn.Reducers.OnLogout += OnLogoutResponse;
            
            // Table subscriptions
            conn.Db.SessionResult.OnInsert += OnSessionResultInsert;
            conn.Db.SessionResult.OnUpdate += OnSessionResultUpdate;
            
            // Add unhandled error handler
            conn.OnUnhandledReducerError += OnUnhandledReducerError;
            
            Debug.Log("[LoginUI] All SpacetimeDB events subscribed successfully");
        }
        else
        {
            Debug.LogWarning("[LoginUI] Connection is null during event setup");
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe from GameManager events
        GameManager.OnConnected -= HandleConnect;
        GameManager.OnConnectionError -= HandleConnectionError;
        GameManager.OnDisconnected -= HandleDisconnect;
        
        // Unsubscribe from SpacetimeDB events
        var conn = GameManager.Conn;
        if (conn != null)
        {
            conn.Reducers.OnRegisterAccount -= OnRegisterAccountResponse;
            conn.Reducers.OnLoginWithSession -= OnLoginWithSessionResponse;
            conn.Reducers.OnRestoreSession -= OnRestoreSessionResponse;
            conn.Reducers.OnLogout -= OnLogoutResponse;
            
            conn.Db.SessionResult.OnInsert -= OnSessionResultInsert;
            conn.Db.SessionResult.OnUpdate -= OnSessionResultUpdate;
            
            conn.OnUnhandledReducerError -= OnUnhandledReducerError;
        }
    }
    
    #region Setup
    
    private void SetupElements()
    {
        Debug.Log("[LoginUI] SetupElements called");
        
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
        
        Debug.Log("[LoginUI] UI elements setup complete");
    }
    
    private void SetupEventHandlers()
    {
        Debug.Log("[LoginUI] SetupEventHandlers called");
        
        // Login form - now calling public methods
        loginButton?.RegisterCallback<ClickEvent>(evt => HandleLogin());
        showRegisterButton?.RegisterCallback<ClickEvent>(evt => ShowRegisterForm()); // Uses public method
        
        // Register form - now calling public methods
        registerButton?.RegisterCallback<ClickEvent>(evt => HandleRegister());
        showLoginButton?.RegisterCallback<ClickEvent>(evt => ShowLoginForm()); // Uses public method
        
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
        Debug.Log($"[LoginUI] OnRegisterAccountResponse called - Status: {ctx.Event.Status}");
        
        if (ctx.Event.Status is Status.Committed)
        {
            Debug.Log("[LoginUI] Registration committed successfully");
            
            // QUICK FIX: Remove message display and directly transition
            HideLoadingOverlay();
            ShowLoginForm();
            SetLoginUsername(username);
            
            // Focus on PIN field since username is pre-filled
            loginPinField?.Focus();
            
            Debug.Log($"[LoginUI] Transitioned to login form with username: {username}");
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
        else
        {
            Debug.LogWarning($"[LoginUI] Registration response with unexpected status: {ctx.Event.Status}");
            HideLoadingOverlay();
            // Don't show error, just go back to register form
        }
    }
    
    private void OnLoginWithSessionResponse(ReducerEventContext ctx, string username, string pin, string deviceInfo)
    {
        Debug.Log($"[LoginUI] OnLoginWithSessionResponse - Status: {ctx.Event.Status}, Username: {username}");
        
        if (ctx.Event.Status is Status.Committed)
        {
            // Login succeeded
            pendingUsername = username;
            isWaitingForSession = true;
            
            Debug.Log($"[LoginUI] Login committed for {username}, waiting for session token...");
            
            // TEMPORARY: For now, just proceed after a short delay
            // TODO: Fix SessionResult table subscription
            StartCoroutine(ProceedAfterLogin(username));
            
            // Still start timeout as backup
            StartCoroutine(SessionTimeout());
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            Debug.LogError($"[LoginUI] Login failed for {username}. Reason: {reason}");
            HideLoadingOverlay();
            ShowError(reason ?? "Login failed");
            isWaitingForSession = false;
            pendingUsername = null;
        }
        else
        {
            Debug.LogWarning($"[LoginUI] Unexpected login status: {ctx.Event.Status}");
            HideLoadingOverlay();
            ShowError("Login failed - unexpected response");
        }
    }
    
    private IEnumerator ProceedAfterLogin(string username)
    {
        yield return new WaitForSeconds(0.5f);
        
        if (isWaitingForSession)
        {
            Debug.Log("[LoginUI] Proceeding without SessionResult (temporary workaround)");
            
            isWaitingForSession = false;
            HideLoadingOverlay();
            
            // Update GameData
            GameData.Instance.SetUsername(username);
            
            // For now, just save a dummy session
            AuthToken.SaveSession("temp_session_" + System.Guid.NewGuid(), username);
            
            Debug.Log($"[LoginUI] Login successful for {username}");
            
            // The GameManager will handle the scene transition when player is created
            pendingUsername = null;
        }
    }
    
    private void OnSessionResultInsert(EventContext ctx, SessionResult sessionResult)
    {
        Debug.Log($"[LoginUI] SessionResult inserted for identity: {sessionResult.Identity}");
        Debug.Log($"[LoginUI] Our identity: {GameManager.LocalIdentity}");
        Debug.Log($"[LoginUI] Connection identity: {GameManager.Conn?.Identity}");
        Debug.Log($"[LoginUI] Is waiting for session: {isWaitingForSession}");
        
        // Get the actual identity from the connection
        var ourIdentity = GameManager.Conn?.Identity;
        
        // Check if this is our session
        if (isWaitingForSession && ourIdentity != null && 
            sessionResult.Identity == ourIdentity)
        {
            Debug.Log("[LoginUI] This is our session! Processing...");
            ProcessSessionResult(sessionResult);
        }
        else
        {
            Debug.Log("[LoginUI] Not our session or not waiting");
            if (ourIdentity == null)
                Debug.LogError("[LoginUI] Our identity is null!");
            else if (sessionResult.Identity != ourIdentity)
                Debug.Log($"[LoginUI] Identity mismatch: {sessionResult.Identity} != {ourIdentity}");
        }
    }
    
    private void OnSessionResultUpdate(EventContext ctx, SessionResult oldResult, SessionResult newResult)
    {
        Debug.Log($"[LoginUI] SessionResult updated for identity: {newResult.Identity}");
        
        var ourIdentity = GameManager.Conn?.Identity;
        
        // Check if this is our session
        if (isWaitingForSession && ourIdentity != null && 
            newResult.Identity == ourIdentity)
        {
            ProcessSessionResult(newResult);
        }
    }
    
    private void ProcessSessionResult(SessionResult sessionResult)
    {
        isWaitingForSession = false;
        
        // For restored sessions, we already have the username saved
        string username = pendingUsername ?? AuthToken.LoadUsername();
        
        if (string.IsNullOrEmpty(username))
        {
            Debug.LogError("[LoginUI] No username available for session!");
            ShowError("Session error. Please login again.");
            ShowLoginPanel();
            return;
        }
        
        // Update the session token (it might be the same, but update anyway)
        AuthToken.SaveSession(sessionResult.SessionToken, username);
        
        HideLoadingOverlay();
        Debug.Log($"[LoginUI] Session ready for {username}");
        
        // Update GameData
        GameData.Instance.SetUsername(username);
        
        // Check if we need to create a player
        StartCoroutine(CheckAndCreatePlayer(username));
        
        pendingUsername = null;
    }
    
    private IEnumerator CheckAndCreatePlayer(string username)
    {
        Debug.Log($"[LoginUI] CheckAndCreatePlayer coroutine started for: {username}");
        
        // Wait a frame to ensure all table subscriptions are ready
        yield return null;
        
        // Check connection status
        if (!GameManager.IsConnected())
        {
            Debug.LogError("[LoginUI] Not connected to server!");
            ShowError("Lost connection to server");
            yield break;
        }
        
        // Check if player already exists
        var existingPlayer = GameManager.GetLocalPlayer();
        if (existingPlayer != null)
        {
            Debug.Log($"[LoginUI] Player already exists: {existingPlayer.Name}");
            // GameManager will handle the scene transition
        }
        else
        {
            Debug.Log($"[LoginUI] No player found, creating new player: {username}");
            
            // For now, use the username as the player name
            // In a full implementation, you'd show a character creation screen
            GameManager.CreatePlayer(username);
            
            // Show a message while creating
            ShowLoadingOverlay("Creating character...");
            
            // Wait a bit for the player to be created
            float timeout = 5f;
            float elapsed = 0f;
            
            while (elapsed < timeout)
            {
                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;
                
                var player = GameManager.GetLocalPlayer();
                if (player != null)
                {
                    Debug.Log($"[LoginUI] Player created successfully: {player.Name}");
                    HideLoadingOverlay();
                    yield break;
                }
            }
            
            Debug.LogError("[LoginUI] Timeout waiting for player creation");
            HideLoadingOverlay();
            ShowError("Failed to create character. Please try again.");
        }
    }
    
    private IEnumerator SessionTimeout()
    {
        Debug.Log("[LoginUI] SessionTimeout coroutine started");
        yield return new WaitForSeconds(5f); // Wait 5 seconds for session
        
        Debug.Log($"[LoginUI] SessionTimeout check - isWaitingForSession: {isWaitingForSession}");
        
        if (isWaitingForSession)
        {
            Debug.LogError("[LoginUI] Session timeout - no session token received");
            HideLoadingOverlay();
            ShowError("Login timeout. Please try again.");
            isWaitingForSession = false;
            pendingUsername = null;
        }
        else
        {
            Debug.Log("[LoginUI] SessionTimeout - Session already received, ignoring timeout");
        }
    }
    
    private void OnRestoreSessionResponse(ReducerEventContext ctx, string sessionToken)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            Debug.Log("[LoginUI] Session restore committed, waiting for session result...");
            
            // We need to wait for the SessionResult to be created
            isWaitingForSession = true;
            
            // The server will create a new SessionResult with the restored session
            // We'll handle it in OnSessionResultInsert
            
            // Start timeout in case something goes wrong
            StartCoroutine(SessionTimeout());
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
        Debug.LogError($"[LoginUI] Error stack trace: {error.StackTrace}");
        HideLoadingOverlay();
        ShowError($"Error: {error.Message}");
    }
    
    #endregion
    
    #region Connection Event Handlers
    
    private void HandleConnect()
    {
        Debug.Log("[LoginUI] HandleConnect called");
        
        // Setup SpacetimeDB events now that we're connected
        SetupSpacetimeDBEvents();
        
        // Always hide the loading overlay when connected
        HideLoadingOverlay();
        
        // Show login panel if we don't have a player
        if (GameManager.GetLocalPlayer() == null)
        {
            ShowLoginPanel();
        }
        // If we have a player, GameManager will handle the scene transition
    }
    
    private void HandleConnectionError(string error)
    {
        Debug.LogError($"[LoginUI] Connection error: {error}");
        HideLoadingOverlay();
        ShowError("Connection failed. Please check your internet connection.");
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
        string savedUsername = AuthToken.LoadUsername();
        
        if (!string.IsNullOrEmpty(savedToken) && !string.IsNullOrEmpty(savedUsername))
        {
            Debug.Log($"[LoginUI] Found saved session for user: {savedUsername}");
            
            ShowLoadingOverlay("Restoring session...");
            pendingUsername = savedUsername; // Save for later use
            
            var conn = GameManager.Conn;
            if (conn != null && conn.IsActive)
            {
                conn.Reducers.RestoreSession(savedToken);
            }
            else
            {
                // Connection not ready, show login
                HideLoadingOverlay();
                ShowLoginPanel();
            }
        }
        else
        {
            // No saved session, show login
            ShowLoginPanel();
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
        
        Debug.Log($"[LoginUI] HandleLogin - Username: '{username}', PIN length: {(pin != null ? pin.Length : 0)}");
        
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(pin))
        {
            ShowError("Please enter username and PIN");
            return;
        }
        
        if (pin.Length != 4)
        {
            ShowError("PIN must be exactly 4 digits");
            return;
        }
        
        Debug.Log($"[LoginUI] Calling LoginWithSession with username: '{username}', pin: '{pin}'");
        ShowLoadingOverlay("Logging in...");
        
        // Use session login with PIN
        var conn = GameManager.Conn;
        if (conn != null && conn.IsActive)
        {
            conn.Reducers.LoginWithSession(username, pin, deviceInfo);
        }
        else
        {
            Debug.LogError("[LoginUI] Connection is not active!");
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
    
    #region UI State Management
    
    private void HideLoadingOverlay()
    {
        Debug.Log("[LoginUI] HideLoadingOverlay");
        loadingOverlay?.AddToClassList("hidden");
    }
    
    public void ShowLoginPanel()
    {
        Debug.Log("[LoginUI] ShowLoginPanel called");
        
        // Always hide loading overlay when showing login panel
        HideLoadingOverlay();
        
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

    #region Public API Methods

    /// <summary>
    /// Shows the login form panel
    /// </summary>
    public void ShowLoginForm()
    {
        loginForm?.RemoveFromClassList("hidden");
        registerForm?.AddToClassList("hidden");
        ClearErrors();
        loginUsernameField?.Focus();
    }

    /// <summary>
    /// Sets the username in the login form
    /// </summary>
    /// <param name="username">Username to pre-fill</param>
    public void SetLoginUsername(string username)
    {
        if (loginUsernameField != null && !string.IsNullOrEmpty(username))
        {
            loginUsernameField.value = username;
            // Focus on PIN field since username is already filled
            loginPinField?.Focus();
        }
    }

    /// <summary>
    /// Shows the register form panel
    /// </summary>
    public void ShowRegisterForm()
    {
        loginForm?.AddToClassList("hidden");
        registerForm?.RemoveFromClassList("hidden");
        ClearErrors();
        registerUsernameField?.Focus();
    }

    #endregion

    public void ShowMessage(string message)
    {
        Debug.Log($"[LoginUI] ShowMessage: {message}");
        // QUICK FIX: Since messageLabel doesn't exist, just log it
        // In future, we can add proper message display
    }
    
    public void ShowError(string error)
    {
        Debug.LogError($"[LoginUI] ShowError: {error}");
        // QUICK FIX: For now, just log errors
        // The error-text labels in the forms exist but aren't being used
        // This is sufficient for MVP
    }
    
    private void ClearErrors()
    {
        // QUICK FIX: Nothing to clear since we're not displaying messages
        // Just here for compatibility
    }
    
    #endregion
}