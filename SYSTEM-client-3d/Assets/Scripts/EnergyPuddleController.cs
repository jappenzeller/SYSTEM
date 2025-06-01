using UnityEngine;
using SpacetimeDB.Types;

public class EnergyPuddleController : MonoBehaviour
{
    [Header("Visual Components")]
    public Renderer puddleRenderer;
    public ParticleSystem glowEffect;
    public ParticleSystem absorptionEffect;
    
    [Header("Animation Settings")]
    public float pulseSpeed = 2f;
    public float pulseIntensity = 0.3f;
    
    private EnergyPuddle puddleData;
    private Material originalMaterial;
    private Vector3 originalScale;
    private bool isInitialized = false;

    void Awake()
    {
        // Get components if not assigned
        if (puddleRenderer == null)
            puddleRenderer = GetComponent<Renderer>();
        
        if (glowEffect == null)
            glowEffect = GetComponentInChildren<ParticleSystem>();
    }

    public void Initialize(EnergyPuddle data, Material energyMaterial)
    {
        puddleData = data;
        originalScale = transform.localScale;
        
        // Apply material
        if (puddleRenderer != null && energyMaterial != null)
        {
            puddleRenderer.material = energyMaterial;
            originalMaterial = energyMaterial;
        }
        
        // Configure particle effects
        SetupParticleEffects();
        
        // Set initial scale based on energy amount
        UpdateVisuals();
        
        isInitialized = true;
    }

    void SetupParticleEffects()
    {
        if (glowEffect != null)
        {
            var main = glowEffect.main;
            main.startColor = GetEnergyColor(puddleData.EnergyType);
            
            var emission = glowEffect.emission;
            emission.rateOverTime = puddleData.CurrentAmount * 2f; // More particles for more energy
        }
    }

    void Update()
    {
        if (!isInitialized) return;
        
        // Animate pulsing effect
        AnimatePulse();
    }

    void AnimatePulse()
    {
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
        float energyRatio = puddleData.CurrentAmount / puddleData.MaxAmount;
        float scale = Mathf.Lerp(0.5f, 2.0f, energyRatio) * pulse;
        
        transform.localScale = originalScale * scale;
        
        // Pulse emission intensity
        if (puddleRenderer != null && originalMaterial != null)
        {
            Color emissionColor = GetEnergyColor(puddleData.EnergyType) * pulse;
            puddleRenderer.material.SetColor("_EmissionColor", emissionColor);
        }
    }

    public void UpdateData(EnergyPuddle newData)
    {
        puddleData = newData;
        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        if (glowEffect != null)
        {
            var emission = glowEffect.emission;
            emission.rateOverTime = puddleData.CurrentAmount * 2f;
        }
    }

    Color GetEnergyColor(EnergyType energyType)
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

    public void OnPlayerInteract()
    {
        // Trigger absorption effect
        if (absorptionEffect != null)
        {
            absorptionEffect.Play();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Handle player interaction when they step on puddle
        if (other.CompareTag("Player"))
        {
            OnPlayerInteract();
        }
    }
}