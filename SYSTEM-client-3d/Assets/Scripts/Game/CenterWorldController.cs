using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Simplified controller for the center world sphere
/// Handles only core world functionality (radius, coordinates, physics)
/// All visual aspects are handled by the prefab's MeshRenderer component
/// </summary>
public class CenterWorldController : MonoBehaviour
{
    [Header("Core Settings")]
    [Tooltip("The radius of the world sphere in world units")]
    [SerializeField] private float worldRadius = 300f;

    [Header("World Features")]
    [SerializeField] private bool enableRotation = false;
    [SerializeField] private float rotationSpeed = 1f;
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool showStats = false;

    // Components (retrieved from the prefab itself)
    private MeshCollider meshCollider;

    // Future extension points
    private List<IWorldFeature> worldFeatures = new List<IWorldFeature>();

    // Properties for external access
    public float Radius => worldRadius;
    public Vector3 CenterPosition => transform.position;

    void Awake()
    {
        // UnityEngine.Debug.Log($"[CenterWorldController] Initializing {gameObject.name}");

#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL-specific: Force correct scale immediately
        if (transform.localScale.magnitude < 100f)
        {
            // UnityEngine.Debug.LogWarning($"[WebGL Fix] Tiny scale detected: {transform.localScale} -> Forcing to {worldRadius}");
            transform.localScale = Vector3.one * worldRadius;
        }
#endif

        InitializeWorld();
    }

    void Start()
    {
        // Diagnostic logging for WebGL
        LogTransformDiagnostics();
        CheckForDuplicateWorlds();
        CreateBoundsVisualization();

        // Force position to origin
        transform.position = Vector3.zero;
        // UnityEngine.Debug.Log($"[WORLD POS] Forced to origin, now at: {transform.position}");

        // Initialize any world features
        SetupWorldFeatures();
    }

    void LogTransformDiagnostics()
    {
        // UnityEngine.Debug.Log($"[WORLD POS] World position: {transform.position}");
        // UnityEngine.Debug.Log($"[WORLD POS] World GLOBAL position: {transform.TransformPoint(Vector3.zero)}");
        // UnityEngine.Debug.Log($"[WORLD SCALE] localScale: {transform.localScale}, lossyScale: {transform.lossyScale}");

        // Log full hierarchy
        Transform current = transform;
        string hierarchy = gameObject.name;
        Vector3 cumulativeScale = transform.localScale;

        while (current.parent != null)
        {
            current = current.parent;
            hierarchy = current.name + " > " + hierarchy;
            cumulativeScale = Vector3.Scale(cumulativeScale, current.localScale);
        }

        // UnityEngine.Debug.Log($"[HIERARCHY] {hierarchy}");
        // UnityEngine.Debug.Log($"[CUMULATIVE SCALE] {cumulativeScale}");
    }

    void CheckForDuplicateWorlds()
    {
        CenterWorldController[] allWorlds = FindObjectsOfType<CenterWorldController>();
        // UnityEngine.Debug.Log($"[DUPLICATE CHECK] Found {allWorlds.Length} worlds in scene!");

        foreach (var world in allWorlds)
        {
            // UnityEngine.Debug.Log($"[WORLD] {world.name} at {world.transform.position} scale {world.transform.localScale}");
        }

        if (allWorlds.Length > 1)
        {
            // UnityEngine.Debug.LogError("Multiple worlds found! Destroying extras...");
            for (int i = 1; i < allWorlds.Length; i++)
            {
                Destroy(allWorlds[i].gameObject);
            }
        }
    }

    void CreateBoundsVisualization()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        GameObject lineObj = new GameObject("WorldBounds");
        LineRenderer line = lineObj.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.SetPosition(0, Vector3.zero);
        line.SetPosition(1, Vector3.up * worldRadius);
        line.startWidth = 10f;
        line.endWidth = 10f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = Color.red;
        line.endColor = Color.red;

        // UnityEngine.Debug.Log($"[BOUNDS] Created red line from origin to {worldRadius} units up");
#endif
    }

    void Update()
    {
        // Handle world rotation if enabled
        if (enableRotation)
        {
            transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);
        }

        // Update any active world features
        foreach (var feature in worldFeatures)
        {
            feature?.Update();
        }
    }

    void InitializeWorld()
    {
        // UnityEngine.Debug.Log("[CenterWorldController] Initializing world components");

        // Get physics components
        SetupPhysicsComponents();

        // Apply the correct scale based on world radius
        ApplyWorldScale();
    }

    void SetupPhysicsComponents()
    {
        // Get collider component that should already exist on the prefab
        meshCollider = GetComponent<MeshCollider>();

        if (meshCollider == null)
        {
            // UnityEngine.Debug.LogWarning("[CenterWorldController] MeshCollider component missing. Adding one for physics.");
            meshCollider = gameObject.AddComponent<MeshCollider>();

            // Try to get mesh from MeshFilter if it exists
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.convex = false; // Non-convex for accurate collision
            }
        }

        // UnityEngine.Debug.Log($"[CenterWorldController] Physics components ready - MeshCollider: {meshCollider != null}");
    }

    void ApplyWorldScale()
    {
        // High-res sphere mesh has radius 1.0, so scale directly by worldRadius
        float targetScale = worldRadius;

        // Get mesh bounds to calculate proper scale
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            float meshRadius = mf.sharedMesh.bounds.extents.magnitude;
            // UnityEngine.Debug.Log($"[CenterWorldController] Mesh radius: {meshRadius}, Target world radius: {worldRadius}");

            // For a unit sphere (radius 1.0), meshRadius should be ~1.732 (sqrt(3) for a cube containing the sphere)
            // Scale to achieve desired worldRadius
            if (meshRadius > 0.1f) // Sanity check
            {
                targetScale = worldRadius / meshRadius * 1.732f; // Adjust for bounds.extents
            }
        }

        transform.localScale = Vector3.one * targetScale;

        // UnityEngine.Debug.Log($"[CenterWorldController] World scale set to {targetScale} for radius {worldRadius}. Current scale: {transform.localScale}");

#if UNITY_WEBGL && !UNITY_EDITOR
        // Double-check scale was applied in WebGL
        // UnityEngine.Debug.Log($"[WebGL] Scale verification - localScale: {transform.localScale}, lossyScale: {transform.lossyScale}");
#endif
    }

    void SetupWorldFeatures()
    {
        // This is where we'll add world features later
        // For example:
        // - Terrain generation
        // - Biome system
        // - Atmosphere effects
        // - Day/night cycle
        // - Weather system
    }

    #region Core World Methods

    /// <summary>
    /// Get a point on the world surface given a direction
    /// </summary>
    public Vector3 GetSurfacePoint(Vector3 direction)
    {
        return transform.position + direction.normalized * worldRadius;
    }

    /// <summary>
    /// Get the up vector at a given world position
    /// </summary>
    public Vector3 GetUpVector(Vector3 worldPosition)
    {
        return (worldPosition - transform.position).normalized;
    }

    /// <summary>
    /// Snap a position to the world surface
    /// </summary>
    public Vector3 SnapToSurface(Vector3 position, float heightOffset = 0f)
    {
        Vector3 direction = (position - transform.position).normalized;
        return transform.position + direction * (worldRadius + heightOffset);
    }

    /// <summary>
    /// Check if a position is inside the world
    /// </summary>
    public bool IsInsideWorld(Vector3 position)
    {
        return Vector3.Distance(position, transform.position) < worldRadius;
    }

    #endregion

    #region Runtime Configuration

    /// <summary>
    /// Change world radius at runtime
    /// </summary>
    public void SetWorldRadius(float newRadius)
    {
        worldRadius = Mathf.Max(10f, newRadius);
        ApplyWorldScale();
    }

    /// <summary>
    /// Enable or disable world rotation
    /// </summary>
    public void SetWorldRotation(bool enable, float speed = 1f, Vector3? axis = null)
    {
        enableRotation = enable;
        rotationSpeed = speed;
        if (axis.HasValue)
        {
            rotationAxis = axis.Value.normalized;
        }

        // UnityEngine.Debug.Log($"[CenterWorldController] World rotation {(enable ? "enabled" : "disabled")} (speed: {speed})");
    }

    #endregion

    #region Future Extension Methods

    public void AddWorldFeature(IWorldFeature feature)
    {
        if (feature != null && !worldFeatures.Contains(feature))
        {
            worldFeatures.Add(feature);
            feature.Initialize(this);
        }
    }

    public void RemoveWorldFeature(IWorldFeature feature)
    {
        if (feature != null && worldFeatures.Contains(feature))
        {
            feature.Cleanup();
            worldFeatures.Remove(feature);
        }
    }

    #endregion

    #region Editor Support

    void OnValidate()
    {
        // Clamp values to reasonable ranges
        worldRadius = Mathf.Max(10f, worldRadius);
        rotationSpeed = Mathf.Clamp(rotationSpeed, -100f, 100f);
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Draw world boundary
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        Gizmos.DrawWireSphere(transform.position, worldRadius);
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        // Draw detailed world visualization
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, worldRadius);

        // Draw axis
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + transform.right * worldRadius * 1.2f);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + transform.up * worldRadius * 1.2f);
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * worldRadius * 1.2f);

        // Draw rotation axis if enabled
        if (enableRotation)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(
                transform.position - rotationAxis.normalized * worldRadius * 1.1f,
                transform.position + rotationAxis.normalized * worldRadius * 1.1f
            );
        }
    }

    void OnGUI()
    {
        if (!showStats || !Application.isPlaying) return;

        int y = 10;
        GUI.Label(new Rect(10, y, 300, 20), $"World Radius: {worldRadius:F1}");
        y += 20;
        GUI.Label(new Rect(10, y, 300, 20), $"Surface Area: {4 * Mathf.PI * worldRadius * worldRadius:F0}");
        y += 20;
        GUI.Label(new Rect(10, y, 300, 20), $"Active Features: {worldFeatures.Count}");
    }

    #endregion
}

/// <summary>
/// Interface for future world features
/// </summary>
public interface IWorldFeature
{
    void Initialize(CenterWorldController world);
    void Update();
    void Cleanup();
}
