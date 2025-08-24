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
        
        // Enable full exceptions for better debugging in WebGL
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace;
        PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
        
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = GetScenes(),
            locationPathName = buildPath,
            target = BuildTarget.WebGL,
            options = envSettings.developmentBuild ? BuildOptions.Development : BuildOptions.None
        };

        Debug.Log($"Building WebGL for {environment} environment...");
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
        // Create a runtime config file that will be included in the build
        string configPath = "Assets/Resources/runtime_config.json";
        
        // Ensure Resources directory exists
        Directory.CreateDirectory("Assets/Resources");
        
        var config = new
        {
            environment = settings.name,
            moduleAddress = settings.moduleAddress,
            moduleName = settings.moduleName,
            enableDebugLogging = settings.enableDebugLogging
        };
        
        string json = JsonUtility.ToJson(config, true);
        File.WriteAllText(configPath, json);
        
        // Refresh the asset database so Unity recognizes the new file
        AssetDatabase.Refresh();
        
        Debug.Log($"Saved runtime config for {settings.name} environment");
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