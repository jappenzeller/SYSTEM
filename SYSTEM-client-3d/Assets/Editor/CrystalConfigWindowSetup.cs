using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using SYSTEM.UI;
using SYSTEM.Game;

namespace SYSTEM.Editor
{
    /// <summary>
    /// Editor utility to set up CrystalConfigWindow in the current scene.
    /// Menu: SYSTEM > UI > Setup Crystal Config Window
    /// </summary>
    public static class CrystalConfigWindowSetup
    {
        [MenuItem("SYSTEM/UI/Setup Crystal Config Window")]
        public static void SetupCrystalConfigWindow()
        {
            // Check if already exists
            var existing = Object.FindFirstObjectByType<CrystalConfigWindow>();
            if (existing != null)
            {
                EditorUtility.DisplayDialog("Already Exists",
                    "CrystalConfigWindow already exists in the scene.\n\nSelect it in hierarchy?", "OK");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Find the UXML asset
            string[] uuidGuids = AssetDatabase.FindAssets("CrystalConfigWindow t:VisualTreeAsset");
            if (uuidGuids.Length == 0)
            {
                EditorUtility.DisplayDialog("Error",
                    "Could not find CrystalConfigWindow.uxml in project.\n\nMake sure Assets/UI/CrystalConfigWindow.uxml exists.", "OK");
                return;
            }

            string uxmlPath = AssetDatabase.GUIDToAssetPath(uuidGuids[0]);
            var uxmlAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);

            // Find or create panel settings
            PanelSettings panelSettings = FindOrCreatePanelSettings();

            // Create the GameObject
            GameObject go = new GameObject("CrystalConfigUI");

            // Add UIDocument
            var uiDoc = go.AddComponent<UIDocument>();
            uiDoc.visualTreeAsset = uxmlAsset;
            uiDoc.panelSettings = panelSettings;

            // Add the controller
            go.AddComponent<CrystalConfigWindow>();

            // Register undo
            Undo.RegisterCreatedObjectUndo(go, "Create CrystalConfigUI");

            // Select it
            Selection.activeGameObject = go;

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            UnityEngine.Debug.Log("[CrystalConfigWindowSetup] Created CrystalConfigUI GameObject. Don't forget to disable/remove the old Canvas-based CrystalMiningWindow!");
        }

        [MenuItem("SYSTEM/UI/Disable Old CrystalMiningWindow")]
        public static void DisableOldWindow()
        {
            // Find old Canvas-based window by name
            var oldWindow = GameObject.Find("CrystalMiningWindow");
            if (oldWindow != null)
            {
                Undo.RecordObject(oldWindow, "Disable CrystalMiningWindow");
                oldWindow.SetActive(false);
                UnityEngine.Debug.Log("[CrystalConfigWindowSetup] Disabled old CrystalMiningWindow GameObject");
                EditorUtility.DisplayDialog("Done", "Old CrystalMiningWindow has been disabled.\n\nYou can delete it later if the new UI works correctly.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Not Found", "Could not find 'CrystalMiningWindow' GameObject in scene.", "OK");
            }
        }

        private static PanelSettings FindOrCreatePanelSettings()
        {
            // Try to find existing panel settings from TransferWindowUI
            var transferWindow = Object.FindFirstObjectByType<TransferWindow>();
            if (transferWindow != null)
            {
                var doc = transferWindow.GetComponent<UIDocument>();
                if (doc != null && doc.panelSettings != null)
                {
                    return doc.panelSettings;
                }
            }

            // Search for any existing panel settings in project
            string[] panelGuids = AssetDatabase.FindAssets("t:PanelSettings");
            if (panelGuids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(panelGuids[0]);
                return AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            }

            // Create new panel settings
            PanelSettings newSettings = ScriptableObject.CreateInstance<PanelSettings>();
            newSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            newSettings.referenceResolution = new Vector2Int(1920, 1080);

            string settingsPath = "Assets/UI/SharedPanelSettings.asset";
            AssetDatabase.CreateAsset(newSettings, settingsPath);
            AssetDatabase.SaveAssets();

            UnityEngine.Debug.Log($"[CrystalConfigWindowSetup] Created new PanelSettings at {settingsPath}");
            return newSettings;
        }
    }
}
