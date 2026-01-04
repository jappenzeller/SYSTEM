namespace SYSTEM.HeadlessClient.Discord;

/// <summary>
/// Configuration for Discord bot integration.
/// </summary>
public class DiscordConfig
{
    /// <summary>Enable/disable Discord integration</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Discord bot token from Developer Portal</summary>
    public string BotToken { get; set; } = "";

    /// <summary>Guild (server) IDs where the bot operates. Empty = all servers.</summary>
    public List<ulong> GuildIds { get; set; } = new();

    /// <summary>Channel IDs for announcements</summary>
    public List<ulong> AnnouncementChannelIds { get; set; } = new();

    /// <summary>Whether to announce game events</summary>
    public bool AnnounceEvents { get; set; } = true;

    /// <summary>Bot activity status message</summary>
    public string StatusMessage { get; set; } = "Mining wave packets | !qai";
}
