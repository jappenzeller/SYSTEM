using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; // ADD THIS for new Input System
using TMPro;
using SpacetimeDB.Types;
using System.Linq;

/// <summary>
/// Handles player interaction with tunnels for world travel.
/// Provides UI for tunnel activation and travel.
/// </summary>
public class TunnelInteractor : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Main tunnel interaction panel")]
    public GameObject tunnelPanel;
    
    [Tooltip("Text showing tunnel destination")]
    public TMP_Text destinationText;
    
    [Tooltip("Text showing tunnel status")]
    public TMP_Text statusText;
    
    [Tooltip("Text showing activation progress")]
    public TMP_Text progressText;
    
    [Tooltip("Slider showing activation progress")]
    public Slider progressSlider;
    
    [Tooltip("Button to activate tunnel")]
    public Button activateButton;
    
    [Tooltip("Button to travel through tunnel")]
    public Button travelButton;
    
    [Tooltip("Input field for energy amount")]
    public TMP_InputField energyInput;
    
    [Tooltip("Text showing player's available energy")]
    public TMP_Text availableEnergyText;
    
    [Tooltip("Close panel button")]
    public Button closeButton;

    [Header("Interaction Settings")]
    [Tooltip("Maximum interaction distance from tunnel")]
    public float interactionRange = 5.0f;
    
    [Tooltip("Key to open tunnel interface")]
    public KeyCode interactionKey = KeyCode.E;

    // Current state
    private Tunnel currentTunnel;
    private bool isNearTunnel = false;
    private Camera playerCamera;
    
    // Input System action
    private InputAction interactAction;

    void Start()
    {
        // Find player camera
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindFirstObjectByType<Camera>();
        }

        // Setup UI
        if (tunnelPanel != null)
        {
            tunnelPanel.SetActive(false);
        }

        // Setup button events
        if (activateButton != null)
        {
            activateButton.onClick.AddListener(OnActivateButtonClicked);
        }
        
        if (travelButton != null)
        {
            travelButton.onClick.AddListener(OnTravelButtonClicked);
        }
        
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseButtonClicked);
        }
        
        // Setup input action for interaction
        SetupInputAction();

        // Subscribe to SpacetimeDB tunnel events
        if (GameManager.IsConnected())
        {
            SetupTunnelEventHandlers();
        }
    }
    
    void SetupInputAction()
    {
        // Create interact action for the E key (or whatever interactionKey is set to)
        string keyBinding = $"<Keyboard>/{interactionKey.ToString().ToLower()}";
        interactAction = new InputAction("Interact", InputActionType.Button, keyBinding);
        interactAction.Enable();
    }
    
    void OnEnable()
    {
        interactAction?.Enable();
    }
    
    void OnDisable()
    {
        interactAction?.Disable();
    }

    void Update()
    {
        // Check for nearby tunnels
        CheckForNearbyTunnels();
        
        // Handle interaction input using new Input System
        if (isNearTunnel && interactAction != null && interactAction.WasPressedThisFrame())
        {
            OpenTunnelInterface();
        }
        
        // Update available energy display
        UpdateAvailableEnergyDisplay();
    }

    void SetupTunnelEventHandlers()
    {
        if (GameManager.Conn?.Db?.Tunnel != null)
        {
            GameManager.Conn.Db.Tunnel.OnUpdate += OnTunnelUpdated;
            GameManager.Conn.Db.Tunnel.OnInsert += OnTunnelCreated;
        }
    }

    void CheckForNearbyTunnels()
    {
        if (!GameManager.IsConnected() || playerCamera == null)
        {
            isNearTunnel = false;
            return;
        }

        // Get current player
        var player = GameManager.GetCurrentPlayer();
        if (player == null)
        {
            isNearTunnel = false;
            return;
        }

        Vector3 playerPosition = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);

        // Find nearest tunnel in current world
        Tunnel nearestTunnel = null;
        float nearestDistance = float.MaxValue;

        // Use the correct SpacetimeDB client iteration method
        var allTunnels = GameManager.Conn.Db.Tunnel.Iter().ToArray();
        
        foreach (var tunnel in allTunnels)
        {
            if (tunnel.FromWorld.X == player.CurrentWorld.X && 
                tunnel.FromWorld.Y == player.CurrentWorld.Y && 
                tunnel.FromWorld.Z == player.CurrentWorld.Z)
            {
                // For now, assume tunnels are at world center + some offset
                // In a real implementation, you'd have tunnel positions stored
                Vector3 tunnelPosition = GetTunnelPosition(tunnel);
                float distance = Vector3.Distance(playerPosition, tunnelPosition);
                
                if (distance < interactionRange && distance < nearestDistance)
                {
                    nearestTunnel = tunnel;
                    nearestDistance = distance;
                }
            }
        }

        bool wasNearTunnel = isNearTunnel;
        isNearTunnel = nearestTunnel != null;
        currentTunnel = nearestTunnel;

        // Show/hide interaction prompt
        if (isNearTunnel != wasNearTunnel)
        {
            if (isNearTunnel)
            {
                ShowInteractionPrompt();
            }
            else
            {
                HideInteractionPrompt();
            }
        }
    }

    Vector3 GetTunnelPosition(Tunnel tunnel)
    {
        // Calculate tunnel position based on destination
        // Tunnels are positioned around the world sphere pointing to their destination
        Vector3 direction = new Vector3(tunnel.ToWorld.X, tunnel.ToWorld.Y, tunnel.ToWorld.Z).normalized;
        return direction * 110f; // Just outside the world sphere (radius 100)
    }

    void ShowInteractionPrompt()
    {
        // TODO: Show UI prompt "Press E to interact with tunnel"
        Debug.Log($"Near tunnel to world ({currentTunnel.ToWorld.X},{currentTunnel.ToWorld.Y},{currentTunnel.ToWorld.Z}) - Press {interactionKey} to interact");
    }

    void HideInteractionPrompt()
    {
        // TODO: Hide UI prompt
    }

    void OpenTunnelInterface()
    {
        if (currentTunnel == null || tunnelPanel == null)
            return;

        // Show tunnel panel
        tunnelPanel.SetActive(true);
        
        // Pause game or lock cursor as needed
        Time.timeScale = 0f; // Pause game while in tunnel interface
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Update tunnel information
        UpdateTunnelUI();
    }

    void UpdateTunnelUI()
    {
        if (currentTunnel == null)
            return;

        // Update destination text
        if (destinationText != null)
        {
            string destName = IsCenter(currentTunnel.ToWorld) ? 
                "Center World" : 
                $"World ({currentTunnel.ToWorld.X},{currentTunnel.ToWorld.Y},{currentTunnel.ToWorld.Z})";
            destinationText.text = $"Destination: {destName}";
        }

        // Update status text
        if (statusText != null)
        {
            statusText.text = $"Status: {currentTunnel.Status}";
        }

        // Update progress
        float progressPercent = currentTunnel.ActivationProgress / currentTunnel.ActivationThreshold;
        
        if (progressSlider != null)
        {
            progressSlider.value = progressPercent;
        }
        
        if (progressText != null)
        {
            progressText.text = $"Activation: {currentTunnel.ActivationProgress:F1}/{currentTunnel.ActivationThreshold:F1} " +
                              $"({progressPercent:P0})";
        }

        // Update buttons based on tunnel status
        if (activateButton != null)
        {
            activateButton.gameObject.SetActive(currentTunnel.Status == "Potential" || currentTunnel.Status == "Activating");
            activateButton.interactable = currentTunnel.Status != "Active";
        }
        
        if (travelButton != null)
        {
            travelButton.gameObject.SetActive(currentTunnel.Status == "Active");
        }

        // Set default energy amount
        if (energyInput != null && string.IsNullOrEmpty(energyInput.text))
        {
            float remainingEnergy = currentTunnel.ActivationThreshold - currentTunnel.ActivationProgress;
            energyInput.text = Mathf.Min(10f, remainingEnergy).ToString("F1");
        }
    }

    void UpdateAvailableEnergyDisplay()
    {
        if (availableEnergyText == null || !GameManager.IsConnected())
            return;

        var player = GameManager.GetCurrentPlayer();
        if (player == null)
            return;

        // Get player's red energy (simplified - in full game you'd show all energy types)
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
        availableEnergyText.text = $"Available Energy (Red): {availableEnergy:F1}";
    }

    void OnActivateButtonClicked()
    {
        if (currentTunnel == null || !GameManager.IsConnected())
            return;

        // Parse energy amount
        if (!float.TryParse(energyInput.text, out float energyAmount) || energyAmount <= 0)
        {
            Debug.LogWarning("Invalid energy amount entered");
            return;
        }

        // Check if player has enough energy
        var player = GameManager.GetCurrentPlayer();
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
        if (availableEnergy < energyAmount)
        {
            Debug.LogWarning("Not enough energy to activate tunnel");
            return;
        }

        // Call tunnel activation
        GameManager.ActivateTunnel(currentTunnel.TunnelId, energyAmount);
        
        Debug.Log($"Activated tunnel {currentTunnel.TunnelId} with {energyAmount} energy");
    }

    void OnTravelButtonClicked()
    {
        if (currentTunnel == null || currentTunnel.Status != "Active")
            return;

        Debug.Log($"Traveling through tunnel to world ({currentTunnel.ToWorld.X},{currentTunnel.ToWorld.Y},{currentTunnel.ToWorld.Z})");

        // Close the interface
        OnCloseButtonClicked();

        // Initiate tunnel travel
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.TransitionThroughTunnel(currentTunnel.TunnelId);
        }
    }

    void OnCloseButtonClicked()
    {
        if (tunnelPanel != null)
        {
            tunnelPanel.SetActive(false);
        }

        // Resume game
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // SpacetimeDB event handlers
    void OnTunnelUpdated(EventContext ctx, Tunnel oldTunnel, Tunnel newTunnel)
    {
        // If this is the tunnel we're currently viewing, update the UI
        if (currentTunnel != null && newTunnel.TunnelId == currentTunnel.TunnelId)
        {
            currentTunnel = newTunnel;
            if (tunnelPanel != null && tunnelPanel.activeInHierarchy)
            {
                UpdateTunnelUI();
            }
        }
    }

    void OnTunnelCreated(EventContext ctx, Tunnel tunnel)
    {
        Debug.Log($"New tunnel created from ({tunnel.FromWorld.X},{tunnel.FromWorld.Y},{tunnel.FromWorld.Z}) " +
                 $"to ({tunnel.ToWorld.X},{tunnel.ToWorld.Y},{tunnel.ToWorld.Z})");
    }

    bool IsCenter(WorldCoords coords)
    {
        return coords.X == 0 && coords.Y == 0 && coords.Z == 0;
    }

    void OnDestroy()
    {
        // Clean up event subscriptions
        if (GameManager.Conn?.Db?.Tunnel != null)
        {
            GameManager.Conn.Db.Tunnel.OnUpdate -= OnTunnelUpdated;
            GameManager.Conn.Db.Tunnel.OnInsert -= OnTunnelCreated;
        }
        
        // Clean up input action
        interactAction?.Disable();
        interactAction?.Dispose();
    }
}