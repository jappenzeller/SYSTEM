using UnityEngine;

public static class AuthToken
{
    private const string TOKEN_KEY = "SpacetimeDB_AuthToken";
    private const string SESSION_TOKEN_KEY = "SpacetimeDB_SessionToken";
    private const string USERNAME_KEY = "SpacetimeDB_Username";
    
    // Auth token management (for SpacetimeDB connection)
    public static void SaveToken(string token)
    {
        PlayerPrefs.SetString(TOKEN_KEY, token);
        PlayerPrefs.Save();
        Debug.Log("[AuthToken] Saved auth token");
    }
    
    public static string LoadToken()
    {
        return PlayerPrefs.GetString(TOKEN_KEY, string.Empty);
    }
    
    public static void ClearToken()
    {
        PlayerPrefs.DeleteKey(TOKEN_KEY);
        PlayerPrefs.Save();
        Debug.Log("[AuthToken] Cleared auth token");
    }
    
    // Session management (for game session)
    public static void SaveSession(string sessionToken, string username)
    {
        PlayerPrefs.SetString(SESSION_TOKEN_KEY, sessionToken);
        PlayerPrefs.SetString(USERNAME_KEY, username);
        PlayerPrefs.Save();
        Debug.Log($"[AuthToken] Saved session for user: {username}");
    }
    
    public static string LoadSessionToken()
    {
        return PlayerPrefs.GetString(SESSION_TOKEN_KEY, string.Empty);
    }
    
    public static string LoadUsername()
    {
        return PlayerPrefs.GetString(USERNAME_KEY, string.Empty);
    }
    
    public static void ClearSession()
    {
        PlayerPrefs.DeleteKey(SESSION_TOKEN_KEY);
        PlayerPrefs.DeleteKey(USERNAME_KEY);
        PlayerPrefs.Save();
        Debug.Log("[AuthToken] Cleared session");
    }
    
    public static bool HasSavedSession()
    {
        return !string.IsNullOrEmpty(LoadSessionToken());
    }
    
    public static void ClearAll()
    {
        ClearToken();
        ClearSession();
    }
}