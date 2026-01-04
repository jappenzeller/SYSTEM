using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using SYSTEM.HeadlessClient.Chat;
using ChatMessage = SYSTEM.HeadlessClient.Chat.ChatMessage;

namespace SYSTEM.HeadlessClient.Twitch;

/// <summary>
/// Twitch chat platform implementation.
/// Wraps TwitchLib.Client and implements IChatPlatform for unified chat handling.
/// </summary>
public class TwitchPlatform : IChatPlatform
{
    private readonly TwitchConfig _config;
    private readonly TwitchClient _client;
    private readonly HashSet<string> _joinedChannels = new(StringComparer.OrdinalIgnoreCase);

    public string PlatformName => "Twitch";
    public bool IsConnected { get; private set; }
    public int MaxMessageLength => 500; // Twitch IRC limit

    public event Func<ChatMessage, Task>? OnMessageReceived;
    public event Action<bool>? OnConnectionChanged;

    public TwitchPlatform(TwitchConfig config)
    {
        _config = config;

        // Create client with rate limiting options
        var clientOptions = new ClientOptions
        {
            MessagesAllowedInPeriod = 750,
            ThrottlingPeriod = TimeSpan.FromSeconds(30)
        };
        var webSocketClient = new WebSocketClient(clientOptions);
        _client = new TwitchClient(webSocketClient);

        // Wire up events
        _client.OnConnected += HandleConnected;
        _client.OnJoinedChannel += HandleJoinedChannel;
        _client.OnMessageReceived += HandleMessageReceived;
        _client.OnLog += HandleLog;
        _client.OnError += HandleError;
        _client.OnDisconnected += HandleDisconnected;
    }

    public Task ConnectAsync(CancellationToken ct = default)
    {
        if (!_config.Enabled || string.IsNullOrEmpty(_config.OAuthToken))
        {
            Console.WriteLine("[Twitch] Platform disabled or no OAuth token configured");
            return Task.CompletedTask;
        }

        try
        {
            var credentials = new ConnectionCredentials(_config.Username, _config.OAuthToken);
            _client.Initialize(credentials, _config.Channel);
            _client.Connect();
            Console.WriteLine($"[Twitch] Connecting as {_config.Username} to #{_config.Channel}...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Twitch] Connection failed: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        if (IsConnected)
        {
            _client.Disconnect();
            IsConnected = false;
        }
        return Task.CompletedTask;
    }

    public Task SendMessageAsync(string channelId, string message)
    {
        if (IsConnected)
        {
            _client.SendMessage(channelId, message);
        }
        return Task.CompletedTask;
    }

    public Task BroadcastAsync(string message)
    {
        if (IsConnected && _config.AnnounceEvents)
        {
            // Send to all joined channels
            foreach (var channel in _joinedChannels)
            {
                _client.SendMessage(channel, message);
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Join an additional channel (Twitch-specific).
    /// </summary>
    public void JoinChannel(string channel)
    {
        var normalizedChannel = channel.TrimStart('#').ToLower();
        if (!_joinedChannels.Contains(normalizedChannel))
        {
            _client.JoinChannel(normalizedChannel);
            Console.WriteLine($"[Twitch] Joining channel #{normalizedChannel}");
        }
    }

    /// <summary>
    /// Leave a channel (Twitch-specific).
    /// </summary>
    public void LeaveChannel(string channel)
    {
        var normalizedChannel = channel.TrimStart('#').ToLower();

        // Don't leave the home channel
        if (normalizedChannel.Equals(_config.Channel, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[Twitch] Can't leave home channel #{_config.Channel}");
            return;
        }

        if (_joinedChannels.Contains(normalizedChannel))
        {
            _client.LeaveChannel(normalizedChannel);
            _joinedChannels.Remove(normalizedChannel);
            Console.WriteLine($"[Twitch] Left channel #{normalizedChannel}");
        }
    }

    #region Event Handlers

    private void HandleConnected(object? sender, OnConnectedArgs e)
    {
        IsConnected = true;
        Console.WriteLine("[Twitch] Connected to Twitch IRC");
        OnConnectionChanged?.Invoke(true);
    }

    private void HandleJoinedChannel(object? sender, OnJoinedChannelArgs e)
    {
        _joinedChannels.Add(e.Channel.ToLower());
        Console.WriteLine($"[Twitch] Joined channel #{e.Channel}");

        if (_config.AnnounceEvents)
        {
            _client.SendMessage(e.Channel, "I am here. Type !qai to speak with me.");
        }
    }

    private async void HandleMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        var chatMessage = new ChatMessage
        {
            Platform = "Twitch",
            UserId = e.ChatMessage.UserId,
            Username = e.ChatMessage.Username,
            Content = e.ChatMessage.Message.Trim(),
            ChannelId = e.ChatMessage.Channel,
            ChannelName = e.ChatMessage.Channel,
            IsModerator = e.ChatMessage.IsModerator,
            IsSubscriber = e.ChatMessage.IsSubscriber
        };

        if (OnMessageReceived != null)
        {
            try
            {
                await OnMessageReceived(chatMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Twitch] Error handling message: {ex.Message}");
            }
        }
    }

    private void HandleLog(object? sender, OnLogArgs e)
    {
        // Uncomment for debug logging
        // Console.WriteLine($"[Twitch] {e.Data}");
    }

    private void HandleError(object? sender, TwitchLib.Communication.Events.OnErrorEventArgs e)
    {
        Console.WriteLine($"[Twitch] Error: {e.Exception.Message}");
    }

    private void HandleDisconnected(object? sender, TwitchLib.Communication.Events.OnDisconnectedEventArgs e)
    {
        IsConnected = false;
        _joinedChannels.Clear();
        Console.WriteLine("[Twitch] Disconnected from Twitch");
        OnConnectionChanged?.Invoke(false);
    }

    #endregion

    public void Dispose()
    {
        DisconnectAsync().Wait();
    }
}
