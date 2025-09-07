using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine.Pool;
using SYSTEM.Game;

public class WavePacketMiningSystem : MonoBehaviour
{
    [Header("Mining Configuration")]
    [SerializeField] private float extractionTime = 2f;

    [SerializeField] private float maxMiningRange = 30f;
    [SerializeField] private float packetSpeed = 5f;
    [SerializeField] private float reachDistance = 1f;
    
    [Header("Visual Settings")]
    [SerializeField] private GameObject wavePacketPrefab;
    [SerializeField] private GameObject particleEffectPrefab;
    [SerializeField] private int particlePoolSize = 20;
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    
    // Core references
    private DbConnection conn;
    private PlayerController playerController;
    private Transform playerTransform;
    
    // Mining state
    private bool isMining = false;
    private WavePacketOrb currentTarget;
    private ulong currentOrbId;
    private float miningTimer;
    private float extractionTimer;
    
    // Active packets tracking
    private Dictionary<ulong, GameObject> activePackets = new Dictionary<ulong, GameObject>();
    private Dictionary<ulong, Coroutine> packetMovementCoroutines = new Dictionary<ulong, Coroutine>();
    
    // Particle effects pool
    private IObjectPool<GameObject> particlePool;
    
    // Events
    public event Action<bool> OnMiningStateChanged;
    
    public event Action<WavePacketSignature> OnWavePacketExtracted;
    
    #region Unity Lifecycle
    
    void Awake()
    {
        // Get connection reference
        conn = GameManager.Conn;
        if (conn == null)
        {
            Debug.LogError("WavePacketMiningSystem: No database connection available!");
            enabled = false;
            return;
        }
        
        // Set up particle pool
        particlePool = new ObjectPool<GameObject>(
            CreatePooledParticle,
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
        
        StopAllCoroutines();
    }
    
    void Update()
    {
        if (isMining && currentTarget != null)
        {
            // Update mining timers
            miningTimer += Time.deltaTime;
            extractionTimer += Time.deltaTime;
            
            // Check if it's time to extract
            if (extractionTimer >= extractionTime)
            {
                RequestExtraction();
                extractionTimer = 0f;
            }
            
            // Check if target is still valid
            if (!IsOrbInRange(currentTarget))
            {
                StopMining();
            }
        }
    }
    
    #endregion
    
    #region Mining Controls
    
    public void StartMining(WavePacketOrb orb)
    {
        if (isMining || orb == null) return;
        
        // Check range
        if (!IsOrbInRange(orb))
        {
            // Debug.Log("Orb is out of range");
            return;
        }
        
        // Get player's crystal type
        var localPlayer = GameManager.GetLocalPlayer();
        if (localPlayer == null)
        {
            Debug.LogError("Cannot start mining - no local player");
            return;
        }
        
        // Find player's crystal
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
            Debug.LogError("Cannot start mining - player has no crystal");
            return;
        }
        
        // Send start mining request to server with crystal type
        currentOrbId = orb.OrbId;
        conn.Reducers.StartMining(currentOrbId, playerCrystal.CrystalType);
    }
    
    public void StopMining()
    {
        if (!isMining) return;
        
        // Send stop mining request to server
        conn.Reducers.StopMining();
    }
    
    public void ToggleMining(WavePacketOrb orb = null)
    {
        if (isMining)
        {
            StopMining();
        }
        else if (orb != null)
        {
            StartMining(orb);
        }
    }
    
    #endregion
    
    #region Server Event Handlers
        
    private void HandleStartMiningResult(ReducerEventContext ctx, ulong orbId, CrystalType crystalType)
    {
        // Debug.Log($"[Mining] StartMining reducer response for orb {orbId} with crystal {crystalType}");
        
        if (ctx.Event.Status is Status.Committed)
        {
            // Debug.Log($"[Mining] Successfully started mining orb {orbId}");
        }
        else if (ctx.Event.Status is Status.Failed(var reason))
        {
            Debug.LogError($"[Mining] Failed to start mining: {reason}");
            
            // Handle specific error cases
            if (reason.Contains("already mining"))
            {
                Debug.LogWarning("[Mining] Already mining another orb");
            }
            else if (reason.Contains("no crystal"))
            {
                Debug.LogError("[Mining] No crystal equipped!");
            }
            
            // Reset mining state on failure
            StopMining();
        }
    }
    
    private void HandleStopMiningResult(ReducerEventContext ctx)
    {
        // Debug.Log("Stop mining response");
        
        isMining = false;
        currentTarget = null;
        currentOrbId = 0;
        miningTimer = 0f;
        extractionTimer = 0f;
        
        OnMiningStateChanged?.Invoke(false);
    }
    
    private void HandleExtractWavePacketResult(ReducerEventContext ctx)
    {
        // Debug.Log("Extract wave packet response");
        // The actual extraction is handled by table insert event
    }
    
    private void HandleWavePacketExtracted(EventContext ctx, WavePacketExtraction extraction)
    {
        var localPlayer = GameManager.GetLocalPlayer();
        if (localPlayer != null && extraction.PlayerId == localPlayer.PlayerId)
        {
            // Debug.Log($"Extracted packet {extraction.WavePacketId} with signature {extraction.Signature}");
            OnWavePacketExtracted?.Invoke(extraction.Signature);
            
            // Create visual packet
            CreateVisualPacket(extraction);
        }
    }
    
    private void HandleWavePacketExtractionRemoved(EventContext ctx, WavePacketExtraction extraction)
    {
        // Clean up visual if it exists
        if (activePackets.TryGetValue(extraction.WavePacketId, out GameObject packet))
        {
            if (packetMovementCoroutines.TryGetValue(extraction.WavePacketId, out Coroutine coroutine))
            {
                StopCoroutine(coroutine);
                packetMovementCoroutines.Remove(extraction.WavePacketId);
            }
            
            Destroy(packet);
            activePackets.Remove(extraction.WavePacketId);
        }
    }
    
    private void HandleWavePacketCaptured(ReducerEventContext ctx, ulong packetId)
    {
        // Debug.Log($"Wave packet {packetId} captured!");
        
        if (ctx.Event.Status is Status.Failed(var reason))
        {
            Debug.LogError($"Failed to capture packet: {reason}");
            return;
        }
        
        // The capture was successful
        // OnWavePacketCaptured event will be fired when we see the storage update
    }
    
    #endregion
    
    #region Visual Effects
    
    private void CreateVisualPacket(WavePacketExtraction extraction)
    {
        if (wavePacketPrefab == null || currentTarget == null) return;
        
        // Get orb position
        var orbObj = GameObject.Find($"WavePacketOrb_{currentOrbId}");
        if (orbObj == null) return;
        
        // Create packet visual
        var packet = Instantiate(wavePacketPrefab, orbObj.transform.position, Quaternion.identity);
        packet.name = $"WavePacket_{extraction.WavePacketId}";
        
        // Configure visual based on signature
        ConfigurePacketVisual(packet, extraction.Signature);
        
        // Track it
        activePackets[extraction.WavePacketId] = packet;
        
        // Start movement coroutine
        var coroutine = StartCoroutine(MovePacketToPlayer(extraction.WavePacketId, packet));
        packetMovementCoroutines[extraction.WavePacketId] = coroutine;
    }
    
    private void ConfigurePacketVisual(GameObject packet, WavePacketSignature signature)
    {
        // Get color based on signature
        Color color = SignatureToColor(signature);
        
        // Apply to renderer
        var renderer = packet.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
            renderer.material.SetColor("_EmissionColor", color * 2f);
        }
        
        // Apply to particle system if present
        var particleSystem = packet.GetComponentInChildren<ParticleSystem>();
        if (particleSystem != null)
        {
            var main = particleSystem.main;
            main.startColor = color;
        }
    }
    
    private Color SignatureToColor(WavePacketSignature signature)
    {
        // Map frequency to color
        float hue = signature.Frequency / (2f * Mathf.PI);
        return Color.HSVToRGB(hue, 0.8f, 1f);
    }
    
    private IEnumerator MovePacketToPlayer(ulong packetId, GameObject packet)
    {
        if (playerTransform == null) yield break;
        
        Vector3 startPos = packet.transform.position;
        float distance = Vector3.Distance(startPos, playerTransform.position);
        float duration = distance / packetSpeed;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            if (packet == null || playerTransform == null) yield break;
            
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float curvedT = movementCurve.Evaluate(t);
            
            // Update position
            Vector3 targetPos = playerTransform.position + Vector3.up * 0.5f;
            packet.transform.position = Vector3.Lerp(startPos, targetPos, curvedT);
            
            // Check if close enough to capture
            if (Vector3.Distance(packet.transform.position, targetPos) < reachDistance)
            {
                // Request capture
                conn.Reducers.CaptureWavePacket(packetId);
                
                // Spawn particle effect
                SpawnCaptureEffect(packet.transform.position);
                
                // Remove from tracking
                packetMovementCoroutines.Remove(packetId);
                activePackets.Remove(packetId);
                
                // Destroy visual
                Destroy(packet);
                yield break;
            }
            
            yield return null;
        }
    }
    
    private void SpawnCaptureEffect(Vector3 position)
    {
        if (particleEffectPrefab == null) return;
        
        var effect = particlePool.Get();
        effect.transform.position = position;
        
        // Return to pool after 2 seconds
        StartCoroutine(ReturnEffectToPool(effect, 2f));
    }
    
    private IEnumerator ReturnEffectToPool(GameObject effect, float delay)
    {
        yield return new WaitForSeconds(delay);
        particlePool.Release(effect);
    }
    
    #endregion
    
    #region Utility Methods
    
    private void RequestExtraction()
    {
        if (!isMining || currentOrbId == 0) return;
        
        // Debug.Log("Requesting wave packet extraction...");
        conn.Reducers.ExtractWavePacket();
    }
    
    private bool IsOrbInRange(WavePacketOrb orb)
    {
        if (orb == null || playerTransform == null) return false;
        
        // Find orb GameObject
        var orbObj = GameObject.Find($"WavePacketOrb_{orb.OrbId}");
        if (orbObj == null) return false;
        
        float distance = Vector3.Distance(playerTransform.position, orbObj.transform.position);
        return distance <= maxMiningRange;
    }
    
    public bool CanMineOrb(WavePacketOrb orb)
    {
        if (orb == null) return false;
        
        // Check if we have a compatible crystal
        var localPlayer = GameManager.GetLocalPlayer();
        if (localPlayer == null) return false;
        
        foreach (var crystal in conn.Db.PlayerCrystal.Iter())
        {
            if (crystal.PlayerId == localPlayer.PlayerId)
            {
                // Check if crystal frequency matches any packet in the orb
                // This would require checking the orb's available packets
                return true; // Simplified for now
            }
        }
        
        return false;
    }
    
    #endregion
    
    #region Pool Management
    
    private GameObject CreatePooledParticle()
    {
        if (particleEffectPrefab == null)
        {
            return new GameObject("Particle Effect");
        }
        return Instantiate(particleEffectPrefab);
    }
    
    private void OnGetFromPool(GameObject obj)
    {
        obj.SetActive(true);
    }
    
    private void OnReturnToPool(GameObject obj)
    {
        obj.SetActive(false);
    }
    
    private void OnDestroyPoolObject(GameObject obj)
    {
        Destroy(obj);
    }
    
    #endregion
    
    #region Debug
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 200, 300, 200));
        GUILayout.Label($"Mining: {isMining}");
        if (currentTarget != null)
        {
            GUILayout.Label($"Target Orb: {currentOrbId}");
            GUILayout.Label($"Mining Time: {miningTimer:F1}s");
            GUILayout.Label($"Next Extraction: {extractionTime - extractionTimer:F1}s");
        }
        GUILayout.Label($"Active Packets: {activePackets.Count}");
        GUILayout.EndArea();
    }
    
    #endregion
}