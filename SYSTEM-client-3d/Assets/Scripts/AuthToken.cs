// AuthToken.cs - Helper class for managing SpaceTimeDB authentication tokens
using UnityEngine;

public static class AuthToken
{
    private const string TOKEN_KEY = "SpacetimeDBToken";
    
    /// <summary>
    /// Save authentication token to PlayerPrefs
    /// </summary>
    public static void SaveToken(string token)
    {
        if (!string.IsNullOrEmpty(token))
        {
            PlayerPrefs.SetString(TOKEN_KEY, token);
            PlayerPrefs.Save();
            Debug.Log("[AuthToken] Token saved");
        }
    }
    
    /// <summary>
    /// Load authentication token from PlayerPrefs
    /// </summary>
    public static string LoadToken()
    {
        return PlayerPrefs.GetString(TOKEN_KEY, null);
    }
    
    /// <summary>
    /// Clear saved authentication token
    /// </summary>
    public static void ClearToken()
    {
        PlayerPrefs.DeleteKey(TOKEN_KEY);
        PlayerPrefs.Save();
        Debug.Log("[AuthToken] Token cleared");
    }
    
    /// <summary>
    /// Check if a token is saved
    /// </summary>
    public static bool HasToken()
    {
        return PlayerPrefs.HasKey(TOKEN_KEY);
    }
}