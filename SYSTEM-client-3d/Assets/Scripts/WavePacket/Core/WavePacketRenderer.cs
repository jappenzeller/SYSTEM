using UnityEngine;
using SpacetimeDB.Types;
using System.Diagnostics;

namespace SYSTEM.WavePacket
{
    /// <summary>
    /// Universal wave packet renderer for all energy visualization
    /// Used by sources, extracted packets, transfers, and distribution packets
    /// </summary>
    public class WavePacketRenderer : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private WavePacketSettings settings;

                [Header("Rendering Mode")]
        [SerializeField] private WavePacketSettings.RenderMode renderMode = WavePacketSettings.RenderMode.GenerateMesh;
        [SerializeField] private GameObject targetPrefab;
        [SerializeField] private MeshFilter targetMeshFilter;
        [SerializeField] private MeshRenderer targetMeshRenderer;

        [Header("Display Mode")]
        [SerializeField] private WavePacketSettings.DisplayMode displayMode = WavePacketSettings.DisplayMode.Static;
        [SerializeField] private bool rotateVisual = false;


        [Header("Current Composition")]
        [SerializeField] private WavePacketSample[] currentComposition;

        private GameObject visualObject;
        private Material visualMaterial;
        private float animationProgress = 0f;
        private bool isAnimating = false;



        void Awake()
        {
            // Initialization happens via Initialize() call when settings are passed explicitly
            // OR if settings are already assigned in prefab
            if (settings != null)
            {
                Initialize(settings);
            }
            // Otherwise wait for explicit Initialize() call
        }

        /// <summary>
        /// Initialize the renderer with settings.
        /// Called explicitly by WavePacketSourceRenderer or other systems.
        /// </summary>
        public void Initialize(WavePacketSettings newSettings)
        {
            if (newSettings == null)
            {
                UnityEngine.Debug.LogError("[WavePacketRenderer] Initialize called with null settings!");
                return;
            }

            Stopwatch awakeTimer = Stopwatch.StartNew();

            this.settings = newSettings;

            // Read display configuration from settings
            displayMode = settings.displayMode;
            rotateVisual = settings.rotateVisual;
            renderMode = settings.renderMode;

            Stopwatch initTimer = Stopwatch.StartNew();
            InitializeVisual();
            initTimer.Stop();

            awakeTimer.Stop();
            UnityEngine.Debug.Log($"[WavePacketRenderer] Initialize: {awakeTimer.ElapsedMilliseconds}ms | InitializeVisual: {initTimer.ElapsedMilliseconds}ms");
        }


        void Update()
        {
            if (rotateVisual && visualObject != null)
            {
                visualObject.transform.Rotate(Vector3.up, settings.rotationSpeed * Time.deltaTime);
            }

            if (isAnimating && displayMode != WavePacketSettings.DisplayMode.Static)
            {
                UpdateAnimation();
            }
        }

        public void SetComposition(WavePacketSample[] composition)
        {
            currentComposition = composition;
            RefreshVisualization();
        }

        /// <summary>
        /// Set the alpha transparency of the wave packet visual.
        /// Used for state-based visual feedback (moving sources are more transparent).
        /// </summary>
        public void SetAlpha(float alpha)
        {
            if (visualMaterial != null)
            {
                // Update main color alpha
                if (visualMaterial.HasProperty("_Color"))
                {
                    Color c = visualMaterial.color;
                    c.a = alpha;
                    visualMaterial.color = c;
                }

                // Update base color alpha (URP)
                if (visualMaterial.HasProperty("_BaseColor"))
                {
                    Color c = visualMaterial.GetColor("_BaseColor");
                    c.a = alpha;
                    visualMaterial.SetColor("_BaseColor", c);
                }

                // Update alpha property for custom shaders
                if (visualMaterial.HasProperty("_Alpha"))
                {
                    visualMaterial.SetFloat("_Alpha", alpha);
                }

                // Enable/disable transparency mode based on alpha
                if (alpha < 1.0f)
                {
                    // Enable transparency
                    visualMaterial.SetFloat("_Surface", 1); // Transparent
                    visualMaterial.SetFloat("_Blend", 0); // Alpha blend
                    visualMaterial.renderQueue = 3000;
                }
                else
                {
                    // Opaque mode
                    visualMaterial.SetFloat("_Surface", 0); // Opaque
                    visualMaterial.renderQueue = 2000;
                }
            }
        }

        public void StartAnimation()
        {
            if (displayMode == WavePacketSettings.DisplayMode.Static) return;

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
                case WavePacketSettings.RenderMode.GenerateMesh:
                    CreateGeneratedMeshVisual();
                    break;
                case WavePacketSettings.RenderMode.UsePrefab:
                    CreatePrefabVisual();
                    break;
                case WavePacketSettings.RenderMode.UseExistingMesh:
                    // Use existing mesh filter
                    break;
            }

            CreateMaterial();
        }

        private void CreateGeneratedMeshVisual()
        {
            visualObject = new GameObject("WavePacketSourceRenderer");
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
            visualObject.name = "WavePacketSourceRenderer_Prefab";
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
            Stopwatch materialTimer = Stopwatch.StartNew();

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
                UnityEngine.Debug.Log($"[WavePacketRenderer] CreateMaterial: Using shader: {shader.name}");
                visualMaterial = new Material(shader);
                targetMeshRenderer.material = visualMaterial;
            }
            else
            {
                UnityEngine.Debug.LogError($"[WavePacketRenderer] CreateMaterial: SHADER NOT FOUND or renderer is null (shader={shader != null}, renderer={targetMeshRenderer != null})");
            }

            materialTimer.Stop();
            UnityEngine.Debug.Log($"[WavePacketRenderer] CreateMaterial: {materialTimer.ElapsedMilliseconds}ms");
        }

        private void RefreshVisualization()
        {
            if (currentComposition == null || currentComposition.Length == 0)
                return;

            Stopwatch refreshTimer = Stopwatch.StartNew();

            float progress = displayMode == WavePacketSettings.DisplayMode.Static ? 1.0f : animationProgress;
            Stopwatch meshGenTimer = Stopwatch.StartNew();
            Mesh mesh = WavePacketMeshGenerator.GenerateWavePacketMesh(currentComposition, settings, progress);
            meshGenTimer.Stop();

            Stopwatch meshAssignTimer = Stopwatch.StartNew();
            if (mesh != null && targetMeshFilter != null)
            {
                UnityEngine.Debug.Log($"[WavePacketRenderer] RefreshVisualization: Mesh created with {mesh.vertexCount} vertices, bounds: {mesh.bounds}");
                targetMeshFilter.mesh = mesh;
            }
            else
            {
                UnityEngine.Debug.LogError($"[WavePacketRenderer] RefreshVisualization: MESH NOT CREATED or MeshFilter null (mesh={mesh != null}, filter={targetMeshFilter != null})");
            }
            meshAssignTimer.Stop();

            refreshTimer.Stop();
            UnityEngine.Debug.Log($"[WavePacketRenderer] RefreshVisualization | Total: {refreshTimer.ElapsedMilliseconds}ms | MeshGen: {meshGenTimer.ElapsedMilliseconds}ms | MeshAssign: {meshAssignTimer.ElapsedMilliseconds}ms");
        }

        private void UpdateAnimation()
        {
            animationProgress += Time.deltaTime / settings.extractionDuration;

            if (displayMode == WavePacketSettings.DisplayMode.Extraction && animationProgress >= 1.0f)
            {
                isAnimating = false;
                animationProgress = 1.0f;
            }
            else if (displayMode == WavePacketSettings.DisplayMode.Animated)
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

        /// <summary>
        /// Create a flying packet - implemented by platform renderers
        /// </summary>
        public virtual GameObject CreateFlyingPacket(WavePacketSample[] samples, Vector3 startPosition, Vector3 targetPosition, float speed)
        {
            UnityEngine.Debug.LogWarning("[WavePacketRenderer] CreateFlyingPacket called on base class - should be overridden by platform renderer");
            return null;
        }

        /// <summary>
        /// Start extraction animation - implemented by platform renderers
        /// </summary>
        public virtual void StartExtraction(WavePacketSample[] samples, Vector3 orbPosition)
        {
            UnityEngine.Debug.LogWarning("[WavePacketRenderer] StartExtraction called on base class - should be overridden by platform renderer");
        }

        /// <summary>
        /// Update extraction progress - implemented by platform renderers
        /// </summary>
        public virtual void UpdateExtraction(float progress)
        {
            UnityEngine.Debug.LogWarning("[WavePacketRenderer] UpdateExtraction called on base class - should be overridden by platform renderer");
        }

        /// <summary>
        /// End extraction animation - implemented by platform renderers
        /// </summary>
        public virtual void EndExtraction()
        {
            UnityEngine.Debug.LogWarning("[WavePacketRenderer] EndExtraction called on base class - should be overridden by platform renderer");
        }
    }
    /// <summary>
    /// Factory for creating platform-appropriate renderer
    /// </summary>
    public static class WavePacketFactory
    {
        public static WavePacketRenderer CreateRenderer(GameObject parent)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return parent.AddComponent<WavePacketRenderer_WebGL>();
#else
            return parent.AddComponent<WavePacketRenderer_Native>();
#endif
        }
    }
}
