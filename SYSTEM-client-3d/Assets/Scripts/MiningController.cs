/*
 * MiningController.cs - Component-Based Mining System for Wave Packets
 * 
 * NOTE: You may need to resolve these potential compile errors:
 * 1. Missing GameManager singleton - ensure GameManager.Instance is set up
 * 2. Missing DbConnection on GameManager - ensure GameManager.Conn property exists
 * 3. Missing WavePacketSignature type - check if it's in SpacetimeDB.Types namespace
 * 4. TMPro namespace - install TextMeshPro package if not already installed
 * 5. Create prefabs for: wavePacketParticlePrefab, captureEffectPrefab
 * 6. The server might need EmitWavePacketOrb events exposed for full functionality
 */

using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;
using TMPro;

// Main mining controller that coordinates all mining operations
public class MiningController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private CrystalInventory crystalInventory;
    [SerializeField] private OrbTargeting orbTargeting;
    [SerializeField] private WavePacketTransport wavePacketTransport;
    [SerializeField] private MiningUI miningUI;
    
    [Header("Mining Settings")]
    [SerializeField] private float miningToggleCooldown = 0.5f;
    
    // Mining state
    private bool isMining;
    private WavePacketOrb currentTarget;
    private float lastToggleTime;
    private Queue<uint> pendingCaptures = new();
    
    // Events
    public event System.Action<bool> OnMiningStateChanged;
    public event System.Action<WavePacketOrb> OnTargetChanged;
    
    private Player localPlayer;
    private DbConnection Conn => GameManager.Instance?.Conn;
    
    void Start()
    {
        // Subscribe to connection events
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnConnected += OnConnected;
        }
    }
    
    void OnConnected(DbConnection conn, Identity identity)
    {
        // Subscribe to reducer events for capture confirmation
        conn.Reducers.OnCaptureWavePacket += OnWavePacketCaptured;
        
        // Note: EmitWavePacketOrb events would need to be subscribed to if they exist
        // For now, we'll need to poll for changes or wait for server to expose these events
    }
    
    void Update()
    {
        // Update local player reference
        if (localPlayer == null && GameManager.LocalIdentity != null)
        {
            localPlayer = Conn?.Db.Player
                .Where(p => p.Identity == GameManager.LocalIdentity)
                .FirstOrDefault();
        }
        
        // Update mining state
        if (isMining && currentTarget != null)
        {
            // Validate mining conditions
            if (!ValidateMiningConditions())
            {
                StopMining();
            }
        }
    }
    
    public void ToggleMining()
    {
        if (Time.time - lastToggleTime < miningToggleCooldown)
            return;
            
        lastToggleTime = Time.time;
        
        if (isMining)
        {
            StopMining();
        }
        else
        {
            StartMining();
        }
    }
    
    private void StartMining()
    {
        if (!CanStartMining())
            return;
            
        // Find best target
        var crystal = crystalInventory.GetActiveCrystal();
        if (crystal == null)
        {
            Debug.LogWarning("No active crystal to mine with");
            return;
        }
        
        currentTarget = orbTargeting.FindBestTarget(
            crystal.CrystalType, 
            transform.position
        );
        
        if (currentTarget == null)
        {
            Debug.Log("No valid targets in range");
            return;
        }
        
        // Call server reducer to start mining
        Conn?.Reducers.StartMining(currentTarget.OrbId);
        
        isMining = true;
        OnMiningStateChanged?.Invoke(true);
        OnTargetChanged?.Invoke(currentTarget);
        
        // Update UI
        miningUI?.SetMiningActive(true);
    }
    
    private void StopMining()
    {
        if (!isMining)
            return;
            
        // Call server reducer to stop mining
        Conn?.Reducers.StopMining();
        
        isMining = false;
        currentTarget = null;
        pendingCaptures.Clear();
        
        OnMiningStateChanged?.Invoke(false);
        OnTargetChanged?.Invoke(null);
        
        // Update UI
        miningUI?.SetMiningActive(false);
        
        // Tell wave packet transport to evaporate all in-flight particles
        wavePacketTransport?.EvaporateAllWavePackets();
    }
    
    private bool CanStartMining()
    {
        if (localPlayer == null)
            return false;
            
        if (!crystalInventory.HasActiveCrystal())
            return false;
            
        if (!GameManager.IsConnected())
            return false;
            
        return true;
    }
    
    private bool ValidateMiningConditions()
    {
        if (localPlayer == null || currentTarget == null)
            return false;
            
        // Check if orb still exists
        var orbStillExists = Conn?.Db.WavePacketOrb
            .OrbId.Find(currentTarget.OrbId) != null;
            
        if (!orbStillExists)
            return false;
            
        // Check range
        float distance = Vector3.Distance(
            transform.position,
            new Vector3(currentTarget.Position.X, currentTarget.Position.Y, currentTarget.Position.Z)
        );
        
        if (distance > orbTargeting.maxRange)
            return false;
            
        return true;
    }
    
    // Server event handlers
    // Note: You'll need to implement polling or state tracking for wave packet emissions
    // since EmitWavePacketOrb might not be exposed as a client-side event
    private void TrackWavePacketEmissions()
    {
        // This would need to be called periodically to detect new wave packet emissions
        // You might track wave packet storage changes or implement a custom event system
    }
    
    private void OnWavePacketCaptured(ReducerEventContext ctx, ulong wavePacketId)
    {
        // Remove from pending if it matches
        if (pendingCaptures.Count > 0 && pendingCaptures.Peek() == wavePacketId)
        {
            pendingCaptures.Dequeue();
        }
        
        // Update UI to show successful capture
        miningUI?.ShowCaptureEffect();
    }
}

// Component for managing player's crystals
public class CrystalInventory : MonoBehaviour
{
    private PlayerCrystal activeCrystal;
    
    void Start()
    {
        // Subscribe to crystal table events
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnConnected += OnConnected;
        }
    }
    
    void OnConnected(DbConnection conn, Identity identity)
    {
        conn.Db.PlayerCrystal.OnInsert += OnCrystalInsert;
        conn.Db.PlayerCrystal.OnUpdate += OnCrystalUpdate;
        conn.Db.PlayerCrystal.OnDelete += OnCrystalDelete;
    }
    
    public bool HasActiveCrystal()
    {
        return activeCrystal != null;
    }
    
    public PlayerCrystal GetActiveCrystal()
    {
        return activeCrystal;
    }
    
    private void OnCrystalInsert(EventContext ctx, PlayerCrystal crystal)
    {
        var localPlayer = GameManager.Conn?.Db.Player
            .Where(p => p.Identity == GameManager.LocalIdentity)
            .FirstOrDefault();
            
        if (localPlayer != null && crystal.PlayerId == localPlayer.PlayerId)
        {
            activeCrystal = crystal;
        }
    }
    
    private void OnCrystalUpdate(EventContext ctx, PlayerCrystal oldCrystal, PlayerCrystal newCrystal)
    {
        if (activeCrystal != null && activeCrystal.CrystalId == newCrystal.CrystalId)
        {
            activeCrystal = newCrystal;
        }
    }
    
    private void OnCrystalDelete(EventContext ctx, PlayerCrystal crystal)
    {
        if (activeCrystal != null && activeCrystal.CrystalId == crystal.CrystalId)
        {
            activeCrystal = null;
        }
    }
}

// Component for finding and validating mining targets
public class OrbTargeting : MonoBehaviour
{
    [Header("Targeting Settings")]
    public float maxRange = 30f;
    [SerializeField] private LayerMask orbLayer;
    
    public WavePacketOrb FindBestTarget(CrystalType crystalType, Vector3 playerPosition)
    {
        if (!GameManager.IsConnected())
            return null;
            
        // Get all orbs from the database
        var allOrbs = GameManager.Conn.Db.WavePacketOrb.Iter().ToList();
        
        // Filter and sort by distance
        var validOrbs = allOrbs
            .Where(orb => IsValidTarget(orb, crystalType, playerPosition))
            .OrderBy(orb => Vector3.Distance(
                playerPosition,
                new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z)
            ))
            .ToList();
            
        return validOrbs.FirstOrDefault();
    }
    
    private bool IsValidTarget(WavePacketOrb orb, CrystalType crystalType, Vector3 playerPosition)
    {
        // Check distance
        var orbPosition = new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z);
        float distance = Vector3.Distance(playerPosition, orbPosition);
        
        if (distance > maxRange)
            return false;
            
        // Check if orb has wave packets of the right frequency
        bool hasMatchingWavePackets = orb.WavePacketComposition.Any(sample => 
            DoesFrequencyMatchCrystal(sample.Frequency, crystalType)
        );
        
        return hasMatchingWavePackets;
    }
    
    private bool DoesFrequencyMatchCrystal(FrequencyBand frequency, CrystalType crystal)
    {
        // Map crystal types to frequency bands based on the game design
        return crystal switch
        {
            CrystalType.Red => frequency == FrequencyBand.Red,
            CrystalType.Green => frequency == FrequencyBand.Green,
            CrystalType.Blue => frequency == FrequencyBand.Blue,
            _ => false
        };
    }
}

// Component for visualizing wave packet transport
public class WavePacketTransport : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private GameObject wavePacketParticlePrefab;
    [SerializeField] private int particlePoolSize = 20;
    [SerializeField] private float particleSpeed = 5f;
    [SerializeField] private AnimationCurve particleSizeCurve;
    
    private ObjectPool<WavePacketParticle> particlePool;
    private Dictionary<uint, WavePacketParticle> activeParticles = new();
    
    void Awake()
    {
        // Initialize object pool
        particlePool = new ObjectPool<WavePacketParticle>(
            () => CreateWavePacketParticle(),
            particle => particle.gameObject.SetActive(true),
            particle => particle.gameObject.SetActive(false),
            particle => Destroy(particle.gameObject),
            maxSize: particlePoolSize
        );
    }
    
    public void CreateWavePacketStream(uint wavePacketId, Vector3 from, Vector3 to, FrequencyBand frequency)
    {
        var particle = particlePool.Get();
        particle.Initialize(wavePacketId, from, to, frequency, particleSpeed);
        activeParticles[wavePacketId] = particle;
        
        // Set color based on frequency
        var renderer = particle.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = GetColorForFrequency(frequency);
        }
    }
    
    public void EvaporateWavePacket(uint wavePacketId)
    {
        if (activeParticles.TryGetValue(wavePacketId, out var particle))
        {
            particle.Evaporate();
            activeParticles.Remove(wavePacketId);
        }
    }
    
    public void EvaporateAllWavePackets()
    {
        foreach (var particle in activeParticles.Values)
        {
            particle.Evaporate();
        }
        activeParticles.Clear();
    }
    
    private WavePacketParticle CreateWavePacketParticle()
    {
        var obj = Instantiate(wavePacketParticlePrefab);
        return obj.AddComponent<WavePacketParticle>();
    }
    
    private Color GetColorForFrequency(FrequencyBand frequency)
    {
        // Map frequency bands to colors
        return frequency switch
        {
            FrequencyBand.Red => Color.red,
            FrequencyBand.Orange => new Color(1f, 0.5f, 0f),
            FrequencyBand.Yellow => Color.yellow,
            FrequencyBand.Green => Color.green,
            FrequencyBand.Blue => Color.blue,
            FrequencyBand.Violet => new Color(0.5f, 0f, 1f),
            _ => Color.white
        };
    }
}

// Simple UI controller for mining interface
public class MiningUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private UnityEngine.UI.Button miningToggleButton;
    [SerializeField] private TMPro.TextMeshProUGUI miningStatusText;
    [SerializeField] private GameObject captureEffectPrefab;
    
    public void SetMiningActive(bool active)
    {
        if (miningToggleButton != null)
        {
            var buttonText = miningToggleButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = active ? "Stop Mining" : "Start Mining";
            }
        }
        
        if (miningStatusText != null)
        {
            miningStatusText.text = active ? "Mining Active" : "Mining Inactive";
        }
    }
    
    public void ShowCaptureEffect()
    {
        if (captureEffectPrefab != null)
        {
            var effect = Instantiate(captureEffectPrefab, transform);
            Destroy(effect, 2f);
        }
    }
}

// Helper class for wave packet particles
public class WavePacketParticle : MonoBehaviour
{
    private uint wavePacketId;
    private Vector3 startPos;
    private Vector3 targetPos;
    private float speed;
    private float startTime;
    private bool isEvaporating;
    
    public void Initialize(uint id, Vector3 from, Vector3 to, FrequencyBand frequency, float moveSpeed)
    {
        wavePacketId = id;
        startPos = from;
        targetPos = to;
        speed = moveSpeed;
        startTime = Time.time;
        isEvaporating = false;
        transform.position = from;
    }
    
    void Update()
    {
        if (isEvaporating)
        {
            // Fade out and shrink
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, Time.deltaTime * 3f);
            if (transform.localScale.magnitude < 0.01f)
            {
                gameObject.SetActive(false);
            }
        }
        else
        {
            // Move towards target
            float journey = (Time.time - startTime) * speed;
            float distance = Vector3.Distance(startPos, targetPos);
            float fractionOfJourney = journey / distance;
            
            transform.position = Vector3.Lerp(startPos, targetPos, fractionOfJourney);
            
            // Check if reached target
            if (fractionOfJourney >= 1f)
            {
                // Notify capture and return to pool
                gameObject.SetActive(false);
            }
        }
    }
    
    public void Evaporate()
    {
        isEvaporating = true;
    }
}