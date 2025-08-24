using UnityEngine;

[CreateAssetMenu(fileName = "BuildSettings", menuName = "SYSTEM/Build Settings")]
public class BuildSettings : ScriptableObject
{
    [System.Serializable]
    public class EnvironmentSettings
    {
        public string name = "Local";
        public string moduleAddress = "127.0.0.1:3000";
        public string moduleName = "system";
        public bool enableDebugLogging = true;
        public bool developmentBuild = true;
    }
    
    [Header("Environments")]
    public EnvironmentSettings localSettings = new EnvironmentSettings
    {
        name = "Local",
        moduleAddress = "127.0.0.1:3000",
        moduleName = "system",
        enableDebugLogging = true,
        developmentBuild = true
    };
    
    public EnvironmentSettings testSettings = new EnvironmentSettings
    {
        name = "Test",
        moduleAddress = "maincloud.spacetimedb.com",
        moduleName = "system-test",
        enableDebugLogging = true,
        developmentBuild = true
    };
    
    public EnvironmentSettings productionSettings = new EnvironmentSettings
    {
        name = "Production",
        moduleAddress = "maincloud.spacetimedb.com",
        moduleName = "system",
        enableDebugLogging = false,
        developmentBuild = false
    };
    
    public EnvironmentSettings GetSettings(string environmentName)
    {
        switch (environmentName.ToLower())
        {
            case "local":
                return localSettings;
            case "test":
                return testSettings;
            case "production":
                return productionSettings;
            default:
                Debug.LogWarning($"Unknown environment: {environmentName}, using local");
                return localSettings;
        }
    }
}