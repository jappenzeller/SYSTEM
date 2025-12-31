# Claude Code Prompt: Headless Client Phase 1 - Connection & Authentication

## Objective

Create a standalone .NET 8 console application that connects to SpacetimeDB, authenticates, and creates a player. This is Phase 1 of the bot/AI character system, establishing the foundation for AWS Fargate deployment.

## Success Criteria

1. Console app connects to local SpacetimeDB (`127.0.0.1:3000`)
2. Console app connects to test server (`maincloud.spacetimedb.com`)
3. Handles login with existing account (username + 4-digit PIN)
4. Handles new account registration
5. Creates player after successful login
6. Bot player visible in Unity client
7. Graceful disconnect handling
8. File-based token persistence (not PlayerPrefs)

---

## Project Structure

Create new project at repository root level (sibling to SYSTEM-server and SYSTEM-client-3d):

```
SYSTEM-headless-client/
├── SYSTEM.HeadlessClient.csproj
├── appsettings.json                 # Default configuration
├── appsettings.Development.json     # Local dev overrides
├── src/
│   ├── Program.cs                   # Entry point, CLI parsing
│   ├── HeadlessClient.cs            # Main client orchestrator
│   ├── Connection/
│   │   ├── SpacetimeConnection.cs   # DbConnection wrapper
│   │   └── ConnectionState.cs       # State enum
│   ├── Auth/
│   │   ├── AuthManager.cs           # Login/register logic
│   │   └── TokenStorage.cs          # File-based token persistence
│   └── Config/
│       └── ClientConfig.cs          # Configuration model
├── autogen/                         # Generated SpacetimeDB bindings
│   └── (copy from Unity project or generate fresh)
└── Dockerfile                       # For Phase 7, but stub it now
```

---

## Step 1: Project Setup

### 1.1 Create .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>SYSTEM.HeadlessClient</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <!-- SpacetimeDB C# SDK - match version used in Unity project -->
    <PackageReference Include="SpacetimeDB.ClientSDK" Version="1.1.1" />
    
    <!-- Configuration -->
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.CommandLine" Version="8.0.0" />
  </ItemGroup>
</Project>
```

### 1.2 Generate autogen bindings

Run from SYSTEM-server directory:
```bash
spacetime generate --lang cs --out-dir ../SYSTEM-headless-client/autogen
```

Or copy from Unity project:
```bash
cp -r SYSTEM-client-3d/Assets/Scripts/autogen/* SYSTEM-headless-client/autogen/
```

**Note:** The autogen files include table definitions (Player, Account, SessionResult, etc.) and reducer bindings.

---

## Step 2: Configuration

### 2.1 appsettings.json

```json
{
  "SpacetimeDB": {
    "ServerUrl": "ws://127.0.0.1:3000",
    "ModuleName": "system",
    "UseSSL": false
  },
  "Bot": {
    "Username": "",
    "Pin": "",
    "DisplayName": "HeadlessBot",
    "DeviceInfo": "HeadlessClient/1.0"
  },
  "Storage": {
    "TokenFilePath": ".bot-token"
  }
}
```

### 2.2 ClientConfig.cs

```csharp
namespace SYSTEM.HeadlessClient.Config;

public class ClientConfig
{
    public SpacetimeDBConfig SpacetimeDB { get; set; } = new();
    public BotConfig Bot { get; set; } = new();
    public StorageConfig Storage { get; set; } = new();
}

public class SpacetimeDBConfig
{
    public string ServerUrl { get; set; } = "ws://127.0.0.1:3000";
    public string ModuleName { get; set; } = "system";
    public bool UseSSL { get; set; } = false;
    
    public string GetConnectionUrl()
    {
        // Handle maincloud shorthand
        if (ServerUrl.Contains("maincloud"))
            return $"wss://{ServerUrl}";
        return UseSSL ? ServerUrl.Replace("ws://", "wss://") : ServerUrl;
    }
}

public class BotConfig
{
    public string Username { get; set; } = "";
    public string Pin { get; set; } = "";
    public string DisplayName { get; set; } = "HeadlessBot";
    public string DeviceInfo { get; set; } = "HeadlessClient/1.0";
}

public class StorageConfig
{
    public string TokenFilePath { get; set; } = ".bot-token";
}
```

### 2.3 Environment Variable Overrides

Support these environment variables (for Docker/Fargate):
- `SPACETIMEDB__SERVERURL` → SpacetimeDB.ServerUrl
- `SPACETIMEDB__MODULENAME` → SpacetimeDB.ModuleName
- `BOT__USERNAME` → Bot.Username
- `BOT__PIN` → Bot.Pin
- `BOT__DISPLAYNAME` → Bot.DisplayName

---

## Step 3: Token Storage

### 3.1 TokenStorage.cs

File-based replacement for Unity's PlayerPrefs:

```csharp
namespace SYSTEM.HeadlessClient.Auth;

public class TokenStorage
{
    private readonly string _tokenFilePath;
    
    public TokenStorage(string tokenFilePath)
    {
        _tokenFilePath = tokenFilePath;
    }
    
    public void SaveToken(string token)
    {
        File.WriteAllText(_tokenFilePath, token);
        Console.WriteLine($"[TokenStorage] Token saved to {_tokenFilePath}");
    }
    
    public string? LoadToken()
    {
        if (!File.Exists(_tokenFilePath))
            return null;
        
        var token = File.ReadAllText(_tokenFilePath).Trim();
        return string.IsNullOrEmpty(token) ? null : token;
    }
    
    public void ClearToken()
    {
        if (File.Exists(_tokenFilePath))
            File.Delete(_tokenFilePath);
    }
}
```

---

## Step 4: Connection Management

### 4.1 ConnectionState.cs

```csharp
namespace SYSTEM.HeadlessClient.Connection;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Subscribing,
    Subscribed,
    Authenticating,
    Authenticated,
    CreatingPlayer,
    Ready,
    Error
}
```

### 4.2 SpacetimeConnection.cs

```csharp
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
    public event Action? OnConnected;
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
        
        OnConnected?.Invoke();
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
        _conn?.Disconnect();
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
```

---

## Step 5: Authentication Manager

### 5.1 AuthManager.cs

Handles login, registration, and session management:

```csharp
using SpacetimeDB;
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
        
        // Subscribe to reducer results for error handling
        conn.Reducers.OnLoginFailed += OnLoginFailed;
        conn.Reducers.OnRegisterAccountFailed += OnRegisterFailed;
        conn.Reducers.OnCreatePlayerFailed += OnCreatePlayerFailed;
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
        conn.Reducers.Login(username, pin, _config.Bot.DeviceInfo);
        
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
        
        // After registration, still need to login
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
        
        Console.WriteLine($"[Auth] Session created! Token: {result.SessionToken[..8]}...");
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
    
    private void OnLoginFailed(ReducerEventContext ctx, string username, string pin, string deviceInfo, string error)
    {
        Console.WriteLine($"[Auth] Login failed: {error}");
        _loginTcs?.TrySetResult(false);
    }
    
    private void OnRegisterFailed(ReducerEventContext ctx, string username, string displayName, string pin, string error)
    {
        Console.WriteLine($"[Auth] Registration failed: {error}");
        _loginTcs?.TrySetResult(false);
    }
    
    private void OnCreatePlayerFailed(ReducerEventContext ctx, string name, string error)
    {
        Console.WriteLine($"[Auth] Player creation failed: {error}");
        _playerTcs?.TrySetResult(false);
    }
    
    #endregion
}
```

---

## Step 6: Main Client Orchestrator

### 6.1 HeadlessClient.cs

```csharp
using SYSTEM.HeadlessClient.Auth;
using SYSTEM.HeadlessClient.Config;
using SYSTEM.HeadlessClient.Connection;

namespace SYSTEM.HeadlessClient;

public class HeadlessClient
{
    private readonly ClientConfig _config;
    private readonly SpacetimeConnection _connection;
    private readonly AuthManager _auth;
    private readonly TokenStorage _tokenStorage;
    
    private readonly CancellationTokenSource _cts = new();
    private bool _running;
    
    public HeadlessClient(ClientConfig config)
    {
        _config = config;
        _tokenStorage = new TokenStorage(config.Storage.TokenFilePath);
        _connection = new SpacetimeConnection(config);
        _auth = new AuthManager(_connection, config, _tokenStorage);
        
        // Wire up events
        _connection.OnSubscribed += OnSubscribed;
        _connection.OnError += OnError;
        _connection.OnDisconnected += OnDisconnected;
    }
    
    public async Task RunAsync()
    {
        Console.WriteLine("=== SYSTEM Headless Client ===");
        Console.WriteLine($"Server: {_config.SpacetimeDB.ServerUrl}");
        Console.WriteLine($"Module: {_config.SpacetimeDB.ModuleName}");
        Console.WriteLine();
        
        _running = true;
        
        // Connect
        _connection.Connect();
        
        // Main loop - process SpacetimeDB messages
        while (_running && !_cts.Token.IsCancellationRequested)
        {
            _connection.Update();
            await Task.Delay(16, _cts.Token); // ~60 updates/sec
        }
    }
    
    private async void OnSubscribed()
    {
        Console.WriteLine("[Client] Subscribed to tables, starting authentication...");
        
        // Initialize auth manager with table callbacks
        _auth.Initialize();
        
        // Attempt login
        var username = _config.Bot.Username;
        var pin = _config.Bot.Pin;
        var displayName = _config.Bot.DisplayName;
        
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(pin))
        {
            Console.WriteLine("[Client] ERROR: Username and PIN required in config");
            Stop();
            return;
        }
        
        bool loginSuccess = await _auth.LoginAsync(username, pin, _cts.Token);
        
        if (!loginSuccess)
        {
            Console.WriteLine("[Client] Login failed, attempting registration...");
            loginSuccess = await _auth.RegisterAsync(username, displayName, pin, _cts.Token);
        }
        
        if (!loginSuccess)
        {
            Console.WriteLine("[Client] Authentication failed completely");
            Stop();
            return;
        }
        
        // Create player if needed
        bool playerSuccess = await _auth.CreatePlayerAsync(displayName, _cts.Token);
        
        if (!playerSuccess)
        {
            Console.WriteLine("[Client] Player creation failed");
            Stop();
            return;
        }
        
        Console.WriteLine();
        Console.WriteLine("=== BOT READY ===");
        Console.WriteLine($"Player: {_auth.LocalPlayer?.Name}");
        Console.WriteLine($"Player ID: {_auth.LocalPlayer?.PlayerId}");
        Console.WriteLine("Press Ctrl+C to stop");
        Console.WriteLine();
    }
    
    private void OnError(string error)
    {
        Console.WriteLine($"[Client] Error: {error}");
    }
    
    private void OnDisconnected(string reason)
    {
        Console.WriteLine($"[Client] Disconnected: {reason}");
        _running = false;
    }
    
    public void Stop()
    {
        Console.WriteLine("[Client] Stopping...");
        _running = false;
        _cts.Cancel();
        _connection.Disconnect();
    }
}
```

---

## Step 7: Entry Point

### 7.1 Program.cs

```csharp
using Microsoft.Extensions.Configuration;
using SYSTEM.HeadlessClient;
using SYSTEM.HeadlessClient.Config;

// Build configuration
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var clientConfig = new ClientConfig();
config.Bind(clientConfig);

// Override from command line for convenience
if (args.Length >= 2)
{
    clientConfig.Bot.Username = args[0];
    clientConfig.Bot.Pin = args[1];
}
if (args.Length >= 3)
{
    clientConfig.Bot.DisplayName = args[2];
}

// Validate config
if (string.IsNullOrEmpty(clientConfig.Bot.Username) || string.IsNullOrEmpty(clientConfig.Bot.Pin))
{
    Console.WriteLine("Usage: SYSTEM.HeadlessClient <username> <pin> [display_name]");
    Console.WriteLine("  Or set BOT__USERNAME and BOT__PIN environment variables");
    Console.WriteLine("  Or configure in appsettings.json");
    return 1;
}

// Create and run client
var client = new HeadlessClient(clientConfig);

// Handle Ctrl+C
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    client.Stop();
};

try
{
    await client.RunAsync();
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"Fatal error: {ex.Message}");
    return 1;
}
```

---

## Step 8: Dockerfile Stub

Prepare for Phase 7, but don't need to test yet:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SYSTEM-headless-client/*.csproj ./SYSTEM-headless-client/
RUN dotnet restore SYSTEM-headless-client/SYSTEM.HeadlessClient.csproj

COPY SYSTEM-headless-client/ ./SYSTEM-headless-client/
RUN dotnet publish SYSTEM-headless-client/SYSTEM.HeadlessClient.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .

ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "SYSTEM.HeadlessClient.dll"]
```

---

## Testing Checklist

### Local Testing

```bash
cd SYSTEM-headless-client

# 1. Build
dotnet build

# 2. Test with local server (start spacetime first)
dotnet run -- testbot 1234 "Test Bot"

# 3. Verify in Unity Editor - should see "Test Bot" player appear
```

### Test Server Testing

Create `appsettings.Development.json`:
```json
{
  "SpacetimeDB": {
    "ServerUrl": "maincloud.spacetimedb.com",
    "ModuleName": "system-test"
  }
}
```

```bash
# Run against test server
DOTNET_ENVIRONMENT=Development dotnet run -- testbot 1234 "Test Bot"
```

### Expected Output

```
=== SYSTEM Headless Client ===
Server: ws://127.0.0.1:3000
Module: system

[Connection] Connecting to ws://127.0.0.1:3000/system...
[Connection] Connected! Identity: 0x1234...
[Connection] Subscription applied - tables synchronized
[Client] Subscribed to tables, starting authentication...
[Auth] Logging in as testbot...
[Auth] Session created! Token: abc12345...
[TokenStorage] Token saved to .bot-token
[Auth] Creating player: Test Bot...
[Auth] Player created: Test Bot (ID: 42)
[Auth] Position: (0.0, 305.0, 0.0)
[Auth] World: (0, 0, 0)

=== BOT READY ===
Player: Test Bot
Player ID: 42
Press Ctrl+C to stop
```

---

## Important Notes

### SpacetimeDB SDK Version
Match the version in Unity project. Check `SYSTEM-client-3d/Packages/manifest.json` or the autogen files for the exact version.

### Reducer Callback Names
The SDK generates `OnXxxFailed` callbacks for Result-returning reducers. Check the autogen `Reducers.cs` for exact method signatures.

### FrameTick Requirement
SpacetimeDB C# SDK requires calling `conn.FrameTick()` regularly to process incoming messages. The main loop handles this at ~60Hz.

### Thread Safety
SpacetimeDB callbacks run on the main thread (whichever calls FrameTick). The async/await pattern with TaskCompletionSource handles the coordination.

---

## Phase 1 Complete When

- [ ] Console app builds without errors
- [ ] Connects to local SpacetimeDB
- [ ] Connects to test server (maincloud)
- [ ] Successfully logs in with existing account
- [ ] Successfully registers new account if login fails
- [ ] Creates player after authentication
- [ ] Player visible in Unity client (same server)
- [ ] Graceful shutdown on Ctrl+C
- [ ] Token persisted to file
- [ ] Can reconnect using saved token (future enhancement)

---

## Next Phase Preview (Phase 2)

After Phase 1 is working, Phase 2 adds:
- Position updates via `update_player_position` reducer
- Basic movement commands
- Wave packet detection (reading from tables)
- Mining initiation

The foundation established here (connection, auth, player) will support all future phases.
