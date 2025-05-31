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

    [Header("Scene Names")]
    [Tooltip("Name of the scene to load after successful login.")]
    public string mainSceneName = "WorldScene";

    private void Start()
    {
        Instance = this;
        Application.targetFrameRate = 60;

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
        Debug.Log("before build");
        Conn = builder.Build();
        Debug.Log("after build");
    }

    // ─── SpacetimeDB Callback Handlers ────────────────────────────────────────────

    private void HandleConnect(DbConnection _conn, Identity identity, string token)
    {
        Debug.Log("[GameManager] Connected to SpacetimeDB.");
        AuthToken.SaveToken(token);
        LocalIdentity = identity;

        // Once connected, subscribe to all tables. We’ll wait until subscription is applied.
        Conn.SubscriptionBuilder()
            .OnApplied(HandleSubscriptionApplied)
            .SubscribeToAllTables();
    }

    private void HandleConnectError(Exception ex)
    {
        Debug.Log($"[GameManager] Connection error: {ex}");
        ShowError("Failed to connect to server.");
    }

    private void HandleDisconnect(DbConnection _conn, Exception ex)
    {
        Debug.Log("[GameManager] Disconnected from SpacetimeDB.");
        if (ex != null)
        {
            Debug.LogException(ex);
        }
    }

    private void HandleSubscriptionApplied(SubscriptionEventContext ctx)
    {
        Debug.Log("[GameManager] Subscription applied – initial tables synced.");

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
        if (usernameInput != null)
        {
            usernameInput.text = PlayerPrefs.GetString("SavedUsername", "");
        }
    }

    // ─── UI Event Handlers ────────────────────────────────────────────────────────

    public void OnConnectButtonClicked()
    {
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

        // Store it in PlayerPrefs so next time it’s remembered
        PlayerPrefs.SetString("SavedUsername", playerName);
        PlayerPrefs.Save();

        // Call the enter_game reducer on the “system” database
        try
        {
            Conn.Reducers.EnterGame(playerName);

            Debug.Log($"[GameManager] enter_game succeeded for \"{playerName}\".");

            // Transition to the main game scene

            SceneManager.LoadScene(mainSceneName);

        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameManager] enter_game RPC failed: {ex}");
            ShowError("Server error. Please try again.");
            connectButton.interactable = true;
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
}

/// Because we’re using C# 9 features in Unity, we need IsExternalInit for records/positional
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
