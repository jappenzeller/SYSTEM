using SpacetimeDB;
using SpacetimeDB.Types;
using SYSTEM.HeadlessClient.Connection;

namespace SYSTEM.HeadlessClient.Mining;

/// <summary>
/// Tracks extracted packets in flight and captures them when they arrive.
/// Simulates packet travel time for headless clients that don't have visual rendering.
/// </summary>
public class ExtractionTracker
{
    private readonly SpacetimeConnection _connection;
    private readonly Dictionary<ulong, PendingExtraction> _inFlight = new();

    private struct PendingExtraction
    {
        public ulong PacketId;
        public ulong SourceId;
        public DateTime ExpectedArrival;
        public uint TotalCount;
        public List<WavePacketSample> Composition;
    }

    /// <summary>
    /// Fired when a packet is successfully captured (added to inventory)
    /// </summary>
    public event Action<ulong, uint>? OnPacketCaptured; // packetId, totalCount

    /// <summary>
    /// Number of packets currently in flight
    /// </summary>
    public int InFlightCount => _inFlight.Count;

    public ExtractionTracker(SpacetimeConnection connection)
    {
        _connection = connection;
    }

    /// <summary>
    /// Initialize event handlers for extraction tracking
    /// </summary>
    public void Initialize()
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        // Subscribe to extraction events
        conn.Db.WavePacketExtraction.OnInsert += OnExtractionCreated;
        conn.Db.WavePacketExtraction.OnDelete += OnExtractionDeleted;

        // Subscribe to capture result
        conn.Reducers.OnCaptureExtractedPacketV2 += OnCaptureResult;

        Console.WriteLine("[ExtractionTracker] Initialized - tracking packet arrivals");
    }

    /// <summary>
    /// Update must be called each frame to check for arrived packets
    /// </summary>
    public void Update()
    {
        if (_inFlight.Count == 0) return;

        var now = DateTime.UtcNow;
        var arrivedPackets = new List<ulong>();

        foreach (var kvp in _inFlight)
        {
            if (now >= kvp.Value.ExpectedArrival)
            {
                arrivedPackets.Add(kvp.Key);
            }
        }

        foreach (var packetId in arrivedPackets)
        {
            CapturePacket(packetId);
        }
    }

    private void OnExtractionCreated(EventContext ctx, WavePacketExtraction extraction)
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        // Only track our own extractions
        // Check by comparing to our player's identity
        var localPlayer = GetLocalPlayer();
        if (localPlayer == null || extraction.PlayerId != localPlayer.PlayerId)
        {
            return;
        }

        // Convert timestamps (milliseconds since epoch)
        var departureTime = DateTimeOffset.FromUnixTimeMilliseconds((long)extraction.DepartureTime).UtcDateTime;
        var expectedArrival = DateTimeOffset.FromUnixTimeMilliseconds((long)extraction.ExpectedArrival).UtcDateTime;
        var travelTime = (expectedArrival - departureTime).TotalSeconds;

        _inFlight[extraction.PacketId] = new PendingExtraction
        {
            PacketId = extraction.PacketId,
            SourceId = extraction.SourceId,
            ExpectedArrival = expectedArrival,
            TotalCount = extraction.TotalCount,
            Composition = extraction.Composition
        };

        Console.WriteLine($"[ExtractionTracker] Packet {extraction.PacketId} in flight from source {extraction.SourceId} " +
                          $"(ETA: {travelTime:F1}s, {extraction.TotalCount} packets)");
    }

    private void OnExtractionDeleted(EventContext ctx, WavePacketExtraction extraction)
    {
        // Packet was captured or cleaned up by server
        if (_inFlight.Remove(extraction.PacketId))
        {
            Console.WriteLine($"[ExtractionTracker] Packet {extraction.PacketId} removed from tracking");
        }
    }

    private void CapturePacket(ulong packetId)
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        if (!_inFlight.TryGetValue(packetId, out var pending))
        {
            return;
        }

        Console.WriteLine($"[ExtractionTracker] Packet {packetId} arrived! Calling CaptureExtractedPacketV2...");

        // Call the capture reducer
        conn.Reducers.CaptureExtractedPacketV2(packetId);

        // Don't remove from tracking yet - wait for OnDelete or reducer result
    }

    private void OnCaptureResult(ReducerEventContext ctx, ulong packetId)
    {
        switch (ctx.Event.Status)
        {
            case Status.Committed:
                if (_inFlight.TryGetValue(packetId, out var pending))
                {
                    Console.WriteLine($"[ExtractionTracker] Packet {packetId} captured successfully! ({pending.TotalCount} packets → inventory)");
                    OnPacketCaptured?.Invoke(packetId, pending.TotalCount);
                    _inFlight.Remove(packetId);
                }
                break;
            case Status.Failed(var reason):
                Console.WriteLine($"[ExtractionTracker] Failed to capture packet {packetId}: {reason}");
                _inFlight.Remove(packetId); // Remove to avoid retrying
                break;
        }
    }

    private Player? GetLocalPlayer()
    {
        var conn = _connection.Conn;
        if (conn == null) return null;

        foreach (var player in conn.Db.Player.Iter())
        {
            if (player.Identity == conn.Identity)
            {
                return player;
            }
        }
        return null;
    }

    /// <summary>
    /// Get status string for debugging
    /// </summary>
    public string GetStatusString()
    {
        if (_inFlight.Count == 0)
            return "No packets in flight";

        return $"{_inFlight.Count} packets in flight";
    }

    public void Dispose()
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        conn.Db.WavePacketExtraction.OnInsert -= OnExtractionCreated;
        conn.Db.WavePacketExtraction.OnDelete -= OnExtractionDeleted;
        conn.Reducers.OnCaptureExtractedPacketV2 -= OnCaptureResult;
    }
}
