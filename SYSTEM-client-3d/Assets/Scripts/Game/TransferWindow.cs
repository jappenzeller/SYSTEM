using UnityEngine;
using UnityEngine.UIElements;
using SpacetimeDB.Types;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;

namespace SYSTEM.Game
{
    /// <summary>
    /// UI controller for the Energy Transfer window (redesigned two-panel layout)
    /// Allows players to transfer packets between their inventory and storage devices
    /// </summary>
    public class TransferWindow : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        private PlayerInputActions playerInputActions;

        // UI Elements - Root
        private VisualElement root;
        private VisualElement transferWindow;
        private Button closeButton;
        private Button transferButton;
        private Button cancelButton;
        private Label validationMessage;

        // Source Panel
        private DropdownField sourceDropdown;
        private Label sourceRed, sourceYellow, sourceGreen, sourceCyan, sourceBlue, sourceMagenta;

        // Destination Panel
        private DropdownField destinationDropdown;
        private IntegerField redAmount, yellowAmount, greenAmount, cyanAmount, blueAmount, magentaAmount;
        private Label totalCount;

        // Frequency constants (matching server - in radians)
        private const float FREQ_RED = 0.0f;
        private const float FREQ_YELLOW = 1.047f;
        private const float FREQ_GREEN = 2.094f;
        private const float FREQ_CYAN = 3.142f;
        private const float FREQ_BLUE = 4.189f;
        private const float FREQ_MAGENTA = 5.236f;

        // State
        private enum LocationType { Inventory, StorageDevice }

        private class TransferLocation
        {
            public LocationType Type;
            public ulong DeviceId; // 0 for inventory
            public string DisplayName;
            public Dictionary<float, uint> Composition; // frequency -> count
        }

        private List<TransferLocation> locations = new List<TransferLocation>();
        private TransferLocation selectedSource;
        private TransferLocation selectedDestination;
        private bool isVisible = false;

        void Awake()
        {
            playerInputActions = new PlayerInputActions();
        }

        void OnEnable()
        {
            playerInputActions.Enable();
            playerInputActions.Gameplay.ToggleTransfer.performed += OnToggleTransferInput;
        }

        void OnDisable()
        {
            playerInputActions.Gameplay.ToggleTransfer.performed -= OnToggleTransferInput;
            playerInputActions.Disable();
        }

        void Start()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument != null)
            {
                InitializeUI();
            }
        }

        private void OnToggleTransferInput(InputAction.CallbackContext context)
        {
            Toggle();
        }

        private void InitializeUI()
        {
            root = uiDocument.rootVisualElement;
            transferWindow = root.Q<VisualElement>("transfer-window");

            if (transferWindow == null)
            {
                UnityEngine.Debug.LogError("[TransferWindow] Could not find transfer-window element");
                return;
            }

            // Buttons
            closeButton = root.Q<Button>("close-button");
            transferButton = root.Q<Button>("transfer-button");
            cancelButton = root.Q<Button>("cancel-button");
            validationMessage = root.Q<Label>("validation-message");

            // Source panel
            sourceDropdown = root.Q<DropdownField>("source-dropdown");
            sourceRed = root.Q<Label>("source-red");
            sourceYellow = root.Q<Label>("source-yellow");
            sourceGreen = root.Q<Label>("source-green");
            sourceCyan = root.Q<Label>("source-cyan");
            sourceBlue = root.Q<Label>("source-blue");
            sourceMagenta = root.Q<Label>("source-magenta");

            // Destination panel
            destinationDropdown = root.Q<DropdownField>("destination-dropdown");
            redAmount = root.Q<IntegerField>("red-amount");
            yellowAmount = root.Q<IntegerField>("yellow-amount");
            greenAmount = root.Q<IntegerField>("green-amount");
            cyanAmount = root.Q<IntegerField>("cyan-amount");
            blueAmount = root.Q<IntegerField>("blue-amount");
            magentaAmount = root.Q<IntegerField>("magenta-amount");
            totalCount = root.Q<Label>("total-count");

            // Wire up events
            closeButton?.RegisterCallback<ClickEvent>(evt => Hide());
            cancelButton?.RegisterCallback<ClickEvent>(evt => Hide());
            transferButton?.RegisterCallback<ClickEvent>(evt => OnTransferClicked());

            sourceDropdown?.RegisterValueChangedCallback(evt => OnSourceChanged(evt.newValue));
            destinationDropdown?.RegisterValueChangedCallback(evt => OnDestinationChanged(evt.newValue));

            // Wire up amount field changes to update total
            redAmount?.RegisterValueChangedCallback(evt => UpdateTotal());
            yellowAmount?.RegisterValueChangedCallback(evt => UpdateTotal());
            greenAmount?.RegisterValueChangedCallback(evt => UpdateTotal());
            cyanAmount?.RegisterValueChangedCallback(evt => UpdateTotal());
            blueAmount?.RegisterValueChangedCallback(evt => UpdateTotal());
            magentaAmount?.RegisterValueChangedCallback(evt => UpdateTotal());

            UnityEngine.Debug.Log("[TransferWindow] UI initialized with new two-panel design");
        }

        public void Toggle()
        {
            if (isVisible)
                Hide();
            else
                Show();
        }

        public void Show()
        {
            if (transferWindow == null) InitializeUI();

            transferWindow.RemoveFromClassList("hidden");
            isVisible = true;

            UnityEngine.Debug.Log("[TransferWindow] ========== TRANSFER WINDOW OPENED ==========");

            // Load available locations (inventory + storage devices)
            LoadLocations();

            // Reset amounts
            ResetAmounts();

            UnityEngine.Debug.Log("[TransferWindow] ========== DATA LOAD COMPLETE ==========");
        }

        public void Hide()
        {
            if (transferWindow == null) return;

            transferWindow.AddToClassList("hidden");
            isVisible = false;

            UnityEngine.Debug.Log("[TransferWindow] Window hidden");
        }

        private void LoadLocations()
        {
            try
            {
                UnityEngine.Debug.Log("[TransferWindow] LoadLocations START");
                locations.Clear();

                if (GameManager.Instance == null || !GameManager.IsConnected())
                {
                    UnityEngine.Debug.LogWarning("[TransferWindow] Not connected");
                    ShowError("Not connected to server");
                    return;
                }

                var conn = GameManager.Conn;
                ulong? playerIdNullable = GetCurrentPlayerId();
                if (!playerIdNullable.HasValue)
                {
                    UnityEngine.Debug.LogWarning("[TransferWindow] No player ID found");
                    ShowError("Player not found");
                    return;
                }
                ulong playerId = playerIdNullable.Value;

                // Add player inventory
                var inventoryLocation = new TransferLocation
                {
                    Type = LocationType.Inventory,
                    DeviceId = 0,
                    DisplayName = "My Inventory",
                    Composition = new Dictionary<float, uint>()
                };

                // Load inventory composition
                var inventory = conn.Db.PlayerInventory.PlayerId.Find(playerId);
                if (inventory != null)
                {
                    foreach (var sample in inventory.InventoryComposition)
                    {
                        inventoryLocation.Composition[sample.Frequency] = sample.Count;
                    }
                    UnityEngine.Debug.Log($"[TransferWindow] Loaded inventory: {inventory.TotalCount} packets");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[TransferWindow] No inventory found for player");
                }

                locations.Add(inventoryLocation);

                // Add storage devices
                foreach (var device in conn.Db.StorageDevice.Iter())
                {
                    if (device.OwnerPlayerId == playerId)
                    {
                        var deviceLocation = new TransferLocation
                        {
                            Type = LocationType.StorageDevice,
                            DeviceId = device.DeviceId,
                            DisplayName = string.IsNullOrEmpty(device.DeviceName)
                                ? $"Storage Device {device.DeviceId}"
                                : device.DeviceName,
                            Composition = new Dictionary<float, uint>()
                        };

                        foreach (var sample in device.StoredComposition)
                        {
                            deviceLocation.Composition[sample.Frequency] = sample.Count;
                        }

                        locations.Add(deviceLocation);
                        UnityEngine.Debug.Log($"[TransferWindow] Loaded storage device: {deviceLocation.DisplayName}");
                    }
                }

                // Populate dropdowns
                var choices = locations.Select(loc => loc.DisplayName).ToList();
                sourceDropdown.choices = choices;
                destinationDropdown.choices = choices;

                if (locations.Count > 0)
                {
                    sourceDropdown.index = 0;
                    selectedSource = locations[0];
                    UpdateSourceDisplay();

                    // Default destination to first storage device if available, otherwise inventory
                    if (locations.Count > 1)
                    {
                        destinationDropdown.index = 1;
                        selectedDestination = locations[1];
                    }
                    else
                    {
                        destinationDropdown.index = 0;
                        selectedDestination = locations[0];
                    }
                }

                UnityEngine.Debug.Log($"[TransferWindow] Loaded {locations.Count} locations");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[TransferWindow] Exception in LoadLocations: {ex.Message}\n{ex.StackTrace}");
                ShowError("Error loading locations");
            }
        }

        private void OnSourceChanged(string displayName)
        {
            selectedSource = locations.FirstOrDefault(loc => loc.DisplayName == displayName);
            if (selectedSource != null)
            {
                UpdateSourceDisplay();
                UnityEngine.Debug.Log($"[TransferWindow] Source changed to: {displayName}");
            }
        }

        private void OnDestinationChanged(string displayName)
        {
            selectedDestination = locations.FirstOrDefault(loc => loc.DisplayName == displayName);
            if (selectedDestination != null)
            {
                UnityEngine.Debug.Log($"[TransferWindow] Destination changed to: {displayName}");
            }
        }

        private void UpdateSourceDisplay()
        {
            if (selectedSource == null) return;

            sourceRed.text = $"Red: {GetCount(selectedSource, FREQ_RED)}";
            sourceYellow.text = $"Yellow: {GetCount(selectedSource, FREQ_YELLOW)}";
            sourceGreen.text = $"Green: {GetCount(selectedSource, FREQ_GREEN)}";
            sourceCyan.text = $"Cyan: {GetCount(selectedSource, FREQ_CYAN)}";
            sourceBlue.text = $"Blue: {GetCount(selectedSource, FREQ_BLUE)}";
            sourceMagenta.text = $"Magenta: {GetCount(selectedSource, FREQ_MAGENTA)}";
        }

        private uint GetCount(TransferLocation location, float frequency)
        {
            if (location == null || location.Composition == null) return 0;

            foreach (var kvp in location.Composition)
            {
                if (System.Math.Abs(kvp.Key - frequency) < 0.01f)
                {
                    return kvp.Value;
                }
            }
            return 0;
        }

        private void UpdateTotal()
        {
            int total = (redAmount?.value ?? 0) +
                       (yellowAmount?.value ?? 0) +
                       (greenAmount?.value ?? 0) +
                       (cyanAmount?.value ?? 0) +
                       (blueAmount?.value ?? 0) +
                       (magentaAmount?.value ?? 0);

            totalCount.text = $"{total}";

            // Validate amounts
            ValidateTransfer();
        }

        private void ValidateTransfer()
        {
            if (selectedSource == null || selectedDestination == null)
            {
                ShowError("Select source and destination");
                transferButton.SetEnabled(false);
                return;
            }

            // Check if source and destination are the same
            if (selectedSource.Type == selectedDestination.Type &&
                selectedSource.DeviceId == selectedDestination.DeviceId)
            {
                ShowError("Source and destination cannot be the same");
                transferButton.SetEnabled(false);
                return;
            }

            // Check if amounts exceed available
            if (!ValidateAmount(FREQ_RED, redAmount?.value ?? 0) ||
                !ValidateAmount(FREQ_YELLOW, yellowAmount?.value ?? 0) ||
                !ValidateAmount(FREQ_GREEN, greenAmount?.value ?? 0) ||
                !ValidateAmount(FREQ_CYAN, cyanAmount?.value ?? 0) ||
                !ValidateAmount(FREQ_BLUE, blueAmount?.value ?? 0) ||
                !ValidateAmount(FREQ_MAGENTA, magentaAmount?.value ?? 0))
            {
                ShowError("Transfer amount exceeds available packets");
                transferButton.SetEnabled(false);
                return;
            }

            // Check if at least 1 packet is being transferred
            int total = (redAmount?.value ?? 0) + (yellowAmount?.value ?? 0) +
                       (greenAmount?.value ?? 0) + (cyanAmount?.value ?? 0) +
                       (blueAmount?.value ?? 0) + (magentaAmount?.value ?? 0);

            if (total == 0)
            {
                ShowError("Select at least one packet to transfer");
                transferButton.SetEnabled(false);
                return;
            }

            // All validation passed
            HideError();
            transferButton.SetEnabled(true);
        }

        private bool ValidateAmount(float frequency, int amount)
        {
            if (amount < 0) return false;
            if (amount == 0) return true;
            return amount <= GetCount(selectedSource, frequency);
        }

        private void ShowError(string message)
        {
            validationMessage.text = message;
            validationMessage.RemoveFromClassList("hidden");
        }

        private void HideError()
        {
            validationMessage.AddToClassList("hidden");
        }

        private void ResetAmounts()
        {
            redAmount.value = 0;
            yellowAmount.value = 0;
            greenAmount.value = 0;
            cyanAmount.value = 0;
            blueAmount.value = 0;
            magentaAmount.value = 0;
            UpdateTotal();
        }

        private void OnTransferClicked()
        {
            if (selectedSource == null || selectedDestination == null)
            {
                ShowError("Select source and destination");
                return;
            }

            // Build composition from amounts
            var composition = new List<WavePacketSample>();

            void AddIfNonZero(float freq, int amount)
            {
                if (amount > 0)
                {
                    composition.Add(new WavePacketSample
                    {
                        Frequency = freq,
                        Amplitude = 1.0f,
                        Phase = 0.0f,
                        Count = (uint)amount
                    });
                }
            }

            AddIfNonZero(FREQ_RED, redAmount.value);
            AddIfNonZero(FREQ_YELLOW, yellowAmount.value);
            AddIfNonZero(FREQ_GREEN, greenAmount.value);
            AddIfNonZero(FREQ_CYAN, cyanAmount.value);
            AddIfNonZero(FREQ_BLUE, blueAmount.value);
            AddIfNonZero(FREQ_MAGENTA, magentaAmount.value);

            if (composition.Count == 0)
            {
                ShowError("No packets to transfer");
                return;
            }

            // Determine if this is inventory->storage or storage->storage
            if (selectedSource.Type == LocationType.Inventory &&
                selectedDestination.Type == LocationType.StorageDevice)
            {
                // Inventory to storage - use initiate_transfer reducer
                UnityEngine.Debug.Log($"[TransferWindow] Initiating transfer from inventory to device {selectedDestination.DeviceId}");
                GameManager.Conn.Reducers.InitiateTransfer(composition, selectedDestination.DeviceId);
            }
            else
            {
                // TODO: Implement storage->inventory and storage->storage transfers
                // These will need new reducers on the server side
                UnityEngine.Debug.LogWarning($"[TransferWindow] Transfer type not yet implemented: {selectedSource.Type} -> {selectedDestination.Type}");
                ShowError("This transfer type is not yet implemented");
                return;
            }

            // Close window after successful transfer initiation
            Hide();
        }

        private ulong? GetCurrentPlayerId()
        {
            try
            {
                UnityEngine.Debug.Log("[TransferWindow] GetCurrentPlayerId called");

                if (GameData.Instance == null)
                {
                    UnityEngine.Debug.LogWarning("[TransferWindow] GameData.Instance is null");
                    return null;
                }

                if (!GameData.Instance.PlayerIdentity.HasValue)
                {
                    UnityEngine.Debug.LogWarning("[TransferWindow] PlayerIdentity has no value");
                    return null;
                }

                UnityEngine.Debug.Log("[TransferWindow] About to get GameManager.Conn");
                var conn = GameManager.Conn;
                if (conn == null)
                {
                    UnityEngine.Debug.LogError("[TransferWindow] GameManager.Conn is null!");
                    return null;
                }
                UnityEngine.Debug.Log("[TransferWindow] ✓ GameManager.Conn is valid");

                UnityEngine.Debug.Log("[TransferWindow] About to access PlayerIdentity.Value");
                var identity = GameData.Instance.PlayerIdentity.Value;
                UnityEngine.Debug.Log($"[TransferWindow] ✓ Got identity: {identity}");

                UnityEngine.Debug.Log($"[TransferWindow] Searching for player with identity: {identity}");
                UnityEngine.Debug.Log("[TransferWindow] Starting Player.Iter() loop");

                int playerCount = 0;
                foreach (var player in conn.Db.Player.Iter())
                {
                    playerCount++;
                    UnityEngine.Debug.Log($"[TransferWindow] Checking player {playerCount}: Identity={player.Identity}");
                    if (player.Identity == identity)
                    {
                        UnityEngine.Debug.Log($"[TransferWindow] ✓ Found matching player: {player.PlayerId}");
                        return player.PlayerId;
                    }
                }

                UnityEngine.Debug.LogWarning($"[TransferWindow] No matching player found in database (checked {playerCount} players)");
                return null;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[TransferWindow] Exception in GetCurrentPlayerId: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }
    }
}
