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
    /// Minimal window for testing crystal composition mining
    /// Press C to toggle, use sliders to set R/G/B counts, click Mine
    /// </summary>
    public class CrystalMiningWindow : MonoBehaviour
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

        [Header("Settings")]
        [SerializeField] private int maxCrystalsPerType = 5;

        private int redCount = 1;
        private int greenCount = 0;
        private int blueCount = 0;

        void Start()
        {
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
                mineButton.onClick.AddListener(OnMineClicked);

            if (closeButton != null)
                closeButton.onClick.AddListener(CloseWindow);

            // Start hidden
            if (windowPanel != null)
                windowPanel.SetActive(false);

            UpdateDisplay();
        }

        void Update()
        {
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

        void OnMineClicked()
        {
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

            // Find mining system and start mining
            var miningSystem = UnityEngine.Object.FindFirstObjectByType<WavePacketMiningSystem>();
            if (miningSystem == null)
            {
                UnityEngine.Debug.LogError("[CrystalMiningWindow] WavePacketMiningSystem not found!");
                if (statusText != null)
                    statusText.text = "Error: Mining system not found";
                return;
            }

            // The mining system will find the nearest orb internally
            // We just need to make sure we're near one and pass the composition

            // Start mining with composition
            // Note: StartMiningWithComposition needs the orb, so we need to access the private method
            // For now, just call StartMining which will use our composition

            // Actually, let's get the current target or find nearest
            var conn = GameManager.Conn;
            if (conn == null)
            {
                UnityEngine.Debug.LogError("[CrystalMiningWindow] No database connection!");
                if (statusText != null)
                    statusText.text = "Error: Not connected";
                return;
            }

            // Find nearest orb by checking the database
            WavePacketOrb nearestOrb = null;
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

            foreach (var orb in conn.Db.WavePacketOrb.Iter())
            {
                var orbObj = UnityEngine.GameObject.Find($"Orb_{orb.OrbId}");
                if (orbObj != null)
                {
                    float distance = UnityEngine.Vector3.Distance(playerTransform.position, orbObj.transform.position);
                    if (distance < nearestDistance && distance <= 30f)
                    {
                        nearestDistance = distance;
                        nearestOrb = orb;
                    }
                }
            }

            if (nearestOrb == null)
            {
                UnityEngine.Debug.LogWarning("[CrystalMiningWindow] No orb in range!");
                if (statusText != null)
                    statusText.text = "No orb in range (max 30 units)!";
                return;
            }

            // Start mining with composition
            miningSystem.StartMiningWithComposition(nearestOrb, composition);

            UnityEngine.Debug.Log($"[CrystalMiningWindow] Started mining with R:{redCount} G:{greenCount} B:{blueCount}");

            if (statusText != null)
                statusText.text = "Mining started!";

            // Close window
            CloseWindow();
        }

        void CloseWindow()
        {
            if (windowPanel != null)
                windowPanel.SetActive(false);
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
