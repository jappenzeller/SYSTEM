using UnityEngine;
using SpacetimeDB.Types;
using System.Collections.Generic;

/// <summary>
/// Controls the emission of energy orbs from the world circuit with physics-based trajectories
/// </summary>
public class WorldCircuitEmissionController : MonoBehaviour
{
    [Header("Emission Settings")]
    [SerializeField] private float baseEmissionSpeed = 80f;  // Increased for better arcs
    [SerializeField] private float speedVariation = 20f;
    [SerializeField] private float minLaunchAngle = 15f;  // Minimum angle from horizontal
    [SerializeField] private float maxLaunchAngle = 75f;  // Maximum angle from horizontal
    
    [Header("Coverage Settings")]
    [SerializeField] private int latitudeBands = 8; // Number of latitude divisions
    [SerializeField] private int longitudeSegments = 12; // Number of longitude divisions per band
    [SerializeField] private float coverageRandomness = 0.1f; // 0-1, adds randomness to coverage pattern
    
    [Header("Debug")]
    [SerializeField] private bool showTrajectoryPredictions = true;
    [SerializeField] private int trajectorySteps = 50;
    
    private WorldCircuit circuitData;
    private float worldRadius;
    private Vector3 circuitPosition;
    private Queue<Vector3> targetDirections;
    
    void Awake()
    {
        GenerateTargetDirections();
    }
    
    public void Initialize(WorldCircuit circuit, float worldRadius)
    {
        this.circuitData = circuit;
        this.worldRadius = worldRadius;
        this.circuitPosition = transform.position; // Should be at north pole
        
        Debug.Log($"[EmissionController] Initialized with world radius: {worldRadius}");
    }
    
    /// <summary>
    /// Generate a distributed set of target directions to cover the globe
    /// </summary>
    void GenerateTargetDirections()
    {
        targetDirections = new Queue<Vector3>();
        
        // Generate points using spherical coordinates
        for (int lat = 0; lat < latitudeBands; lat++)
        {
            float theta = Mathf.PI * (lat + 0.5f) / latitudeBands; // 0 to PI
            
            // Adjust longitude segments based on latitude to maintain even distribution
            int lonSegments = Mathf.Max(1, Mathf.RoundToInt(longitudeSegments * Mathf.Sin(theta)));
            
            for (int lon = 0; lon < lonSegments; lon++)
            {
                float phi = 2f * Mathf.PI * lon / lonSegments; // 0 to 2PI
                
                // Convert spherical to Cartesian
                Vector3 direction = new Vector3(
                    Mathf.Sin(theta) * Mathf.Cos(phi),
                    Mathf.Cos(theta),
                    Mathf.Sin(theta) * Mathf.Sin(phi)
                );
                
                // Add some randomness
                if (coverageRandomness > 0)
                {
                    direction += Random.insideUnitSphere * coverageRandomness;
                    direction.Normalize();
                }
                
                targetDirections.Enqueue(direction);
            }
        }
        
        Debug.Log($"[EmissionController] Generated {targetDirections.Count} target directions for global coverage");
    }
    
    /// <summary>
    /// Calculate launch velocity needed to reach a target point on the sphere
    /// </summary>
    public Vector3 CalculateLaunchVelocity(Vector3 targetSurfacePoint)
    {
        // Calculate the direction from circuit to target
        Vector3 toTarget = targetSurfacePoint - circuitPosition;
        float horizontalDistance = new Vector3(toTarget.x, 0, toTarget.z).magnitude;
        
        // Determine launch angle based on distance
        float normalizedDistance = Mathf.Clamp01(horizontalDistance / (worldRadius * 2f));
        float launchAngle = Mathf.Lerp(maxLaunchAngle, minLaunchAngle, normalizedDistance);
        
        // Calculate launch direction
        Vector3 horizontalDirection = new Vector3(toTarget.x, 0, toTarget.z).normalized;
        
        // Create an upward-angled launch vector
        Vector3 launchDirection = (horizontalDirection + Vector3.up * Mathf.Tan(launchAngle * Mathf.Deg2Rad)).normalized;
        
        // Calculate speed with variation
        float speed = baseEmissionSpeed + Random.Range(-speedVariation, speedVariation);
        
        // Add some lateral spread for visual variety
        Vector3 spread = Vector3.Cross(Vector3.up, horizontalDirection) * Random.Range(-0.2f, 0.2f);
        launchDirection = (launchDirection + spread).normalized;
        
        return launchDirection * speed;
    }
    
    /// <summary>
    /// Calculate optimal launch angle for given distance
    /// </summary>
    float CalculateOptimalLaunchAngle(float distance)
    {
        // For maximum range, optimal angle is 45 degrees in vacuum
        // We'll adjust based on distance to world edge
        float normalizedDistance = distance / (worldRadius * 2f);
        
        // Closer targets need higher angles, farther targets need lower angles
        float baseAngle = 45f;
        float angleAdjustment = (1f - normalizedDistance) * 30f; // Up to 30 degrees adjustment
        
        return baseAngle + angleAdjustment;
    }
    
    /// <summary>
    /// Calculate required launch speed for given distance and angle
    /// </summary>
    float CalculateLaunchSpeed(float distance, float angle)
    {
        // Use the base emission speed with some calculation based on distance
        float normalizedDistance = distance / (worldRadius * 2f);
        float speedMultiplier = 0.5f + normalizedDistance; // 0.5x to 1.5x based on distance
        
        return baseEmissionSpeed * speedMultiplier;
    }
    
    /// <summary>
    /// Get emission parameters for the next orb
    /// </summary>
    public EmissionParameters GetNextEmissionParameters()
    {
        // Get next target direction from queue
        if (targetDirections.Count == 0)
        {
            GenerateTargetDirections(); // Regenerate if we've used all
        }
        
        Vector3 targetDirection = targetDirections.Dequeue();
        targetDirections.Enqueue(targetDirection); // Put it back at the end
        
        // Calculate target point on sphere surface
        Vector3 targetPoint = targetDirection * worldRadius;
        
        // Calculate launch velocity
        Vector3 velocity = CalculateLaunchVelocity(targetPoint);
        
        return new EmissionParameters
        {
            InitialVelocity = velocity,
            TargetSurfacePoint = targetPoint,
            EstimatedFlightTime = EstimateFlightTime(velocity, targetPoint)
        };
    }
    
    /// <summary>
    /// Estimate flight time for trajectory planning
    /// </summary>
    float EstimateFlightTime(Vector3 velocity, Vector3 targetPoint)
    {
        // Simplified time estimation
        float distance = (targetPoint - circuitPosition).magnitude;
        float averageSpeed = velocity.magnitude * 0.7f; // Account for deceleration
        return distance / averageSpeed;
    }
    
    /// <summary>
    /// Update physics for an orb (call this in orb's Update)
    /// </summary>
    public Vector3 UpdateOrbPhysics(Vector3 currentVelocity, Vector3 currentPosition, float gravity, float airDensity, float deltaTime)
    {
        // Apply gravity (towards world center)
        Vector3 gravityDirection = -currentPosition.normalized;
        Vector3 gravityForce = gravityDirection * Mathf.Abs(gravity);
        
        // Apply air resistance (simplified drag)
        Vector3 dragForce = -currentVelocity.normalized * currentVelocity.magnitude * airDensity;
        
        // Update velocity
        Vector3 newVelocity = currentVelocity + (gravityForce + dragForce) * deltaTime;
        
        return newVelocity;
    }
    
    void OnDrawGizmos()
    {
        if (!showTrajectoryPredictions || targetDirections == null) return;
        
        // Visualize some trajectory predictions
        int previewCount = Mathf.Min(5, targetDirections.Count);
        var tempQueue = new Queue<Vector3>(targetDirections);
        
        for (int i = 0; i < previewCount; i++)
        {
            if (tempQueue.Count == 0) break;
            
            Vector3 targetDir = tempQueue.Dequeue();
            Vector3 targetPoint = targetDir * worldRadius;
            
            // Draw target point
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetPoint, 2f);
            
            // Draw predicted trajectory
            if (Application.isPlaying)
            {
                Vector3 velocity = CalculateLaunchVelocity(targetPoint);
                DrawTrajectory(transform.position, velocity);
            }
        }
    }
    
    void DrawTrajectory(Vector3 startPos, Vector3 initialVelocity)
    {
        Vector3 pos = startPos;
        Vector3 vel = initialVelocity;
        float dt = 0.1f;
        float gravity = 30f; // Local gravity value for visualization
        float airDensity = 0.02f; // Local air density for visualization
        
        Gizmos.color = Color.yellow;
        
        for (int i = 0; i < trajectorySteps; i++)
        {
            Vector3 nextPos = pos + vel * dt;
            
            // Check if we've hit the sphere
            if (nextPos.magnitude <= worldRadius)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pos, nextPos);
                break;
            }
            
            Gizmos.DrawLine(pos, nextPos);
            
            // Update for next step
            pos = nextPos;
            vel = UpdateOrbPhysics(vel, pos, gravity, airDensity, dt);
        }
    }
}

public struct EmissionParameters
{
    public Vector3 InitialVelocity;
    public Vector3 TargetSurfacePoint;
    public float EstimatedFlightTime;
}