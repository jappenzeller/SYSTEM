using UnityEngine;
using System.Collections.Generic;
using SYSTEM.Game;

/// <summary>
/// Main controller for the center world sphere
/// Designed to be extended with more features later
/// </summary>
public class CenterWorldController : MonoBehaviour
{
    [Header("World Prefab Settings")]
    [Tooltip("The prefab to use for the world sphere. Should have MeshFilter, MeshRenderer, and MeshCollider components.")]
    [SerializeField] private GameObject worldSpherePrefab;
    [Tooltip("Optional: ScriptableObject for managing multiple world types")]
    [SerializeField] private SYSTEM.Game.WorldPrefabManager worldPrefabManager;
    
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
        UnityEngine.Debug.LogError($"[CenterWorldController.Awake] Starting initialization for {gameObject.name}");
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
        UnityEngine.Debug.LogError("[CenterWorldController.InitializeWorld] Creating world sphere...");
        // Create the sphere object
        CreateWorldSphere();
        
        // Apply initial settings
        ApplyWorldSettings();
    }
    
    void CreateWorldSphere()
    {
        UnityEngine.Debug.Log("[CenterWorldController] Creating world sphere");
        
        // Validate prefab before using it
        if (worldSpherePrefab == null)
        {
            UnityEngine.Debug.LogWarning("[CenterWorldController] No world sphere prefab assigned! Creating fallback sphere.");
            UnityEngine.Debug.LogWarning("[CenterWorldController] Please assign a prefab to the worldSpherePrefab field in the inspector for better performance.");
            CreateFallbackSphere();
            return;
        }
        
        if (!ValidatePrefab(worldSpherePrefab))
        {
            UnityEngine.Debug.LogError("[CenterWorldController] Assigned prefab is invalid! Using fallback sphere.");
            CreateFallbackSphere();
            return;
        }
        
        UnityEngine.Debug.Log("[CenterWorldController] Using prefab system for world sphere");
        CreateWorldFromPrefab();
    }
    
    void CreateWorldFromPrefab()
    {
        try
        {
            UnityEngine.Debug.Log($"[CenterWorldController] Attempting to instantiate prefab: {worldSpherePrefab.name}");
            
            // Instantiate the prefab
            sphereObject = Instantiate(worldSpherePrefab, transform);
            
            // Validate instantiation succeeded
            if (sphereObject == null)
            {
                UnityEngine.Debug.LogError("[CenterWorldController] Failed to instantiate prefab - sphereObject is null! Falling back to primitive sphere.");
                CreateFallbackSphere();
                return;
            }
            
            sphereObject.name = "WorldSphere (Prefab)";
            sphereObject.transform.localPosition = Vector3.zero;
            
            // Unity's default sphere has diameter of 1, so scale by worldRadius * 2
            float targetScale = worldRadius * 2f;
            sphereObject.transform.localScale = Vector3.one * targetScale;
            
            UnityEngine.Debug.Log($"[CenterWorldController] Prefab instantiated successfully: {sphereObject.name}");
            
            // Get components and validate them
            meshRenderer = sphereObject.GetComponent<MeshRenderer>();
            meshFilter = sphereObject.GetComponent<MeshFilter>();
            
            if (meshRenderer == null)
            {
                UnityEngine.Debug.LogWarning("[CenterWorldController] Prefab missing MeshRenderer after instantiation, adding one");
                meshRenderer = sphereObject.AddComponent<MeshRenderer>();
            }
            
            if (meshFilter == null)
            {
                UnityEngine.Debug.LogWarning("[CenterWorldController] Prefab missing MeshFilter after instantiation, adding one");
                meshFilter = sphereObject.AddComponent<MeshFilter>();
                // Use Unity's default sphere mesh as fallback
                GameObject tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                meshFilter.sharedMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(tempSphere);
                UnityEngine.Debug.Log("[CenterWorldController] Added fallback sphere mesh to MeshFilter");
            }
            
            // Validate mesh exists
            if (meshFilter.sharedMesh == null)
            {
                UnityEngine.Debug.LogWarning("[CenterWorldController] Prefab MeshFilter has no mesh assigned, using fallback");
                GameObject tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                meshFilter.sharedMesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(tempSphere);
            }
            
            // Add or verify MeshCollider
            MeshCollider meshCollider = sphereObject.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                UnityEngine.Debug.Log("[CenterWorldController] Adding MeshCollider to prefab");
                meshCollider = sphereObject.AddComponent<MeshCollider>();
            }
            
            if (meshFilter.sharedMesh != null)
            {
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.convex = false;
            }
            
            // Success logging
            UnityEngine.Debug.Log($"[CenterWorldController] Prefab sphere created successfully!");
            UnityEngine.Debug.Log($"[CenterWorldController] - Scale: {targetScale}");
            UnityEngine.Debug.Log($"[CenterWorldController] - Mesh: {(meshFilter.sharedMesh != null ? meshFilter.sharedMesh.name : "null")}");
            UnityEngine.Debug.Log($"[CenterWorldController] - Vertex count: {(meshFilter.sharedMesh != null ? meshFilter.sharedMesh.vertexCount : 0)}");
            UnityEngine.Debug.Log($"[CenterWorldController] - Has MeshRenderer: {meshRenderer != null}");
            UnityEngine.Debug.Log($"[CenterWorldController] - Has MeshFilter: {meshFilter != null}");
            UnityEngine.Debug.Log($"[CenterWorldController] - Has MeshCollider: {meshCollider != null}");
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[CenterWorldController] Exception during prefab instantiation: {ex.Message}");
            UnityEngine.Debug.LogError($"[CenterWorldController] Stack trace: {ex.StackTrace}");
            UnityEngine.Debug.LogError("[CenterWorldController] Falling back to primitive sphere creation");
            CreateFallbackSphere();
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
    
    /// <summary>
    /// Switch to a different world type at runtime
    /// </summary>
    public void SwitchWorldType(string worldTypeName)
    {
        if (worldPrefabManager == null)
        {
            UnityEngine.Debug.LogError("[CenterWorldController] No WorldPrefabManager assigned for world type switching");
            return;
        }
        
        // Get the prefab for this world type
        GameObject newPrefab = worldPrefabManager.GetWorldPrefab(worldTypeName);
        if (newPrefab == null)
        {
            UnityEngine.Debug.LogError($"[CenterWorldController] No prefab found for world type: {worldTypeName}");
            return;
        }
        
        // Store current state
        bool wasRotating = enableRotation;
        Vector3 currentRotation = sphereObject != null ? sphereObject.transform.rotation.eulerAngles : Vector3.zero;
        
        // Destroy current world sphere
        if (sphereObject != null)
        {
            UnityEngine.Debug.Log($"[CenterWorldController] Destroying current world sphere to switch types");
            DestroyImmediate(sphereObject);
        }
        
        // Create new world with new prefab
        worldSpherePrefab = newPrefab;
        CreateWorldFromPrefab();
        
        // Apply world-specific settings
        Material worldMaterial = worldPrefabManager.GetWorldMaterial(worldTypeName);
        if (worldMaterial != null)
        {
            baseMaterial = worldMaterial;
        }
        
        float worldTypeRadius = worldPrefabManager.GetWorldRadius(worldTypeName);
        if (worldTypeRadius > 0 && Mathf.Abs(worldTypeRadius - worldRadius) > 0.1f)
        {
            SetWorldRadius(worldTypeRadius);
        }
        
        // Restore state
        if (sphereObject != null && wasRotating)
        {
            sphereObject.transform.rotation = Quaternion.Euler(currentRotation);
        }
        
        // Apply visual settings
        ApplyWorldSettings();
        
        UnityEngine.Debug.Log($"[CenterWorldController] Switched to world type: {worldTypeName}");
    }
    
    /// <summary>
    /// Change world radius at runtime
    /// </summary>
    public void SetWorldRadius(float newRadius)
    {
        worldRadius = Mathf.Max(10f, newRadius);
        
        if (sphereObject != null)
        {
            // Scale the object to match the new radius
            float targetScale = worldRadius * 2f;
            sphereObject.transform.localScale = Vector3.one * targetScale;
            UnityEngine.Debug.Log($"[CenterWorldController] World radius changed to {worldRadius} (scale: {targetScale})");
        }
    }
    
    /// <summary>
    /// Apply a different material to the world at runtime
    /// </summary>
    public void SetWorldMaterial(Material newMaterial)
    {
        if (newMaterial != null)
        {
            baseMaterial = newMaterial;
            ApplyWorldSettings();
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
    
    /// <summary>
    /// Recreate the world sphere (useful if prefab or settings changed)
    /// </summary>
    public void RegenerateWorld()
    {
        if (sphereObject != null)
        {
            UnityEngine.Debug.Log("[CenterWorldController] Destroying existing world sphere to regenerate");
            DestroyImmediate(sphereObject);
            sphereObject = null;
            meshFilter = null;
            meshRenderer = null;
        }
        
        // Recreate the world sphere
        CreateWorldSphere();
        ApplyWorldSettings();
        
        UnityEngine.Debug.Log("[CenterWorldController] World regenerated");
    }
    
    #endregion
    
    #region Editor Support
    
    void OnValidate()
    {
        // Clamp values to reasonable ranges
        worldRadius = Mathf.Max(10f, worldRadius);
        rotationSpeed = Mathf.Clamp(rotationSpeed, -100f, 100f);
        
        // Validate prefab settings
        if (worldSpherePrefab == null)
        {
            UnityEngine.Debug.LogWarning("[CenterWorldController.OnValidate] No worldSpherePrefab is assigned. Please assign a prefab to the worldSpherePrefab field in the inspector.");
        }
        else
        {
            // Validate the assigned prefab has required components
            ValidatePrefabInEditor(worldSpherePrefab);
        }
    }
    
    /// <summary>
    /// Editor-only validation that provides warnings without runtime overhead
    /// </summary>
    private void ValidatePrefabInEditor(GameObject prefab)
    {
        if (prefab == null) return;
        
        #if UNITY_EDITOR
        MeshRenderer prefabRenderer = prefab.GetComponent<MeshRenderer>();
        MeshFilter prefabFilter = prefab.GetComponent<MeshFilter>();
        
        if (prefabRenderer == null)
        {
            UnityEngine.Debug.LogWarning($"[CenterWorldController] Assigned prefab '{prefab.name}' is missing a MeshRenderer component. It will be added automatically at runtime.", this);
        }
        
        if (prefabFilter == null)
        {
            UnityEngine.Debug.LogWarning($"[CenterWorldController] Assigned prefab '{prefab.name}' is missing a MeshFilter component. It will be added automatically at runtime.", this);
        }
        else if (prefabFilter.sharedMesh == null)
        {
            UnityEngine.Debug.LogWarning($"[CenterWorldController] Assigned prefab '{prefab.name}' has a MeshFilter but no mesh is assigned. A fallback mesh will be used at runtime.", this);
        }
        #endif
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
    
    /// <summary>
    /// Validates that a prefab has the required components for use as a world sphere
    /// </summary>
    /// <param name="prefab">The prefab to validate</param>
    /// <returns>True if the prefab is valid, false otherwise</returns>
    private bool ValidatePrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            UnityEngine.Debug.LogError("[CenterWorldController.ValidatePrefab] Prefab is null");
            return false;
        }
        
        UnityEngine.Debug.Log($"[CenterWorldController.ValidatePrefab] Validating prefab: {prefab.name}");
        
        // Check for MeshRenderer (can be added if missing, but warn)
        MeshRenderer prefabRenderer = prefab.GetComponent<MeshRenderer>();
        if (prefabRenderer == null)
        {
            UnityEngine.Debug.LogWarning($"[CenterWorldController.ValidatePrefab] Prefab '{prefab.name}' is missing MeshRenderer component. Will be added automatically.");
        }
        
        // Check for MeshFilter (can be added if missing, but warn)
        MeshFilter prefabFilter = prefab.GetComponent<MeshFilter>();
        if (prefabFilter == null)
        {
            UnityEngine.Debug.LogWarning($"[CenterWorldController.ValidatePrefab] Prefab '{prefab.name}' is missing MeshFilter component. Will be added automatically.");
        }
        else
        {
            // If MeshFilter exists, check if it has a mesh
            if (prefabFilter.sharedMesh == null)
            {
                UnityEngine.Debug.LogWarning($"[CenterWorldController.ValidatePrefab] Prefab '{prefab.name}' MeshFilter has no mesh assigned. Fallback mesh will be used.");
            }
            else
            {
                UnityEngine.Debug.Log($"[CenterWorldController.ValidatePrefab] Prefab mesh found: {prefabFilter.sharedMesh.name} ({prefabFilter.sharedMesh.vertexCount} vertices)");
            }
        }
        
        UnityEngine.Debug.Log($"[CenterWorldController.ValidatePrefab] Prefab '{prefab.name}' validation passed");
        return true;
    }
    
    /// <summary>
    /// Creates a fallback Unity primitive sphere when procedural generation or prefab instantiation fails
    /// </summary>
    private void CreateFallbackSphere()
    {
        UnityEngine.Debug.Log("[CenterWorldController.CreateFallbackSphere] Creating fallback primitive sphere");
        
        // Clean up any existing sphere object
        if (sphereObject != null)
        {
            DestroyImmediate(sphereObject);
        }
        
        // Create fallback Unity primitive sphere
        sphereObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereObject.name = "WorldSphere (Fallback)";
        sphereObject.transform.parent = transform;
        sphereObject.transform.localPosition = Vector3.zero;
        
        // Unity's default sphere has diameter of 1, so scale by worldRadius * 2
        float targetScale = worldRadius * 2f;
        sphereObject.transform.localScale = Vector3.one * targetScale;
        
        // Get the components from the primitive
        meshRenderer = sphereObject.GetComponent<MeshRenderer>();
        meshFilter = sphereObject.GetComponent<MeshFilter>();
        
        UnityEngine.Debug.Log($"[CenterWorldController.CreateFallbackSphere] Fallback sphere created successfully with scale: {targetScale}");
        
        #if UNITY_WEBGL && !UNITY_EDITOR
        UnityEngine.Debug.Log("[CenterWorldController.WebGL] FALLBACK COMPLETE: Unity primitive sphere created successfully");
        UnityEngine.Debug.Log($"[CenterWorldController.WebGL] Fallback sphere name: {sphereObject.name}");
        UnityEngine.Debug.Log($"[CenterWorldController.WebGL] Fallback sphere scale: {sphereObject.transform.localScale}");
        UnityEngine.Debug.Log($"[CenterWorldController.WebGL] Target scale was: {targetScale}");
        UnityEngine.Debug.Log($"[CenterWorldController.WebGL] MeshRenderer exists: {meshRenderer != null}");
        UnityEngine.Debug.Log($"[CenterWorldController.WebGL] MeshFilter exists: {meshFilter != null}");
        if (meshFilter != null && meshFilter.mesh != null)
        {
            UnityEngine.Debug.Log($"[CenterWorldController.WebGL] Primitive mesh vertex count: {meshFilter.mesh.vertexCount}");
            UnityEngine.Debug.Log($"[CenterWorldController.WebGL] Primitive mesh bounds: {meshFilter.mesh.bounds}");
        }
        #else
        UnityEngine.Debug.Log($"[CenterWorldController] Fallback primitive sphere created with scale: {targetScale}");
        #endif
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