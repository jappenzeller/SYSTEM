namespace SYSTEM.HeadlessClient.Twitch;

/// <summary>
/// Configuration for Twitch bot integration.
/// </summary>
public class TwitchConfig
{
    public bool Enabled { get; set; } = false;
    public string Username { get; set; } = "system_qai_test";
    public string OAuthToken { get; set; } = "";
    public string Channel { get; set; } = "";
    public bool AnnounceEvents { get; set; } = true;
}
