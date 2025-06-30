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
    [SerializeField] private float extractionInterval = 2f; // seconds between extractions
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
    private float lastExtractionTime = 0f;
    
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
    public event Action<float> OnExtractionProgress;
    
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
        
        // Find player controller
        playerController = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
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
            conn.Reducers.OnExtractWavePacket += HandleExtractWavePacketResult;
            conn.Reducers.OnCaptureWavePacket += HandleWavePacketCaptured;
            
            // Subscribe to table events
            conn.Db.WavePacketExtraction.OnInsert += HandleWavePacketExtracted;
            conn.Db.WavePacketExtraction.OnDelete += HandleWavePacketExtractionRemoved;
        }
    }
    
    void OnDisable()
    {
        // Unsubscribe from events
        if (conn != null)
        {
            conn.Reducers.OnStartMining -= HandleStartMiningResult;
            conn.Reducers.OnStopMining -= HandleStopMiningResult;
            conn.Reducers.OnExtractWavePacket -= HandleExtractWavePacketResult;
            conn.Reducers.OnCaptureWavePacket -= HandleWavePacketCaptured;
            
            conn.Db.WavePacketExtraction.OnInsert -= HandleWavePacketExtracted;
            conn.Db.WavePacketExtraction.OnDelete -= HandleWavePacketExtractionRemoved;
        }
        
        // Clean up active mining
        if (isMining)
        {
            StopMining();
        }
    }
    
    void Update()
    {
        if (isMining && currentOrb != null)
        {
            // Update extraction beam
            if (activeExtractionBeam != null)
            {
                UpdateExtractionBeam();
            }
            
            // Check mining range
            CheckMiningRange();
            
            // Handle extraction timing
            float timeSinceLastExtraction = Time.time - lastExtractionTime;
            if (timeSinceLastExtraction >= extractionInterval)
            {
                // Request extraction from server
                if (currentOrbId.HasValue)
                {
                    conn.Reducers.ExtractWavePacket(currentOrbId.Value);
                    lastExtractionTime = Time.time;
                }
            }
            
            // Update extraction progress for UI
            float extractionProgress = timeSinceLastExtraction / extractionInterval;
            OnExtractionProgress?.Invoke(extractionProgress);
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
        
        // Get player
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
    
    public bool CanMineOrb(WavePacketOrb orb)
    {
        if (playerCrystal == null) return false;
        
        return orb.WavePacketComposition.Any(sample => 
            sample.Amount > 0 && CanMineWavePacket(sample.Signature, playerCrystal.CrystalType));
    }
    
    #endregion
    
    #region SpacetimeDB Event Handlers
    
    private void HandleStartMiningResult(ReducerEventContext ctx, ulong orbId)
    {
        Debug.Log($"Mining started for orb {orbId}");
        
        // Find the orb
        currentOrb = null;
        foreach (var orb in conn.Db.WavePacketOrb.Iter())
        {
            if (orb.OrbId == orbId)
            {
                currentOrb = orb;
                currentOrbId = orbId;
                break;
            }
        }
        
        if (currentOrb != null)
        {
            StartMiningVisuals();
            lastExtractionTime = Time.time - extractionInterval; // Allow immediate first extraction
        }
    }
    
    private void HandleStopMiningResult(ReducerEventContext ctx)
    {
        Debug.Log("Mining stopped");
        StopMining();
    }
    
    private void HandleExtractWavePacketResult(ReducerEventContext ctx, ulong orbId)
    {
        // Server has processed the extraction request
        // The actual packet info will come through WavePacketExtraction table insert
        Debug.Log($"Extraction processed for orb {orbId}");
    }
    
    private void HandleWavePacketExtracted(EventContext ctx, WavePacketExtraction extraction)
    {
        var player = GetLocalPlayer();
        if (player == null || extraction.PlayerId != player.PlayerId) return;
        
        Debug.Log($"Wave packet extracted: ID {extraction.WavePacketId}, Frequency {extraction.Signature.Frequency}");
        
        // Create visual packet
        CreateAndAnimateWavePacket(extraction);
        
        // Play audio
        if (packetCaptureClip != null && miningAudioSource != null)
        {
            miningAudioSource.PlayOneShot(packetCaptureClip, 0.5f);
        }
    }
    
    private void HandleWavePacketExtractionRemoved(EventContext ctx, WavePacketExtraction extraction)
    {
        // Extraction was removed (either captured or timed out)
        Debug.Log($"Wave packet extraction removed: {extraction.WavePacketId}");
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
                if (sample.Amount > 0 && CanMineWavePacket(sample.Signature, crystalType))
                {
                    hasMatchingPackets = true;
                    break;
                }
            }
            
            if (hasMatchingPackets && distance < closestDistance)
            {
                closestOrb = orb;
                closestDistance = distance;
            }
        }
        
        return closestOrb;
    }
    
    private bool CanMineWavePacket(WavePacketSignature signature, CrystalType crystal)
    {
        float frequency = signature.Frequency;
        float crystalFrequency = crystal switch
        {
            CrystalType.Red => 0.0f,    // 0 radians normalized
            CrystalType.Green => 0.333f, // 2π/3 radians normalized
            CrystalType.Blue => 0.667f,  // 4π/3 radians normalized
            _ => 0f
        };
        
        // Check if within ±π/6 radians (±1/12 in normalized)
        float diff = Mathf.Abs(frequency - crystalFrequency);
        // Handle wrap-around
        if (diff > 0.5f) diff = 1f - diff;
        
        return diff <= 1f/12f;
    }
    
    private void CheckMiningRange()
    {
        var player = GetLocalPlayer();
        if (player == null || currentOrb == null) return;
        
        var playerPos = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);
        var orbPos = new Vector3(currentOrb.Position.X, currentOrb.Position.Y, currentOrb.Position.Z);
        float distance = Vector3.Distance(playerPos, orbPos);
        
        if (distance > maxMiningRange)
        {
            Debug.Log("Out of mining range, stopping mining");
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
    
    private void CreateAndAnimateWavePacket(WavePacketExtraction extraction)
    {
        if (currentOrb == null) return;
        
        var particle = particlePool.Get();
        particle.PacketId = extraction.WavePacketId;
        particle.Signature = extraction.Signature;
        
        // Set initial position at orb
        var orbPos = new Vector3(currentOrb.Position.X, currentOrb.Position.Y, currentOrb.Position.Z);
        particle.transform.position = orbPos;
        
        // Set color based on frequency
        Color packetColor = GetWavePacketColor(extraction.Signature.Frequency);
        if (particle.Renderer != null)
        {
            particle.Renderer.material.color = packetColor;
            particle.Renderer.material.SetColor("_EmissionColor", packetColor * 2f);
        }
        
        if (particle.Light != null)
        {
            particle.Light.color = packetColor;
            particle.Light.intensity = 2f + extraction.Signature.Resonance;
        }
        
        if (particle.Trail != null)
        {
            particle.Trail.startColor = packetColor;
            particle.Trail.endColor = packetColor * (extraction.Signature.FluxPattern / 65535f);
        }
        
        // Track active particle
        activeParticles[extraction.WavePacketId] = particle;
        
        // Calculate flight time
        float currentTime = (float)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        float flightTime = (extraction.ExpectedArrival - extraction.DepartureTime) / 1000f;
        
        // Start animation coroutine
        var animCoroutine = StartCoroutine(AnimateWavePacket(particle, extraction.WavePacketId, flightTime));
        particleAnimations[extraction.WavePacketId] = animCoroutine;
    }
    
    private IEnumerator AnimateWavePacket(WavePacketParticle particle, ulong packetId, float flightTime)
    {
        if (playerTransform == null) yield break;
        
        Vector3 startPos = particle.transform.position;
        float elapsedTime = 0f;
        
        while (elapsedTime < flightTime)
        {
            if (particle == null || playerTransform == null) yield break;
            
            // Homing behavior - track player position
            Vector3 targetPos = playerTransform.position;
            float t = elapsedTime / flightTime;
            
            // Smooth curve animation
            t = particleSizeCurve.Evaluate(t);
            particle.transform.position = Vector3.Lerp(startPos, targetPos, t);
            
            // Update size based on distance
            float scale = Mathf.Lerp(1f, 0.5f, t);
            particle.transform.localScale = Vector3.one * scale;
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Packet arrived - capture it
        if (conn != null && conn.Identity != null)
        {
            conn.Reducers.CaptureWavePacket(packetId);
        }
        
        // Clean up
        activeParticles.Remove(packetId);
        particleAnimations.Remove(packetId);
        ReturnParticleToPool(particle);
    }
    
    #endregion
    
    #region Object Pool Methods
    
    private WavePacketParticle CreateWavePacketParticle()
    {
        if (wavePacketParticlePrefab == null) return null;
        
        var go = Instantiate(wavePacketParticlePrefab);
        return go.AddComponent<WavePacketParticle>();
    }
    
    private void OnGetFromPool(WavePacketParticle particle)
    {
        particle.gameObject.SetActive(true);
        particle.Reset();
    }
    
    private void OnReturnToPool(WavePacketParticle particle)
    {
        if (particle != null && particle.gameObject != null)
        {
            particle.gameObject.SetActive(false);
            particle.Reset();
        }
    }
    
    private void OnDestroyPoolObject(WavePacketParticle particle)
    {
        if (particle != null && particle.gameObject != null)
        {
            Destroy(particle.gameObject);
        }
    }
    
    private void ReturnParticleToPool(WavePacketParticle particle)
    {
        if (particlePool != null && particle != null)
        {
            particlePool.Release(particle);
        }
    }
    
    #endregion
    
    #region Helper Methods
    
    private Color GetCrystalColor(CrystalType type)
    {
        return type switch
        {
            CrystalType.Red => Color.red,
            CrystalType.Green => Color.green,
            CrystalType.Blue => Color.blue,
            _ => Color.white
        };
    }
    
    private Color GetWavePacketColor(float frequency)
    {
        // Use gradient if available, otherwise calculate from frequency
        if (frequencyColorGradient != null)
        {
            return frequencyColorGradient.Evaluate(frequency);
        }
        
        // Manual color calculation based on frequency
        float hue = frequency;
        return Color.HSVToRGB(hue, 1f, 1f);
    }
    
    #endregion
}