using UnityEngine;
using UnityEngine.UIElements;
using SpacetimeDB.Types;
using SpacetimeDB;
using System.Collections;

public class LoginUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private GameManager gameManager;
    
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
        if (gameManager == null || gameManager.Conn == null) return;
        
        // Handle registration response
        gameManager.Conn.Reducers.OnRegisterAccount += OnRegisterResponse;
        
        // Handle login with session response
        gameManager.Conn.Reducers.OnLoginWithSession += OnLoginWithSessionResponse;
        
        // Handle session restoration response
        gameManager.Conn.Reducers.OnRestoreSession += OnRestoreSessionResponse;
        
        // Handle logout response
        gameManager.Conn.Reducers.OnLogout += OnLogoutResponse;
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
        yield return new WaitUntil(() => gameManager != null && gameManager.IsConnected);
        
        string sessionToken = AuthToken.LoadSession();
        if (!string.IsNullOrEmpty(sessionToken))
        {
            Debug.Log("[LoginUI] Attempting to restore session");
            gameManager.Conn.Reducers.RestoreSession(sessionToken);
            
            // Wait for response (handled in OnRestoreSessionResponse)
            yield return new WaitForSeconds(5f); // Timeout after 5 seconds
            
            // If we're still showing loading, the restore likely failed
            if (loadingOverlay != null && loadingOverlay.style.display == DisplayStyle.Flex)
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
        
        // Call login with session reducer
        string deviceInfo = AuthToken.GetDeviceInfo();
        gameManager.Conn.Reducers.LoginWithSession(username, pin, deviceInfo);
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
        
        // Call register reducer
        gameManager.Conn.Reducers.RegisterAccount(username, displayName, pin);
    }
    
    private void HandleLogout()
    {
        ShowLoadingOverlay("Logging out...");
        
        // Call logout reducer
        gameManager.Conn.Reducers.Logout();
        
        // Clear local session
        AuthToken.ClearSession();
        GameData.Instance.ClearSession();
    }
    
    private void HandleRetryConnection()
    {
        if (gameManager != null)
        {
            gameManager.RetryConnection();
        }
    }
    
    #region Reducer Response Handlers
    
    private void OnRegisterResponse(ReducerEventContext ctx, string username, string displayName, string pin)
    {
        if (ctx.Event.Status == Status.Committed)
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
        else if (ctx.Event.Status is Status.Failed failed)
        {
            HideLoadingOverlay();
            ShowError(failed.Reason ?? "Registration failed");
        }
    }
    
    private void OnLoginWithSessionResponse(ReducerEventContext ctx, string username, string pin, string deviceInfo)
    {
        if (ctx.Event.Status == Status.Committed)
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
        else if (ctx.Event.Status is Status.Failed failed)
        {
            HideLoadingOverlay();
            ShowError(failed.Reason ?? "Login failed");
        }
    }
    
    private void OnRestoreSessionResponse(ReducerEventContext ctx, string sessionToken)
    {
        if (ctx.Event.Status == Status.Committed)
        {
            HideLoadingOverlay();
            Debug.Log("[LoginUI] Session restored successfully");
            
            // The GameManager will handle the scene transition when player is ready
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
    
    private void ShowLoginPanel()
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
    
    private void ShowLoadingOverlay(string message = "Loading...")
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
    
    private void HideLoadingOverlay()
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
    
    private void ShowError(string message)
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
        if (gameManager?.Conn != null)
        {
            gameManager.Conn.Reducers.OnRegisterAccount -= OnRegisterResponse;
            gameManager.Conn.Reducers.OnLoginWithSession -= OnLoginWithSessionResponse;
            gameManager.Conn.Reducers.OnRestoreSession -= OnRestoreSessionResponse;
            gameManager.Conn.Reducers.OnLogout -= OnLogoutResponse;
        }
    }
}