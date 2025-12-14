using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using SpacetimeDB.Types;
using SYSTEM.Game;

namespace SYSTEM.UI
{
    /// <summary>
    /// Crystal configuration window using UI Toolkit.
    /// Configure mining crystals (R/G/B only, max 3 per color, max 3 total).
    /// Press C to toggle, M to start mining (handled by MiningManager).
    /// </summary>
    public class CrystalConfigWindow : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Settings")]
        [SerializeField] private float configCooldownDuration = 30f;

        // UI Elements
        private VisualElement root;
        private VisualElement windowElement;
        private Button closeButton;
        private SliderInt redSlider;
        private SliderInt greenSlider;
        private SliderInt blueSlider;
        private Label redCount;
        private Label greenCount;
        private Label blueCount;
        private Label totalCount;
        private Label statusMessage;
        private Button quantumOptimizeButton;
        private Button skipButton;
        private Label cooldownMessage;

        // State
        private bool isWindowVisible = false;
        private float lastConfigApplyTime = -30f;
        private Coroutine cooldownCoroutine;
        private SYSTEM.Debug.CursorController cursorController;

        // Crystal counts (current config)
        private int redValue = 0;
        private int greenValue = 0;
        private int blueValue = 0;

        // Public accessors for MiningManager
        public int RedCount => redValue;
        public int GreenCount => greenValue;
        public int BlueCount => blueValue;
        public bool HasValidConfig => (redValue + greenValue + blueValue) > 0;

        private bool IsCooldownActive => Time.time - lastConfigApplyTime < configCooldownDuration;
        private float CooldownRemaining => Mathf.Max(0, configCooldownDuration - (Time.time - lastConfigApplyTime));

        void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }
        }

        void OnEnable()
        {
            cursorController = Object.FindFirstObjectByType<SYSTEM.Debug.CursorController>();

            if (uiDocument == null)
            {
                UnityEngine.Debug.LogError("[CrystalConfigWindow] UIDocument not assigned!");
                return;
            }

            root = uiDocument.rootVisualElement;
            windowElement = root.Q<VisualElement>("crystal-config-window");

            if (windowElement == null)
            {
                UnityEngine.Debug.LogError("[CrystalConfigWindow] Could not find crystal-config-window element!");
                return;
            }

            // Get UI elements
            closeButton = root.Q<Button>("close-button");
            redSlider = root.Q<SliderInt>("red-slider");
            greenSlider = root.Q<SliderInt>("green-slider");
            blueSlider = root.Q<SliderInt>("blue-slider");
            redCount = root.Q<Label>("red-count");
            greenCount = root.Q<Label>("green-count");
            blueCount = root.Q<Label>("blue-count");
            totalCount = root.Q<Label>("total-count");
            statusMessage = root.Q<Label>("status-message");
            quantumOptimizeButton = root.Q<Button>("quantum-optimize-button");
            skipButton = root.Q<Button>("skip-button");
            cooldownMessage = root.Q<Label>("cooldown-message");

            // Setup event handlers
            if (closeButton != null)
                closeButton.clicked += CloseWindow;

            if (redSlider != null)
                redSlider.RegisterValueChangedCallback(OnRedSliderChanged);

            if (greenSlider != null)
                greenSlider.RegisterValueChangedCallback(OnGreenSliderChanged);

            if (blueSlider != null)
                blueSlider.RegisterValueChangedCallback(OnBlueSliderChanged);

            if (quantumOptimizeButton != null)
                quantumOptimizeButton.clicked += OnQuantumOptimizeClicked;

            if (skipButton != null)
                skipButton.clicked += OnApplyConfigClicked;

            // Start hidden
            windowElement.AddToClassList("hidden");
            isWindowVisible = false;

            UpdateDisplay();
        }

        void OnDisable()
        {
            // Unsubscribe events
            if (closeButton != null)
                closeButton.clicked -= CloseWindow;

            if (redSlider != null)
                redSlider.UnregisterValueChangedCallback(OnRedSliderChanged);

            if (greenSlider != null)
                greenSlider.UnregisterValueChangedCallback(OnGreenSliderChanged);

            if (blueSlider != null)
                blueSlider.UnregisterValueChangedCallback(OnBlueSliderChanged);

            if (quantumOptimizeButton != null)
                quantumOptimizeButton.clicked -= OnQuantumOptimizeClicked;

            if (skipButton != null)
                skipButton.clicked -= OnApplyConfigClicked;
        }

        void Update()
        {
            // Toggle with C key
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            {
                ToggleWindow();
            }

            // Close with Escape
            if (isWindowVisible && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseWindow();
            }
        }

        private void ToggleWindow()
        {
            if (isWindowVisible)
            {
                CloseWindow();
            }
            else
            {
                OpenWindow();
            }
        }

        private void OpenWindow()
        {
            if (windowElement == null) return;

            windowElement.RemoveFromClassList("hidden");
            isWindowVisible = true;

            // Unlock cursor for UI interaction
            if (cursorController != null)
            {
                cursorController.ForceUnlock();
            }

            UpdateDisplay();
            UnityEngine.Debug.Log("[CrystalConfigWindow] Window opened");
        }

        private void CloseWindow()
        {
            if (windowElement == null) return;

            windowElement.AddToClassList("hidden");
            isWindowVisible = false;

            // Lock cursor for gameplay
            if (cursorController != null)
            {
                cursorController.ForceLock();
            }

            UnityEngine.Debug.Log("[CrystalConfigWindow] Window closed");
        }

        private void OnRedSliderChanged(ChangeEvent<int> evt)
        {
            if (IsCooldownActive)
            {
                redSlider.SetValueWithoutNotify(redValue);
                return;
            }

            int newTotal = evt.newValue + greenValue + blueValue;
            if (newTotal > 3)
            {
                redSlider.SetValueWithoutNotify(evt.previousValue);
                return;
            }

            redValue = evt.newValue;
            UpdateDisplay();
        }

        private void OnGreenSliderChanged(ChangeEvent<int> evt)
        {
            if (IsCooldownActive)
            {
                greenSlider.SetValueWithoutNotify(greenValue);
                return;
            }

            int newTotal = redValue + evt.newValue + blueValue;
            if (newTotal > 3)
            {
                greenSlider.SetValueWithoutNotify(evt.previousValue);
                return;
            }

            greenValue = evt.newValue;
            UpdateDisplay();
        }

        private void OnBlueSliderChanged(ChangeEvent<int> evt)
        {
            if (IsCooldownActive)
            {
                blueSlider.SetValueWithoutNotify(blueValue);
                return;
            }

            int newTotal = redValue + greenValue + evt.newValue;
            if (newTotal > 3)
            {
                blueSlider.SetValueWithoutNotify(evt.previousValue);
                return;
            }

            blueValue = evt.newValue;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            int total = redValue + greenValue + blueValue;

            if (redCount != null) redCount.text = redValue.ToString();
            if (greenCount != null) greenCount.text = greenValue.ToString();
            if (blueCount != null) blueCount.text = blueValue.ToString();
            if (totalCount != null) totalCount.text = $"{total} / 3";

            // Update status message based on config
            if (statusMessage != null)
            {
                if (total == 0)
                {
                    statusMessage.text = "Select at least 1 crystal to mine";
                }
                else
                {
                    statusMessage.text = "Press M to start mining when near a source";
                }
            }

            // Enable/disable apply button based on cooldown
            if (skipButton != null)
            {
                skipButton.SetEnabled(!IsCooldownActive);
            }
        }

        private void OnQuantumOptimizeClicked()
        {
            UnityEngine.Debug.Log("[CrystalConfigWindow] Quantum Optimize clicked - Coming soon!");
            if (statusMessage != null)
            {
                statusMessage.text = "Quantum Optimize minigame coming soon!";
            }
        }

        private void OnApplyConfigClicked()
        {
            if (IsCooldownActive)
            {
                return;
            }

            int total = redValue + greenValue + blueValue;
            if (total == 0)
            {
                if (statusMessage != null)
                {
                    statusMessage.text = "Select at least 1 crystal first!";
                }
                return;
            }

            // Apply config and start cooldown
            lastConfigApplyTime = Time.time;

            if (cooldownCoroutine != null)
            {
                StopCoroutine(cooldownCoroutine);
            }
            cooldownCoroutine = StartCoroutine(CooldownCoroutine());

            UnityEngine.Debug.Log($"[CrystalConfigWindow] Config applied: R={redValue} G={greenValue} B={blueValue}");

            if (statusMessage != null)
            {
                statusMessage.text = "Config saved! Press M near a source to mine.";
            }
        }

        private IEnumerator CooldownCoroutine()
        {
            if (cooldownMessage != null)
            {
                cooldownMessage.RemoveFromClassList("hidden");
            }

            if (skipButton != null)
            {
                skipButton.SetEnabled(false);
            }

            // Disable sliders during cooldown
            SetSlidersEnabled(false);

            while (CooldownRemaining > 0)
            {
                if (cooldownMessage != null)
                {
                    int secondsLeft = Mathf.CeilToInt(CooldownRemaining);
                    cooldownMessage.text = $"Config locked. Wait {secondsLeft}s...";
                }
                yield return new WaitForSeconds(0.5f);
            }

            // Cooldown complete
            if (cooldownMessage != null)
            {
                cooldownMessage.AddToClassList("hidden");
            }

            if (skipButton != null)
            {
                skipButton.SetEnabled(true);
            }

            SetSlidersEnabled(true);

            if (statusMessage != null)
            {
                statusMessage.text = "Config unlocked! Adjust crystals as needed.";
            }

            cooldownCoroutine = null;
        }

        private void SetSlidersEnabled(bool enabled)
        {
            if (redSlider != null) redSlider.SetEnabled(enabled);
            if (greenSlider != null) greenSlider.SetEnabled(enabled);
            if (blueSlider != null) blueSlider.SetEnabled(enabled);
        }

        /// <summary>
        /// Build composition for mining based on current config.
        /// Called by MiningManager when M key is pressed.
        /// </summary>
        public List<WavePacketSample> GetMiningComposition()
        {
            var composition = new List<WavePacketSample>();

            if (redValue > 0)
            {
                composition.Add(new WavePacketSample
                {
                    Frequency = 0.0f,  // Red
                    Amplitude = 1.0f,
                    Phase = 0.0f,
                    Count = (uint)redValue
                });
            }

            if (greenValue > 0)
            {
                composition.Add(new WavePacketSample
                {
                    Frequency = 2.094f,  // Green (2π/3)
                    Amplitude = 1.0f,
                    Phase = 0.0f,
                    Count = (uint)greenValue
                });
            }

            if (blueValue > 0)
            {
                composition.Add(new WavePacketSample
                {
                    Frequency = 4.189f,  // Blue (4π/3)
                    Amplitude = 1.0f,
                    Phase = 0.0f,
                    Count = (uint)blueValue
                });
            }

            return composition;
        }

        /// <summary>
        /// Check if config window is visible.
        /// </summary>
        public bool IsVisible => isWindowVisible;
    }
}
