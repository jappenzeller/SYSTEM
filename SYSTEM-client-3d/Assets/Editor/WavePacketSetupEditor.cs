using UnityEngine;
using UnityEditor;
using SYSTEM.WavePacket;

namespace SYSTEM.EditorTools
{
    /// <summary>
    /// Editor utility to set up Wave Packet Visualization components in the scene
    /// </summary>
    public class WavePacketSetupEditor : EditorWindow
    {
        [MenuItem("SYSTEM/Wave Packet/Setup Scene Components")]
        static void SetupSceneComponents()
        {
            // Find or create MiningSystem GameObject
            GameObject miningSystemObj = GameObject.Find("MiningSystem");
            WavePacketMiningSystem miningSystem = null;

            if (miningSystemObj == null)
            {
                miningSystemObj = new GameObject("MiningSystem");
                var initializer = miningSystemObj.AddComponent<SYSTEM.Game.MiningSystemInitializer>();
                miningSystem = miningSystemObj.AddComponent<WavePacketMiningSystem>();
                UnityEngine.Debug.Log("[Setup] Created MiningSystem GameObject with WavePacketMiningSystem");
            }
            else
            {
                miningSystem = miningSystemObj.GetComponent<WavePacketMiningSystem>();
                if (miningSystem == null)
                {
                    miningSystem = miningSystemObj.AddComponent<WavePacketMiningSystem>();
                    UnityEngine.Debug.Log("[Setup] Added WavePacketMiningSystem to existing MiningSystem GameObject");
                }
                else
                {
                    UnityEngine.Debug.Log("[Setup] MiningSystem already exists");
                }
            }

            // Find or create ExtractionVisualController as child of MiningSystem
            ExtractionVisualController extractionController = miningSystemObj.GetComponentInChildren<ExtractionVisualController>();

            if (extractionController == null)
            {
                GameObject controllerObj = new GameObject("ExtractionVisualController");
                controllerObj.transform.SetParent(miningSystemObj.transform);
                extractionController = controllerObj.AddComponent<ExtractionVisualController>();
                UnityEngine.Debug.Log("[Setup] Created ExtractionVisualController as child of MiningSystem");
            }
            else
            {
                UnityEngine.Debug.Log("[Setup] ExtractionVisualController already exists");
            }

            // Link ExtractionVisualController to WavePacketMiningSystem
            if (miningSystem != null)
            {
                SerializedObject so = new SerializedObject(miningSystem);
                SerializedProperty extractionProp = so.FindProperty("extractionVisualController");

                if (extractionProp != null)
                {
                    extractionProp.objectReferenceValue = extractionController;
                    so.ApplyModifiedProperties();
                    UnityEngine.Debug.Log("[Setup] Linked ExtractionVisualController to WavePacketMiningSystem");
                }
            }

            // Expand the MiningSystem in hierarchy and select it
            EditorGUIUtility.PingObject(miningSystemObj);
            Selection.activeGameObject = miningSystemObj;

            UnityEngine.Debug.Log("[Setup] Wave Packet visualization setup complete! Check Inspector for settings.");
            UnityEngine.Debug.Log("[Setup] Hierarchy: MiningSystem > ExtractionVisualController");
        }

        [MenuItem("SYSTEM/Wave Packet/Create Test Scene")]
        static void CreateTestScene()
        {
            // Create a complete test setup
            GameObject testRoot = new GameObject("WavePacketTest");

            // Create ExtractionVisualController
            GameObject controllerObj = new GameObject("ExtractionVisualController");
            controllerObj.transform.SetParent(testRoot.transform);
            ExtractionVisualController controller = controllerObj.AddComponent<ExtractionVisualController>();

            // Create test script
            GameObject testObj = new GameObject("VisualizationTest");
            testObj.transform.SetParent(testRoot.transform);
            WavePacketVisualizationTest test = testObj.AddComponent<WavePacketVisualizationTest>();

            // Link them via SerializedObject
            SerializedObject testSO = new SerializedObject(test);
            SerializedProperty controllerProp = testSO.FindProperty("extractionController");
            if (controllerProp != null)
            {
                controllerProp.objectReferenceValue = controller;
                testSO.ApplyModifiedProperties();
            }

            // Add camera if none exists
            if (Camera.main == null)
            {
                GameObject cameraObj = new GameObject("Main Camera");
                cameraObj.tag = "MainCamera";
                Camera cam = cameraObj.AddComponent<Camera>();
                cam.transform.position = new Vector3(0, 5, -10);
                cam.transform.LookAt(Vector3.zero);
                cameraObj.transform.SetParent(testRoot.transform);
            }

            Selection.activeGameObject = testRoot;
            EditorGUIUtility.PingObject(testRoot);

            UnityEngine.Debug.Log("[Setup] Test scene created! Press Play and use keys 1-6, F, M, S to test visualizations.");
        }

        [MenuItem("SYSTEM/Wave Packet/Create Isolated Test Scene")]
        static void CreateIsolatedTestScene()
        {
            // Create test root GameObject
            GameObject testObj = new GameObject("WavePacketRendererTest");
            WavePacketRendererTestScene testScript = testObj.AddComponent<WavePacketRendererTestScene>();

            // Add camera if none exists
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraObj = new GameObject("Main Camera");
                cameraObj.tag = "MainCamera";
                mainCamera = cameraObj.AddComponent<Camera>();
                UnityEngine.Debug.Log("[Setup] Created Main Camera");
            }

            // Position camera to view test area
            mainCamera.transform.position = new Vector3(0, 2, -8);
            mainCamera.transform.LookAt(Vector3.zero);

            // Add directional light if none exists
            Light directionalLight = UnityEngine.Object.FindFirstObjectByType<Light>();
            if (directionalLight == null || directionalLight.type != LightType.Directional)
            {
                GameObject lightObj = new GameObject("Directional Light");
                directionalLight = lightObj.AddComponent<Light>();
                directionalLight.type = LightType.Directional;
                directionalLight.transform.rotation = Quaternion.Euler(50, -30, 0);
                UnityEngine.Debug.Log("[Setup] Created Directional Light");
            }

            Selection.activeGameObject = testObj;
            EditorGUIUtility.PingObject(testObj);

            UnityEngine.Debug.Log("[Setup] Isolated test scene created!");
            UnityEngine.Debug.Log("[Setup] Press Play to auto-run tests, or use the on-screen buttons to test manually.");
            UnityEngine.Debug.Log("[Setup] Configure test settings in the Inspector before running.");
        }

        [MenuItem("SYSTEM/Wave Packet/Create Extraction Material")]
        static void CreateExtractionMaterial()
        {
            // Create material with the WavePacketDisc shader
            Material mat = new Material(Shader.Find("SYSTEM/WavePacketDisc"));
            mat.name = "ExtractionDiscMaterial";
            mat.SetFloat("_EmissionStrength", 0.8f);
            mat.SetFloat("_Alpha", 1.0f);

            // Save to Assets/Materials
            string path = "Assets/Materials";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }

            string assetPath = AssetDatabase.GenerateUniqueAssetPath(path + "/ExtractionDiscMaterial.mat");
            AssetDatabase.CreateAsset(mat, assetPath);
            AssetDatabase.SaveAssets();

            EditorGUIUtility.PingObject(mat);
            Selection.activeObject = mat;

            UnityEngine.Debug.Log($"[Setup] Created ExtractionDiscMaterial at {assetPath}");
        }
    }
}
