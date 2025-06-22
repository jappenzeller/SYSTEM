using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections;
using System.Collections.Generic;

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
    
    // State
    private bool isInitialized = false;
    
    private void OnEnable()
    {
        Debug.Log("[LoginUI] OnEnable called");
        
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
            Debug.Log($"[LoginUI] UIDocument found: {uiDocument != null}");
        }
        
        // Delay initialization to ensure UI is ready
        StartCoroutine(DelayedInit());
    }
    
    private IEnumerator DelayedInit()
    {
        Debug.Log("[LoginUI] DelayedInit started");
        
        // Wait for end of frame to ensure UI is fully loaded
        yield return new WaitForEndOfFrame();
        
        Debug.Log($"[LoginUI] Root visual element exists: {uiDocument?.rootVisualElement != null}");
        
        SetupUIReferences();
        SetupEventHandlers();
        
        // Fix text field focus
        FixTextFieldFocus();
        
        // Start with UI hidden
        HideAll();
        
        isInitialized = true;
        Debug.Log("[LoginUI] Initialization complete");
    }
    
    private void FixTextFieldFocus()
    {
        // This forces Unity to properly initialize text fields
        root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }
    
    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        // Unregister after first call
        root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        
        // Force all text fields to be properly initialized
        var textFields = root.Query<TextField>().ToList();
        foreach (var field in textFields)
        {
            InitializeTextField(field);
        }
    }
    
    private void SetupUIReferences()
    {
        root = uiDocument.rootVisualElement;
        Debug.Log($"[LoginUI] Root element: {root != null}, child count: {root?.childCount ?? 0}");
        
        // Panels
        authPanel = root.Q<VisualElement>("auth-panel");
        loginForm = root.Q<VisualElement>("login-form");
        registerForm = root.Q<VisualElement>("register-form");
        characterPanel = root.Q<VisualElement>("character-panel");
        loadingOverlay = root.Q<VisualElement>("loading-overlay");
        
        Debug.Log($"[LoginUI] Found panels - auth: {authPanel != null}, login: {loginForm != null}, register: {registerForm != null}");
        
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
    
    private void InitializeTextField(TextField field)
    {
        if (field == null) return;
        
        // Make field focusable
        field.focusable = true;
        field.delegatesFocus = true;
        field.tabIndex = 0;
        
        // Force internal elements to be focusable
        var textInput = field.Q(className: "unity-text-input");
        if (textInput != null)
        {
            textInput.focusable = true;
            textInput.tabIndex = 0;
        }
        
        // Fix cursor visibility on focus
        field.RegisterCallback<FocusInEvent>(evt => {
            field.schedule.Execute(() => {
                field.cursorIndex = field.text.Length;
                field.SelectNone();
            }).ExecuteLater(50);
        });
        
        // Ensure field updates properly
        field.RegisterCallback<ChangeEvent<string>>(evt => {
            field.SetValueWithoutNotify(evt.newValue);
        });
    }
    
    private void SetupPinField(TextField pinField)
    {
        if (pinField == null) return;
        
        // Initialize as regular text field first
        InitializeTextField(pinField);
        
        // Then add PIN-specific behavior
        pinField.maxLength = 4;
        
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
        if (loginButton != null) 
            loginButton.clicked += HandleLogin;
        
        if (showRegisterButton != null) 
            showRegisterButton.clicked += () => ShowRegisterForm();
        
        // Register form  
        if (registerButton != null) 
            registerButton.clicked += HandleRegister;
            
        if (showLoginButton != null) 
            showLoginButton.clicked += () => ShowLoginForm();
        
        // Character creation
        if (createCharacterButton != null) 
            createCharacterButton.clicked += HandleCreateCharacter;
        
        // Enter key support
        if (loginPinField != null)
        {
            loginPinField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    HandleLogin();
            });
        }
        
        if (registerConfirmPinField != null)
        {
            registerConfirmPinField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    HandleRegister();
            });
        }
    }
    
    private void HandleLogin()
    {
        HideError();
        
        string username = loginUsernameField?.value.Trim() ?? "";
        string pin = loginPinField?.value ?? "";
        
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
        
        string username = registerUsernameField?.value.Trim() ?? "";
        string pin = registerPinField?.value ?? "";
        string confirmPin = registerConfirmPinField?.value ?? "";
        
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
        
        string characterName = characterNameField?.value.Trim() ?? "";
        
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
        if (!isInitialized) return;
        
        HideAll();
        authPanel?.RemoveFromClassList("hidden");
        ShowLoginForm();
        
        // Force focus with a slight delay
        root.schedule.Execute(() => {
            loginUsernameField?.Focus();
            
            // Double-check focus on the internal element
            var textInput = loginUsernameField?.Q(className: "unity-text-input");
            textInput?.Focus();
        }).ExecuteLater(100);
    }
    
    public void ShowLoginForm()
    {
        if (loginForm != null) loginForm.RemoveFromClassList("hidden");
        if (registerForm != null) registerForm.AddToClassList("hidden");
        
        // Focus username field after showing
        root.schedule.Execute(() => {
            loginUsernameField?.Focus();
        }).ExecuteLater(50);
    }
    
    public void ShowRegisterForm()
    {
        if (loginForm != null) loginForm.AddToClassList("hidden");
        if (registerForm != null) registerForm.RemoveFromClassList("hidden");
        
        // Focus username field after showing
        root.schedule.Execute(() => {
            registerUsernameField?.Focus();
        }).ExecuteLater(50);
    }
    
    public void ShowCharacterCreation(string defaultName = "")
    {
        HideAll();
        if (characterPanel != null) characterPanel.RemoveFromClassList("hidden");
        
        if (!string.IsNullOrEmpty(defaultName) && characterNameField != null)
            characterNameField.value = defaultName;
            
        // Focus character name field after showing
        root.schedule.Execute(() => {
            characterNameField?.Focus();
        }).ExecuteLater(50);
    }
    
    public void ShowLoading(string message = "Loading...")
    {
        if (loadingOverlay != null) loadingOverlay.RemoveFromClassList("hidden");
        if (loadingText != null) loadingText.text = message;
    }
    
    public void HideLoading()
    {
        if (loadingOverlay != null) loadingOverlay.AddToClassList("hidden");
    }
    
    public void ShowError(string message)
    {
        if (errorText != null)
        {
            errorText.text = message;
            errorText.RemoveFromClassList("hidden");
        }
        
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
        if (errorText != null)
            errorText.AddToClassList("hidden");
    }
    
    private void HideAll()
    {
        if (authPanel != null) authPanel.AddToClassList("hidden");
        if (characterPanel != null) characterPanel.AddToClassList("hidden");
        HideError();
        HideLoading();
    }
    
    // Utility methods
    public void SetLoginButtonEnabled(bool enabled)
    {
        if (loginButton != null)
            loginButton.SetEnabled(enabled);
    }
    
    public void SetRegisterButtonEnabled(bool enabled)
    {
        if (registerButton != null)
            registerButton.SetEnabled(enabled);
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
    
    // Debug method - call this from a button or external script if needed
    public void DebugFocusState()
    {
        Debug.Log($"=== UI Focus Debug ===");
        Debug.Log($"Login username focused: {loginUsernameField?.focusController?.focusedElement == loginUsernameField}");
        Debug.Log($"Cursor visible: {UnityEngine.Cursor.visible}");
        Debug.Log($"Cursor lock state: {UnityEngine.Cursor.lockState}");
        Debug.Log($"Login username value: '{loginUsernameField?.value}'");
        Debug.Log($"Can type: {loginUsernameField?.focusable}");
    }
}