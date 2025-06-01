using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;

public class DistributionSphereController : MonoBehaviour
{
    [Header("Visual Components")]
    public Renderer sphereRenderer;
    public ParticleSystem energyFlowEffect;
    public ParticleSystem activationEffect;
    public Light sphereLight;
    public LineRenderer[] connectionLines;
    public Canvas uiCanvas;
    public TMPro.TextMeshProUGUI statusText;
    public TMPro.TextMeshProUGUI bufferText;
    
    [Header("Materials")]
    public Material idleMaterial;
    public Material activeMaterial;
    public Material overloadedMaterial;
    
    [Header("Animation Settings")]
    public float rotationSpeed = 30f;
    public float pulseSpeed = 1.5f;
    public float pulseIntensity = 0.1f;
    public float energyFlowSpeed = 2f;
    public float connectionLineWidth = 0.1f;
    
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip activationSound;
    public AudioClip energyTransferSound;
    public AudioClip overloadWarningSound;
    
    [Header("Coverage Visualization")]
    public GameObject coverageIndicator;
    public Material coverageWireframeMaterial;
    public bool showCoverageInGame = false;
    
    private DistributionSphere sphereData;
    private Vector3 originalScale;
    private float currentEnergyLevel = 0f;
    private bool isInitialized = false;
    private List<GameObject> connectedDevices = new List<GameObject>();
    private Dictionary<ulong, LineRenderer> deviceConnections = new Dictionary<ulong, LineRenderer>();
    
    // Energy buffer visualization
    private float targetEnergyLevel = 0f;
    private Color baseEmissionColor;
    
    // Status states
    public enum SphereStatus
    {
        Idle,
        Active,
        Transferring,
        Overloaded,
        Tunnel
    }
    private SphereStatus currentStatus = SphereStatus.Idle;

    void Awake()
    {
        // Get components if not assigned
        if (sphereRenderer == null)
            sphereRenderer = GetComponent<Renderer>();
        
        if (energyFlowEffect == null)
            energyFlowEffect = GetComponentInChildren<ParticleSystem>();
        
        if (sphereLight == null)
            sphereLight = GetComponentInChildren<Light>();
            
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
            
        if (uiCanvas == null)
            uiCanvas = GetComponentInChildren<Canvas>();
    }

    void Start()
    {
        originalScale = transform.localScale;
        
        // Set up UI canvas to face camera
        if (uiCanvas != null)
        {
            uiCanvas.worldCamera = Camera.main;
            uiCanvas.transform.localPosition = Vector3.up * 2f;
        }
        
        // Initialize connection lines pool
        InitializeConnectionLines();
        
        // Set base emission color
        if (sphereRenderer != null && sphereRenderer.material.HasProperty("_EmissionColor"))
        {
            baseEmissionColor = sphereRenderer.material.GetColor("_EmissionColor");
        }
    }

    public void Initialize(DistributionSphere data)
    {
        sphereData = data;
        
        // Set initial scale based on coverage radius
        float scale = Mathf.Lerp(0.5f, 3f, data.CoverageRadius / 200f);
        transform.localScale = originalScale * scale;
        
        // Determine sphere type and configure accordingly
        if (data.TunnelId.HasValue)
        {
            SetStatus(SphereStatus.Tunnel);
        }
        else
        {
            SetStatus(SphereStatus.Idle);
        }
        
        // Create coverage visualization
        SetupCoverageVisualization();
        
        // Configure initial visual effects
        SetupVisualEffects();
        
        // Update UI
        UpdateUI();
        
        isInitialized = true;
        
        Debug.Log($"Initialized distribution sphere {data.SphereId} with {data.CoverageRadius} radius");
    }

    void SetupCoverageVisualization()
    {
        if (coverageIndicator != null)
        {
            // Create wireframe sphere to show coverage area
            coverageIndicator.transform.localScale = Vector3.one * sphereData.CoverageRadius * 2f;
            
            var renderer = coverageIndicator.GetComponent<Renderer>();
            if (renderer != null && coverageWireframeMaterial != null)
            {
                renderer.material = coverageWireframeMaterial;
                renderer.enabled = showCoverageInGame;
            }
        }
    }

    void SetupVisualEffects()
    {
        // Configure particle effects based on sphere type
        if (energyFlowEffect != null)
        {
            var main = energyFlowEffect.main;
            var emission = energyFlowEffect.emission;
            
            switch (currentStatus)
            {
                case SphereStatus.Tunnel:
                    main.startColor = Color.cyan;
                    emission.rateOverTime = 50f;
                    break;
                case SphereStatus.Active:
                    main.startColor = Color.green;
                    emission.rateOverTime = 30f;
                    break;
                default:
                    main.startColor = Color.blue;
                    emission.rateOverTime = 10f;
                    break;
            }
        }
        
        // Configure lighting
        if (sphereLight != null)
        {
            sphereLight.color = GetStatusColor();
            sphereLight.intensity = Mathf.Lerp(1f, 3f, currentEnergyLevel / sphereData.BufferCapacity);
            sphereLight.range = sphereData.CoverageRadius * 0.5f;
        }
    }

    void Update()
    {
        if (!isInitialized) return;
        
        // Update animations
        AnimateRotation();
        AnimatePulsing();
        UpdateEnergyFlow();
        
        // Update energy level smoothly
        if (Mathf.Abs(currentEnergyLevel - targetEnergyLevel) > 0.1f)
        {
            currentEnergyLevel = Mathf.Lerp(currentEnergyLevel, targetEnergyLevel, Time.deltaTime * 2f);
            UpdateEnergyVisualization();
        }
        
        // Update UI to face camera
        if (uiCanvas != null && Camera.main != null)
        {
            uiCanvas.transform.LookAt(Camera.main.transform);
            uiCanvas.transform.Rotate(0, 180, 0); // Flip to face camera properly
        }
    }

    void AnimateRotation()
    {
        // Rotate based on status
        float speedMultiplier = currentStatus switch
        {
            SphereStatus.Transferring => 2f,
            SphereStatus.Overloaded => 3f,
            SphereStatus.Tunnel => 1.5f,
            _ => 1f
        };
        
        transform.Rotate(Vector3.up, rotationSpeed * speedMultiplier * Time.deltaTime);
    }

    void AnimatePulsing()
    {
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
        
        // Pulse based on energy level
        float energyPulse = 1f + (currentEnergyLevel / sphereData.BufferCapacity) * 0.2f;
        transform.localScale = originalScale * pulse * energyPulse;
        
        // Pulse light intensity
        if (sphereLight != null)
        {
            float baseIntensity = Mathf.Lerp(1f, 3f, currentEnergyLevel / sphereData.BufferCapacity);
            sphereLight.intensity = baseIntensity * pulse;
        }
    }

    void UpdateEnergyFlow()
    {
        if (energyFlowEffect != null)
        {
            var velocityOverLifetime = energyFlowEffect.velocityOverLifetime;
            if (velocityOverLifetime.enabled)
            {
                // Make particles flow faster when transferring energy
                float flowSpeed = currentStatus == SphereStatus.Transferring ? energyFlowSpeed * 2f : energyFlowSpeed;
                velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(flowSpeed);
            }
            
            // Update emission rate based on energy level
            var emission = energyFlowEffect.emission;
            float baseRate = currentStatus switch
            {
                SphereStatus.Tunnel => 50f,
                SphereStatus.Active => 30f,
                SphereStatus.Transferring => 60f,
                SphereStatus.Overloaded => 100f,
                _ => 10f
            };
            emission.rateOverTime = baseRate * (1f + currentEnergyLevel / sphereData.BufferCapacity);
        }
    }

    void UpdateEnergyVisualization()
    {
        // Update material emission based on energy level
        if (sphereRenderer != null && sphereRenderer.material.HasProperty("_EmissionColor"))
        {
            float energyRatio = currentEnergyLevel / sphereData.BufferCapacity;
            Color emissionColor = Color.Lerp(baseEmissionColor, GetStatusColor() * 2f, energyRatio);
            sphereRenderer.material.SetColor("_EmissionColor", emissionColor);
        }
        
        // Update UI
        UpdateUI();
    }

    void SetStatus(SphereStatus newStatus)
    {
        if (currentStatus == newStatus) return;
        
        SphereStatus oldStatus = currentStatus;
        currentStatus = newStatus;
        
        // Update material
        Material targetMaterial = currentStatus switch
        {
            SphereStatus.Active or SphereStatus.Transferring => activeMaterial,
            SphereStatus.Overloaded => overloadedMaterial,
            _ => idleMaterial
        };
        
        if (sphereRenderer != null && targetMaterial != null)
        {
            sphereRenderer.material = targetMaterial;
        }
        
        // Play transition effects
        PlayStatusTransitionEffect(oldStatus, newStatus);
        
        // Update visual effects
        SetupVisualEffects();
    }

    void PlayStatusTransitionEffect(SphereStatus from, SphereStatus to)
    {
        // Play activation effect when becoming active
        if (to == SphereStatus.Active && activationEffect != null)
        {
            activationEffect.Play();
        }
        
        // Play appropriate sound
        AudioClip soundToPlay = to switch
        {
            SphereStatus.Active => activationSound,
            SphereStatus.Transferring => energyTransferSound,
            SphereStatus.Overloaded => overloadWarningSound,
            _ => null
        };
        
        if (audioSource != null && soundToPlay != null)
        {
            audioSource.PlayOneShot(soundToPlay);
        }
    }

    Color GetStatusColor()
    {
        return currentStatus switch
        {
            SphereStatus.Active => Color.green,
            SphereStatus.Transferring => Color.yellow,
            SphereStatus.Overloaded => Color.red,
            SphereStatus.Tunnel => Color.cyan,
            _ => Color.blue
        };
    }

    void UpdateUI()
    {
        if (statusText != null)
        {
            statusText.text = currentStatus.ToString();
            statusText.color = GetStatusColor();
        }
        
        if (bufferText != null)
        {
            float percentage = (currentEnergyLevel / sphereData.BufferCapacity) * 100f;
            bufferText.text = $"Buffer: {percentage:F0}%\n{currentEnergyLevel:F0}/{sphereData.BufferCapacity:F0}";
            
            // Color code the buffer level
            if (percentage > 90f)
                bufferText.color = Color.red;
            else if (percentage > 70f)
                bufferText.color = Color.yellow;
            else
                bufferText.color = Color.white;
        }
    }

    void InitializeConnectionLines()
    {
        // Create a pool of line renderers for device connections
        if (connectionLines == null || connectionLines.Length == 0)
        {
            connectionLines = new LineRenderer[10]; // Support up to 10 connections
            
            for (int i = 0; i < connectionLines.Length; i++)
            {
                GameObject lineObj = new GameObject($"Connection Line {i}");
                lineObj.transform.SetParent(transform);
                
                LineRenderer line = lineObj.AddComponent<LineRenderer>();
                line.material = new Material(Shader.Find("Sprites/Default"));
                line.material.color = Color.cyan;
                line.startWidth = connectionLineWidth;
                line.endWidth = connectionLineWidth * 0.5f;
                line.positionCount = 2;
                line.enabled = false;
                
                connectionLines[i] = line;
            }
        }
    }

    public void UpdateEnergyLevel(float newLevel)
    {
        targetEnergyLevel = Mathf.Clamp(newLevel, 0f, sphereData.BufferCapacity);
        
        // Update status based on energy level
        float energyRatio = targetEnergyLevel / sphereData.BufferCapacity;
        
        if (energyRatio > 0.95f)
        {
            SetStatus(SphereStatus.Overloaded);
        }
        else if (energyRatio > 0.1f)
        {
            SetStatus(SphereStatus.Active);
        }
        else
        {
            SetStatus(SphereStatus.Idle);
        }
    }

    public void ShowEnergyTransfer(Vector3 targetPosition, EnergyType energyType, float amount)
    {
        SetStatus(SphereStatus.Transferring);
        
        // Create energy transfer visual effect
        StartCoroutine(AnimateEnergyTransfer(targetPosition, energyType, amount));
    }

    System.Collections.IEnumerator AnimateEnergyTransfer(Vector3 targetPosition, EnergyType energyType, float amount)
    {
        // Find available line renderer
        LineRenderer line = null;
        foreach (var lr in connectionLines)
        {
            if (!lr.enabled)
            {
                line = lr;
                break;
            }
        }
        
        if (line != null)
        {
            line.enabled = true;
            line.material.color = GetEnergyTypeColor(energyType);
            
            // Animate energy beam
            float duration = 1f;
            float startTime = Time.time;
            
            while (Time.time - startTime < duration)
            {
                float progress = (Time.time - startTime) / duration;
                
                line.SetPosition(0, transform.position);
                line.SetPosition(1, Vector3.Lerp(transform.position, targetPosition, progress));
                
                // Fade out the line
                Color lineColor = line.material.color;
                lineColor.a = 1f - progress;
                line.material.color = lineColor;
                
                yield return null;
            }
            
            line.enabled = false;
        }
        
        // Return to previous status after transfer
        yield return new WaitForSeconds(0.5f);
        if (currentStatus == SphereStatus.Transferring)
        {
            SetStatus(currentEnergyLevel > sphereData.BufferCapacity * 0.1f ? SphereStatus.Active : SphereStatus.Idle);
        }
    }

    Color GetEnergyTypeColor(EnergyType energyType)
    {
        return energyType switch
        {
            EnergyType.Red => Color.red,
            EnergyType.Green => Color.green,
            EnergyType.Blue => Color.blue,
            EnergyType.Cyan => Color.cyan,
            EnergyType.Magenta => Color.magenta,
            EnergyType.Yellow => Color.yellow,
            _ => Color.white
        };
    }

    public void ToggleCoverageVisualization()
    {
        showCoverageInGame = !showCoverageInGame;
        if (coverageIndicator != null)
        {
            var renderer = coverageIndicator.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = showCoverageInGame;
            }
        }
    }

    // Called when player clicks on sphere
    void OnMouseDown()
    {
        if (isInitialized)
        {
            ToggleCoverageVisualization();
            Debug.Log($"Distribution Sphere {sphereData.SphereId} - Status: {currentStatus}, Energy: {currentEnergyLevel:F1}/{sphereData.BufferCapacity:F1}");
        }
    }

    // Debug visualization
    void OnDrawGizmosSelected()
    {
        if (isInitialized)
        {
            // Draw coverage radius
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, sphereData.CoverageRadius);
            
            // Draw connections to nearby devices
            Gizmos.color = Color.yellow;
            foreach (var device in connectedDevices)
            {
                if (device != null)
                {
                    Gizmos.DrawLine(transform.position, device.transform.position);
                }
            }
        }
        
#if UNITY_EDITOR
        // Show status info in scene view
        if (isInitialized)
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, 
                $"Sphere {sphereData.SphereId}\nStatus: {currentStatus}\nEnergy: {currentEnergyLevel:F1}/{sphereData.BufferCapacity:F1}\nCoverage: {sphereData.CoverageRadius:F1}");
        }
#endif
    }

    void OnDestroy()
    {
        // Clean up any ongoing coroutines or effects
        StopAllCoroutines();
    }
}