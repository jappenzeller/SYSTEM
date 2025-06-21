// WorldCircuitController.cs - NEW SCRIPT for the circuit prefab
using UnityEngine;
using SpacetimeDB.Types;

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
    
    private WorldCircuit circuitData;
    private GameObject[] qubitObjects;
    private float animationTime = 0f;
    
    public void Initialize(WorldCircuit circuit)
    {
        circuitData = circuit;
        qubitCount = (int)circuit.QubitCount; // PascalCase property name
        worldCoords = circuit.WorldCoords;
        
        SetupVisuals();
        UpdateEffects();
    }
    
    public void UpdateCircuit(WorldCircuit newCircuit)
    {
        circuitData = newCircuit;
        
        // Check if qubit count changed
        if (qubitCount != (int)newCircuit.QubitCount) // PascalCase property name
        {
            qubitCount = (int)newCircuit.QubitCount; // PascalCase property name
            SetupVisuals();
        }
        
        UpdateEffects();
    }
    
    private void SetupVisuals()
    {
        // Clean up existing qubits
        if (qubitObjects != null)
        {
            foreach (var qubit in qubitObjects)
            {
                if (qubit != null) Destroy(qubit);
            }
        }
        
        // Create new qubit visuals
        qubitObjects = new GameObject[qubitCount];
        
        if (qubitVisualPrefab != null && qubitContainer != null)
        {
            for (int i = 0; i < qubitCount; i++)
            {
                // Arrange qubits in a circle
                float angle = (i / (float)qubitCount) * 360f * Mathf.Deg2Rad;
                Vector3 localPos = new Vector3(
                    Mathf.Cos(angle) * qubitOrbitRadius,
                    0f,
                    Mathf.Sin(angle) * qubitOrbitRadius
                );
                
                GameObject qubit = Instantiate(qubitVisualPrefab, qubitContainer);
                qubit.transform.localPosition = localPos;
                qubit.name = $"Qubit_{i}";
                qubitObjects[i] = qubit;
            }
        }
    }
    
    private void UpdateEffects()
    {
        // Scale particle emission with qubit count
        if (energyEmissionParticles != null)
        {
            var emission = energyEmissionParticles.emission;
            emission.rateOverTime = 10f + (5f * qubitCount);
            
            var main = energyEmissionParticles.main;
            main.startSpeed = 5f + (0.5f * qubitCount);
        }
        
        // Adjust light intensity
        if (circuitLight != null)
        {
            circuitLight.intensity = 2f + (0.5f * qubitCount);
            circuitLight.range = 20f + (5f * qubitCount);
        }
        
        // Adjust audio
        if (circuitHum != null)
        {
            circuitHum.volume = 0.3f + (0.05f * qubitCount);
            circuitHum.pitch = 0.8f + (0.05f * qubitCount);
        }
    }
    
    void Update()
    {
        if (!animateQubits || qubitObjects == null) return;
        
        animationTime += Time.deltaTime;
        
        // Rotate qubit container
        if (qubitContainer != null)
        {
            qubitContainer.Rotate(Vector3.up, qubitOrbitSpeed * Time.deltaTime);
        }
        
        // Animate individual qubits
        for (int i = 0; i < qubitObjects.Length; i++)
        {
            if (qubitObjects[i] != null)
            {
                // Vertical oscillation
                float phase = (i / (float)qubitCount) * Mathf.PI * 2f;
                float yOffset = Mathf.Sin(animationTime * 2f + phase) * 0.5f;
                
                Vector3 pos = qubitObjects[i].transform.localPosition;
                pos.y = yOffset;
                qubitObjects[i].transform.localPosition = pos;
                
                // Pulse scale
                if (qubitPulseCurve != null && qubitPulseCurve.length > 0)
                {
                    float pulseScale = qubitPulseCurve.Evaluate((animationTime + phase) % 1f);
                    qubitObjects[i].transform.localScale = Vector3.one * pulseScale;
                }
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Show circuit influence area
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, 50f);
        
        // Show qubit orbit radius
        if (qubitContainer != null)
        {
            Gizmos.color = Color.cyan;
            GizmosExtensions.DrawWireCircle(qubitContainer.position, qubitContainer.up, qubitOrbitRadius);
        }
        
        // Show energy emission direction
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, Vector3.up * 20f);
    }
}

// Extension method for Gizmos (add to a utility class)
public static class GizmosExtensions
{
    public static void DrawWireCircle(Vector3 center, Vector3 normal, float radius)
    {
        Vector3 v1 = Vector3.Cross(normal, Vector3.up).normalized;
        if (v1.magnitude < 0.01f)
            v1 = Vector3.Cross(normal, Vector3.forward).normalized;
        
        Vector3 v2 = Vector3.Cross(normal, v1).normalized;
        
        const int segments = 32;
        Vector3 lastPoint = center + v1 * radius;
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 point = center + (v1 * Mathf.Cos(angle) + v2 * Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(lastPoint, point);
            lastPoint = point;
        }
    }
}