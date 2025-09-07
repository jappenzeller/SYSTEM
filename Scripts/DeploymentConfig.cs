using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace SYSTEM.Deployment
{
    /// <summary>
    /// Unity configuration class for SpacetimeDB deployment system
    /// Integrates with existing BuildConfiguration and BuildSettings
    /// </summary>
    [CreateAssetMenu(fileName = "DeploymentConfig", menuName = "SYSTEM/Deployment Configuration")]
    public class DeploymentConfig : ScriptableObject
    {
        [System.Serializable]
        public class EnvironmentConfig
        {
            public string name = "test";
            public string serverUrl = "https://maincloud.spacetimedb.com";
            public string moduleName = "system-test";
            public bool requiresAuth = true;
            public string cloudFrontDistributionId = "";
            public string awsRegion = "us-east-1";
            public string s3BucketName = "system-unity-game";
            
            [Header("Connection Settings")]
            public int connectionTimeout = 30;
            public int queryTimeout = 10;
            public int maxRetries = 3;
            
            [Header("Verification Settings")]
            public bool autoVerifyAfterDeploy = true;
            public string[] tablesToVerify = new string[] { "Player", "World", "Orb", "WavePacket", "Crystal" };
        }
        
        [Header("Environment Configurations")]
        public EnvironmentConfig localConfig = new EnvironmentConfig
        {
            name = "local",
            serverUrl = "127.0.0.1:3000",
            moduleName = "system",
            requiresAuth = false,
            cloudFrontDistributionId = ""
        };
        
        public EnvironmentConfig testConfig = new EnvironmentConfig
        {
            name = "test",
            serverUrl = "https://maincloud.spacetimedb.com",
            moduleName = "system-test",
            requiresAuth = true,
            cloudFrontDistributionId = "ENIM1XA5ZCZOT"
        };
        
        public EnvironmentConfig productionConfig = new EnvironmentConfig
        {
            name = "production",
            serverUrl = "https://maincloud.spacetimedb.com",
            moduleName = "system",
            requiresAuth = true,
            cloudFrontDistributionId = "E3HQWKXYZ9MNOP"
        };
        
        [Header("Deployment Settings")]
        public string serverModulePath = "../SYSTEM-server";
        public string clientAutogenPath = "Assets/Scripts/autogen";
        public string buildConfigPath = "Assets/StreamingAssets/build-config.json";
        public string logPath = "Logs/deployment";
        
        [Header("Build Settings")]
        public bool generateCSharpBindings = true;
        public bool updateBuildConfig = true;
        public bool cleanBeforeBuild = true;
        public bool useReleaseMode = true;
        
        [Header("Verification")]
        public bool runPostDeploymentVerification = true;
        public string verificationQueriesPath = "Scripts/post-deploy-verify.sql";
        public float verificationTimeout = 30f;
        
        [Header("Cache Management")]
        public bool supportCloudFrontInvalidation = true;
        public string[] invalidationPaths = new string[] { "/*" };
        
        /// <summary>
        /// Get environment configuration by name
        /// </summary>
        public EnvironmentConfig GetEnvironmentConfig(string environmentName)
        {
            switch (environmentName.ToLower())
            {
                case "local":
                    return localConfig;
                case "test":
                    return testConfig;
                case "production":
                case "prod":
                    return productionConfig;
                default:
                    Debug.LogWarning($"Unknown environment: {environmentName}, defaulting to test");
                    return testConfig;
            }
        }
        
        /// <summary>
        /// Get current environment from BuildSettings or build config
        /// </summary>
        public static async Task<string> GetCurrentEnvironment()
        {
            // First check BuildSettings if available
            var buildSettings = Resources.Load<BuildSettings>("BuildSettings");
            if (buildSettings != null)
            {
                return buildSettings.currentEnvironment.ToString().ToLower();
            }
            
            // Then check StreamingAssets build config
            string configPath = Path.Combine(Application.streamingAssetsPath, "build-config.json");
            
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                // WebGL async load
                using (UnityWebRequest request = UnityWebRequest.Get(configPath))
                {
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                        await Task.Yield();
                    
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var config = JsonUtility.FromJson<BuildConfigData>(request.downloadHandler.text);
                        return config.environment;
                    }
                }
            }
            else if (File.Exists(configPath))
            {
                // Standalone file read
                string json = File.ReadAllText(configPath);
                var config = JsonUtility.FromJson<BuildConfigData>(json);
                return config.environment;
            }
            
            // Default to test if nothing found
            Debug.LogWarning("Could not determine environment, defaulting to test");
            return "test";
        }
        
        /// <summary>
        /// Generate build configuration JSON
        /// </summary>
        public void GenerateBuildConfig(string environment)
        {
            var config = GetEnvironmentConfig(environment);
            var buildConfig = new BuildConfigData
            {
                environment = environment,
                serverUrl = config.serverUrl,
                moduleName = config.moduleName,
                buildTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                version = Application.version
            };
            
            string json = JsonUtility.ToJson(buildConfig, true);
            string path = Path.Combine(Application.dataPath, "StreamingAssets", "build-config.json");
            
            // Ensure directory exists
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            File.WriteAllText(path, json);
            Debug.Log($"Build configuration generated for {environment} environment: {path}");
        }
        
        /// <summary>
        /// Validate deployment configuration
        /// </summary>
        public bool ValidateConfiguration(string environment)
        {
            var config = GetEnvironmentConfig(environment);
            
            // Check server URL
            if (string.IsNullOrEmpty(config.serverUrl))
            {
                Debug.LogError($"Server URL is empty for {environment} environment");
                return false;
            }
            
            // Check module name
            if (string.IsNullOrEmpty(config.moduleName))
            {
                Debug.LogError($"Module name is empty for {environment} environment");
                return false;
            }
            
            // Check module path exists
            string fullModulePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", serverModulePath));
            if (!Directory.Exists(fullModulePath))
            {
                Debug.LogError($"Server module path does not exist: {fullModulePath}");
                return false;
            }
            
            // Check Cargo.toml exists
            string cargoPath = Path.Combine(fullModulePath, "Cargo.toml");
            if (!File.Exists(cargoPath))
            {
                Debug.LogError($"Cargo.toml not found at: {cargoPath}");
                return false;
            }
            
            Debug.Log($"Configuration validated for {environment} environment");
            return true;
        }
        
        /// <summary>
        /// Get deployment command for current platform
        /// </summary>
        public string GetDeploymentCommand(string environment, DeploymentOptions options)
        {
            string scriptName = Application.platform == RuntimePlatform.WindowsEditor 
                ? "deploy-spacetimedb.ps1" 
                : "deploy-spacetimedb.sh";
            
            string scriptPath = Path.Combine(Application.dataPath, "..", "Scripts", scriptName);
            
            var args = new List<string>();
            args.Add($"--environment {environment}");
            
            if (options.deleteData)
                args.Add("--delete-data");
            if (options.invalidateCache)
                args.Add("--invalidate-cache");
            if (options.publishOnly)
                args.Add("--publish-only");
            if (options.verify)
                args.Add("--verify");
            if (options.buildConfig)
                args.Add("--build-config");
            if (options.skipBuild)
                args.Add("--skip-build");
            if (options.nonInteractive)
                args.Add("--yes");
            
            string command = Application.platform == RuntimePlatform.WindowsEditor
                ? $"powershell.exe -ExecutionPolicy Bypass -File \"{scriptPath}\" {string.Join(" ", args)}"
                : $"bash \"{scriptPath}\" {string.Join(" ", args)}";
            
            return command;
        }
        
        [System.Serializable]
        public class DeploymentOptions
        {
            public bool deleteData = false;
            public bool invalidateCache = false;
            public bool publishOnly = false;
            public bool verify = true;
            public bool buildConfig = true;
            public bool skipBuild = false;
            public bool nonInteractive = false;
        }
        
        [System.Serializable]
        private class BuildConfigData
        {
            public string environment;
            public string serverUrl;
            public string moduleName;
            public string buildTime;
            public string version;
        }
        
#if UNITY_EDITOR
        /// <summary>
        /// Editor menu integration for deployment
        /// </summary>
        [UnityEditor.MenuItem("SYSTEM/Deploy/Deploy to Local")]
        public static void DeployToLocal()
        {
            DeployToEnvironment("local");
        }
        
        [UnityEditor.MenuItem("SYSTEM/Deploy/Deploy to Test")]
        public static void DeployToTest()
        {
            DeployToEnvironment("test");
        }
        
        [UnityEditor.MenuItem("SYSTEM/Deploy/Deploy to Production")]
        public static void DeployToProduction()
        {
            if (UnityEditor.EditorUtility.DisplayDialog(
                "Deploy to Production",
                "Are you sure you want to deploy to PRODUCTION environment?\n\nThis action cannot be undone.",
                "Deploy",
                "Cancel"))
            {
                DeployToEnvironment("production");
            }
        }
        
        [UnityEditor.MenuItem("SYSTEM/Deploy/Verify Current Deployment")]
        public static async void VerifyDeployment()
        {
            string environment = await GetCurrentEnvironment();
            Debug.Log($"Verifying deployment for {environment} environment...");
            
            var config = CreateInstance<DeploymentConfig>();
            var options = new DeploymentOptions
            {
                verify = true,
                skipBuild = true,
                publishOnly = true
            };
            
            string command = config.GetDeploymentCommand(environment, options);
            System.Diagnostics.Process.Start("cmd.exe", "/c " + command);
        }
        
        private static void DeployToEnvironment(string environment)
        {
            Debug.Log($"Starting deployment to {environment} environment...");
            
            var config = CreateInstance<DeploymentConfig>();
            
            if (!config.ValidateConfiguration(environment))
            {
                Debug.LogError("Configuration validation failed");
                return;
            }
            
            // Generate build config
            config.GenerateBuildConfig(environment);
            
            // Prepare deployment options
            var options = new DeploymentOptions
            {
                verify = true,
                buildConfig = true,
                invalidateCache = environment != "local"
            };
            
            // Get and execute deployment command
            string command = config.GetDeploymentCommand(environment, options);
            Debug.Log($"Executing: {command}");
            
            System.Diagnostics.Process.Start("cmd.exe", "/c " + command);
        }
#endif
    }
    
    /// <summary>
    /// Runtime deployment status tracker
    /// </summary>
    public class DeploymentStatus : MonoBehaviour
    {
        public enum Status
        {
            Idle,
            Connecting,
            Connected,
            Deploying,
            Verifying,
            Completed,
            Failed
        }
        
        public Status currentStatus = Status.Idle;
        public string currentEnvironment;
        public string lastDeploymentTime;
        public string lastError;
        public float connectionLatency;
        
        private static DeploymentStatus instance;
        public static DeploymentStatus Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("DeploymentStatus");
                    instance = go.AddComponent<DeploymentStatus>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }
        
        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        public async Task<bool> TestConnection(string environment)
        {
            currentStatus = Status.Connecting;
            
            var config = ScriptableObject.CreateInstance<DeploymentConfig>();
            var envConfig = config.GetEnvironmentConfig(environment);
            
            try
            {
                // Test connection with UnityWebRequest
                string testUrl = envConfig.serverUrl.StartsWith("http") 
                    ? envConfig.serverUrl 
                    : $"http://{envConfig.serverUrl}";
                
                using (UnityWebRequest request = UnityWebRequest.Get(testUrl))
                {
                    request.timeout = envConfig.connectionTimeout;
                    
                    float startTime = Time.realtimeSinceStartup;
                    var operation = request.SendWebRequest();
                    
                    while (!operation.isDone)
                        await Task.Yield();
                    
                    connectionLatency = (Time.realtimeSinceStartup - startTime) * 1000f;
                    
                    if (request.result == UnityWebRequest.Result.Success || 
                        request.result == UnityWebRequest.Result.ProtocolError)
                    {
                        currentStatus = Status.Connected;
                        Debug.Log($"Connection test successful: {connectionLatency:F2}ms");
                        return true;
                    }
                    else
                    {
                        lastError = request.error;
                        currentStatus = Status.Failed;
                        Debug.LogError($"Connection test failed: {request.error}");
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                lastError = e.Message;
                currentStatus = Status.Failed;
                Debug.LogError($"Connection test exception: {e.Message}");
                return false;
            }
        }
    }
}