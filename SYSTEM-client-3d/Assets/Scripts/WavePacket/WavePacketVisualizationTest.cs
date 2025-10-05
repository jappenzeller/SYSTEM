using UnityEngine;
using SpacetimeDB.Types;

namespace SYSTEM.WavePacket
{
    /// <summary>
    /// Test script for wave packet visualization system
    /// Press keys to trigger different visual tests
    /// </summary>
    public class WavePacketVisualizationTest : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ExtractionVisualController extractionController;

        [Header("Test Settings")]
        [SerializeField] private Vector3 testPosition = new Vector3(0, 5, 0);
        [SerializeField] private bool autoTest = false;

        private bool testRunning = false;

        void Start()
        {
            if (extractionController == null)
            {
                GameObject controllerObj = new GameObject("ExtractionVisualController");
                extractionController = controllerObj.AddComponent<ExtractionVisualController>();
            }

            if (autoTest)
            {
                Invoke(nameof(TestFullSpectrum), 1f);
            }
        }

        void Update()
        {
            // Press 1-6 for single color tests
            if (Input.GetKeyDown(KeyCode.Alpha1)) TestSingleColor(0.0f, "Red");
            if (Input.GetKeyDown(KeyCode.Alpha2)) TestSingleColor(1.047f, "Yellow");
            if (Input.GetKeyDown(KeyCode.Alpha3)) TestSingleColor(2.094f, "Green");
            if (Input.GetKeyDown(KeyCode.Alpha4)) TestSingleColor(3.142f, "Cyan");
            if (Input.GetKeyDown(KeyCode.Alpha5)) TestSingleColor(4.189f, "Blue");
            if (Input.GetKeyDown(KeyCode.Alpha6)) TestSingleColor(5.236f, "Magenta");

            // Press F for full spectrum test
            if (Input.GetKeyDown(KeyCode.F)) TestFullSpectrum();

            // Press M for mixed (RGB) test
            if (Input.GetKeyDown(KeyCode.M)) TestMixedRGB();

            // Press S to stop extraction
            if (Input.GetKeyDown(KeyCode.S)) StopTest();
        }

        void TestSingleColor(float frequency, string colorName)
        {
            UnityEngine.Debug.Log($"[Test] Testing {colorName} extraction");

            WavePacketSample[] samples = new WavePacketSample[]
            {
                new WavePacketSample { Frequency = frequency, Amplitude = 1.0f, Phase = 0.0f, Count = 20 }
            };

            extractionController.StartExtraction(1, samples, testPosition);
            testRunning = true;
        }

        void TestFullSpectrum()
        {
            UnityEngine.Debug.Log("[Test] Testing full spectrum extraction");

            WavePacketSample[] samples = new WavePacketSample[]
            {
                new WavePacketSample { Frequency = 0.0f, Amplitude = 1.0f, Phase = 0.0f, Count = 10 },    // Red
                new WavePacketSample { Frequency = 1.047f, Amplitude = 1.0f, Phase = 0.0f, Count = 10 },  // Yellow
                new WavePacketSample { Frequency = 2.094f, Amplitude = 1.0f, Phase = 0.0f, Count = 10 },  // Green
                new WavePacketSample { Frequency = 3.142f, Amplitude = 1.0f, Phase = 0.0f, Count = 10 },  // Cyan
                new WavePacketSample { Frequency = 4.189f, Amplitude = 1.0f, Phase = 0.0f, Count = 10 },  // Blue
                new WavePacketSample { Frequency = 5.236f, Amplitude = 1.0f, Phase = 0.0f, Count = 10 }   // Magenta
            };

            extractionController.StartExtraction(1, samples, testPosition);
            testRunning = true;
        }

        void TestMixedRGB()
        {
            UnityEngine.Debug.Log("[Test] Testing mixed RGB extraction");

            WavePacketSample[] samples = new WavePacketSample[]
            {
                new WavePacketSample { Frequency = 0.0f, Amplitude = 1.0f, Phase = 0.0f, Count = 10 },    // Red
                new WavePacketSample { Frequency = 2.094f, Amplitude = 1.0f, Phase = 0.0f, Count = 20 },  // Green
                new WavePacketSample { Frequency = 4.189f, Amplitude = 1.0f, Phase = 0.0f, Count = 30 }   // Blue
            };

            extractionController.StartExtraction(1, samples, testPosition);
            testRunning = true;
        }

        void StopTest()
        {
            UnityEngine.Debug.Log("[Test] Stopping extraction");
            extractionController.StopExtraction(1);
            testRunning = false;
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 300));
            GUILayout.Label("Wave Packet Visualization Test");
            GUILayout.Label("Press 1-6: Single color test");
            GUILayout.Label("Press F: Full spectrum");
            GUILayout.Label("Press M: Mixed RGB");
            GUILayout.Label("Press S: Stop");
            GUILayout.Label("");
            GUILayout.Label($"Test Running: {testRunning}");
            GUILayout.EndArea();
        }
    }
}
