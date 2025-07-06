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
    
    private int nextSpawnIndex = 0;
    
    void Awake()
    {
        // Auto-find world controller if not assigned
        if (worldController == null)
        {
            worldController = GetComponentInChildren<CenterWorldController>();
        }
    }
    
    /// <summary>
    /// Get a spawn position for a new player
    /// </summary>
    public Vector3 GetSpawnPosition(bool isLocalPlayer = false)
    {
        if (worldController == null)
        {
            Debug.LogError("[WorldSpawnSystem] No world controller found!");
            return Vector3.up * 301f; // Fallback position
        }
        
        Vector3 spawnDirection;
        
        if (fixedSpawnPoints != null && fixedSpawnPoints.Length > 0)
        {
            // Use fixed spawn points
            Transform spawnPoint = fixedSpawnPoints[nextSpawnIndex % fixedSpawnPoints.Length];
            nextSpawnIndex++;
            spawnDirection = (spawnPoint.position - worldController.CenterPosition).normalized;
        }
        else if (useRandomSpawnPoints)
        {
            // Random spawn around north pole with spread
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float spread = Random.Range(0f, spawnSpreadRadius);
            
            // Start from north pole (0,1,0) and add some horizontal spread
            Vector3 northPole = Vector3.up;
            Vector3 offset = new Vector3(
                Mathf.Sin(angle) * spread / worldController.Radius,
                0,
                Mathf.Cos(angle) * spread / worldController.Radius
            );
            
            spawnDirection = (northPole + offset).normalized;
        }
        else
        {
            // Default spawn at north pole
            spawnDirection = Vector3.up;
        }
        
        // Use world controller's snap to surface method
        return worldController.SnapToSurface(
            worldController.CenterPosition + spawnDirection * worldController.Radius,
            defaultSpawnHeight
        );
    }
    
    /// <summary>
    /// Get the up vector for a spawn position
    /// </summary>
    public Vector3 GetSpawnUpVector(Vector3 spawnPosition)
    {
        if (worldController == null) return Vector3.up;
        
        return worldController.GetUpVector(spawnPosition);
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
        
        // Get spawn position
        Vector3 spawnPos = GetSpawnPosition(isLocalPlayer);
        playerObject.transform.position = spawnPos;
        
        // Set rotation
        playerObject.transform.rotation = GetSpawnRotation(spawnPos);
        
        Debug.Log($"[WorldSpawnSystem] Spawned player at {spawnPos} (distance from center: {spawnPos.magnitude})");
    }
    
    void OnDrawGizmosSelected()
    {
        if (worldController == null) return;
        
        // Visualize spawn points
        Gizmos.color = Color.green;
        
        if (fixedSpawnPoints != null)
        {
            foreach (var point in fixedSpawnPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, 1f);
                    Gizmos.DrawLine(worldController.CenterPosition, point.position);
                }
            }
        }
        
        // Show spawn area
        if (useRandomSpawnPoints)
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Vector3 northPole = worldController.GetSurfacePoint(Vector3.up);
            Gizmos.DrawWireSphere(northPole, spawnSpreadRadius);
        }
    }
}