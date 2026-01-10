using SpacetimeDB.Types;
using SYSTEM.HeadlessClient.AI;
using SYSTEM.HeadlessClient.Behavior;
using SYSTEM.HeadlessClient.Connection;
using SYSTEM.HeadlessClient.Inventory;
using SYSTEM.HeadlessClient.Mining;
using SYSTEM.HeadlessClient.Sensing;
using SYSTEM.HeadlessClient.World;

namespace SYSTEM.HeadlessClient.Chat;

/// <summary>
/// Platform-agnostic command handler for QAI.
/// Extracted from TwitchBot for cross-platform support.
/// </summary>
public class QaiCommandHandler
{
    private readonly SpacetimeConnection? _connection;
    private readonly WorldManager? _worldManager;
    private readonly SourceDetector? _sourceDetector;
    private readonly MiningController? _miningController;
    private readonly InventoryTracker? _inventoryTracker;
    private readonly BehaviorStateMachine? _behaviorStateMachine;
    private readonly QaiChatHandler? _chatHandler;
    private readonly DateTime _startTime;

    public QaiCommandHandler(
        SpacetimeConnection? connection,
        WorldManager? worldManager,
        SourceDetector? sourceDetector,
        MiningController? miningController,
        InventoryTracker? inventoryTracker,
        BehaviorStateMachine? behaviorStateMachine,
        QaiChatHandler? chatHandler,
        DateTime startTime)
    {
        _connection = connection;
        _worldManager = worldManager;
        _sourceDetector = sourceDetector;
        _miningController = miningController;
        _inventoryTracker = inventoryTracker;
        _behaviorStateMachine = behaviorStateMachine;
        _chatHandler = chatHandler;
        _startTime = startTime;
    }

    /// <summary>
    /// Handle a chat message and return a response.
    /// </summary>
    public async Task<string> HandleAsync(ChatMessage message, bool isPrivileged)
    {
        // Extract content after "!qai " (preserve original case for chat messages)
        var content = message.Content.Length > 4 ? message.Content.Substring(4).Trim() : "";
        var contentLower = content.ToLower();

        // Check if this user is in the game (player presence awareness)
        var player = FindPlayerByName(message.Username);
        var isInGame = player != null;

        return await HandleQaiCommandAsync(contentLower, content, message.Username, isPrivileged, isInGame, player);
    }

    /// <summary>
    /// Find a player in the game by their name (case-insensitive).
    /// </summary>
    public Player? FindPlayerByName(string name)
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
    private async Task<string> HandleQaiCommandAsync(
        string contentLower,
        string originalContent,
        string username,
        bool isPrivileged,
        bool isInGame,
        Player? player)
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
        }

        // Default: AI-powered chat response with player presence awareness
        return await GeneratePresenceAwareResponseAsync(username, isInGame, player, originalContent);
    }

    /// <summary>
    /// Generate a response based on whether the user is in the game, using AI if available.
    /// </summary>
    private async Task<string> GeneratePresenceAwareResponseAsync(
        string username,
        bool isInGame,
        Player? player,
        string message)
    {
        string response;

        // If chat handler is available, use AI-powered response
        if (_chatHandler != null)
        {
            try
            {
                response = await _chatHandler.GenerateResponseAsync(username, message, isInGame, player);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Chat] AI response failed: {ex.Message}");
                response = GetFallbackResponse(username, isInGame, player);
            }
        }
        else
        {
            response = GetFallbackResponse(username, isInGame, player);
        }

        // Broadcast to in-game display (chat bubbles)
        BroadcastToGame(response);

        return response;
    }

    /// <summary>
    /// Get a fallback response when AI is not available.
    /// </summary>
    private string GetFallbackResponse(string username, bool isInGame, Player? player)
    {
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

    /// <summary>
    /// Broadcast a message to in-game chat bubbles via SpacetimeDB.
    /// </summary>
    private void BroadcastToGame(string message)
    {
        try
        {
            _connection?.Conn?.Reducers.BroadcastChatMessage(message);
            Console.WriteLine($"[Chat] Broadcast to game: {message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Chat] Failed to broadcast to game: {ex.Message}");
        }
    }

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

        if (args.Length == 0) return "Usage: !qai walk <distance> (e.g., !qai walk 50)";

        if (!float.TryParse(args[0], out float distance))
            return "Invalid distance. Usage: !qai walk <distance>";

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

        if (args.Length == 0) return "Usage: !qai rotate <degrees> (e.g., !qai rotate 90)";

        if (!float.TryParse(args[0], out float degrees))
            return "Invalid degrees. Usage: !qai rotate <degrees>";

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
}
