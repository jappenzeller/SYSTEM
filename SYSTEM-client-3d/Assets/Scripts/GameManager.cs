using System;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

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
    [Header("Login UI Elements")]
    [Tooltip("Panel that contains the InputField and Connect Button (enable/disable as needed).")]
    public GameObject loginPanel;         // The parent GameObject for login UI

    [Tooltip("InputField where the user types their username.")]
    public TMP_InputField usernameInput;      // Or TMP_InputField if you use TextMeshPro

    [Tooltip("Connect button that the user clicks to enter the game.")]
    public Button connectButton;

    [Tooltip("Text element for showing errors (e.g. 'Name required' or 'Server error')")]
    public TMP_Text errorText;                // Or TMP_Text if using TextMeshPro

    [Header("Scene Transition Settings")]
    [Tooltip("Should we auto-transition to center world after login? (Set false if login scene has world selection)")]
    public bool autoTransitionToCenterWorld = true;

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
        // For testing purposes, it is often convenient to comment the following lines out and
        // export an executable for the project using File -> Build Settings.
        // Then, you can run the executable multiple times. Since the executable will not check for
        // a saved auth token, each run of will receive a different Identifier,
        // and their circles will be able to eat each other.
        if (AuthToken.Token != "")
        {
            builder = builder.WithToken(AuthToken.Token);
        }

        // Building the connection will establish a connection to the SpacetimeDB
        // server.
      //  Debug.Log("before build");
        Conn = builder.Build();
     //   Debug.Log("after build");
    }

    void SetupLoginScene()
    {
        // Pre-fill username if we have one saved
        if (usernameInput != null && GameData.Instance != null)
        {
            usernameInput.text = GameData.Instance.Username ?? "";
        }
        
        // Initially hide login UI until connected
        if (loginPanel != null)
        {
            loginPanel.SetActive(false);
        }
        
        // Disable connect button until ready
        if (connectButton != null)
        {
            connectButton.interactable = false;
            // Ensure the listener for the connect button is added
            // Remove any existing listeners first to prevent duplicates if this method is ever called more than once.
           // connectButton.onClick.RemoveAllListeners(); 
          //  connectButton.onClick.AddListener(OnConnectButtonClicked);
          //  Debug.Log("[GameManager.SetupLoginScene] Added OnConnectButtonClicked listener to connectButton.");

        }
    }

    // ─── SpacetimeDB Callback Handlers ────────────────────────────────────────────

    private void HandleConnect(DbConnection _conn, Identity identity, string token)
    {
       // Debug.Log("[GameManager] Connected to SpacetimeDB.");
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
      //  Debug.Log($"[GameManager] Connection error: {ex}");
        ShowError("Failed to connect to server.");
    }

    private void HandleDisconnect(DbConnection _conn, Exception ex)
    {
       // Debug.Log("[GameManager] Disconnected from SpacetimeDB.");
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
      //  Debug.Log("[GameManager] Subscription applied – initial tables synced.");

        // Set up SpacetimeDB event handlers
        SetupSpacetimeDBEventHandlers();

        // Check if player already exists in the database
        var existingPlayer = Conn.Db.Player.Identity.Find(LocalIdentity);
        if (existingPlayer != null)
        {
         //   Debug.Log($"[GameManager] Found existing player: {existingPlayer.Name}");
            
            // Update GameData with existing player info
            if (GameData.Instance != null)
            {
                GameData.Instance.SyncWithPlayerData(existingPlayer);
            }
            
            // If we're in login scene but player exists, transition to their world
            if (SceneManager.GetActiveScene().name == "LoginScene")
            {
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
        }

        // Only show login UI if we're in the login scene and no existing player
        if (SceneManager.GetActiveScene().name == "LoginScene")
        {
            ShowLoginUI();
        }
    }

    void SetupSpacetimeDBEventHandlers()
    {
        // Subscribe to player updates to track world changes
        Conn.Db.Player.OnUpdate += OnPlayerUpdated;
        Conn.Db.Player.OnInsert += OnPlayerJoined;
        Conn.Db.Player.OnDelete += OnPlayerLeft;
        
       // Debug.Log("[GameManager] SpacetimeDB event handlers set up");
    }

    void ShowLoginUI()
    {
        // Now that subscription is ready, show the login UI
        if (loginPanel != null)
        {
            loginPanel.SetActive(true);
        }

        // Enable the Connect button so user can type & click
        if (connectButton != null)
        {
            connectButton.interactable = true;
        }

        // Optionally pre-fill usernameInput with a saved PlayerPrefs value:
        if (usernameInput != null && GameData.Instance != null)
        {
            usernameInput.text = GameData.Instance.Username ?? PlayerPrefs.GetString("SavedUsername", "");
        }
    }

    // ─── SpacetimeDB Event Handlers ──────────────────────────────────────────────

    void OnPlayerUpdated(EventContext ctx, Player oldPlayer, Player newPlayer)
    {
    //        Debug.Log($"[GAME MANAGER] OnPlayerUpdated - Player: {newPlayer.Name}, Identity: {newPlayer.Identity}");
 //   Debug.Log($"[GAME MANAGER] Old Rotation: {oldPlayer.Rotation.X:F3},{oldPlayer.Rotation.Y:F3},{oldPlayer.Rotation.Z:F3},{oldPlayer.Rotation.W:F3}");
 //   Debug.Log($"[GAME MANAGER] New Rotation: {newPlayer.Rotation.X:F3},{newPlayer.Rotation.Y:F3},{newPlayer.Rotation.Z:F3},{newPlayer.Rotation.W:F3}");
 //   Debug.Log($"[GAME MANAGER] Time: {Time.time:F3}");
        // If this is our player and their world changed, update GameData
        if (newPlayer.Identity == LocalIdentity && GameData.Instance != null)
        {
            // Check if world actually changed
            if (oldPlayer.CurrentWorld.X != newPlayer.CurrentWorld.X ||
                oldPlayer.CurrentWorld.Y != newPlayer.CurrentWorld.Y ||
                oldPlayer.CurrentWorld.Z != newPlayer.CurrentWorld.Z)
            {
        //        Debug.Log($"[GameManager] Our player moved from world ({oldPlayer.CurrentWorld.X},{oldPlayer.CurrentWorld.Y},{oldPlayer.CurrentWorld.Z}) to ({newPlayer.CurrentWorld.X},{newPlayer.CurrentWorld.Y},{newPlayer.CurrentWorld.Z})");
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
         //   Debug.Log($"[GameManager] Our player joined: {player.Name}");
            if (GameData.Instance != null)
            {
                GameData.Instance.SyncWithPlayerData(player);
            }
        }
        else
        {
   //         Debug.Log($"[GameManager] Another player joined: {player.Name}");
        }
    }

    void OnPlayerLeft(EventContext ctx, Player player)
    {
        if (player.Identity == LocalIdentity)
        {
    //        Debug.Log("[GameManager] Our player left the game");
            // This shouldn't normally happen unless we disconnected
        }
        else
        {
  //          Debug.Log($"[GameManager] Player left: {player.Name}");
        }
    }

    // ─── UI Event Handlers ────────────────────────────────────────────────────────

    public void OnConnectButtonClicked()
    {
   //     Debug.Log("Connect clicked");
        // Hide any previous error
        if (errorText != null)
        {
            errorText.gameObject.SetActive(false);
        }

        // Read and validate the username
        string playerName = usernameInput != null ? usernameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(playerName))
        {
            ShowError("Username is required.");
            return;
        }
        if (playerName.Length > 32)
        {
            ShowError("Username must be 32 characters or less.");
            return;
        }

        // Disable the button while we call the reducer (avoid double‐click)
        connectButton.interactable = false;

        // Store it in GameData and PlayerPrefs so next time it's remembered
        if (GameData.Instance != null)
        {
            GameData.Instance.SetUsername(playerName);
        }
        PlayerPrefs.SetString("SavedUsername", playerName);
        PlayerPrefs.Save();

        // Call the enter_game reducer on the "system" database
        try
        {
            Conn.Reducers.EnterGame(playerName);

      //      Debug.Log($"[GameManager] enter_game succeeded for \"{playerName}\".");

            // Wait a moment for the server response, then transition
            StartCoroutine(WaitForPlayerDataAndTransition());
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameManager] enter_game RPC failed: {ex}");
            ShowError("Server error. Please try again.");
            connectButton.interactable = true;
        }
    }

    System.Collections.IEnumerator WaitForPlayerDataAndTransition()
    {
        // Wait a short time for the server to respond with player data
        float timeout = 5.0f;
        float elapsed = 0f;
        
        Player ourPlayer = null;
        while (elapsed < timeout)
        {
            ourPlayer = Conn.Db.Player.Identity.Find(LocalIdentity);
            if (ourPlayer != null)
            {
                break;
            }
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        
        if (ourPlayer == null)
        {
            ShowError("Failed to create player. Please try again.");
            connectButton.interactable = true;
            yield break;
        }
        
        // Player created successfully, transition to appropriate world
      //  Debug.Log($"[GameManager] Player data received, transitioning to world ({ourPlayer.CurrentWorld.X},{ourPlayer.CurrentWorld.Y},{ourPlayer.CurrentWorld.Z})");
        
        if (SceneTransitionManager.Instance != null)
        {
            if (autoTransitionToCenterWorld || SceneTransitionManager.IsCenter(ourPlayer.CurrentWorld))
            {
                SceneTransitionManager.Instance.TransitionToCenterWorld();
            }
            else
            {
                SceneTransitionManager.Instance.TransitionToWorld(ourPlayer.CurrentWorld);
            }
        }
        else
        {
            // Fallback if no transition manager
            string targetScene = SceneTransitionManager.IsCenter(ourPlayer.CurrentWorld) ? "CenterWorldScene" : "WorldScene";
            SceneManager.LoadScene(targetScene);
        }
    }

    private void ShowError(string message)
    {
        if (errorText != null)
        {
            errorText.text = message;
            errorText.gameObject.SetActive(true);
        }
    }

    // ─── Public Helpers ───────────────────────────────────────────────────────────

    public static bool IsConnected()
    {
        return Conn != null && Conn.IsActive;
    }

    public void Disconnect()
    {
        if (Conn != null)
        {
            Conn.Disconnect();
            Conn = null;
        }
    }

    // ─── World Navigation Helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Request to move player to a specific world (calls server-side logic)
    /// </summary>
    public void RequestMoveToWorld(WorldCoords targetCoords)
    {
        // TODO: Implement server-side reducer for world movement
        // For now, this is a placeholder for future functionality
        //Debug.Log(($"[GameManager] Requesting move to world ({targetCoords.X},{targetCoords.Y},{targetCoords.Z})");
        
        // This would call a SpacetimeDB reducer like:
        // Conn.Reducers.MoveToWorld(targetCoords);
    }

    /// <summary>
    /// Activate a tunnel (spend energy to unlock a new world)
    /// </summary>
    public void ActivateTunnel(ulong tunnelId, float energyAmount)
    {
        if (!IsConnected())
        {
            Debug.LogWarning("[GameManager] Cannot activate tunnel - not connected to server");
            return;
        }
        
        try
        {
            Conn.Reducers.ActivateTunnel(tunnelId, energyAmount);
            //Debug.Log(($"[GameManager] Attempting to activate tunnel {tunnelId} with {energyAmount} energy");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameManager] Failed to activate tunnel: {ex}");
        }
    }

    /// <summary>
    /// Get the current player data from SpacetimeDB
    /// </summary>
    public Player GetCurrentPlayer()
    {
        if (IsConnected() && LocalIdentity != default)
        {
            return Conn.Db.Player.Identity.Find(LocalIdentity);
        }
        return null;
    }

    void OnDestroy()
    {
        // Clean up event subscriptions
        if (Conn?.Db?.Player != null)
        {
            Conn.Db.Player.OnUpdate -= OnPlayerUpdated;
            Conn.Db.Player.OnInsert -= OnPlayerJoined;
            Conn.Db.Player.OnDelete -= OnPlayerLeft;
        }
    }
}

/// Because we're using C# 9 features in Unity, we need IsExternalInit for records/positional
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}