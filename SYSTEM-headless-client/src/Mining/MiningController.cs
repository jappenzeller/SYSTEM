using SpacetimeDB;
using SpacetimeDB.ClientApi;
using SpacetimeDB.Types;
using SYSTEM.HeadlessClient.Connection;
using SYSTEM.HeadlessClient.Sensing;

namespace SYSTEM.HeadlessClient.Mining;

/// <summary>
/// Controls mining operations - starting, stopping, and monitoring extraction.
/// Uses server-authoritative state from MiningSession table.
/// </summary>
public class MiningController
{
    private readonly SpacetimeConnection _connection;
    private readonly SourceDetector _sourceDetector;

    // Extraction timer (only local state needed)
    private float _extractionTimer;
    private DateTime _miningStartTime;
    private uint _lastLoggedTotalExtracted;

    // Mining constants
    public const float EXTRACTION_INTERVAL = 2.0f; // seconds per packet

    // Events
    public event Action<ulong, uint>? OnPacketExtracted; // sourceId, packetCount
    public event Action<ulong>? OnMiningStarted;
    public event Action<ulong, uint>? OnMiningStopped; // sourceId, totalExtracted

    /// <summary>
    /// Get the active mining session from the server's MiningSession table.
    /// Returns null if not currently mining.
    /// </summary>
    public MiningSession? GetActiveSession()
    {
        var conn = _connection.Conn;
        if (conn == null) return null;

        foreach (var session in conn.Db.MiningSession.Iter())
        {
            if (session.PlayerIdentity == conn.Identity && session.IsActive)
            {
                return session;
            }
        }
        return null;
    }

    // Server-authoritative properties
    public bool IsMining => GetActiveSession() != null;
    public ulong? CurrentSourceId => GetActiveSession()?.SourceId;
    public ulong? CurrentSessionId => GetActiveSession()?.SessionId;

    public MiningController(SpacetimeConnection connection, SourceDetector sourceDetector)
    {
        _connection = connection;
        _sourceDetector = sourceDetector;
    }

    /// <summary>
    /// Initialize event handlers
    /// </summary>
    public void Initialize()
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        // Subscribe to mining-related reducer callbacks
        conn.Reducers.OnStartMiningV2 += OnStartMiningResult;
        conn.Reducers.OnStopMiningV2 += OnStopMiningResult;
        conn.Reducers.OnExtractPacketsV2 += OnExtractPacketsResult;

        // Subscribe to mining session table for state changes
        conn.Db.MiningSession.OnInsert += OnMiningSessionInsert;
        conn.Db.MiningSession.OnUpdate += OnMiningSessionUpdate;
        conn.Db.MiningSession.OnDelete += OnMiningSessionDelete;

        // Subscribe to extraction events
        conn.Db.WavePacketExtraction.OnInsert += OnExtractionInsert;

        Console.WriteLine("[Mining] Mining controller initialized (server-authoritative)");
    }

    /// <summary>
    /// Start mining a specific source with a crystal composition
    /// </summary>
    public void StartMining(ulong sourceId, List<WavePacketSample> crystalComposition)
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        // Check if already mining this source
        var currentSession = GetActiveSession();
        if (currentSession != null)
        {
            if (currentSession.SourceId == sourceId)
            {
                Console.WriteLine($"[Mining] Already mining source {sourceId}");
                return;
            }
            Console.WriteLine($"[Mining] Already mining source {currentSession.SourceId}, stopping first...");
            StopMining();
        }

        Console.WriteLine($"[Mining] Starting mining on source {sourceId}...");
        LogCrystalComposition(crystalComposition);

        _miningStartTime = DateTime.UtcNow;
        _extractionTimer = 0f;
        _lastLoggedTotalExtracted = 0;

        conn.Reducers.StartMiningV2(sourceId, crystalComposition);
    }

    /// <summary>
    /// Start mining with a default full-spectrum crystal
    /// </summary>
    public void StartMiningWithDefaultCrystal(ulong sourceId)
    {
        var crystal = CreateDefaultCrystal();
        StartMining(sourceId, crystal);
    }

    /// <summary>
    /// Start mining the closest source in range with default crystal
    /// </summary>
    public bool StartMiningClosestSource()
    {
        var closest = _sourceDetector.GetClosestSource();
        if (closest == null)
        {
            Console.WriteLine("[Mining] No sources in range to mine");
            return false;
        }

        StartMiningWithDefaultCrystal(closest.SourceId);
        return true;
    }

    /// <summary>
    /// Start mining the richest source in range with default crystal
    /// </summary>
    public bool StartMiningRichestSource()
    {
        var richest = _sourceDetector.GetRichestSource();
        if (richest == null)
        {
            Console.WriteLine("[Mining] No sources in range to mine");
            return false;
        }

        StartMiningWithDefaultCrystal(richest.SourceId);
        return true;
    }

    /// <summary>
    /// Stop current mining operation
    /// </summary>
    public void StopMining()
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        var session = GetActiveSession();
        if (session == null)
        {
            Console.WriteLine("[Mining] Not currently mining");
            return;
        }

        Console.WriteLine($"[Mining] Stopping mining session {session.SessionId}...");
        conn.Reducers.StopMiningV2(session.SessionId);
    }

    /// <summary>
    /// Create a default crystal that can mine all frequencies
    /// </summary>
    public static List<WavePacketSample> CreateDefaultCrystal()
    {
        // 6 base frequencies (in radians)
        float pi = MathF.PI;
        return new List<WavePacketSample>
        {
            new WavePacketSample(0f, 1f, 0f, 10),           // Red
            new WavePacketSample(pi / 3f, 1f, 0f, 10),      // Yellow
            new WavePacketSample(2f * pi / 3f, 1f, 0f, 10), // Green
            new WavePacketSample(pi, 1f, 0f, 10),           // Cyan
            new WavePacketSample(4f * pi / 3f, 1f, 0f, 10), // Blue
            new WavePacketSample(5f * pi / 3f, 1f, 0f, 10)  // Magenta
        };
    }

    /// <summary>
    /// Create a crystal tuned to a specific frequency
    /// </summary>
    public static List<WavePacketSample> CreateSingleFrequencyCrystal(float frequency, uint count = 100)
    {
        return new List<WavePacketSample>
        {
            new WavePacketSample(frequency, 1f, 0f, count)
        };
    }

    /// <summary>
    /// Get mining status string
    /// </summary>
    public string GetMiningStatus()
    {
        var session = GetActiveSession();
        if (session == null)
            return "Not mining";

        var elapsed = DateTime.UtcNow - _miningStartTime;
        return $"Mining source {session.SourceId} (session {session.SessionId}): {session.TotalExtracted} packets in {elapsed.TotalSeconds:F1}s";
    }

    /// <summary>
    /// Update mining systems - must be called each frame to trigger extractions
    /// </summary>
    public void Update(float deltaTime)
    {
        var session = GetActiveSession();
        if (session == null) return;

        _extractionTimer += deltaTime;

        if (_extractionTimer >= EXTRACTION_INTERVAL)
        {
            _extractionTimer = 0f;
            ExtractPackets(session);
        }
    }

    /// <summary>
    /// Request packet extraction from the current mining session
    /// </summary>
    private void ExtractPackets(MiningSession session)
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        // Build extraction request from session's crystal composition
        var extractionRequest = session.CrystalComposition
            .Select(c => new ExtractionRequest { Frequency = c.Frequency, Count = 1 })
            .ToList();

        Console.WriteLine($"[Mining] Requesting extraction from session {session.SessionId} ({extractionRequest.Count} frequencies)");
        conn.Reducers.ExtractPacketsV2(session.SessionId, extractionRequest);
    }

    private void LogCrystalComposition(List<WavePacketSample> crystal)
    {
        Console.WriteLine("[Mining] Crystal composition:");
        foreach (var sample in crystal)
        {
            string name = GetFrequencyName(sample.Frequency);
            Console.WriteLine($"  {name}: freq={sample.Frequency:F3}, amp={sample.Amplitude:F2}, count={sample.Count}");
        }
    }

    private static string GetFrequencyName(float frequency)
    {
        if (frequency < 0.5f) return "Red";
        if (frequency < 1.3f) return "Yellow";
        if (frequency < 2.5f) return "Green";
        if (frequency < 3.5f) return "Cyan";
        if (frequency < 4.5f) return "Blue";
        return "Magenta";
    }

    #region Reducer Event Handlers

    private void OnStartMiningResult(ReducerEventContext ctx, ulong sourceId, List<WavePacketSample> crystalComposition)
    {
        switch (ctx.Event.Status)
        {
            case Status.Committed:
                Console.WriteLine($"[Mining] StartMiningV2 reducer committed for source {sourceId}");
                break;
            case Status.Failed(var reason):
                Console.WriteLine($"[Mining] Failed to start mining: {reason}");
                break;
        }
    }

    private void OnStopMiningResult(ReducerEventContext ctx, ulong sessionId)
    {
        switch (ctx.Event.Status)
        {
            case Status.Committed:
                Console.WriteLine($"[Mining] StopMiningV2 reducer committed for session {sessionId}");
                break;
            case Status.Failed(var reason):
                Console.WriteLine($"[Mining] Failed to stop mining: {reason}");
                break;
        }
    }

    private void OnExtractPacketsResult(ReducerEventContext ctx, ulong sessionId, List<ExtractionRequest> requestedFrequencies)
    {
        switch (ctx.Event.Status)
        {
            case Status.Failed(var reason):
                Console.WriteLine($"[Mining] Extraction failed: {reason}");
                break;
        }
    }

    #endregion

    #region Table Event Handlers

    private void OnMiningSessionInsert(EventContext ctx, MiningSession session)
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        if (session.PlayerIdentity == conn.Identity)
        {
            _miningStartTime = DateTime.UtcNow;
            _extractionTimer = 0f;
            Console.WriteLine($"[Mining] Session {session.SessionId} started on source {session.SourceId}");
            OnMiningStarted?.Invoke(session.SourceId);
        }
    }

    private void OnMiningSessionUpdate(EventContext ctx, MiningSession oldSession, MiningSession newSession)
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        if (newSession.PlayerIdentity == conn.Identity)
        {
            // Log extraction progress periodically
            if (newSession.TotalExtracted > _lastLoggedTotalExtracted)
            {
                _lastLoggedTotalExtracted = newSession.TotalExtracted;
            }
        }
    }

    private void OnMiningSessionDelete(EventContext ctx, MiningSession session)
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        if (session.PlayerIdentity == conn.Identity)
        {
            Console.WriteLine($"[Mining] Session {session.SessionId} ended. Total extracted: {session.TotalExtracted} packets");
            OnMiningStopped?.Invoke(session.SourceId, session.TotalExtracted);
        }
    }

    private void OnExtractionInsert(EventContext ctx, WavePacketExtraction extraction)
    {
        var session = GetActiveSession();
        if (session == null) return;

        // Check if this extraction is for our current source
        if (extraction.SourceId == session.SourceId)
        {
            uint packetCount = extraction.TotalCount;
            Console.WriteLine($"[Mining] Extracted {packetCount} packets from source {extraction.SourceId}");
            OnPacketExtracted?.Invoke(extraction.SourceId, packetCount);
        }
    }

    #endregion
}
