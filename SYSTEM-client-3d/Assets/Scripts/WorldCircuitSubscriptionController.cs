using UnityEngine;
using UnityEngine.Events;
using SpacetimeDB.Types;
using System.Linq;
using SpacetimeDB;
using SYSTEM.Game;

/// <summary>
/// Manages world and circuit subscriptions
/// </summary>
public class WorldCircuitSubscriptionController : SubscribableController
{
    public override string GetControllerName() => "WorldCircuitController";
    
    [Header("Circuit Events")]
    public UnityEvent<WorldCircuit> OnCircuitLoaded = new UnityEvent<WorldCircuit>();
    public UnityEvent<WorldCircuit> OnCircuitUpdated = new UnityEvent<WorldCircuit>();
    
    private WorldCircuit currentCircuit;
    
    void Start()
    {
        // Auto-register with orchestrator
        SubscriptionOrchestrator.Instance?.RegisterController(this);
    }
    
    public override void Subscribe(WorldCoords worldCoords)
    {
        if (!GameManager.IsConnected()) return;
        
        Unsubscribe(); // Clean up any existing subscription
        
        // For now, subscribe to all worlds and circuits, filter client-side
        string[] queries = new string[]
        {
            "SELECT * FROM world",
            "SELECT * FROM world_circuit"
        };
        
        currentSubscription = conn.SubscriptionBuilder()
            .OnApplied((ctx) => 
            {
                OnSubscriptionApplied();
                LoadInitialCircuit(worldCoords);
            })
            .OnError((ctx, error) => OnSubscriptionError(error))
            .Subscribe(queries);
            
        // Setup event handlers
        conn.Db.WorldCircuit.OnInsert += HandleCircuitInsert;
        conn.Db.WorldCircuit.OnUpdate += HandleCircuitUpdate;
        conn.Db.WorldCircuit.OnDelete += HandleCircuitDelete;
    }
    
    public override void Unsubscribe()
    {
        currentSubscription?.Unsubscribe();
        currentSubscription = null;
        
        // Cleanup event handlers
        if (conn != null)
        {
            conn.Db.WorldCircuit.OnInsert -= HandleCircuitInsert;
            conn.Db.WorldCircuit.OnUpdate -= HandleCircuitUpdate;
            conn.Db.WorldCircuit.OnDelete -= HandleCircuitDelete;
        }
        
        isSubscribed = false;
    }
    
    void LoadInitialCircuit(WorldCoords worldCoords)
    {
        // Use the index to find the circuit
        currentCircuit = conn.Db.WorldCircuit.Iter().FirstOrDefault(wc => 
            wc.WorldCoords.X == worldCoords.X && 
            wc.WorldCoords.Y == worldCoords.Y && 
            wc.WorldCoords.Z == worldCoords.Z);
        if (currentCircuit != null)
        {
            OnCircuitLoaded?.Invoke(currentCircuit);
        }
    }
    
    void HandleCircuitInsert(EventContext ctx, WorldCircuit circuit)
    {
        if (IsRelevantCircuit(circuit))
        {
            currentCircuit = circuit;
            OnCircuitLoaded?.Invoke(circuit);
        }
    }
    
    void HandleCircuitUpdate(EventContext ctx, WorldCircuit oldCircuit, WorldCircuit newCircuit)
    {
        if (IsRelevantCircuit(newCircuit))
        {
            currentCircuit = newCircuit;
            OnCircuitUpdated?.Invoke(newCircuit);
        }
    }
    
    void HandleCircuitDelete(EventContext ctx, WorldCircuit circuit)
    {
        if (IsRelevantCircuit(circuit))
        {
            currentCircuit = null;
        }
    }
    
    bool IsRelevantCircuit(WorldCircuit circuit)
    {
        var currentCoords = GameData.Instance?.GetCurrentWorldCoords();
        return currentCoords != null && 
               circuit.WorldCoords.X == currentCoords.X &&
               circuit.WorldCoords.Y == currentCoords.Y &&
               circuit.WorldCoords.Z == currentCoords.Z;
    }
    
    public WorldCircuit GetCurrentCircuit() => currentCircuit;
    public bool HasCircuit() => currentCircuit != null;
}