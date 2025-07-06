// AuthToken.cs - Enhanced session management for SpaceTimeDB
using UnityEngine;
using System;

public static class AuthToken
{
    // Keys for PlayerPrefs storage
    private const string TOKEN_KEY = "SpacetimeDBToken";        // SpacetimeDB identity token
    private const string SESSION_KEY = "SpacetimeDBSession";    // Our game session token
    private const string USERNAME_KEY = "LastUsername";         // For auto-fill
    private const string DEVICE_ID_KEY = "DeviceId";           // Unique device identifier
    
    #region Session Management
    
    /// <summary>
    /// Save a game session after successful login
    /// </summary>
    public static void SaveSession(string sessionToken, string username)
    {
        if (!string.IsNullOrEmpty(sessionToken) && !string.IsNullOrEmpty(username))
        {
            PlayerPrefs.SetString(SESSION_KEY, sessionToken);
            PlayerPrefs.SetString(USERNAME_KEY, username);
            PlayerPrefs.Save();
            Debug.Log($"[AuthToken] Session saved for user: {username}");
        }
    }
    
    /// <summary>
    /// Load saved session token
    /// </summary>
    public static string LoadSession()
    {
        return PlayerPrefs.GetString(SESSION_KEY, null);
    }
    
    /// <summary>
    /// Load last logged in username
    /// </summary>
    public static string LoadLastUsername()
    {
        return PlayerPrefs.GetString(USERNAME_KEY, null);
    }
    
    /// <summary>
    /// Check if we have a saved session
    /// </summary>
    public static bool HasSession()
    {
        return PlayerPrefs.HasKey(SESSION_KEY) && !string.IsNullOrEmpty(PlayerPrefs.GetString(SESSION_KEY));
    }
    
    /// <summary>
    /// Clear session data (for logout)
    /// </summary>
    public static void ClearSession()
    {
        PlayerPrefs.DeleteKey(SESSION_KEY);
        // Keep username for convenience
        PlayerPrefs.Save();
        Debug.Log("[AuthToken] Session cleared");
    }
    
    #endregion
    
    #region SpacetimeDB Token Management
    
    /// <summary>
    /// Save SpacetimeDB authentication token
    /// </summary>
    public static void SaveToken(string token)
    {
        if (!string.IsNullOrEmpty(token))
        {
            PlayerPrefs.SetString(TOKEN_KEY, token);
            PlayerPrefs.Save();
            Debug.Log("[AuthToken] SpacetimeDB token saved");
        }
    }
    
    /// <summary>
    /// Load SpacetimeDB authentication token
    /// </summary>
    public static string LoadToken()
    {
        return PlayerPrefs.GetString(TOKEN_KEY, null);
    }
    
    /// <summary>
    /// Clear SpacetimeDB token
    /// </summary>
    public static void ClearToken()
    {
        PlayerPrefs.DeleteKey(TOKEN_KEY);
        PlayerPrefs.Save();
        Debug.Log("[AuthToken] SpacetimeDB token cleared");
    }
    
    /// <summary>
    /// Check if a SpacetimeDB token is saved
    /// </summary>
    public static bool HasToken()
    {
        return PlayerPrefs.HasKey(TOKEN_KEY) && !string.IsNullOrEmpty(PlayerPrefs.GetString(TOKEN_KEY));
    }
    
    #endregion
    
    #region Device Management
    
    /// <summary>
    /// Get or create a unique device identifier
    /// </summary>
    public static string GetDeviceId()
    {
        if (!PlayerPrefs.HasKey(DEVICE_ID_KEY))
        {
            string deviceId = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(DEVICE_ID_KEY, deviceId);
            PlayerPrefs.Save();
        }
        return PlayerPrefs.GetString(DEVICE_ID_KEY);
    }
    
    /// <summary>
    /// Get device platform info
    /// </summary>
    public static string GetDeviceInfo()
    {
        #if UNITY_STANDALONE_WIN
            return "Windows";
        #elif UNITY_STANDALONE_OSX
            return "macOS";
        #elif UNITY_STANDALONE_LINUX
            return "Linux";
        #elif UNITY_WEBGL
            return "Web";
        #elif UNITY_IOS
            return "iOS";
        #elif UNITY_ANDROID
            return "Android";
        #elif UNITY_EDITOR
            return "Unity Editor";
        #else
            return "Unknown";
        #endif
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Clear all stored authentication data
    /// </summary>
    public static void ClearAll()
    {
        ClearSession();
        ClearToken();
        PlayerPrefs.DeleteKey(USERNAME_KEY);
        // Don't clear device ID - it should persist
        PlayerPrefs.Save();
        Debug.Log("[AuthToken] All auth data cleared");
    }
    
    /// <summary>
    /// Debug: Print all stored auth data
    /// </summary>
    public static void DebugPrintAuthData()
    {
        Debug.Log($"[AuthToken] Debug Info:");
        Debug.Log($"  - Has Session: {HasSession()}");
        Debug.Log($"  - Has Token: {HasToken()}");
        Debug.Log($"  - Last Username: {LoadLastUsername() ?? "None"}");
        Debug.Log($"  - Device ID: {GetDeviceId()}");
        Debug.Log($"  - Device Info: {GetDeviceInfo()}");
    }
    
    #endregion
}