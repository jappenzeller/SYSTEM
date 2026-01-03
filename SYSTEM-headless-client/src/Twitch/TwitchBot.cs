using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using SpacetimeDB.Types;
using SYSTEM.HeadlessClient.AI;
using SYSTEM.HeadlessClient.Behavior;
using SYSTEM.HeadlessClient.Connection;
using SYSTEM.HeadlessClient.Inventory;
using SYSTEM.HeadlessClient.Mining;
using SYSTEM.HeadlessClient.Sensing;
using SYSTEM.HeadlessClient.World;

namespace SYSTEM.HeadlessClient.Twitch;

/// <summary>
/// Twitch IRC bot for chat commands and event announcements.
/// All commands use the unified !qai format for QAI personality.
/// </summary>
public class TwitchBot : IDisposable
{
    private readonly TwitchConfig _config;
    private readonly TwitchClient _client;
    private readonly SpacetimeConnection? _connection;
    private readonly WorldManager? _worldManager;
    private readonly SourceDetector? _sourceDetector;
    private readonly MiningController? _miningController;
    private readonly InventoryTracker? _inventoryTracker;
    private readonly BehaviorStateMachine? _behaviorStateMachine;
    private readonly DateTime _startTime;
    private readonly QaiChatHandler? _chatHandler;

    private bool _isConnected;

    // Privileged users who can control QAI
    private static readonly HashSet<string> PrivilegedUsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "superstringman",  // Architect
        "exelbox"
    };

    public TwitchBot(
        TwitchConfig config,
        SpacetimeConnection? connection,
        WorldManager? worldManager,
        SourceDetector? sourceDetector,
        MiningController? miningController,
        InventoryTracker? inventoryTracker,
        BehaviorStateMachine? behaviorStateMachine,
        DateTime startTime,
        QaiChatHandler? chatHandler = null)
    {
        _config = config;
        _connection = connection;
        _worldManager = worldManager;
        _sourceDetector = sourceDetector;
        _miningController = miningController;
        _inventoryTracker = inventoryTracker;
        _behaviorStateMachine = behaviorStateMachine;
        _startTime = startTime;
        _chatHandler = chatHandler;

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
            _client.SendMessage(e.Channel, "I am here. Type !qai to speak with me.");
        }
    }

    private async void OnMessageReceived(object? sender, OnMessageReceivedArgs e)
    {
        var message = e.ChatMessage.Message.Trim();
        var messageLower = message.ToLower();

        // Debug: log all messages received
        Console.WriteLine($"[Twitch] #{e.ChatMessage.Channel} <{e.ChatMessage.Username}>: {message}");

        // Only respond to !qai commands (unified command structure)
        if (!messageLower.StartsWith("!qai")) return;

        var username = e.ChatMessage.Username;
        var isPrivileged = PrivilegedUsers.Contains(username);

        // Extract content after "!qai " (preserve original case for chat messages)
        var content = message.Length > 4 ? message.Substring(4).Trim() : "";
        var contentLower = content.ToLower();

        // Check if this user is in the game (player presence awareness)
        var player = FindPlayerByName(username);
        var isInGame = player != null;

        // Handle the command/message (async for AI responses)
        var response = await HandleQaiCommandAsync(contentLower, content, username, isPrivileged, isInGame, player, e.ChatMessage.Channel);

        if (!string.IsNullOrEmpty(response))
        {
            _client.SendMessage(e.ChatMessage.Channel, response);
        }
    }

    /// <summary>
    /// Find a player in the game by their name (case-insensitive).
    /// </summary>
    private Player? FindPlayerByName(string name)
    {
        var conn = _connection?.Conn;
        if (conn == null) return null;

        foreach (var player in conn.Db.Player.Iter())
        {
            if (player.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return player;
        }
        return null;
    }

    /// <summary>
    /// Process !qai commands with player presence awareness.
    /// </summary>
    private async Task<string> HandleQaiCommandAsync(string contentLower, string originalContent, string username, bool isPrivileged, bool isInGame, Player? player, string channel)
    {
        // Special keyword commands (case-insensitive matching)
        switch (contentLower)
        {
            case "":
            case "help":
                return isPrivileged
                    ? "I understand: status, sources, mine, stop, scan, walk <dist>, rotate <deg>, reset, inventory, position"
                    : "I understand: inventory, position, or just speak to me.";

            // Public commands
            case "inventory":
                return GetInventoryResponse();
            case "position":
                return GetPositionResponse();
        }

        // Privileged commands (exact match or prefix match)
        if (isPrivileged)
        {
            if (contentLower == "status")
                return GetStatusResponse();
            if (contentLower == "sources")
                return GetSourcesResponse();
            if (contentLower == "mine")
                return HandleMineCommand();
            if (contentLower == "stop")
                return HandleStopCommand();
            if (contentLower == "scan")
                return HandleScanCommand();
            if (contentLower == "walkstop")
                return HandleWalkStopCommand();
            if (contentLower == "reset")
                return HandleResetCommand();

            // Commands with arguments
            if (contentLower.StartsWith("walk "))
            {
                var args = contentLower.Substring(5).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return HandleWalkCommand(args);
            }
            if (contentLower.StartsWith("rotate "))
            {
                var args = contentLower.Substring(7).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return HandleRotateCommand(args);
            }
            if (contentLower.StartsWith("join "))
            {
                var args = contentLower.Substring(5).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return HandleJoinCommand(args);
            }
            if (contentLower.StartsWith("leave "))
            {
                var args = contentLower.Substring(6).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return HandleLeaveCommand(args);
            }
        }

        // Default: AI-powered chat response with player presence awareness
        return await GeneratePresenceAwareResponseAsync(username, isInGame, player, originalContent);
    }

    /// <summary>
    /// Generate a response based on whether the user is in the game, using AI if available.
    /// </summary>
    private async Task<string> GeneratePresenceAwareResponseAsync(string username, bool isInGame, Player? player, string message)
    {
        // If chat handler is available, use AI-powered response
        if (_chatHandler != null)
        {
            try
            {
                return await _chatHandler.GenerateResponseAsync(username, message, isInGame, player);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Twitch] AI response failed: {ex.Message}");
                // Fall through to default response
            }
        }

        // Fallback: Simple presence-aware response
        if (isInGame && player != null)
        {
            // User is in the game - QAI can "see" them
            var pos = player.Position;
            return $"Hello {username}. I see you at ({pos.X:F0}, {pos.Y:F0}, {pos.Z:F0}).";
        }
        else
        {
            // User is not in the game - QAI can only "hear" them
            return $"I hear your voice, {username}, but I cannot see you in the lattice. Are you outside?";
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
        if (_inventoryTracker?.IsFull == true) return "Inventory full, can't mine";

        // Stop any stale mining session first (e.g., source left range)
        if (_miningController.IsMining)
        {
            _miningController.StopMining();
        }

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

    private string HandleJoinCommand(string[] args)
    {
        if (args.Length == 0) return "Usage: !join <channel>";

        var channel = args[0].TrimStart('#').ToLower();
        if (string.IsNullOrWhiteSpace(channel)) return "Invalid channel name";

        try
        {
            _client.JoinChannel(channel);
            Console.WriteLine($"[Twitch] Joining channel #{channel}");
            return $"Joining #{channel}...";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Twitch] Failed to join #{channel}: {ex.Message}");
            return $"Failed to join #{channel}: {ex.Message}";
        }
    }

    private string HandleLeaveCommand(string[] args)
    {
        if (args.Length == 0) return "Usage: !leave <channel>";

        var channel = args[0].TrimStart('#').ToLower();
        if (string.IsNullOrWhiteSpace(channel)) return "Invalid channel name";

        // Don't leave the home channel
        if (channel.Equals(_config.Channel, StringComparison.OrdinalIgnoreCase))
            return $"Can't leave home channel #{_config.Channel}";

        try
        {
            _client.LeaveChannel(channel);
            Console.WriteLine($"[Twitch] Leaving channel #{channel}");
            return $"Left #{channel}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Twitch] Failed to leave #{channel}: {ex.Message}");
            return $"Failed to leave #{channel}: {ex.Message}";
        }
    }

    private string HandleScanCommand()
    {
        if (_sourceDetector == null) return "Source detector not available";

        _sourceDetector.ScanForSources();
        var count = _sourceDetector.SourcesInRange.Count;
        return $"Scan complete: {count} source{(count == 1 ? "" : "s")} in range";
    }

    private string HandleWalkCommand(string[] args)
    {
        if (_worldManager == null) return "World manager not available";

        if (args.Length == 0) return "Usage: !walk <distance> (e.g., !walk 50)";

        if (!float.TryParse(args[0], out float distance))
            return "Invalid distance. Usage: !walk <distance>";

        _worldManager.StartWalkingForDistance(1, 0, distance);
        return $"Walking forward {distance:F0} units...";
    }

    private string HandleWalkStopCommand()
    {
        if (_worldManager == null) return "World manager not available";

        _worldManager.StopWalking();
        var pos = _worldManager.Position;
        return $"Stopped at ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})";
    }

    private string HandleRotateCommand(string[] args)
    {
        if (_worldManager == null) return "World manager not available";

        if (args.Length == 0) return "Usage: !rotate <degrees> (e.g., !rotate 90)";

        if (!float.TryParse(args[0], out float degrees))
            return "Invalid degrees. Usage: !rotate <degrees>";

        _worldManager.Rotate(degrees);
        _worldManager.SendPositionUpdate();
        return $"Rotated {degrees:F0}°";
    }

    private string HandleResetCommand()
    {
        if (_worldManager == null) return "World manager not available";

        _worldManager.ResetToStart();
        _worldManager.SendPositionUpdate();
        var pos = _worldManager.Position;
        return $"Reset to start: ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})";
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
