using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using SpacetimeDB.Types;
using SYSTEM.Game;
using TMPro;

namespace SYSTEM.UI
{
    /// <summary>
    /// DEPRECATED: Use CrystalConfigWindow instead (UI Toolkit version).
    /// This old Canvas/Slider-based window has been replaced.
    /// File kept for reference only - do not use in new scenes.
    /// </summary>
    [System.Obsolete("Use CrystalConfigWindow instead")]
    public class CrystalMiningWindow_DEPRECATED : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject windowPanel;
        [SerializeField] private Slider redSlider;
        [SerializeField] private Slider greenSlider;
        [SerializeField] private Slider blueSlider;
        [SerializeField] private TextMeshProUGUI redCountText;
        [SerializeField] private TextMeshProUGUI greenCountText;
        [SerializeField] private TextMeshProUGUI blueCountText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button mineButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI mineButtonText;

        [Header("Settings")]
        [SerializeField] private int maxCrystalsPerType = 5;

        private int redCount = 1;
        private int greenCount = 0;
        private int blueCount = 0;
        private MiningManager miningSystem;
        private SYSTEM.Debug.CursorController cursorController;

        void Start()
        {
            // Find required systems
            miningSystem = UnityEngine.Object.FindFirstObjectByType<MiningManager>();
            cursorController = UnityEngine.Object.FindFirstObjectByType<SYSTEM.Debug.CursorController>();

            // Setup sliders
            if (redSlider != null)
            {
                redSlider.minValue = 0;
                redSlider.maxValue = maxCrystalsPerType;
                redSlider.value = redCount;
                redSlider.onValueChanged.AddListener(OnRedChanged);
            }

            if (greenSlider != null)
            {
                greenSlider.minValue = 0;
                greenSlider.maxValue = maxCrystalsPerType;
                greenSlider.value = greenCount;
                greenSlider.onValueChanged.AddListener(OnGreenChanged);
            }

            if (blueSlider != null)
            {
                blueSlider.minValue = 0;
                blueSlider.maxValue = maxCrystalsPerType;
                blueSlider.value = blueCount;
                blueSlider.onValueChanged.AddListener(OnBlueChanged);
            }

            // Setup buttons
            if (mineButton != null)
                mineButton.onClick.AddListener(ToggleMining);

            if (closeButton != null)
                closeButton.onClick.AddListener(CloseWindow);

            // Start hidden
            if (windowPanel != null)
                windowPanel.SetActive(false);

            UpdateDisplay();
        }

        void Update()
        {
            // Toggle mining with M key
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            {
                ToggleMining();
            }

            // Toggle with C key (new Input System)
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            {
                UnityEngine.Debug.Log("[CrystalMiningWindow] C key pressed!");

                if (windowPanel != null)
                {
                    bool newState = !windowPanel.activeSelf;
                    windowPanel.SetActive(newState);
                    UnityEngine.Debug.Log($"[CrystalMiningWindow] Window toggled to: {newState}");

                    if (newState)
                    {
                        UpdateDisplay();
                        UpdateMiningButton();

                        // Unlock cursor when opening window
                        if (cursorController != null)
                        {
                            cursorController.ForceUnlock();
                            UnityEngine.Debug.Log("[CrystalMiningWindow] Cursor unlocked");
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning("[CrystalMiningWindow] CursorController not found!");
                        }
                    }
                }
                else
                {
                    UnityEngine.Debug.LogError("[CrystalMiningWindow] windowPanel is NULL! Assign it in Inspector.");
                }
            }

            // Close with Escape (new Input System)
            if (windowPanel != null && windowPanel.activeSelf &&
                Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseWindow();
            }

            // Update mining button state if window is visible
            if (windowPanel != null && windowPanel.activeSelf)
            {
                UpdateMiningButton();
            }
        }

        void OnRedChanged(float value)
        {
            redCount = Mathf.RoundToInt(value);
            UpdateDisplay();
        }

        void OnGreenChanged(float value)
        {
            greenCount = Mathf.RoundToInt(value);
            UpdateDisplay();
        }

        void OnBlueChanged(float value)
        {
            blueCount = Mathf.RoundToInt(value);
            UpdateDisplay();
        }

        void UpdateDisplay()
        {
            if (redCountText != null)
                redCountText.text = $"Red: {redCount}";

            if (greenCountText != null)
                greenCountText.text = $"Green: {greenCount}";

            if (blueCountText != null)
                blueCountText.text = $"Blue: {blueCount}";

            int total = redCount + greenCount + blueCount;
            bool hasAnyCrystal = total > 0;

            if (mineButton != null)
                mineButton.interactable = hasAnyCrystal;

            if (statusText != null)
            {
                if (hasAnyCrystal)
                {
                    string composition = "";
                    if (redCount > 0) composition += $"{redCount}R ";
                    if (greenCount > 0) composition += $"{greenCount}G ";
                    if (blueCount > 0) composition += $"{blueCount}B";

                    int efficiency = Mathf.Min(total * 10, 100);
                    statusText.text = $"{composition.Trim()}\nTotal: {total} | Efficiency: {efficiency}%";
                }
                else
                {
                    statusText.text = "Select at least 1 crystal";
                }
            }
        }

        void UpdateMiningButton()
        {
            if (miningSystem == null || mineButtonText == null)
                return;

            if (miningSystem.IsMining)
            {
                mineButtonText.text = "Stop Mining";
            }
            else
            {
                mineButtonText.text = "Start Mining";
            }
        }

        void ToggleMining()
        {
            if (miningSystem == null)
            {
                UnityEngine.Debug.LogError("[CrystalMiningWindow] MiningManager not found!");
                if (statusText != null)
                    statusText.text = "Error: Mining system not found";
                return;
            }

            // If already mining, stop it
            if (miningSystem.IsMining)
            {
                miningSystem.StopMining();
                UnityEngine.Debug.Log("[CrystalMiningWindow] Stopped mining");
                if (statusText != null)
                    statusText.text = "Mining stopped";
                UpdateMiningButton();
                return;
            }

            // Build composition
            var composition = new List<WavePacketSample>();

            if (redCount > 0)
            {
                composition.Add(new WavePacketSample
                {
                    Frequency = 0.0f,      // Red
                    Amplitude = 1.0f,
                    Phase = 0.0f,
                    Count = (uint)redCount
                });
            }

            if (greenCount > 0)
            {
                composition.Add(new WavePacketSample
                {
                    Frequency = 2.094f,    // Green
                    Amplitude = 1.0f,
                    Phase = 0.0f,
                    Count = (uint)greenCount
                });
            }

            if (blueCount > 0)
            {
                composition.Add(new WavePacketSample
                {
                    Frequency = 4.189f,    // Blue
                    Amplitude = 1.0f,
                    Phase = 0.0f,
                    Count = (uint)blueCount
                });
            }

            // The mining system will find the nearest source internally
            // We just need to make sure we're near one and pass the composition

            // Start mining with composition
            // Note: StartMiningWithComposition needs the source, so we need to access the private method
            // For now, just call StartMining which will use our composition

            // Actually, let's get the current target or find nearest source
            var conn = GameManager.Conn;
            if (conn == null)
            {
                UnityEngine.Debug.LogError("[CrystalMiningWindow] No database connection!");
                if (statusText != null)
                    statusText.text = "Error: Not connected";
                return;
            }

            // Find nearest source by checking the database
            WavePacketSource nearestSource = null;
            float nearestDistance = float.MaxValue;

            // Get local player's GameObject (not the database Player object)
            var playerController = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (playerController == null)
            {
                UnityEngine.Debug.LogError("[CrystalMiningWindow] No local player controller!");
                if (statusText != null)
                    statusText.text = "Error: No player";
                return;
            }

            var playerTransform = playerController.transform;

            foreach (var source in conn.Db.WavePacketSource.Iter())
            {
                var orbObj = UnityEngine.GameObject.Find($"WavePacketSource_{source.SourceId}");
                if (orbObj != null)
                {
                    float distance = UnityEngine.Vector3.Distance(playerTransform.position, orbObj.transform.position);
                    if (distance < nearestDistance && distance <= 30f)
                    {
                        nearestDistance = distance;
                        nearestSource = source;
                    }
                }
            }

            if (nearestSource == null)
            {
                UnityEngine.Debug.LogWarning("[CrystalMiningWindow] No source in range!");
                if (statusText != null)
                    statusText.text = "No source in range (max 30 units)!";
                return;
            }

            // Start mining with composition
            miningSystem.StartMiningWithComposition(nearestSource, composition);

            UnityEngine.Debug.Log($"[CrystalMiningWindow] Started mining with R:{redCount} G:{greenCount} B:{blueCount}");

            if (statusText != null)
                statusText.text = "Mining started!";

            // Update button text
            UpdateMiningButton();

            // Close window
            CloseWindow();
        }

        void CloseWindow()
        {
            if (windowPanel != null)
                windowPanel.SetActive(false);

            // Lock cursor when closing window
            if (cursorController != null)
            {
                cursorController.ForceLock();
            }
        }

        /// <summary>
        /// Public method for testing - set specific crystal counts
        /// </summary>
        public void SetCrystalCounts(int red, int green, int blue)
        {
            redCount = Mathf.Clamp(red, 0, maxCrystalsPerType);
            greenCount = Mathf.Clamp(green, 0, maxCrystalsPerType);
            blueCount = Mathf.Clamp(blue, 0, maxCrystalsPerType);

            if (redSlider != null) redSlider.value = redCount;
            if (greenSlider != null) greenSlider.value = greenCount;
            if (blueSlider != null) blueSlider.value = blueCount;

            UpdateDisplay();
        }
    }
}
