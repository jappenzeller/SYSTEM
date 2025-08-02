using System.Collections.Generic;
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

    // Get world circuit at specific coordinates - using iteration instead of index
    public WorldCircuit GetWorldCircuitAt(sbyte x, sbyte y, sbyte z)
    {
        var coords = new WorldCoords(x, y, z);
        return conn.Db.WorldCircuit.Iter().FirstOrDefault(wc => 
            wc.WorldCoords.X == coords.X && 
            wc.WorldCoords.Y == coords.Y && 
            wc.WorldCoords.Z == coords.Z);
    }

    // Get world circuit at center (0,0,0)
    public WorldCircuit GetCenterWorldCircuit()
    {
        var centerCoords = new WorldCoords(0, 0, 0);
        return conn.Db.WorldCircuit.Iter().FirstOrDefault(wc => 
            wc.WorldCoords.X == centerCoords.X && 
            wc.WorldCoords.Y == centerCoords.Y && 
            wc.WorldCoords.Z == centerCoords.Z);
    }

    // Get all circuits in a specific shell level
    public List<WorldCircuit> GetCircuitsInShell(int shellLevel)
    {
        var results = new List<WorldCircuit>();
        
        foreach (var circuit in conn.Db.WorldCircuit.Iter())
        {
            int circuitShell = Mathf.Max(
                Mathf.Abs(circuit.WorldCoords.X), 
                Mathf.Abs(circuit.WorldCoords.Y), 
                Mathf.Abs(circuit.WorldCoords.Z)
            );
            
            if (circuitShell == shellLevel)
            {
                results.Add(circuit);
            }
        }
        
        return results;
    }

    // Get circuits that are ready to emit (past their emission interval)
    public List<WorldCircuit> GetCircuitsReadyToEmit(ulong currentTime)
    {
        var results = new List<WorldCircuit>();
        
        foreach (var circuit in conn.Db.WorldCircuit.Iter())
        {
            if (currentTime >= circuit.LastEmissionTime + circuit.EmissionIntervalMs)
            {
                results.Add(circuit);
            }
        }
        
        return results;
    }

    // Get circuits with specific qubit count
    public List<WorldCircuit> GetCircuitsByQubitCount(byte qubitCount)
    {
        var results = new List<WorldCircuit>();
        
        foreach (var circuit in conn.Db.WorldCircuit.Iter())
        {
            if (circuit.QubitCount == qubitCount)
            {
                results.Add(circuit);
            }
        }
        
        return results;
    }

    // Get circuits within a range of coordinates
    public List<WorldCircuit> GetCircuitsInRange(WorldCoords center, int range)
    {
        var results = new List<WorldCircuit>();
        
        foreach (var circuit in conn.Db.WorldCircuit.Iter())
        {
            if (Mathf.Abs(circuit.WorldCoords.X - center.X) <= range &&
                Mathf.Abs(circuit.WorldCoords.Y - center.Y) <= range &&
                Mathf.Abs(circuit.WorldCoords.Z - center.Z) <= range)
            {
                results.Add(circuit);
            }
        }
        
        return results;
    }

    // Get high-emission circuits (more orbs per emission)
    public List<WorldCircuit> GetHighEmissionCircuits(uint minOrbsPerEmission)
    {
        var results = new List<WorldCircuit>();
        
        foreach (var circuit in conn.Db.WorldCircuit.Iter())
        {
            if (circuit.OrbsPerEmission >= minOrbsPerEmission)
            {
                results.Add(circuit);
            }
        }
        
        // Sort by emission count descending
        results.Sort((a, b) => b.OrbsPerEmission.CompareTo(a.OrbsPerEmission));
        
        return results;
    }

    // Check if a circuit exists at coordinates - using iteration
    public bool CircuitExistsAt(WorldCoords coords)
    {
        return conn.Db.WorldCircuit.Iter().Any(wc => 
            wc.WorldCoords.X == coords.X && 
            wc.WorldCoords.Y == coords.Y && 
            wc.WorldCoords.Z == coords.Z);
    }

    // Get adjacent circuits (6-connected neighbors)
    public List<WorldCircuit> GetAdjacentCircuits(WorldCoords coords)
    {
        var results = new List<WorldCircuit>();
        var offsets = new (sbyte x, sbyte y, sbyte z)[]
        {
            (1, 0, 0), (-1, 0, 0),
            (0, 1, 0), (0, -1, 0),
            (0, 0, 1), (0, 0, -1)
        };

        foreach (var offset in offsets)
        {
            var adjacentCoords = new WorldCoords(
                (sbyte)(coords.X + offset.x),
                (sbyte)(coords.Y + offset.y),
                (sbyte)(coords.Z + offset.z)
            );
            
            var circuit = conn.Db.WorldCircuit.Iter().FirstOrDefault(wc => 
                wc.WorldCoords.X == adjacentCoords.X && 
                wc.WorldCoords.Y == adjacentCoords.Y && 
                wc.WorldCoords.Z == adjacentCoords.Z);
                
            if (circuit != null)
            {
                results.Add(circuit);
            }
        }
        
        return results;
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

// Extension methods for even cleaner syntax when working with IEnumerable
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

// Helper to use with SpacetimeDB table handle - REMOVED as WorldCoords index doesn't exist
// The extension method approach above is cleaner anyway