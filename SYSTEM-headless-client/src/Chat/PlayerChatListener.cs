using SpacetimeDB.Types;
using SYSTEM.HeadlessClient.Connection;
using SYSTEM.HeadlessClient.World;

namespace SYSTEM.HeadlessClient.Chat;

/// <summary>
/// Listens for in-game player chat messages and processes them if the player is nearby.
/// Players within 15 units of QAI will get their messages processed as !qai commands.
/// </summary>
public class PlayerChatListener
{
    private const float PROXIMITY_RANGE = 15.0f;

    private readonly SpacetimeConnection _connection;
    private readonly WorldManager _worldManager;
    private readonly QaiCommandHandler _commandHandler;
    private readonly ulong _qaiPlayerId;

    public PlayerChatListener(
        SpacetimeConnection connection,
        WorldManager worldManager,
        QaiCommandHandler commandHandler,
        ulong qaiPlayerId)
    {
        _connection = connection;
        _worldManager = worldManager;
        _commandHandler = commandHandler;
        _qaiPlayerId = qaiPlayerId;
    }

    public void Initialize()
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        // Subscribe to player chat messages
        conn.Db.PlayerChatMessage.OnInsert += OnPlayerChatInsert;
        Console.WriteLine("[PlayerChatListener] Initialized - listening for in-game player chat");
    }

    private async void OnPlayerChatInsert(EventContext ctx, PlayerChatMessage message)
    {
        // Don't respond to our own messages
        if (message.SenderPlayerId == _qaiPlayerId)
        {
            return;
        }

        // Check if player is within range
        var qaiPosition = _worldManager.Position;
        var playerPosition = new DbVector3(message.PositionX, message.PositionY, message.PositionZ);

        float distance = CalculateDistance(qaiPosition, playerPosition);

        if (distance > PROXIMITY_RANGE)
        {
            Console.WriteLine($"[PlayerChatListener] {message.SenderName} is too far ({distance:F1} units > {PROXIMITY_RANGE})");
            return;
        }

        Console.WriteLine($"[PlayerChatListener] {message.SenderName} is within range ({distance:F1} units): \"{message.Content}\"");

        // Process as if it were a !qai command
        // Create a chat message that looks like "!qai <content>"
        var chatMessage = new ChatMessage
        {
            Platform = "InGame",
            UserId = message.SenderPlayerId.ToString(),
            Username = message.SenderName,
            Content = $"!qai {message.Content}",
            ChannelId = "world",
            ChannelName = "World Chat",
            IsSubscriber = false,
            IsModerator = false
        };

        try
        {
            // Use privileged mode for in-game proximity chat (same access as subscribers)
            var response = await _commandHandler.HandleAsync(chatMessage, isPrivileged: true);
            Console.WriteLine($"[PlayerChatListener] Response to {message.SenderName}: {response}");

            // The QaiCommandHandler.HandleAsync already broadcasts to game via BroadcastToGame()
            // so the chat bubble will appear automatically
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PlayerChatListener] Error handling message: {ex.Message}");
        }
    }

    private float CalculateDistance(DbVector3 a, DbVector3 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public void Dispose()
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        conn.Db.PlayerChatMessage.OnInsert -= OnPlayerChatInsert;
    }
}
