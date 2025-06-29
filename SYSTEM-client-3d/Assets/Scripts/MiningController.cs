using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using SpacetimeDB;
using SpacetimeDB.Types;
using SpacetimeDB.ClientApi;

public class MiningController : MonoBehaviour
{
    [Header("Connection")]
    [SerializeField] private DbConnection conn;
    
    [Header("Mining Settings")]
    [SerializeField] private float maxRange = 30f;
    [SerializeField] private float extractionInterval = 2f;
    [SerializeField] private float wavePacketSpeed = 5f;
    
    [Header("Visual Components")]
    [SerializeField] private LineRenderer tractorBeamPrefab;
    [SerializeField] private GameObject wavePacketPrefab;
    [SerializeField] private ParticleSystem miningParticles;
    [SerializeField] private Transform crystalMount;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip startMiningClip;
    [SerializeField] private AudioClip stopMiningClip;
    [SerializeField] private AudioClip packetExtractClip;
    [SerializeField] private AudioClip packetCaptureClip;
    
    // State
    private bool isMining = false;
    private ulong? targetOrbId = null;
    private WavePacketOrb targetOrb = null;
    private LineRenderer activeTractorBeam;
    private Dictionary<ulong, GameObject> inFlightPackets = new Dictionary<ulong, GameObject>();
    private Dictionary<ulong, Coroutine> packetCoroutines = new Dictionary<ulong, Coroutine>();
    
    // References
    private Transform playerTransform;
    private PlayerController playerController;
    
    // Crystal visual
    private GameObject crystalVisual;
    
    // Events
    public event Action<bool> OnMiningStateChanged;
    public event Action<WavePacketSignature> OnPacketExtracted;
    public event Action<WavePacketSignature> OnPacketCaptured;
    
    // Helper struct for wave packet arguments
    private struct WavePacketSentArgs
    {
        public ulong PlayerId;
        public ulong WavePacketId;
        public WavePacketSignature Signature;
        public ulong ExpectedArrival;
    }
    
    void Awake()
    {
        playerTransform = transform;
        playerController = GetComponent<PlayerController>();
    }
    
    void OnEnable()
    {
        if (conn != null)
        {
            SubscribeToEvents();
        }
    }
    
    void OnDisable()
    {
        if (conn != null)
        {
            UnsubscribeFromEvents();
        }
        
        if (isMining)
        {
            StopMiningVisuals();
        }
    }
    
    void Update()
    {
        if (isMining && targetOrb != null)
        {
            UpdateTractorBeam();
            CheckMiningDistance();
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
        
        // Fixed: Use proper iteration instead of Where clause
        Player localPlayer = null;
        foreach (var player in conn.Db.Player.Iter())
        {
            if (player.Identity == conn.Identity)
            {
                localPlayer = player;
                break;
            }
        }
        
        if (localPlayer == null)
        {
            Debug.LogError("Local player not found");
            return;
        }
        
        // Check for crystal
        PlayerCrystal playerCrystal = null;
        foreach (var crystal in conn.Db.PlayerCrystal.Iter())
        {
            if (crystal.PlayerId == localPlayer.PlayerId)
            {
                playerCrystal = crystal;
                break;
            }
        }
        
        if (playerCrystal == null)
        {
            Debug.LogError("No crystal equipped!");
            return;
        }
        
        // Find closest compatible orb
        var orb = FindClosestMinableOrb(localPlayer, playerCrystal.CrystalType);
        if (orb == null)
        {
            Debug.Log("No minable orbs in range");
            return;
        }
        
        targetOrbId = orb.OrbId;
        targetOrb = orb;
        
        // Request mining from server
        conn.Reducers.StartMining(orb.OrbId);
    }
    
    public void RequestStopMining()
    {
        if (!isMining) return;
        
        conn.Reducers.StopMining();
    }
    
    public bool CanMineOrb(WavePacketOrb orb)
    {
        if (orb == null) return false;
        
        var player = GetLocalPlayer();
        if (player == null) return false;
        
        var crystal = GetPlayerCrystal(player.PlayerId);
        if (crystal == null) return false;
        
        // Check distance
        var playerPos = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);
        var orbPos = new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z);
        float distance = Vector3.Distance(playerPos, orbPos);
        
        if (distance > maxRange) return false;
        
        // Check if orb has matching wave packets
        foreach (var sample in orb.WavePacketComposition)
        {
            if (DoesSignatureMatchCrystal(sample.Signature, crystal.CrystalType))
            {
                return true;
            }
        }
        
        return false;
    }
    
    #endregion
    
    #region Event Handlers
    
    private void SubscribeToEvents()
    {
        conn.Reducers.OnStartMining += HandleStartMiningResult;
        conn.Reducers.OnStopMining += HandleStopMiningResult;
        conn.Reducers.OnSendWavePacket += HandleWavePacketSent;
        conn.Reducers.OnCaptureWavePacket += HandleWavePacketCaptured;
    }
    
    private void UnsubscribeFromEvents()
    {
        conn.Reducers.OnStartMining -= HandleStartMiningResult;
        conn.Reducers.OnStopMining -= HandleStopMiningResult;
        conn.Reducers.OnSendWavePacket -= HandleWavePacketSent;
        conn.Reducers.OnCaptureWavePacket -= HandleWavePacketCaptured;
    }
    
    private void HandleStartMiningResult(ReducerEventContext ctx, ulong orbId)
    {
        StartMiningVisuals();
        Debug.Log($"Started mining orb {orbId}");
    }
    
    private void HandleStopMiningResult(ReducerEventContext ctx)
    {
        StopMiningVisuals();
        Debug.Log("Stopped mining");
    }
    
    private void HandleWavePacketSent(ReducerEventContext ctx, ulong playerId, ulong wavePacketId, WavePacketSignature signature, ulong expectedArrival)
    {
        var player = GetLocalPlayer();
        if (player == null || playerId != player.PlayerId) return;
        
        // Create args struct for visual
        var args = new WavePacketSentArgs
        {
            PlayerId = playerId,
            WavePacketId = wavePacketId,
            Signature = signature,
            ExpectedArrival = expectedArrival
        };
        
        CreateWavePacketVisual(args);
        OnPacketExtracted?.Invoke(signature);
        
        if (packetExtractClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(packetExtractClip, 0.7f);
        }
    }
    
    private void HandleWavePacketCaptured(ReducerEventContext ctx, ulong wavePacketId)
    {
        Debug.Log($"Captured wave packet {wavePacketId}");
        
        if (inFlightPackets.ContainsKey(wavePacketId))
        {
            CleanupPacket(wavePacketId);
        }
        
        if (packetCaptureClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(packetCaptureClip);
        }
    }
    
    #endregion
    
    #region Mining Logic
    
    private WavePacketOrb FindClosestMinableOrb(Player player, CrystalType crystalType)
    {
        WavePacketOrb closestOrb = null;
        float closestDistance = float.MaxValue;
        
        var playerPos = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);
        
        // Fixed: Use iteration instead of Where clause
        foreach (var orb in conn.Db.WavePacketOrb.Iter())
        {
            // Check if in same world
            if (orb.WorldCoords.X != player.CurrentWorld.X ||
                orb.WorldCoords.Y != player.CurrentWorld.Y ||
                orb.WorldCoords.Z != player.CurrentWorld.Z)
                continue;
            
            var orbPos = new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z);
            float distance = Vector3.Distance(playerPos, orbPos);
            
            if (distance > maxRange) continue;
            
            // Check if orb has matching packets
            bool hasMatching = false;
            foreach (var sample in orb.WavePacketComposition)
            {
                if (DoesSignatureMatchCrystal(sample.Signature, crystalType))
                {
                    hasMatching = true;
                    break;
                }
            }
            
            if (hasMatching && distance < closestDistance)
            {
                closestOrb = orb;
                closestDistance = distance;
            }
        }
        
        return closestOrb;
    }
    
    private bool DoesSignatureMatchCrystal(WavePacketSignature signature, CrystalType crystal)
    {
        // Map crystal types to frequency ranges
        float crystalFrequency = crystal switch
        {
            CrystalType.Red => 0f,           // 0 radians
            CrystalType.Green => 1f/3f,      // 2π/3 radians normalized
            CrystalType.Blue => 2f/3f,       // 4π/3 radians normalized
            _ => 0f
        };
        
        float signatureRadian = signature.Frequency * 2f * Mathf.PI;
        float crystalRadian = crystalFrequency * 2f * Mathf.PI;
        float diff = Mathf.Abs(signatureRadian - crystalRadian);
        
        // Handle wrap-around
        if (diff > Mathf.PI) 
            diff = 2f * Mathf.PI - diff;
        
        return diff <= Mathf.PI / 6f; // ±30 degrees
    }
    
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
    
    private void CheckMiningDistance()
    {
        if (!isMining || targetOrb == null) return;
        
        var orbPos = new Vector3(targetOrb.Position.X, targetOrb.Position.Y, targetOrb.Position.Z);
        float distance = Vector3.Distance(playerTransform.position, orbPos);
        
        if (distance > maxRange)
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
        
        // Create tractor beam
        if (tractorBeamPrefab != null && targetOrb != null)
        {
            activeTractorBeam = Instantiate(tractorBeamPrefab, transform);
            UpdateTractorBeam();
        }
        
        // Start particle effect
        if (miningParticles != null)
        {
            miningParticles.Play();
        }
        
        // Update crystal visual
        UpdateCrystalVisual();
        
        // Play start sound
        if (startMiningClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(startMiningClip);
        }
    }
    
    private void StopMiningVisuals()
    {
        isMining = false;
        targetOrbId = null;
        targetOrb = null;
        OnMiningStateChanged?.Invoke(false);
        
        // Destroy tractor beam
        if (activeTractorBeam != null)
        {
            Destroy(activeTractorBeam.gameObject);
            activeTractorBeam = null;
        }
        
        // Stop particles
        if (miningParticles != null)
        {
            miningParticles.Stop();
        }
        
        // Clean up in-flight packets
        foreach (var kvp in inFlightPackets)
        {
            Destroy(kvp.Value);
        }
        inFlightPackets.Clear();
        
        // Stop all packet animations
        foreach (var coroutine in packetCoroutines.Values)
        {
            if (coroutine != null) StopCoroutine(coroutine);
        }
        packetCoroutines.Clear();
        
        // Play stop sound
        if (stopMiningClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(stopMiningClip);
        }
    }
    
    private void UpdateTractorBeam()
    {
        if (activeTractorBeam == null || targetOrb == null) return;
        
        var orbPos = new Vector3(targetOrb.Position.X, targetOrb.Position.Y, targetOrb.Position.Z);
        
        activeTractorBeam.SetPosition(0, playerTransform.position + Vector3.up * 0.5f);
        activeTractorBeam.SetPosition(1, orbPos);
        
        // Update color based on crystal type
        var player = GetLocalPlayer();
        if (player != null)
        {
            var crystal = GetPlayerCrystal(player.PlayerId);
            if (crystal != null)
            {
                Color beamColor = GetCrystalColor(crystal.CrystalType);
                activeTractorBeam.startColor = beamColor * 0.8f;
                activeTractorBeam.endColor = beamColor * 0.3f;
            }
        }
    }
    
    private void UpdateCrystalVisual()
    {
        var player = GetLocalPlayer();
        if (player == null) return;
        
        var crystal = GetPlayerCrystal(player.PlayerId);
        if (crystal == null) return;
        
        // Fixed: Use CrystalType instead of non-existent CrystalId
        // Create or update crystal visual based on type
        if (crystalMount != null)
        {
            // Clear existing crystal
            foreach (Transform child in crystalMount)
            {
                Destroy(child.gameObject);
            }
            
            // Create new crystal visual
            // Fixed: Use CrystalType for visual selection
            string crystalPrefabName = crystal.CrystalType switch
            {
                CrystalType.Red => "RedCrystal",
                CrystalType.Green => "GreenCrystal",
                CrystalType.Blue => "BlueCrystal",
                _ => "DefaultCrystal"
            };
            
            // Load and instantiate crystal prefab
            var prefab = Resources.Load<GameObject>($"Crystals/{crystalPrefabName}");
            if (prefab != null)
            {
                crystalVisual = Instantiate(prefab, crystalMount);
                
                // Set crystal color
                var renderer = crystalVisual.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = GetCrystalColor(crystal.CrystalType);
                }
            }
        }
    }
    
    private void CreateWavePacketVisual(WavePacketSentArgs args)
    {
        if (wavePacketPrefab == null || targetOrb == null) return;
        
        // Create packet visual
        var packet = Instantiate(wavePacketPrefab);
        var orbPos = new Vector3(targetOrb.Position.X, targetOrb.Position.Y, targetOrb.Position.Z);
        packet.transform.position = orbPos;
        
        // Set color based on frequency
        var renderer = packet.GetComponent<Renderer>();
        if (renderer != null)
        {
            Color color = GetWavePacketColor(args.Signature.Frequency);
            renderer.material.color = color;
            renderer.material.SetColor("_EmissionColor", color * 2f);
        }
        
        // Add glow based on resonance
        var light = packet.GetComponent<Light>();
        if (light != null)
        {
            light.color = GetWavePacketColor(args.Signature.Frequency);
            // Fixed: Use Resonance instead of Amplitude
            light.intensity = 1f + args.Signature.Resonance;
        }
        
        // Configure trail
        var trail = packet.GetComponent<TrailRenderer>();
        if (trail != null)
        {
            Color trailColor = GetWavePacketColor(args.Signature.Frequency);
            trail.startColor = trailColor;
            // Fixed: Use FluxPattern instead of Coherence
            trail.endColor = trailColor * (args.Signature.FluxPattern / 65535f);
        }
        
        // Store packet
        inFlightPackets[args.WavePacketId] = packet;
        
        // Calculate travel time
        float distance = Vector3.Distance(playerTransform.position, orbPos);
        float travelTime = distance / wavePacketSpeed;
        
        // Start animation
        var coroutine = StartCoroutine(AnimateWavePacket(packet, args.WavePacketId, travelTime));
        packetCoroutines[args.WavePacketId] = coroutine;
    }
    
    private IEnumerator AnimateWavePacket(GameObject packet, ulong packetId, float duration)
    {
        Vector3 start = packet.transform.position;
        Vector3 targetOffset = Vector3.up * 0.5f; // Offset to player center
        float elapsed = 0;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Smooth step interpolation
            float smoothT = Mathf.SmoothStep(0, 1, t);
            
            // Update position with arc
            Vector3 current = Vector3.Lerp(start, playerTransform.position + targetOffset, smoothT);
            current.y += Mathf.Sin(smoothT * Mathf.PI) * 2f; // Arc height
            packet.transform.position = current;
            
            // Pulse effect
            float scale = 1f + Mathf.Sin(elapsed * 10f) * 0.1f;
            packet.transform.localScale = Vector3.one * scale;
            
            yield return null;
        }
        
        // Notify server of capture
        conn.Reducers.CaptureWavePacket(packetId);
    }
    
    private void CleanupPacket(ulong packetId)
    {
        if (inFlightPackets.TryGetValue(packetId, out var packet))
        {
            Destroy(packet);
            inFlightPackets.Remove(packetId);
        }
        
        if (packetCoroutines.TryGetValue(packetId, out var coroutine))
        {
            if (coroutine != null) StopCoroutine(coroutine);
            packetCoroutines.Remove(packetId);
        }
    }
    
    #endregion
    
    #region Helper Methods
    
    private Color GetWavePacketColor(float frequency)
    {
        // Map frequency (0-1) to color wheel
        // 0 = Red, 1/6 = Yellow, 1/3 = Green, 1/2 = Cyan, 2/3 = Blue, 5/6 = Magenta
        return Color.HSVToRGB(frequency, 0.8f, 1f);
    }
    
    private Color GetCrystalColor(CrystalType crystal)
    {
        float frequency = crystal switch
        {
            CrystalType.Red => 0f,
            CrystalType.Green => 1f/3f,
            CrystalType.Blue => 2f/3f,
            _ => 0f
        };
        
        return GetWavePacketColor(frequency);
    }
    
    public bool IsMining => isMining;
    public WavePacketOrb CurrentOrb => targetOrb;
    public float MiningRange => maxRange;
    
    #endregion
    
    #region Debug
    
    [ContextMenu("Debug - List Nearby Orbs")]
    private void DebugListNearbyOrbs()
    {
        var player = GetLocalPlayer();
        if (player == null)
        {
            Debug.Log("No local player");
            return;
        }
        
        var playerPos = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);
        
        Debug.Log("=== Nearby Wave Packet Orbs ===");
        foreach (var orb in conn.Db.WavePacketOrb.Iter())
        {
            if (orb.WorldCoords.X != player.CurrentWorld.X ||
                orb.WorldCoords.Y != player.CurrentWorld.Y ||
                orb.WorldCoords.Z != player.CurrentWorld.Z)
                continue;
            
            var orbPos = new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z);
            float distance = Vector3.Distance(playerPos, orbPos);
            
            if (distance <= maxRange)
            {
                Debug.Log($"Orb {orb.OrbId}: {distance:F1}m away, {orb.TotalWavePackets} packets");
                
                // List composition
                foreach (var sample in orb.WavePacketComposition)
                {
                    string colorName = GetColorName(sample.Signature.Frequency);
                    Debug.Log($"  - {colorName}: {sample.Amount} packets");
                }
            }
        }
    }
    
    private string GetColorName(float frequency)
    {
        float rad = frequency * 2f * Mathf.PI;
        
        if (rad < Mathf.PI / 6f || rad > 11f * Mathf.PI / 6f) return "Red";
        if (rad < Mathf.PI / 2f) return "Yellow";
        if (rad < 5f * Mathf.PI / 6f) return "Green";
        if (rad < 7f * Mathf.PI / 6f) return "Cyan";
        if (rad < 3f * Mathf.PI / 2f) return "Blue";
        return "Magenta";
    }
    
    #endregion
}