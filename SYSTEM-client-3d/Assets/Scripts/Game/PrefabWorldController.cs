using UnityEngine;
using System.Collections.Generic;
using SYSTEM.Debug;

namespace SYSTEM.Game
{
    /// <summary>
    /// Controller for prefab-based world spheres - WebGL-friendly alternative to procedural generation
    /// </summary>
    public class PrefabWorldController : MonoBehaviour
    {
        [Header("Prefab Settings")]
        [SerializeField] private GameObject worldSpherePrefab;
        [SerializeField] private float worldRadius = 300f;
        
        [Header("Material Settings")]
        [SerializeField] private Material defaultWorldMaterial;
        [SerializeField] private Color primaryColor = new Color(0.2f, 0.3f, 0.5f);
        
        [Header("World Features")]
        [SerializeField] private bool enableRotation = false;
        [SerializeField] private float rotationSpeed = 1f;
        [SerializeField] private Vector3 rotationAxis = Vector3.up;
        
        [Header("Debug")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private bool showStats = false;
        
        // Components
        private GameObject sphereInstance;
        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private MeshCollider meshCollider;
        
        // Properties for external access
        public float Radius => worldRadius;
        public Vector3 CenterPosition => transform.position;
        
        void Awake()
        {
            // UnityEngine.Debug.Log($"[PrefabWorldController.Awake] Starting initialization for {gameObject.name}");
            InitializeWorld();
        }
        
        void Update()
        {
            // Handle world rotation if enabled
            if (enableRotation && sphereInstance != null)
            {
                sphereInstance.transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);
            }
        }
        
        void InitializeWorld()
        {
            // UnityEngine.Debug.Log("[PrefabWorldController.InitializeWorld] Creating world sphere from prefab...");
            
            if (worldSpherePrefab != null)
            {
                CreateWorldFromPrefab();
            }
            else
            {
                // UnityEngine.Debug.LogWarning("[PrefabWorldController] No prefab assigned, creating fallback sphere");
                CreateFallbackSphere();
            }
            
            ApplyWorldSettings();
        }
        
        void CreateWorldFromPrefab()
        {
            // Instantiate the prefab
            sphereInstance = Instantiate(worldSpherePrefab, transform);
            sphereInstance.name = "WorldSphere";
            sphereInstance.transform.localPosition = Vector3.zero;
            
            // High-res sphere mesh has radius 1.0, so scale directly by worldRadius
            float targetScale = worldRadius;
            sphereInstance.transform.localScale = Vector3.one * targetScale;
            
            // Get components
            meshRenderer = sphereInstance.GetComponent<MeshRenderer>();
            meshFilter = sphereInstance.GetComponent<MeshFilter>();
            meshCollider = sphereInstance.GetComponent<MeshCollider>();
            
            // Ensure we have all required components
            if (meshRenderer == null)
            {
                meshRenderer = sphereInstance.AddComponent<MeshRenderer>();
            }
            if (meshFilter == null)
            {
                meshFilter = sphereInstance.AddComponent<MeshFilter>();
            }
            if (meshCollider == null)
            {
                meshCollider = sphereInstance.AddComponent<MeshCollider>();
                if (meshFilter.sharedMesh != null)
                {
                    meshCollider.sharedMesh = meshFilter.sharedMesh;
                    meshCollider.convex = false;
                }
            }
            
            // UnityEngine.Debug.Log($"[PrefabWorldController] Prefab sphere created with scale: {targetScale}");
        }
        
        void CreateFallbackSphere()
        {
            // Create Unity primitive sphere as fallback
            sphereInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereInstance.name = "WorldSphere (Fallback)";
            sphereInstance.transform.parent = transform;
            sphereInstance.transform.localPosition = Vector3.zero;
            
            // High-res sphere mesh has radius 1.0, so scale directly by worldRadius
            float targetScale = worldRadius;
            sphereInstance.transform.localScale = Vector3.one * targetScale;
            
            // Get the components from the primitive
            meshRenderer = sphereInstance.GetComponent<MeshRenderer>();
            meshFilter = sphereInstance.GetComponent<MeshFilter>();
            meshCollider = sphereInstance.GetComponent<MeshCollider>();
            
            // Replace the default collider with a MeshCollider for better accuracy
            if (meshCollider != null && meshCollider is SphereCollider)
            {
                Destroy(meshCollider);
                meshCollider = sphereInstance.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.convex = false;
            }
            
            // UnityEngine.Debug.Log($"[PrefabWorldController] Fallback primitive sphere created with scale: {targetScale}");
        }
        
        void ApplyWorldSettings()
        {
            if (meshRenderer == null) return;
            
            // Apply material
            if (defaultWorldMaterial != null)
            {
                meshRenderer.material = defaultWorldMaterial;
                meshRenderer.material.color = primaryColor;
            }
            else
            {
                // Create default material for WebGL compatibility
                Material defaultMat = new Material(Shader.Find("Unlit/Color"));
                if (defaultMat == null)
                {
                    // Fallback to standard shader if Unlit/Color not found
                    defaultMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                }
                defaultMat.color = primaryColor;
                meshRenderer.material = defaultMat;
                // UnityEngine.Debug.Log("[PrefabWorldController] Created default material");
            }
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
        
        /// <summary>
        /// Set the world prefab at runtime
        /// </summary>
        public void SetWorldPrefab(GameObject prefab)
        {
            worldSpherePrefab = prefab;
            
            // If already initialized, recreate with new prefab
            if (sphereInstance != null)
            {
                Destroy(sphereInstance);
                InitializeWorld();
            }
        }
        
        /// <summary>
        /// Update world radius at runtime
        /// </summary>
        public void SetWorldRadius(float radius)
        {
            worldRadius = Mathf.Max(10f, radius);
            
            if (sphereInstance != null)
            {
                float targetScale = worldRadius * 2f;
                sphereInstance.transform.localScale = Vector3.one * targetScale;
            }
        }
        
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
            GUI.Label(new Rect(10, y, 300, 20), "Mode: Prefab-based");
        }
        
        #endregion
    }
}
