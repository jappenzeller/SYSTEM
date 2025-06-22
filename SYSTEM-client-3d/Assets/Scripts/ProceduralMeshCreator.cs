using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralMeshCreator : MonoBehaviour
{
    [Header("Mesh Type")]
    public MeshType meshType = MeshType.Sphere;
    
    [Header("Common Settings")]
    public int resolution = 20;
    public float size = 1f;
    
    [Header("Sphere Settings")]
    public float sphereRadius = 1f;
    
    [Header("Terrain Settings")]
    public float terrainWidth = 10f;
    public float terrainDepth = 10f;
    public float noiseScale = 0.3f;
    public float heightMultiplier = 2f;
    
    [Header("Crystal Settings")]
    public int crystalSides = 6;
    public float crystalHeight = 2f;
    public float crystalRadius = 0.5f;
    
    [Header("Volcano Settings")]
    public float volcanoBaseRadius = 3f;
    public float volcanoTopRadius = 1f;
    public float volcanoHeight = 2f;
    public float craterDepth = 0.5f;
    
    private MeshFilter meshFilter;
    private Mesh mesh;
    private bool needsRegeneration = false;
    
    public enum MeshType
    {
        Sphere,
        Terrain,
        Crystal,
        Volcano,
        Cylinder,
        Torus
    }
    
    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
    }
    
    void Start()
    {
        GenerateMesh();
    }
    
    void Update()
    {
        // Handle mesh regeneration requested from OnValidate
        if (needsRegeneration && meshFilter != null)
        {
            GenerateMesh();
            needsRegeneration = false;
        }
    }
    
    [ContextMenu("Regenerate Mesh")]
    public void GenerateMesh()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null) return;
        }
        
        switch (meshType)
        {
            case MeshType.Sphere:
                mesh = CreateSphere(sphereRadius, resolution);
                break;
            case MeshType.Terrain:
                mesh = CreateTerrain(terrainWidth, terrainDepth, resolution);
                break;
            case MeshType.Crystal:
                mesh = CreateCrystal(crystalRadius, crystalHeight, crystalSides);
                break;
            case MeshType.Volcano:
                mesh = CreateVolcano(volcanoBaseRadius, volcanoTopRadius, volcanoHeight, craterDepth, resolution);
                break;
            case MeshType.Cylinder:
                mesh = CreateCylinder(size, size * 2f, resolution);
                break;
            case MeshType.Torus:
                mesh = CreateTorus(size, size * 0.3f, resolution, resolution / 2);
                break;
        }
        
        if (mesh != null)
        {
            meshFilter.mesh = mesh;
        }
    }
    
    #region Sphere Mesh
    Mesh CreateSphere(float radius, int segments)
    {
        Mesh sphereMesh = new Mesh();
        sphereMesh.name = "Procedural Sphere";
        
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        
        // Generate vertices
        for (int lat = 0; lat <= segments; lat++)
        {
            float theta = lat * Mathf.PI / segments;
            float sinTheta = Mathf.Sin(theta);
            float cosTheta = Mathf.Cos(theta);
            
            for (int lon = 0; lon <= segments; lon++)
            {
                float phi = lon * 2f * Mathf.PI / segments;
                float sinPhi = Mathf.Sin(phi);
                float cosPhi = Mathf.Cos(phi);
                
                Vector3 vertex = new Vector3(
                    radius * sinTheta * cosPhi,
                    radius * cosTheta,
                    radius * sinTheta * sinPhi
                );
                
                vertices.Add(vertex);
                normals.Add(vertex.normalized);
                uvs.Add(new Vector2((float)lon / segments, (float)lat / segments));
            }
        }
        
        // Generate triangles
        for (int lat = 0; lat < segments; lat++)
        {
            for (int lon = 0; lon < segments; lon++)
            {
                int current = lat * (segments + 1) + lon;
                int next = current + segments + 1;
                
                // First triangle
                triangles.Add(current);
                triangles.Add(next);
                triangles.Add(current + 1);
                
                // Second triangle
                triangles.Add(current + 1);
                triangles.Add(next);
                triangles.Add(next + 1);
            }
        }
        
        sphereMesh.vertices = vertices.ToArray();
        sphereMesh.triangles = triangles.ToArray();
        sphereMesh.normals = normals.ToArray();
        sphereMesh.uv = uvs.ToArray();
        sphereMesh.RecalculateBounds();
        
        return sphereMesh;
    }
    #endregion
    
    #region Terrain Mesh
    Mesh CreateTerrain(float width, float depth, int segments)
    {
        Mesh terrainMesh = new Mesh();
        terrainMesh.name = "Procedural Terrain";
        
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        
        // Generate vertices
        float stepX = width / segments;
        float stepZ = depth / segments;
        
        for (int z = 0; z <= segments; z++)
        {
            for (int x = 0; x <= segments; x++)
            {
                float xPos = x * stepX - width * 0.5f;
                float zPos = z * stepZ - depth * 0.5f;
                
                // Generate height using Perlin noise
                float height = Mathf.PerlinNoise(xPos * noiseScale, zPos * noiseScale) * heightMultiplier;
                
                vertices.Add(new Vector3(xPos, height, zPos));
                normals.Add(Vector3.up); // Will recalculate later
                uvs.Add(new Vector2((float)x / segments, (float)z / segments));
            }
        }
        
        // Generate triangles
        for (int z = 0; z < segments; z++)
        {
            for (int x = 0; x < segments; x++)
            {
                int current = z * (segments + 1) + x;
                int next = current + segments + 1;
                
                // First triangle
                triangles.Add(current);
                triangles.Add(next);
                triangles.Add(current + 1);
                
                // Second triangle
                triangles.Add(current + 1);
                triangles.Add(next);
                triangles.Add(next + 1);
            }
        }
        
        terrainMesh.vertices = vertices.ToArray();
        terrainMesh.triangles = triangles.ToArray();
        terrainMesh.uv = uvs.ToArray();
        terrainMesh.RecalculateNormals();
        terrainMesh.RecalculateBounds();
        
        return terrainMesh;
    }
    #endregion
    
    #region Crystal Mesh
    Mesh CreateCrystal(float radius, float height, int sides)
    {
        Mesh crystalMesh = new Mesh();
        crystalMesh.name = "Procedural Crystal";
        
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        
        // Top vertex
        vertices.Add(new Vector3(0, height * 0.5f, 0));
        
        // Bottom vertex
        vertices.Add(new Vector3(0, -height * 0.5f, 0));
        
        // Middle vertices
        for (int i = 0; i < sides; i++)
        {
            float angle = i * 2f * Mathf.PI / sides;
            vertices.Add(new Vector3(
                radius * Mathf.Cos(angle),
                0,
                radius * Mathf.Sin(angle)
            ));
        }
        
        // Top triangles
        for (int i = 0; i < sides; i++)
        {
            triangles.Add(0); // Top vertex
            triangles.Add(2 + i); // Current middle vertex
            triangles.Add(2 + (i + 1) % sides); // Next middle vertex
        }
        
        // Bottom triangles
        for (int i = 0; i < sides; i++)
        {
            triangles.Add(1); // Bottom vertex
            triangles.Add(2 + (i + 1) % sides); // Next middle vertex
            triangles.Add(2 + i); // Current middle vertex
        }
        
        crystalMesh.vertices = vertices.ToArray();
        crystalMesh.triangles = triangles.ToArray();
        crystalMesh.RecalculateNormals();
        crystalMesh.RecalculateBounds();
        
        return crystalMesh;
    }
    #endregion
    
    #region Volcano Mesh
    Mesh CreateVolcano(float baseRadius, float topRadius, float height, float craterDepth, int segments)
    {
        Mesh volcanoMesh = new Mesh();
        volcanoMesh.name = "Procedural Volcano";
        
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        
        // Generate vertices
        int levels = 5;
        for (int level = 0; level <= levels; level++)
        {
            float t = (float)level / levels;
            float y = t * height;
            float radius = Mathf.Lerp(baseRadius, topRadius, t);
            
            // Apply crater shape at the top
            if (level == levels)
            {
                y -= craterDepth;
                radius *= 0.7f;
            }
            
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * 2f * Mathf.PI / segments;
                vertices.Add(new Vector3(
                    radius * Mathf.Cos(angle),
                    y,
                    radius * Mathf.Sin(angle)
                ));
                uvs.Add(new Vector2((float)i / segments, t));
            }
        }
        
        // Generate triangles
        for (int level = 0; level < levels; level++)
        {
            for (int i = 0; i < segments; i++)
            {
                int current = level * (segments + 1) + i;
                int next = current + segments + 1;
                
                // First triangle
                triangles.Add(current);
                triangles.Add(next);
                triangles.Add(current + 1);
                
                // Second triangle
                triangles.Add(current + 1);
                triangles.Add(next);
                triangles.Add(next + 1);
            }
        }
        
        volcanoMesh.vertices = vertices.ToArray();
        volcanoMesh.triangles = triangles.ToArray();
        volcanoMesh.uv = uvs.ToArray();
        volcanoMesh.RecalculateNormals();
        volcanoMesh.RecalculateBounds();
        
        return volcanoMesh;
    }
    #endregion
    
    #region Cylinder Mesh
    Mesh CreateCylinder(float radius, float height, int segments)
    {
        Mesh cylinderMesh = new Mesh();
        cylinderMesh.name = "Procedural Cylinder";
        
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        
        // Bottom cap center
        vertices.Add(new Vector3(0, -height * 0.5f, 0));
        uvs.Add(new Vector2(0.5f, 0.5f));
        
        // Bottom cap edge
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            vertices.Add(new Vector3(
                radius * Mathf.Cos(angle),
                -height * 0.5f,
                radius * Mathf.Sin(angle)
            ));
            uvs.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(angle), 0.5f + 0.5f * Mathf.Sin(angle)));
        }
        
        // Top cap edge
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            vertices.Add(new Vector3(
                radius * Mathf.Cos(angle),
                height * 0.5f,
                radius * Mathf.Sin(angle)
            ));
            uvs.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(angle), 0.5f + 0.5f * Mathf.Sin(angle)));
        }
        
        // Top cap center
        vertices.Add(new Vector3(0, height * 0.5f, 0));
        uvs.Add(new Vector2(0.5f, 0.5f));
        
        // Bottom cap triangles
        for (int i = 0; i < segments; i++)
        {
            triangles.Add(0);
            triangles.Add(i + 2);
            triangles.Add(i + 1);
        }
        
        // Side triangles
        int bottomOffset = 1;
        int topOffset = segments + 2;
        for (int i = 0; i < segments; i++)
        {
            // First triangle
            triangles.Add(bottomOffset + i);
            triangles.Add(topOffset + i);
            triangles.Add(bottomOffset + i + 1);
            
            // Second triangle
            triangles.Add(bottomOffset + i + 1);
            triangles.Add(topOffset + i);
            triangles.Add(topOffset + i + 1);
        }
        
        // Top cap triangles
        int topCenterIndex = vertices.Count - 1;
        for (int i = 0; i < segments; i++)
        {
            triangles.Add(topCenterIndex);
            triangles.Add(topOffset + i);
            triangles.Add(topOffset + i + 1);
        }
        
        cylinderMesh.vertices = vertices.ToArray();
        cylinderMesh.triangles = triangles.ToArray();
        cylinderMesh.uv = uvs.ToArray();
        cylinderMesh.RecalculateNormals();
        cylinderMesh.RecalculateBounds();
        
        return cylinderMesh;
    }
    #endregion
    
    #region Torus Mesh
    Mesh CreateTorus(float radius, float tubeRadius, int segments, int tubeSegments)
    {
        Mesh torusMesh = new Mesh();
        torusMesh.name = "Procedural Torus";
        
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        
        // Generate vertices
        for (int i = 0; i <= segments; i++)
        {
            float u = (float)i / segments * 2f * Mathf.PI;
            Matrix4x4 transform = Matrix4x4.TRS(
                new Vector3(radius * Mathf.Cos(u), 0, radius * Mathf.Sin(u)),
                Quaternion.Euler(0, u * Mathf.Rad2Deg, 0),
                Vector3.one
            );
            
            for (int j = 0; j <= tubeSegments; j++)
            {
                float v = (float)j / tubeSegments * 2f * Mathf.PI;
                
                Vector3 localPoint = new Vector3(
                    tubeRadius * Mathf.Cos(v),
                    tubeRadius * Mathf.Sin(v),
                    0
                );
                
                Vector3 point = transform.MultiplyPoint3x4(localPoint);
                vertices.Add(point);
                
                Vector3 normal = (point - new Vector3(radius * Mathf.Cos(u), 0, radius * Mathf.Sin(u))).normalized;
                normals.Add(normal);
                
                uvs.Add(new Vector2((float)i / segments, (float)j / tubeSegments));
            }
        }
        
        // Generate triangles
        for (int i = 0; i < segments; i++)
        {
            for (int j = 0; j < tubeSegments; j++)
            {
                int current = i * (tubeSegments + 1) + j;
                int next = current + tubeSegments + 1;
                
                triangles.Add(current);
                triangles.Add(next);
                triangles.Add(current + 1);
                
                triangles.Add(current + 1);
                triangles.Add(next);
                triangles.Add(next + 1);
            }
        }
        
        torusMesh.vertices = vertices.ToArray();
        torusMesh.triangles = triangles.ToArray();
        torusMesh.normals = normals.ToArray();
        torusMesh.uv = uvs.ToArray();
        torusMesh.RecalculateBounds();
        
        return torusMesh;
    }
    #endregion
    
    void OnValidate()
    {
        // Clamp values to reasonable ranges
        resolution = Mathf.Clamp(resolution, 3, 100);
        size = Mathf.Max(0.1f, size);
        sphereRadius = Mathf.Max(0.1f, sphereRadius);
        terrainWidth = Mathf.Max(1f, terrainWidth);
        terrainDepth = Mathf.Max(1f, terrainDepth);
        noiseScale = Mathf.Max(0.01f, noiseScale);
        heightMultiplier = Mathf.Max(0f, heightMultiplier);
        crystalSides = Mathf.Clamp(crystalSides, 3, 20);
        crystalHeight = Mathf.Max(0.1f, crystalHeight);
        crystalRadius = Mathf.Max(0.1f, crystalRadius);
        volcanoBaseRadius = Mathf.Max(0.5f, volcanoBaseRadius);
        volcanoTopRadius = Mathf.Max(0.1f, volcanoTopRadius);
        volcanoHeight = Mathf.Max(0.5f, volcanoHeight);
        craterDepth = Mathf.Clamp(craterDepth, 0f, volcanoHeight * 0.8f);
        
        // Instead of directly calling GenerateMesh, set a flag
        if (Application.isPlaying)
        {
            needsRegeneration = true;
        }
        #if UNITY_EDITOR
        else if (!Application.isPlaying)
        {
            // In edit mode, use DelayCall to regenerate after OnValidate
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) // Check if object still exists
                {
                    if (meshFilter == null)
                    {
                        meshFilter = GetComponent<MeshFilter>();
                    }
                    
                    if (meshFilter != null)
                    {
                        GenerateMesh();
                    }
                }
            };
        }
        #endif
    }
    
    void OnDestroy()
    {
        // Clean up generated mesh
        if (mesh != null)
        {
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(mesh);
            }
            else
            #endif
            {
                Destroy(mesh);
            }
        }
    }
}