using System.Linq;
using SpacetimeDB.Types;
using UnityEngine;

public class WorldCircuitQueries
{
    private DbConnection conn;

    public WorldCircuitQueries(DbConnection connection)
    {
        conn = connection;
    }

    // Get world circuit at specific coordinates
    public WorldCircuit GetWorldCircuitAt(sbyte x, sbyte y, sbyte z)
    {
        return conn.Db.WorldCircuit
            .Where(wc => wc.WorldCoords.X == x && 
                        wc.WorldCoords.Y == y && 
                        wc.WorldCoords.Z == z)
            .FirstOrDefault();
    }

    // Get world circuit at center (0,0,0)
    public WorldCircuit GetCenterWorldCircuit()
    {
        return conn.Db.WorldCircuit
            .Where(wc => wc.WorldCoords.X == 0 && 
                        wc.WorldCoords.Y == 0 && 
                        wc.WorldCoords.Z == 0)
            .FirstOrDefault();
    }

    // Get all circuits in a specific shell level
    public List<WorldCircuit> GetCircuitsInShell(int shellLevel)
    {
        return conn.Db.WorldCircuit
            .Where(wc => Mathf.Max(Mathf.Abs(wc.WorldCoords.X), 
                                  Mathf.Abs(wc.WorldCoords.Y), 
                                  Mathf.Abs(wc.WorldCoords.Z)) == shellLevel)
            .ToList();
    }

    // Get circuits that are ready to emit (past their emission interval)
    public List<WorldCircuit> GetCircuitsReadyToEmit(ulong currentTime)
    {
        return conn.Db.WorldCircuit
            .Where(wc => currentTime >= wc.LastEmissionTime + wc.EmissionIntervalMs)
            .ToList();
    }

    // Get circuits with specific qubit count
    public List<WorldCircuit> GetCircuitsByQubitCount(byte qubitCount)
    {
        return conn.Db.WorldCircuit
            .Where(wc => wc.QubitCount == qubitCount)
            .ToList();
    }

    // Get circuits within a range of coordinates
    public List<WorldCircuit> GetCircuitsInRange(WorldCoords center, int range)
    {
        return conn.Db.WorldCircuit
            .Where(wc => Mathf.Abs(wc.WorldCoords.X - center.X) <= range &&
                        Mathf.Abs(wc.WorldCoords.Y - center.Y) <= range &&
                        Mathf.Abs(wc.WorldCoords.Z - center.Z) <= range)
            .ToList();
    }

    // Get high-emission circuits (more orbs per emission)
    public List<WorldCircuit> GetHighEmissionCircuits(uint minOrbsPerEmission)
    {
        return conn.Db.WorldCircuit
            .Where(wc => wc.OrbsPerEmission >= minOrbsPerEmission)
            .OrderByDescending(wc => wc.OrbsPerEmission)
            .ToList();
    }

    // Check if a circuit exists at coordinates
    public bool CircuitExistsAt(WorldCoords coords)
    {
        return conn.Db.WorldCircuit
            .Any(wc => wc.WorldCoords.X == coords.X && 
                      wc.WorldCoords.Y == coords.Y && 
                      wc.WorldCoords.Z == coords.Z);
    }

    // Get adjacent circuits (6-connected neighbors)
    public List<WorldCircuit> GetAdjacentCircuits(WorldCoords coords)
    {
        var offsets = new (sbyte x, sbyte y, sbyte z)[]
        {
            (1, 0, 0), (-1, 0, 0),
            (0, 1, 0), (0, -1, 0),
            (0, 0, 1), (0, 0, -1)
        };

        return offsets
            .Select(offset => GetWorldCircuitAt(
                (sbyte)(coords.X + offset.x),
                (sbyte)(coords.Y + offset.y),
                (sbyte)(coords.Z + offset.z)))
            .Where(circuit => circuit != null)
            .ToList();
    }

    // Example usage in a MonoBehaviour
    public void ExampleUsage()
    {
        // Get center circuit
        var centerCircuit = GetCenterWorldCircuit();
        if (centerCircuit != null)
        {
            Debug.Log($"Center circuit has {centerCircuit.QubitCount} qubits");
        }

        // Get all shell 1 circuits
        var shell1Circuits = GetCircuitsInShell(1);
        Debug.Log($"Found {shell1Circuits.Count} circuits in shell 1");

        // Check for circuits ready to emit
        ulong currentTime = (ulong)(Time.time * 1000); // Convert to milliseconds
        var readyCircuits = GetCircuitsReadyToEmit(currentTime);
        foreach (var circuit in readyCircuits)
        {
            Debug.Log($"Circuit at ({circuit.WorldCoords.X},{circuit.WorldCoords.Y},{circuit.WorldCoords.Z}) ready to emit!");
        }
    }
}

// Extension methods for even cleaner syntax
public static class WorldCircuitExtensions
{
    public static WorldCircuit AtCoords(this IEnumerable<WorldCircuit> circuits, WorldCoords coords)
    {
        return circuits.FirstOrDefault(wc => 
            wc.WorldCoords.X == coords.X && 
            wc.WorldCoords.Y == coords.Y && 
            wc.WorldCoords.Z == coords.Z);
    }

    public static IEnumerable<WorldCircuit> InShell(this IEnumerable<WorldCircuit> circuits, int shellLevel)
    {
        return circuits.Where(wc => 
            Mathf.Max(Mathf.Abs(wc.WorldCoords.X), 
                     Mathf.Abs(wc.WorldCoords.Y), 
                     Mathf.Abs(wc.WorldCoords.Z)) == shellLevel);
    }

    public static IEnumerable<WorldCircuit> ReadyToEmit(this IEnumerable<WorldCircuit> circuits, ulong currentTime)
    {
        return circuits.Where(wc => currentTime >= wc.LastEmissionTime + wc.EmissionIntervalMs);
    }
}

// Usage with extension methods
public class CleanerUsageExample
{
    private DbConnection conn;

    public void Example()
    {
        // Super clean syntax with extension methods
        var centerCircuit = conn.Db.WorldCircuit.AtCoords(new WorldCoords(0, 0, 0));
        
        var shell2ReadyCircuits = conn.Db.WorldCircuit
            .InShell(2)
            .ReadyToEmit((ulong)(Time.time * 1000))
            .ToList();
    }
}