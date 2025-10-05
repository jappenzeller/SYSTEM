using UnityEngine;
using UnityEditor;

namespace SYSTEM.WavePacket.Editor
{
    public static class WavePacketMenuItems
    {
        [MenuItem("SYSTEM/Wave Packet/Create Default Settings")]
        public static void CreateDefaultSettings()
        {
            var settings = ScriptableObject.CreateInstance<WavePacketSettings>();
            
            string path = "Assets/Settings/WavePacketSettings_Default.asset";
            string directory = System.IO.Path.GetDirectoryName(path);
            
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }
            
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = settings;
            
            UnityEngine.Debug.Log($"Created default Wave Packet Settings at {path}");
        }

        [MenuItem("SYSTEM/Wave Packet/Create Test Scene GameObject")]
        public static void CreateTestSceneGameObject()
        {
            GameObject testObj = new GameObject("WavePacketTest");
            testObj.AddComponent<WavePacketTestController>();
            
            Selection.activeGameObject = testObj;
            
            UnityEngine.Debug.Log("Created Wave Packet Test GameObject. Assign settings in Inspector.");
        }

        [MenuItem("SYSTEM/Wave Packet/Create Display")]
        public static void CreateVisualizer()
        {
            GameObject visualizerObj = new GameObject("WavePacketDisplay");
            visualizerObj.AddComponent<WavePacketDisplay>();
            
            Selection.activeGameObject = visualizerObj;
            
            UnityEngine.Debug.Log("Created Wave Packet Visualizer. Configure settings in Inspector.");
        }
    }
}
