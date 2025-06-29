using System;
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
    private Dictionary<ulong, GameObject> orbVisuals = new Dictionary<ulong, GameObject>();
    private Dictionary<ulong, WavePacketFlight> inFlightPackets = new Dictionary<ulong, WavePacketFlight>();
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
            OnGameManagerConnected(GameManager.Conn, GameManager.LocalIdentity.Value);
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
    
    void Update()
    {
        if (isMining && playerTransform != null)
        {
            UpdateMiningTarget();
        }
        
        UpdateInFlightPackets();
    }
    
    void OnGameManagerConnected(DbConnection conn, SpacetimeDB.Identity identity)
    {
        // Subscribe to wave packet tables
        conn.Db.WavePacketOrb.OnInsert += OnWavePacketOrbInsert;
        conn.Db.WavePacketOrb.OnUpdate += OnWavePacketOrbUpdate;
        conn.Db.WavePacketOrb.OnDelete += OnWavePacketOrbDelete;
        
        conn.Db.PlayerCrystal.OnInsert += OnPlayerCrystalInsert;
        conn.Db.PlayerCrystal.OnUpdate += OnPlayerCrystalUpdate;
        conn.Db.PlayerCrystal.OnDelete += OnPlayerCrystalDelete;
        
        conn.Db.WavePacketStorage.OnInsert += OnWavePacketStorageUpdate;
        conn.Db.WavePacketStorage.OnUpdate += OnWavePacketStorageChanged;
        conn.Db.WavePacketStorage.OnDelete += OnWavePacketStorageDelete;
        
        // Subscribe to reducer callbacks
        conn.Reducers.OnStartMining += OnStartMiningResult;
        conn.Reducers.OnStopMining += OnStopMiningResult;
        conn.Reducers.OnCaptureWavePacket += OnCaptureWavePacketResult;
        
        // Check if we already have a local player and crystal
        CheckForExistingCrystal();
    }
    
    void OnGameManagerDisconnected(Exception error)
    {
        UnsubscribeFromEvents();
        
        // Clean up visuals
        foreach (var kvp in orbVisuals)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }
        orbVisuals.Clear();
        
        foreach (var kvp in inFlightPackets)
        {
            if (kvp.Value.visual != null)
                Destroy(kvp.Value.visual);
        }
        inFlightPackets.Clear();
        
        // Hide UIs
        if (crystalSelectionUI) crystalSelectionUI.SetActive(false);
        if (miningUI) miningUI.SetActive(false);
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
        
        if (GameManager.Conn?.Reducers != null)
        {
            GameManager.Conn.Reducers.OnStartMining -= OnStartMiningResult;
            GameManager.Conn.Reducers.OnStopMining -= OnStopMiningResult;
            GameManager.Conn.Reducers.OnCaptureWavePacket -= OnCaptureWavePacketResult;
        }
    }
    
    // ============================================================================
    // Crystal Selection
    // ============================================================================
    
    void CheckForExistingCrystal()
    {
        localPlayer = GameManager.GetCurrentPlayer();
        if (localPlayer == null) return;
        
        var crystal = GameManager.Conn.Db.PlayerCrystal.PlayerId.Find(localPlayer.PlayerId);
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
        
        orbVisuals[orb.OrbId] = orbObj;
    }
    
    void UpdateOrbVisual(WavePacketOrb orb)
    {
        if (!orbVisuals.ContainsKey(orb.OrbId)) return;
        
        GameObject orbObj = orbVisuals[orb.OrbId];
        if (orbObj == null) return;
        
        // Update position
        orbObj.transform.position = new Vector3(orb.Position.X, orb.Position.Y, orb.Position.Z);
        
        // Update color
        Color orbColor = CalculateOrbColor(orb);
        SetOrbVisualColor(orbObj, orbColor, orb.TotalWavePackets);
    }
    
    void RemoveOrbVisual(ulong orbId)
    {
        if (orbVisuals.ContainsKey(orbId))
        {
            if (orbVisuals[orbId] != null)
                Destroy(orbVisuals[orbId]);
            orbVisuals.Remove(orbId);
        }
        
        // If this was our target, clear it
        if (targetOrbId == orbId)
        {
            targetOrbId = null;
            DisableTractorBeam();
        }
    }
    
    Color CalculateOrbColor(WavePacketOrb orb)
    {
        if (orb.WavePacketComposition.Count == 0)
            return Color.white;
        
        float totalR = 0, totalG = 0, totalB = 0;
        uint totalPackets = 0;
        
        foreach (var sample in orb.WavePacketComposition)
        {
            if (sample.Amount > 0)
            {
                Color sampleColor = GetColorFromSignature(sample.Signature);
                totalR += sampleColor.r * sample.Amount;
                totalG += sampleColor.g * sample.Amount;
                totalB += sampleColor.b * sample.Amount;
                totalPackets += sample.Amount;
            }
        }
        
        if (totalPackets > 0)
        {
            return new Color(totalR / totalPackets, totalG / totalPackets, totalB / totalPackets);
        }
        
        return Color.white;
    }
    
    Color GetColorFromSignature(WavePacketSignature signature)
    {
        // Convert frequency (0-1) to hue (0-360)
        float hue = signature.Frequency * 360f;
        
        // Use amplitude for brightness
        float brightness = 0.3f + (signature.Amplitude * 0.7f);
        
        // Use coherence for saturation
        float saturation = 0.4f + (signature.Coherence * 0.6f);
        
        return Color.HSVToRGB(hue / 360f, saturation, brightness);
    }
    
    void SetOrbVisualColor(GameObject orbObj, Color color, uint intensity)
    {
        var renderer = orbObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
            
            // Adjust emission based on intensity
            if (renderer.material.HasProperty("_EmissionColor"))
            {
                float emissionStrength = Mathf.Clamp01(intensity / 100f);
                renderer.material.SetColor("_EmissionColor", color * emissionStrength);
            }
        }
    }
    
    // ============================================================================
    // Mining Logic
    // ============================================================================
    
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
    
    void UpdateMiningTarget()
    {
        if (!GameManager.IsConnected() || localPlayer == null || localPlayerCrystal == null)
            return;
        
        // Find closest valid orb
        WavePacketOrb closestOrb = null;
        float closestDistance = float.MaxValue;
        
        var orbs = GameManager.Conn.Db.WavePacketOrb.Iter();
        foreach (var orb in orbs)
        {
            // Check if orb has matching wave packets
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
    
    // ============================================================================
    // Mining Visuals
    // ============================================================================
    
    void UpdateTractorBeam(ulong orbId)
    {
        if (!orbVisuals.ContainsKey(orbId)) return;
        
        if (currentTractorBeam == null && tractorBeamPrefab != null)
        {
            currentTractorBeam = Instantiate(tractorBeamPrefab);
        }
        
        if (currentTractorBeam != null)
        {
            currentTractorBeam.SetActive(true);
            
            // Update beam position/rotation to connect player to orb
            Vector3 orbPos = orbVisuals[orbId].transform.position;
            Vector3 playerPos = playerTransform.position;
            Vector3 midpoint = (orbPos + playerPos) / 2f;
            
            currentTractorBeam.transform.position = midpoint;
            currentTractorBeam.transform.LookAt(orbPos);
            
            float distance = Vector3.Distance(playerPos, orbPos);
            currentTractorBeam.transform.localScale = new Vector3(1, 1, distance);
        }
    }
    
    void DisableTractorBeam()
    {
        if (currentTractorBeam != null)
        {
            currentTractorBeam.SetActive(false);
        }
    }
    
    void CreateWavePacketVisual(ulong packetId, ulong orbId, WavePacketSignature signature, float flightTime)
    {
        if (!orbVisuals.ContainsKey(orbId) || wavePacketParticlePrefab == null) return;
        
        GameObject orbObj = orbVisuals[orbId];
        GameObject packetVisual = Instantiate(wavePacketParticlePrefab, orbObj.transform.position, Quaternion.identity);
        
        // Set packet color
        Color packetColor = GetColorFromSignature(signature);
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
        
        inFlightPackets[packetId] = flight;
    }
    
    void UpdateInFlightPackets()
    {
        var toRemove = new List<ulong>();
        
        foreach (var kvp in inFlightPackets)
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
            inFlightPackets.Remove(id);
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
        
        if (targetOrbId.HasValue && orbVisuals.ContainsKey(targetOrbId.Value))
        {
            var orb = GameManager.Conn.Db.WavePacketOrb.OrbId.Find(targetOrbId.Value);
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
    
    void UpdateInventoryDisplay()
    {
        if (localPlayer == null || !GameManager.IsConnected()) return;
        
        // Get player's wave packet storage
        var storage = GameManager.Conn.Db.WavePacketStorage.Iter()
            .Where(s => s.OwnerType == "Player" && s.OwnerId == localPlayer.PlayerId)
            .FirstOrDefault();
        
        if (storage != null)
        {
            // Count packets by frequency band
            Dictionary<string, uint> packetCounts = new Dictionary<string, uint>
            {
                ["Red"] = 0,
                ["Green"] = 0,
                ["Blue"] = 0,
                ["Yellow"] = 0,
                ["Cyan"] = 0,
                ["Magenta"] = 0
            };
            
            foreach (var sample in storage.WavePacketComposition)
            {
                string colorBand = GetColorBandFromSignature(sample.Signature);
                if (packetCounts.ContainsKey(colorBand))
                {
                    packetCounts[colorBand] += sample.Amount;
                }
            }
            
            // Update UI texts
            if (redPacketsText) redPacketsText.text = $"Red: {packetCounts["Red"]}";
            if (greenPacketsText) greenPacketsText.text = $"Green: {packetCounts["Green"]}";
            if (bluePacketsText) bluePacketsText.text = $"Blue: {packetCounts["Blue"]}";
            if (yellowPacketsText) yellowPacketsText.text = $"Yellow: {packetCounts["Yellow"]}";
            if (cyanPacketsText) cyanPacketsText.text = $"Cyan: {packetCounts["Cyan"]}";
            if (magentaPacketsText) magentaPacketsText.text = $"Magenta: {packetCounts["Magenta"]}";
        }
    }
    
    string GetColorBandFromSignature(WavePacketSignature signature)
    {
        float freq = signature.Frequency;
        
        if (freq < 0.17f) return "Red";
        else if (freq < 0.33f) return "Yellow";
        else if (freq < 0.5f) return "Green";
        else if (freq < 0.67f) return "Cyan";
        else if (freq < 0.83f) return "Blue";
        else return "Magenta";
    }
    
    // ============================================================================
    // Reducer Callbacks
    // ============================================================================
    
    void OnStartMiningResult(ReducerEventContext ctx, ulong orbId)
    {
        if (ctx.Event.Status is Status.Failed(var error))
        {
            Debug.LogError($"Failed to start mining: {error}");
            isMining = false;
            targetOrbId = null;
            DisableTractorBeam();
            UpdateMiningUI();
        }
    }
    
    void OnStopMiningResult(ReducerEventContext ctx)
    {
        // Mining stopped successfully
    }
    
    void OnCaptureWavePacketResult(ReducerEventContext ctx, ulong wavePacketId)
    {
        if (ctx.Event.Status is Status.Committed)
        {
            Debug.Log($"Successfully captured wave packet {wavePacketId}");
            UpdateInventoryDisplay();
        }
    }
    
    // ============================================================================
    // Storage Event Handlers
    // ============================================================================
    
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
    
    // ============================================================================
    // Cleanup
    // ============================================================================
    
    void ClearAllVisuals()
    {
        foreach (var kvp in orbVisuals)
        {
            if (kvp.Value != null)
                Destroy(kvp.Value);
        }
        orbVisuals.Clear();
        
        foreach (var kvp in inFlightPackets)
        {
            if (kvp.Value.visual != null)
                Destroy(kvp.Value.visual);
        }
        inFlightPackets.Clear();
        
        if (currentTractorBeam != null)
        {
            Destroy(currentTractorBeam);
            currentTractorBeam = null;
        }
        
        if (rangeIndicator != null)
        {
            Destroy(rangeIndicator);
            rangeIndicator = null;
        }
    }
}