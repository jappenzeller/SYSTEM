using SpacetimeDB.Types;
using SYSTEM.HeadlessClient.Connection;

namespace SYSTEM.HeadlessClient.Inventory;

/// <summary>
/// Tracks player inventory state from SpacetimeDB table updates.
/// Fires events when inventory changes or reaches capacity.
/// </summary>
public class InventoryTracker
{
    public const int MAX_CAPACITY = 300;

    private readonly SpacetimeConnection _connection;
    private readonly ulong _playerId;

    public int TotalCount { get; private set; }
    public bool IsFull => TotalCount >= MAX_CAPACITY;
    public List<WavePacketSample> Composition { get; private set; } = new();

    public event Action? OnInventoryFull;
    public event Action<int, int>? OnInventoryChanged; // (oldCount, newCount)

    public InventoryTracker(SpacetimeConnection connection, ulong playerId)
    {
        _connection = connection;
        _playerId = playerId;
    }

    public void Initialize()
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        // Subscribe to inventory table events
        conn.Db.PlayerInventory.OnInsert += OnInventoryInsert;
        conn.Db.PlayerInventory.OnUpdate += OnInventoryUpdate;
        conn.Db.PlayerInventory.OnDelete += OnInventoryDelete;

        // Load current inventory state
        LoadCurrentInventory();
    }

    private void LoadCurrentInventory()
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        var inventory = conn.Db.PlayerInventory.PlayerId.Find(_playerId);
        if (inventory != null)
        {
            UpdateFromInventory(inventory);
            Console.WriteLine($"[Inventory] Loaded: {TotalCount}/{MAX_CAPACITY} packets");
        }
        else
        {
            Console.WriteLine("[Inventory] No existing inventory found");
        }
    }

    private void OnInventoryInsert(EventContext ctx, PlayerInventory inventory)
    {
        if (inventory.PlayerId != _playerId) return;

        int oldCount = TotalCount;
        UpdateFromInventory(inventory);
        Console.WriteLine($"[Inventory] Created: {TotalCount}/{MAX_CAPACITY} packets");

        OnInventoryChanged?.Invoke(oldCount, TotalCount);
        CheckFull();
    }

    private void OnInventoryUpdate(EventContext ctx, PlayerInventory oldInv, PlayerInventory newInv)
    {
        if (newInv.PlayerId != _playerId) return;

        int oldCount = TotalCount;
        UpdateFromInventory(newInv);
        Console.WriteLine($"[Inventory] Updated: {TotalCount}/{MAX_CAPACITY} packets (+{TotalCount - oldCount})");

        OnInventoryChanged?.Invoke(oldCount, TotalCount);
        CheckFull();
    }

    private void OnInventoryDelete(EventContext ctx, PlayerInventory inventory)
    {
        if (inventory.PlayerId != _playerId) return;

        int oldCount = TotalCount;
        TotalCount = 0;
        Composition.Clear();
        Console.WriteLine("[Inventory] Deleted (reset to 0)");

        OnInventoryChanged?.Invoke(oldCount, 0);
    }

    private void UpdateFromInventory(PlayerInventory inventory)
    {
        TotalCount = (int)inventory.TotalCount;
        Composition = inventory.InventoryComposition.ToList();
    }

    private void CheckFull()
    {
        if (IsFull)
        {
            Console.WriteLine($"[Inventory] FULL! ({TotalCount}/{MAX_CAPACITY})");
            OnInventoryFull?.Invoke();
        }
    }

    public void Dispose()
    {
        var conn = _connection.Conn;
        if (conn == null) return;

        conn.Db.PlayerInventory.OnInsert -= OnInventoryInsert;
        conn.Db.PlayerInventory.OnUpdate -= OnInventoryUpdate;
        conn.Db.PlayerInventory.OnDelete -= OnInventoryDelete;
    }

    /// <summary>
    /// Get inventory breakdown by frequency color
    /// </summary>
    public Dictionary<string, int> GetCompositionSummary()
    {
        var summary = new Dictionary<string, int>();
        foreach (var sample in Composition)
        {
            string color = GetFrequencyColor(sample.Frequency);
            if (summary.ContainsKey(color))
                summary[color] += (int)sample.Count;
            else
                summary[color] = (int)sample.Count;
        }
        return summary;
    }

    private static string GetFrequencyColor(float frequency)
    {
        if (frequency < 0.5f) return "red";
        if (frequency < 1.3f) return "yellow";
        if (frequency < 2.5f) return "green";
        if (frequency < 3.5f) return "cyan";
        if (frequency < 4.5f) return "blue";
        return "magenta";
    }
}
