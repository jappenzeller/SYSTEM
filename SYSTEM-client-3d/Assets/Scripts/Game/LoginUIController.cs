using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using SpacetimeDB;
using SpacetimeDB.Types;
using SYSTEM.Game;

/// <summary>
/// Handles the login UI functionality through EventBus events only.
/// Fully event-driven with no direct coupling to GameManager.
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
    private Label messageLabel;
    
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
    private Label errorText;
    
    // State
    private bool isProcessingLogin = false;
    private string pendingUsername = null;
    
    // System readiness
    private bool isInitialized = false;
    
    #region Unity Lifecycle
    
    void Start()
    {
        Debug.Log("[LoginUIController] Start() called, initializing UI");
        // GameEventBus guaranteed to exist via RuntimeInitializeOnLoadMethod
        Initialize();
    }
    
    private void Initialize()
    {
        // Subscribe to EventBus events
        SubscribeToEvents();
        
        SetupUI();
        
        // Set initial UI state based on current EventBus state
        if (GameEventBus.Instance != null)
        {
            UpdateUIForState(GameEventBus.Instance.CurrentState);
        }
        
        // Mark as initialized
        isInitialized = true;
        
        Debug.Log("[LoginUIController] Initialized successfully");
    }
    
    void OnDestroy()
    {
        UnsubscribeFromEvents();
    }
    
    #endregion
    
    #region Event Subscriptions
    
    private void SubscribeToEvents()
    {
        GameEventBus.Instance.Subscribe<StateChangedEvent>(OnStateChanged);
        
        // Connection events
        GameEventBus.Instance.Subscribe<ConnectionEstablishedEvent>(OnConnectionEstablished);
        GameEventBus.Instance.Subscribe<ConnectionLostEvent>(OnConnectionLost);
        GameEventBus.Instance.Subscribe<ConnectionFailedEvent>(OnConnectionFailed);
        
        // Login/Session events
        GameEventBus.Instance.Subscribe<LoginSuccessfulEvent>(OnLoginSuccessful);
        GameEventBus.Instance.Subscribe<LoginFailedEvent>(OnLoginFailed);
        GameEventBus.Instance.Subscribe<SessionCreatedEvent>(OnSessionCreated);
        GameEventBus.Instance.Subscribe<SessionRestoredEvent>(OnSessionRestored);
        
        // Player events
        GameEventBus.Instance.Subscribe<LocalPlayerCheckStartedEvent>(OnPlayerCheckStarted);
        GameEventBus.Instance.Subscribe<LocalPlayerCreatedEvent>(OnPlayerCreated);
        GameEventBus.Instance.Subscribe<LocalPlayerRestoredEvent>(OnPlayerRestored);
        GameEventBus.Instance.Subscribe<LocalPlayerReadyEvent>(OnPlayerReady);
        GameEventBus.Instance.Subscribe<LocalPlayerNotFoundEvent>(OnPlayerNotFound);
        GameEventBus.Instance.Subscribe<PlayerCreationFailedEvent>(OnPlayerCreationFailed);
    }
    
    private void UnsubscribeFromEvents()
    {
        if (GameEventBus.Instance != null)
        {
            GameEventBus.Instance.Unsubscribe<StateChangedEvent>(OnStateChanged);
            GameEventBus.Instance.Unsubscribe<ConnectionEstablishedEvent>(OnConnectionEstablished);
            GameEventBus.Instance.Unsubscribe<ConnectionLostEvent>(OnConnectionLost);
            GameEventBus.Instance.Unsubscribe<ConnectionFailedEvent>(OnConnectionFailed);
            GameEventBus.Instance.Unsubscribe<LoginSuccessfulEvent>(OnLoginSuccessful);
            GameEventBus.Instance.Unsubscribe<LoginFailedEvent>(OnLoginFailed);
            GameEventBus.Instance.Unsubscribe<SessionCreatedEvent>(OnSessionCreated);
            GameEventBus.Instance.Unsubscribe<SessionRestoredEvent>(OnSessionRestored);
            GameEventBus.Instance.Unsubscribe<LocalPlayerCheckStartedEvent>(OnPlayerCheckStarted);
            GameEventBus.Instance.Unsubscribe<LocalPlayerCreatedEvent>(OnPlayerCreated);
            GameEventBus.Instance.Unsubscribe<LocalPlayerRestoredEvent>(OnPlayerRestored);
            GameEventBus.Instance.Unsubscribe<LocalPlayerReadyEvent>(OnPlayerReady);
            GameEventBus.Instance.Unsubscribe<LocalPlayerNotFoundEvent>(OnPlayerNotFound);
            GameEventBus.Instance.Unsubscribe<PlayerCreationFailedEvent>(OnPlayerCreationFailed);
        }
    }
    
    #endregion
    
    #region UI Setup
    
    private void SetupUI()
    {
        if (uiDocument == null)
        {
            Debug.LogError("UIDocument is not assigned!");
            return;
        }
        
        root = uiDocument.rootVisualElement;
        
        // Get UI elements - FIXED: Using exact names from UXML (kebab-case)
        authPanel = root.Q<VisualElement>("auth-panel");
        loginForm = root.Q<VisualElement>("login-form");
        registerForm = root.Q<VisualElement>("register-form");
        loadingOverlay = root.Q<VisualElement>("loading-overlay");
        
        // Login form - FIXED: Using exact names from UXML
        loginUsernameField = root.Q<TextField>("login-username");
        loginPinField = root.Q<TextField>("login-pin");
        loginButton = root.Q<Button>("login-button");
        showRegisterButton = root.Q<Button>("show-register-button");
        
        // Register form - FIXED: Using exact names from UXML
        registerUsernameField = root.Q<TextField>("register-username");
        registerDisplayNameField = root.Q<TextField>("register-display-name");
        registerPinField = root.Q<TextField>("register-pin");
        registerConfirmPinField = root.Q<TextField>("register-confirm-pin");
        registerButton = root.Q<Button>("register-button");
        showLoginButton = root.Q<Button>("show-login-button");
        
        // Loading - FIXED: Using exact names from UXML
        loadingText = root.Q<Label>("loading-text");
        errorText = root.Q<Label>("error-text");
        
        // Debug log to verify elements were found
        
        // Setup event handlers
        SetupEventHandlers();
    }
    
    private void SetupEventHandlers()
    {
        
        // Login button
        if (loginButton != null)
        {
            loginButton.RegisterCallback<ClickEvent>(evt => HandleLogin());
        }
        else
        {
            Debug.LogError("Login button not found!");
        }
        
        // Register button
        if (registerButton != null)
        {
            Debug.LogError($"  - Button enabled: {registerButton.enabledSelf}");
            Debug.LogError($"  - Button display: {registerButton.style.display.value}");
            Debug.LogError($"  - Button classes: {string.Join(", ", registerButton.GetClasses())}");
            
            registerButton.RegisterCallback<ClickEvent>(evt => 
            {
                Debug.LogError($"Event target: {evt.target}");
                Debug.LogError($"Event current target: {evt.currentTarget}");
                HandleRegister();
            });
            
        }
        else
        {
            Debug.LogError("Register button not found!");
            
            // Try to find it a different way
            var allButtons = root.Query<Button>().ToList();
            foreach (var btn in allButtons)
            {
                Debug.LogError($"  - Button name: '{btn.name}', text: '{btn.text}'");
            }
        }
        
        // Show register form button
        if (showRegisterButton != null)
        {
            showRegisterButton.RegisterCallback<ClickEvent>(evt => ShowRegisterForm());
        }
        else
        {
        }
        
        // Show login form button  
        if (showLoginButton != null)
        {
            showLoginButton.RegisterCallback<ClickEvent>(evt => ShowLoginForm());
        }
        else
        {
        }
        
        // Enter key handlers
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
    }
    
    #endregion
    
    #region UI State Management
    
    private void UpdateUIForState(GameEventBus.GameState state)
    {
        
        switch (state)
        {
            case GameEventBus.GameState.Disconnected:
            case GameEventBus.GameState.Connecting:
                ShowConnectingUI();
                break;
                
            case GameEventBus.GameState.Connected:
                // Stay in connecting state until we know if player exists
                ShowConnectingUI("Checking account...");
                break;
                
            case GameEventBus.GameState.CheckingPlayer:
                ShowConnectingUI("Checking for existing player...");
                break;
                
            case GameEventBus.GameState.WaitingForLogin:
                ShowLoginUI();
                break;
                
            case GameEventBus.GameState.Authenticating:
                ShowLoadingOverlay("Logging in...");
                break;
                
            case GameEventBus.GameState.Authenticated:
                ShowLoadingOverlay("Loading character...");
                break;
                
            case GameEventBus.GameState.CreatingPlayer:
                ShowLoadingOverlay("Creating character...");
                break;
                
            case GameEventBus.GameState.PlayerReady:
                ShowLoadingOverlay("Entering world...");
                break;
                
            case GameEventBus.GameState.LoadingWorld:
                ShowLoadingOverlay("Loading world...");
                break;
                
            case GameEventBus.GameState.InGame:
                // Hide all UI - we're in the game
                HideAll();
                break;
        }
    }
    
    private void ShowConnectingUI(string message = "Connecting to server...")
    {
        ShowLoadingOverlay(message);
        if (authPanel != null)
        {
            authPanel.style.display = DisplayStyle.None;
        }
    }
    
    private void ShowLoginUI()
    {
        HideLoadingOverlay();
        
        if (authPanel != null)
        {
            authPanel.style.display = DisplayStyle.Flex;
            authPanel.RemoveFromClassList("hidden");
        }
        else
        {
            Debug.LogError("authPanel is null!");
        }
        
        ShowLoginForm();
        HideMessage();
    }
    
    private void HideAll()
    {
        if (authPanel != null)
        {
            authPanel.style.display = DisplayStyle.None;
        }
        HideLoadingOverlay();
    }
    
    private void ShowLoginForm()
    {
        loginForm?.RemoveFromClassList("hidden");
        registerForm?.AddToClassList("hidden");
        loginUsernameField?.Focus();
    }
    
    private void ShowRegisterForm()
    {
        
        if (loginForm != null)
        {
            loginForm.AddToClassList("hidden");
        }
        
        if (registerForm != null)
        {
            registerForm.RemoveFromClassList("hidden");
            Debug.LogError($"  - Register form display: {registerForm.style.display.value}");
            Debug.LogError($"  - Register form classes: {string.Join(", ", registerForm.GetClasses())}");
        }
        
        // Check if register button is accessible
        if (registerButton != null)
        {
            Debug.LogError($"  - Enabled: {registerButton.enabledSelf}");
            Debug.LogError($"  - Display: {registerButton.style.display.value}");
            Debug.LogError($"  - Visible: {registerButton.visible}");
            Debug.LogError($"  - World bounds: {registerButton.worldBound}");
            
            // Check if something is blocking it
            var parent = registerButton.parent;
            while (parent != null)
            {
                bool hasHiddenClass = false;
                foreach (var className in parent.GetClasses())
                {
                    if (className == "hidden")
                    {
                        hasHiddenClass = true;
                        break;
                    }
                }
                
                if (parent.style.display == DisplayStyle.None || hasHiddenClass)
                {
                }
                parent = parent.parent;
            }
        }
        
        registerUsernameField?.Focus();
    }
    
    private void ShowLoadingOverlay(string text)
    {
        if (loadingOverlay != null)
        {
            loadingOverlay.RemoveFromClassList("hidden");
            loadingOverlay.style.display = DisplayStyle.Flex;
        }
        
        if (loadingText != null)
        {
            loadingText.text = text;
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
    
    private void ShowMessage(string message, bool isError = false)
    {
        if (errorText != null)
        {
            errorText.text = message;
            errorText.RemoveFromClassList("hidden");
            
            if (isError)
            {
                errorText.AddToClassList("error-text");
            }
            else
            {
                errorText.RemoveFromClassList("error-text");
            }
        }
    }
    
    private void HideMessage()
    {
        errorText?.AddToClassList("hidden");
    }
    
    #endregion
    
    #region Login/Register Handlers
    
    private void HandleLogin()
    {
        if (isProcessingLogin) return;
        
        string username = loginUsernameField?.value;
        string pin = loginPinField?.value;
        
        if (string.IsNullOrEmpty(username))
        {
            ShowMessage("Please enter a username", true);
            return;
        }
        
        if (string.IsNullOrEmpty(pin) || pin.Length != 4)
        {
            ShowMessage("Please enter a 4-digit PIN", true);
            return;
        }
        
        isProcessingLogin = true;
        pendingUsername = username;
        
        // Store username temporarily in GameData
        GameData.Instance.SetUsername(username);
        
        ShowLoadingOverlay("Logging in...");
        
        // Publish login started event
        GameEventBus.Instance.Publish(new LoginStartedEvent
        {
            Username = username
        });
        
        // Call the login reducer using static property
        if (GameManager.IsConnected() && GameManager.Conn != null)
        {
            GameManager.Conn.Reducers.LoginWithSession(username, pin, GetDeviceInfo());
        }
        else
        {
            ShowMessage("Not connected to server", true);
            isProcessingLogin = false;
        }
    }
    
    private void HandleRegister()
    {
        
        Debug.LogError($"Current EventBus State: {GameEventBus.Instance.CurrentState}");
        Debug.LogError($"Is Processing Login: {isProcessingLogin}");
        Debug.LogError($"GameManager Connected: {GameManager.IsConnected()}");
        Debug.LogError($"GameManager.Conn: {GameManager.Conn}");
        
        // Check if button is interactable
        if (registerButton != null)
        {
            Debug.LogError($"Register Button Interactable: {registerButton.enabledSelf}");
            Debug.LogError($"Register Button Display: {registerButton.style.display.value}");
        }
        
        string username = registerUsernameField?.value;
        string displayName = registerDisplayNameField?.value;
        string pin = registerPinField?.value;
        string confirmPin = registerConfirmPinField?.value;
        
        
        if (string.IsNullOrEmpty(username))
        {
            ShowMessage("Please enter a username", true);
            return;
        }
        
        if (string.IsNullOrEmpty(displayName))
        {
            ShowMessage("Please enter a display name", true);
            return;
        }
        
        if (string.IsNullOrEmpty(pin) || pin.Length != 4)
        {
            ShowMessage("Please enter a 4-digit PIN", true);
            return;
        }
        
        if (pin != confirmPin)
        {
            ShowMessage("PINs do not match", true);
            return;
        }
        
        // Actually call the register reducer
        if (GameManager.IsConnected() && GameManager.Conn != null)
        {
            
            ShowLoadingOverlay("Creating account...");
            
            try
            {
                // Call the RegisterAccount reducer
                Debug.LogError("[LoginUI] About to invoke Reducers.RegisterAccount...");
                GameManager.Conn.Reducers.RegisterAccount(username, displayName, pin);
                Debug.LogError("[LoginUI] RegisterAccount reducer called successfully!");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LoginUI] ERROR calling RegisterAccount: {e.Message}");
                Debug.LogError($"[LoginUI] Stack trace: {e.StackTrace}");
                ShowMessage($"Registration error: {e.Message}", true);
                return;
            }
            
            // Store the username and PIN for automatic login after account creation
            pendingUsername = username;
            
            // After a short delay, try to login with the new account
            StartCoroutine(AutoLoginAfterRegistration(username, pin));
        }
        else
        {
            Debug.LogError("[LoginUI] === CANNOT REGISTER - NOT CONNECTED ===");
            Debug.LogError($"  - IsConnected: {GameManager.IsConnected()}");
            Debug.LogError($"  - Conn is null: {GameManager.Conn == null}");
            ShowMessage("Not connected to server", true);
        }
    }
    
    private string GetDeviceInfo()
    {
        return $"{SystemInfo.deviceModel}|{SystemInfo.operatingSystem}|{SystemInfo.deviceUniqueIdentifier}";
    }
    
    private IEnumerator AutoLoginAfterRegistration(string username, string pin)
    {
        Debug.Log("[LoginUI] Waiting 2 seconds for account creation to complete...");
        yield return new WaitForSeconds(2f);
        
        Debug.Log($"[LoginUI] Auto-logging in as {username}...");
        ShowLoadingOverlay($"Logging in as {username}...");
        
        // CRITICAL: Store username in GameData so EventBridge can use it
        GameData.Instance.SetUsername(username);
        Debug.Log($"[LoginUI] Set GameData.Username to: {username}");
        
        // Store pending username for player creation
        pendingUsername = username;
        
        // Try to login with the newly created account
        if (GameManager.IsConnected() && GameManager.Conn != null)
        {
            // Publish login started event
            GameEventBus.Instance.Publish(new LoginStartedEvent
            {
                Username = username
            });
            
            GameManager.Conn.Reducers.LoginWithSession(username, pin, GetDeviceInfo());
        }
        else
        {
            ShowMessage("Lost connection to server", true);
        }
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnStateChanged(StateChangedEvent evt)
    {
        UpdateUIForState(evt.NewState);
    }
    
    private void OnConnectionEstablished(ConnectionEstablishedEvent evt)
    {
        Debug.Log("[LoginUI] Connection established");
    }
    
    private void OnConnectionLost(ConnectionLostEvent evt)
    {
        ShowMessage("Connection lost. Please try again.", true);
        isProcessingLogin = false;
    }
    
    private void OnConnectionFailed(ConnectionFailedEvent evt)
    {
        ShowMessage($"Connection failed: {evt.Error}", true);
        isProcessingLogin = false;
    }
    
    private void OnLoginSuccessful(LoginSuccessfulEvent evt)
    {
        Debug.Log($"[LoginUI] Login successful for {evt.Username}");
        ShowLoadingOverlay("Loading character...");
        
        // Reset the login processing flag
        isProcessingLogin = false;
    }
    
    private void OnLoginFailed(LoginFailedEvent evt)
    {
        Debug.Log($"[LoginUI] Login failed: {evt.Reason}");
        ShowMessage(evt.Reason, true);
        isProcessingLogin = false;
        HideLoadingOverlay();
    }
    
    private void OnSessionCreated(SessionCreatedEvent evt)
    {
        Debug.Log($"[LoginUI] Session created for {evt.Username}");
    }
    
    private void OnSessionRestored(SessionRestoredEvent evt)
    {
        Debug.Log($"[LoginUI] Session restored");
    }
    
    private void OnPlayerCheckStarted(LocalPlayerCheckStartedEvent evt)
    {
        ShowLoadingOverlay("Checking for existing character...");
    }
    
    private void OnPlayerCreated(LocalPlayerCreatedEvent evt)
    {
        Debug.Log($"[LoginUI] Player created: {evt.Player.Name}");
    }
    
    private void OnPlayerRestored(LocalPlayerRestoredEvent evt)
    {
        Debug.Log($"[LoginUI] Player restored: {evt.Player.Name}");
    }
    
    private void OnPlayerReady(LocalPlayerReadyEvent evt)
    {
        Debug.Log($"[LoginUI] Player ready: {evt.Player.Name}");
        
        // Hide the login UI
        HideAll();
        
        // Trigger world loading to transition to InGame state
        Debug.Log($"[LoginUI] Publishing WorldLoadStartedEvent for player's world: ({evt.Player.CurrentWorld.X},{evt.Player.CurrentWorld.Y},{evt.Player.CurrentWorld.Z})");
        GameEventBus.Instance.Publish(new WorldLoadStartedEvent
        {
            TargetWorld = evt.Player.CurrentWorld
        });
        
        // Simulate world loaded after a short delay (in a real scenario, this would come from actual world loading)
        StartCoroutine(PublishWorldLoadedAfterDelay(evt.Player.CurrentWorld));
    }
    
    private void OnPlayerNotFound(LocalPlayerNotFoundEvent evt)
    {
        Debug.Log("[LoginUI] No player found after login");
        
        // MVP: The SpacetimeDBEventBridge will automatically create a player
        // Just show a loading message
        ShowLoadingOverlay("Creating your character...");
    }
    
    private void OnPlayerCreationFailed(PlayerCreationFailedEvent evt)
    {
        ShowMessage($"Failed to create character: {evt.Reason}", true);
        isProcessingLogin = false;
    }
    
    private IEnumerator PublishWorldLoadedAfterDelay(WorldCoords worldCoords)
    {
        // Wait a moment to simulate world loading
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log($"[LoginUI] Publishing WorldLoadedEvent for world: ({worldCoords.X},{worldCoords.Y},{worldCoords.Z})");
        
        // Create a dummy world object (in a real scenario, this would come from actual world data)
        var world = new World(
            WorldId: 0,
            WorldCoords: worldCoords,
            WorldName: "Center World",
            WorldType: "center",
            ShellLevel: 0
        );
        
        GameEventBus.Instance.Publish(new WorldLoadedEvent
        {
            World = world
        });
    }
    
    #endregion
}