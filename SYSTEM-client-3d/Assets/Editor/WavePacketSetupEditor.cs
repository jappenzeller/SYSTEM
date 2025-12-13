using UnityEngine;
using UnityEditor;

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
            // Find or create MiningManager GameObject
            GameObject miningManagerObj = GameObject.Find("MiningManager");
            if (miningManagerObj == null)
            {
                // Try legacy name
                miningManagerObj = GameObject.Find("MiningSystem");
            }
            MiningManager miningManager = null;

            if (miningManagerObj == null)
            {
                miningManagerObj = new GameObject("MiningManager");
                miningManager = miningManagerObj.AddComponent<MiningManager>();
                UnityEngine.Debug.Log("[Setup] Created MiningManager GameObject");
            }
            else
            {
                miningManager = miningManagerObj.GetComponent<MiningManager>();
                if (miningManager == null)
                {
                    miningManager = miningManagerObj.AddComponent<MiningManager>();
                    UnityEngine.Debug.Log("[Setup] Added MiningManager to existing GameObject");
                }
                else
                {
                    UnityEngine.Debug.Log("[Setup] MiningManager already exists");
                }
            }

            // Expand the MiningManager in hierarchy and select it
            EditorGUIUtility.PingObject(miningManagerObj);
            Selection.activeGameObject = miningManagerObj;

            UnityEngine.Debug.Log("[Setup] Wave Packet visualization setup complete! Check Inspector for settings.");
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
