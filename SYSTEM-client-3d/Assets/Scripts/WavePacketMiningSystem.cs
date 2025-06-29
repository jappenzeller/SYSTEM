using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;

public class WavePacketMiningSystem : MonoBehaviour
{
    [Header("Wave Packet Orb Prefabs")]
    public GameObject wavePacketOrbPrefab;
    public GameObject wavePacketParticlePrefab;
    
    [Header("Mining Visuals")]
    public GameObject tractorBeamPrefab;
    public GameObject miningRangeIndicatorPrefab;
    
    [Header("UI References")]
    public GameObject crystalSelectionUI;
    public Button redCrystalButton;
    public Button greenCrystalButton;
    public Button blueCrystalButton;
    
    public GameObject miningUI;
    public Button miningToggleButton;
    public Text miningStatusText;
    public Text targetOrbInfoText;
    
    public GameObject inventoryUI;
    public Text redPacketsText;
    public Text greenPacketsText;
    public Text bluePacketsText;
    public Text yellowPacketsText;
    public Text cyanPacketsText;
    public Text magentaPacketsText;
    
    [Header("Mining Configuration")]
    public float miningRange = 30f;
    public float wavePacketSpeed = 5f;
    
    // Active game objects
    private Dictionary<ulong, GameObject> activeOrbs = new Dictionary<ulong, GameObject>();
    private Dictionary<ulong, WavePacketFlight> flyingPackets = new Dictionary<ulong, WavePacketFlight>();
    private GameObject currentTractorBeam;
    private GameObject rangeIndicator;
    
    // Current state
    private bool isMining = false;
    private ulong? targetOrbId = null;
    private PlayerCrystal localPlayerCrystal;
    
    // References
    private Player localPlayer;
    private Transform playerTransform;
    
    private class WavePacketFlight
    {
        public GameObject visual;
        public ulong packetId;
        public Vector3 startPos;
        public Vector3 targetPlayer;
        public float startTime;
        public float duration;
        public WavePacketSignature signature;
    }
    
    void Start()
    {
        // Set up UI buttons
        if (redCrystalButton) redCrystalButton.onClick.AddListener(() => SelectCrystal(CrystalType.Red));
        if (greenCrystalButton) greenCrystalButton.onClick.AddListener(() => SelectCrystal(CrystalType.Green));
        if (blueCrystalButton) blueCrystalButton.onClick.AddListener(() => SelectCrystal(CrystalType.Blue));
        if (miningToggleButton) miningToggleButton.onClick.AddListener(ToggleMining);
        
        // Subscribe to database events
        if (GameManager.IsConnected())
        {
            OnGameManagerConnected();
        }
        
        GameManager.OnConnected += OnGameManagerConnected;
        GameManager.OnDisconnected += OnGameManagerDisconnected;
        
        // Get player transform
        var playerController = FindObjectOfType<PlayerController>();
        if (playerController) playerTransform = playerController.transform;
        
        // Hide UIs initially
        if (crystalSelectionUI) crystalSelectionUI.SetActive(false);
        if (miningUI) miningUI.SetActive(false);
    }
    
    void OnDestroy()
    {
        GameManager.OnConnected -= OnGameManagerConnected;
        GameManager.OnDisconnected -= OnGameManagerDisconnected;
        
        UnsubscribeFromEvents();
    }
    
    void OnGameManagerConnected()
    {
        // Subscribe to wave packet tables
        GameManager.Conn.Db.WavePacketOrb.OnInsert += OnWavePacketOrbInsert;
        GameManager.Conn.Db.WavePacketOrb.OnUpdate += OnWavePacketOrbUpdate;
        GameManager.Conn.Db.WavePacketOrb.OnDelete += OnWavePacketOrbDelete;
        
        GameManager.Conn.Db.PlayerCrystal.OnInsert += OnPlayerCrystalInsert;
        GameManager.Conn.Db.PlayerCrystal.OnUpdate += OnPlayerCrystalUpdate;
        GameManager.Conn.Db.PlayerCrystal.OnDelete += OnPlayerCrystalDelete;
        
        GameManager.Conn.Db.WavePacketStorage.OnInsert += OnWavePacketStorageUpdate;
        GameManager.Conn.Db.WavePacketStorage.OnUpdate += OnWavePacketStorageChanged;
        GameManager.Conn.Db.WavePacketStorage.OnDelete += OnWavePacketStorageDelete;
        
        // Check if player needs to choose crystal
        CheckPlayerCrystalStatus();
    }
    
    void OnGameManagerDisconnected()
    {
        UnsubscribeFromEvents();
        ClearAllVisuals();
    }
    
    void UnsubscribeFromEvents()
    {
        if (GameManager.Conn?.Db != null)
        {
            GameManager.Conn.Db.WavePacketOrb.OnInsert -= OnWavePacketOrbInsert;
            GameManager.Conn.Db.WavePacketOrb.OnUpdate -= OnWavePacketOrbUpdate;
            GameManager.Conn.Db.WavePacketOrb.OnDelete -= OnWavePacketOrbDelete;
            
            GameManager.Conn.Db.PlayerCrystal.OnInsert -= OnPlayerCrystalInsert;
            GameManager.Conn.Db.PlayerCrystal.OnUpdate -= OnPlayerCrystalUpdate;
            GameManager.Conn.Db.PlayerCrystal.OnDelete -= OnPlayerCrystalDelete;
            
            GameManager.Conn.Db.WavePacketStorage.OnInsert -= OnWavePacketStorageUpdate;
            GameManager.Conn.Db.WavePacketStorage.OnUpdate -= OnWavePacketStorageChanged;
            GameManager.Conn.Db.WavePacketStorage.OnDelete -= OnWavePacketStorageDelete;
        }
    }
    
    // ============================================================================
    // Crystal Selection
    // ============================================================================
    
    void CheckPlayerCrystalStatus()
    {
        localPlayer = GameManager.GetCurrentPlayer();
        if (localPlayer == null) return;
        
        var crystal = GameManager.Conn.Db.PlayerCrystal.FindByPlayerId(localPlayer.PlayerId);
        if (crystal == null)
        {
            // Show crystal selection UI
            if (crystalSelectionUI) crystalSelectionUI.SetActive(true);
        }
        else
        {
            localPlayerCrystal = crystal;
            if (crystalSelectionUI) crystalSelectionUI.SetActive(false);
            if (miningUI) miningUI.SetActive(true);
            UpdateMiningUI();
        }
    }
    
    void SelectCrystal(CrystalType crystalType)
    {
        if (!GameManager.IsConnected() || localPlayer == null) return;
        
        GameManager.Conn.Reducers.ChooseStartingCrystal(crystalType);
        
        // UI will update when we receive the insert event
    }
    
    void OnPlayerCrystalInsert(EventContext ctx, PlayerCrystal crystal)
    {
        if (localPlayer != null && crystal.PlayerId == localPlayer.PlayerId)
        {
            localPlayerCrystal = crystal;
            if (crystalSelectionUI) crystalSelectionUI.SetActive(false);
            if (miningUI) miningUI.SetActive(true);
            UpdateMiningUI();
        }
    }
    
    void OnPlayerCrystalUpdate(EventContext ctx, PlayerCrystal oldCrystal, PlayerCrystal newCrystal)
    {
        if (localPlayer != null && newCrystal.PlayerId == localPlayer.PlayerId)
        {
            localPlayerCrystal = newCrystal;
            UpdateMiningUI();
        }
    }
    
    void OnPlayerCrystalDelete(EventContext ctx, PlayerCrystal crystal)
    {
        if (localPlayer != null && crystal.PlayerId == localPlayer.PlayerId)
        {
            localPlayerCrystal = null;
            if (miningUI) miningUI.SetActive(false);
            if (crystalSelectionUI) crystalSelectionUI.SetActive(true);
        }
    }
    
    // ============================================================================
    // Wave Packet Orb Management
    // ============================================================================
    
    void OnWavePacketOrbInsert(EventContext ctx, WavePacketOrb orb)
    {
        CreateOrbVisual(orb);
    }
    
    void OnWavePacketOrbUpdate(EventContext ctx, WavePacketOrb oldOrb, WavePacketOrb newOrb)
    {
        UpdateOrbVisual(newOrb);
    }
    
    void OnWavePacketOrbDelete(EventContext ctx, WavePacketOrb orb)
    {
        RemoveOrbVisual(orb.OrbId);
    }
    
    void CreateOrbVisual(WavePacketOrb orb)
    {
        if (wavePacketOrbPrefab == null) return;
        
        Vector3 position = new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z);
        GameObject orbObj = Instantiate(wavePacketOrbPrefab, position, Quaternion.identity);
        
        // Calculate orb color from composition
        Color orbColor = CalculateOrbColor(orb);
        SetOrbVisualColor(orbObj, orbColor, orb.TotalWavePackets);
        
        // Add physics
        var rb = orbObj.GetComponent<Rigidbody>();
        if (rb == null) rb = orbObj.AddComponent<Rigidbody>();
        rb.linearVelocity = new Vector3(orb.Velocity.X, orb.Velocity.Y, orb.Velocity.Z);
        rb.useGravity = true;
        
        // Add collector component
        var collector = orbObj.AddComponent<WavePacketOrbCollector>();
        collector.Initialize(orb.OrbId, orb.TotalWavePackets);
        
        // Store reference
        activeOrbs[orb.OrbId] = orbObj;
    }
    
    void UpdateOrbVisual(WavePacketOrb orb)
    {
        if (!activeOrbs.TryGetValue(orb.OrbId, out GameObject orbObj)) return;
        if (orbObj == null) return;
        
        // Update position
        orbObj.transform.position = new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z);
        
        // Update color based on new composition
        Color orbColor = CalculateOrbColor(orb);
        SetOrbVisualColor(orbObj, orbColor, orb.TotalWavePackets);
        
        // Update velocity
        var rb = orbObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = new Vector3(orb.Velocity.X, orb.Velocity.Y, orb.Velocity.Z);
        }
    }
    
    void RemoveOrbVisual(ulong orbId)
    {
        if (activeOrbs.TryGetValue(orbId, out GameObject orbObj))
        {
            if (orbObj != null) Destroy(orbObj);
            activeOrbs.Remove(orbId);
        }
        
        // Stop mining if this was our target
        if (targetOrbId == orbId)
        {
            StopMining();
        }
    }
    
    Color CalculateOrbColor(WavePacketOrb orb)
    {
        if (orb.WavePacketComposition == null || orb.WavePacketComposition.Count == 0)
            return Color.gray;
        
        float totalPackets = orb.TotalWavePackets;
        if (totalPackets == 0) return Color.black;
        
        // Calculate weighted average of radians
        float weightedX = 0f;
        float weightedY = 0f;
        
        foreach (var sample in orb.WavePacketComposition)
        {
            float weight = sample.Amount / totalPackets;
            float radians = sample.Signature.Frequency * 2f * Mathf.PI;
            weightedX += Mathf.Cos(radians) * weight;
            weightedY += Mathf.Sin(radians) * weight;
        }
        
        // Convert back to frequency
        float avgRadians = Mathf.Atan2(weightedY, weightedX);
        if (avgRadians < 0) avgRadians += 2f * Mathf.PI;
        float avgFrequency = avgRadians / (2f * Mathf.PI);
        
        // Map frequency to color
        return FrequencyToColor(avgFrequency);
    }
    
    Color FrequencyToColor(float frequency)
    {
        // HSV to RGB where frequency maps to hue
        return Color.HSVToRGB(frequency, 1f, 1f);
    }
    
    void SetOrbVisualColor(GameObject orbObj, Color color, uint intensity)
    {
        var renderer = orbObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
            if (renderer.material.HasProperty("_EmissionColor"))
            {
                float emissionStrength = Mathf.Lerp(0.1f, 1f, intensity / 100f);
                renderer.material.SetColor("_EmissionColor", color * emissionStrength);
            }
        }
        
        var light = orbObj.GetComponentInChildren<Light>();
        if (light != null)
        {
            light.color = color;
            light.intensity = Mathf.Lerp(0.5f, 2f, intensity / 100f);
        }
    }
    
    // ============================================================================
    // Mining System
    // ============================================================================
    
    void Update()
    {
        if (!GameManager.IsConnected() || localPlayer == null) return;
        
        UpdateMiningTargeting();
        UpdateFlyingPackets();
        UpdateRangeIndicator();
    }
    
    void UpdateMiningTargeting()
    {
        if (!isMining || localPlayerCrystal == null) return;
        
        // Find closest valid orb
        WavePacketOrb closestOrb = null;
        float closestDistance = float.MaxValue;
        
        foreach (var orb in GameManager.Conn.Db.WavePacketOrb.Iter())
        {
            // Check if in same world
            if (orb.WorldCoords.X != localPlayer.CurrentWorld.X ||
                orb.WorldCoords.Y != localPlayer.CurrentWorld.Y ||
                orb.WorldCoords.Z != localPlayer.CurrentWorld.Z)
                continue;
            
            // Check if has matching packets
            bool hasMatching = false;
            foreach (var sample in orb.WavePacketComposition)
            {
                if (sample.Amount > 0 && CrystalMatchesSignature(localPlayerCrystal.CrystalType, sample.Signature))
                {
                    hasMatching = true;
                    break;
                }
            }
            
            if (!hasMatching) continue;
            
            // Check distance
            Vector3 orbPos = new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z);
            float distance = Vector3.Distance(playerTransform.position, orbPos);
            
            if (distance <= miningRange && distance < closestDistance)
            {
                closestDistance = distance;
                closestOrb = orb;
            }
        }
        
        // Update target
        if (closestOrb != null)
        {
            if (targetOrbId != closestOrb.OrbId)
            {
                // New target
                if (targetOrbId.HasValue)
                {
                    GameManager.Conn.Reducers.StopMining();
                }
                
                targetOrbId = closestOrb.OrbId;
                GameManager.Conn.Reducers.StartMining(closestOrb.OrbId);
                UpdateTractorBeam(closestOrb.OrbId);
            }
        }
        else if (targetOrbId.HasValue)
        {
            // Lost target
            GameManager.Conn.Reducers.StopMining();
            targetOrbId = null;
            DisableTractorBeam();
        }
        
        UpdateTargetInfo();
    }
    
    bool CrystalMatchesSignature(CrystalType crystal, WavePacketSignature signature)
    {
        float crystalRadian = crystal switch
        {
            CrystalType.Red => 0f,
            CrystalType.Green => 2f * Mathf.PI / 3f,
            CrystalType.Blue => 4f * Mathf.PI / 3f,
            _ => 0f
        };
        
        float signatureRadian = signature.Frequency * 2f * Mathf.PI;
        float diff = Mathf.Abs(signatureRadian - crystalRadian);
        if (diff > Mathf.PI) diff = 2f * Mathf.PI - diff;
        
        return diff <= Mathf.PI / 6f; // ±30 degrees
    }
    
    void ToggleMining()
    {
        isMining = !isMining;
        
        if (!isMining && targetOrbId.HasValue)
        {
            GameManager.Conn.Reducers.StopMining();
            targetOrbId = null;
            DisableTractorBeam();
        }
        
        UpdateMiningUI();
    }
    
    void UpdateTractorBeam(ulong orbId)
    {
        if (!activeOrbs.TryGetValue(orbId, out GameObject orbObj)) return;
        
        if (currentTractorBeam == null && tractorBeamPrefab != null)
        {
            currentTractorBeam = Instantiate(tractorBeamPrefab);
        }
        
        if (currentTractorBeam != null)
        {
            var lineRenderer = currentTractorBeam.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                lineRenderer.SetPosition(0, playerTransform.position);
                lineRenderer.SetPosition(1, orbObj.transform.position);
            }
            currentTractorBeam.SetActive(true);
        }
    }
    
    void DisableTractorBeam()
    {
        if (currentTractorBeam != null)
        {
            currentTractorBeam.SetActive(false);
        }
    }
    
    void UpdateRangeIndicator()
    {
        if (isMining && rangeIndicator == null && miningRangeIndicatorPrefab != null)
        {
            rangeIndicator = Instantiate(miningRangeIndicatorPrefab);
        }
        
        if (rangeIndicator != null)
        {
            rangeIndicator.SetActive(isMining);
            if (isMining)
            {
                rangeIndicator.transform.position = playerTransform.position;
                rangeIndicator.transform.localScale = Vector3.one * (miningRange * 2f);
            }
        }
    }
    
    // ============================================================================
    // Wave Packet Flight Visualization
    // ============================================================================
    
    public void OnWavePacketExtracted(ulong packetId, WavePacketSignature signature, float flightTime)
    {
        if (wavePacketParticlePrefab == null || !targetOrbId.HasValue) return;
        if (!activeOrbs.TryGetValue(targetOrbId.Value, out GameObject orbObj)) return;
        
        // Create flying packet visual
        GameObject packetVisual = Instantiate(wavePacketParticlePrefab, orbObj.transform.position, Quaternion.identity);
        
        // Set color
        Color packetColor = FrequencyToColor(signature.Frequency);
        var renderer = packetVisual.GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = packetColor;
        
        var particle = packetVisual.GetComponent<ParticleSystem>();
        if (particle != null)
        {
            var main = particle.main;
            main.startColor = packetColor;
        }
        
        // Store flight info
        var flight = new WavePacketFlight
        {
            visual = packetVisual,
            packetId = packetId,
            startPos = orbObj.transform.position,
            targetPlayer = playerTransform.position,
            startTime = Time.time,
            duration = flightTime,
            signature = signature
        };
        
        flyingPackets[packetId] = flight;
    }
    
    void UpdateFlyingPackets()
    {
        var toRemove = new List<ulong>();
        
        foreach (var kvp in flyingPackets)
        {
            var flight = kvp.Value;
            float elapsed = Time.time - flight.startTime;
            float t = elapsed / flight.duration;
            
            if (t >= 1f)
            {
                // Arrived - try to capture
                if (GameManager.IsConnected())
                {
                    GameManager.Conn.Reducers.CaptureWavePacket(flight.packetId);
                }
                
                if (flight.visual != null) Destroy(flight.visual);
                toRemove.Add(kvp.Key);
            }
            else
            {
                // Update position - track player
                Vector3 currentTarget = playerTransform.position;
                flight.visual.transform.position = Vector3.Lerp(flight.startPos, currentTarget, t);
            }
        }
        
        foreach (var id in toRemove)
        {
            flyingPackets.Remove(id);
        }
    }
    
    // ============================================================================
    // UI Updates
    // ============================================================================
    
    void UpdateMiningUI()
    {
        if (miningToggleButton != null)
        {
            var buttonText = miningToggleButton.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.text = isMining ? "Stop Mining" : "Start Mining";
            }
        }
        
        if (miningStatusText != null)
        {
            if (localPlayerCrystal != null)
            {
                string crystalName = localPlayerCrystal.CrystalType.ToString();
                miningStatusText.text = $"Crystal: {crystalName}\nMining: {(isMining ? "Active" : "Inactive")}";
            }
            else
            {
                miningStatusText.text = "No Crystal Selected";
            }
        }
    }
    
    void UpdateTargetInfo()
    {
        if (targetOrbInfoText == null) return;
        
        if (targetOrbId.HasValue && activeOrbs.ContainsKey(targetOrbId.Value))
        {
            var orb = GameManager.Conn.Db.WavePacketOrb.FindByOrbId(targetOrbId.Value);
            if (orb != null)
            {
                Vector3 orbPos = new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z);
                float distance = Vector3.Distance(playerTransform.position, orbPos);
                targetOrbInfoText.text = $"Target: Orb {orb.OrbId}\nDistance: {distance:F1}m\nPackets: {orb.TotalWavePackets}";
            }
        }
        else
        {
            targetOrbInfoText.text = "No Target";
        }
    }
    
    void OnWavePacketStorageUpdate(EventContext ctx, WavePacketStorage storage)
    {
        UpdateInventoryDisplay();
    }
    
    void OnWavePacketStorageChanged(EventContext ctx, WavePacketStorage oldStorage, WavePacketStorage newStorage)
    {
        UpdateInventoryDisplay();
    }
    
    void OnWavePacketStorageDelete(EventContext ctx, WavePacketStorage storage)
    {
        UpdateInventoryDisplay();
    }
    
    void UpdateInventoryDisplay()
    {
        if (localPlayer == null) return;
        
        // Count packets by frequency band
        var packetCounts = new Dictionary<FrequencyBand, uint>();
        
        foreach (var storage in GameManager.Conn.Db.WavePacketStorage.Iter())
        {
            if (storage.OwnerType == "player" && storage.OwnerId == localPlayer.PlayerId)
            {
                packetCounts[storage.FrequencyBand] = storage.TotalWavePackets;
            }
        }
        
        // Update UI texts
        if (redPacketsText) redPacketsText.text = $"Red: {packetCounts.GetValueOrDefault(FrequencyBand.Red, 0)}";
        if (greenPacketsText) greenPacketsText.text = $"Green: {packetCounts.GetValueOrDefault(FrequencyBand.Green, 0)}";
        if (bluePacketsText) bluePacketsText.text = $"Blue: {packetCounts.GetValueOrDefault(FrequencyBand.Blue, 0)}";
        if (yellowPacketsText) yellowPacketsText.text = $"Yellow: {packetCounts.GetValueOrDefault(FrequencyBand.Yellow, 0)}";
        if (cyanPacketsText) cyanPacketsText.text = $"Cyan: {packetCounts.GetValueOrDefault(FrequencyBand.Green, 0)}"; // Note: Check the correct band
        if (magentaPacketsText) magentaPacketsText.text = $"Magenta: {packetCounts.GetValueOrDefault(FrequencyBand.Violet, 0)}"; // Note: Check the correct band
    }
    
    void ClearAllVisuals()
    {
        foreach (var orb in activeOrbs.Values)
        {
            if (orb != null) Destroy(orb);
        }
        activeOrbs.Clear();
        
        foreach (var flight in flyingPackets.Values)
        {
            if (flight.visual != null) Destroy(flight.visual);
        }
        flyingPackets.Clear();
        
        if (currentTractorBeam != null) Destroy(currentTractorBeam);
        if (rangeIndicator != null) Destroy(rangeIndicator);
    }
}

// ============================================================================
// Helper Component for Orb Collection
// ============================================================================

public class WavePacketOrbCollector : MonoBehaviour
{
    private ulong orbId;
    private uint totalPackets;
    
    public void Initialize(ulong id, uint packets)
    {
        orbId = id;
        totalPackets = packets;
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            var localPlayer = GameManager.GetCurrentPlayer();
            if (localPlayer != null)
            {
                GameManager.Conn.Reducers.CollectWavePacketOrb(orbId, localPlayer.PlayerId);
                Debug.Log($"Collecting wave packet orb {orbId} with {totalPackets} packets");
            }
        }
    }
}