using SpacetimeDB.Types;
using SYSTEM.HeadlessClient.Behavior;
using SYSTEM.HeadlessClient.Inventory;
using SYSTEM.HeadlessClient.Mining;
using SYSTEM.HeadlessClient.Sensing;
using SYSTEM.HeadlessClient.World;

namespace SYSTEM.HeadlessClient.AI;

/// <summary>
/// Builds game context for injection into QAI prompts.
/// </summary>
public static class GameContextBuilder
{
    /// <summary>
    /// Build a context object with current game state for the AI.
    /// </summary>
    public static object Build(
        string playerName,
        Player? player,
        WorldManager? world,
        SourceDetector? sources,
        MiningController? mining,
        InventoryTracker? inventory,
        BehaviorStateMachine? behavior,
        DateTime startTime)
    {
        var uptime = DateTime.UtcNow - startTime;

        return new
        {
            // Player context
            player_name = playerName,
            player_in_game = player != null,
            player_position = player != null ? new
            {
                x = player.Position.X,
                y = player.Position.Y,
                z = player.Position.Z
            } : null,
            player_world = player != null ? new
            {
                x = player.CurrentWorld.X,
                y = player.CurrentWorld.Y,
                z = player.CurrentWorld.Z
            } : null,

            // QAI state
            qai_position = world != null ? new
            {
                x = world.Position.X,
                y = world.Position.Y,
                z = world.Position.Z
            } : null,
            // Environment
            sources_in_range = sources?.SourcesInRange.Count ?? 0,
            richest_source_packets = sources?.GetRichestSource()?.TotalWavePackets ?? 0,

            // Mining state (server-authoritative)
            mining_status = mining?.IsMining == true ? "active" : "idle",
            mining_source_id = mining?.CurrentSourceId,

            // Inventory
            inventory_count = inventory?.TotalCount ?? 0,
            inventory_capacity = InventoryTracker.MAX_CAPACITY,
            inventory_full = inventory?.IsFull ?? false,
            inventory_composition = inventory?.GetCompositionSummary(),

            // Behavior
            behavior_state = behavior?.CurrentState.ToString() ?? "unknown",

            // Session
            uptime_seconds = (int)uptime.TotalSeconds,
            uptime_formatted = $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}"
        };
    }

    /// <summary>
    /// Build a minimal context for quick status checks.
    /// </summary>
    public static object BuildMinimal(
        WorldManager? world,
        MiningController? mining,
        InventoryTracker? inventory,
        BehaviorStateMachine? behavior)
    {
        return new
        {
            position = world != null ? $"({world.Position.X:F0}, {world.Position.Y:F0}, {world.Position.Z:F0})" : "unknown",
            state = behavior?.CurrentState.ToString() ?? "unknown",
            mining = mining?.IsMining ?? false,
            inventory = $"{inventory?.TotalCount ?? 0}/{InventoryTracker.MAX_CAPACITY}"
        };
    }
}
