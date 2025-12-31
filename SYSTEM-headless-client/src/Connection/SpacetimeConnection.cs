using SpacetimeDB;
using SpacetimeDB.Types;
using SYSTEM.HeadlessClient.Config;

namespace SYSTEM.HeadlessClient.Connection;

public class SpacetimeConnection
{
    private readonly ClientConfig _config;
    private DbConnection? _conn;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public DbConnection? Conn => _conn;
    public Identity? Identity => _conn?.Identity;

    // Events
    public event Action<string>? OnConnected; // token
    public event Action<string>? OnDisconnected;
    public event Action<string>? OnError;
    public event Action? OnSubscribed;

    public SpacetimeConnection(ClientConfig config)
    {
        _config = config;
    }

    public void Connect()
    {
        if (State != ConnectionState.Disconnected)
        {
            Console.WriteLine($"[Connection] Already in state {State}, cannot connect");
            return;
        }

        State = ConnectionState.Connecting;
        var url = _config.SpacetimeDB.GetConnectionUrl();
        var module = _config.SpacetimeDB.ModuleName;

        Console.WriteLine($"[Connection] Connecting to {url}/{module}...");

        _conn = DbConnection.Builder()
            .WithUri(url)
            .WithModuleName(module)
            .OnConnect(HandleConnected)
            .OnConnectError(HandleConnectError)
            .OnDisconnect(HandleDisconnected)
            .Build();
    }

    private void HandleConnected(DbConnection conn, Identity identity, string token)
    {
        Console.WriteLine($"[Connection] Connected! Identity: {identity}");
        State = ConnectionState.Connected;

        // Subscribe to all tables
        State = ConnectionState.Subscribing;
        conn.SubscriptionBuilder()
            .OnApplied(HandleSubscriptionApplied)
            .OnError(HandleSubscriptionError)
            .SubscribeToAllTables();

        OnConnected?.Invoke(token);
    }

    private void HandleSubscriptionApplied(SubscriptionEventContext ctx)
    {
        Console.WriteLine("[Connection] Subscription applied - tables synchronized");
        State = ConnectionState.Subscribed;
        OnSubscribed?.Invoke();
    }

    private void HandleSubscriptionError(ErrorContext ctx, Exception error)
    {
        Console.WriteLine($"[Connection] Subscription error: {error.Message}");
        State = ConnectionState.Error;
        OnError?.Invoke(error.Message);
    }

    private void HandleConnectError(Exception error)
    {
        Console.WriteLine($"[Connection] Connection error: {error.Message}");
        State = ConnectionState.Error;
        OnError?.Invoke(error.Message);
    }

    private void HandleDisconnected(DbConnection conn, Exception? error)
    {
        var reason = error?.Message ?? "Connection closed";
        Console.WriteLine($"[Connection] Disconnected: {reason}");
        State = ConnectionState.Disconnected;
        OnDisconnected?.Invoke(reason);
    }

    public void Disconnect()
    {
        if (_conn != null && _conn.IsActive)
        {
            _conn.Disconnect();
        }
        _conn = null;
        State = ConnectionState.Disconnected;
    }

    /// <summary>
    /// Must be called regularly to process SpacetimeDB messages
    /// </summary>
    public void Update()
    {
        _conn?.FrameTick();
    }
}
