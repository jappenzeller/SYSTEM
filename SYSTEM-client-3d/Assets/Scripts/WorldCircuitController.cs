// WorldCircuitController.cs - UPDATED with filter-based queries
using UnityEngine;
using SpacetimeDB.Types;
using System.Linq;
using System.Collections.Generic;

public class WorldCircuitController : MonoBehaviour
{
    [Header("Circuit Data")]
    [SerializeField] private int qubitCount = 0;
    [SerializeField] private WorldCoords worldCoords;
    
    [Header("Visual Elements")]
    [SerializeField] private GameObject qubitVisualPrefab;
    [SerializeField] private Transform qubitContainer;
    [SerializeField] private float qubitOrbitRadius = 5f;
    [SerializeField] private float qubitOrbitSpeed = 30f;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem energyEmissionParticles;
    [SerializeField] private Light circuitLight;
    [SerializeField] private AudioSource circuitHum;
    
    [Header("Animation")]
    [SerializeField] private bool animateQubits = true;
    [SerializeField] private AnimationCurve qubitPulseCurve;

    [Header("Emission")]
    [SerializeField] private WorldCircuitEmissionController emissionController;
    private float lastEmissionTime;
    
    private WorldCircuit circuitData;
    private GameObject[] qubitObjects;
    private float animationTime = 0f;
    
    public void Initialize(WorldCircuit circuit)
    {
        circuitData = circuit;
        qubitCount = (int)circuit.QubitCount;
        worldCoords = circuit.WorldCoords;
        
        SetupVisuals();
        UpdateEffects();
    }
    
    // In Awake or Start, add the emission controller
    void Awake()
    {
        emissionController = gameObject.AddComponent<WorldCircuitEmissionController>();
    }

    // Add method to handle emission
    public void EmitEnergyOrbs(int count)
    {
        if (emissionController == null) return;
        
        for (int i = 0; i < count; i++)
        {
            var emissionParams = emissionController.GetNextEmissionParameters();
            
            // Here you would trigger the server to create the orb with this velocity
            // For now, log it
            Debug.Log($"[WorldCircuit] Emit orb with velocity: {emissionParams.InitialVelocity}, " +
                    $"targeting: {emissionParams.TargetSurfacePoint}");
        }
    }
    
    public void UpdateCircuit(WorldCircuit newCircuit)
    {
        circuitData = newCircuit;

        // Check if qubit count changed
        if (qubitCount != (int)newCircuit.QubitCount)
        {
            qubitCount = (int)newCircuit.QubitCount;
            SetupVisuals(); // Recreate visuals if qubit count changed
        }

        UpdateEffects();
    }
    
    void SetupVisuals()
    {
        // Clear existing qubits
        if (qubitObjects != null)
        {
            foreach (var qubit in qubitObjects)
            {
                if (qubit != null) Destroy(qubit);
            }
        }
        
        // Create new qubit visuals
        qubitObjects = new GameObject[qubitCount];
        
        for (int i = 0; i < qubitCount; i++)
        {
            if (qubitVisualPrefab != null && qubitContainer != null)
            {
                GameObject qubit = Instantiate(qubitVisualPrefab, qubitContainer);
                
                // Position qubits in a circle
                float angle = (i / (float)qubitCount) * 2f * Mathf.PI;
                Vector3 localPos = new Vector3(
                    Mathf.Cos(angle) * qubitOrbitRadius,
                    0,
                    Mathf.Sin(angle) * qubitOrbitRadius
                );
                qubit.transform.localPosition = localPos;
                
                qubitObjects[i] = qubit;
            }
        }
    }
    
    void UpdateEffects()
    {
        if (circuitData == null) return;
        
        // Update particle system
        if (energyEmissionParticles != null)
        {
            var emission = energyEmissionParticles.emission;
            emission.rateOverTime = circuitData.OrbsPerEmission * 2f; // Visual indicator
            
            var main = energyEmissionParticles.main;
            main.maxParticles = (int)(circuitData.OrbsPerEmission * 10);
        }
        
        // Update light intensity based on qubit count
        if (circuitLight != null)
        {
            circuitLight.intensity = 1f + (qubitCount * 0.5f);
            circuitLight.range = 10f + (qubitCount * 5f);
        }
        
        // Update audio
        if (circuitHum != null)
        {
            circuitHum.pitch = 0.8f + (qubitCount * 0.1f);
            circuitHum.volume = 0.3f + (qubitCount * 0.1f);
        }
    }

    void Update()
    {
        if (!animateQubits || qubitObjects == null) return;

        animationTime += Time.deltaTime;

        // Animate qubits
        for (int i = 0; i < qubitObjects.Length; i++)
        {
            if (qubitObjects[i] == null) continue;

            // Orbit animation
            float angle = (i / (float)qubitCount) * 2f * Mathf.PI + (animationTime * qubitOrbitSpeed * Mathf.Deg2Rad);
            Vector3 localPos = new Vector3(
                Mathf.Cos(angle) * qubitOrbitRadius,
                Mathf.Sin(animationTime * 2f + i) * 0.5f, // Vertical oscillation
                Mathf.Sin(angle) * qubitOrbitRadius
            );
            qubitObjects[i].transform.localPosition = localPos;

            // Pulse animation
            if (qubitPulseCurve != null && qubitPulseCurve.length > 0)
            {
                float pulseScale = qubitPulseCurve.Evaluate((animationTime + i * 0.2f) % 1f);
                qubitObjects[i].transform.localScale = Vector3.one * pulseScale;
            }

            // Rotation
            qubitObjects[i].transform.Rotate(Vector3.up, 180f * Time.deltaTime);
        }

        // Check if ready to emit using filter query
        CheckEmissionStatus();
        
        if (circuitData != null && Time.time - lastEmissionTime > circuitData.EmissionIntervalMs / 1000f)
        {
            lastEmissionTime = Time.time;
            EmitEnergyOrbs((int)circuitData.OrbsPerEmission);
        }
    }
    
    void CheckEmissionStatus()
    {
        if (circuitData == null || !GameManager.IsConnected()) return;
        
        // Check if this specific circuit is ready to emit
        ulong currentTime = (ulong)(Time.time * 1000);
        bool isReady = currentTime >= circuitData.LastEmissionTime + circuitData.EmissionIntervalMs;
        
        // Visual feedback for emission readiness
        if (energyEmissionParticles != null)
        {
            var main = energyEmissionParticles.main;
            main.startSpeed = isReady ? 10f : 5f;
            main.startLifetime = isReady ? 2f : 1f;
        }
        
        if (circuitLight != null && isReady)
        {
            // Pulsing light when ready
            circuitLight.intensity = 1f + (qubitCount * 0.5f) + Mathf.Sin(Time.time * 5f) * 0.5f;
        }
    }
    
    public void TriggerEmissionEffect()
    {
        // Visual burst effect
        if (energyEmissionParticles != null)
        {
            energyEmissionParticles.Emit(50);
        }
        
        // Light flash
        if (circuitLight != null)
        {
            StartCoroutine(LightFlash());
        }
        
        // Audio effect
        if (circuitHum != null)
        {
            circuitHum.PlayOneShot(circuitHum.clip, 1.5f);
        }
    }
    
    System.Collections.IEnumerator LightFlash()
    {
        float originalIntensity = circuitLight.intensity;
        circuitLight.intensity = originalIntensity * 3f;
        
        float duration = 0.3f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            circuitLight.intensity = Mathf.Lerp(originalIntensity * 3f, originalIntensity, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        circuitLight.intensity = originalIntensity;
    }
    
    // Helper method to get emission countdown
    public float GetTimeUntilEmission()
    {
        if (circuitData == null) return -1f;
        
        ulong currentTime = (ulong)(Time.time * 1000);
        ulong nextEmissionTime = circuitData.LastEmissionTime + circuitData.EmissionIntervalMs;
        
        if (currentTime >= nextEmissionTime) return 0f;
        
        return (nextEmissionTime - currentTime) / 1000f;
    }
    
    // Get neighboring circuits using proper SpacetimeDB API
    public WorldCircuit[] GetNeighboringCircuits()
    {
        if (!GameManager.IsConnected()) return new WorldCircuit[0];
        
        // Get circuits in adjacent worlds
        var offsets = new (sbyte x, sbyte y, sbyte z)[]
        {
            (1, 0, 0), (-1, 0, 0),
            (0, 1, 0), (0, -1, 0),
            (0, 0, 1), (0, 0, -1)
        };
        
        var neighbors = new List<WorldCircuit>();
        
        foreach (var offset in offsets)
        {
            var adjacentCoords = new WorldCoords(
                (sbyte)(worldCoords.X + offset.x),
                (sbyte)(worldCoords.Y + offset.y),
                (sbyte)(worldCoords.Z + offset.z)
            );

            var circuit = GameManager.Conn.Db.WorldCircuit.Iter().FirstOrDefault(wc =>
                wc.WorldCoords.X == adjacentCoords.X &&
                wc.WorldCoords.Y == adjacentCoords.Y &&
                wc.WorldCoords.Z == adjacentCoords.Z);
                
            if (circuit != null)
            {
                neighbors.Add(circuit);
            }
        }
        
        return neighbors.ToArray();
    }
    
    void OnDrawGizmosSelected()
    {
        // Visualize circuit range
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 10f + (qubitCount * 5f));
        
        // Show emission countdown
        float timeUntilEmission = GetTimeUntilEmission();
        if (timeUntilEmission >= 0)
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 15f,
                $"Next emission in: {timeUntilEmission:F1}s"
            );
#endif
        }
    }
}