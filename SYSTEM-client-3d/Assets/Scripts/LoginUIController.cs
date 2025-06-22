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
    
    private void OnEnable()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();
            
        SetupUIReferences();
        SetupEventHandlers();
        
        // Start with UI hidden
        HideAll();
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
        loginButton = root.Q<Button>("login-button");
        showRegisterButton = root.Q<Button>("show-register-button");
        
        // Register form
        registerUsernameField = root.Q<TextField>("register-username");
        registerPinField = root.Q<TextField>("register-pin");
        registerConfirmPinField = root.Q<TextField>("register-confirm-pin");
        registerButton = root.Q<Button>("register-button");
        showLoginButton = root.Q<Button>("show-login-button");
        
        // Character creation
        characterNameField = root.Q<TextField>("character-name");
        createCharacterButton = root.Q<Button>("create-character-button");
        
        // Other elements
        errorText = root.Q<Label>("error-text");
        loadingText = root.Q<Label>("loading-text");
        
        // Setup PIN fields to only accept numbers
        SetupPinField(loginPinField);
        SetupPinField(registerPinField);
        SetupPinField(registerConfirmPinField);
    }
    
    private void SetupPinField(TextField pinField)
    {
        pinField.RegisterValueChangedCallback(evt =>
        {
            string newValue = evt.newValue;
            string filtered = "";
            
            foreach (char c in newValue)
            {
                if (char.IsDigit(c) && filtered.Length < 4)
                    filtered += c;
            }
            
            if (filtered != newValue)
                pinField.SetValueWithoutNotify(filtered);
        });
    }
    
    private void SetupEventHandlers()
    {
        // Login form
        loginButton.clicked += HandleLogin;
        showRegisterButton.clicked += () => ShowRegisterForm();
        
        // Register form  
        registerButton.clicked += HandleRegister;
        showLoginButton.clicked += () => ShowLoginForm();
        
        // Character creation
        createCharacterButton.clicked += HandleCreateCharacter;
        
        // Enter key support
        loginPinField.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                HandleLogin();
        });
        
        registerConfirmPinField.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                HandleRegister();
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
        
        OnCreateCharacterRequested?.Invoke(characterName);
    }
    
    // Public UI Control Methods
    public void ShowAuthPanel()
    {
        HideAll();
        authPanel.RemoveFromClassList("hidden");
        ShowLoginForm();
    }
    
    public void ShowLoginForm()
    {
        loginForm.RemoveFromClassList("hidden");
        registerForm.AddToClassList("hidden");
        loginUsernameField.Focus();
    }
    
    public void ShowRegisterForm()
    {
        loginForm.AddToClassList("hidden");
        registerForm.RemoveFromClassList("hidden");
        registerUsernameField.Focus();
    }
    
    public void ShowCharacterCreation(string defaultName = "")
    {
        HideAll();
        characterPanel.RemoveFromClassList("hidden");
        
        if (!string.IsNullOrEmpty(defaultName))
            characterNameField.value = defaultName;
            
        characterNameField.Focus();
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
        authPanel.AddToClassList("hidden");
        characterPanel.AddToClassList("hidden");
        HideError();
        HideLoading();
    }
    
    // Utility methods
    public void SetLoginButtonEnabled(bool enabled)
    {
        loginButton.SetEnabled(enabled);
    }
    
    public void SetRegisterButtonEnabled(bool enabled)
    {
        registerButton.SetEnabled(enabled);
    }
    
    public void ClearForms()
    {
        loginUsernameField.value = "";
        loginPinField.value = "";
        registerUsernameField.value = "";
        registerPinField.value = "";
        registerConfirmPinField.value = "";
        characterNameField.value = "";
    }
}