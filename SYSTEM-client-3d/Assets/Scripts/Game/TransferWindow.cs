using UnityEngine;
using UnityEngine.UIElements;
using SpacetimeDB.Types;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

namespace SYSTEM.Game
{
    /// <summary>
    /// UI controller for the Energy Transfer window
    /// Allows players to transfer wave packet compositions to storage devices
    /// </summary>
    public class TransferWindow : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        private PlayerInputActions playerInputActions;

        private VisualElement root;
        private VisualElement transferWindow;
        private Button closeButton;
        private Button transferButton;
        private Button cancelButton;

        // Frequency sliders
        private SliderInt redSlider, yellowSlider, greenSlider, cyanSlider, blueSlider, magentaSlider;
        private Label redValue, yellowValue, greenValue, cyanValue, blueValue, magentaValue;
        private Label redInventory, yellowInventory, greenInventory, cyanInventory, blueInventory, magentaInventory;
        private Label totalCount;
        private Label validationMessage;

        // Storage selection
        private DropdownField storageDropdown;
        private Label storageInfo;

        // Frequency constants (matching server)
        private const float FREQ_RED = 0.0f;
        private const float FREQ_YELLOW = 1.047f;
        private const float FREQ_GREEN = 2.094f;
        private const float FREQ_CYAN = 3.142f;
        private const float FREQ_BLUE = 4.189f;
        private const float FREQ_MAGENTA = 5.236f;

        // State
        private List<StorageDevice> availableDevices = new List<StorageDevice>();
        private ulong selectedDeviceId = 0;
        private bool isVisible = false;

        void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            // Initialize input actions
            playerInputActions = new PlayerInputActions();
            playerInputActions.Gameplay.ToggleTransfer.performed += ctx => Toggle();
        }

        void OnEnable()
        {
            if (root == null && uiDocument != null)
            {
                InitializeUI();
            }

            // Enable input actions
            playerInputActions?.Gameplay.Enable();

            // Subscribe to GameEventBus events
            // TODO: Subscribe to InventoryUpdatedEvent when it exists
            // SpacetimeDB.Types.GameEventBus.Instance.Subscribe<SpacetimeDB.Types.InventoryUpdatedEvent>(OnInventoryUpdated);
        }

        void OnDisable()
        {
            // Disable input actions
            playerInputActions?.Gameplay.Disable();

            // Unsubscribe from events
            if (SpacetimeDB.Types.GameEventBus.Instance != null)
            {
                // TODO: Unsubscribe when event exists
                // SpacetimeDB.Types.GameEventBus.Instance.Unsubscribe<SpacetimeDB.Types.InventoryUpdatedEvent>(OnInventoryUpdated);
            }
        }

        void InitializeUI()
        {
            root = uiDocument.rootVisualElement;
            transferWindow = root.Q<VisualElement>("transfer-window");

            // Header buttons
            closeButton = root.Q<Button>("close-button");
            closeButton.clicked += Hide;

            // Frequency sliders
            redSlider = root.Q<SliderInt>("red-slider");
            yellowSlider = root.Q<SliderInt>("yellow-slider");
            greenSlider = root.Q<SliderInt>("green-slider");
            cyanSlider = root.Q<SliderInt>("cyan-slider");
            blueSlider = root.Q<SliderInt>("blue-slider");
            magentaSlider = root.Q<SliderInt>("magenta-slider");

            // Slider value labels
            redValue = root.Q<Label>("red-value");
            yellowValue = root.Q<Label>("yellow-value");
            greenValue = root.Q<Label>("green-value");
            cyanValue = root.Q<Label>("cyan-value");
            blueValue = root.Q<Label>("blue-value");
            magentaValue = root.Q<Label>("magenta-value");

            // Inventory labels
            redInventory = root.Q<Label>("red-inventory");
            yellowInventory = root.Q<Label>("yellow-inventory");
            greenInventory = root.Q<Label>("green-inventory");
            cyanInventory = root.Q<Label>("cyan-inventory");
            blueInventory = root.Q<Label>("blue-inventory");
            magentaInventory = root.Q<Label>("magenta-inventory");

            // Total and validation
            totalCount = root.Q<Label>("total-count");
            validationMessage = root.Q<Label>("validation-message");

            // Storage dropdown
            storageDropdown = root.Q<DropdownField>("storage-dropdown");
            storageInfo = root.Q<Label>("storage-info");

            // Action buttons
            transferButton = root.Q<Button>("transfer-button");
            cancelButton = root.Q<Button>("cancel-button");

            transferButton.clicked += OnTransferClicked;
            cancelButton.clicked += Hide;

            // Register slider change callbacks
            redSlider.RegisterValueChangedCallback(evt => OnSliderChanged());
            yellowSlider.RegisterValueChangedCallback(evt => OnSliderChanged());
            greenSlider.RegisterValueChangedCallback(evt => OnSliderChanged());
            cyanSlider.RegisterValueChangedCallback(evt => OnSliderChanged());
            blueSlider.RegisterValueChangedCallback(evt => OnSliderChanged());
            magentaSlider.RegisterValueChangedCallback(evt => OnSliderChanged());

            // Register storage dropdown callback
            storageDropdown.RegisterValueChangedCallback(evt => OnStorageChanged(evt.newValue));

            UnityEngine.Debug.Log("[TransferWindow] UI initialized");
        }

        // Helper method to get current player ID
        private ulong? GetCurrentPlayerId()
        {
            if (GameData.Instance == null || !GameData.Instance.PlayerIdentity.HasValue)
            {
                return null;
            }

            var conn = GameManager.Conn;
            var identity = GameData.Instance.PlayerIdentity.Value;

            foreach (var player in conn.Db.Player.Iter())
            {
                if (player.Identity == identity)
                {
                    return player.PlayerId;
                }
            }

            return null;
        }

        public void Toggle()
        {
            if (isVisible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        public void Show()
        {
            if (transferWindow == null) InitializeUI();

            transferWindow.RemoveFromClassList("hidden");
            isVisible = true;

            // Load player's storage devices
            LoadStorageDevices();

            // Update inventory display
            UpdateInventoryDisplay();

            // Reset sliders
            ResetSliders();

            UnityEngine.Debug.Log("[TransferWindow] Window shown");
        }

        public void Hide()
        {
            if (transferWindow == null) return;

            transferWindow.AddToClassList("hidden");
            isVisible = false;

            UnityEngine.Debug.Log("[TransferWindow] Window hidden");
        }

        private void LoadStorageDevices()
        {
            availableDevices.Clear();

            if (GameManager.Instance == null || !GameManager.IsConnected())
            {
                storageDropdown.choices = new List<string> { "No connection" };
                storageDropdown.SetEnabled(false);
                return;
            }

            var conn = GameManager.Conn;
            ulong? playerIdNullable = GetCurrentPlayerId();
            if (!playerIdNullable.HasValue)
            {
                storageDropdown.choices = new List<string> { "Player not found" };
                storageDropdown.SetEnabled(false);
                return;
            }
            ulong playerId = playerIdNullable.Value;

            // Load player's storage devices
            foreach (var device in conn.Db.StorageDevice.Iter())
            {
                if (device.OwnerPlayerId == playerId)
                {
                    availableDevices.Add(device);
                }
            }

            if (availableDevices.Count == 0)
            {
                storageDropdown.choices = new List<string> { "No storage devices" };
                storageDropdown.SetEnabled(false);
                storageInfo.text = "Create a storage device first";
                return;
            }

            // Populate dropdown
            var choices = availableDevices.Select(d =>
                string.IsNullOrEmpty(d.DeviceName) ? $"Device {d.DeviceId}" : d.DeviceName
            ).ToList();

            storageDropdown.choices = choices;
            storageDropdown.index = 0;
            storageDropdown.SetEnabled(true);

            // Select first device
            if (availableDevices.Count > 0)
            {
                selectedDeviceId = availableDevices[0].DeviceId;
                UpdateStorageInfo(availableDevices[0]);
            }
        }

        private void OnStorageChanged(string deviceName)
        {
            int index = storageDropdown.index;
            if (index >= 0 && index < availableDevices.Count)
            {
                selectedDeviceId = availableDevices[index].DeviceId;
                UpdateStorageInfo(availableDevices[index]);
            }
        }

        private void UpdateStorageInfo(StorageDevice device)
        {
            // Calculate total stored packets
            uint totalStored = 0;
            foreach (var sample in device.StoredComposition)
            {
                totalStored += sample.Count;
            }

            uint maxCapacity = device.CapacityPerFrequency * 6; // 6 frequencies
            storageInfo.text = $"Capacity: {totalStored} / {maxCapacity} total";
        }

        private void UpdateInventoryDisplay()
        {
            if (GameManager.Instance == null || !GameManager.IsConnected())
            {
                return;
            }

            var conn = GameManager.Conn;
            ulong? playerIdNullable = GetCurrentPlayerId();
            if (!playerIdNullable.HasValue) return;
            ulong playerId = playerIdNullable.Value;

            var inventory = conn.Db.PlayerInventory.PlayerId.Find(playerId);
            if (inventory != null)
            {
                // Extract counts from composition for each frequency
                uint redCount = GetCountForFrequency(inventory.InventoryComposition, FREQ_RED);
                uint yellowCount = GetCountForFrequency(inventory.InventoryComposition, FREQ_YELLOW);
                uint greenCount = GetCountForFrequency(inventory.InventoryComposition, FREQ_GREEN);
                uint cyanCount = GetCountForFrequency(inventory.InventoryComposition, FREQ_CYAN);
                uint blueCount = GetCountForFrequency(inventory.InventoryComposition, FREQ_BLUE);
                uint magentaCount = GetCountForFrequency(inventory.InventoryComposition, FREQ_MAGENTA);

                redInventory.text = $"Inventory: {redCount}";
                yellowInventory.text = $"Inventory: {yellowCount}";
                greenInventory.text = $"Inventory: {greenCount}";
                cyanInventory.text = $"Inventory: {cyanCount}";
                blueInventory.text = $"Inventory: {blueCount}";
                magentaInventory.text = $"Inventory: {magentaCount}";

                // Update slider max values
                redSlider.highValue = (int)System.Math.Min(5, redCount);
                yellowSlider.highValue = (int)System.Math.Min(5, yellowCount);
                greenSlider.highValue = (int)System.Math.Min(5, greenCount);
                cyanSlider.highValue = (int)System.Math.Min(5, cyanCount);
                blueSlider.highValue = (int)System.Math.Min(5, blueCount);
                magentaSlider.highValue = (int)System.Math.Min(5, magentaCount);
            }
        }

        // Helper method to get packet count for a specific frequency from composition
        private uint GetCountForFrequency(List<WavePacketSample> composition, float targetFreq)
        {
            foreach (var sample in composition)
            {
                // Match with tolerance for floating point comparison
                if (System.Math.Abs(sample.Frequency - targetFreq) < 0.01f)
                {
                    return sample.Count;
                }
            }
            return 0;
        }

        private void ResetSliders()
        {
            redSlider.value = 0;
            yellowSlider.value = 0;
            greenSlider.value = 0;
            cyanSlider.value = 0;
            blueSlider.value = 0;
            magentaSlider.value = 0;

            OnSliderChanged();
        }

        private void OnSliderChanged()
        {
            // Update value labels
            redValue.text = redSlider.value.ToString();
            yellowValue.text = yellowSlider.value.ToString();
            greenValue.text = greenSlider.value.ToString();
            cyanValue.text = cyanSlider.value.ToString();
            blueValue.text = blueSlider.value.ToString();
            magentaValue.text = magentaSlider.value.ToString();

            // Calculate total
            int total = redSlider.value + yellowSlider.value + greenSlider.value +
                       cyanSlider.value + blueSlider.value + magentaSlider.value;

            totalCount.text = $"{total} / 30";

            // Validate
            ValidateTransfer(total);
        }

        private void ValidateTransfer(int total)
        {
            validationMessage.AddToClassList("hidden");
            transferButton.SetEnabled(true);

            if (total == 0)
            {
                validationMessage.RemoveFromClassList("hidden");
                validationMessage.text = "Select at least one packet to transfer";
                transferButton.SetEnabled(false);
                return;
            }

            if (total > 30)
            {
                validationMessage.RemoveFromClassList("hidden");
                validationMessage.text = "Cannot transfer more than 30 packets at once";
                transferButton.SetEnabled(false);
                return;
            }

            if (availableDevices.Count == 0)
            {
                validationMessage.RemoveFromClassList("hidden");
                validationMessage.text = "No storage devices available";
                transferButton.SetEnabled(false);
                return;
            }
        }

        private void OnTransferClicked()
        {
            // Build composition from sliders
            List<WavePacketSample> composition = new List<WavePacketSample>();

            if (redSlider.value > 0)
                composition.Add(new WavePacketSample { Frequency = FREQ_RED, Amplitude = 1.0f, Phase = 0.0f, Count = (uint)redSlider.value });

            if (yellowSlider.value > 0)
                composition.Add(new WavePacketSample { Frequency = FREQ_YELLOW, Amplitude = 1.0f, Phase = 0.0f, Count = (uint)yellowSlider.value });

            if (greenSlider.value > 0)
                composition.Add(new WavePacketSample { Frequency = FREQ_GREEN, Amplitude = 1.0f, Phase = 0.0f, Count = (uint)greenSlider.value });

            if (cyanSlider.value > 0)
                composition.Add(new WavePacketSample { Frequency = FREQ_CYAN, Amplitude = 1.0f, Phase = 0.0f, Count = (uint)cyanSlider.value });

            if (blueSlider.value > 0)
                composition.Add(new WavePacketSample { Frequency = FREQ_BLUE, Amplitude = 1.0f, Phase = 0.0f, Count = (uint)blueSlider.value });

            if (magentaSlider.value > 0)
                composition.Add(new WavePacketSample { Frequency = FREQ_MAGENTA, Amplitude = 1.0f, Phase = 0.0f, Count = (uint)magentaSlider.value });

            // Call reducer
            if (GameManager.Instance != null && GameManager.IsConnected())
            {
                var conn = GameManager.Conn;

                UnityEngine.Debug.Log($"[TransferWindow] Initiating transfer: {composition.Count} frequencies to device {selectedDeviceId}");

                conn.Reducers.InitiateTransfer(composition, selectedDeviceId);

                // Hide window after initiating transfer
                Hide();
            }
            else
            {
                UnityEngine.Debug.LogError("[TransferWindow] Cannot initiate transfer - not connected");
            }
        }

        // TODO: Uncomment when InventoryUpdatedEvent exists
        /*private void OnInventoryUpdated(SpacetimeDB.Types.InventoryUpdatedEvent evt)
        {
            if (isVisible)
            {
                UpdateInventoryDisplay();
            }
        }*/
    }
}
