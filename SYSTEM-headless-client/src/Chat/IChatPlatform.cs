namespace SYSTEM.HeadlessClient.Chat;

/// <summary>
/// Represents a chat message from any platform.
/// </summary>
public class ChatMessage
{
    /// <summary>Platform identifier (e.g., "Twitch", "Discord")</summary>
    public required string Platform { get; init; }

    /// <summary>Platform-specific user ID</summary>
    public required string UserId { get; init; }

    /// <summary>Display name of the user</summary>
    public required string Username { get; init; }

    /// <summary>Message text content</summary>
    public required string Content { get; init; }

    /// <summary>Channel/server ID where message was sent</summary>
    public required string ChannelId { get; init; }

    /// <summary>Human-readable channel name</summary>
    public required string ChannelName { get; init; }

    /// <summary>Whether the user is a platform moderator</summary>
    public bool IsModerator { get; init; }

    /// <summary>Whether the user is a subscriber/member</summary>
    public bool IsSubscriber { get; init; }

    /// <summary>When the message was received</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Abstraction for chat platforms (Twitch, Discord, etc.)
/// Enables QAI to operate as a single identity across multiple platforms.
/// </summary>
public interface IChatPlatform : IDisposable
{
    /// <summary>Platform identifier (e.g., "Twitch", "Discord")</summary>
    string PlatformName { get; }

    /// <summary>Whether the platform is currently connected</summary>
    bool IsConnected { get; }

    /// <summary>Maximum message length for this platform</summary>
    int MaxMessageLength { get; }

    /// <summary>Connect to the platform</summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>Disconnect from the platform</summary>
    Task DisconnectAsync();

    /// <summary>Send a message to a specific channel</summary>
    Task SendMessageAsync(string channelId, string message);

    /// <summary>Broadcast to all configured channels</summary>
    Task BroadcastAsync(string message);

    /// <summary>Fired when a message is received</summary>
    event Func<ChatMessage, Task>? OnMessageReceived;

    /// <summary>Fired when connection state changes</summary>
    event Action<bool>? OnConnectionChanged;
}
