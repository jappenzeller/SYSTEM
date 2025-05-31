using UnityEngine;

/// <summary>
/// Holds data that persists across scenes (e.g., the logged-in username).
/// </summary>
public class GameData : MonoBehaviour
{
    public static GameData Instance { get; private set; }

    /// <summary>Username entered on the login screen.</summary>
    public string Username { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Call once at login success to store the username.</summary>
    public void SetUsername(string name)
    {
        Username = name;
    }
}
