using SYSTEM.HeadlessClient.Behavior;
using SYSTEM.HeadlessClient.Twitch;

namespace SYSTEM.HeadlessClient.Config;

public class ClientConfig
{
    public SpacetimeDBConfig SpacetimeDB { get; set; } = new();
    public QAIConfig QAI { get; set; } = new();
    public BehaviorConfig Behavior { get; set; } = new();
    public TwitchConfig Twitch { get; set; } = new();
    public McpConfig Mcp { get; set; } = new();
    public StorageConfig Storage { get; set; } = new();
}

public class McpConfig
{
    public bool Enabled { get; set; } = true;
}

public class SpacetimeDBConfig
{
    public string ServerUrl { get; set; } = "ws://127.0.0.1:3000";
    public string ModuleName { get; set; } = "system";
    public bool UseSSL { get; set; } = false;

    public string GetConnectionUrl()
    {
        // Handle maincloud shorthand
        if (ServerUrl.Contains("maincloud"))
            return $"wss://{ServerUrl}";
        return UseSSL ? ServerUrl.Replace("ws://", "wss://") : ServerUrl;
    }
}

public class QAIConfig
{
    public string Username { get; set; } = "";
    public string Pin { get; set; } = "";
    public string DisplayName { get; set; } = "QAI";
    public string DeviceInfo { get; set; } = "QAI/1.0";
}

public class StorageConfig
{
    public string TokenFilePath { get; set; } = ".qai-token";
}
