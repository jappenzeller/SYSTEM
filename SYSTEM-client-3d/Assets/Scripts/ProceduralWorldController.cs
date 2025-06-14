using UnityEngine;
using System.Collections.Generic;
using SpacetimeDB.Types;

public class ProceduralWorldController : MonoBehaviour
{
    [Header("World Components")]
    public Transform worldSphere;
    public Transform atmosphere;
    public Transform featuresContainer;
    public Transform lightsContainer;
    public Transform effectsContainer;
    
    [Header("Procedural Settings")]
    public float noiseScale = 0.1f;
    public float heightVariation = 5f;
    public int meshResolution = 100; // Increased from 50 for higher poly count
    
    [Header("Lighting")]
    public Light worldLight;
    public Gradient lightColorByShell;
    public AnimationCurve lightIntensityByShell;
    
    [Header("Audio")]
    public AudioSource ambientAudio;
    public AudioClip[] ambientSounds;
    
    private ProceduralWorldManager.WorldData worldData;
    private MeshFilter sphereMeshFilter;
    private MeshRenderer sphereMeshRenderer;
    private bool isInitialized = false;
    
    // Mesh generation
    private Mesh originalSphereMesh;
    private Vector3[] originalVertices;
    private Vector3[] modifiedVertices;

    void Awake()
    {
        // Cache components
        if (worldSphere == null)
            worldSphere = transform.Find("WorldSphere");
            
        if (worldSphere != null)
        {
            sphereMeshFilter = worldSphere.GetComponent<MeshFilter>();
            sphereMeshRenderer = worldSphere.GetComponent<MeshRenderer>();
        }
        
        if (atmosphere == null)
            atmosphere = transform.Find("Atmosphere");
            
        if (featuresContainer == null)
        {
            GameObject container = new GameObject("Features");
            container.transform.SetParent(transform);
            featuresContainer = container.transform;
        }
        
        if (lightsContainer == null)
        {
            GameObject container = new GameObject("Lights");
            container.transform.SetParent(transform);
            lightsContainer = container.transform;
        }
        
        if (effectsContainer == null)
        {
            GameObject container = new GameObject("Effects");
            container.transform.SetParent(transform);
            effectsContainer = container.transform;
        }
    }

    public void Initialize(ProceduralWorldManager.WorldData data)
    {
        worldData = data;
        
        // Apply world properties
        ApplyWorldScale();
        GenerateProceduralTerrain();
        SetupLighting();
        SetupAmbientAudio();
        
        isInitialized = true;
        
        //Debug.Log($"Initialized procedural world at {data.coords.X},{data.coords.Y},{data.coords.Z}");
    }

    void ApplyWorldScale()
    {
        if (worldSphere != null)
        {
            float diameter = worldData.radius * 2f;
            worldSphere.localScale = Vector3.one * diameter;
        }
        
        if (atmosphere != null)
        {
            float atmosphereScale = (worldData.radius + 5f) * 2f;
            atmosphere.localScale = Vector3.one * atmosphereScale;
        }
    }

    void GenerateProceduralTerrain()
    {
        if (sphereMeshFilter == null) return;
        
        // Get or create sphere mesh
        if (originalSphereMesh == null)
        {
            originalSphereMesh = CreateSphereMesh(meshResolution);
            originalVertices = originalSphereMesh.vertices;
        }
        
        // Create modified mesh
        Mesh proceduralMesh = Instantiate(originalSphereMesh);
        modifiedVertices = new Vector3[originalVertices.Length];
        
        // Apply procedural height displacement
        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 vertex = originalVertices[i];
            
            // Generate noise-based height
            float noiseValue = GenerateNoiseValue(vertex);
            float heightOffset = noiseValue * heightVariation;
            
            // Apply height displacement along vertex normal
            Vector3 direction = vertex.normalized;
            modifiedVertices[i] = vertex + direction * heightOffset;
        }
        
        proceduralMesh.vertices = modifiedVertices;
        proceduralMesh.RecalculateNormals();
        proceduralMesh.RecalculateBounds();
        
        sphereMeshFilter.mesh = proceduralMesh;
    }

    float GenerateNoiseValue(Vector3 position)
    {
        // Multi-octave Perlin noise for realistic terrain
        float noise = 0f;
        float amplitude = 1f;
        float frequency = noiseScale;
        
        // Use world data seed for consistency
        Vector3 samplePoint = position + Vector3.one * worldData.terrainSeed * 1000f;
        
        for (int octave = 0; octave < 4; octave++)
        {
            noise += Mathf.PerlinNoise(
                samplePoint.x * frequency,
                samplePoint.z * frequency
            ) * amplitude;
            
            // Add another octave with Y component
            noise += Mathf.PerlinNoise(
                samplePoint.y * frequency,
                samplePoint.x * frequency
            ) * amplitude * 0.5f;
            
            amplitude *= 0.5f;
            frequency *= 2f;
        }
        
        // Normalize to -1 to 1 range
        return (noise - 0.5f) * 2f;
    }

    Mesh CreateSphereMesh(int resolution)
    {
        // Create a simple sphere mesh if none exists
        // In practice, you might want to use a more sophisticated sphere generator
        Mesh mesh = new Mesh();
        
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        
        // Generate vertices using spherical coordinates
        for (int lat = 0; lat <= resolution; lat++)
        {
            float theta = lat * Mathf.PI / resolution;
            float sinTheta = Mathf.Sin(theta);
            float cosTheta = Mathf.Cos(theta);
            
            for (int lon = 0; lon <= resolution; lon++)
            {
                float phi = lon * 2f * Mathf.PI / resolution;
                float sinPhi = Mathf.Sin(phi);
                float cosPhi = Mathf.Cos(phi);
                
                Vector3 vertex = new Vector3(
                    sinTheta * cosPhi,
                    cosTheta,
                    sinTheta * sinPhi
                );
                
                vertices.Add(vertex);
                uvs.Add(new Vector2((float)lon / resolution, (float)lat / resolution));
            }
        }
        
        // Generate triangles
        for (int lat = 0; lat < resolution; lat++)
        {
            for (int lon = 0; lon < resolution; lon++)
            {
                int current = lat * (resolution + 1) + lon;
                int next = current + resolution + 1;
                
                triangles.Add(current);
                triangles.Add(next);
                triangles.Add(current + 1);
                
                triangles.Add(current + 1);
                triangles.Add(next);
                triangles.Add(next + 1);
            }
        }
        
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        
        return mesh;
    }

    void SetupLighting()
    {
        if (worldLight != null)
        {
            // Set light color based on shell level
            float shellNormalized = (float)worldData.shellLevel / 5f; // Assuming max 5 shells
            worldLight.color = lightColorByShell.Evaluate(shellNormalized);
            worldLight.intensity = lightIntensityByShell.Evaluate(shellNormalized);
        }
        
        // Create additional atmospheric lighting
        CreateAtmosphericLights();
    }

    void CreateAtmosphericLights()
    {
        // Create ambient rim lighting effect
        for (int i = 0; i < 3; i++)
        {
            GameObject lightObj = new GameObject($"Atmospheric Light {i}");
            lightObj.transform.SetParent(lightsContainer);
            
            Light atmosphericLight = lightObj.AddComponent<Light>();
            atmosphericLight.type = LightType.Directional;
            atmosphericLight.intensity = 0.3f;
            atmosphericLight.color = Color.Lerp(Color.blue, Color.white, worldData.shellLevel / 3f);
            
            // Position lights around the world
            float angle = i * 120f * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0.5f, Mathf.Sin(angle));
            lightObj.transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    void SetupAmbientAudio()
    {
        if (ambientAudio != null && ambientSounds.Length > 0)
        {
            // Choose ambient sound based on biome type
            int soundIndex = worldData.biomeType % ambientSounds.Length;
            ambientAudio.clip = ambientSounds[soundIndex];
            ambientAudio.loop = true;
            ambientAudio.volume = 0.3f;
            ambientAudio.spatialBlend = 1f; // 3D spatial audio
            
            // Vary pitch slightly based on world coordinates for variety
            float pitchVariation = (worldData.coords.X + worldData.coords.Y + worldData.coords.Z) * 0.1f;
            ambientAudio.pitch = 1f + Mathf.Sin(pitchVariation) * 0.2f;
            
            ambientAudio.Play();
        }
    }

    // Public API for runtime modifications
    public void SetBiomeMaterial(Material material)
    {
        if (sphereMeshRenderer != null)
        {
            sphereMeshRenderer.material = material;
        }
    }

    public void AddSurfaceFeature(GameObject featurePrefab, Vector3 surfacePosition)
    {
        if (featurePrefab != null && featuresContainer != null)
        {
            GameObject feature = Instantiate(featurePrefab, featuresContainer);
            
            // Position on surface
            Vector3 worldPosition = transform.TransformPoint(surfacePosition);
            feature.transform.position = worldPosition;
            
            // Orient to surface normal
            Vector3 surfaceNormal = surfacePosition.normalized;
            feature.transform.LookAt(feature.transform.position + transform.TransformDirection(surfaceNormal));
        }
    }

    public void UpdateProceduralTerrain(float newNoiseScale, float newHeightVariation)
    {
        noiseScale = newNoiseScale;
        heightVariation = newHeightVariation;
        GenerateProceduralTerrain();
    }

    // Animation and effects
    void Update()
    {
        if (!isInitialized) return;
        
        // Gentle world rotation
        transform.Rotate(Vector3.up, 5f * Time.deltaTime);
        
        // Atmospheric effects
        if (atmosphere != null)
        {
            // Pulse atmosphere slightly
            float pulse = 1f + Mathf.Sin(Time.time * 0.5f) * 0.05f;
            atmosphere.localScale = Vector3.one * (worldData.radius + 5f) * 2f * pulse;
        }
    }

    // Utility methods
    public Vector3 GetSurfacePosition(Vector3 direction)
    {
        direction = direction.normalized;
        
        // If we have modified vertices, raycast against the mesh
        if (modifiedVertices != null && modifiedVertices.Length > 0)
        {
            // Simple approximation: find closest vertex and use its distance
            float closestDistance = worldData.radius;
            foreach (Vector3 vertex in modifiedVertices)
            {
                if (Vector3.Dot(vertex.normalized, direction) > 0.9f) // Close enough to direction
                {
                    closestDistance = vertex.magnitude;
                    break;
                }
            }
            return direction * closestDistance;
        }
        
        // Fallback to sphere radius
        return direction * worldData.radius;
    }

    public bool IsPositionOnSurface(Vector3 position, float tolerance = 2f)
    {
        float distanceFromCenter = position.magnitude;
        return Mathf.Abs(distanceFromCenter - worldData.radius) <= tolerance;
    }

    // Debug information
    void OnDrawGizmosSelected()
    {
        if (worldData.coords != null)
        {
            // Draw world bounds
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, worldData.radius);
            
            // Draw features
            if (worldData.features != null)
            {
                Gizmos.color = Color.red;
                foreach (var feature in worldData.features)
                {
                    Vector3 worldPos = transform.TransformPoint(feature.position);
                    Gizmos.DrawWireCube(worldPos, Vector3.one * 2f);
                }
            }
        }

#if UNITY_EDITOR
        if (isInitialized)
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * (worldData.radius + 20f), 
                $"Procedural World\nCoords: {worldData.coords.X},{worldData.coords.Y},{worldData.coords.Z}\n" +
                $"Shell: {worldData.shellLevel}\nBiome: {worldData.biomeType}\nFeatures: {worldData.features?.Count ?? 0}");
        }
#endif
    }

    void OnDestroy()
    {
        // Clean up generated meshes
        if (sphereMeshFilter != null && sphereMeshFilter.mesh != null)
        {
            DestroyImmediate(sphereMeshFilter.mesh);
        }
    }
}