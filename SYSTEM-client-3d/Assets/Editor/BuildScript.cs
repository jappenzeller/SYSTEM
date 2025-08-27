using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;
using System.Linq;

public class BuildScript
{
    private static string[] GetScenes()
    {
        return EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
    }

    [MenuItem("Build/Build Local WebGL")]
    public static void BuildWebGLLocal()
    {
        BuildWebGL("local");
    }

    [MenuItem("Build/Build Test WebGL")]
    public static void BuildWebGLTest()
    {
        BuildWebGL("test");
    }

    [MenuItem("Build/Build Production WebGL")]
    public static void BuildWebGLProduction()
    {
        BuildWebGL("production");
    }

    [MenuItem("Build/Build Local Windows")]
    public static void BuildWindowsLocal()
    {
        BuildWindows("local");
    }

    [MenuItem("Build/Build Test Windows")]
    public static void BuildWindowsTest()
    {
        BuildWindows("test");
    }

    [MenuItem("Build/Build Production Windows")]
    public static void BuildWindowsProduction()
    {
        BuildWindows("production");
    }

    private static void BuildWebGL(string environment)
    {
        string buildPath = "WebBuild";
        
        // Note: We now use runtime platform detection for environment
        // WebGL builds automatically connect to test server (maincloud.spacetimedb.com/system-test)
        Debug.Log($"========================================");
        Debug.Log($"Building WebGL for {environment} environment");
        Debug.Log($"Build will use RUNTIME platform detection:");
        Debug.Log($"  RuntimePlatform.WebGLPlayer → maincloud.spacetimedb.com/system-test");
        Debug.Log($"  Application.isEditor → localhost:3000/system");
        Debug.Log($"  Other platforms → maincloud.spacetimedb.com/system");
        Debug.Log($"========================================");
        
        // Load build settings for other configurations (not for connection anymore)
        var settings = LoadBuildSettings();
        if (settings == null)
        {
            // Create default settings if none exist
            settings = ScriptableObject.CreateInstance<BuildSettings>();
        }

        var envSettings = settings.GetSettings(environment);
        
        // Configure player settings (but not for connection - that's hardcoded now)
        ConfigurePlayerSettings(envSettings);
        
        // Enable full exceptions for better debugging in WebGL
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace;
        PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
        
        // Compression settings for better loading
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = true;
        
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = GetScenes(),
            locationPathName = buildPath,
            target = BuildTarget.WebGL,
            options = envSettings.developmentBuild ? BuildOptions.Development : BuildOptions.None
        };

        Debug.Log($"Building to: {System.IO.Path.GetFullPath(buildPath)}");
        
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            string fullPath = System.IO.Path.GetFullPath(buildPath);
            Debug.Log($"========================================");
            Debug.Log($"✅ WEBGL BUILD SUCCEEDED!");
            Debug.Log($"Size: {summary.totalSize / 1024 / 1024} MB");
            Debug.Log($"Location: {fullPath}");
            Debug.Log($"========================================");
            Debug.Log($"WebGL builds will connect to:");
            Debug.Log($"  URL: https://maincloud.spacetimedb.com");
            Debug.Log($"  Module: system-test");
            Debug.Log($"========================================");
            Debug.Log($"To deploy, run from project root:");
            Debug.Log($"  .\\Deploy-UnityWebGL.ps1 -BucketName system-game-test");
        }
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError($"========================================");
            Debug.LogError($"❌ WEBGL BUILD FAILED!");
            Debug.LogError($"Check console for errors");
            Debug.LogError($"========================================");
        }
    }

    private static void BuildWindows(string environment)
    {
        string buildPath = "Build/SYSTEM-client-3d.exe";
        
        // Ensure directory exists
        Directory.CreateDirectory("Build");
        
        // Load build settings
        var settings = LoadBuildSettings();
        if (settings == null)
        {
            Debug.LogError("BuildSettings asset not found! Please create one at Assets/Resources/BuildSettings.asset");
            return;
        }

        var envSettings = settings.GetSettings(environment);
        
        // Configure player settings
        ConfigurePlayerSettings(envSettings);
        
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = GetScenes(),
            locationPathName = buildPath,
            target = BuildTarget.StandaloneWindows64,
            options = envSettings.developmentBuild ? BuildOptions.Development : BuildOptions.None
        };

        Debug.Log($"Building Windows for {environment} environment...");
        Debug.Log($"Server: {envSettings.moduleAddress}/{envSettings.moduleName}");
        
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Build succeeded: {summary.totalSize} bytes");
            Debug.Log($"Build location: {buildPath}");
        }
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError("Build failed!");
        }
    }

    private static BuildSettings LoadBuildSettings()
    {
        // Try to load from Resources first
        var settings = Resources.Load<BuildSettings>("BuildSettings");
        
        if (settings == null)
        {
            // Try to find it anywhere in the project
            string[] guids = AssetDatabase.FindAssets("t:BuildSettings");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                settings = AssetDatabase.LoadAssetAtPath<BuildSettings>(path);
            }
        }
        
        return settings;
    }

    private static void ConfigurePlayerSettings(BuildSettings.EnvironmentSettings envSettings)
    {
        // Store environment settings in PlayerPrefs or a runtime config file
        // This is a simple approach - you might want to use a more sophisticated method
        
        // Set scripting define symbols based on environment
        string defineSymbols = envSettings.developmentBuild ? "DEVELOPMENT_BUILD" : "";
        if (envSettings.enableDebugLogging)
        {
            defineSymbols += ";DEBUG_LOGGING";
        }
        
        PlayerSettings.SetScriptingDefineSymbolsForGroup(
            BuildTargetGroup.Standalone, 
            defineSymbols
        );
        
        PlayerSettings.SetScriptingDefineSymbolsForGroup(
            BuildTargetGroup.WebGL, 
            defineSymbols
        );
        
        // Configure other player settings
        PlayerSettings.productName = "SYSTEM";
        PlayerSettings.companyName = "SpaceTime";
        
        // You might want to save the environment settings to a file that gets included in the build
        SaveRuntimeConfig(envSettings);
    }

    private static void SaveRuntimeConfig(BuildSettings.EnvironmentSettings settings)
    {
        // NOTE: We now use runtime platform detection (Application.platform)
        // This method is kept for backwards compatibility but is no longer used
        // WebGL runtime: maincloud.spacetimedb.com/system-test
        // Editor runtime: localhost:3000/system
        // Standalone runtime: maincloud.spacetimedb.com/system
        
        Debug.Log($"[BuildScript] Note: Using runtime platform detection (Application.platform)");
        Debug.Log($"[BuildScript] WebGL builds will detect RuntimePlatform.WebGLPlayer at runtime");
        Debug.Log($"[BuildScript] Connection settings are determined at runtime based on platform");
    }

    // Command line build support
    public static void BuildFromCommandLine()
    {
        string environment = GetCommandLineArg("-environment", "local");
        string platform = GetCommandLineArg("-platform", "webgl");
        
        Debug.Log($"Building from command line: {platform} for {environment}");
        
        switch (platform.ToLower())
        {
            case "webgl":
                BuildWebGL(environment);
                break;
            case "windows":
                BuildWindows(environment);
                break;
            default:
                Debug.LogError($"Unknown platform: {platform}");
                break;
        }
    }

    private static string GetCommandLineArg(string name, string defaultValue)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == name && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }
        return defaultValue;
    }
}