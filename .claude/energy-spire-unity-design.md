# Energy Spire System - Unity Visualization Design

**Date:** 2025-10-12
**Status:** 📋 DESIGN - Fitting with existing manager patterns

## Established Unity Architecture Patterns

### Pattern Analysis from Existing Managers

**OrbVisualizationManager Pattern:**
- ✅ Subscribes to **GameEventBus** events (NOT direct database subscriptions)
- ✅ Uses **SystemDebug** with category-based logging
- ✅ Dictionary tracking: `Dictionary<ulong, GameObject> activeOrbs`
- ✅ OnEnable/OnDisable for subscription management
- ✅ Prefab + fallback primitive creation
- ✅ Separate Create/Update/Remove methods
- ✅ DontDestroyOnLoad NOT used (scene-specific)

**TransferVisualizationManager Pattern:**
- ⚠️ Subscribes to **SpacetimeDB tables directly** (older pattern)
- ✅ Uses coroutines for animations
- ✅ Calls reducers after visualization completes
- ✅ Simple GameObject.CreatePrimitive for MVP visualization
- ❌ Missing: Event-driven architecture (uses direct DB subscriptions)

**SpacetimeDBEventBridge Pattern:**
- ✅ **ONLY** component that touches SpacetimeDB tables
- ✅ Publishes GameEventBus events for all table changes
- ✅ DontDestroyOnLoad enabled (persists across scenes)
- ✅ Handles OnInsert/OnUpdate/OnDelete → publishes events

## Recommended Architecture for Energy Spire System

### Three-Manager Approach

```
SpacetimeDBEventBridge (existing)
  ├─ Subscribes to DistributionSphere table
  ├─ Subscribes to QuantumTunnel table
  ├─ Subscribes to WorldCircuit table (optional)
  └─ Publishes GameEventBus events

EnergySpireVisualizationManager (NEW)
  ├─ Subscribes to GameEventBus spire events
  ├─ Creates/updates/removes spire GameObjects
  ├─ Tracks: Dictionary<ulong, EnergySpireComponents>
  └─ Handles visual hierarchy (CircuitBase + Sphere + Ring)

TransferVisualizationManager (EXISTING - needs update)
  ├─ Update FlashSpire() to use EnergySpireVisualizationManager
  ├─ Find spires by position or ID
  └─ Apply flash effects during packet routing
```

### Data Flow

```
Server: DistributionSphere inserted
  ↓
SpacetimeDBEventBridge.OnDistributionSphereInsert()
  ↓
GameEventBus.Publish(new DistributionSphereInsertedEvent)
  ↓
EnergySpireVisualizationManager.OnDistributionSphereInserted()
  ↓
CreateSpireVisualization(sphere)
  ↓
GameObject hierarchy created in scene
```

## Component Hierarchy Design

### EnergySpireComponents (tracking struct)

```csharp
public class EnergySpireComponents
{
    public ulong SphereId { get; set; }
    public ulong? TunnelId { get; set; }
    public ulong? CircuitId { get; set; }

    public GameObject RootObject { get; set; }          // Parent container
    public GameObject CircuitBase { get; set; }         // Optional ground component
    public GameObject DistributionSphere { get; set; }  // Mid-level routing sphere
    public GameObject QuantumTunnelRing { get; set; }   // Top-level ring

    public Renderer SphereRenderer { get; set; }        // For flash effects
    public Renderer RingRenderer { get; set; }          // For charge visualization
}
```

### GameObject Hierarchy (per spire)

```
EnergySpire_North (root)
├── CircuitBase (optional)
│   ├── BaseMesh (hexagonal platform)
│   └── BaseLight (emissive glow)
├── DistributionSphere (required)
│   ├── SphereMesh (semi-transparent, radius ~40)
│   └── FlashEffect (flash when packets route through)
└── QuantumTunnelRing (required)
    ├── RingMesh (directional ring, ~60-75 units above surface)
    └── ChargeIndicator (fill based on ring_charge 0-100)
```

## Event Definitions (Add to GameEventBus.cs)

### New Event Types

```csharp
// DistributionSphere events
public class DistributionSphereInsertedEvent : IGameEvent
{
    public DistributionSphere Sphere { get; set; }
}

public class DistributionSphereUpdatedEvent : IGameEvent
{
    public DistributionSphere OldSphere { get; set; }
    public DistributionSphere NewSphere { get; set; }
}

public class DistributionSphereDeletedEvent : IGameEvent
{
    public DistributionSphere Sphere { get; set; }
}

// QuantumTunnel events
public class QuantumTunnelInsertedEvent : IGameEvent
{
    public QuantumTunnel Tunnel { get; set; }
}

public class QuantumTunnelUpdatedEvent : IGameEvent
{
    public QuantumTunnel OldTunnel { get; set; }
    public QuantumTunnel NewTunnel { get; set; }
}

public class QuantumTunnelDeletedEvent : IGameEvent
{
    public QuantumTunnel Tunnel { get; set; }
}

// WorldCircuit events (optional)
public class WorldCircuitInsertedEvent : IGameEvent
{
    public WorldCircuit Circuit { get; set; }
}

// Initial load events
public class InitialSpiresLoadedEvent : IGameEvent
{
    public List<DistributionSphere> Spheres { get; set; }
    public List<QuantumTunnel> Tunnels { get; set; }
}
```

## SpacetimeDBEventBridge Modifications

Add to `SubscribeToTableEvents()`:

```csharp
// Distribution sphere events
conn.Db.DistributionSphere.OnInsert += OnDistributionSphereInsert;
conn.Db.DistributionSphere.OnUpdate += OnDistributionSphereUpdate;
conn.Db.DistributionSphere.OnDelete += OnDistributionSphereDelete;

// Quantum tunnel events
conn.Db.QuantumTunnel.OnInsert += OnQuantumTunnelInsert;
conn.Db.QuantumTunnel.OnUpdate += OnQuantumTunnelUpdate;
conn.Db.QuantumTunnel.OnDelete += OnQuantumTunnelDelete;

// Optional: World circuit events
conn.Db.WorldCircuit.OnInsert += OnWorldCircuitInsert;
```

Add event handlers:

```csharp
void OnDistributionSphereInsert(EventContext ctx, DistributionSphere sphere)
{
    GameEventBus.Instance.Publish(new DistributionSphereInsertedEvent { Sphere = sphere });
}

void OnDistributionSphereUpdate(EventContext ctx, DistributionSphere oldSphere, DistributionSphere newSphere)
{
    GameEventBus.Instance.Publish(new DistributionSphereUpdatedEvent
    {
        OldSphere = oldSphere,
        NewSphere = newSphere
    });
}
// ... etc
```

Add to `LoadInitialOrbsForWorld()` section (new method):

```csharp
void LoadInitialSpiresForWorld(WorldCoords worldCoords)
{
    var spheres = new List<DistributionSphere>();
    var tunnels = new List<QuantumTunnel>();

    foreach (var sphere in conn.Db.DistributionSphere.Iter())
    {
        if (sphere.WorldCoords.Equals(worldCoords))
            spheres.Add(sphere);
    }

    foreach (var tunnel in conn.Db.QuantumTunnel.Iter())
    {
        if (tunnel.WorldCoords.Equals(worldCoords))
            tunnels.Add(tunnel);
    }

    if (spheres.Count > 0)
    {
        GameEventBus.Instance.Publish(new InitialSpiresLoadedEvent
        {
            Spheres = spheres,
            Tunnels = tunnels
        });
    }
}
```

## EnergySpireVisualizationManager Implementation

### Class Structure

```csharp
namespace SYSTEM.Game
{
    public class EnergySpireVisualizationManager : MonoBehaviour
    {
        [Header("Visualization Settings")]
        [SerializeField] private GameObject spirePrefab;         // Optional prefab
        [SerializeField] private GameObject circuitBasePrefab;   // Optional
        [SerializeField] private GameObject distributionSpherePrefab; // Optional
        [SerializeField] private GameObject tunnelRingPrefab;    // Optional

        [Header("Sizing")]
        [SerializeField] private float sphereRadius = 40f;        // Match server
        [SerializeField] private float ringHeight = 65f;          // ~60-75 units
        [SerializeField] private float circuitScale = 15f;

        [Header("Colors")]
        [SerializeField] private Color redTunnelColor = Color.red;
        [SerializeField] private Color greenTunnelColor = Color.green;
        [SerializeField] private Color blueTunnelColor = Color.blue;
        [SerializeField] private Color flashColor = Color.cyan;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;

        // Tracking
        private Dictionary<ulong, EnergySpireComponents> activeSpires =
            new Dictionary<ulong, EnergySpireComponents>();
        private Material sphereMaterial;
        private Material ringMaterial;

        void Awake()
        {
            SystemDebug.Log(SystemDebug.Category.OrbVisualization,
                "EnergySpireVisualizationManager Awake");
            CreateMaterials();
        }

        void OnEnable()
        {
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.Subscribe<DistributionSphereInsertedEvent>(OnSphereInserted);
                GameEventBus.Instance.Subscribe<DistributionSphereUpdatedEvent>(OnSphereUpdated);
                GameEventBus.Instance.Subscribe<QuantumTunnelInsertedEvent>(OnTunnelInserted);
                GameEventBus.Instance.Subscribe<QuantumTunnelUpdatedEvent>(OnTunnelUpdated);
                GameEventBus.Instance.Subscribe<InitialSpiresLoadedEvent>(OnInitialSpiresLoaded);
                GameEventBus.Instance.Subscribe<WorldTransitionStartedEvent>(OnWorldTransition);
            }
        }

        void OnDisable()
        {
            // Unsubscribe and cleanup
        }

        // Event handlers
        private void OnSphereInserted(DistributionSphereInsertedEvent evt) { }
        private void OnTunnelInserted(QuantumTunnelInsertedEvent evt) { }
        private void OnSphereUpdated(DistributionSphereUpdatedEvent evt) { }
        private void OnTunnelUpdated(QuantumTunnelUpdatedEvent evt) { }
        private void OnInitialSpiresLoaded(InitialSpiresLoadedEvent evt) { }

        // Visualization methods
        private EnergySpireComponents CreateSpireVisualization(
            DistributionSphere sphere, QuantumTunnel tunnel) { }
        private void UpdateSphereFlash(ulong sphereId) { }
        private void UpdateTunnelCharge(ulong tunnelId, float charge) { }

        // Public API for TransferVisualizationManager
        public void FlashSphereById(ulong sphereId) { }
        public GameObject GetSpireGameObject(ulong sphereId) { }
    }
}
```

## TransferVisualizationManager Updates

Update `FlashSpire()` method:

```csharp
private void FlashSpire(ulong spireId)
{
    // Find EnergySpireVisualizationManager in scene
    var spireManager = FindObjectOfType<EnergySpireVisualizationManager>();
    if (spireManager != null)
    {
        spireManager.FlashSphereById(spireId);
    }
    else
    {
        UnityEngine.Debug.LogWarning("[TransferVisualization] EnergySpireVisualizationManager not found");
    }
}
```

## SystemDebug Category Addition

Add to `SystemDebug.cs`:

```csharp
public enum Category
{
    // ... existing categories ...
    SpireSystem,           // DistributionSphere/QuantumTunnel database events
    SpireVisualization,    // Spire GameObject creation and rendering
}
```

## MVP Implementation Plan

### Phase 1: Basic Visualization (MVP)
1. ✅ Server tables and reducers complete
2. ⏳ Add events to GameEventBus.cs
3. ⏳ Add table subscriptions to SpacetimeDBEventBridge.cs
4. ⏳ Create EnergySpireVisualizationManager with primitives (no prefabs)
5. ⏳ Simple sphere + ring visualization at correct positions

### Phase 2: Visual Polish
1. Create prefabs for CircuitBase, DistributionSphere, QuantumTunnelRing
2. Add materials and shaders
3. Implement flash effects
4. Add charge fill visualization
5. Particle effects

### Phase 3: Advanced Features
1. Ring rotation toward connected world
2. Tunnel beam visualization
3. Energy flow particles
4. LOD system for distant spires
5. Secondary/tertiary tier spires (12 + 8)

## Key Design Decisions

1. **Event-Driven Architecture**: Follow OrbVisualizationManager pattern, NOT TransferVisualizationManager
2. **SpacetimeDBEventBridge is Gateway**: Only component that touches database
3. **MVP with Primitives**: Use CreatePrimitive for initial implementation
4. **Dictionary Tracking**: Track spires by sphere_id
5. **Hierarchy Management**: Match DistributionSphere with QuantumTunnel by world_coords + cardinal_direction
6. **SystemDebug Logging**: Use SpireSystem and SpireVisualization categories
7. **No DontDestroyOnLoad**: Spires are world-specific, recreated on world transition

## Testing Strategy

```bash
# 1. Spawn main 6 spires on origin world
spacetime call system spawn_main_spires 0 0 0

# 2. Load game scene in Unity
# 3. Check EnergySpireVisualizationManager logs
# 4. Verify 6 spire GameObjects created
# 5. Check positions match cardinal directions (N/S/E/W/Forward/Back)

# 6. Test routing flash effect
spacetime call system initialize_player_inventory
spacetime call system create_storage_device 0.0 100.0 0.0
spacetime call system initiate_transfer 10 1

# 7. Watch TransferVisualizationManager flash spheres as packets route through
```

## File Checklist

### New Files to Create
- [ ] `EnergySpireVisualizationManager.cs` - Main visualization manager
- [ ] Event definitions (add to `GameEventBus.cs`)

### Files to Modify
- [ ] `SpacetimeDBEventBridge.cs` - Add spire table subscriptions
- [ ] `TransferVisualizationManager.cs` - Update FlashSpire() method
- [ ] `SystemDebug.cs` - Add SpireSystem and SpireVisualization categories
- [ ] `GameEventBus.cs` - Add spire events to allowedEventsPerState

### Testing Files
- [ ] Test scene with EnergySpireVisualizationManager component
- [ ] Debug menu to spawn spires on command
