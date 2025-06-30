using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections;

public class LoginUIController : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;
    
    // UI Elements
    private VisualElement root;
    private VisualElement authPanel;
    private VisualElement loginForm;
    private VisualElement registerForm;
    private VisualElement characterPanel;
    private VisualElement loadingOverlay;
    
    private TextField loginUsernameField;
    private TextField loginPinField;
    private TextField registerUsernameField;
    private TextField registerPinField;
    private TextField registerConfirmPinField;
    private TextField characterNameField;
    
    private Button loginButton;
    private Button registerButton;
    private Button createCharacterButton;
    private Button showRegisterButton;
    private Button showLoginButton;
    
    private Label errorText;
    private Label loadingText;
    
    // Events
    public event Action<string, string> OnLoginRequested;
    public event Action<string, string> OnRegisterRequested;
    public event Action<string> OnCreateCharacterRequested;
    
    private void Awake()
    {
        // FIX: Ensure UI Document is hidden before any rendering occurs
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();
            
        // Hide the entire UI document immediately
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            uiDocument.rootVisualElement.style.display = DisplayStyle.None;
        }
    }
    
    private void OnEnable()
    {
        SetupUIReferences();
        SetupEventHandlers();
        
        // Start with UI hidden (but don't flash)
        HideAll();
        
        // IMPORTANT: Focus the UI Document
        uiDocument.rootVisualElement.focusable = true;
        uiDocument.rootVisualElement.Focus();
        
        // Subscribe to GameManager's ready event
        GameManager.OnLoginUIReady += HandleLoginUIReady;
    }
    
    private void OnDisable()
    {
        // Clean up event handlers to prevent memory leaks
        loginButton?.UnregisterCallback<ClickEvent>(evt => HandleLogin());
        registerButton?.UnregisterCallback<ClickEvent>(evt => HandleRegister());
        createCharacterButton?.UnregisterCallback<ClickEvent>(evt => HandleCreateCharacter());
        showRegisterButton?.UnregisterCallback<ClickEvent>(evt => ShowRegisterForm());
        showLoginButton?.UnregisterCallback<ClickEvent>(evt => ShowLoginForm());
        
        // Unsubscribe from GameManager event
        GameManager.OnLoginUIReady -= HandleLoginUIReady;
    }
    
    private void HandleLoginUIReady()
    {
        Debug.Log("[LoginUIController] Received LoginUIReady event, showing auth panel");
        ShowAuthPanel();
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
        
        // Enter key handlers
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
        
        characterNameField?.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                HandleCreateCharacter();
        });
    }
    
    private void HandleLogin()
    {
        HideError();
        
        string username = loginUsernameField.value.Trim();
        string pin = loginPinField.value;
        
        if (string.IsNullOrEmpty(username))
        {
            ShowError("Username is required");
            return;
        }
        
        if (pin.Length != 4)
        {
            ShowError("PIN must be 4 digits");
            return;
        }
        
        ShowLoading("Logging in...");
        OnLoginRequested?.Invoke(username, pin);
    }
    
    private void HandleRegister()
    {
        HideError();
        
        string username = registerUsernameField.value.Trim();
        string pin = registerPinField.value;
        string confirmPin = registerConfirmPinField.value;
        
        if (string.IsNullOrEmpty(username))
        {
            ShowError("Username is required");
            return;
        }
        
        if (username.Length < 3 || username.Length > 20)
        {
            ShowError("Username must be 3-20 characters");
            return;
        }
        
        if (pin.Length != 4)
        {
            ShowError("PIN must be 4 digits");
            return;
        }
        
        if (pin != confirmPin)
        {
            ShowError("PINs do not match");
            return;
        }
        
        ShowLoading("Creating account...");
        OnRegisterRequested?.Invoke(username, pin);
    }
    
    private void HandleCreateCharacter()
    {
        HideError();
        
        string characterName = characterNameField.value.Trim();
        
        if (string.IsNullOrEmpty(characterName))
        {
            ShowError("Character name is required");
            return;
        }
        
        if (characterName.Length < 2 || characterName.Length > 20)
        {
            ShowError("Character name must be 2-20 characters");
            return;
        }
        
        ShowLoading("Creating character...");
        OnCreateCharacterRequested?.Invoke(characterName);
    }
    
    // Public UI Control Methods
    public void ShowAuthPanel()
    {
        // FIX: First make the root visible if it was hidden
        root.style.display = DisplayStyle.Flex;
        
        HideAll();
        authPanel.RemoveFromClassList("hidden");
        ShowLoginForm();
    }
    
    public void ShowLoginForm()
    {
        loginForm.RemoveFromClassList("hidden");
        registerForm.AddToClassList("hidden");
        StartCoroutine(FocusFieldNextFrame(loginUsernameField));
    }
    
    public void ShowRegisterForm()
    {
        loginForm.AddToClassList("hidden");
        registerForm.RemoveFromClassList("hidden");
        StartCoroutine(FocusFieldNextFrame(registerUsernameField));
    }
    
    public void ShowCharacterCreation(string defaultName = "")
    {
        // FIX: Ensure root is visible
        root.style.display = DisplayStyle.Flex;
        
        HideAll();
        characterPanel.RemoveFromClassList("hidden");
        
        if (!string.IsNullOrEmpty(defaultName))
            characterNameField.value = defaultName;
            
        StartCoroutine(FocusFieldNextFrame(characterNameField));
    }
    
    public void ShowLoading(string message = "Loading...")
    {
        loadingOverlay.RemoveFromClassList("hidden");
        loadingText.text = message;
    }
    
    public void HideLoading()
    {
        loadingOverlay.AddToClassList("hidden");
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
        errorText.AddToClassList("hidden");
    }
    
    private void HideAll()
    {
        authPanel?.AddToClassList("hidden");
        characterPanel?.AddToClassList("hidden");
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
}