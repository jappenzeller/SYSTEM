using UnityEngine;
using SpacetimeDB.Types;
using SYSTEM.Debug;

namespace SYSTEM.WavePacket
{
    /// <summary>
    /// Test controller for new refactored wave packet system
    /// Drop on empty GameObject and configure settings
    /// </summary>
    public class WavePacketTestController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private WavePacketSettings settings;

        [Header("Test Configuration")]
        [SerializeField] private bool autoStart = true;
        [SerializeField] private float testDelay = 3f;

        [Header("Test Selections")]
        [SerializeField] private bool testIndividualColors = true;
        [SerializeField] private bool testFullSpectrum = true;
        [SerializeField] private bool testMixedComposition = true;

        private WavePacketRenderer visualizer;
        private int currentTestIndex = 0;

        void Start()
        {
            // Create settings if not assigned
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<WavePacketSettings>();
                SystemDebug.LogWarning(SystemDebug.Category.WavePacketSystem, "No settings assigned, using default");
            }

            // Create visualizer
            GameObject visualizerObj = new GameObject("WavePacketRenderer");
            visualizerObj.transform.SetParent(transform);
            visualizerObj.transform.localPosition = Vector3.zero;

            visualizer = visualizerObj.AddComponent<WavePacketRenderer>();

            // Assign settings via reflection or create a public setter
            var settingsField = typeof(WavePacketRenderer).GetField("settings", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (settingsField != null)
            {
                settingsField.SetValue(visualizer, settings);
            }

            if (autoStart)
            {
                InvokeRepeating(nameof(RunNextTest), 1f, testDelay);
            }
        }

        void RunNextTest()
        {
            if (currentTestIndex < 6 && testIndividualColors)
            {
                TestSingleColor(currentTestIndex);
            }
            else if (currentTestIndex == 6 && testFullSpectrum)
            {
                TestFullSpectrum();
            }
            else if (currentTestIndex == 7 && testMixedComposition)
            {
                TestMixedComposition();
            }
            else
            {
                CancelInvoke(nameof(RunNextTest));
                SystemDebug.Log(SystemDebug.Category.WavePacketSystem, "[Test] All tests complete");
                return;
            }

            currentTestIndex++;
        }

        void TestSingleColor(int colorIndex)
        {
            string[] colorNames = { "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta" };
            float[] frequencies = { 0.0f, 1.047f, 2.094f, 3.142f, 4.189f, 5.236f };

            SystemDebug.Log(SystemDebug.Category.WavePacketSystem, $"[Test] Testing {colorNames[colorIndex]}");

            var sample = new WavePacketSample
            {
                Frequency = frequencies[colorIndex],
                Amplitude = 1.0f,
                Phase = 0.0f,
                Count = 20
            };

            visualizer.SetComposition(new WavePacketSample[] { sample });
        }

        void TestFullSpectrum()
        {
            SystemDebug.Log(SystemDebug.Category.WavePacketSystem, "[Test] Testing Full Spectrum");

            var samples = new WavePacketSample[]
            {
                new WavePacketSample { Frequency = 0.0f, Amplitude = 1.0f, Phase = 0.0f, Count = 20 },
                new WavePacketSample { Frequency = 1.047f, Amplitude = 1.0f, Phase = 0.0f, Count = 20 },
                new WavePacketSample { Frequency = 2.094f, Amplitude = 1.0f, Phase = 0.0f, Count = 20 },
                new WavePacketSample { Frequency = 3.142f, Amplitude = 1.0f, Phase = 0.0f, Count = 20 },
                new WavePacketSample { Frequency = 4.189f, Amplitude = 1.0f, Phase = 0.0f, Count = 20 },
                new WavePacketSample { Frequency = 5.236f, Amplitude = 1.0f, Phase = 0.0f, Count = 20 }
            };

            visualizer.SetComposition(samples);
        }

        void TestMixedComposition()
        {
            SystemDebug.Log(SystemDebug.Category.WavePacketSystem, "[Test] Testing Mixed Composition");

            var samples = new WavePacketSample[]
            {
                new WavePacketSample { Frequency = 0.0f, Amplitude = 1.0f, Phase = 0.0f, Count = 30 },
                new WavePacketSample { Frequency = 2.094f, Amplitude = 1.0f, Phase = 0.0f, Count = 20 },
                new WavePacketSample { Frequency = 4.189f, Amplitude = 1.0f, Phase = 0.0f, Count = 10 }
            };

            visualizer.SetComposition(samples);
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 500));
            GUILayout.Label("Wave Packet Test Controller");
            GUILayout.Label($"Settings: {(settings != null ? settings.name : "Default")}");
            GUILayout.Label("");

            string[] colorNames = { "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta" };
            GUILayout.Label("Individual Colors:");
            for (int i = 0; i < 6; i++)
            {
                int index = i;
                if (GUILayout.Button($"Test {colorNames[i]}"))
                {
                    TestSingleColor(index);
                }
            }

            GUILayout.Label("");
            if (GUILayout.Button("Test Full Spectrum"))
            {
                TestFullSpectrum();
            }

            if (GUILayout.Button("Test Mixed Composition"))
            {
                TestMixedComposition();
            }

            GUILayout.EndArea();
        }
    }
}
