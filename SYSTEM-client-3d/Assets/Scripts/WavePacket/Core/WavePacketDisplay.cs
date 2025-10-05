using UnityEngine;
using SpacetimeDB.Types;

namespace SYSTEM.WavePacket
{
    /// <summary>
    /// Component-based wave packet visualizer
    /// Can render to prefabs, custom meshes, or generate meshes dynamically
    /// </summary>
    public class WavePacketDisplay : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private WavePacketSettings settings;

        [Header("Rendering Mode")]
        [SerializeField] private RenderMode renderMode = RenderMode.GenerateMesh;
        [SerializeField] private GameObject targetPrefab;
        [SerializeField] private MeshFilter targetMeshFilter;
        [SerializeField] private MeshRenderer targetMeshRenderer;

        [Header("Display Mode")]
        [SerializeField] private DisplayMode displayMode = DisplayMode.Static;
        [SerializeField] private bool rotateVisual = true;

        [Header("Current Composition")]
        [SerializeField] private WavePacketSample[] currentComposition;

        private GameObject visualObject;
        private Material visualMaterial;
        private float animationProgress = 0f;
        private bool isAnimating = false;

        public enum RenderMode
        {
            GenerateMesh,      // Create mesh dynamically
            UsePrefab,         // Instantiate and modify a prefab
            UseExistingMesh    // Use provided MeshFilter
        }

        public enum DisplayMode
        {
            Static,            // Show full wave packet statically
            Animated,          // Animate expansion/contraction
            Extraction         // Play extraction animation once
        }

        void Awake()
        {
            if (settings == null)
            {
                UnityEngine.Debug.LogWarning("No WavePacketSettings assigned! Creating default.");
                settings = ScriptableObject.CreateInstance<WavePacketSettings>();
            }

            InitializeVisual();
        }

        void Update()
        {
            if (rotateVisual && visualObject != null)
            {
                visualObject.transform.Rotate(Vector3.up, settings.rotationSpeed * Time.deltaTime);
            }

            if (isAnimating && displayMode != DisplayMode.Static)
            {
                UpdateAnimation();
            }
        }

        public void SetComposition(WavePacketSample[] composition)
        {
            currentComposition = composition;
            RefreshVisualization();
        }

        public void StartAnimation()
        {
            if (displayMode == DisplayMode.Static) return;

            isAnimating = true;
            animationProgress = 0f;
        }

        public void StopAnimation()
        {
            isAnimating = false;
        }

        private void InitializeVisual()
        {
            switch (renderMode)
            {
                case RenderMode.GenerateMesh:
                    CreateGeneratedMeshVisual();
                    break;
                case RenderMode.UsePrefab:
                    CreatePrefabVisual();
                    break;
                case RenderMode.UseExistingMesh:
                    // Use existing mesh filter
                    break;
            }

            CreateMaterial();
        }

        private void CreateGeneratedMeshVisual()
        {
            visualObject = new GameObject("WavePacketVisual");
            visualObject.transform.SetParent(transform);
            visualObject.transform.localPosition = Vector3.zero;
            visualObject.transform.localRotation = Quaternion.identity;
            visualObject.transform.localScale = Vector3.one;

            targetMeshFilter = visualObject.AddComponent<MeshFilter>();
            targetMeshRenderer = visualObject.AddComponent<MeshRenderer>();
        }

        private void CreatePrefabVisual()
        {
            if (targetPrefab == null)
            {
                UnityEngine.Debug.LogError("Target prefab is null!");
                CreateGeneratedMeshVisual();
                return;
            }

            visualObject = Instantiate(targetPrefab, transform);
            visualObject.name = "WavePacketVisual_Prefab";
            visualObject.transform.localPosition = Vector3.zero;

            targetMeshFilter = visualObject.GetComponent<MeshFilter>();
            targetMeshRenderer = visualObject.GetComponent<MeshRenderer>();

            if (targetMeshFilter == null || targetMeshRenderer == null)
            {
                UnityEngine.Debug.LogError("Prefab must have MeshFilter and MeshRenderer!");
            }
        }

        private void CreateMaterial()
        {
            Shader shader = settings.customShader;
            if (shader == null)
            {
                shader = Shader.Find("SYSTEM/WavePacketDisc");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                }
            }

            if (shader != null && targetMeshRenderer != null)
            {
                visualMaterial = new Material(shader);
                targetMeshRenderer.material = visualMaterial;
            }
        }

        private void RefreshVisualization()
        {
            if (currentComposition == null || currentComposition.Length == 0)
                return;

            float progress = displayMode == DisplayMode.Static ? 1.0f : animationProgress;
            Mesh mesh = WavePacketMeshGenerator.GenerateWavePacketMesh(currentComposition, settings, progress);

            if (mesh != null && targetMeshFilter != null)
            {
                targetMeshFilter.mesh = mesh;
            }
        }

        private void UpdateAnimation()
        {
            animationProgress += Time.deltaTime / settings.extractionDuration;

            if (displayMode == DisplayMode.Extraction && animationProgress >= 1.0f)
            {
                isAnimating = false;
                animationProgress = 1.0f;
            }
            else if (displayMode == DisplayMode.Animated)
            {
                animationProgress = Mathf.PingPong(Time.time / settings.extractionDuration, 1.0f);
            }

            RefreshVisualization();
        }

        void OnValidate()
        {
            if (Application.isPlaying && currentComposition != null)
            {
                RefreshVisualization();
            }
        }
    }
}
