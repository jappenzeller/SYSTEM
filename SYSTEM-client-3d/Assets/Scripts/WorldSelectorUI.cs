using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // ADD THIS LINE for new Input System
using TMPro;
using SpacetimeDB.Types;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// World selector UI for choosing destinations in the quantum metaverse.
/// Shows available worlds, tunnel status, and allows world navigation.
/// </summary>
public class WorldSelectorUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Main world selector panel")]
    public GameObject worldSelectorPanel;
    
    [Tooltip("Scroll view for world list")]
    public ScrollRect worldScrollView;
    
    [Tooltip("Content parent for world entries")]
    public Transform worldListContent;
    
    [Tooltip("Prefab for world entry UI")]
    public GameObject worldEntryPrefab;
    
    [Tooltip("Current world display text")]
    public TMP_Text currentWorldText;
    
    [Tooltip("Button to open world selector")]
    public Button openSelectorButton;
    
    [Tooltip("Button to close world selector")]
    public Button closeSelectorButton;
    
    [Tooltip("Button to refresh world list")]
    public Button refreshButton;

    [Header("World Entry Prefab Components")]
    [Tooltip("These should match the components in your worldEntryPrefab")]
    public string worldNameTextComponent = "WorldNameText";
    public string worldStatusTextComponent = "WorldStatusText";
    public string tunnelProgressComponent = "TunnelProgressSlider";
    public string selectButtonComponent = "SelectButton";
    public string activateButtonComponent = "ActivateButton";

    [Header("Settings")]
    [Tooltip("Key to toggle world selector")]
    public KeyCode toggleKey = KeyCode.M;
    
    [Tooltip("Only show this in center world")]
    public bool centerWorldOnly = true;

    // Current state
    private List<GameObject> worldEntries = new List<GameObject>();
    private bool isOpen = false;
    
    // Input System
    private InputAction toggleAction;

    void Start()
    {
        // Setup UI
        if (worldSelectorPanel != null)
        {
            worldSelectorPanel.SetActive(false);
        }

        // Setup button events
        if (openSelectorButton != null)
        {
            openSelectorButton.onClick.AddListener(OpenWorldSelector);
        }
        
        if (closeSelectorButton != null)
        {
            closeSelectorButton.onClick.AddListener(CloseWorldSelector);
        }
        
        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(RefreshWorldList);
        }

        // Subscribe to SpacetimeDB events
        if (GameManager.IsConnected())
        {
            SetupEventHandlers();
        }
        
        // Setup input action
        SetupInputAction();

        // Initial update
        UpdateCurrentWorldDisplay();
    }
    
    void SetupInputAction()
    {
        // Create toggle action for the M key
        toggleAction = new InputAction("ToggleWorldSelector", InputActionType.Button, "<Keyboard>/m");
        toggleAction.Enable();
    }

    void Update()
    {
        // Toggle with key using new Input System
        if (toggleAction != null && toggleAction.WasPressedThisFrame())
        {
            if (CanUseWorldSelector())
            {
                ToggleWorldSelector();
            }
        }

        // Update current world display
        UpdateCurrentWorldDisplay();
    }
    
    void OnEnable()
    {
        toggleAction?.Enable();
    }
    
    void OnDisable()
    {
        toggleAction?.Disable();
    }

    bool CanUseWorldSelector()
    {
        if (!GameManager.IsConnected())
            return false;

        if (centerWorldOnly && GameData.Instance != null)
        {
            return GameData.Instance.IsInCenterWorld();
        }

        return true;
    }

    void SetupEventHandlers()
    {
        if (GameManager.Conn?.Db != null)
        {
            GameManager.Conn.Db.Tunnel.OnUpdate += OnTunnelUpdated;
            GameManager.Conn.Db.Tunnel.OnInsert += OnTunnelCreated;
            GameManager.Conn.Db.World.OnUpdate += OnWorldUpdated;
        }
    }

    void UpdateCurrentWorldDisplay()
    {
        if (currentWorldText == null || GameData.Instance == null)
            return;

        string worldName = GameData.Instance.GetCurrentWorldString();
        int shellLevel = GameData.Instance.GetCurrentWorldShellLevel();
        
        currentWorldText.text = $"Current: {worldName} (Shell {shellLevel})";
    }

    public void ToggleWorldSelector()
    {
        if (isOpen)
        {
            CloseWorldSelector();
        }
        else
        {
            OpenWorldSelector();
        }
    }

    public void OpenWorldSelector()
    {
        if (!CanUseWorldSelector() || worldSelectorPanel == null)
            return;

        worldSelectorPanel.SetActive(true);
        isOpen = true;

        // Pause game
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Populate world list
        RefreshWorldList();
    }

    public void CloseWorldSelector()
    {
        if (worldSelectorPanel == null)
            return;

        worldSelectorPanel.SetActive(false);
        isOpen = false;

        // Resume game
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void RefreshWorldList()
    {
        if (!GameManager.IsConnected() || worldListContent == null)
            return;

        // Clear existing entries
        ClearWorldEntries();

        // Get current player to determine available worlds
        var player = GameManager.Instance.GetCurrentPlayer();
        if (player == null)
            return;

        // Get all worlds and tunnels
        var worlds = GameManager.Conn.Db.World.Iter().ToArray();
        var allTunnels = GameManager.Conn.Db.Tunnel.Iter().ToArray();
        
        var tunnels = new List<Tunnel>();
        foreach (var tunnel in allTunnels)
        {
            if (tunnel.FromWorld.X == player.CurrentWorld.X && 
                tunnel.FromWorld.Y == player.CurrentWorld.Y && 
                tunnel.FromWorld.Z == player.CurrentWorld.Z)
            {
                tunnels.Add(tunnel);
            }
        }

        // Create entries for each accessible world
        foreach (var tunnel in tunnels)
        {
            World targetWorld = null;
            foreach (var world in worlds)
            {
                if (world.WorldCoords.X == tunnel.ToWorld.X && 
                    world.WorldCoords.Y == tunnel.ToWorld.Y && 
                    world.WorldCoords.Z == tunnel.ToWorld.Z)
                {
                    targetWorld = world;
                    break;
                }
            }

            if (targetWorld != null)
            {
                CreateWorldEntry(targetWorld, tunnel);
            }
        }

        // Also add current world as "stay here" option
        World currentWorld = null;
        foreach (var world in worlds)
        {
            if (world.WorldCoords.X == player.CurrentWorld.X && 
                world.WorldCoords.Y == player.CurrentWorld.Y && 
                world.WorldCoords.Z == player.CurrentWorld.Z)
            {
                currentWorld = world;
                break;
            }
        }

        if (currentWorld != null)
        {
            CreateCurrentWorldEntry(currentWorld);
        }
    }

    void CreateWorldEntry(World world, Tunnel tunnel)
    {
        if (worldEntryPrefab == null || worldListContent == null)
            return;

        GameObject entry = Instantiate(worldEntryPrefab, worldListContent);
        worldEntries.Add(entry);

        // Get components
        var worldNameText = entry.transform.Find(worldNameTextComponent)?.GetComponent<TMP_Text>();
        var worldStatusText = entry.transform.Find(worldStatusTextComponent)?.GetComponent<TMP_Text>();
        var tunnelProgressSlider = entry.transform.Find(tunnelProgressComponent)?.GetComponent<Slider>();
        var selectButton = entry.transform.Find(selectButtonComponent)?.GetComponent<Button>();
        var activateButton = entry.transform.Find(activateButtonComponent)?.GetComponent<Button>();

        // Set world name
        if (worldNameText != null)
        {
            string worldName = IsCenter(world.WorldCoords) ? 
                "Center World" : 
                $"World ({world.WorldCoords.X},{world.WorldCoords.Y},{world.WorldCoords.Z})";
            worldNameText.text = worldName;
        }

        // Set world status
        if (worldStatusText != null)
        {
            string status = $"Shell {world.ShellLevel} • {world.Status}";
            if (tunnel.Status == "Active")
            {
                status += " • Accessible";
                worldStatusText.color = Color.green;
            }
            else
            {
                status += $" • {tunnel.Status}";
                worldStatusText.color = tunnel.Status == "Activating" ? Color.yellow : Color.red;
            }
            worldStatusText.text = status;
        }

        // Set tunnel progress
        if (tunnelProgressSlider != null)
        {
            float progress = tunnel.ActivationProgress / tunnel.ActivationThreshold;
            tunnelProgressSlider.value = progress;
            
            // Add progress text if slider has a child text component
            var progressText = tunnelProgressSlider.GetComponentInChildren<TMP_Text>();
            if (progressText != null)
            {
                progressText.text = $"{progress:P0}";
            }
        }

        // Setup select button
        if (selectButton != null)
        {
            selectButton.interactable = (tunnel.Status == "Active");
            selectButton.onClick.AddListener(() => SelectWorld(world.WorldCoords, tunnel.TunnelId));
            
            var buttonText = selectButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = tunnel.Status == "Active" ? "Travel" : "Locked";
            }
        }

        // Setup activate button
        if (activateButton != null)
        {
            bool canActivate = (tunnel.Status == "Potential" || tunnel.Status == "Activating");
            activateButton.gameObject.SetActive(canActivate);
            
            if (canActivate)
            {
                activateButton.onClick.AddListener(() => ShowActivateDialog(tunnel));
            }
        }
    }

    void CreateCurrentWorldEntry(World world)
    {
        if (worldEntryPrefab == null || worldListContent == null)
            return;

        GameObject entry = Instantiate(worldEntryPrefab, worldListContent);
        worldEntries.Add(entry);

        // Get components
        var worldNameText = entry.transform.Find(worldNameTextComponent)?.GetComponent<TMP_Text>();
        var worldStatusText = entry.transform.Find(worldStatusTextComponent)?.GetComponent<TMP_Text>();
        var tunnelProgressSlider = entry.transform.Find(tunnelProgressComponent)?.GetComponent<Slider>();
        var selectButton = entry.transform.Find(selectButtonComponent)?.GetComponent<Button>();
        var activateButton = entry.transform.Find(activateButtonComponent)?.GetComponent<Button>();

        // Set as current world
        if (worldNameText != null)
        {
            string worldName = IsCenter(world.WorldCoords) ? 
                "Center World" : 
                $"World ({world.WorldCoords.X},{world.WorldCoords.Y},{world.WorldCoords.Z})";
            worldNameText.text = $"{worldName} (Current)";
            worldNameText.color = Color.cyan;
        }

        if (worldStatusText != null)
        {
            worldStatusText.text = $"Shell {world.ShellLevel} • You are here";
            worldStatusText.color = Color.cyan;
        }

        // Hide tunnel progress for current world
        if (tunnelProgressSlider != null)
        {
            tunnelProgressSlider.gameObject.SetActive(false);
        }

        // Setup buttons for current world
        if (selectButton != null)
        {
            selectButton.interactable = false;
            var buttonText = selectButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = "Current";
            }
        }

        if (activateButton != null)
        {
            activateButton.gameObject.SetActive(false);
        }
    }

    void ClearWorldEntries()
    {
        foreach (var entry in worldEntries)
        {
            if (entry != null)
            {
                Destroy(entry);
            }
        }
        worldEntries.Clear();
    }

    void SelectWorld(WorldCoords targetCoords, ulong tunnelId)
    {
        Debug.Log($"Selected world ({targetCoords.X},{targetCoords.Y},{targetCoords.Z}) via tunnel {tunnelId}");
        
        // Close the selector
        CloseWorldSelector();
        
        // Initiate travel
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.TransitionThroughTunnel(tunnelId);
        }
    }

    void ShowActivateDialog(Tunnel tunnel)
    {
        // Simple activation - in a full game you'd show a proper dialog
        float energyNeeded = tunnel.ActivationThreshold - tunnel.ActivationProgress;
        float energyToSpend = Mathf.Min(10f, energyNeeded);
        
        Debug.Log($"Attempting to activate tunnel {tunnel.TunnelId} with {energyToSpend} energy");
        
        // Check if player has enough energy
        var player = GameManager.Instance.GetCurrentPlayer();
        if (player == null)
            return;

        EnergyStorage energyStorage = null;
        var allEnergyStorage = GameManager.Conn.Db.EnergyStorage.Iter().ToArray();
        
        foreach (var storage in allEnergyStorage)
        {
            if (storage.OwnerType == "player" && 
                storage.OwnerId == player.PlayerId && 
                storage.EnergyType == EnergyType.Red)
            {
                energyStorage = storage;
                break;
            }
        }

        float availableEnergy = energyStorage?.Amount ?? 0f;
        if (availableEnergy < energyToSpend)
        {
            Debug.LogWarning("Not enough energy to activate tunnel");
            return;
        }

        // Call activation
        GameManager.Instance.ActivateTunnel(tunnel.TunnelId, energyToSpend);
        
        // Refresh the list after a short delay
        Invoke(nameof(RefreshWorldList), 0.5f);
    }

    // Event handlers
    void OnTunnelUpdated(EventContext ctx, Tunnel oldTunnel, Tunnel newTunnel)
    {
        if (isOpen)
        {
            // Refresh list to show updated tunnel status
            RefreshWorldList();
        }
    }

    void OnTunnelCreated(EventContext ctx, Tunnel tunnel)
    {
        if (isOpen)
        {
            RefreshWorldList();
        }
    }

    void OnWorldUpdated(EventContext ctx, World oldWorld, World newWorld)
    {
        if (isOpen)
        {
            RefreshWorldList();
        }
    }

    bool IsCenter(WorldCoords coords)
    {
        return coords.X == 0 && coords.Y == 0 && coords.Z == 0;
    }

    // Public methods for external UI
    public void ShowWorldSelector()
    {
        OpenWorldSelector();
    }

    public void HideWorldSelector()
    {
        CloseWorldSelector();
    }

    public bool IsWorldSelectorOpen()
    {
        return isOpen;
    }

    void OnDestroy()
    {
        // Clean up event subscriptions
        if (GameManager.Conn?.Db != null)
        {
            GameManager.Conn.Db.Tunnel.OnUpdate -= OnTunnelUpdated;
            GameManager.Conn.Db.Tunnel.OnInsert -= OnTunnelCreated;
            GameManager.Conn.Db.World.OnUpdate -= OnWorldUpdated;
        }
        
        // Clear entries
        ClearWorldEntries();
        
        // Clean up input action
        toggleAction?.Disable();
        toggleAction?.Dispose();
    }
}