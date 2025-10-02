using UnityEngine;
using UnityEditor;
using System.IO;

namespace SYSTEM.Editor
{
    /// <summary>
    /// Procedurally generates grid textures for the wave packet distortion effect
    /// </summary>
    public class GridTextureGenerator : EditorWindow
    {
        // Grid parameters
        private int textureResolution = 512;
        private int gridCells = 16;
        private float lineWidth = 2f;
        private Color backgroundColor = new Color(0, 0, 0, 0); // Transparent
        private Color lineColor = new Color(0.2f, 0.3f, 0.4f, 0.5f); // Matches shader default
        private bool addGlow = true;
        private float glowIntensity = 0.3f;
        private bool generateDots = false;
        private float dotSize = 3f;

        [MenuItem("SYSTEM/Tools/Grid Texture Generator")]
        public static void ShowWindow()
        {
            GetWindow<GridTextureGenerator>("Grid Texture Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Wave Packet Grid Texture Generator", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            // Resolution settings
            GUILayout.Label("Texture Settings", EditorStyles.boldLabel);
            textureResolution = EditorGUILayout.IntSlider("Resolution", textureResolution, 128, 2048);

            EditorGUILayout.Space();

            // Grid settings
            GUILayout.Label("Grid Properties", EditorStyles.boldLabel);
            gridCells = EditorGUILayout.IntSlider("Grid Cells", gridCells, 4, 64);
            lineWidth = EditorGUILayout.Slider("Line Width (pixels)", lineWidth, 0.5f, 5f);

            EditorGUILayout.Space();

            // Color settings
            GUILayout.Label("Colors", EditorStyles.boldLabel);
            backgroundColor = EditorGUILayout.ColorField("Background Color", backgroundColor);
            lineColor = EditorGUILayout.ColorField("Line Color", lineColor);

            EditorGUILayout.Space();

            // Effects
            GUILayout.Label("Effects", EditorStyles.boldLabel);
            addGlow = EditorGUILayout.Toggle("Add Glow Effect", addGlow);
            if (addGlow)
            {
                glowIntensity = EditorGUILayout.Slider("Glow Intensity", glowIntensity, 0.1f, 1f);
            }

            generateDots = EditorGUILayout.Toggle("Add Intersection Dots", generateDots);
            if (generateDots)
            {
                dotSize = EditorGUILayout.Slider("Dot Size", dotSize, 1f, 10f);
            }

            EditorGUILayout.Space();

            // Generate buttons
            if (GUILayout.Button("Generate Grid Texture", GUILayout.Height(30)))
            {
                GenerateGridTexture();
            }

            if (GUILayout.Button("Generate Multiple Variations", GUILayout.Height(30)))
            {
                GenerateVariations();
            }
        }

        public void GenerateGridTexture(string suffix = "")
        {
            Texture2D gridTexture = new Texture2D(textureResolution, textureResolution, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[textureResolution * textureResolution];

            // Fill background
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = backgroundColor;
            }

            // Calculate cell size
            float cellSize = (float)textureResolution / gridCells;

            // Draw grid lines with anti-aliasing
            for (int i = 0; i <= gridCells; i++)
            {
                float linePosition = i * cellSize;

                // Draw vertical lines
                DrawLine(pixels, linePosition, true, cellSize);

                // Draw horizontal lines
                DrawLine(pixels, linePosition, false, cellSize);
            }

            // Add glow effect if enabled
            if (addGlow)
            {
                ApplyGlowEffect(pixels);
            }

            // Add intersection dots if enabled
            if (generateDots)
            {
                DrawIntersectionDots(pixels, cellSize);
            }

            gridTexture.SetPixels(pixels);
            gridTexture.Apply();

            SaveTexture(gridTexture, suffix);
        }

        private void DrawLine(Color[] pixels, float position, bool vertical, float cellSize)
        {
            int intPos = Mathf.RoundToInt(position);

            for (int j = 0; j < textureResolution; j++)
            {
                // Anti-aliased line drawing
                for (float w = -lineWidth/2f; w <= lineWidth/2f; w += 0.5f)
                {
                    int linePixel = Mathf.RoundToInt(intPos + w);
                    if (linePixel < 0 || linePixel >= textureResolution) continue;

                    // Calculate alpha based on distance from center of line
                    float alpha = 1f - Mathf.Abs(w) / (lineWidth/2f) * 0.5f;
                    Color pixelColor = lineColor;
                    pixelColor.a *= alpha;

                    int index;
                    if (vertical)
                    {
                        index = j * textureResolution + linePixel;
                    }
                    else
                    {
                        index = linePixel * textureResolution + j;
                    }

                    if (index >= 0 && index < pixels.Length)
                    {
                        // Blend with existing pixel
                        pixels[index] = BlendColors(pixels[index], pixelColor);
                    }
                }
            }
        }

        private void ApplyGlowEffect(Color[] pixels)
        {
            Color[] glowPixels = new Color[pixels.Length];
            System.Array.Copy(pixels, glowPixels, pixels.Length);

            // Simple glow by blurring bright pixels
            int blurRadius = Mathf.RoundToInt(lineWidth * 2);

            for (int y = 0; y < textureResolution; y++)
            {
                for (int x = 0; x < textureResolution; x++)
                {
                    int index = y * textureResolution + x;
                    Color originalColor = pixels[index];

                    if (originalColor.a > 0.01f) // If this is a line pixel
                    {
                        // Add glow to surrounding pixels
                        for (int dy = -blurRadius; dy <= blurRadius; dy++)
                        {
                            for (int dx = -blurRadius; dx <= blurRadius; dx++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;

                                if (nx >= 0 && nx < textureResolution && ny >= 0 && ny < textureResolution)
                                {
                                    int glowIndex = ny * textureResolution + nx;
                                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                                    float falloff = 1f - (distance / blurRadius);

                                    if (falloff > 0)
                                    {
                                        Color glowColor = lineColor;
                                        glowColor.a *= falloff * glowIntensity * originalColor.a;
                                        glowPixels[glowIndex] = BlendColors(glowPixels[glowIndex], glowColor);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Copy glow pixels back
            System.Array.Copy(glowPixels, pixels, pixels.Length);
        }

        private void DrawIntersectionDots(Color[] pixels, float cellSize)
        {
            Color dotColor = lineColor;
            dotColor.a = Mathf.Min(1f, dotColor.a * 1.5f); // Make dots slightly brighter

            for (int gridY = 0; gridY <= gridCells; gridY++)
            {
                for (int gridX = 0; gridX <= gridCells; gridX++)
                {
                    int centerX = Mathf.RoundToInt(gridX * cellSize);
                    int centerY = Mathf.RoundToInt(gridY * cellSize);

                    // Draw circular dot
                    for (int dy = -(int)dotSize; dy <= (int)dotSize; dy++)
                    {
                        for (int dx = -(int)dotSize; dx <= (int)dotSize; dx++)
                        {
                            int x = centerX + dx;
                            int y = centerY + dy;

                            if (x >= 0 && x < textureResolution && y >= 0 && y < textureResolution)
                            {
                                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                                if (distance <= dotSize)
                                {
                                    float alpha = 1f - (distance / dotSize);
                                    Color pixelDotColor = dotColor;
                                    pixelDotColor.a *= alpha;

                                    int index = y * textureResolution + x;
                                    pixels[index] = BlendColors(pixels[index], pixelDotColor);
                                }
                            }
                        }
                    }
                }
            }
        }

        private Color BlendColors(Color bottom, Color top)
        {
            // Alpha blending
            float alpha = top.a;
            float invAlpha = 1f - alpha;

            return new Color(
                bottom.r * invAlpha + top.r * alpha,
                bottom.g * invAlpha + top.g * alpha,
                bottom.b * invAlpha + top.b * alpha,
                Mathf.Min(1f, bottom.a + top.a)
            );
        }

        private void SaveTexture(Texture2D texture, string suffix = "")
        {
            // Ensure Textures directory exists
            string texturesPath = "Assets/Textures";
            if (!AssetDatabase.IsValidFolder(texturesPath))
            {
                AssetDatabase.CreateFolder("Assets", "Textures");
            }

            // Generate filename
            string filename = $"WavePacketGrid{suffix}_{gridCells}x{gridCells}_{textureResolution}.png";
            string fullPath = Path.Combine(Application.dataPath, "Textures", filename);

            // Save PNG
            byte[] bytes = texture.EncodeToPNG();
            File.WriteAllBytes(fullPath, bytes);

            // Import and configure the texture
            AssetDatabase.Refresh();

            string assetPath = $"Assets/Textures/{filename}";
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = textureResolution;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            UnityEngine.Debug.Log($"Grid texture saved to: {assetPath}");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath));
        }

        private void GenerateVariations()
        {
            // Save current settings
            Color originalLineColor = lineColor;

            // Generate variations with different colors matching frequency spectrum
            Color[] spectrumColors = new Color[]
            {
                new Color(1f, 0f, 0f, 0.5f),    // Red
                new Color(1f, 1f, 0f, 0.5f),    // Yellow
                new Color(0f, 1f, 0f, 0.5f),    // Green
                new Color(0f, 1f, 1f, 0.5f),    // Cyan
                new Color(0f, 0f, 1f, 0.5f),    // Blue
                new Color(1f, 0f, 1f, 0.5f)     // Magenta
            };

            for (int i = 0; i < spectrumColors.Length; i++)
            {
                lineColor = spectrumColors[i];
                string colorName = GetColorName(i);
                GenerateGridTexture($"_{colorName}");
            }

            // Restore original color
            lineColor = originalLineColor;

            UnityEngine.Debug.Log($"Generated {spectrumColors.Length} grid texture variations");
        }

        private string GetColorName(int index)
        {
            string[] names = { "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta" };
            return index < names.Length ? names[index] : $"Color{index}";
        }
    }

    /// <summary>
    /// Quick access menu items for common grid generation tasks
    /// </summary>
    public static class GridTextureQuickGenerate
    {
        [MenuItem("SYSTEM/Quick Generate/Standard Grid Texture")]
        public static void GenerateStandardGrid()
        {
            EditorWindow.GetWindow<GridTextureGenerator>("Grid Texture Generator").Show();
        }

        [MenuItem("SYSTEM/Test/Apply Grid to Test Plane")]
        public static void TestGridTexture()
        {
            // Create a plane
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "GridTestPlane";
            plane.transform.position = Vector3.zero;
            plane.transform.localScale = Vector3.one * 10f;

            // Load the most recent generated texture
            string[] guids = AssetDatabase.FindAssets("WavePacketGrid t:Texture2D", new[] { "Assets/Textures" });

            if (guids.Length > 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                Texture2D gridTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

                if (gridTex != null)
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.mainTexture = gridTex;
                    mat.SetFloat("_Surface", 1); // Transparent
                    mat.SetFloat("_Blend", 0); // Alpha blending
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000; // Transparent queue

                    plane.GetComponent<Renderer>().material = mat;

                    UnityEngine.Debug.Log($"Grid texture applied to test plane from: {assetPath}");
                    Selection.activeGameObject = plane;
                }
            }
            else
            {
                UnityEngine.Debug.LogWarning("No grid textures found. Generate one first using SYSTEM → Tools → Grid Texture Generator");
            }
        }
    }
}
