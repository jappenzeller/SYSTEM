using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using SYSTEM.HeadlessClient.Behavior;
using SYSTEM.HeadlessClient.Inventory;
using SYSTEM.HeadlessClient.Mining;
using SYSTEM.HeadlessClient.Sensing;
using SYSTEM.HeadlessClient.World;

namespace SYSTEM.HeadlessClient.Twitch;

/// <summary>
/// Twitch IRC bot for chat commands and event announcements.
/// </summary>
public class TwitchBot : IDisposable
{
    private readonly TwitchConfig _config;
    private readonly TwitchClient _client;
    private readonly WorldManager? _worldManager;
    private readonly SourceDetector? _sourceDetector;
    private readonly MiningController? _miningController;
    private readonly InventoryTracker? _inventoryTracker;
    private readonly BehaviorStateMachine? _behaviorStateMachine;
    private readonly DateTime _startTime;

    private bool _isConnected;

    // Privileged users who can control QAI
    private static readonly HashSet<string> PrivilegedUsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "superstringman",  // Architect
        "exelbox"
    };

    public TwitchBot(
        TwitchConfig config,
        WorldManager? worldManager,
        SourceDetector? sourceDetector,
        MiningController? miningController,
        InventoryTracker? inventoryTracker,
        BehaviorStateMachine? behaviorStateMachine,
        DateTime startTime)
    {
        _config = config;
        _worldManager = worldManager;
        _sourceDetector = sourceDetector;
        _miningController = miningController;
        _inventoryTracker = inventoryTracker;
        _behaviorStateMachine = behaviorStateMachine;
        _startTime = startTime;

        // Create client
        var clientOptions = new ClientOptions
        {
            MessagesAllowedInPeriod = 750,
            ThrottlingPeriod = TimeSpan.FromSeconds(30)
        };
        var webSocketClient = new WebSocketClient(clientOptions);
        _client = new TwitchClient(webSocketClient);

        // Wire up events
        _client.OnConnected += OnConnected;
        _client.OnJoinedChannel += OnJoinedChannel;
        _client.OnMessageReceived += OnMessageReceived;
        _client.OnLog += OnLog;
        _client.OnError += OnError;
        _client.OnDisconnected += OnDisconnected;
    }

    public void Connect()
    {
        if (!_config.Enabled || string.IsNullOrEmpty(_config.OAuthToken))
        {
            Console.WriteLine("[Twitch] Bot disabled or no OAuth token configured");
            return;
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
    }

    public void Disconnect()
    {
        if (_isConnected)
        {
            _client.Disconnect();
            _isConnected = false;
        }
    }

    #region Event Handlers

    private void OnConnected(object? sender, OnConnectedArgs e)
    {
        _isConnected = true;
        Console.WriteLine($"[Twitch] Connected to Twitch IRC");
    }

    private void OnJoinedChannel(object? sender, OnJoinedChannelArgs e)
    {
        Console.WriteLine($"[Twitch] Joined channel #{e.Channel}");
        if (_config.AnnounceEvents)
        {
            _client.SendMessage(e.Channel, "QAI agent online! Type !help for commands.");
        }
    }

    private void OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        var message = e.ChatMessage.Message.ToLower().Trim();

        if (!message.StartsWith("!")) return;

        var command = message.Split(' ')[0];
        var username = e.ChatMessage.Username;
        var isPrivileged = PrivilegedUsers.Contains(username);

        // Public commands - available to everyone
        string? response = command switch
        {
            "!help" => isPrivileged
                ? "Commands: !status, !sources, !mine, !stop, !inventory, !position"
                : "Commands: !inventory, !position",
            "!inventory" => GetInventoryResponse(),
            "!position" => GetPositionResponse(),
            _ => null
        };

        // Privileged commands - only for superstringman and exelbox
        if (response == null && isPrivileged)
        {
            response = command switch
            {
                "!status" => GetStatusResponse(),
                "!sources" => GetSourcesResponse(),
                "!mine" => HandleMineCommand(),
                "!stop" => HandleStopCommand(),
                _ => null
            };
        }

        if (response != null)
        {
            _client.SendMessage(e.ChatMessage.Channel, response);
        }
    }

    private void OnLog(object? sender, OnLogArgs e)
    {
        // Uncomment for debug logging
        // Console.WriteLine($"[Twitch] {e.Data}");
    }

    private void OnError(object? sender, TwitchLib.Communication.Events.OnErrorEventArgs e)
    {
        Console.WriteLine($"[Twitch] Error: {e.Exception.Message}");
    }

    private void OnDisconnected(object? sender, TwitchLib.Communication.Events.OnDisconnectedEventArgs e)
    {
        _isConnected = false;
        Console.WriteLine("[Twitch] Disconnected from Twitch");
    }

    #endregion

    #region Command Responses

    private string GetStatusResponse()
    {
        var uptime = DateTime.UtcNow - _startTime;
        var state = _behaviorStateMachine?.CurrentState.ToString() ?? "unknown";
        var mining = _miningController?.IsMining == true ? "mining" : "idle";
        return $"QAI Status: {state} | {mining} | Uptime: {uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
    }

    private string GetSourcesResponse()
    {
        if (_sourceDetector == null) return "Source detector not available";
        var count = _sourceDetector.SourcesInRange.Count;
        if (count == 0) return "No sources in range";
        var richest = _sourceDetector.GetRichestSource();
        return $"{count} sources in range. Richest: {richest?.TotalWavePackets ?? 0} packets";
    }

    private string HandleMineCommand()
    {
        if (_miningController == null) return "Mining controller not available";
        if (_miningController.IsMining) return "Already mining!";
        if (_inventoryTracker?.IsFull == true) return "Inventory full, can't mine";

        var success = _miningController.StartMiningClosestSource();
        return success ? "Started mining closest source!" : "No sources in range";
    }

    private string HandleStopCommand()
    {
        if (_miningController == null) return "Mining controller not available";
        if (!_miningController.IsMining) return "Not mining";

        _miningController.StopMining();
        return "Mining stopped";
    }

    private string GetInventoryResponse()
    {
        if (_inventoryTracker == null) return "Inventory tracker not available";
        var count = _inventoryTracker.TotalCount;
        var cap = InventoryTracker.MAX_CAPACITY;
        var status = _inventoryTracker.IsFull ? "FULL" : $"{(count * 100 / cap)}%";

        var composition = _inventoryTracker.GetCompositionSummary();
        if (composition.Count == 0)
            return $"Inventory: {count}/{cap} ({status}) - empty";

        // Format: R:10 Y:5 G:3 C:0 B:2 M:1
        var colors = new[] { "red", "yellow", "green", "cyan", "blue", "magenta" };
        var labels = new[] { "R", "Y", "G", "C", "B", "M" };
        var breakdown = string.Join(" ", colors.Select((c, i) =>
            $"{labels[i]}:{(composition.TryGetValue(c, out var v) ? v : 0)}"));

        return $"Inventory: {count}/{cap} ({status}) | {breakdown}";
    }

    private string GetPositionResponse()
    {
        if (_worldManager == null) return "World manager not available";
        var pos = _worldManager.Position;
        return $"Position: ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})";
    }

    #endregion

    #region Announcements

    public void AnnounceInventoryFull()
    {
        if (_isConnected && _config.AnnounceEvents)
        {
            _client.SendMessage(_config.Channel, "Inventory is FULL! Need to offload packets.");
        }
    }

    public void AnnounceMiningStarted(ulong sourceId)
    {
        if (_isConnected && _config.AnnounceEvents)
        {
            _client.SendMessage(_config.Channel, $"Started mining source {sourceId}");
        }
    }

    public void AnnounceMiningComplete(ulong sourceId, uint totalExtracted)
    {
        if (_isConnected && _config.AnnounceEvents)
        {
            _client.SendMessage(_config.Channel, $"Mining complete! Extracted {totalExtracted} packets from source {sourceId}");
        }
    }

    public void AnnounceSourceDiscovered(ulong sourceId, uint packetCount)
    {
        if (_isConnected && _config.AnnounceEvents)
        {
            _client.SendMessage(_config.Channel, $"New source discovered! Source {sourceId} with {packetCount} packets");
        }
    }

    #endregion

    public void Dispose()
    {
        Disconnect();
    }
}
