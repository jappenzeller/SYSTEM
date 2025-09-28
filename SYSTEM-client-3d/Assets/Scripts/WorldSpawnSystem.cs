using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Centralized spawn system for consistent player placement on worlds
/// </summary>
public class WorldSpawnSystem : MonoBehaviour
{
    [Header("Spawn Configuration")]
    [SerializeField] private float defaultSpawnHeight = 1.0f; // Height above surface
    [SerializeField] private float spawnSpreadRadius = 10f; // Spread players around spawn point
    [SerializeField] private bool useRandomSpawnPoints = true;
    [SerializeField] private Transform[] fixedSpawnPoints; // Optional fixed spawn locations
    
    [Header("References")]
    [SerializeField] private CenterWorldController worldController;
    [SerializeField] private SYSTEM.Game.PrefabWorldController prefabWorldController;
    
    private int nextSpawnIndex = 0;
    private float cachedWorldRadius = -1f;
    
    void Awake()
    {
        // Auto-find world controller if not assigned
        if (worldController == null && prefabWorldController == null)
        {
            // Try to find CenterWorldController first
            worldController = GetComponentInChildren<CenterWorldController>();
            
            if (worldController == null && transform.parent != null)
            {
                worldController = transform.parent.GetComponentInChildren<CenterWorldController>();
            }
            
            if (worldController == null)
            {
                worldController = FindFirstObjectByType<CenterWorldController>();
            }
            
            // If no CenterWorldController, try PrefabWorldController
            if (worldController == null)
            {
                prefabWorldController = GetComponentInChildren<SYSTEM.Game.PrefabWorldController>();
                
                if (prefabWorldController == null && transform.parent != null)
                {
                    prefabWorldController = transform.parent.GetComponentInChildren<SYSTEM.Game.PrefabWorldController>();
                }
                
                if (prefabWorldController == null)
                {
                    prefabWorldController = FindFirstObjectByType<SYSTEM.Game.PrefabWorldController>();
                }
            }
            
            if (worldController != null)
            {
                // Debug.Log($"[WorldSpawnSystem] Found CenterWorldController: {worldController.name}");
                cachedWorldRadius = worldController.Radius;
            }
            else if (prefabWorldController != null)
            {
                // Debug.Log($"[WorldSpawnSystem] Found PrefabWorldController: {prefabWorldController.name}");
                cachedWorldRadius = prefabWorldController.Radius;
            }
            else
            {
                // Debug.LogWarning("[WorldSpawnSystem] No world controller found, using default radius");
                cachedWorldRadius = 300f; // Default fallback
            }
        }
    }
    
    /// <summary>
    /// Get a spawn position for a new player
    /// </summary>
    public Vector3 GetSpawnPosition(bool isLocalPlayer = false)
    {
        // Get world parameters from whichever controller is available
        Vector3 centerPosition = GetWorldCenter();
        float radius = GetWorldRadius();
        
        if (radius <= 0)
        {
            // Debug.LogError("[WorldSpawnSystem] Invalid world radius!");
            return Vector3.up * 301f; // Fallback position
        }
        
        Vector3 spawnDirection;
        
        if (fixedSpawnPoints != null && fixedSpawnPoints.Length > 0)
        {
            // Use fixed spawn points
            Transform spawnPoint = fixedSpawnPoints[nextSpawnIndex % fixedSpawnPoints.Length];
            nextSpawnIndex++;
            spawnDirection = (spawnPoint.position - centerPosition).normalized;
        }
        else if (useRandomSpawnPoints)
        {
            // Random spawn around north pole with spread
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float spread = Random.Range(0f, spawnSpreadRadius);
            
            // Start from north pole (0,1,0) and add some horizontal spread
            Vector3 northPole = Vector3.up;
            Vector3 offset = new Vector3(
                Mathf.Sin(angle) * spread / radius,
                0,
                Mathf.Cos(angle) * spread / radius
            );
            
            spawnDirection = (northPole + offset).normalized;
        }
        else
        {
            // Default spawn at north pole
            spawnDirection = Vector3.up;
        }
        
        // Calculate spawn position using world parameters
        return SnapToSurface(centerPosition + spawnDirection * radius, defaultSpawnHeight);
    }
    
    /// <summary>
    /// Get the up vector for a spawn position
    /// </summary>
    public Vector3 GetSpawnUpVector(Vector3 spawnPosition)
    {
        return GetUpVector(spawnPosition);
    }
    
    /// <summary>
    /// Get spawn rotation for a player at a position
    /// </summary>
    public Quaternion GetSpawnRotation(Vector3 spawnPosition, float yawOffset = 0f)
    {
        Vector3 up = GetSpawnUpVector(spawnPosition);
        
        // Create a forward direction tangent to the sphere
        Vector3 forward = Vector3.Cross(up, Vector3.right);
        if (forward.magnitude < 0.1f)
        {
            forward = Vector3.Cross(up, Vector3.forward);
        }
        forward.Normalize();
        
        // Apply yaw offset if specified
        if (Mathf.Abs(yawOffset) > 0.01f)
        {
            forward = Quaternion.AngleAxis(yawOffset, up) * forward;
        }
        
        return Quaternion.LookRotation(forward, up);
    }
    
    /// <summary>
    /// Complete spawn setup for a player
    /// </summary>
    public void SetupPlayerSpawn(GameObject playerObject, bool isLocalPlayer = false)
    {
        if (playerObject == null) return;
        
        // Ensure we have a valid world controller reference
        if (worldController == null && prefabWorldController == null)
        {
            // Try one more time to find the controllers
            worldController = FindFirstObjectByType<CenterWorldController>();
            
            if (worldController == null)
            {
                prefabWorldController = FindFirstObjectByType<SYSTEM.Game.PrefabWorldController>();
            }
            
            if (worldController == null && prefabWorldController == null)
            {
                // Debug.LogError("[WorldSpawnSystem] Cannot spawn player - no world controller found!");
                // Fallback position
                playerObject.transform.position = new Vector3(0, 301f, 0);
                playerObject.transform.rotation = Quaternion.identity;
                return;
            }
        }
        
        // Get spawn position
        Vector3 spawnPos = GetSpawnPosition(isLocalPlayer);
        playerObject.transform.position = spawnPos;
        
        // Set rotation
        playerObject.transform.rotation = GetSpawnRotation(spawnPos);
        
        // Debug.Log($"[WorldSpawnSystem] Spawned player at {spawnPos} (distance from center: {spawnPos.magnitude})");
    }
    
    #region Unified World Access Methods
    
    /// <summary>
    /// Get world radius from whichever controller is available
    /// </summary>
    private float GetWorldRadius()
    {
        if (cachedWorldRadius > 0) return cachedWorldRadius;
        
        if (worldController != null)
        {
            cachedWorldRadius = worldController.Radius;
        }
        else if (prefabWorldController != null)
        {
            cachedWorldRadius = prefabWorldController.Radius;
        }
        else
        {
            cachedWorldRadius = 300f; // Default fallback
        }
        
        return cachedWorldRadius;
    }
    
    /// <summary>
    /// Get world center position from whichever controller is available
    /// </summary>
    private Vector3 GetWorldCenter()
    {
        if (worldController != null)
        {
            return worldController.CenterPosition;
        }
        else if (prefabWorldController != null)
        {
            return prefabWorldController.CenterPosition;
        }
        else
        {
            return Vector3.zero; // Default fallback
        }
    }
    
    /// <summary>
    /// Snap position to world surface using whichever controller is available
    /// </summary>
    private Vector3 SnapToSurface(Vector3 position, float heightOffset)
    {
        if (worldController != null)
        {
            return worldController.SnapToSurface(position, heightOffset);
        }
        else if (prefabWorldController != null)
        {
            return prefabWorldController.SnapToSurface(position, heightOffset);
        }
        else
        {
            // Fallback calculation
            Vector3 direction = position.normalized;
            return direction * (GetWorldRadius() + heightOffset);
        }
    }
    
    /// <summary>
    /// Get up vector at position using whichever controller is available
    /// </summary>
    private Vector3 GetUpVector(Vector3 worldPosition)
    {
        if (worldController != null)
        {
            return worldController.GetUpVector(worldPosition);
        }
        else if (prefabWorldController != null)
        {
            return prefabWorldController.GetUpVector(worldPosition);
        }
        else
        {
            // Fallback calculation
            return (worldPosition - GetWorldCenter()).normalized;
        }
    }
    
    #endregion
    
    void OnDrawGizmosSelected()
    {
        // Works with either controller type
        float radius = GetWorldRadius();
        Vector3 center = GetWorldCenter();
        
        if (radius <= 0) return;
        
        // Check if we have a valid controller
        bool hasValidController = (worldController != null || prefabWorldController != null);
        if (!hasValidController) return;
        
        // Visualize spawn points
        Gizmos.color = Color.green;
        
        if (fixedSpawnPoints != null)
        {
            foreach (var point in fixedSpawnPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, 1f);
                    Gizmos.DrawLine(center, point.position);
                }
            }
        }
        
        // Show spawn area
        if (useRandomSpawnPoints)
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            // Calculate north pole position safely
            Vector3 northPole = center + Vector3.up * radius;
            Gizmos.DrawWireSphere(northPole, spawnSpreadRadius);
        }
    }
}