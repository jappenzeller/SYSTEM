using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Main controller for the center world sphere
/// Designed to be extended with more features later
/// </summary>
public class CenterWorldController : MonoBehaviour
{
    [Header("Core Settings")]
    [SerializeField] private float worldRadius = 300f;
    [SerializeField] private int meshResolution = 32; // For future procedural mesh
    
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
    
    // Components
    private GameObject sphereObject;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    
    // Future extension points
    private List<IWorldFeature> worldFeatures = new List<IWorldFeature>();
    
    // Properties for external access
    public float Radius => worldRadius;
    public Vector3 CenterPosition => transform.position;
    
    void Awake()
    {
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
        if (enableRotation && sphereObject != null)
        {
            sphereObject.transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);
        }
        
        // Update any active world features
        foreach (var feature in worldFeatures)
        {
            feature?.Update();
        }
    }
    
    void InitializeWorld()
    {
        // Create the sphere object
        CreateWorldSphere();
        
        // Apply initial settings
        ApplyWorldSettings();
    }
    
    void CreateWorldSphere()
    {
        // For now, use Unity primitive
        // Later can be replaced with procedural mesh
        sphereObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereObject.name = "WorldSphere";
        sphereObject.transform.parent = transform;
        sphereObject.transform.localPosition = Vector3.zero;
        
        // Scale to world size (primitive sphere has diameter of 1)
        sphereObject.transform.localScale = Vector3.one * (worldRadius * 2f);
        
        // Get components
        meshRenderer = sphereObject.GetComponent<MeshRenderer>();
        meshFilter = sphereObject.GetComponent<MeshFilter>();
        
        // Remove collider for now (add back later if needed)
        Collider col = sphereObject.GetComponent<Collider>();
        if (col != null)
        {
            DestroyImmediate(col);
        }
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
            // Create default material
            Material defaultMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            defaultMat.color = primaryColor;
            meshRenderer.material = defaultMat;
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
    
    public void RegenerateMesh()
    {
        // Future: Implement procedural mesh generation
        Debug.Log("Procedural mesh generation not yet implemented");
    }
    
    #endregion
    
    #region Editor Support
    
    void OnValidate()
    {
        // Clamp values to reasonable ranges
        worldRadius = Mathf.Max(10f, worldRadius);
        meshResolution = Mathf.Clamp(meshResolution, 8, 128);
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