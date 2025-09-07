using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Main controller for the center world sphere
/// Works with pre-configured prefab components instead of creating them at runtime
/// </summary>
public class CenterWorldController : MonoBehaviour
{
    [Header("Core Settings")]
    [Tooltip("The radius of the world sphere in world units")]
    [SerializeField] private float worldRadius = 300f;
    
    [Header("Visual Settings")]
    [SerializeField] private Material baseMaterial;
    [SerializeField] private Color primaryColor = new Color(0.2f, 0.3f, 0.5f);
    [SerializeField] private Color secondaryColor = new Color(0.3f, 0.4f, 0.6f);
    
    [Header("World Features")]
    [SerializeField] private bool enableRotation = false;
    [SerializeField] private float rotationSpeed = 1f;
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    
    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool showStats = false;
    
    // Components (retrieved from the prefab itself)
    private GameObject sphereObject;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    
    // Future extension points
    private List<IWorldFeature> worldFeatures = new List<IWorldFeature>();
    
    // Properties for external access
    public float Radius => worldRadius;
    public Vector3 CenterPosition => transform.position;
    
    void Awake()
    {
        UnityEngine.Debug.Log($"[CenterWorldController] Initializing {gameObject.name}");
        InitializeWorld();
    }
    
    void Start()
    {
        // Initialize any world features
        SetupWorldFeatures();
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
        UnityEngine.Debug.Log("[CenterWorldController] Initializing world components");
        
        // Get existing components from the prefab
        SetupComponents();
        
        // Apply initial settings
        ApplyWorldSettings();
        
        // Apply the correct scale based on world radius
        ApplyWorldScale();
    }
    
    void SetupComponents()
    {
        // The sphere object is this GameObject itself
        sphereObject = gameObject;
        
        // Get components that should already exist on the prefab
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
        
        // Validate components exist
        if (meshRenderer == null)
        {
            UnityEngine.Debug.LogError("[CenterWorldController] MeshRenderer component missing! Please add it to the prefab.");
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }
        
        if (meshFilter == null)
        {
            UnityEngine.Debug.LogError("[CenterWorldController] MeshFilter component missing! Please add it to the prefab.");
            meshFilter = gameObject.AddComponent<MeshFilter>();
            // Assign Unity's built-in sphere mesh as fallback
            GameObject tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meshFilter.sharedMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(tempSphere);
        }
        
        if (meshCollider == null)
        {
            UnityEngine.Debug.LogWarning("[CenterWorldController] MeshCollider component missing. Adding one for physics.");
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }
        
        // Ensure MeshCollider uses the same mesh as MeshFilter
        if (meshCollider != null && meshFilter != null && meshFilter.sharedMesh != null)
        {
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = false; // Non-convex for accurate collision
        }
        
        UnityEngine.Debug.Log($"[CenterWorldController] Components ready - MeshRenderer: {meshRenderer != null}, MeshFilter: {meshFilter != null}, MeshCollider: {meshCollider != null}");
    }
    
    void ApplyWorldScale()
    {
        // Unity's default sphere has diameter of 1, so scale by worldRadius * 2
        float targetScale = worldRadius * 2f;
        transform.localScale = Vector3.one * targetScale;
        
        UnityEngine.Debug.Log($"[CenterWorldController] World scale set to {targetScale} for radius {worldRadius}");
    }
    
    void ApplyWorldSettings()
    {
        if (meshRenderer == null) return;
        
        // Apply material
        if (baseMaterial != null)
        {
            meshRenderer.material = baseMaterial;
            meshRenderer.material.color = primaryColor;
        }
        else
        {
            // Create default material for WebGL compatibility
            Material defaultMat = new Material(Shader.Find("Unlit/Color"));
            if (defaultMat.shader == null)
            {
                // Fallback to standard shader if Unlit/Color not found
                defaultMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }
            defaultMat.color = primaryColor;
            meshRenderer.material = defaultMat;
            UnityEngine.Debug.Log("[CenterWorldController] Created default material");
        }
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
    /// Apply a different material to the world at runtime
    /// </summary>
    public void SetWorldMaterial(Material newMaterial)
    {
        if (newMaterial != null && meshRenderer != null)
        {
            baseMaterial = newMaterial;
            meshRenderer.material = newMaterial;
            meshRenderer.material.color = primaryColor;
            UnityEngine.Debug.Log($"[CenterWorldController] Applied new material: {newMaterial.name}");
        }
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
        
        UnityEngine.Debug.Log($"[CenterWorldController] World rotation {(enable ? "enabled" : "disabled")} (speed: {speed})");
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