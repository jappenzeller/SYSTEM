using System;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static DbConnection Conn { get; private set; }

    // If you ever want to switch to cloud, uncomment below and comment localhost
    // const string SERVER_URL = "https://maincloud.spacetimedb.com";
    const string SERVER_URL = "http://127.0.0.1:3000";
    const string MODULE_NAME = "system";

    public static Identity LocalIdentity { get; private set; }
    public static GameManager Instance { get; private set; }

    // ─── UI References ───────────────────────────────────────────────────────────
    [Header("UI Toolkit Integration")]
    [Tooltip("Reference to the LoginUIController component")]
    public LoginUIController loginUI;

    [Header("Scene Transition Settings")]
    [Tooltip("Should we auto-transition to center world after login? (Set false if login scene has world selection)")]
    public bool autoTransitionToCenterWorld = true;

    // Authentication state
    private bool isAuthenticated = false;
    private string authenticatedUsername = "";

    private void Start()
    {
        Instance = this;
        Application.targetFrameRate = 60;

        // Check if we're in the login scene specifically
        if (SceneManager.GetActiveScene().name == "LoginScene")
        {
            SetupLoginScene();
        }

        // In order to build a connection to SpacetimeDB we need to register
        // our callbacks and specify a SpacetimeDB server URI and module name.
        var builder = DbConnection.Builder()
            .OnConnect(HandleConnect)
            .OnConnectError(HandleConnectError)
            .OnDisconnect(HandleDisconnect)
            .WithUri(SERVER_URL)
            .WithModuleName(MODULE_NAME);

        // If the user has a SpacetimeDB auth token stored in the Unity PlayerPrefs,
        // we can use it to authenticate the connection.
        if (AuthToken.Token != "")
        {
            builder = builder.WithToken(AuthToken.Token);
        }

        // Building the connection will establish a connection to the SpacetimeDB server.
        Conn = builder.Build();
    }

    void SetupLoginScene()
    {
        // Setup UI Toolkit event handlers if available
        if (loginUI != null)
        {
            loginUI.OnLoginRequested += OnLoginRequested;
            loginUI.OnRegisterRequested += OnRegisterRequested;
            loginUI.OnCreateCharacterRequested += OnCreateCharacterRequested;
            
            // Hide UI initially until connected
            loginUI.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("[GameManager] LoginUIController not found! Please assign it in the inspector.");
        }
    }

    // ─── SpacetimeDB Callback Handlers ────────────────────────────────────────────

    private void HandleConnect(DbConnection _conn, Identity identity, string token)
    {
        Debug.Log("[GameManager] Connected to SpacetimeDB.");
        AuthToken.SaveToken(token);
        LocalIdentity = identity;

        // Store identity in GameData
        if (GameData.Instance != null)
        {
            GameData.Instance.SetPlayerIdentity(identity);
        }

        // Once connected, subscribe to all tables. We'll wait until subscription is applied.
        Conn.SubscriptionBuilder()
            .OnApplied(HandleSubscriptionApplied)
            .SubscribeToAllTables();
    }

    private void HandleConnectError(Exception ex)
    {
        Debug.LogError($"[GameManager] Connection error: {ex}");
        if (loginUI != null)
        {
            loginUI.ShowError("Failed to connect to server.");
        }
    }

    private void HandleDisconnect(DbConnection _conn, Exception ex)
    {
        Debug.Log("[GameManager] Disconnected from SpacetimeDB.");
        if (ex != null)
        {
            Debug.LogException(ex);
        }
        
        // Clear login state
        if (GameData.Instance != null)
        {
            GameData.Instance.ClearSession();
        }
        
        // Return to login scene if not already there
        if (SceneManager.GetActiveScene().name != "LoginScene")
        {
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.TransitionToLogin();
            }
            else
            {
                SceneManager.LoadScene("LoginScene");
            }
        }
    }

    private void HandleSubscriptionApplied(SubscriptionEventContext ctx)
    {
        Debug.Log("[GameManager] Subscription applied – initial tables synced.");

        // Check if we're in the login scene
        if (SceneManager.GetActiveScene().name == "LoginScene")
        {
            // Check if player already exists in the database
            var existingPlayer = Conn.Db.Player.Identity.Find(LocalIdentity);
            if (existingPlayer != null)
            {
                Debug.Log($"[GameManager] Found existing player: {existingPlayer.Name}");
                
                // Update GameData with existing player info
                if (GameData.Instance != null)
                {
                    GameData.Instance.SyncWithPlayerData(existingPlayer);
                }
                
                // If we're in login scene but player exists, transition to their world
                if (SceneTransitionManager.Instance != null)
                {
                    if (SceneTransitionManager.IsCenter(existingPlayer.CurrentWorld))
                    {
                        SceneTransitionManager.Instance.TransitionToCenterWorld();
                    }
                    else
                    {
                        SceneTransitionManager.Instance.TransitionToWorld(existingPlayer.CurrentWorld);
                    }
                }
                return; // Skip showing login UI
            }

            // Only show login UI if we're in the login scene and no existing player
            ShowLoginUI();
        }

        // Set up SpacetimeDB event handlers
        SetupSpacetimeDBEventHandlers();
    }

    void SetupSpacetimeDBEventHandlers()
    {
        // Subscribe to player updates to track world changes
        Conn.Db.Player.OnUpdate += OnPlayerUpdated;
        Conn.Db.Player.OnInsert += OnPlayerJoined;
        Conn.Db.Player.OnDelete += OnPlayerLeft;
        
        Debug.Log("[GameManager] SpacetimeDB event handlers set up");
    }

    void ShowLoginUI()
    {
        // Now that subscription is ready, show the login UI
        if (loginUI != null)
        {
            loginUI.gameObject.SetActive(true);
            loginUI.ShowAuthPanel();
        }
    }

    // ─── UI Toolkit Event Handlers ────────────────────────────────────────────────

    private void OnLoginRequested(string username, string pin)
    {
        loginUI.ShowLoading("Logging in...");
        loginUI.SetLoginButtonEnabled(false);
        
        try
        {
            Conn.Reducers.LoginAccount(username, pin);
            StartCoroutine(WaitForAuthenticationResult(username));
        }
        catch (Exception ex)
        {
            Debug.LogError($"Login failed: {ex}");
            loginUI.ShowError("Login failed. Please try again.");
            loginUI.HideLoading();
            loginUI.SetLoginButtonEnabled(true);
        }
    }
    
    private void OnRegisterRequested(string username, string pin)
    {
        loginUI.ShowLoading("Creating account...");
        loginUI.SetRegisterButtonEnabled(false);
        
        try
        {
            Conn.Reducers.RegisterAccount(username, pin);
            StartCoroutine(WaitForRegistrationResult(username));
        }
        catch (Exception ex)
        {
            Debug.LogError($"Registration failed: {ex}");
            loginUI.ShowError("Registration failed. Please try again.");
            loginUI.HideLoading();
            loginUI.SetRegisterButtonEnabled(true);
        }
    }
    
    private void OnCreateCharacterRequested(string characterName)
    {
        if (!isAuthenticated)
        {
            loginUI.ShowError("Not authenticated. Please login first.");
            return;
        }
        
        loginUI.ShowLoading("Creating character...");
        
        try
        {
            Conn.Reducers.EnterGame(characterName);
            StartCoroutine(WaitForCharacterCreation());
        }
        catch (Exception ex)
        {
            Debug.LogError($"Character creation failed: {ex}");
            loginUI.ShowError("Failed to create character. Name might be taken.");
            loginUI.HideLoading();
        }
    }
    
    IEnumerator WaitForAuthenticationResult(string username)
    {
        yield return new WaitForSeconds(1.0f);
        
        // Check if we have a player
        var player = Conn.Db.Player.Identity.Find(LocalIdentity);
        if (player != null)
        {
            // Already have a character, go to game
            isAuthenticated = true;
            authenticatedUsername = username;
            loginUI.HideLoading();
            TransitionToGame(player);
        }
        else
        {
            // Need to create character
            isAuthenticated = true;
            authenticatedUsername = username;
            loginUI.HideLoading();
            loginUI.ShowCharacterCreation(username);
        }
        
        loginUI.SetLoginButtonEnabled(true);
    }
    
    IEnumerator WaitForRegistrationResult(string username)
    {
        yield return new WaitForSeconds(1.0f);
        
        // Auto-login after registration
        isAuthenticated = true;
        authenticatedUsername = username;
        loginUI.HideLoading();
        loginUI.ShowCharacterCreation(username);
        
        loginUI.SetRegisterButtonEnabled(true);
    }
    
    IEnumerator WaitForCharacterCreation()
    {
        float timeout = 5.0f;
        float elapsed = 0f;
        
        Player ourPlayer = null;
        while (elapsed < timeout)
        {
            ourPlayer = Conn.Db.Player.Identity.Find(LocalIdentity);
            if (ourPlayer != null) break;
            
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        
        loginUI.HideLoading();
        
        if (ourPlayer == null)
        {
            loginUI.ShowError("Failed to create character. Please try again.");
            yield break;
        }
        
        TransitionToGame(ourPlayer);
    }
    
    void TransitionToGame(Player player)
    {
        if (GameData.Instance != null)
        {
            GameData.Instance.SetUsername(player.Name);
            GameData.Instance.SyncWithPlayerData(player);
        }
        
        Debug.Log($"[GameManager] Transitioning to game world for player: {player.Name}");
        
        if (SceneTransitionManager.Instance != null)
        {
            if (SceneTransitionManager.IsCenter(player.CurrentWorld))
            {
                SceneTransitionManager.Instance.TransitionToCenterWorld();
            }
            else
            {
                SceneTransitionManager.Instance.TransitionToWorld(player.CurrentWorld);
            }
        }
    }

    // ─── SpacetimeDB Event Handlers ──────────────────────────────────────────────

    void OnPlayerUpdated(EventContext ctx, Player oldPlayer, Player newPlayer)
    {
        // If this is our player and their world changed, update GameData
        if (newPlayer.Identity == LocalIdentity && GameData.Instance != null)
        {
            // Check if world actually changed
            if (oldPlayer.CurrentWorld.X != newPlayer.CurrentWorld.X ||
                oldPlayer.CurrentWorld.Y != newPlayer.CurrentWorld.Y ||
                oldPlayer.CurrentWorld.Z != newPlayer.CurrentWorld.Z)
            {
                Debug.Log($"[GameManager] Our player moved from world ({oldPlayer.CurrentWorld.X},{oldPlayer.CurrentWorld.Y},{oldPlayer.CurrentWorld.Z}) to ({newPlayer.CurrentWorld.X},{newPlayer.CurrentWorld.Y},{newPlayer.CurrentWorld.Z})");
                GameData.Instance.OnPlayerWorldUpdated(newPlayer);
            }

            // Update other player data
            GameData.Instance.SyncWithPlayerData(newPlayer);
        }
    }

    void OnPlayerJoined(EventContext ctx, Player player)
    {
        if (player.Identity == LocalIdentity)
        {
            Debug.Log($"[GameManager] Our player joined: {player.Name}");
            if (GameData.Instance != null)
            {
                GameData.Instance.SyncWithPlayerData(player);
            }
        }
        else
        {
            Debug.Log($"[GameManager] Another player joined: {player.Name}");
        }
    }

    void OnPlayerLeft(EventContext ctx, Player player)
    {
        if (player.Identity == LocalIdentity)
        {
            Debug.Log("[GameManager] Our player left the game");
            // This shouldn't normally happen unless we disconnected
        }
        else
        {
            Debug.Log($"[GameManager] Player left: {player.Name}");
        }
    }

    // ─── Helper Methods ───────────────────────────────────────────────────────────

    public static bool IsConnected()
    {
        return Conn != null && LocalIdentity != null;
    }

    public static Player GetCurrentPlayer()
    {
        if (Conn == null || LocalIdentity == null) return null;
        return Conn.Db.Player.Identity.Find(LocalIdentity);
    }

    public static void ActivateTunnel(ulong tunnelId, float energyAmount)
    {
        if (!IsConnected())
        {
            Debug.LogError("[GameManager] Cannot activate tunnel - not connected");
            return;
        }
        
        try
        {
            Conn.Reducers.ActivateTunnel(tunnelId, energyAmount);
            Debug.Log($"[GameManager] Activated tunnel {tunnelId} with {energyAmount} energy");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameManager] Failed to activate tunnel: {ex.Message}");
        }
    }

    void ShowError(string message)
    {
        if (loginUI != null)
        {
            loginUI.ShowError(message);
        }
        else
        {
            Debug.LogError($"[GameManager] Error: {message}");
        }
    }
}