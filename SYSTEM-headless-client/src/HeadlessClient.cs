using SYSTEM.HeadlessClient.AI;
using SYSTEM.HeadlessClient.Api;
using SYSTEM.HeadlessClient.Auth;
using SYSTEM.HeadlessClient.Behavior;
using SYSTEM.HeadlessClient.Chat;
using SYSTEM.HeadlessClient.Config;
using SYSTEM.HeadlessClient.Connection;
using SYSTEM.HeadlessClient.Discord;
using SYSTEM.HeadlessClient.Inventory;
using SYSTEM.HeadlessClient.Mcp;
using SYSTEM.HeadlessClient.Mining;
using SYSTEM.HeadlessClient.Sensing;
using SYSTEM.HeadlessClient.Twitch;
using SYSTEM.HeadlessClient.World;

namespace SYSTEM.HeadlessClient;

public class HeadlessClient
{
    private readonly ClientConfig _config;
    private readonly SpacetimeConnection _connection;
    private readonly AuthManager _auth;
    private readonly TokenStorage _tokenStorage;

    // Phase 2: World, Sensing, Mining systems
    private WorldManager? _worldManager;
    private SourceDetector? _sourceDetector;
    private MiningController? _miningController;
    private DateTime _startTime;
    private float _lastStatusLogTime;
    private bool _pendingMiningStart; // Prevents duplicate StartMining calls

    // Phase 3: Inventory and Behavior systems
    private InventoryTracker? _inventoryTracker;
    private BehaviorStateMachine? _behaviorStateMachine;

    // Command API
    private CommandServer? _commandServer;
    private const int API_PORT = 8080;

    // Chat Platform Manager (Twitch, Discord, etc.)
    private ChatPlatformManager? _chatPlatformManager;

    // AI Chat Handler
    private QaiChatHandler? _chatHandler;

    // In-game player chat listener (for proximity chat)
    private PlayerChatListener? _playerChatListener;

    private readonly CancellationTokenSource _cts = new();
    private bool _running;
    private bool _systemsInitialized;

    // Constants
    private const float STATUS_LOG_INTERVAL = 10.0f; // Log status every 10 seconds

    // Timing for deltaTime calculation
    private float _lastFrameTime;

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
        Console.WriteLine("=== SYSTEM QAI Client ===");
        Console.WriteLine($"Server: {_config.SpacetimeDB.ServerUrl}");
        Console.WriteLine($"Module: {_config.SpacetimeDB.ModuleName}");
        Console.WriteLine();

        _running = true;
        _startTime = DateTime.UtcNow;

        // Connect
        _connection.Connect();

        // Main loop - process SpacetimeDB messages and update systems
        while (_running && !_cts.Token.IsCancellationRequested)
        {
            _connection.Update();

            // Update Phase 2 systems if initialized
            if (_systemsInitialized)
            {
                UpdateSystems();
            }

            await Task.Delay(16, _cts.Token); // ~60 updates/sec
        }
    }

    private void UpdateSystems()
    {
        float currentTime = (float)(DateTime.UtcNow - _startTime).TotalSeconds;
        float deltaTime = currentTime - _lastFrameTime;
        _lastFrameTime = currentTime;

        // Clamp deltaTime to avoid huge jumps
        if (deltaTime > 0.1f) deltaTime = 0.016f;

        // Update world manager (processes walking and sends position updates)
        _worldManager?.Update(currentTime, deltaTime);

        // Update source detector (scans for nearby sources)
        _sourceDetector?.Update(currentTime);

        // Update mining controller (triggers extractions when mining)
        _miningController?.Update(deltaTime);

        // Update behavior state machine (handles autonomous decisions)
        _behaviorStateMachine?.Update(deltaTime);

        // Periodic status logging
        if (currentTime - _lastStatusLogTime >= STATUS_LOG_INTERVAL)
        {
            LogStatus();
            _lastStatusLogTime = currentTime;
        }
    }

    private void LogStatus()
    {
        Console.WriteLine();
        Console.WriteLine($"[Status] Uptime: {(DateTime.UtcNow - _startTime).TotalMinutes:F1} minutes");

        if (_worldManager != null)
        {
            var pos = _worldManager.Position;
            Console.WriteLine($"[Status] Position: ({pos.X:F1}, {pos.Y:F1}, {pos.Z:F1})");
        }

        if (_sourceDetector != null)
        {
            Console.WriteLine($"[Status] Sources in range: {_sourceDetector.SourcesInRange.Count}");
        }

        if (_miningController != null)
        {
            Console.WriteLine($"[Status] Mining: {_miningController.GetMiningStatus()}");
        }

        if (_inventoryTracker != null)
        {
            Console.WriteLine($"[Status] Inventory: {_inventoryTracker.TotalCount}/{InventoryTracker.MAX_CAPACITY} packets");
        }

        if (_behaviorStateMachine != null)
        {
            Console.WriteLine($"[Status] Behavior: {_behaviorStateMachine.GetStatusString()}");
        }

        Console.WriteLine();
    }

    private async void OnSubscribed()
    {
        Console.WriteLine("[Client] Subscribed to tables, starting authentication...");

        try
        {
            // Initialize auth manager with table callbacks
            _auth.Initialize();

            // Attempt login
            var username = _config.QAI.Username;
            var pin = _config.QAI.Pin;
            var displayName = _config.QAI.DisplayName;

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

            // Initialize Phase 2 systems
            InitializeSystems();

            Console.WriteLine();
            Console.WriteLine("=== QAI READY ===");
            Console.WriteLine($"Player: {_auth.LocalPlayer?.Name}");
            Console.WriteLine($"Player ID: {_auth.LocalPlayer?.PlayerId}");
            Console.WriteLine("Systems: World, Sensing, Mining - ACTIVE");
            Console.WriteLine("Press Ctrl+C to stop");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Client] Exception during authentication: {ex.Message}");
            Stop();
        }
    }

    private void InitializeSystems()
    {
        Console.WriteLine("[Client] Initializing Phase 2 systems...");

        // Create world manager
        _worldManager = new WorldManager(_connection);
        _worldManager.Initialize();

        // Initialize from player's current position
        if (_auth.LocalPlayer != null)
        {
            _worldManager.InitializeFromPlayer(_auth.LocalPlayer);
        }

        // Create source detector
        _sourceDetector = new SourceDetector(_connection, _worldManager);
        _sourceDetector.Initialize();

        // Wire up source detector events
        _sourceDetector.OnSourceEnterRange += OnSourceEnterRange;
        _sourceDetector.OnSourceExitRange += OnSourceExitRange;

        // Create mining controller
        _miningController = new MiningController(_connection, _sourceDetector);
        _miningController.Initialize();

        // Wire up mining events
        _miningController.OnMiningStarted += OnMiningStarted;
        _miningController.OnMiningStopped += OnMiningStopped;
        _miningController.OnPacketExtracted += OnPacketExtracted;

        // Do initial scan for sources
        _sourceDetector.ScanForSources();

        // Phase 3: Create inventory tracker and behavior state machine
        if (_auth.LocalPlayer != null)
        {
            _inventoryTracker = new InventoryTracker(_connection, _auth.LocalPlayer.PlayerId);
            _inventoryTracker.Initialize();

            _behaviorStateMachine = new BehaviorStateMachine(
                _inventoryTracker,
                _miningController,
                _sourceDetector,
                _worldManager,
                _config.Behavior);

            // Start exploring if configured
            _behaviorStateMachine.StartExploringIfConfigured();

            Console.WriteLine("[Client] Phase 3 systems initialized (Inventory, Behavior)");
        }

        // Start command API server
        var handler = new CommandHandler(_auth, _worldManager, _sourceDetector, _miningController, _startTime);
        _commandServer = new CommandServer(handler, API_PORT);

        // Create and wire up MCP server
        var mcpServer = new McpServer(
            _auth,
            _worldManager,
            _sourceDetector,
            _miningController,
            _inventoryTracker,
            _behaviorStateMachine,
            _startTime);
        _commandServer.SetMcpServer(mcpServer);
        Console.WriteLine("[Client] MCP server initialized");

        _commandServer.Start();

        // Create AI chat handler (if enabled)
        if (_config.Bedrock.Enabled)
        {
            _chatHandler = new QaiChatHandler(
                _config.Bedrock,
                _worldManager,
                _sourceDetector,
                _miningController,
                _inventoryTracker,
                _behaviorStateMachine,
                _startTime);
            Console.WriteLine("[Client] AI Chat Handler initialized (Bedrock enabled)");
        }
        else
        {
            Console.WriteLine("[Client] AI Chat Handler disabled (using fallback responses)");
        }

        // Create platform-agnostic command handler
        var commandHandler = new QaiCommandHandler(
            _connection,
            _worldManager,
            _sourceDetector,
            _miningController,
            _inventoryTracker,
            _behaviorStateMachine,
            _chatHandler,
            _startTime);

        // Create in-game player chat listener for proximity chat
        if (_auth.LocalPlayer != null)
        {
            _playerChatListener = new PlayerChatListener(
                _connection,
                _worldManager,
                commandHandler,
                _auth.LocalPlayer.PlayerId);
            _playerChatListener.Initialize();
            Console.WriteLine("[Client] In-game player chat listener initialized (15 unit proximity)");
        }

        // Create chat platform manager
        _chatPlatformManager = new ChatPlatformManager(
            commandHandler,
            _config.PrivilegedUsers);

        // Register Twitch platform
        if (_config.Twitch.Enabled)
        {
            var twitchPlatform = new TwitchPlatform(_config.Twitch);
            _chatPlatformManager.RegisterPlatform(twitchPlatform);
            Console.WriteLine("[Client] Twitch platform registered");
        }

        // Register Discord platform
        if (_config.Discord.Enabled)
        {
            var discordPlatform = new DiscordPlatform(_config.Discord);
            _chatPlatformManager.RegisterPlatform(discordPlatform);
            Console.WriteLine("[Client] Discord platform registered");
        }

        // Connect all platforms
        _ = _chatPlatformManager.ConnectAllAsync(_cts.Token);

        _systemsInitialized = true;
        Console.WriteLine("[Client] All systems initialized");
    }

    #region Source Detector Events

    private void OnSourceEnterRange(SpacetimeDB.Types.WavePacketSource source)
    {
        Console.WriteLine($"[QAI] Source {source.SourceId} detected! ({source.TotalWavePackets} packets)");

        // Auto-mine if not already mining or pending
        if (_miningController != null && !_miningController.IsMining && !_pendingMiningStart)
        {
            Console.WriteLine("[QAI] Starting automatic mining...");
            _pendingMiningStart = true;
            _miningController.StartMiningWithDefaultCrystal(source.SourceId);
        }
    }

    private void OnSourceExitRange(SpacetimeDB.Types.WavePacketSource source)
    {
        Console.WriteLine($"[QAI] Source {source.SourceId} left range");

        // Stop mining if we're mining and this was our source (or source tracking is broken)
        if (_miningController != null && _miningController.IsMining)
        {
            var currentSource = _miningController.CurrentSourceId;
            if (currentSource == null || currentSource == source.SourceId)
            {
                Console.WriteLine("[QAI] Stopping mining (source left range)");
                _miningController.StopMining();
                _pendingMiningStart = false;
            }
        }
    }

    #endregion

    #region Mining Events

    private void OnMiningStarted(ulong sourceId)
    {
        Console.WriteLine($"[QAI] Mining session started on source {sourceId}");
        _pendingMiningStart = false; // Mining confirmed, clear pending flag
    }

    private void OnMiningStopped(ulong sourceId, uint totalExtracted)
    {
        Console.WriteLine($"[QAI] Mining session ended. Extracted {totalExtracted} packets from source {sourceId}");
        _pendingMiningStart = false; // Clear pending flag

        // Look for another source to mine
        if (_sourceDetector != null && _miningController != null)
        {
            var nextSource = _sourceDetector.GetRichestSource();
            if (nextSource != null && nextSource.SourceId != sourceId)
            {
                Console.WriteLine($"[QAI] Found another source, starting mining...");
                _miningController.StartMiningWithDefaultCrystal(nextSource.SourceId);
            }
        }
    }

    private void OnPacketExtracted(ulong sourceId, uint packetCount)
    {
        // Just log for now, extracted packets will be in inventory
    }

    #endregion

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

        // Dispose player chat listener
        _playerChatListener?.Dispose();

        // Disconnect all chat platforms
        _chatPlatformManager?.Dispose();

        // Dispose chat handler
        _chatHandler?.Dispose();

        // Stop command server
        _commandServer?.Stop();

        // Stop mining if active
        if (_miningController?.IsMining == true)
        {
            _miningController.StopMining();
        }

        _running = false;
        _cts.Cancel();
        _connection.Disconnect();
    }
}
