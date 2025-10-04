using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Holds data that persists across scenes (e.g., the logged-in username, current world).
/// Extended to support world navigation and scene transitions.
/// </summary>
public class GameData : MonoBehaviour
{
    public static GameData Instance { get; private set; }

    [Header("Player Data")]
    /// <summary>Username entered on the login screen.</summary>
    public string Username { get; private set; }

    /// <summary>Current world coordinates where the player is located.</summary>
    public WorldCoords CurrentWorldCoords { get; private set; }

    /// <summary>Player's Identity in SpacetimeDB.</summary>
    public SpacetimeDB.Identity? PlayerIdentity { get; private set; }

    /// <summary>Currently selected crystal type for mining.</summary>
    public CrystalType SelectedCrystal { get; set; } = CrystalType.Red;

    [Header("Session State")]
    /// <summary>Whether the player has successfully logged into the game.</summary>
    public bool IsLoggedIn { get; private set; } = false;
    
    /// <summary>Whether we're in a transition between worlds.</summary>
    public bool IsTransitioning { get; private set; } = false;

    void Awake()
    {
        // WebGL Debug
        #if UNITY_WEBGL && !UNITY_EDITOR
        // Debug.Log($"[GameData] WebGL: Awake() called, Instance before = {Instance}");
        #endif

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Initialize with center world as default
        CurrentWorldCoords = new WorldCoords { X = 0, Y = 0, Z = 0 };

        #if UNITY_WEBGL && !UNITY_EDITOR
        // Debug.Log($"[GameData] WebGL: Awake() complete, Instance = {Instance}");
        #endif
    }

    #region Player Session Management

    /// <summary>Call once at login success to store the username.</summary>
    public void SetUsername(string name)
    {
        Username = name;
   //    // Debug.Log($"[GameData] Username set to: {name}");
    }

    /// <summary>Set the player's identity after successful connection.</summary>
    public void SetPlayerIdentity(SpacetimeDB.Identity identity)
    {
        PlayerIdentity = identity;
        IsLoggedIn = true;
   //     // Debug.Log($"[GameData] Player identity set, logged in: {identity}");
    }

    /// <summary>Clear session data (for logout).</summary>
    public void ClearSession()
    {
        Username = string.Empty;
        PlayerIdentity = null;
        IsLoggedIn = false;
        CurrentWorldCoords = new WorldCoords { X = 0, Y = 0, Z = 0 };
 //       // Debug.Log("[GameData] Session cleared");
    }

    #endregion

    #region World Navigation

    /// <summary>Set the current world coordinates.</summary>
/// <summary>Set the current world coordinates.</summary>
    public void SetCurrentWorldCoords(WorldCoords coords)
    {
        var oldCoords = CurrentWorldCoords;

        // Check if the world coordinates are actually changing
        if (oldCoords.X == coords.X && oldCoords.Y == coords.Y && oldCoords.Z == coords.Z)
        {
            // If not changing, no need to proceed or log/notify
            return; 
        }
        CurrentWorldCoords = coords;
        
  //      // Debug.Log($"[GameData] World changed from ({oldCoords.X},{oldCoords.Y},{oldCoords.Z}) to ({coords.X},{coords.Y},{coords.Z})");
        
        // Notify scene transition manager if it exists and is ready
        // Use try-catch to handle timing issues safely
        try
        {
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.OnPlayerWorldChanged(coords);
            }
            else
            {
    //            // Debug.Log("[GameData] SceneTransitionManager not available yet, will handle world change later");
            }
        }
        catch (System.Exception)
        {
    //        // Debug.LogWarning($"[GameData] Failed to notify SceneTransitionManager: {e.Message}");
            // Don't rethrow - this is not critical for game functionality
        }
    }

    /// <summary>Get the current world coordinates.</summary>
    public WorldCoords GetCurrentWorldCoords()
    {
        return CurrentWorldCoords;
    }

    /// <summary>Check if player is currently in the center world.</summary>
    public bool IsInCenterWorld()
    {
        return CurrentWorldCoords.X == 0 && CurrentWorldCoords.Y == 0 && CurrentWorldCoords.Z == 0;
    }

    /// <summary>Check if player is in a specific world.</summary>
    public bool IsInWorld(WorldCoords coords)
    {
        return CurrentWorldCoords.X == coords.X && 
               CurrentWorldCoords.Y == coords.Y && 
               CurrentWorldCoords.Z == coords.Z;
    }

    /// <summary>Set transition state.</summary>
    public void SetTransitioning(bool transitioning)
    {
        IsTransitioning = transitioning;
    }

    #endregion

    #region SpacetimeDB Integration

    /// <summary>Update world coordinates when player moves to a new world in SpacetimeDB.</summary>
    public void OnPlayerWorldUpdated(Player player)
    {
        if (PlayerIdentity.HasValue && player.Identity == PlayerIdentity.Value)
        {
            SetCurrentWorldCoords(player.CurrentWorld);
        }
    }

    /// <summary>Sync with SpacetimeDB player data.</summary>
    public void SyncWithPlayerData(Player player)
    {
        if (PlayerIdentity.HasValue && player.Identity == PlayerIdentity.Value)
        {
            SetUsername(player.Name);
            SetCurrentWorldCoords(player.CurrentWorld);
        }
    }

    #endregion

    #region Persistence (Optional - using PlayerPrefs)

    /// <summary>Save important data to PlayerPrefs for persistence across app restarts.</summary>
    public void SaveToPlayerPrefs()
    {
        if (!string.IsNullOrEmpty(Username))
        {
            PlayerPrefs.SetString("SavedUsername", Username);
        }
        
        // Save current world coords
        PlayerPrefs.SetInt("CurrentWorld_X", CurrentWorldCoords.X);
        PlayerPrefs.SetInt("CurrentWorld_Y", CurrentWorldCoords.Y);
        PlayerPrefs.SetInt("CurrentWorld_Z", CurrentWorldCoords.Z);
        
        PlayerPrefs.Save();
       // // Debug.Log("[GameData] Data saved to PlayerPrefs");
    }

    /// <summary>Load data from PlayerPrefs.</summary>
    public void LoadFromPlayerPrefs()
    {
        // Load username (will be used for auto-fill in login)
        if (PlayerPrefs.HasKey("SavedUsername"))
        {
            string savedUsername = PlayerPrefs.GetString("SavedUsername", "");
            if (!string.IsNullOrEmpty(savedUsername))
            {
                // Don't automatically set as logged in, just store for reference
                Username = savedUsername;
            }
        }
        
        // Load world coords (but don't auto-set since we need to verify with server)
        if (PlayerPrefs.HasKey("CurrentWorld_X"))
        {
            var savedCoords = new WorldCoords
            {
                X = (sbyte)PlayerPrefs.GetInt("CurrentWorld_X", 0),
                Y = (sbyte)PlayerPrefs.GetInt("CurrentWorld_Y", 0),
                Z = (sbyte)PlayerPrefs.GetInt("CurrentWorld_Z", 0)
            };
            // Store but don't act on it until server confirms
         //   // Debug.Log($"[GameData] Loaded saved world coords: ({savedCoords.X},{savedCoords.Y},{savedCoords.Z})");
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>Get a formatted string representation of current world.</summary>
    public string GetCurrentWorldString()
    {
        if (IsInCenterWorld())
        {
            return "Center World";
        }
        else
        {
            return $"World ({CurrentWorldCoords.X},{CurrentWorldCoords.Y},{CurrentWorldCoords.Z})";
        }
    }

    /// <summary>Get world shell level (0 for center, 1 for first shell, etc.).</summary>
    public int GetCurrentWorldShellLevel()
    {
        if (IsInCenterWorld())
        {
            return 0;
        }
        
        // Shell level is the maximum absolute coordinate
        return Mathf.Max(Mathf.Abs(CurrentWorldCoords.X), 
                        Mathf.Abs(CurrentWorldCoords.Y), 
                        Mathf.Abs(CurrentWorldCoords.Z));
    }

    /// <summary>Check if current world is a valid Shell 1 world.</summary>
    public bool IsInShell1World()
    {
        return GetCurrentWorldShellLevel() == 1;
    }

    #endregion

    void Start()
    {
        // Load any saved data
        LoadFromPlayerPrefs();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && IsLoggedIn)
        {
            SaveToPlayerPrefs();
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && IsLoggedIn)
        {
            SaveToPlayerPrefs();
        }
    }

    void OnDestroy()
    {
        if (IsLoggedIn)
        {
            SaveToPlayerPrefs();
        }
    }
}
