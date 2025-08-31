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
        // Use environment-specific build directory
        string buildPath = $"Build/{CapitalizeFirst(environment)}";
        
        // Ensure directory exists
        Directory.CreateDirectory(buildPath);
        
        // Load build settings
        var settings = LoadBuildSettings();
        if (settings == null)
        {
            // Create default settings if none exist
            settings = ScriptableObject.CreateInstance<BuildSettings>();
        }

        var envSettings = settings.GetSettings(environment);
        
        // Determine connection settings based on environment
        string serverUrl = "";
        string moduleName = "";
        
        switch (environment.ToLower())
        {
            case "local":
                serverUrl = "http://127.0.0.1:3000";
                moduleName = "system";
                break;
            case "test":
                serverUrl = "https://maincloud.spacetimedb.com";
                moduleName = "system-test";
                break;
            case "production":
                serverUrl = "https://maincloud.spacetimedb.com";
                moduleName = "system";
                break;
        }
        
        Debug.Log($"========================================");
        Debug.Log($"Building WebGL for {environment.ToUpper()} environment");
        Debug.Log($"Output Directory: {System.IO.Path.GetFullPath(buildPath)}");
        Debug.Log($"Connection Configuration:");
        Debug.Log($"  Server URL: {serverUrl}");
        Debug.Log($"  Module Name: {moduleName}");
        Debug.Log($"  Debug Logging: {envSettings.enableDebugLogging}");
        Debug.Log($"  Development Build: {envSettings.developmentBuild}");
        Debug.Log($"========================================");
        
        // Configure player settings with environment-specific defines
        ConfigurePlayerSettings(envSettings, environment);
        
        // Save runtime configuration for this specific build
        SaveRuntimeConfig(envSettings, environment, buildPath);
        
        // Enable full exceptions for better debugging in WebGL
        PlayerSettings.WebGL.exceptionSupport = envSettings.developmentBuild ? 
            WebGLExceptionSupport.FullWithStacktrace : 
            WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
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

        Debug.Log($"Starting build process...");
        
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            string fullPath = System.IO.Path.GetFullPath(buildPath);
            Debug.Log($"========================================");
            Debug.Log($"✅ WEBGL BUILD SUCCEEDED!");
            Debug.Log($"Environment: {environment.ToUpper()}");
            Debug.Log($"Size: {summary.totalSize / 1024 / 1024} MB");
            Debug.Log($"Location: {fullPath}");
            Debug.Log($"========================================");
            Debug.Log($"This build will connect to:");
            Debug.Log($"  URL: {serverUrl}");
            Debug.Log($"  Module: {moduleName}");
            Debug.Log($"========================================");
            
            if (environment.ToLower() == "test")
            {
                Debug.Log($"To deploy to test environment, run:");
                Debug.Log($"  .\\Deploy-UnityWebGL.ps1 -BucketName system-game-test");
            }
            else if (environment.ToLower() == "production")
            {
                Debug.Log($"To deploy to production, run:");
                Debug.Log($"  .\\Deploy-UnityWebGL.ps1 -BucketName system-game-prod");
            }
        }
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError($"========================================");
            Debug.LogError($"❌ WEBGL BUILD FAILED!");
            Debug.LogError($"Environment: {environment.ToUpper()}");
            Debug.LogError($"Check console for errors");
            Debug.LogError($"========================================");
        }
    }

    private static void BuildWindows(string environment)
    {
        // Use environment-specific build directory
        string buildDir = $"Build/{CapitalizeFirst(environment)}";
        string buildPath = $"{buildDir}/SYSTEM-client-3d.exe";
        
        // Ensure directory exists
        Directory.CreateDirectory(buildDir);
        
        // Load build settings
        var settings = LoadBuildSettings();
        if (settings == null)
        {
            // Create default settings if none exist
            settings = ScriptableObject.CreateInstance<BuildSettings>();
        }

        var envSettings = settings.GetSettings(environment);
        
        // Determine connection settings based on environment
        string serverUrl = "";
        string moduleName = "";
        
        switch (environment.ToLower())
        {
            case "local":
                serverUrl = "http://127.0.0.1:3000";
                moduleName = "system";
                break;
            case "test":
                serverUrl = "https://maincloud.spacetimedb.com";
                moduleName = "system-test";
                break;
            case "production":
                serverUrl = "https://maincloud.spacetimedb.com";
                moduleName = "system";
                break;
        }
        
        Debug.Log($"========================================");
        Debug.Log($"Building Windows for {environment.ToUpper()} environment");
        Debug.Log($"Output Directory: {System.IO.Path.GetFullPath(buildDir)}");
        Debug.Log($"Connection Configuration:");
        Debug.Log($"  Server URL: {serverUrl}");
        Debug.Log($"  Module Name: {moduleName}");
        Debug.Log($"  Debug Logging: {envSettings.enableDebugLogging}");
        Debug.Log($"  Development Build: {envSettings.developmentBuild}");
        Debug.Log($"========================================");
        
        // Configure player settings with environment-specific defines
        ConfigurePlayerSettings(envSettings, environment);
        
        // Save runtime configuration for this specific build
        SaveRuntimeConfig(envSettings, environment, buildDir);
        
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = GetScenes(),
            locationPathName = buildPath,
            target = BuildTarget.StandaloneWindows64,
            options = envSettings.developmentBuild ? BuildOptions.Development : BuildOptions.None
        };
        
        Debug.Log($"Starting build process...");
        
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            string fullPath = System.IO.Path.GetFullPath(buildPath);
            Debug.Log($"========================================");
            Debug.Log($"✅ WINDOWS BUILD SUCCEEDED!");
            Debug.Log($"Environment: {environment.ToUpper()}");
            Debug.Log($"Size: {summary.totalSize / 1024 / 1024} MB");
            Debug.Log($"Location: {fullPath}");
            Debug.Log($"========================================");
            Debug.Log($"This build will connect to:");
            Debug.Log($"  URL: {serverUrl}");
            Debug.Log($"  Module: {moduleName}");
            Debug.Log($"========================================");
        }
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError($"========================================");
            Debug.LogError($"❌ WINDOWS BUILD FAILED!");
            Debug.LogError($"Environment: {environment.ToUpper()}");
            Debug.LogError($"========================================");
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

    private static void ConfigurePlayerSettings(BuildSettings.EnvironmentSettings envSettings, string environment)
    {
        // Set environment-specific scripting define symbols
        string defineSymbols = "";
        
        // Add environment define
        switch (environment.ToLower())
        {
            case "local":
                defineSymbols = "ENV_LOCAL";
                break;
            case "test":
                defineSymbols = "ENV_TEST";
                break;
            case "production":
                defineSymbols = "ENV_PRODUCTION";
                break;
        }
        
        // Add additional defines based on settings
        if (envSettings.developmentBuild)
        {
            defineSymbols += ";DEVELOPMENT_BUILD";
        }
        
        if (envSettings.enableDebugLogging)
        {
            defineSymbols += ";DEBUG_LOGGING";
        }
        
        // Apply defines to both WebGL and Standalone
        PlayerSettings.SetScriptingDefineSymbolsForGroup(
            BuildTargetGroup.Standalone, 
            defineSymbols
        );
        
        PlayerSettings.SetScriptingDefineSymbolsForGroup(
            BuildTargetGroup.WebGL, 
            defineSymbols
        );
        
        Debug.Log($"Applied scripting defines: {defineSymbols}");
        
        // Configure other player settings
        PlayerSettings.productName = "SYSTEM";
        PlayerSettings.companyName = "SpaceTime";
    }

    private static void SaveRuntimeConfig(BuildSettings.EnvironmentSettings settings, string environment, string buildPath)
    {
        // Create a runtime configuration file that will be included in the build
        // This allows the game to know which environment it was built for
        
        // Determine the correct server URL based on environment
        string serverUrl = "";
        string moduleName = settings.moduleName;
        
        switch (environment.ToLower())
        {
            case "local":
                serverUrl = "http://127.0.0.1:3000";
                moduleName = "system";
                break;
            case "test":
                serverUrl = "https://maincloud.spacetimedb.com";
                moduleName = "system-test";
                break;
            case "production":
                serverUrl = "https://maincloud.spacetimedb.com";
                moduleName = "system";
                break;
            default:
                // Use settings values as fallback
                serverUrl = settings.moduleAddress.StartsWith("http") ? 
                    settings.moduleAddress : 
                    $"http://{settings.moduleAddress}";
                break;
        }
        
        string configContent = $@"{{
    ""environment"": ""{environment}"",
    ""serverUrl"": ""{serverUrl}"",
    ""moduleName"": ""{moduleName}"",
    ""enableDebugLogging"": {settings.enableDebugLogging.ToString().ToLower()},
    ""developmentBuild"": {settings.developmentBuild.ToString().ToLower()}
}}";

        // Save to StreamingAssets so it gets included in the build
        string streamingAssetsPath = "Assets/StreamingAssets";
        Directory.CreateDirectory(streamingAssetsPath);
        
        string configPath = Path.Combine(streamingAssetsPath, "build-config.json");
        File.WriteAllText(configPath, configContent);
        
        Debug.Log($"Saved runtime configuration to: {configPath}");
        Debug.Log($"Configuration content:\n{configContent}");
        
        // Refresh asset database to ensure the file is included
        AssetDatabase.Refresh();
    }

    private static string CapitalizeFirst(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        
        return char.ToUpper(text[0]) + text.Substring(1).ToLower();
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