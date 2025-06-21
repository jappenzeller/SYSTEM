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
    
    [Header("Physics")]
    public float gravity = 30f;  // Increased for more noticeable arc
    public float airDensity = 0.02f;  // Reduced for less drag
    private Vector3 currentVelocity;
    private bool usePhysicsMovement = true;
    
    [Header("Network Interpolation")]
    public float interpolationSpeed = 10f;
    private Vector3 networkPosition;
    private Vector3 networkVelocity;
    private bool hasNetworkUpdate = false;
    
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
        currentVelocity = velocity;
        usePhysicsMovement = true;
        
        // Calculate where this orb should land (sphere surface) - only used for interpolation mode
        targetPosition = transform.position.normalized * this.worldRadius; 
        
        // Enable trail effect for falling
        if (trailEffect != null && !trailEffect.isPlaying)
        {
            trailEffect.Play();
        }
    }

    void UpdateFalling()
    {
        if (usePhysicsMovement)
        {
            UpdatePhysicsMovement();
        }
        else
        {
            UpdateInterpolationMovement();
        }
    }

    void UpdatePhysicsMovement()
    {
        // Handle network interpolation if we have updates
        if (hasNetworkUpdate)
        {
            // Smoothly interpolate position if the difference is significant
            float positionDifference = Vector3.Distance(transform.position, networkPosition);
            
            if (positionDifference > 0.1f && positionDifference < 10f) // Don't interpolate huge jumps
            {
                transform.position = Vector3.Lerp(transform.position, networkPosition, Time.deltaTime * interpolationSpeed);
                
                // Blend velocity too
                currentVelocity = Vector3.Lerp(currentVelocity, networkVelocity, Time.deltaTime * interpolationSpeed);
            }
            else if (positionDifference >= 10f)
            {
                // Snap to position for large differences
                transform.position = networkPosition;
                currentVelocity = networkVelocity;
            }
            
            // Clear the flag once we're close enough
            if (positionDifference < 0.5f)
            {
                hasNetworkUpdate = false;
            }
        }
        
        // Get world center (assuming world is at origin)
        Vector3 worldCenter = Vector3.zero;
        
        // Calculate gravity direction (from orb towards world center)
        Vector3 toCenter = worldCenter - transform.position;
        Vector3 gravityDirection = toCenter.normalized;
        
        // Apply spherical gravity (stronger when farther from surface)
        float distanceFromCenter = transform.position.magnitude;
        float heightAboveSurface = distanceFromCenter - worldRadius;
        float gravityStrength = gravity * (1f + heightAboveSurface * 0.01f); // Slightly stronger when higher
        Vector3 gravityForce = gravityDirection * gravityStrength;
        
        // Apply air resistance (proportional to velocity squared, but gentler)
        Vector3 dragForce = Vector3.zero;
        if (currentVelocity.magnitude > 0.1f)
        {
            dragForce = -currentVelocity.normalized * (currentVelocity.magnitude * airDensity);
        }
        
        // Update velocity
        currentVelocity += (gravityForce + dragForce) * Time.deltaTime;
        
        // Update position
        Vector3 newPosition = transform.position + currentVelocity * Time.deltaTime;
        
        // Check if we've hit the world surface
        float newDistanceToCenter = newPosition.magnitude;
        if (newDistanceToCenter <= worldRadius + 0.5f) // Small buffer to prevent clipping
        {
            // Calculate impact point on perfect sphere
            Vector3 impactPoint = newPosition.normalized * worldRadius;
            
            // Smooth landing
            transform.position = Vector3.Lerp(transform.position, impactPoint, Time.deltaTime * 10f);
            
            if (Vector3.Distance(transform.position, impactPoint) < 0.1f)
            {
                transform.position = impactPoint;
                TriggerImpactEffect();
                isFalling = false;
                currentVelocity = Vector3.zero;
                hasNetworkUpdate = false; // Clear any pending updates
            }
        }
        else
        {
            transform.position = newPosition;
            
            // Smooth rotation to face movement direction
            if (currentVelocity.magnitude > 0.5f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(currentVelocity, -gravityDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
            }
        }
        
        // Add subtle arc visualization with trail
        if (trailEffect != null)
        {
            var main = trailEffect.main;
            main.startSpeed = currentVelocity.magnitude * 0.1f;
        }
    }

    void UpdateInterpolationMovement()
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
        orbData = newData;
        
        // Store network update for interpolation
        networkPosition = new Vector3(newData.Position.X, newData.Position.Y, newData.Position.Z);
        networkVelocity = new Vector3(newData.Velocity.X, newData.Velocity.Y, newData.Velocity.Z);
        hasNetworkUpdate = true;
        
        // If not falling yet and velocity indicates it should be, start falling
        if (!isFalling && networkVelocity.magnitude > 0.1f)
        {
            startPosition = transform.position;
            currentVelocity = networkVelocity;
            StartFalling(networkVelocity);
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
            // Debug.Log($"Energy orb {orbData.OrbId} touched by player");
        }
    }

    // Debug visualization in scene view
    void OnDrawGizmosSelected()
    {
        if (isFalling && usePhysicsMovement)
        {
            // Show current velocity vector
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, currentVelocity * 0.5f);
            
            // Show gravity direction
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, -transform.position.normalized * 5f);
        }
        else if (isFalling)
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