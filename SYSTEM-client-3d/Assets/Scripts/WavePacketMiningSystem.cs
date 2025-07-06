using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;

/// <summary>
/// Manages the wave packet mining system.
/// Now subscribes directly to SpaceTimeDB events instead of using EventBus.
/// </summary>
public class WavePacketMiningSystem : MonoBehaviour
{
    [Header("Mining Settings")]
    [SerializeField] private float miningRange = 30f;
    [SerializeField] private float extractionInterval = 2f; // 2 seconds between extractions
    [SerializeField] private GameObject extractionBeamPrefab;
    [SerializeField] private GameObject wavePacketParticlePrefab;
    
    [Header("Audio")]
    [SerializeField] private AudioSource miningAudioSource;
    [SerializeField] private AudioClip miningStartClip;
    [SerializeField] private AudioClip miningLoopClip;
    [SerializeField] private AudioClip miningStopClip;
    [SerializeField] private AudioClip packetExtractClip;
    [SerializeField] private AudioClip packetCaptureClip;
    
    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;
    
    // State
    private bool isMining = false;
    private ulong? currentOrbId = null;
    private WavePacketOrb currentOrb = null;
    private PlayerCrystal playerCrystal = null;
    private float lastExtractionTime = 0f;
    
    // Visual elements
    private LineRenderer activeExtractionBeam;
    private Dictionary<ulong, WavePacketParticle> activeParticles = new Dictionary<ulong, WavePacketParticle>();
    private Dictionary<ulong, Coroutine> particleAnimations = new Dictionary<ulong, Coroutine>();
    
    // Cached references
    private IDbConnection conn;
    private Transform playerTransform;
    private WavePacketParticlePool particlePool;
    
    // Events
    public static event System.Action<bool> OnMiningStateChanged;
    public static event System.Action<float> OnExtractionProgress;
    public static event System.Action<WavePacketSignature> OnWavePacketCaptured;
    
    void Start()
    {
        if (!GameManager.IsConnected())
        {
            enabled = false;
            return;
        }
        
        conn = GameManager.Conn;
        
        // Find particle pool
        particlePool = GetComponent<WavePacketParticlePool>();
        if (particlePool == null)
        {
            particlePool = gameObject.AddComponent<WavePacketParticlePool>();
        }
        
        SubscribeToEvents();
    }
    
    void OnDestroy()
    {
        UnsubscribeFromEvents();
        
        // Clean up active mining
        if (isMining)
        {
            StopMining();
        }
    }
    
    void SubscribeToEvents()
    {
        // Reducer responses
        conn.Reducers.OnStartMining += HandleStartMiningResult;
        conn.Reducers.OnStopMining += HandleStopMiningResult;
        conn.Reducers.OnExtractWavePacket += HandleExtractWavePacketResult;
        conn.Reducers.OnCaptureWavePacket += HandleCaptureWavePacketResult;
        
        // Table events
        conn.Db.WavePacketExtraction.OnInsert += HandleWavePacketExtracted;
        conn.Db.WavePacketExtraction.OnDelete += HandleWavePacketExtractionRemoved;
    }
    
    void UnsubscribeFromEvents()
    {
        if (conn != null)
        {
            conn.Reducers.OnStartMining -= HandleStartMiningResult;
            conn.Reducers.OnStopMining -= HandleStopMiningResult;
            conn.Reducers.OnExtractWavePacket -= HandleExtractWavePacketResult;
            conn.Reducers.OnCaptureWavePacket -= HandleCaptureWavePacketResult;
            
            conn.Db.WavePacketExtraction.OnInsert -= HandleWavePacketExtracted;
            conn.Db.WavePacketExtraction.OnDelete -= HandleWavePacketExtractionRemoved;
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
                if (currentOrbId.HasValue && conn != null)
                {
                    conn.Reducers.ExtractWavePacket();
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
        if (ctx.Event.Status is Status.Committed)
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
        else if (ctx.Event.Status is Status.Failed failed)
        {
            Debug.LogError($"Failed to start mining: {failed.Message}");
        }
    }
    
    private void HandleStopMiningResult(ReducerEventContext ctx)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            Debug.Log("Mining stopped");
            StopMining();
        }
    }
    
    private void HandleExtractWavePacketResult(ReducerEventContext ctx)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            // Server has processed the extraction request
            // The actual packet info will come through WavePacketExtraction table insert
            Debug.Log("Extraction processed");
        }
        else if (ctx.Event.Status is Status.Failed failed)
        {
            Debug.LogError($"Failed to extract wave packet: {failed.Message}");
        }
    }
    
    private void HandleCaptureWavePacketResult(ReducerEventContext ctx, ulong wavePacketId)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            Debug.Log($"Wave packet {wavePacketId} captured successfully!");
            
            // Remove the visual particle if it exists
            if (activeParticles.TryGetValue(wavePacketId, out var particle))
            {
                // Play capture effect
                if (packetCaptureClip != null && miningAudioSource != null)
                {
                    miningAudioSource.PlayOneShot(packetCaptureClip, 0.7f);
                }
                
                // Clean up particle
                if (particleAnimations.TryGetValue(wavePacketId, out var anim))
                {
                    if (anim != null) StopCoroutine(anim);
                    particleAnimations.Remove(wavePacketId);
                }
                
                ReturnParticleToPool(particle);
                activeParticles.Remove(wavePacketId);
            }
        }
    }
    
    private void HandleWavePacketExtracted(EventContext ctx, WavePacketExtraction extraction)
    {
        var player = GetLocalPlayer();
        if (player == null || extraction.PlayerId != player.PlayerId) return;
        
        Debug.Log($"Wave packet extracted: ID {extraction.WavePacketId}, Frequency {extraction.Signature.Frequency}");
        
        // Create visual packet
        CreateAndAnimateWavePacket(extraction);
        
        // Play audio
        if (packetExtractClip != null && miningAudioSource != null)
        {
            miningAudioSource.PlayOneShot(packetExtractClip, 0.5f);
        }
    }
    
    private void HandleWavePacketExtractionRemoved(EventContext ctx, WavePacketExtraction extraction)
    {
        // Extraction was removed (either captured or timed out)
        Debug.Log($"Wave packet extraction removed: {extraction.WavePacketId}");
    }
    
    #endregion
    
    #region Mining Logic
    
    private void StartMiningVisuals()
    {
        isMining = true;
        OnMiningStateChanged?.Invoke(true);
        
        // Get player transform
        var localPlayer = GameManager.GetLocalPlayer();
        if (localPlayer != null)
        {
            var playerObj = GameObject.Find($"Player_{localPlayer.Name}");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
        }
        
        // Create extraction beam
        if (extractionBeamPrefab != null && playerTransform != null)
        {
            var beamObj = Instantiate(extractionBeamPrefab);
            activeExtractionBeam = beamObj.GetComponent<LineRenderer>();
            if (activeExtractionBeam == null)
            {
                activeExtractionBeam = beamObj.AddComponent<LineRenderer>();
            }
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
        var renderer = particle.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = GetFrequencyColor(extraction.Signature.Frequency);
        }
        
        activeParticles[extraction.WavePacketId] = particle;
        
        // Calculate flight time
        float distance = Vector3.Distance(orbPos, playerTransform.position);
        float flightTime = distance / 5f; // 5 units per second
        
        // Start animation
        var anim = StartCoroutine(AnimateWavePacket(particle, orbPos, playerTransform, flightTime, extraction.WavePacketId));
        particleAnimations[extraction.WavePacketId] = anim;
    }
    
    private IEnumerator AnimateWavePacket(WavePacketParticle particle, Vector3 startPos, Transform target, float duration, ulong packetId)
    {
        float elapsed = 0f;
        
        while (elapsed < duration && target != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Curved path
            Vector3 currentPos = Vector3.Lerp(startPos, target.position, t);
            currentPos.y += Mathf.Sin(t * Mathf.PI) * 2f; // Arc
            
            particle.transform.position = currentPos;
            
            yield return null;
        }
        
        // Reached player - request capture
        if (conn != null)
        {
            conn.Reducers.CaptureWavePacket(packetId);
        }
    }
    
    private void CheckMiningRange()
    {
        if (currentOrb == null || playerTransform == null) return;
        
        var orbPos = new Vector3(currentOrb.Position.X, currentOrb.Position.Y, currentOrb.Position.Z);
        float distance = Vector3.Distance(playerTransform.position, orbPos);
        
        if (distance > miningRange)
        {
            Debug.Log("Out of mining range");
            RequestStopMining();
        }
    }
    
    #endregion
    
    #region Helper Methods
    
    private Player GetLocalPlayer()
    {
        return GameManager.GetLocalPlayer();
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
            
            // Check if orb has compatible packets
            if (!CanMineOrb(orb))
                continue;
            
            // Check distance
            var orbPos = new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z);
            float distance = Vector3.Distance(playerPos, orbPos);
            
            if (distance <= miningRange && distance < closestDistance)
            {
                closestDistance = distance;
                closestOrb = orb;
            }
        }
        
        return closestOrb;
    }
    
    private bool CanMineWavePacket(WavePacketSignature signature, CrystalType crystalType)
    {
        float crystalFrequency = GetCrystalFrequency(crystalType);
        float frequencyDiff = Mathf.Abs(signature.Frequency - crystalFrequency);
        
        // Can mine packets within ±π/6 radians (30 degrees)
        float maxDiff = 1f / 6f; // Since frequency is normalized 0-1
        return frequencyDiff <= maxDiff;
    }
    
    private float GetCrystalFrequency(CrystalType type)
    {
        switch (type)
        {
            case CrystalType.Red: return 0.2f;
            case CrystalType.Green: return 0.575f;
            case CrystalType.Blue: return 0.725f;
            default: return 0.5f;
        }
    }
    
    private Color GetCrystalColor(CrystalType type)
    {
        switch (type)
        {
            case CrystalType.Red: return Color.red;
            case CrystalType.Green: return Color.green;
            case CrystalType.Blue: return Color.blue;
            default: return Color.white;
        }
    }
    
    private Color GetFrequencyColor(float frequency)
    {
        // Map frequency (0-1) to color spectrum
        return Color.HSVToRGB(frequency, 1f, 1f);
    }
    
    private void ReturnParticleToPool(WavePacketParticle particle)
    {
        if (particlePool != null)
        {
            particlePool.Return(particle);
        }
        else
        {
            Destroy(particle.gameObject);
        }
    }
    
    void Log(string message)
    {
        if (debugLogging)
            Debug.Log($"[MiningSystem] {message}");
    }
    
    #endregion
}