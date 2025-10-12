using UnityEngine;
using SpacetimeDB.Types;
using System.Collections.Generic;

namespace SYSTEM.WavePacket
{
    /// <summary>
    /// Standalone test for wave packet waveRenderer - no game dependencies
    /// Drop this on an empty GameObject in a test scene
    /// </summary>
    public class WavePacketRendererTestScene : MonoBehaviour
    {
        [Header("Test Configuration")]
        [SerializeField] private bool autoStart = true;
        [SerializeField] private float testDelay = 1f;
        [SerializeField] private bool pauseAtHalfway = true;

        [Header("Test Samples")]
        [SerializeField] private bool testIndividualColors = true;
        [SerializeField] private bool testFullSpectrum = true;
        [SerializeField] private bool testMixedRGB = true;
        [SerializeField] private bool testFlyingPacket = false;

        [Header("Positions")]
        [SerializeField] private Vector3 extractionPosition = Vector3.zero;
        [SerializeField] private Vector3 flyingPacketStart = new Vector3(-5, 0, 0);
        [SerializeField] private Vector3 flyingPacketTarget = new Vector3(5, 0, 0);

        private WavePacketRenderer waveRenderer;
        private int testIndex = 0;

        void Start()
        {
            // Create waveRenderer
            GameObject rendererObj = new GameObject("WavePacketRenderer_Test");
            rendererObj.transform.SetParent(transform);
            waveRenderer = WavePacketFactory.CreateRenderer(rendererObj);

            UnityEngine.Debug.Log($"[Test] Created {waveRenderer.GetType().Name}");

            if (autoStart)
            {
                InvokeRepeating(nameof(RunNextTest), testDelay, testDelay * 3f);
            }
        }

        void RunNextTest()
        {
            // Stop any current extraction
            if (waveRenderer != null)
            {
                waveRenderer.EndExtraction();
            }

            // Run tests in sequence
            if (testIndividualColors && testIndex >= 0 && testIndex < 6)
            {
                TestIndividualColor(testIndex);
            }
            else if (testIndex == 6 && testFullSpectrum)
            {
                TestFullSpectrum();
            }
            else if (testIndex == 7 && testMixedRGB)
            {
                TestMixedRGB();
            }
            else if (testIndex == 8 && testFlyingPacket)
            {
                TestFlyingPacket();
            }
            else
            {
                CancelInvoke(nameof(RunNextTest));
                UnityEngine.Debug.Log("[Test] All tests complete");
                return;
            }

            testIndex++;
        }

        void TestIndividualColor(int colorIndex)
        {
            string[] colorNames = { "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta" };
            float[] frequencies = { 0.0f, 1.047f, 2.094f, 3.142f, 4.189f, 5.236f };

            UnityEngine.Debug.Log($"[Test] === Testing Single {colorNames[colorIndex]} Extraction ===");

            WavePacketSample[] samples = new WavePacketSample[]
            {
                new WavePacketSample { Frequency = frequencies[colorIndex], Amplitude = 1.0f, Phase = 0.0f, Count = 20 }
            };

            waveRenderer.StartExtraction(samples, extractionPosition);

            // Pause halfway if enabled
            if (pauseAtHalfway)
            {
                Invoke(nameof(PauseExtraction), 1f);
            }
        }

        void PauseExtraction()
        {
            if (waveRenderer != null)
            {
                waveRenderer.UpdateExtraction(0.5f); // Freeze at 50%
                UnityEngine.Debug.Log("[Test] Extraction paused at 50% - examine mesh and material");
            }
        }

        void TestFullSpectrum()
        {
            UnityEngine.Debug.Log("[Test] === Testing Full Spectrum Extraction ===");

            WavePacketSample[] samples = new WavePacketSample[]
            {
                new WavePacketSample { Frequency = 0.0f, Amplitude = 1.0f, Phase = 0.0f, Count = 20 },    // Red
                new WavePacketSample { Frequency = 1.047f, Amplitude = 1.0f, Phase = 0.0f, Count = 20 },  // Yellow
                new WavePacketSample { Frequency = 2.094f, Amplitude = 1.0f, Phase = 0.0f, Count = 20 },  // Green
                new WavePacketSample { Frequency = 3.142f, Amplitude = 1.0f, Phase = 0.0f, Count = 20 },  // Cyan
                new WavePacketSample { Frequency = 4.189f, Amplitude = 1.0f, Phase = 0.0f, Count = 20 },  // Blue
                new WavePacketSample { Frequency = 5.236f, Amplitude = 1.0f, Phase = 0.0f, Count = 20 }   // Magenta
            };

            waveRenderer.StartExtraction(samples, extractionPosition);
        }

        void TestMixedRGB()
        {
            UnityEngine.Debug.Log("[Test] === Testing Mixed RGB Extraction ===");

            WavePacketSample[] samples = new WavePacketSample[]
            {
                new WavePacketSample { Frequency = 0.0f, Amplitude = 1.0f, Phase = 0.0f, Count = 10 },    // Red
                new WavePacketSample { Frequency = 2.094f, Amplitude = 1.0f, Phase = 0.0f, Count = 20 },  // Green
                new WavePacketSample { Frequency = 4.189f, Amplitude = 1.0f, Phase = 0.0f, Count = 30 }   // Blue
            };

            waveRenderer.StartExtraction(samples, extractionPosition);
        }

        void TestFlyingPacket()
        {
            UnityEngine.Debug.Log("[Test] === Testing Flying Packet ===");

            WavePacketSample[] samples = new WavePacketSample[]
            {
                new WavePacketSample { Frequency = 2.094f, Amplitude = 1.0f, Phase = 0.0f, Count = 10 }  // Green
            };

            GameObject packet = waveRenderer.CreateFlyingPacket(samples, flyingPacketStart, flyingPacketTarget, 5f);

            if (packet != null)
            {
                UnityEngine.Debug.Log($"[Test] Created flying packet: {packet.name}");
            }
            else
            {
                UnityEngine.Debug.LogError("[Test] Failed to create flying packet!");
            }
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 400));
            GUILayout.Label("Wave Packet Renderer Test Scene");
            GUILayout.Label($"Renderer: {waveRenderer?.GetType().Name ?? "None"}");
            GUILayout.Label("");
            GUILayout.Label("Manual Tests - Individual Colors:");

            string[] colorNames = { "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta" };
            for (int i = 0; i < 6; i++)
            {
                int index = i; // Capture for lambda
                if (GUILayout.Button($"Test {colorNames[i]}"))
                {
                    TestIndividualColor(index);
                }
            }

            GUILayout.Label("");
            GUILayout.Label("Other Tests:");

            if (GUILayout.Button("Test Full Spectrum"))
            {
                TestFullSpectrum();
            }

            if (GUILayout.Button("Test Mixed RGB"))
            {
                TestMixedRGB();
            }

            if (GUILayout.Button("Stop Extraction"))
            {
                waveRenderer?.EndExtraction();
            }

            GUILayout.EndArea();
        }
    }
}
