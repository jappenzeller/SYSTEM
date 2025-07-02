using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class LoginUIController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    
    // UI Elements
    private VisualElement root;
    private VisualElement authPanel;
    private VisualElement loginForm;
    private VisualElement registerForm;
    private VisualElement characterPanel;
    private VisualElement loadingOverlay;
    private VisualElement retryPanel;
    
    // Form fields
    private TextField loginUsernameField;
    private TextField loginPinField;
    private TextField registerUsernameField;
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
    
    // Text elements
    private Label errorText;
    private Label loadingText;
    
    // Events
    public event Action<string, string> OnLoginRequested;
    public event Action<string, string> OnRegisterRequested;
    public event Action<string> OnCreateCharacterRequested;
    
    void OnEnable()
    {
        SetupUIReferences();
        SetupEventHandlers();
        
        // Start with everything hidden
        HideAll();
    }
    
    void OnDisable()
    {
        // Unregister event handlers
        loginButton?.UnregisterCallback<ClickEvent>(evt => HandleLogin());
        registerButton?.UnregisterCallback<ClickEvent>(evt => HandleRegister());
        createCharacterButton?.UnregisterCallback<ClickEvent>(evt => HandleCreateCharacter());
        showRegisterButton?.UnregisterCallback<ClickEvent>(evt => ShowRegisterForm());
        showLoginButton?.UnregisterCallback<ClickEvent>(evt => ShowLoginForm());
        retryConnectionButton?.UnregisterCallback<ClickEvent>(evt => HandleRetryConnection());
    }
    
    private void SetupUIReferences()
    {
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
        
        // Text elements
        errorText = root.Q<Label>("error-text");
        loadingText = root.Q<Label>("loading-text");
    }
    
    private void SetupEventHandlers()
    {
        // Button click events
        loginButton?.RegisterCallback<ClickEvent>(evt => HandleLogin());
        registerButton?.RegisterCallback<ClickEvent>(evt => HandleRegister());
        createCharacterButton?.RegisterCallback<ClickEvent>(evt => HandleCreateCharacter());
        showRegisterButton?.RegisterCallback<ClickEvent>(evt => ShowRegisterForm());
        showLoginButton?.RegisterCallback<ClickEvent>(evt => ShowLoginForm());
        retryConnectionButton?.RegisterCallback<ClickEvent>(evt => HandleRetryConnection());
        
        // Enter key support for PIN fields
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
        
        characterNameField?.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                HandleCreateCharacter();
            }
        });
    }
    
    #region Form Handlers
    
    private void HandleLogin()
    {
        string username = loginUsernameField.value.Trim();
        string pin = loginPinField.value;
        
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
        
        SetLoginButtonEnabled(false);
        ShowLoading("Logging in...");
        OnLoginRequested?.Invoke(username, pin);
    }
    
    private void HandleRegister()
    {
        string username = registerUsernameField.value.Trim();
        string pin = registerPinField.value;
        string confirmPin = registerConfirmPinField.value;
        
        if (string.IsNullOrEmpty(username))
        {
            ShowError("Please enter a username");
            return;
        }
        
        if (username.Length < 3 || username.Length > 20)
        {
            ShowError("Username must be between 3 and 20 characters");
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
        
        SetRegisterButtonEnabled(false);
        ShowLoading("Creating account...");
        OnRegisterRequested?.Invoke(username, pin);
    }
    
    private void HandleCreateCharacter()
    {
        string characterName = characterNameField.value.Trim();
        
        if (string.IsNullOrEmpty(characterName))
        {
            ShowError("Please enter a character name");
            return;
        }
        
        if (characterName.Length < 3 || characterName.Length > 20)
        {
            ShowError("Character name must be between 3 and 20 characters");
            return;
        }
        
        SetCreateCharacterButtonEnabled(false);
        ShowLoading("Creating character...");
        OnCreateCharacterRequested?.Invoke(characterName);
    }
    
    private void HandleRetryConnection()
    {
        // This will be handled by GameManager via the callback
        HideRetryPanel();
    }
    
    #endregion
    
    #region UI State Management
    
    public void ShowAuthPanel()
    {
        HideAll();
        authPanel?.RemoveFromClassList("hidden");
        ShowLoginForm();
    }
    
    public void HideAuthPanel()
    {
        authPanel?.AddToClassList("hidden");
    }
    
    public void ShowLoginForm()
    {
        loginForm?.RemoveFromClassList("hidden");
        registerForm?.AddToClassList("hidden");
        HideError();
        
        // Focus username field
        StartCoroutine(FocusFieldNextFrame(loginUsernameField));
    }
    
    public void ShowRegisterForm()
    {
        loginForm?.AddToClassList("hidden");
        registerForm?.RemoveFromClassList("hidden");
        HideError();
        
        // Focus username field
        StartCoroutine(FocusFieldNextFrame(registerUsernameField));
    }
    
    public void ShowCharacterCreation()
    {
        HideAll();
        characterPanel?.RemoveFromClassList("hidden");
        
        // Focus character name field
        StartCoroutine(FocusFieldNextFrame(characterNameField));
    }
    
    public void ShowLoading(string message = "Loading...")
    {
        loadingOverlay?.RemoveFromClassList("hidden");
        if (loadingText != null)
        {
            loadingText.text = message;
        }
    }
    
    public void UpdateLoadingText(string message)
    {
        if (loadingText != null)
        {
            loadingText.text = message;
        }
    }
    
    public void HideLoading()
    {
        loadingOverlay?.AddToClassList("hidden");
    }
    
    public void ShowError(string message)
    {
        errorText.text = message;
        errorText.RemoveFromClassList("hidden");
        
        // Auto-hide after 5 seconds
        StartCoroutine(HideErrorAfterDelay(5f));
    }
    
    private IEnumerator HideErrorAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideError();
    }
    
    public void HideError()
    {
        errorText?.AddToClassList("hidden");
    }
    
    public void ShowRetryConnection(Action retryCallback)
    {
        if (retryPanel == null)
        {
            // Create retry panel dynamically if it doesn't exist
            CreateRetryPanel();
        }
        
        retryPanel?.RemoveFromClassList("hidden");
        
        // Store the callback
        if (retryConnectionButton != null)
        {
            retryConnectionButton.clicked -= HandleRetryConnection;
            retryConnectionButton.clicked += () =>
            {
                retryCallback?.Invoke();
                HideRetryPanel();
            };
        }
    }
    
    private void HideRetryPanel()
    {
        retryPanel?.AddToClassList("hidden");
    }
    
    private void CreateRetryPanel()
    {
        // Create retry panel dynamically
        retryPanel = new VisualElement();
        retryPanel.name = "retry-panel";
        retryPanel.AddToClassList("panel");
        retryPanel.AddToClassList("hidden");
        
        var retryForm = new VisualElement();
        retryForm.AddToClassList("form");
        
        var retryTitle = new Label("Connection Failed");
        retryTitle.AddToClassList("title");
        
        var retryInfo = new Label("Unable to connect to the game server");
        retryInfo.AddToClassList("info-text");
        
        retryConnectionButton = new Button(() => HandleRetryConnection());
        retryConnectionButton.name = "retry-connection-button";
        retryConnectionButton.text = "Retry Connection";
        retryConnectionButton.AddToClassList("primary-button");
        
        retryForm.Add(retryTitle);
        retryForm.Add(retryInfo);
        retryForm.Add(retryConnectionButton);
        
        retryPanel.Add(retryForm);
        root.Add(retryPanel);
    }
    
    private void HideAll()
    {
        authPanel?.AddToClassList("hidden");
        characterPanel?.AddToClassList("hidden");
        retryPanel?.AddToClassList("hidden");
        HideError();
        HideLoading();
    }
    
    private IEnumerator FocusFieldNextFrame(TextField field)
    {
        yield return null; // Wait one frame
        field?.Focus();
    }
    
    // Utility methods
    public void SetLoginButtonEnabled(bool enabled)
    {
        loginButton?.SetEnabled(enabled);
    }
    
    public void SetRegisterButtonEnabled(bool enabled)
    {
        registerButton?.SetEnabled(enabled);
    }
    
    public void SetCreateCharacterButtonEnabled(bool enabled)
    {
        createCharacterButton?.SetEnabled(enabled);
    }
    
    public void ClearForms()
    {
        if (loginUsernameField != null) loginUsernameField.value = "";
        if (loginPinField != null) loginPinField.value = "";
        if (registerUsernameField != null) registerUsernameField.value = "";
        if (registerPinField != null) registerPinField.value = "";
        if (registerConfirmPinField != null) registerConfirmPinField.value = "";
        if (characterNameField != null) characterNameField.value = "";
    }
    
    // Response handlers for server feedback
    public void HandleLoginSuccess()
    {
        HideLoading();
        ClearForms();
        // GameManager will handle scene transition
    }
    
    public void HandleLoginError(string error)
    {
        HideLoading();
        ShowError(error);
        SetLoginButtonEnabled(true);
    }
    
    public void HandleRegisterSuccess()
    {
        HideLoading();
        ShowError("Account created! Please log in.");
        ShowLoginForm();
        loginUsernameField.value = registerUsernameField.value;
        ClearForms();
    }
    
    public void HandleRegisterError(string error)
    {
        HideLoading();
        ShowError(error);
        SetRegisterButtonEnabled(true);
    }
    
    public void HandleCreateCharacterSuccess()
    {
        HideLoading();
        ClearForms();
        // GameManager will handle scene transition
    }
    
    public void HandleCreateCharacterError(string error)
    {
        HideLoading();
        ShowError(error);
        SetCreateCharacterButtonEnabled(true);
    }
    
    #endregion
}