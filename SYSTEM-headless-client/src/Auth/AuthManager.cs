using SpacetimeDB;
using SpacetimeDB.ClientApi;
using SpacetimeDB.Types;
using SYSTEM.HeadlessClient.Config;
using SYSTEM.HeadlessClient.Connection;

namespace SYSTEM.HeadlessClient.Auth;

public class AuthManager
{
    private readonly SpacetimeConnection _connection;
    private readonly ClientConfig _config;
    private readonly TokenStorage _tokenStorage;

    private TaskCompletionSource<bool>? _loginTcs;
    private TaskCompletionSource<bool>? _playerTcs;

    public bool IsLoggedIn { get; private set; }
    public bool HasPlayer { get; private set; }
    public Player? LocalPlayer { get; private set; }

    public AuthManager(SpacetimeConnection connection, ClientConfig config, TokenStorage tokenStorage)
    {
        _connection = connection;
        _config = config;
        _tokenStorage = tokenStorage;
    }

    public void Initialize()
    {
        var conn = _connection.Conn;
        if (conn == null) throw new InvalidOperationException("Not connected");

        // Subscribe to table events
        conn.Db.SessionResult.OnInsert += OnSessionResultInsert;
        conn.Db.Player.OnInsert += OnPlayerInsert;
        conn.Db.Player.OnDelete += OnPlayerDelete;

        // Subscribe to reducer callbacks for error/success handling
        conn.Reducers.OnLogin += OnLoginResult;
        conn.Reducers.OnRegisterAccount += OnRegisterResult;
        conn.Reducers.OnCreatePlayer += OnCreatePlayerResult;
    }

    /// <summary>
    /// Attempt login. Returns true if successful.
    /// </summary>
    public async Task<bool> LoginAsync(string username, string pin, CancellationToken ct = default)
    {
        var conn = _connection.Conn;
        if (conn == null) return false;

        _loginTcs = new TaskCompletionSource<bool>();

        Console.WriteLine($"[Auth] Logging in as {username}...");
        conn.Reducers.Login(username, pin, _config.QAI.DeviceInfo);

        // Wait for SessionResult or timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            var result = await _loginTcs.Task.WaitAsync(cts.Token);
            return result;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[Auth] Login timed out");
            return false;
        }
    }

    /// <summary>
    /// Register new account. Returns true if successful.
    /// </summary>
    public async Task<bool> RegisterAsync(string username, string displayName, string pin, CancellationToken ct = default)
    {
        var conn = _connection.Conn;
        if (conn == null) return false;

        _loginTcs = new TaskCompletionSource<bool>();

        Console.WriteLine($"[Auth] Registering new account: {username} ({displayName})...");
        conn.Reducers.RegisterAccount(username, displayName, pin);

        // Wait for registration to complete
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            // Wait briefly for registration to complete
            await Task.Delay(1000, cts.Token);

            // Now login
            return await LoginAsync(username, pin, cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[Auth] Registration timed out");
            return false;
        }
    }

    /// <summary>
    /// Create player after login. Returns true if successful.
    /// </summary>
    public async Task<bool> CreatePlayerAsync(string playerName, CancellationToken ct = default)
    {
        var conn = _connection.Conn;
        if (conn == null || !IsLoggedIn) return false;

        // Check if player already exists for this identity
        var existingPlayer = conn.Db.Player.Identity.Find(conn.Identity!.Value);
        if (existingPlayer != null)
        {
            Console.WriteLine($"[Auth] Player already exists: {existingPlayer.Name}");
            LocalPlayer = existingPlayer;
            HasPlayer = true;
            return true;
        }

        _playerTcs = new TaskCompletionSource<bool>();

        Console.WriteLine($"[Auth] Creating player: {playerName}...");
        conn.Reducers.CreatePlayer(playerName);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            return await _playerTcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[Auth] Player creation timed out");
            return false;
        }
    }

    #region Event Handlers

    private void OnSessionResultInsert(EventContext ctx, SessionResult result)
    {
        var conn = _connection.Conn;
        if (conn == null || result.Identity != conn.Identity) return;

        Console.WriteLine($"[Auth] Session created! Token: {result.SessionToken[..Math.Min(8, result.SessionToken.Length)]}...");
        _tokenStorage.SaveToken(result.SessionToken);
        IsLoggedIn = true;
        _loginTcs?.TrySetResult(true);
    }

    private void OnPlayerInsert(EventContext ctx, Player player)
    {
        var conn = _connection.Conn;
        if (conn == null || player.Identity != conn.Identity) return;

        Console.WriteLine($"[Auth] Player created: {player.Name} (ID: {player.PlayerId})");
        Console.WriteLine($"[Auth] Position: ({player.Position.X:F1}, {player.Position.Y:F1}, {player.Position.Z:F1})");
        Console.WriteLine($"[Auth] World: ({player.CurrentWorld.X}, {player.CurrentWorld.Y}, {player.CurrentWorld.Z})");

        LocalPlayer = player;
        HasPlayer = true;
        _playerTcs?.TrySetResult(true);
    }

    private void OnPlayerDelete(EventContext ctx, Player player)
    {
        if (LocalPlayer?.PlayerId == player.PlayerId)
        {
            Console.WriteLine($"[Auth] Local player deleted!");
            LocalPlayer = null;
            HasPlayer = false;
        }
    }

    private void OnLoginResult(ReducerEventContext ctx, string username, string pin, string deviceInfo)
    {
        // Check if the login failed using pattern matching
        switch (ctx.Event.Status)
        {
            case Status.Failed(var reason):
                Console.WriteLine($"[Auth] Login failed: {reason}");
                _loginTcs?.TrySetResult(false);
                break;
        }
    }

    private void OnRegisterResult(ReducerEventContext ctx, string username, string displayName, string pin)
    {
        // Check if registration failed using pattern matching
        switch (ctx.Event.Status)
        {
            case Status.Failed(var reason):
                Console.WriteLine($"[Auth] Registration failed: {reason}");
                _loginTcs?.TrySetResult(false);
                break;
            case Status.Committed:
                Console.WriteLine($"[Auth] Registration successful for {username}");
                break;
        }
    }

    private void OnCreatePlayerResult(ReducerEventContext ctx, string name)
    {
        // Check if player creation failed using pattern matching
        switch (ctx.Event.Status)
        {
            case Status.Failed(var reason):
                Console.WriteLine($"[Auth] Player creation failed: {reason}");
                _playerTcs?.TrySetResult(false);
                break;
        }
    }

    #endregion
}
