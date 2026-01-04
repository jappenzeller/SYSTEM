namespace SYSTEM.HeadlessClient.Chat;

/// <summary>
/// Coordinates multiple chat platforms, providing unified command handling and announcements.
/// QAI operates as a single identity across all platforms.
/// </summary>
public class ChatPlatformManager : IDisposable
{
    private readonly List<IChatPlatform> _platforms = new();
    private readonly QaiCommandHandler _commandHandler;
    private readonly HashSet<string> _privilegedUsers;

    public event Action<string, string>? OnAnnouncementSent; // (platform, message)

    public ChatPlatformManager(
        QaiCommandHandler commandHandler,
        IEnumerable<string> privilegedUsers)
    {
        _commandHandler = commandHandler;
        _privilegedUsers = new HashSet<string>(privilegedUsers, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Register a chat platform with the manager.
    /// </summary>
    public void RegisterPlatform(IChatPlatform platform)
    {
        platform.OnMessageReceived += HandleMessageAsync;
        platform.OnConnectionChanged += connected =>
        {
            Console.WriteLine($"[ChatManager] {platform.PlatformName} connection: {(connected ? "connected" : "disconnected")}");
        };
        _platforms.Add(platform);
        Console.WriteLine($"[ChatManager] Registered platform: {platform.PlatformName}");
    }

    /// <summary>
    /// Connect all registered platforms.
    /// </summary>
    public async Task ConnectAllAsync(CancellationToken ct = default)
    {
        Console.WriteLine($"[ChatManager] Connecting {_platforms.Count} platform(s)...");
        var tasks = _platforms.Select(p => ConnectPlatformAsync(p, ct));
        await Task.WhenAll(tasks);
    }

    private async Task ConnectPlatformAsync(IChatPlatform platform, CancellationToken ct)
    {
        try
        {
            await platform.ConnectAsync(ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatManager] Failed to connect {platform.PlatformName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Disconnect all platforms.
    /// </summary>
    public async Task DisconnectAllAsync()
    {
        Console.WriteLine("[ChatManager] Disconnecting all platforms...");
        var tasks = _platforms.Select(p => DisconnectPlatformAsync(p));
        await Task.WhenAll(tasks);
    }

    private async Task DisconnectPlatformAsync(IChatPlatform platform)
    {
        try
        {
            await platform.DisconnectAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatManager] Failed to disconnect {platform.PlatformName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Broadcast announcement to all connected platforms.
    /// </summary>
    public async Task AnnounceAsync(string message)
    {
        foreach (var platform in _platforms.Where(p => p.IsConnected))
        {
            try
            {
                // Truncate message to platform limit
                var truncated = message.Length > platform.MaxMessageLength
                    ? message[..(platform.MaxMessageLength - 3)] + "..."
                    : message;

                await platform.BroadcastAsync(truncated);
                OnAnnouncementSent?.Invoke(platform.PlatformName, truncated);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatManager] Failed to announce on {platform.PlatformName}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Announce inventory full across all platforms.
    /// </summary>
    public Task AnnounceInventoryFullAsync()
        => AnnounceAsync("Inventory is FULL! Need to offload packets.");

    /// <summary>
    /// Announce mining started across all platforms.
    /// </summary>
    public Task AnnounceMiningStartedAsync(ulong sourceId)
        => AnnounceAsync($"Started mining source {sourceId}");

    /// <summary>
    /// Announce mining complete across all platforms.
    /// </summary>
    public Task AnnounceMiningCompleteAsync(ulong sourceId, uint totalExtracted)
        => AnnounceAsync($"Mining complete! Extracted {totalExtracted} packets from source {sourceId}");

    /// <summary>
    /// Announce source discovered across all platforms.
    /// </summary>
    public Task AnnounceSourceDiscoveredAsync(ulong sourceId, uint packetCount)
        => AnnounceAsync($"New source discovered! Source {sourceId} with {packetCount} packets");

    /// <summary>
    /// Handle incoming message from any platform.
    /// </summary>
    private async Task HandleMessageAsync(ChatMessage message)
    {
        // Only respond to !qai commands (unified command structure)
        if (!message.Content.StartsWith("!qai", StringComparison.OrdinalIgnoreCase))
            return;

        Console.WriteLine($"[{message.Platform}] #{message.ChannelName} <{message.Username}>: {message.Content}");

        var isPrivileged = _privilegedUsers.Contains(message.Username);

        try
        {
            var response = await _commandHandler.HandleAsync(message, isPrivileged);

            if (!string.IsNullOrEmpty(response))
            {
                var platform = _platforms.FirstOrDefault(p => p.PlatformName == message.Platform);
                if (platform != null)
                {
                    // Truncate to platform limit
                    if (response.Length > platform.MaxMessageLength)
                        response = response[..(platform.MaxMessageLength - 3)] + "...";

                    await platform.SendMessageAsync(message.ChannelId, response);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatManager] Error handling message: {ex.Message}");
        }
    }

    public void Dispose()
    {
        foreach (var platform in _platforms)
        {
            platform.OnMessageReceived -= HandleMessageAsync;
            platform.Dispose();
        }
        _platforms.Clear();
    }
}
