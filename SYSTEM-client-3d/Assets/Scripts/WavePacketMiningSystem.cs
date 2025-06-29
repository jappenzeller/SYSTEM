using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using SpacetimeDB.Types;
using SpacetimeDB.ClientApi;

public class WavePacketMiningSystem : MonoBehaviour
{
    [Header("Connection")]
    [SerializeField] private DbConnection conn;
    
    [Header("Mining Settings")]
    [SerializeField] private float maxMiningRange = 30f;
    [SerializeField] private float extractionRate = 2f; // seconds per packet
    [SerializeField] private float packetSpeed = 5f; // units per second
    
    [Header("Visual Components")]
    [SerializeField] private LineRenderer extractionBeamPrefab;
    [SerializeField] private GameObject wavePacketParticlePrefab;
    [SerializeField] private int particlePoolSize = 20;
    [SerializeField] private AnimationCurve particleSizeCurve;
    [SerializeField] private Gradient frequencyColorGradient;
    
    [Header("Audio")]
    [SerializeField] private AudioSource miningAudioSource;
    [SerializeField] private AudioClip miningStartClip;
    [SerializeField] private AudioClip miningLoopClip;
    [SerializeField] private AudioClip packetCaptureClip;
    [SerializeField] private AudioClip miningStopClip;
    
    // State
    private bool isMining = false;
    private ulong? currentOrbId = null;
    private WavePacketOrb currentOrb = null;
    private PlayerCrystal playerCrystal = null;
    
    // Visual components
    private LineRenderer activeExtractionBeam;
    private ObjectPool<WavePacketParticle> particlePool;
    private Dictionary<ulong, WavePacketParticle> activeParticles = new Dictionary<ulong, WavePacketParticle>();
    private Dictionary<ulong, Coroutine> particleAnimations = new Dictionary<ulong, Coroutine>();
    
    // References
    private PlayerController playerController;
    private Transform playerTransform;
    
    // Events
    public event Action<bool> OnMiningStateChanged;
    public event Action<ulong> OnWavePacketCaptured;
    public event Action<float> OnMiningProgress;
    
    private class WavePacketParticle : MonoBehaviour
    {
        public ulong PacketId { get; set; }
        public WavePacketSignature Signature { get; set; }
        public Renderer Renderer { get; private set; }
        public Light Light { get; private set; }
        public TrailRenderer Trail { get; private set; }
        
        void Awake()
        {
            Renderer = GetComponent<Renderer>();
            Light = GetComponent<Light>();
            Trail = GetComponent<TrailRenderer>();
        }
        
        public void Reset()
        {
            PacketId = 0;
            if (Trail != null) Trail.Clear();
        }
    }
    
    void Awake()
    {
        // Initialize object pool
        particlePool = new ObjectPool<WavePacketParticle>(
            CreateWavePacketParticle,
            OnGetFromPool,
            OnReturnToPool,
            OnDestroyPoolObject,
            collectionCheck: true,
            defaultCapacity: particlePoolSize,
            maxSize: particlePoolSize * 2
        );
        
        // Find player controller - Fixed: Use FindFirstObjectByType instead of FindObjectOfType
        playerController = Object.FindFirstObjectByType<PlayerController>();
        if (playerController != null)
        {
            playerTransform = playerController.transform;
        }
    }
    
    void OnEnable()
    {
        // Subscribe to SpacetimeDB events
        if (conn != null)
        {
            conn.Reducers.OnStartMining += HandleStartMiningResult;
            conn.Reducers.OnStopMining += HandleStopMiningResult;
            conn.Reducers.OnSendWavePacket += HandleWavePacketSent;
            conn.Reducers.OnCaptureWavePacket += HandleWavePacketCaptured;
        }
    }
    
    void OnDisable()
    {
        // Unsubscribe from events
        if (conn != null)
        {
            conn.Reducers.OnStartMining -= HandleStartMiningResult;
            conn.Reducers.OnStopMining -= HandleStopMiningResult;
            conn.Reducers.OnSendWavePacket -= HandleWavePacketSent;
            conn.Reducers.OnCaptureWavePacket -= HandleWavePacketCaptured;
        }
        
        // Clean up active mining
        if (isMining)
        {
            StopMining();
        }
    }
    
    void Update()
    {
        if (isMining && currentOrb != null && activeExtractionBeam != null)
        {
            UpdateExtractionBeam();
            CheckMiningRange();
        }
    }
    
    #region Public Methods
    
    public void ToggleMining()
    {
        if (isMining)
        {
            RequestStopMining();
        }
        else
        {
            RequestStartMining();
        }
    }
    
    public void RequestStartMining()
    {
        if (isMining) return;
        
        // Get player crystal
        var player = GetLocalPlayer();
        if (player == null)
        {
            Debug.LogError("Local player not found!");
            return;
        }
        
        // Check if player has a crystal
        playerCrystal = GetPlayerCrystal(player.PlayerId);
        if (playerCrystal == null)
        {
            Debug.LogError("No crystal equipped!");
            return;
        }
        
        // Find closest minable orb
        var targetOrb = FindClosestMinableOrb(player, playerCrystal.CrystalType);
        if (targetOrb == null)
        {
            Debug.Log("No minable orbs in range");
            return;
        }
        
        // Send mining request to server
        Debug.Log($"Requesting to mine orb {targetOrb.OrbId}");
        conn.Reducers.StartMining(targetOrb.OrbId);
    }
    
    public void RequestStopMining()
    {
        if (!isMining) return;
        
        Debug.Log("Requesting to stop mining");
        conn.Reducers.StopMining();
    }
    
    #endregion
    
    // Helper struct for wave packet arguments
    private struct WavePacketSentArgs
    {
        public ulong PlayerId;
        public ulong WavePacketId;
        public WavePacketSignature Signature;
        public ulong ExpectedArrival;
    }
    
    #region SpacetimeDB Event Handlers
    
    private void HandleStartMiningResult(ReducerEventContext ctx, ulong orbId)
    {
        if (orbId == currentOrbId)
        {
            Debug.Log($"Started mining orb {orbId}");
            StartMiningVisuals();
        }
    }
    
    private void HandleStopMiningResult(ReducerEventContext ctx)
    {
        Debug.Log("Stopped mining");
        StopMining();
    }
    
    private void HandleWavePacketSent(ReducerEventContext ctx, ulong playerId, ulong wavePacketId, WavePacketSignature signature, ulong expectedArrival)
    {
        var player = GetLocalPlayer();
        if (player == null || playerId != player.PlayerId) return;
        
        Debug.Log($"Wave packet sent: ID {wavePacketId}, Frequency {signature.Frequency}");
        
        // Create visual packet with args
        var args = new WavePacketSentArgs
        {
            PlayerId = playerId,
            WavePacketId = wavePacketId,
            Signature = signature,
            ExpectedArrival = expectedArrival
        };
        CreateAndAnimateWavePacket(args);
        
        // Play audio
        if (packetCaptureClip != null && miningAudioSource != null)
        {
            miningAudioSource.PlayOneShot(packetCaptureClip, 0.5f);
        }
    }
    
    private void HandleWavePacketCaptured(ReducerEventContext ctx, ulong wavePacketId)
    {
        Debug.Log($"Wave packet {wavePacketId} captured!");
        OnWavePacketCaptured?.Invoke(wavePacketId);
        
        // Clean up particle if it exists
        if (activeParticles.TryGetValue(wavePacketId, out var particle))
        {
            ReturnParticleToPool(particle);
        }
    }
    
    #endregion
    
    #region Mining Logic
    
    private Player GetLocalPlayer()
    {
        if (conn?.Identity == null) return null;
        
        foreach (var player in conn.Db.Player.Iter())
        {
            if (player.Identity == conn.Identity)
            {
                return player;
            }
        }
        return null;
    }
    
    private PlayerCrystal GetPlayerCrystal(ulong playerId)
    {
        foreach (var crystal in conn.Db.PlayerCrystal.Iter())
        {
            if (crystal.PlayerId == playerId)
            {
                return crystal;
            }
        }
        return null;
    }
    
    private WavePacketOrb FindClosestMinableOrb(Player player, CrystalType crystalType)
    {
        WavePacketOrb closestOrb = null;
        float closestDistance = float.MaxValue;
        
        var playerPos = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);
        
        foreach (var orb in conn.Db.WavePacketOrb.Iter())
        {
            // Check if in same world
            if (orb.WorldCoords.X != player.CurrentWorld.X ||
                orb.WorldCoords.Y != player.CurrentWorld.Y ||
                orb.WorldCoords.Z != player.CurrentWorld.Z)
                continue;
            
            // Check distance
            var orbPos = new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z);
            float distance = Vector3.Distance(playerPos, orbPos);
            
            if (distance > maxMiningRange) continue;
            
            // Check if orb has matching wave packets
            bool hasMatchingPackets = false;
            foreach (var sample in orb.WavePacketComposition)
            {
                if (CanMineWavePacket(sample.Signature, crystalType))
                {
                    hasMatchingPackets = true;
                    break;
                }
            }
            
            if (hasMatchingPackets && distance < closestDistance)
            {
                closestOrb = orb;
                closestDistance = distance;
                currentOrbId = orb.OrbId;
                currentOrb = orb;
            }
        }
        
        return closestOrb;
    }
    
    private bool CanMineWavePacket(WavePacketSignature signature, CrystalType crystal)
    {
        float crystalFreq = GetCrystalFrequency(crystal);
        float packetFreq = signature.Frequency;
        
        // Convert to radians for comparison
        float crystalRad = crystalFreq * 2f * Mathf.PI;
        float packetRad = packetFreq * 2f * Mathf.PI;
        
        float diff = Mathf.Abs(crystalRad - packetRad);
        
        // Handle wrap-around
        if (diff > Mathf.PI) 
            diff = 2f * Mathf.PI - diff;
        
        return diff <= Mathf.PI / 6f; // ±30 degrees tolerance
    }
    
    private float GetCrystalFrequency(CrystalType crystal)
    {
        return crystal switch
        {
            CrystalType.Red => 0f,           // 0 radians
            CrystalType.Green => 1f/3f,      // 2π/3 radians normalized to 0-1
            CrystalType.Blue => 2f/3f,       // 4π/3 radians normalized to 0-1
            _ => 0f
        };
    }
    
    private void CheckMiningRange()
    {
        if (!isMining || currentOrb == null || playerTransform == null) return;
        
        var orbPos = new Vector3(currentOrb.Position.X, currentOrb.Position.Y, currentOrb.Position.Z);
        float distance = Vector3.Distance(playerTransform.position, orbPos);
        
        if (distance > maxMiningRange)
        {
            Debug.Log("Out of mining range!");
            RequestStopMining();
        }
    }
    
    #endregion
    
    #region Visual Effects
    
    private void StartMiningVisuals()
    {
        isMining = true;
        OnMiningStateChanged?.Invoke(true);
        
        // Create extraction beam
        if (extractionBeamPrefab != null && currentOrb != null)
        {
            activeExtractionBeam = Instantiate(extractionBeamPrefab, transform);
            UpdateExtractionBeam();
        }
        
        // Start audio
        if (miningAudioSource != null)
        {
            if (miningStartClip != null)
                miningAudioSource.PlayOneShot(miningStartClip);
            
            if (miningLoopClip != null)
            {
                miningAudioSource.clip = miningLoopClip;
                miningAudioSource.loop = true;
                miningAudioSource.Play();
            }
        }
    }
    
    private void StopMining()
    {
        isMining = false;
        currentOrbId = null;
        currentOrb = null;
        OnMiningStateChanged?.Invoke(false);
        
        // Clean up extraction beam
        if (activeExtractionBeam != null)
        {
            Destroy(activeExtractionBeam.gameObject);
            activeExtractionBeam = null;
        }
        
        // Clean up in-flight packets
        foreach (var particle in activeParticles.Values)
        {
            // Add evaporation effect here if desired
            ReturnParticleToPool(particle);
        }
        activeParticles.Clear();
        
        // Stop animations
        foreach (var anim in particleAnimations.Values)
        {
            if (anim != null) StopCoroutine(anim);
        }
        particleAnimations.Clear();
        
        // Stop audio
        if (miningAudioSource != null)
        {
            miningAudioSource.Stop();
            if (miningStopClip != null)
                miningAudioSource.PlayOneShot(miningStopClip);
        }
    }
    
    private void UpdateExtractionBeam()
    {
        if (activeExtractionBeam == null || currentOrb == null || playerTransform == null) return;
        
        var orbPos = new Vector3(currentOrb.Position.X, currentOrb.Position.Y, currentOrb.Position.Z);
        
        activeExtractionBeam.SetPosition(0, playerTransform.position);
        activeExtractionBeam.SetPosition(1, orbPos);
        
        // Update beam color based on crystal type
        if (playerCrystal != null)
        {
            Color beamColor = GetCrystalColor(playerCrystal.CrystalType);
            activeExtractionBeam.startColor = beamColor;
            activeExtractionBeam.endColor = beamColor * 0.5f;
        }
    }
    
    private void CreateAndAnimateWavePacket(WavePacketSentArgs args)
    {
        if (currentOrb == null) return;
        
        var particle = particlePool.Get();
        particle.PacketId = args.WavePacketId;
        particle.Signature = args.Signature;
        
        // Set initial position at orb
        var orbPos = new Vector3(currentOrb.Position.X, currentOrb.Position.Y, currentOrb.Position.Z);
        particle.transform.position = orbPos;
        
        // Set color based on frequency
        Color packetColor = GetWavePacketColor(args.Signature.Frequency);
        if (particle.Renderer != null)
        {
            particle.Renderer.material.color = packetColor;
            particle.Renderer.material.SetColor("_EmissionColor", packetColor * 2f);
        }
        
        if (particle.Light != null)
        {
            particle.Light.color = packetColor;
            // Fixed: Use Resonance instead of Amplitude
            particle.Light.intensity = 2f + args.Signature.Resonance;
        }
        
        if (particle.Trail != null)
        {
            particle.Trail.startColor = packetColor;
            // Fixed: Use FluxPattern instead of Coherence (normalized to 0-1)
            particle.Trail.endColor = packetColor * (args.Signature.FluxPattern / 65535f);
        }
        
        // Track active particle
        activeParticles[args.WavePacketId] = particle;
        
        // Calculate travel time
        float distance = Vector3.Distance(playerTransform.position, orbPos);
        float travelTime = distance / packetSpeed;
        
        // Start animation
        var animCoroutine = StartCoroutine(AnimateWavePacket(particle, args.WavePacketId, travelTime));
        particleAnimations[args.WavePacketId] = animCoroutine;
    }
    
    private IEnumerator AnimateWavePacket(WavePacketParticle particle, ulong packetId, float duration)
    {
        Vector3 start = particle.transform.position;
        float elapsed = 0;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Smooth interpolation with easing
            float easedT = Mathf.SmoothStep(0, 1, t);
            
            // Update position with slight arc
            Vector3 current = Vector3.Lerp(start, playerTransform.position, easedT);
            current.y += Mathf.Sin(easedT * Mathf.PI) * 2f; // Arc motion
            particle.transform.position = current;
            
            // Update particle size
            float scale = particleSizeCurve.Evaluate(easedT);
            particle.transform.localScale = Vector3.one * scale;
            
            // Update light intensity
            if (particle.Light != null)
            {
                particle.Light.intensity = Mathf.Lerp(2f, 4f, easedT);
            }
            
            yield return null;
        }
        
        // Notify server of capture
        conn.Reducers.CaptureWavePacket(packetId);
        
        // Clean up
        activeParticles.Remove(packetId);
        particleAnimations.Remove(packetId);
        ReturnParticleToPool(particle);
    }
    
    #endregion
    
    #region Object Pool
    
    private WavePacketParticle CreateWavePacketParticle()
    {
        var go = Instantiate(wavePacketParticlePrefab);
        return go.AddComponent<WavePacketParticle>();
    }
    
    private void OnGetFromPool(WavePacketParticle particle)
    {
        particle.gameObject.SetActive(true);
    }
    
    private void OnReturnToPool(WavePacketParticle particle)
    {
        particle.Reset();
        particle.gameObject.SetActive(false);
    }
    
    private void OnDestroyPoolObject(WavePacketParticle particle)
    {
        Destroy(particle.gameObject);
    }
    
    private void ReturnParticleToPool(WavePacketParticle particle)
    {
        particlePool.Release(particle);
    }
    
    #endregion
    
    #region Helper Methods
    
    private Color GetWavePacketColor(float frequency)
    {
        // Use gradient if available, otherwise calculate
        if (frequencyColorGradient != null)
        {
            return frequencyColorGradient.Evaluate(frequency);
        }
        
        // Map frequency (0-1) to color wheel
        return Color.HSVToRGB(frequency, 0.8f, 1f);
    }
    
    private Color GetCrystalColor(CrystalType crystal)
    {
        float frequency = GetCrystalFrequency(crystal);
        return GetWavePacketColor(frequency);
    }
    
    public bool IsMining => isMining;
    public WavePacketOrb CurrentOrb => currentOrb;
    public PlayerCrystal CurrentCrystal => playerCrystal;
    
    #endregion
    
    #region Debug
    
    [ContextMenu("Debug - List Minable Orbs")]
    private void DebugListMinableOrbs()
    {
        var player = GetLocalPlayer();
        if (player == null)
        {
            Debug.Log("No local player found");
            return;
        }
        
        var crystal = GetPlayerCrystal(player.PlayerId);
        if (crystal == null)
        {
            Debug.Log("No crystal equipped");
            return;
        }
        
        var playerPos = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);
        int orbCount = 0;
        
        foreach (var orb in conn.Db.WavePacketOrb.Iter())
        {
            if (orb.WorldCoords.X != player.CurrentWorld.X ||
                orb.WorldCoords.Y != player.CurrentWorld.Y ||
                orb.WorldCoords.Z != player.CurrentWorld.Z)
                continue;
            
            var orbPos = new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z);
            float distance = Vector3.Distance(playerPos, orbPos);
            
            if (distance <= maxMiningRange)
            {
                int matchingPackets = 0;
                foreach (var sample in orb.WavePacketComposition)
                {
                    if (CanMineWavePacket(sample.Signature, crystal.CrystalType))
                    {
                        matchingPackets++;
                    }
                }
                
                if (matchingPackets > 0)
                {
                    Debug.Log($"Orb {orb.OrbId}: {distance:F1}m away, {matchingPackets} matching packets, {orb.TotalWavePackets} total");
                    orbCount++;
                }
            }
        }
        
        Debug.Log($"Found {orbCount} minable orbs in range");
    }
    
    // Fixed: Update storage check to use correct property name
    [ContextMenu("Debug - Show Storage")]
    private void DebugShowStorage()
    {
        var player = GetLocalPlayer();
        if (player == null)
        {
            Debug.Log("No local player found");
            return;
        }
        
        Debug.Log("=== Wave Packet Storage ===");
        foreach (var storage in conn.Db.WavePacketStorage.Iter())
        {
            if (storage.OwnerType == "player" && storage.OwnerId == player.PlayerId)
            {
                Debug.Log($"Band: {storage.FrequencyBand}, Count: {storage.TotalWavePackets}");
                // Fixed: Use SignatureSamples instead of WavePacketComposition
                foreach (var sample in storage.SignatureSamples)
                {
                    Debug.Log($"  - Frequency: {sample.Signature.Frequency:F3}, Amount: {sample.Amount}");
                }
            }
        }
    }
    
    #endregion
}