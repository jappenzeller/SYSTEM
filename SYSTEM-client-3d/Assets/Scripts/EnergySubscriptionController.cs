using System.Collections.Generic;
using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Manages energy-related subscriptions (orbs and puddles)
/// </summary>
public class EnergySubscriptionController : SubscribableController
{
    public override string GetControllerName() => "EnergyController";
    
    private Dictionary<ulong, EnergyOrb> trackedOrbs = new Dictionary<ulong, EnergyOrb>();
    private Dictionary<ulong, EnergyPuddle> trackedPuddles = new Dictionary<ulong, EnergyPuddle>();
    
    void Start()
    {
        SubscriptionOrchestrator.Instance?.RegisterController(this);
    }
    
    public override void Subscribe(WorldCoords worldCoords)
    {
        if (!GameManager.IsConnected()) return;
        
        Unsubscribe();
        
        // Subscribe to all energy data, filter client-side
        string[] queries = new string[]
        {
            "SELECT * FROM energy_orb",
            "SELECT * FROM energy_puddle"
        };
        
        currentSubscription = conn.SubscriptionBuilder()
            .OnApplied((ctx) => 
            {
                OnSubscriptionApplied();
                LoadInitialEnergy();
            })
            .OnError((ctx, error) => OnSubscriptionError(error))
            .Subscribe(queries);
            
        // Setup event handlers
        conn.Db.EnergyOrb.OnInsert += HandleOrbInsert;
        conn.Db.EnergyOrb.OnUpdate += HandleOrbUpdate;
        conn.Db.EnergyOrb.OnDelete += HandleOrbDelete;
        
        conn.Db.EnergyPuddle.OnInsert += HandlePuddleInsert;
        conn.Db.EnergyPuddle.OnUpdate += HandlePuddleUpdate;
        conn.Db.EnergyPuddle.OnDelete += HandlePuddleDelete;
    }
    
    public override void Unsubscribe()
    {
        currentSubscription?.Unsubscribe();
        currentSubscription = null;
        
        if (conn != null)
        {
            conn.Db.EnergyOrb.OnInsert -= HandleOrbInsert;
            conn.Db.EnergyOrb.OnUpdate -= HandleOrbUpdate;
            conn.Db.EnergyOrb.OnDelete -= HandleOrbDelete;
            
            conn.Db.EnergyPuddle.OnInsert -= HandlePuddleInsert;
            conn.Db.EnergyPuddle.OnUpdate -= HandlePuddleUpdate;
            conn.Db.EnergyPuddle.OnDelete -= HandlePuddleDelete;
        }
        
        trackedOrbs.Clear();
        trackedPuddles.Clear();
        isSubscribed = false;
    }
    
    void LoadInitialEnergy()
    {
        // Initial load handled by events
        Debug.Log($"[{GetControllerName()}] Initial load - Orbs: {trackedOrbs.Count}, Puddles: {trackedPuddles.Count}");
    }
    
    void HandleOrbInsert(EventContext ctx, EnergyOrb orb)
    {
        if (IsInCurrentWorld(orb.WorldCoords))
        {
            trackedOrbs[orb.OrbId] = orb;
            // Notify other systems
            EventBus.Publish(new EnergyOrbCreatedEvent { Orb = orb });
        }
    }
    
    void HandleOrbUpdate(EventContext ctx, EnergyOrb oldOrb, EnergyOrb newOrb)
    {
        if (trackedOrbs.ContainsKey(newOrb.OrbId))
        {
            trackedOrbs[newOrb.OrbId] = newOrb;
            EventBus.Publish(new EnergyOrbUpdatedEvent { OldOrb = oldOrb, NewOrb = newOrb });
        }
    }
    
    void HandleOrbDelete(EventContext ctx, EnergyOrb orb)
    {
        if (trackedOrbs.Remove(orb.OrbId))
        {
            EventBus.Publish(new EnergyOrbDeletedEvent { Orb = orb });
        }
    }
    
    void HandlePuddleInsert(EventContext ctx, EnergyPuddle puddle)
    {
        if (IsInCurrentWorld(puddle.WorldCoords))
        {
            trackedPuddles[puddle.PuddleId] = puddle;
            EventBus.Publish(new EnergyPuddleCreatedEvent { Puddle = puddle });
        }
    }
    
    void HandlePuddleUpdate(EventContext ctx, EnergyPuddle oldPuddle, EnergyPuddle newPuddle)
    {
        if (trackedPuddles.ContainsKey(newPuddle.PuddleId))
        {
            trackedPuddles[newPuddle.PuddleId] = newPuddle;
            EventBus.Publish(new EnergyPuddleUpdatedEvent { OldPuddle = oldPuddle, NewPuddle = newPuddle });
        }
    }
    
    void HandlePuddleDelete(EventContext ctx, EnergyPuddle puddle)
    {
        if (trackedPuddles.Remove(puddle.PuddleId))
        {
            EventBus.Publish(new EnergyPuddleDeletedEvent { Puddle = puddle });
        }
    }
    
    bool IsInCurrentWorld(WorldCoords coords)
    {
        var currentCoords = GameData.Instance?.GetCurrentWorldCoords();
        return currentCoords != null && 
               coords.X == currentCoords.X &&
               coords.Y == currentCoords.Y &&
               coords.Z == currentCoords.Z;
    }
    
    // Public API
    public IEnumerable<EnergyOrb> GetTrackedOrbs() => trackedOrbs.Values;
    public IEnumerable<EnergyPuddle> GetTrackedPuddles() => trackedPuddles.Values;
    public int OrbCount => trackedOrbs.Count;
    public int PuddleCount => trackedPuddles.Count;
}