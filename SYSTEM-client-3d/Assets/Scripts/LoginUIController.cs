using UnityEngine;
using UnityEngine.UIElements;
using SpacetimeDB.Types;
using SpacetimeDB;
using System.Collections;

public class LoginUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument uiDocument;
    
    // UI Element References
    private VisualElement root;
    private VisualElement authPanel;
    private VisualElement characterPanel;
    private VisualElement loadingOverlay;
    private VisualElement retryPanel;
    
    // Forms
    private VisualElement loginForm;
    private VisualElement registerForm;
    
    // Input fields
    private TextField loginUsernameField;
    private TextField loginPinField;
    private TextField registerUsernameField;
    private TextField registerDisplayNameField;
    private TextField registerPinField;
    private TextField registerConfirmPinField;
    private TextField characterNameField;
    
    // Buttons
    private Button loginButton;
    private Button registerButton;
    private Button createCharacterButton;
    private Button showRegisterButton;
    private Button showLoginButton;
    private Button retryConnectionButton;
    private Button logoutButton;
    
    // Text elements
    private Label errorText;
    private Label loadingText;
    
    private void Start()
    {
        SetupUI();
        SetupEventHandlers();
        SetupReducerHandlers();
        
        // Check for saved session
        CheckForSavedSession();
    }
    
    private void SetupUI()
    {
        if (uiDocument == null)
        {
            Debug.LogError("[LoginUI] UIDocument is not assigned!");
            return;
        }
        
        root = uiDocument.rootVisualElement;
        
        // Panels
        authPanel = root.Q<VisualElement>("auth-panel");
        loginForm = root.Q<VisualElement>("login-form");
        registerForm = root.Q<VisualElement>("register-form");
        characterPanel = root.Q<VisualElement>("character-panel");
        loadingOverlay = root.Q<VisualElement>("loading-overlay");
        retryPanel = root.Q<VisualElement>("retry-panel");
        
        // Login form
        loginUsernameField = root.Q<TextField>("login-username");
        loginPinField = root.Q<TextField>("login-pin");
        
        // Register form  
        registerUsernameField = root.Q<TextField>("register-username");
        registerDisplayNameField = root.Q<TextField>("register-display-name");
        registerPinField = root.Q<TextField>("register-pin");
        registerConfirmPinField = root.Q<TextField>("register-confirm-pin");
        
        // Character creation
        characterNameField = root.Q<TextField>("character-name");
        
        // Buttons
        loginButton = root.Q<Button>("login-button");
        registerButton = root.Q<Button>("register-button");
        createCharacterButton = root.Q<Button>("create-character-button");
        showRegisterButton = root.Q<Button>("show-register-button");
        showLoginButton = root.Q<Button>("show-login-button");
        retryConnectionButton = root.Q<Button>("retry-connection-button");
        logoutButton = root.Q<Button>("logout-button");
        
        // Text elements
        errorText = root.Q<Label>("error-text");
        loadingText = root.Q<Label>("loading-text");
        
        // Set PIN fields to password mode
        if (loginPinField != null) loginPinField.isPasswordField = true;
        if (registerPinField != null) registerPinField.isPasswordField = true;
        if (registerConfirmPinField != null) registerConfirmPinField.isPasswordField = true;
    }
    
    private void SetupEventHandlers()
    {
        // Button click events
        loginButton?.RegisterCallback<ClickEvent>(evt => HandleLogin());
        registerButton?.RegisterCallback<ClickEvent>(evt => HandleRegister());
        showRegisterButton?.RegisterCallback<ClickEvent>(evt => ShowRegisterForm());
        showLoginButton?.RegisterCallback<ClickEvent>(evt => ShowLoginForm());
        retryConnectionButton?.RegisterCallback<ClickEvent>(evt => HandleRetryConnection());
        logoutButton?.RegisterCallback<ClickEvent>(evt => HandleLogout());
        
        // Enter key support
        loginPinField?.RegisterCallback<KeyDownEvent>(evt => {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                HandleLogin();
        });
        
        registerConfirmPinField?.RegisterCallback<KeyDownEvent>(evt => {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                HandleRegister();
        });
    }
    
    private void SetupReducerHandlers()
    {
        if (!GameManager.IsConnected()) return;
        
        var conn = GameManager.Conn;
        
        // Handle registration response
        conn.Reducers.OnRegisterAccount += OnRegisterResponse;
        
        // Handle login with session response
        conn.Reducers.OnLoginWithSession += OnLoginWithSessionResponse;
        
        // Handle session restoration response
        conn.Reducers.OnRestoreSession += OnRestoreSessionResponse;
        
        // Handle logout response
        conn.Reducers.OnLogout += OnLogoutResponse;
    }
    
    private void CheckForSavedSession()
    {
        // Auto-fill last username if available
        string lastUsername = AuthToken.LoadLastUsername();
        if (!string.IsNullOrEmpty(lastUsername) && loginUsernameField != null)
        {
            loginUsernameField.value = lastUsername;
        }
        
        // Check if we have a saved session
        if (AuthToken.HasSession() && !string.IsNullOrEmpty(lastUsername))
        {
            Debug.Log($"[LoginUI] Found saved session for {lastUsername}");
            ShowMessage($"Welcome back, {lastUsername}!");
            
            // Attempt automatic session restoration
            StartCoroutine(TryRestoreSession());
        }
        else
        {
            ShowLoginPanel();
        }
    }
    
    private IEnumerator TryRestoreSession()
    {
        ShowLoadingOverlay("Restoring session...");
        
        // Wait for connection to be established
        yield return new WaitUntil(() => GameManager.IsConnected());
        
        string sessionToken = AuthToken.LoadSession();
        if (!string.IsNullOrEmpty(sessionToken))
        {
            Debug.Log("[LoginUI] Attempting to restore session");
            GameManager.Instance.RestoreSession(sessionToken);
            
            // Wait for response (handled in OnRestoreSessionResponse)
            yield return new WaitForSeconds(5f); // Timeout after 5 seconds
            
            // If we're still showing loading, the restore likely failed
            if (loadingOverlay != null && !loadingOverlay.ClassListContains("hidden"))
            {
                HideLoadingOverlay();
                ShowError("Session expired. Please login again.");
                ShowLoginPanel();
            }
        }
        else
        {
            HideLoadingOverlay();
            ShowLoginPanel();
        }
    }
    
    private void HandleLogin()
    {
        ClearError();
        
        string username = loginUsernameField?.value.Trim() ?? "";
        string pin = loginPinField?.value.Trim() ?? "";
        
        // Validation
        if (string.IsNullOrEmpty(username))
        {
            ShowError("Please enter your username");
            return;
        }
        
        if (pin.Length != 4 || !System.Text.RegularExpressions.Regex.IsMatch(pin, @"^\d{4}$"))
        {
            ShowError("PIN must be exactly 4 digits");
            return;
        }
        
        ShowLoadingOverlay("Logging in...");
        
        // Store username for GameData
        GameData.Instance.SetUsername(username);
        
        // Call login through GameManager
        GameManager.Instance.Login(username, pin);
    }
    
    private void HandleRegister()
    {
        ClearError();
        
        string username = registerUsernameField?.value.Trim() ?? "";
        string displayName = registerDisplayNameField?.value.Trim() ?? "";
        string pin = registerPinField?.value.Trim() ?? "";
        string confirmPin = registerConfirmPinField?.value.Trim() ?? "";
        
        // Validation
        if (string.IsNullOrEmpty(username) || username.Length < 3 || username.Length > 20)
        {
            ShowError("Username must be 3-20 characters");
            return;
        }
        
        if (string.IsNullOrEmpty(displayName) || displayName.Length < 3 || displayName.Length > 20)
        {
            ShowError("Display name must be 3-20 characters");
            return;
        }
        
        if (pin.Length != 4 || !System.Text.RegularExpressions.Regex.IsMatch(pin, @"^\d{4}$"))
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
        
        // Call register through GameManager
        GameManager.Instance.Register(username, displayName, pin);
    }
    
    private void HandleRetryConnection()
    {
        ShowLoadingOverlay("Reconnecting...");
        GameManager.Instance.Connect();
    }
    
    private void HandleLogout()
    {
        ShowLoadingOverlay("Logging out...");
        GameManager.Instance.Logout();
    }
    
    #region Reducer Response Handlers
    
    private void OnRegisterResponse(ReducerEventContext ctx, string username, string displayName, string pin)
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
        else if (ctx.Event.Status is Status.Failed failed)
        {
            HideLoadingOverlay();
            ShowError(failed.Message ?? "Registration failed");
        }
    }
    
    private void OnLoginWithSessionResponse(ReducerEventContext ctx, string username, string pin, string deviceInfo)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            // Login succeeded - session token will be saved via SessionResult table
            // GameManager will handle scene transition when player is created
            UpdateLoadingText("Login successful...");
        }
        else if (ctx.Event.Status is Status.Failed failed)
        {
            HideLoadingOverlay();
            ShowError(failed.Message ?? "Login failed");
        }
    }
    
    private void OnRestoreSessionResponse(ReducerEventContext ctx, string sessionToken)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            HideLoadingOverlay();
            Debug.Log("[LoginUI] Session restored successfully");
            // GameManager will handle the scene transition when player is ready
        }
        else if (ctx.Event.Status is Status.Failed failed)
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
        authPanel?.RemoveFromClassList("hidden");
        ShowLoginForm();
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
    
    public void ShowLoadingOverlay(string message = "Loading...")
    {
        if (loadingOverlay != null)
        {
            loadingOverlay.RemoveFromClassList("hidden");
            loadingOverlay.style.display = DisplayStyle.Flex;
        }
        
        if (loadingText != null)
        {
            loadingText.text = message;
        }
    }
    
    public void HideLoadingOverlay()
    {
        if (loadingOverlay != null)
        {
            loadingOverlay.AddToClassList("hidden");
            loadingOverlay.style.display = DisplayStyle.None;
        }
    }
    
    public void UpdateLoadingText(string text)
    {
        if (loadingText != null)
        {
            loadingText.text = text;
        }
    }
    
    public void ShowError(string message)
    {
        if (errorText != null)
        {
            errorText.text = message;
            errorText.RemoveFromClassList("hidden");
            errorText.style.display = DisplayStyle.Flex;
        }
        Debug.LogWarning($"[LoginUI] Error: {message}");
    }
    
    private void ShowMessage(string message)
    {
        if (errorText != null)
        {
            errorText.text = message;
            errorText.RemoveFromClassList("error");
            errorText.AddToClassList("success");
            errorText.RemoveFromClassList("hidden");
            errorText.style.display = DisplayStyle.Flex;
        }
    }
    
    private void ClearError()
    {
        if (errorText != null)
        {
            errorText.text = "";
            errorText.AddToClassList("hidden");
            errorText.style.display = DisplayStyle.None;
            errorText.RemoveFromClassList("success");
            errorText.AddToClassList("error");
        }
    }
    
    #endregion
    
    private void OnDestroy()
    {
        // Cleanup reducer handlers
        if (GameManager.IsConnected())
        {
            var conn = GameManager.Conn;
            conn.Reducers.OnRegisterAccount -= OnRegisterResponse;
            conn.Reducers.OnLoginWithSession -= OnLoginWithSessionResponse;
            conn.Reducers.OnRestoreSession -= OnRestoreSessionResponse;
            conn.Reducers.OnLogout -= OnLogoutResponse;
        }
    }
}