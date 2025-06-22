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
    
    [Header("Audio")]
    public AudioClip spawnSound;
    public AudioClip impactSound;
    
    [Header("Parabola Settings")]
    public float fixedVelocity = 25f;  // Fixed speed for all orbs
    public float maxHeight = 100f;     // Maximum height of parabola
    
    private EnergyOrb orbData;
    private Material originalMaterial;
    private Vector3 originalScale;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private bool isInitialized = false;
    private bool isFalling = false;
    private float journeyStartTime;
    private float journeyDuration;
    private Color energyColor;
    private float worldRadius;

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
        
        // Check if we should start falling based on velocity
        Vector3 velocity = new Vector3(data.Velocity.X, data.Velocity.Y, data.Velocity.Z);
        if (velocity.magnitude > 0.1f)
        {
            // Server has indicated this orb should fall - start local trajectory
            StartParabolicJourney();
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
            emission.rateOverTime = orbData.EnergyAmount * 5f;
            
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

    void StartParabolicJourney()
    {
        // Pick a random point on the globe surface
        targetPosition = GetRandomPointOnGlobe();
        
        // Ensure we start from current position (smooth any initial discrepancies)
        startPosition = transform.position;
        
        // Calculate journey duration based on fixed velocity
        float distance = Vector3.Distance(startPosition, targetPosition);
        journeyDuration = distance / fixedVelocity;
        
        // Prevent extremely short journeys that might cause jitter
        if (journeyDuration < 0.5f)
        {
            journeyDuration = 0.5f;
        }
        
        // Start the journey
        isFalling = true;
        journeyStartTime = Time.time;
        
        // Enable trail effect
        if (trailEffect != null && !trailEffect.isPlaying)
        {
            trailEffect.Play();
        }
    }

    Vector3 GetRandomPointOnGlobe()
    {
        // Generate random spherical coordinates
        float theta = Random.Range(0f, 2f * Mathf.PI);  // Azimuthal angle
        float phi = Mathf.Acos(Random.Range(-1f, 1f));  // Polar angle (properly distributed)
        
        // Convert to Cartesian coordinates on sphere surface
        float x = worldRadius * Mathf.Sin(phi) * Mathf.Cos(theta);
        float y = worldRadius * Mathf.Cos(phi);
        float z = worldRadius * Mathf.Sin(phi) * Mathf.Sin(theta);
        
        return new Vector3(x, y, z);
    }

    void Update()
    {
        if (!isInitialized) return;
        
        if (isFalling)
        {
            UpdateParabolicMotion();
        }
        
        // Always animate rotation and pulsing
        AnimateRotation();
        AnimatePulsing();
    }

    void UpdateParabolicMotion()
    {
        float elapsedTime = Time.time - journeyStartTime;
        float t = Mathf.Clamp01(elapsedTime / journeyDuration);
        
        if (t >= 1f)
        {
            // Journey complete
            transform.position = targetPosition;
            TriggerImpactEffect();
            isFalling = false;
            return;
        }
        
        // Calculate position along parabolic path
        Vector3 newPosition = CalculateParabolicPosition(t);
        
        // Store previous position for velocity calculation
        Vector3 previousPosition = transform.position;
        
        // Directly set position (no interpolation - let the parabolic math handle smoothness)
        transform.position = newPosition;
        
        // Calculate velocity for rotation
        Vector3 velocity = (newPosition - previousPosition) / Time.deltaTime;
        
        // Smooth rotation to face movement direction
        if (velocity.magnitude > 0.1f)
        {
            Vector3 up = transform.position.normalized; // Up is away from globe center
            Quaternion targetRotation = Quaternion.LookRotation(velocity, up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
        
        // Update trail effect
        if (trailEffect != null)
        {
            var main = trailEffect.main;
            main.startSpeed = (1f - t) * 2f; // Fade trail as we approach target
        }
    }

    Vector3 CalculateParabolicPosition(float t)
    {
        // Use smooth step for more natural motion
        float smoothT = Mathf.SmoothStep(0f, 1f, t);
        
        // Linear interpolation between start and end
        Vector3 linearPos = Vector3.Slerp(startPosition, targetPosition, smoothT);
        
        // Calculate height offset for parabola using a sine curve for smoothness
        float parabolicHeight = Mathf.Sin(t * Mathf.PI) * maxHeight;
        
        // Get the "up" direction (away from globe center)
        Vector3 upDirection = linearPos.normalized;
        
        // Apply height offset
        return linearPos + upDirection * parabolicHeight;
    }

    void AnimateRotation()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        transform.Rotate(Vector3.right, rotationSpeed * 0.3f * Time.deltaTime);
    }

    void AnimatePulsing()
    {
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseIntensity;
        transform.localScale = originalScale * pulse;
        
        if (orbLight != null)
        {
            float baseLightIntensity = Mathf.Lerp(0.5f, 2.0f, orbData.EnergyAmount / 20f);
            orbLight.intensity = baseLightIntensity * pulse;
        }
        
        if (orbRenderer != null && orbRenderer.material.HasProperty("_EmissionColor"))
        {
            Color emissionColor = energyColor * pulse * 0.5f;
            orbRenderer.material.SetColor("_EmissionColor", emissionColor);
        }
    }

    public void UpdateData(EnergyOrb newData)
    {
        orbData = newData;
        
        // IMPORTANT: Ignore position updates while orb is falling
        // The trajectory is calculated locally for smooth motion
        if (!isFalling)
        {
            // Only update position if we're not in a falling state
            Vector3 newPosition = new Vector3(newData.Position.X, newData.Position.Y, newData.Position.Z);
            
            // Check if this is a significant position change (teleport/respawn)
            float distance = Vector3.Distance(transform.position, newPosition);
            if (distance > 1f)
            {
                // This is likely a teleport or respawn - update immediately
                transform.position = newPosition;
                startPosition = newPosition;
            }
            
            // Check if server is telling us to start falling
            Vector3 velocity = new Vector3(newData.Velocity.X, newData.Velocity.Y, newData.Velocity.Z);
            if (velocity.magnitude > 0.1f && !isFalling)
            {
                // Server says we should be falling - start journey
                startPosition = transform.position;
                StartParabolicJourney();
            }
        }
        
        // Always update visual properties
        if (orbLight != null)
        {
            orbLight.intensity = Mathf.Lerp(0.5f, 2.0f, orbData.EnergyAmount / 20f);
            orbLight.range = Mathf.Lerp(2f, 8f, orbData.EnergyAmount / 20f);
        }
    }

    void TriggerImpactEffect()
    {
        if (impactEffect != null)
        {
            impactEffect.transform.position = transform.position;
            impactEffect.Play();
        }
        
        PlaySound(impactSound);
        StartCoroutine(FlashEffect());
    }

    System.Collections.IEnumerator FlashEffect()
    {
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
        if (impactEffect != null)
        {
            impactEffect.transform.SetParent(null);
            impactEffect.Play();
            Destroy(impactEffect.gameObject, impactEffect.main.duration + impactEffect.main.startLifetime.constantMax);
        }
        
        PlaySound(impactSound);
        StartCoroutine(FadeOutAndDestroy());
    }

    System.Collections.IEnumerator FadeOutAndDestroy()
    {
        float fadeDuration = 0.5f;
        float startTime = Time.time;
        
        while (Time.time - startTime < fadeDuration)
        {
            float alpha = 1f - ((Time.time - startTime) / fadeDuration);
            
            if (orbRenderer != null)
            {
                Color color = orbRenderer.material.color;
                color.a = alpha;
                orbRenderer.material.color = color;
            }
            
            if (orbLight != null)
            {
                orbLight.intensity *= 0.95f;
            }
            
            transform.localScale = originalScale * alpha;
            
            yield return null;
        }
        
        Destroy(gameObject);
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    Color GetEnergyColor(EnergyType type)
    {
        switch (type)
        {
            case EnergyType.Red:
                return new Color(1f, 0.2f, 0.2f); // Red
            case EnergyType.Green:
                return new Color(0.2f, 1f, 0.2f); // Green
            case EnergyType.Blue:
                return new Color(0.2f, 0.4f, 1f); // Blue
            case EnergyType.Cyan:
                return new Color(0.2f, 1f, 0.8f); // Cyan
            case EnergyType.Magenta:
                return new Color(0.8f, 0.2f, 1f); // Magenta
            case EnergyType.Yellow:
                return new Color(1f, 0.8f, 0.2f); // Yellow
            default:
                return Color.white;
        }
    }
}