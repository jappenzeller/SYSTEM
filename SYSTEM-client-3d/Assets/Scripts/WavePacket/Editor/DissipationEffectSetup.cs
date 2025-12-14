using UnityEngine;
using UnityEditor;
using SYSTEM.WavePacket.Effects;

namespace SYSTEM.WavePacket.Editor
{
    /// <summary>
    /// Editor utility to create the DissipationEffect prefab with proper particle system settings.
    /// Menu: SYSTEM > Effects > Create Dissipation Effect Prefab
    /// </summary>
    public static class DissipationEffectSetup
    {
        [MenuItem("SYSTEM/Effects/Create Dissipation Effect Prefab")]
        public static void CreateDissipationEffectPrefab()
        {
            // Create the GameObject
            GameObject effectObj = new GameObject("DissipationEffect");

            // Add ParticleSystem
            ParticleSystem ps = effectObj.AddComponent<ParticleSystem>();

            // Configure Main module
            var main = ps.main;
            main.duration = 1.0f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startColor = Color.white; // Will be set at runtime
            main.gravityModifier = -0.2f; // Slight upward drift
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.maxParticles = 50;

            // Configure Emission module - burst
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 15, 25) // Burst of 15-25 particles at time 0
            });

            // Configure Shape module - sphere
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
            shape.radiusThickness = 1f; // Emit from entire volume

            // Configure Color over Lifetime - fade out
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            // Configure Size over Lifetime - shrink slightly
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1f);
            sizeCurve.AddKey(1f, 0.3f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Configure Renderer module
            var renderer = effectObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            // Try to find default particle material
            Material particleMaterial = FindParticleMaterial();
            if (particleMaterial != null)
            {
                renderer.material = particleMaterial;
            }

            // Add DissipationEffect component
            effectObj.AddComponent<DissipationEffect>();

            // Create Effects folder if it doesn't exist
            string folderPath = "Assets/Prefabs/Effects";
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs", "Effects");
            }

            // Save as prefab
            string prefabPath = $"{folderPath}/DissipationEffect.prefab";

            // Check if prefab already exists
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                if (!EditorUtility.DisplayDialog("Overwrite?",
                    $"DissipationEffect prefab already exists at {prefabPath}.\nOverwrite it?", "Yes", "No"))
                {
                    Object.DestroyImmediate(effectObj);
                    return;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(effectObj, prefabPath);
            Object.DestroyImmediate(effectObj);

            // Select the created prefab
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            UnityEngine.Debug.Log($"[DissipationEffectSetup] Created DissipationEffect prefab at {prefabPath}");
            UnityEngine.Debug.Log("[DissipationEffectSetup] Assign this prefab to WavePacketSourceManager's 'Dissipation Effect Prefab' field.");
        }

        private static Material FindParticleMaterial()
        {
            // Try to find existing particle materials
            string[] guids = AssetDatabase.FindAssets("t:Material Default-Particle");
            if (guids.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            // Try built-in particle material
            Material builtIn = Resources.Load<Material>("Default-Particle");
            if (builtIn != null) return builtIn;

            // Try to find any particle material
            guids = AssetDatabase.FindAssets("Particle t:Material");
            foreach (string guid in guids)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat != null && mat.shader != null && mat.shader.name.Contains("Particle"))
                {
                    return mat;
                }
            }

            UnityEngine.Debug.LogWarning("[DissipationEffectSetup] Could not find particle material. Please assign one manually.");
            return null;
        }
    }
}
