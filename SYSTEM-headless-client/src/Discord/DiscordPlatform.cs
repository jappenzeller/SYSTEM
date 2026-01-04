using Discord;
using Discord.WebSocket;
using SYSTEM.HeadlessClient.Chat;

namespace SYSTEM.HeadlessClient.Discord;

/// <summary>
/// Discord chat platform implementation using Discord.Net.
/// Implements IChatPlatform for unified chat handling.
/// </summary>
public class DiscordPlatform : IChatPlatform
{
    private readonly DiscordConfig _config;
    private readonly DiscordSocketClient _client;

    public string PlatformName => "Discord";
    public bool IsConnected { get; private set; }
    public int MaxMessageLength => 2000; // Discord limit

    public event Func<ChatMessage, Task>? OnMessageReceived;
    public event Action<bool>? OnConnectionChanged;

    public DiscordPlatform(DiscordConfig config)
    {
        _config = config;

        var socketConfig = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds
                           | GatewayIntents.GuildMessages
                           | GatewayIntents.MessageContent,
            LogLevel = LogSeverity.Info
        };

        _client = new DiscordSocketClient(socketConfig);
        _client.MessageReceived += HandleMessageAsync;
        _client.Ready += HandleReady;
        _client.Disconnected += HandleDisconnected;
        _client.Log += HandleLog;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (!_config.Enabled || string.IsNullOrEmpty(_config.BotToken))
        {
            Console.WriteLine("[Discord] Platform disabled or no bot token configured");
            return;
        }

        try
        {
            Console.WriteLine("[Discord] Connecting...");
            await _client.LoginAsync(TokenType.Bot, _config.BotToken);
            await _client.StartAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Discord] Connection failed: {ex.Message}");
        }
    }

    public async Task DisconnectAsync()
    {
        if (IsConnected)
        {
            await _client.StopAsync();
            await _client.LogoutAsync();
            IsConnected = false;
        }
    }

    public async Task SendMessageAsync(string channelId, string message)
    {
        if (!ulong.TryParse(channelId, out var id))
        {
            Console.WriteLine($"[Discord] Invalid channel ID: {channelId}");
            return;
        }

        if (_client.GetChannel(id) is IMessageChannel channel)
        {
            try
            {
                await channel.SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Discord] Failed to send message: {ex.Message}");
            }
        }
    }

    public async Task BroadcastAsync(string message)
    {
        if (!IsConnected || !_config.AnnounceEvents)
            return;

        foreach (var channelId in _config.AnnouncementChannelIds)
        {
            if (_client.GetChannel(channelId) is IMessageChannel channel)
            {
                try
                {
                    await channel.SendMessageAsync(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Discord] Failed to broadcast to channel {channelId}: {ex.Message}");
                }
            }
        }
    }

    #region Event Handlers

    private Task HandleReady()
    {
        IsConnected = true;
        Console.WriteLine($"[Discord] Connected as {_client.CurrentUser.Username}#{_client.CurrentUser.Discriminator}");
        Console.WriteLine($"[Discord] In {_client.Guilds.Count} guild(s)");

        // Set status
        _client.SetGameAsync(_config.StatusMessage, type: ActivityType.Playing);

        OnConnectionChanged?.Invoke(true);
        return Task.CompletedTask;
    }

    private Task HandleDisconnected(Exception ex)
    {
        IsConnected = false;
        Console.WriteLine($"[Discord] Disconnected: {ex.Message}");
        OnConnectionChanged?.Invoke(false);
        return Task.CompletedTask;
    }

    private Task HandleLog(LogMessage log)
    {
        // Only log warnings and errors
        if (log.Severity <= LogSeverity.Warning)
        {
            Console.WriteLine($"[Discord] {log.Severity}: {log.Message}");
            if (log.Exception != null)
            {
                Console.WriteLine($"[Discord] Exception: {log.Exception.Message}");
            }
        }
        return Task.CompletedTask;
    }

    private async Task HandleMessageAsync(SocketMessage msg)
    {
        // Ignore bots and system messages
        if (msg.Author.IsBot || msg is not SocketUserMessage userMsg)
            return;

        // Only respond in guilds we're configured for (or all if no filter)
        if (userMsg.Channel is SocketGuildChannel guildChannel)
        {
            if (_config.GuildIds.Count > 0 && !_config.GuildIds.Contains(guildChannel.Guild.Id))
                return;
        }

        // Only respond in designated QAI channels (uses AnnouncementChannelIds for filtering)
        if (_config.AnnouncementChannelIds.Count > 0 && !_config.AnnouncementChannelIds.Contains(msg.Channel.Id))
            return;

        var chatMessage = new ChatMessage
        {
            Platform = "Discord",
            UserId = msg.Author.Id.ToString(),
            Username = msg.Author.Username,
            Content = msg.Content.Trim(),
            ChannelId = msg.Channel.Id.ToString(),
            ChannelName = msg.Channel.Name,
            IsModerator = userMsg.Author is SocketGuildUser guildUser &&
                         guildUser.GuildPermissions.ManageMessages,
            IsSubscriber = false // Discord doesn't have native subscription like Twitch
        };

        if (OnMessageReceived != null)
        {
            try
            {
                await OnMessageReceived(chatMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Discord] Error handling message: {ex.Message}");
            }
        }
    }

    #endregion

    public void Dispose()
    {
        DisconnectAsync().Wait();
        _client.Dispose();
    }
}
