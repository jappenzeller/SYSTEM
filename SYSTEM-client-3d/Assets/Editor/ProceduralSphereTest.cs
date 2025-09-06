using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utility to test procedural sphere generation
/// </summary>
public static class ProceduralSphereTest
{
    [MenuItem("Tools/Test Procedural Sphere/Small (R=10, S=2)")]
    static void TestSmallSphere()
    {
        CreateTestSphere(10f, 2, "TestSphere_Small");
    }
    
    [MenuItem("Tools/Test Procedural Sphere/Medium (R=100, S=3)")]
    static void TestMediumSphere()
    {
        CreateTestSphere(100f, 3, "TestSphere_Medium");
    }
    
    [MenuItem("Tools/Test Procedural Sphere/Large (R=300, S=4)")]
    static void TestLargeSphere()
    {
        CreateTestSphere(300f, 4, "TestSphere_Large");
    }
    
    [MenuItem("Tools/Test Procedural Sphere/World Size (R=5000, S=4)")]
    static void TestWorldSphere()
    {
        CreateTestSphere(5000f, 4, "TestSphere_World");
    }
    
    [MenuItem("Tools/Test Procedural Sphere/High Detail (R=100, S=5)")]
    static void TestHighDetailSphere()
    {
        CreateTestSphere(100f, 5, "TestSphere_HighDetail");
    }
    
    [MenuItem("Tools/Test Procedural Sphere/Clear Cache")]
    static void ClearSphereCache()
    {
        ProceduralSphereGenerator.ClearCache();
        Debug.Log("[ProceduralSphereTest] Cache cleared");
    }
    
    static void CreateTestSphere(float radius, int subdivisions, string name)
    {
        // Generate the mesh
        Mesh sphereMesh = ProceduralSphereGenerator.GenerateIcosphere(radius, subdivisions, false);
        
        if (sphereMesh == null)
        {
            Debug.LogError($"[ProceduralSphereTest] Failed to generate sphere!");
            return;
        }
        
        // Create GameObject
        GameObject sphereObj = new GameObject(name);
        
        // Add components
        MeshFilter meshFilter = sphereObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = sphereObj.AddComponent<MeshRenderer>();
        
        // Set mesh
        meshFilter.mesh = sphereMesh;
        
        // Create a simple material
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(
            Random.Range(0.5f, 1f),
            Random.Range(0.5f, 1f),
            Random.Range(0.5f, 1f)
        );
        meshRenderer.material = mat;
        
        // Position at origin
        sphereObj.transform.position = Vector3.zero;
        
        // Select the object
        Selection.activeGameObject = sphereObj;
        
        // Focus camera on it
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
        }
        
        Debug.Log($"[ProceduralSphereTest] Created sphere: {name}");
        Debug.Log($"  Radius: {radius}");
        Debug.Log($"  Subdivisions: {subdivisions}");
        Debug.Log($"  Vertices: {sphereMesh.vertexCount}");
        Debug.Log($"  Triangles: {sphereMesh.triangles.Length / 3}");
        Debug.Log($"  Bounds: {sphereMesh.bounds}");
        
        // Validate radius
        Vector3[] vertices = sphereMesh.vertices;
        float minDist = float.MaxValue;
        float maxDist = float.MinValue;
        
        foreach (var vertex in vertices)
        {
            float dist = vertex.magnitude;
            minDist = Mathf.Min(minDist, dist);
            maxDist = Mathf.Max(maxDist, dist);
        }
        
        Debug.Log($"  Radius validation: Min={minDist:F4}, Max={maxDist:F4}, Target={radius}");
        float error = Mathf.Max(Mathf.Abs(minDist - radius), Mathf.Abs(maxDist - radius));
        if (error > 0.001f)
        {
            Debug.LogWarning($"  Radius error: {error:F6}");
        }
        else
        {
            Debug.Log($"  ✓ Radius is exact within tolerance");
        }
    }
    
    [MenuItem("Tools/Test Procedural Sphere/Compare with Unity Primitive")]
    static void CompareWithUnityPrimitive()
    {
        // Create Unity primitive
        GameObject unityPrimitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        unityPrimitive.name = "Unity_Primitive_Sphere";
        unityPrimitive.transform.position = new Vector3(-2, 0, 0);
        
        // Get Unity mesh stats
        MeshFilter unityMeshFilter = unityPrimitive.GetComponent<MeshFilter>();
        Mesh unityMesh = unityMeshFilter.sharedMesh;
        
        // Create procedural sphere with similar size
        CreateTestSphere(0.5f, 2, "Procedural_Sphere");
        GameObject procSphere = GameObject.Find("Procedural_Sphere");
        if (procSphere != null)
        {
            procSphere.transform.position = new Vector3(2, 0, 0);
        }
        
        Debug.Log("=== Comparison ===");
        Debug.Log($"Unity Primitive: {unityMesh.vertexCount} vertices, {unityMesh.triangles.Length / 3} triangles");
        
        if (procSphere != null)
        {
            MeshFilter procMeshFilter = procSphere.GetComponent<MeshFilter>();
            if (procMeshFilter != null && procMeshFilter.mesh != null)
            {
                Mesh procMesh = procMeshFilter.mesh;
                Debug.Log($"Procedural (S=2): {procMesh.vertexCount} vertices, {procMesh.triangles.Length / 3} triangles");
                
                float improvement = (float)procMesh.triangles.Length / unityMesh.triangles.Length;
                Debug.Log($"Triangle count ratio: {improvement:F1}x");
            }
        }
    }
}