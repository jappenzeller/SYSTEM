using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// [DEPRECATED] - Use prefab-based world system instead for better WebGL compatibility.
/// This class was previously used to generate procedural icosphere meshes for world spheres.
/// Kept for backward compatibility but should not be used in new code.
/// See: WorldController now uses prefab-based world spheres.
/// </summary>
[System.Obsolete("ProceduralSphereGenerator is deprecated. Use prefab-based world spheres for better WebGL compatibility and performance.")]
public static class ProceduralSphereGenerator
{
    // Cache for generated meshes to avoid regeneration
    private static Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
    
    /// <summary>
    /// Generate an icosphere mesh with specified radius and subdivision level.
    /// </summary>
    /// <param name="radius">Exact radius of the sphere</param>
    /// <param name="subdivisions">Number of subdivision iterations (0-6 recommended)</param>
    /// <param name="cacheMesh">Whether to cache the generated mesh</param>
    /// <returns>Generated icosphere mesh</returns>
    public static Mesh GenerateIcosphere(float radius, int subdivisions, bool cacheMesh = true)
    {
        // Check cache first
        string cacheKey = $"icosphere_{radius}_{subdivisions}";
        if (cacheMesh && meshCache.ContainsKey(cacheKey))
        {
            // Debug.Log($"[ProceduralSphere] Using cached mesh: {cacheKey}");
            return meshCache[cacheKey];
        }
        
        // Start timing
        float startTime = Time.realtimeSinceStartup;
        
        // Clamp subdivisions to reasonable range
        subdivisions = Mathf.Clamp(subdivisions, 0, 6);
        
        // Create base icosahedron
        List<Vector3> vertices;
        List<int> triangles;
        CreateIcosahedron(radius, out vertices, out triangles);
        
        // Apply subdivisions
        for (int i = 0; i < subdivisions; i++)
        {
            SubdivideMesh(ref vertices, ref triangles, radius);
        }
        
        // Generate UV coordinates
        Vector2[] uvs = GenerateUVCoordinates(vertices);
        
        // Calculate normals (all point outward from center)
        Vector3[] normals = CalculateNormals(vertices);
        
        // Create the mesh
        Mesh mesh = new Mesh();
        mesh.name = $"ProceduralIcosphere_R{radius}_S{subdivisions}";
        
        // Use 32-bit indices for large meshes
        if (vertices.Count > 65535)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }
        
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs;
        mesh.normals = normals;
        
        // Optimize mesh
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        mesh.Optimize();
        
        // Validate mesh
        ValidateMesh(mesh, radius);
        
        // Log statistics
        float generationTime = (Time.realtimeSinceStartup - startTime) * 1000f;
        Debug.Log($"[ProceduralSphere] Generated icosphere: " +
                  $"Radius={radius}, Subdivisions={subdivisions}, " +
                  $"Vertices={vertices.Count}, Triangles={triangles.Count / 3}, " +
                  $"Time={generationTime:F2}ms");
        
        // Cache if requested
        if (cacheMesh)
        {
            meshCache[cacheKey] = mesh;
        }
        
        return mesh;
    }
    
    /// <summary>
    /// Create the base icosahedron with 12 vertices and 20 triangles.
    /// </summary>
    private static void CreateIcosahedron(float radius, out List<Vector3> vertices, out List<int> triangles)
    {
        vertices = new List<Vector3>();
        triangles = new List<int>();
        
        // Golden ratio
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        
        // Create 12 vertices of icosahedron
        // Normalize to radius after creation
        vertices.Add(NormalizeToRadius(new Vector3(-1, t, 0), radius));
        vertices.Add(NormalizeToRadius(new Vector3(1, t, 0), radius));
        vertices.Add(NormalizeToRadius(new Vector3(-1, -t, 0), radius));
        vertices.Add(NormalizeToRadius(new Vector3(1, -t, 0), radius));
        
        vertices.Add(NormalizeToRadius(new Vector3(0, -1, t), radius));
        vertices.Add(NormalizeToRadius(new Vector3(0, 1, t), radius));
        vertices.Add(NormalizeToRadius(new Vector3(0, -1, -t), radius));
        vertices.Add(NormalizeToRadius(new Vector3(0, 1, -t), radius));
        
        vertices.Add(NormalizeToRadius(new Vector3(t, 0, -1), radius));
        vertices.Add(NormalizeToRadius(new Vector3(t, 0, 1), radius));
        vertices.Add(NormalizeToRadius(new Vector3(-t, 0, -1), radius));
        vertices.Add(NormalizeToRadius(new Vector3(-t, 0, 1), radius));
        
        // Create 20 triangles of icosahedron
        // 5 faces around point 0
        triangles.AddRange(new int[] { 0, 11, 5 });
        triangles.AddRange(new int[] { 0, 5, 1 });
        triangles.AddRange(new int[] { 0, 1, 7 });
        triangles.AddRange(new int[] { 0, 7, 10 });
        triangles.AddRange(new int[] { 0, 10, 11 });
        
        // 5 adjacent faces
        triangles.AddRange(new int[] { 1, 5, 9 });
        triangles.AddRange(new int[] { 5, 11, 4 });
        triangles.AddRange(new int[] { 11, 10, 2 });
        triangles.AddRange(new int[] { 10, 7, 6 });
        triangles.AddRange(new int[] { 7, 1, 8 });
        
        // 5 faces around point 3
        triangles.AddRange(new int[] { 3, 9, 4 });
        triangles.AddRange(new int[] { 3, 4, 2 });
        triangles.AddRange(new int[] { 3, 2, 6 });
        triangles.AddRange(new int[] { 3, 6, 8 });
        triangles.AddRange(new int[] { 3, 8, 9 });
        
        // 5 adjacent faces
        triangles.AddRange(new int[] { 4, 9, 5 });
        triangles.AddRange(new int[] { 2, 4, 11 });
        triangles.AddRange(new int[] { 6, 2, 10 });
        triangles.AddRange(new int[] { 8, 6, 7 });
        triangles.AddRange(new int[] { 9, 8, 1 });
    }
    
    /// <summary>
    /// Subdivide the mesh by splitting each triangle into 4 smaller triangles.
    /// </summary>
    private static void SubdivideMesh(ref List<Vector3> vertices, ref List<int> triangles, float radius)
    {
        // Dictionary to cache midpoint vertices
        Dictionary<long, int> midpointCache = new Dictionary<long, int>();
        
        List<int> newTriangles = new List<int>();
        
        // Process each triangle
        for (int i = 0; i < triangles.Count; i += 3)
        {
            int v0 = triangles[i];
            int v1 = triangles[i + 1];
            int v2 = triangles[i + 2];
            
            // Get or create midpoint vertices
            int m01 = GetMidpointVertex(v0, v1, ref vertices, ref midpointCache, radius);
            int m12 = GetMidpointVertex(v1, v2, ref vertices, ref midpointCache, radius);
            int m20 = GetMidpointVertex(v2, v0, ref vertices, ref midpointCache, radius);
            
            // Create 4 new triangles
            newTriangles.AddRange(new int[] { v0, m01, m20 });
            newTriangles.AddRange(new int[] { v1, m12, m01 });
            newTriangles.AddRange(new int[] { v2, m20, m12 });
            newTriangles.AddRange(new int[] { m01, m12, m20 });
        }
        
        triangles = newTriangles;
    }
    
    /// <summary>
    /// Get or create a midpoint vertex between two vertices.
    /// </summary>
    private static int GetMidpointVertex(int v0, int v1, ref List<Vector3> vertices, 
                                         ref Dictionary<long, int> cache, float radius)
    {
        // Create unique key for this edge
        long key = ((long)Mathf.Min(v0, v1) << 32) + Mathf.Max(v0, v1);
        
        // Check if midpoint already exists
        if (cache.ContainsKey(key))
        {
            return cache[key];
        }
        
        // Create new midpoint vertex
        Vector3 midpoint = (vertices[v0] + vertices[v1]) / 2f;
        midpoint = NormalizeToRadius(midpoint, radius);
        
        int newIndex = vertices.Count;
        vertices.Add(midpoint);
        cache[key] = newIndex;
        
        return newIndex;
    }
    
    /// <summary>
    /// Normalize a vertex to be exactly radius distance from origin.
    /// </summary>
    private static Vector3 NormalizeToRadius(Vector3 vertex, float radius)
    {
        return vertex.normalized * radius;
    }
    
    /// <summary>
    /// Generate UV coordinates for sphere vertices using spherical mapping.
    /// </summary>
    private static Vector2[] GenerateUVCoordinates(List<Vector3> vertices)
    {
        Vector2[] uvs = new Vector2[vertices.Count];
        
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 v = vertices[i].normalized;
            
            // Calculate spherical coordinates
            float phi = Mathf.Atan2(v.z, v.x);
            float theta = Mathf.Asin(v.y);
            
            // Convert to UV coordinates
            float u = (phi + Mathf.PI) / (2f * Mathf.PI);
            float v_coord = (theta + Mathf.PI / 2f) / Mathf.PI;
            
            uvs[i] = new Vector2(u, v_coord);
        }
        
        return uvs;
    }
    
    /// <summary>
    /// Calculate normals for all vertices (pointing outward from center).
    /// </summary>
    private static Vector3[] CalculateNormals(List<Vector3> vertices)
    {
        Vector3[] normals = new Vector3[vertices.Count];
        
        for (int i = 0; i < vertices.Count; i++)
        {
            normals[i] = vertices[i].normalized;
        }
        
        return normals;
    }
    
    /// <summary>
    /// Validate that all vertices are exactly radius distance from origin.
    /// </summary>
    private static void ValidateMesh(Mesh mesh, float radius)
    {
        Vector3[] vertices = mesh.vertices;
        float maxError = 0f;
        int errorCount = 0;
        
        for (int i = 0; i < vertices.Length; i++)
        {
            float distance = vertices[i].magnitude;
            float error = Mathf.Abs(distance - radius);
            
            if (error > 0.0001f) // Tolerance for floating point errors
            {
                errorCount++;
                maxError = Mathf.Max(maxError, error);
            }
        }
        
        if (errorCount > 0)
        {
            // Debug.LogWarning($"[ProceduralSphere] Validation warning: {errorCount} vertices have distance errors. Max error: {maxError}");
        }
        else
        {
            // Debug.Log($"[ProceduralSphere] Validation passed: All vertices are exactly {radius} units from origin");
        }
        
        // Log bounds
        // Debug.Log($"[ProceduralSphere] Mesh bounds: Center={mesh.bounds.center}, Size={mesh.bounds.size}");
    }
    
    /// <summary>
    /// Clear the mesh cache to free memory.
    /// </summary>
    public static void ClearCache()
    {
        meshCache.Clear();
        // Debug.Log("[ProceduralSphere] Mesh cache cleared");
    }
    
    /// <summary>
    /// Get recommended subdivision level based on platform and performance requirements.
    /// </summary>
    public static int GetRecommendedSubdivisions(RuntimePlatform platform)
    {
        switch (platform)
        {
            case RuntimePlatform.WebGLPlayer:
                return 3; // Lower for WebGL performance
            case RuntimePlatform.Android:
            case RuntimePlatform.IPhonePlayer:
                return 4; // Mobile optimization
            case RuntimePlatform.WindowsPlayer:
            case RuntimePlatform.OSXPlayer:
            case RuntimePlatform.LinuxPlayer:
                return 5; // Desktop can handle more
            case RuntimePlatform.WindowsEditor:
            case RuntimePlatform.OSXEditor:
            case RuntimePlatform.LinuxEditor:
                return 5; // Editor testing
            default:
                return 4; // Safe default
        }
    }
}
