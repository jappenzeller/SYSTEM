using UnityEngine;
using SpacetimeDB.Types;

public class EnergyOrbController : MonoBehaviour
{
    [Header("Visual Components")]
    public Renderer orbRenderer;
    public ParticleSystem trailEffect;
    public ParticleSystem impactEffect;
    public Light orbLight;
    public AudioSource audioSource;
    
    [Header("Animation Settings")]
    public float rotationSpeed = 90f;
    public float pulseSpeed = 3f;
    public float pulseIntensity = 0.2f;
    public float floatAmplitude = 0.5f;
    public float floatSpeed = 2f;
    
    [Header("Audio")]
    public AudioClip spawnSound;
    public AudioClip impactSound;
    
    [Header("Movement")]
    public float fallSpeed = 20f;
    public AnimationCurve fallCurve = AnimationCurve.Linear(0, 0, 1, 1);
    
    private EnergyOrb orbData;
    private Material originalMaterial;
    private Vector3 originalScale;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private bool isInitialized = false;
    private bool isFalling = false;
    private float fallStartTime;
    private Color energyColor;
    private float worldRadius; // To store the world radius for accurate falling

    void Awake()
    {
        // Get components if not assigned
        if (orbRenderer == null)
            orbRenderer = GetComponent<Renderer>();
        
        if (trailEffect == null)
            trailEffect = GetComponentInChildren<ParticleSystem>();
        
        if (orbLight == null)
            orbLight = GetComponentInChildren<Light>();
            
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void Initialize(EnergyOrb data, Material energyMaterial, float worldRadius)
    {
        orbData = data;
        originalScale = transform.localScale;
        startPosition = transform.position;
        energyColor = GetEnergyColor(data.EnergyType);
        this.worldRadius = worldRadius;
        
        // Apply material
        if (orbRenderer != null && energyMaterial != null)
        {
            orbRenderer.material = energyMaterial;
            originalMaterial = energyMaterial;
        }
        
        // Configure visual effects
        SetupVisualEffects();
        
        // Play spawn sound
        PlaySound(spawnSound);
        
        // Check if this orb should be falling
        Vector3 velocity = new Vector3(data.Velocity.X, data.Velocity.Y, data.Velocity.Z);
        if (velocity.magnitude > 0.1f)
        {
            StartFalling(velocity);
        }
        
        isInitialized = true;
    }

    void SetupVisualEffects()
    {
        // Configure particle trail
        if (trailEffect != null)
        {
            var main = trailEffect.main;
            main.startColor = energyColor;
            
            var emission = trailEffect.emission;
            emission.rateOverTime = orbData.EnergyAmount * 5f; // More particles for more energy
            
            // Make trail follow the orb's energy type
            var velocityOverLifetime = trailEffect.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        }
        
        // Configure light
        if (orbLight != null)
        {
            orbLight.color = energyColor;
            orbLight.intensity = Mathf.Lerp(0.5f, 2.0f, orbData.EnergyAmount / 20f);
            orbLight.range = Mathf.Lerp(2f, 8f, orbData.EnergyAmount / 20f);
        }
        
        // Set emission color if material supports it
        if (orbRenderer != null && orbRenderer.material.HasProperty("_EmissionColor"))
        {
            orbRenderer.material.SetColor("_EmissionColor", energyColor * 0.5f);
            orbRenderer.material.EnableKeyword("_EMISSION");
        }
    }

    void Update()
    {
        if (!isInitialized) return;
        
        if (isFalling)
        {
            UpdateFalling();
        }
        else
        {
            // Animate floating/idle orb
            AnimateFloating();
        }
        
        // Always animate rotation and pulsing
        AnimateRotation();
        AnimatePulsing();
    }

    void StartFalling(Vector3 velocity)
    {
        isFalling = true;
        fallStartTime = Time.time;
        
        // Calculate where this orb should land (sphere surface)
        targetPosition = transform.position.normalized * this.worldRadius; 
        
        // Enable trail effect for falling
        if (trailEffect != null && !trailEffect.isPlaying)
        {
            trailEffect.Play();
        }
    }

    void UpdateFalling()
    {
        float distance = Vector3.Distance(startPosition, targetPosition);
        
        // Prevent division by zero - if we're already at target, stop falling
        if (distance < 0.01f)
        {
            transform.position = targetPosition;
            TriggerImpactEffect();
            isFalling = false;
            return;
        }
        
        float fallProgress = (Time.time - fallStartTime) * fallSpeed / distance;
        fallProgress = fallCurve.Evaluate(fallProgress);
        
        if (fallProgress >= 1f)
        {
            // Orb has reached the surface - let the server handle the impact
            transform.position = targetPosition;
            TriggerImpactEffect();
            isFalling = false;
        }
        else
        {
            // Interpolate position
            transform.position = Vector3.Lerp(startPosition, targetPosition, fallProgress);
        }
    }

    void AnimateFloating()
    {
        // Gentle floating motion for idle orbs
        float floatOffset = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        Vector3 floatPosition = startPosition + Vector3.up * floatOffset;
        transform.position = floatPosition;
    }

    void AnimateRotation()
    {
        // Continuous rotation
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        transform.Rotate(Vector3.right, rotationSpeed * 0.3f * Time.deltaTime);
    }

    void AnimatePulsing()
    {
        // Pulsing scale and light intensity
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
        transform.localScale = originalScale * pulse;
        
        // Pulse light intensity
        if (orbLight != null)
        {
            float baseLightIntensity = Mathf.Lerp(0.5f, 2.0f, orbData.EnergyAmount / 20f);
            orbLight.intensity = baseLightIntensity * pulse;
        }
        
        // Pulse emission
        if (orbRenderer != null && orbRenderer.material.HasProperty("_EmissionColor"))
        {
            Color emissionColor = energyColor * pulse * 0.5f;
            orbRenderer.material.SetColor("_EmissionColor", emissionColor);
        }
    }

    public void UpdateData(EnergyOrb newData)
    {
        Vector3 oldPosition = transform.position;
        orbData = newData;
        
        // Update position
        Vector3 newPosition = new Vector3(newData.Position.X, newData.Position.Y, newData.Position.Z);
        transform.position = newPosition;
        
        // Check if orb started falling
        Vector3 velocity = new Vector3(newData.Velocity.X, newData.Velocity.Y, newData.Velocity.Z);
        if (!isFalling && velocity.magnitude > 0.1f)
        {
            startPosition = oldPosition;
            StartFalling(velocity);
        }
        
        // Update visual effects if energy amount changed
        if (orbLight != null)
        {
            orbLight.intensity = Mathf.Lerp(0.5f, 2.0f, orbData.EnergyAmount / 20f);
            orbLight.range = Mathf.Lerp(2f, 8f, orbData.EnergyAmount / 20f);
        }
    }

    void TriggerImpactEffect()
    {
        // Play impact particle effect
        if (impactEffect != null)
        {
            impactEffect.transform.position = transform.position;
            impactEffect.Play();
        }
        
        // Play impact sound
        PlaySound(impactSound);
        
        // Flash effect
        StartCoroutine(FlashEffect());
    }

    System.Collections.IEnumerator FlashEffect()
    {
        // Bright flash when hitting surface
        if (orbLight != null)
        {
            float originalIntensity = orbLight.intensity;
            orbLight.intensity = originalIntensity * 3f;
            
            yield return new WaitForSeconds(0.1f);
            
            orbLight.intensity = originalIntensity;
        }
    }

    public void DestroyWithEffect()
    {
        // Trigger a nice destruction effect before destroying
        if (impactEffect != null)
        {
            // Detach and play impact effect
            impactEffect.transform.SetParent(null);
            impactEffect.Play();
            
            // Destroy the effect after it finishes
            Destroy(impactEffect.gameObject, impactEffect.main.duration + impactEffect.main.startLifetime.constantMax);
        }
        
        // Play destruction sound
        PlaySound(impactSound);
        
        // Fade out and destroy
        StartCoroutine(FadeOutAndDestroy());
    }

    System.Collections.IEnumerator FadeOutAndDestroy()
    {
        float fadeDuration = 0.5f;
        float startTime = Time.time;
        
        while (Time.time - startTime < fadeDuration)
        {
            float alpha = 1f - ((Time.time - startTime) / fadeDuration);
            
            // Fade renderer
            if (orbRenderer != null)
            {
                Color color = orbRenderer.material.color;
                color.a = alpha;
                orbRenderer.material.color = color;
            }
            
            // Fade light
            if (orbLight != null)
            {
                orbLight.intensity *= alpha;
            }
            
            // Shrink scale
            transform.localScale = originalScale * alpha;
            
            yield return null;
        }
        
        Destroy(gameObject);
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

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Handle interactions with players or other objects
        if (other.CompareTag("Player"))
        {
            // Could trigger special effects or early collection
   //         Debug.Log($"Energy orb {orbData.OrbId} touched by player");
        }
    }

    // Debug visualization in scene view
    void OnDrawGizmosSelected()
    {
        if (isFalling)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, targetPosition);
            Gizmos.DrawWireSphere(targetPosition, 1f);
        }
        
        // Show energy amount as a label
        if (isInitialized)
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, 
                $"Energy: {orbData.EnergyAmount:F1}\nType: {orbData.EnergyType}");
#endif
        }
    }
}