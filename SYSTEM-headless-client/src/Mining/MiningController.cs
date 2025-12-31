using SpacetimeDB;
using SpacetimeDB.ClientApi;
using SpacetimeDB.Types;
using SYSTEM.HeadlessClient.Connection;
using SYSTEM.HeadlessClient.Sensing;

namespace SYSTEM.HeadlessClient.Mining;

/// <summary>
/// Controls mining operations - starting, stopping, and monitoring extraction.
/// </summary>
public class MiningController
{
    private readonly SpacetimeConnection _connection;
    private readonly SourceDetector _sourceDetector;

    // Mining state
    private ulong? _currentSessionId;
    private ulong? _currentSourceId;
    private List<WavePacketSample>? _currentCrystal;
    private DateTime _miningStartTime;
    private uint _totalPacketsExtracted;

    // Extraction timer
    private float _extractionTimer;

    // Mining constants
    public const float EXTRACTION_INTERVAL = 2.0f; // seconds per packet

    // Events
    public event Action<ulong, uint>? OnPacketExtracted; // sourceId, packetCount
    public event Action<ulong>? OnMiningStarted;
    public event Action<ulong, uint>? OnMiningStopped; // sourceId, totalExtracted

    public bool IsMining => _currentSessionId.HasValue;
    public ulong? CurrentSourceId => _currentSourceId;
    public ulong? CurrentSessionId => _currentSessionId;

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

        // Subscribe to mining session table to track our session
        conn.Db.MiningSession.OnInsert += OnMiningSessionInsert;
        conn.Db.MiningSession.OnUpdate += OnMiningSessionUpdate;
        conn.Db.MiningSession.OnDelete += OnMiningSessionDelete;

        // Subscribe to extraction events
        conn.Db.WavePacketExtraction.OnInsert += OnExtractionInsert;

        Console.WriteLine("[Mining] Mining controller initialized");
    }

    /// <summary>
    /// Start mining a specific source with a crystal composition
    /// </summary>
    public void StartMining(ulong sourceId, List<WavePacketSample> crystalComposition)
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        if (_currentSessionId.HasValue)
        {
            Console.WriteLine($"[Mining] Already mining source {_currentSourceId ?? 0}, stopping first...");
            StopMining();
        }

        Console.WriteLine($"[Mining] Starting mining on source {sourceId}...");
        LogCrystalComposition(crystalComposition);

        _currentSourceId = sourceId;
        _currentCrystal = crystalComposition;
        _miningStartTime = DateTime.UtcNow;
        _totalPacketsExtracted = 0;

        conn.Reducers.StartMiningV2(sourceId, crystalComposition);
    }

    /// <summary>
    /// Start mining with a default full-spectrum crystal
    /// </summary>
    public void StartMiningWithDefaultCrystal(ulong sourceId)
    {
        // Create a default crystal that can extract all frequencies
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

        if (!_currentSessionId.HasValue)
        {
            Console.WriteLine("[Mining] Not currently mining (no active session)");
            return;
        }

        Console.WriteLine($"[Mining] Stopping mining session {_currentSessionId.Value}...");
        conn.Reducers.StopMiningV2(_currentSessionId.Value);
    }

    /// <summary>
    /// Create a default crystal that can mine all frequencies
    /// </summary>
    public static List<WavePacketSample> CreateDefaultCrystal()
    {
        // 6 base frequencies (in radians)
        // Red: 0, Yellow: π/3, Green: 2π/3, Cyan: π, Blue: 4π/3, Magenta: 5π/3
        float pi = MathF.PI;
        return new List<WavePacketSample>
        {
            new WavePacketSample(0f, 1f, 0f, 10),           // Red
            new WavePacketSample(pi / 3f, 1f, 0f, 10),      // Yellow (~1.047)
            new WavePacketSample(2f * pi / 3f, 1f, 0f, 10), // Green (~2.094)
            new WavePacketSample(pi, 1f, 0f, 10),           // Cyan (~3.142)
            new WavePacketSample(4f * pi / 3f, 1f, 0f, 10), // Blue (~4.189)
            new WavePacketSample(5f * pi / 3f, 1f, 0f, 10)  // Magenta (~5.236)
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
        if (!_currentSessionId.HasValue)
            return "Not mining";

        var elapsed = DateTime.UtcNow - _miningStartTime;
        return $"Mining source {_currentSourceId} (session {_currentSessionId}): {_totalPacketsExtracted} packets in {elapsed.TotalSeconds:F1}s";
    }

    /// <summary>
    /// Update mining systems - must be called each frame to trigger extractions
    /// </summary>
    public void Update(float deltaTime)
    {
        // Only process if actively mining with a valid session
        if (!IsMining || !_currentSessionId.HasValue)
            return;

        _extractionTimer += deltaTime;

        if (_extractionTimer >= EXTRACTION_INTERVAL)
        {
            _extractionTimer = 0f;
            ExtractPackets();
        }
    }

    /// <summary>
    /// Request packet extraction from the current mining session
    /// </summary>
    private void ExtractPackets()
    {
        var conn = _connection.Conn;
        if (conn == null || !_currentSessionId.HasValue || _currentCrystal == null)
            return;

        // Build extraction request from crystal composition
        // Request 1 packet per frequency per extraction cycle
        var extractionRequest = _currentCrystal
            .Select(c => new ExtractionRequest { Frequency = c.Frequency, Count = 1 })
            .ToList();

        Console.WriteLine($"[Mining] Requesting extraction from session {_currentSessionId.Value} ({extractionRequest.Count} frequencies)");
        conn.Reducers.ExtractPacketsV2(_currentSessionId.Value, extractionRequest);
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
                // Session will be created by server and we'll receive it via OnMiningSessionInsert
                break;
            case Status.Failed(var reason):
                Console.WriteLine($"[Mining] Failed to start mining: {reason}");
                _currentSourceId = null;
                _currentCrystal = null;
                break;
        }
    }

    private void OnStopMiningResult(ReducerEventContext ctx, ulong sessionId)
    {
        switch (ctx.Event.Status)
        {
            case Status.Committed:
                Console.WriteLine($"[Mining] StopMiningV2 reducer committed for session {sessionId}");
                // Session cleanup happens via OnMiningSessionDelete
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

        // Check if this is our session
        if (session.PlayerIdentity == conn.Identity)
        {
            _currentSessionId = session.SessionId;
            _currentSourceId = session.SourceId;
            Console.WriteLine($"[Mining] Mining session {session.SessionId} started on source {session.SourceId}");
            OnMiningStarted?.Invoke(session.SourceId);
        }
    }

    private void OnMiningSessionUpdate(EventContext ctx, MiningSession oldSession, MiningSession newSession)
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        // Check if this is our session
        if (newSession.SessionId == _currentSessionId)
        {
            // Track total extracted from session updates
            if (newSession.TotalExtracted > _totalPacketsExtracted)
            {
                _totalPacketsExtracted = newSession.TotalExtracted;
            }
        }
    }

    private void OnMiningSessionDelete(EventContext ctx, MiningSession session)
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        // Check if this was our session
        if (session.SessionId == _currentSessionId)
        {
            var sourceId = _currentSourceId ?? 0;
            Console.WriteLine($"[Mining] Mining session {session.SessionId} ended. Total extracted: {session.TotalExtracted} packets");
            OnMiningStopped?.Invoke(sourceId, session.TotalExtracted);
            _currentSessionId = null;
            _currentSourceId = null;
            _currentCrystal = null;
        }
    }

    private void OnExtractionInsert(EventContext ctx, WavePacketExtraction extraction)
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        // Check if this is our extraction (by player ID)
        if (extraction.SourceId == _currentSourceId)
        {
            uint packetCount = extraction.TotalCount;
            _totalPacketsExtracted += packetCount;
            Console.WriteLine($"[Mining] Extracted {packetCount} packets from source {extraction.SourceId} (total: {_totalPacketsExtracted})");
            OnPacketExtracted?.Invoke(extraction.SourceId, packetCount);
        }
    }

    #endregion
}
