using SpacetimeDB;
using SpacetimeDB.Types;
using SYSTEM.HeadlessClient.Connection;
using SYSTEM.HeadlessClient.World;

namespace SYSTEM.HeadlessClient.Sensing;

/// <summary>
/// Detects wave packet sources within mining range.
/// Mining range is 20 units.
/// </summary>
public class SourceDetector
{
    private readonly SpacetimeConnection _connection;
    private readonly WorldManager _worldManager;

    // Mining constants (matching Unity client)
    public const float MINING_RANGE = 20f;
    public const float SCAN_INTERVAL = 1.0f; // seconds

    // Current state
    private float _lastScanTime;
    private readonly List<WavePacketSource> _sourcesInRange = new();
    private readonly HashSet<ulong> _knownSourceIds = new();

    // Events
    public event Action<WavePacketSource>? OnSourceEnterRange;
    public event Action<WavePacketSource>? OnSourceExitRange;
    public event Action<List<WavePacketSource>>? OnScanComplete;

    public IReadOnlyList<WavePacketSource> SourcesInRange => _sourcesInRange;

    public SourceDetector(SpacetimeConnection connection, WorldManager worldManager)
    {
        _connection = connection;
        _worldManager = worldManager;
    }

    /// <summary>
    /// Initialize event handlers for source table updates
    /// </summary>
    public void Initialize()
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        conn.Db.WavePacketSource.OnInsert += OnSourceInsert;
        conn.Db.WavePacketSource.OnUpdate += OnSourceUpdate;
        conn.Db.WavePacketSource.OnDelete += OnSourceDelete;

        Console.WriteLine("[Sensing] Source detector initialized");
    }

    /// <summary>
    /// Periodic update - scan for sources in range
    /// </summary>
    public void Update(float currentTime)
    {
        if (currentTime - _lastScanTime >= SCAN_INTERVAL)
        {
            ScanForSources();
            _lastScanTime = currentTime;
        }
    }

    /// <summary>
    /// Force a scan for sources in range
    /// </summary>
    public void ScanForSources()
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        var currentPos = _worldManager.Position;
        var newSourcesInRange = new List<WavePacketSource>();
        var newSourceIds = new HashSet<ulong>();

        // Iterate all sources and find those in range
        foreach (var source in conn.Db.WavePacketSource.Iter())
        {
            float distance = WorldManager.Distance(currentPos, source.Position);

            if (distance <= MINING_RANGE)
            {
                newSourcesInRange.Add(source);
                newSourceIds.Add(source.SourceId);

                // Check if this is a newly detected source
                if (!_knownSourceIds.Contains(source.SourceId))
                {
                    Console.WriteLine($"[Sensing] Source {source.SourceId} entered range (distance: {distance:F1})");
                    OnSourceEnterRange?.Invoke(source);
                }
            }
        }

        // Check for sources that left range
        foreach (var oldId in _knownSourceIds)
        {
            if (!newSourceIds.Contains(oldId))
            {
                var oldSource = conn.Db.WavePacketSource.SourceId.Find(oldId);
                if (oldSource != null)
                {
                    Console.WriteLine($"[Sensing] Source {oldId} left range");
                    OnSourceExitRange?.Invoke(oldSource);
                }
            }
        }

        // Update state
        _sourcesInRange.Clear();
        _sourcesInRange.AddRange(newSourcesInRange);
        _knownSourceIds.Clear();
        foreach (var id in newSourceIds)
            _knownSourceIds.Add(id);

        OnScanComplete?.Invoke(_sourcesInRange);
    }

    /// <summary>
    /// Get the closest source within mining range
    /// </summary>
    public WavePacketSource? GetClosestSource()
    {
        if (_sourcesInRange.Count == 0)
            return null;

        var currentPos = _worldManager.Position;
        WavePacketSource? closest = null;
        float closestDist = float.MaxValue;

        foreach (var source in _sourcesInRange)
        {
            float dist = WorldManager.Distance(currentPos, source.Position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = source;
            }
        }

        return closest;
    }

    /// <summary>
    /// Get the richest source (most total packets) within range
    /// </summary>
    public WavePacketSource? GetRichestSource()
    {
        if (_sourcesInRange.Count == 0)
            return null;

        WavePacketSource? richest = null;
        uint maxPackets = 0;

        foreach (var source in _sourcesInRange)
        {
            if (source.TotalWavePackets > maxPackets)
            {
                maxPackets = source.TotalWavePackets;
                richest = source;
            }
        }

        return richest;
    }

    /// <summary>
    /// Get sources that contain a specific frequency
    /// </summary>
    public List<WavePacketSource> GetSourcesWithFrequency(float targetFrequency, float tolerance = 0.05f)
    {
        var results = new List<WavePacketSource>();

        foreach (var source in _sourcesInRange)
        {
            foreach (var sample in source.WavePacketComposition)
            {
                if (Math.Abs(sample.Frequency - targetFrequency) <= tolerance && sample.Count > 0)
                {
                    results.Add(source);
                    break;
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Log current source status
    /// </summary>
    public void LogSourceStatus()
    {
        if (_sourcesInRange.Count == 0)
        {
            Console.WriteLine("[Sensing] No sources in range");
            return;
        }

        Console.WriteLine($"[Sensing] {_sourcesInRange.Count} source(s) in range:");
        var currentPos = _worldManager.Position;

        foreach (var source in _sourcesInRange)
        {
            float dist = WorldManager.Distance(currentPos, source.Position);
            Console.WriteLine($"  Source {source.SourceId}: {source.TotalWavePackets} packets, {dist:F1} units away");

            if (source.WavePacketComposition.Count > 0)
            {
                var freqStr = string.Join(", ", source.WavePacketComposition
                    .Where(s => s.Count > 0)
                    .Select(s => $"{GetFrequencyName(s.Frequency)}:{s.Count}"));
                Console.WriteLine($"    Composition: {freqStr}");
            }
        }
    }

    private static string GetFrequencyName(float frequency)
    {
        // 6 base frequencies from CLAUDE.md
        // Red(0), Yellow(1/6), Green(1/3), Cyan(1/2), Blue(2/3), Magenta(5/6)
        // These are in radians: 0, ~1.047, ~2.094, ~3.142, ~4.189, ~5.236

        if (frequency < 0.5f) return "Red";
        if (frequency < 1.3f) return "Yellow";
        if (frequency < 2.5f) return "Green";
        if (frequency < 3.5f) return "Cyan";
        if (frequency < 4.5f) return "Blue";
        return "Magenta";
    }

    #region Event Handlers

    private void OnSourceInsert(EventContext ctx, WavePacketSource source)
    {
        var currentPos = _worldManager.Position;
        float distance = WorldManager.Distance(currentPos, source.Position);

        if (distance <= MINING_RANGE)
        {
            if (!_knownSourceIds.Contains(source.SourceId))
            {
                _sourcesInRange.Add(source);
                _knownSourceIds.Add(source.SourceId);
                Console.WriteLine($"[Sensing] New source {source.SourceId} appeared in range ({distance:F1} units)");
                OnSourceEnterRange?.Invoke(source);
            }
        }
    }

    private void OnSourceUpdate(EventContext ctx, WavePacketSource oldSource, WavePacketSource newSource)
    {
        // Update our cached version if we're tracking this source
        int index = _sourcesInRange.FindIndex(s => s.SourceId == newSource.SourceId);
        if (index >= 0)
        {
            _sourcesInRange[index] = newSource;
        }
    }

    private void OnSourceDelete(EventContext ctx, WavePacketSource source)
    {
        if (_knownSourceIds.Contains(source.SourceId))
        {
            _sourcesInRange.RemoveAll(s => s.SourceId == source.SourceId);
            _knownSourceIds.Remove(source.SourceId);
            Console.WriteLine($"[Sensing] Source {source.SourceId} deleted");
            OnSourceExitRange?.Invoke(source);
        }
    }

    #endregion
}
