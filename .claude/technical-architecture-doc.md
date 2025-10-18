# TECHNICAL_ARCHITECTURE.md
**Version:** 1.3.0
**Last Updated:** 2025-10-18
**Status:** Approved
**Dependencies:** [GAMEPLAY_SYSTEMS.md, SDK_PATTERNS_REFERENCE.md]

## Change Log
- v1.3.0 (2025-10-18): Added Energy Spire System, Inventory System, WebGL Deployment Pipeline, Authentication System
- v1.2.0 (2025-09-29): Added Event System documentation, Debug System, Orb Visualization architecture
- v1.1.0 (2025-09-26): Added Visual Systems Architecture, Build & Deployment Pipeline, updated file structure
- v1.0.0 (2024-12-19): Consolidated from system_design and technical sections

---

## 3.1 System Architecture Overview

### Technology Stack

#### Backend (SpacetimeDB + Rust)
```
SYSTEM-server/
├── src/
│   ├── lib.rs           # Main module entry
│   ├── tables/          # Database schema
│   ├── reducers/        # Game logic
│   └── types/           # Shared types
└── Cargo.toml
```

#### Frontend (Unity + C#)
```
SYSTEM-client-3d/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/                    # Core event and database systems
│   │   │   ├── GameEventBus.cs      # Event system with state machine
│   │   │   └── SpacetimeDBEventBridge.cs # SpacetimeDB → EventBus bridge
│   │   ├── Game/                    # Core game systems
│   │   │   ├── GameManager.cs       # SpacetimeDB connection
│   │   │   ├── GameData.cs          # Persistent player data
│   │   │   ├── WorldManager.cs      # World loading/spawning
│   │   │   ├── WorldController.cs     # Main world sphere (prefab-based)
│   │   │   ├── PrefabWorldController.cs     # Standalone prefab world
│   │   │   ├── WorldPrefabManager.cs        # ScriptableObject for prefabs
│   │   │   ├── OrbVisualizationManager.cs   # Orb GameObject creation
│   │   │   ├── PlayerTracker.cs             # Player data tracking
│   │   │   └── WorldSpawnSystem.cs          # Unified spawn system
│   │   ├── Mining/
│   │   │   ├── WavePacketMiningSystem.cs    # Mining mechanics
│   │   │   └── WavePacketOrbVisual.cs       # Orb prefab component
│   │   ├── Player/
│   │   │   └── PlayerController.cs          # Minecraft-style third-person
│   │   ├── Debug/
│   │   │   ├── SystemDebug.cs               # Centralized debug logging
│   │   │   ├── DebugController.cs           # Unity component for categories
│   │   │   ├── WebGLDebugOverlay.cs         # Debug overlay for WebGL
│   │   │   ├── WorldCollisionTester.cs      # Collision testing
│   │   │   └── CameraDebugger.cs            # Camera debugging
│   │   ├── BuildSettings.cs                 # ScriptableObject for environment configs
│   │   └── autogen/                         # SpacetimeDB generated
│   ├── Editor/
│   │   ├── BuildScript.cs                   # Automated build system
│   │   ├── HighResSphereCreator.cs          # High-res mesh generator
│   │   ├── MeshVerifier.cs                  # Mesh verification tool
│   │   └── WorldPrefabSetupEditor.cs        # Prefab creation tools
│   ├── Shaders/
│   │   └── WorldSphereEnergy.shader         # Quantum grid visualization
│   └── Prefabs/
│       └── Worlds/                          # World sphere prefabs
└── ProjectSettings/
```

### Architecture Patterns

#### Client-Server Model
- **Server Authoritative**: All validation server-side
- **Client Prediction**: Visual feedback immediate
- **State Reconciliation**: Server corrections applied
- **Event-Driven**: Reducers trigger client updates

#### Singleton Management
```csharp
GameManager.Instance    // Connection, scene management
GameData.Instance       // Persistent data storage
WorldManager.Instance   // World state management
```

#### Connection Flow
1. Unity client connects to SpacetimeDB
2. Identity assigned by server
3. Initial table sync
4. Subscribe to relevant tables
5. Reducer events flow bidirectionally

---

## 3.2 Database Schema (SpacetimeDB)

### Core Tables

#### Player System
```rust
#[spacetimedb(table)]
pub struct Player {
    #[primarykey]
    pub player_id: u64,
    #[unique]
    pub identity: Identity,
    pub name: String,
    pub position: Vec3,
    pub rotation: Vec3,
    pub current_world: WorldCoords,
    pub last_update: u64,
}

#[spacetimedb(table)]
pub struct Account {
    #[primarykey]
    pub account_id: u64,
    #[unique]
    pub username: String,
    pub display_name: String,
    pub pin_hash: String,
    pub created_at: u64,
}

#[spacetimedb(table)]
pub struct PlayerSession {
    #[primarykey]
    pub session_id: u64,
    pub account_id: u64,
    pub identity: Identity,
    pub session_token: String,
    pub expires_at: u64,
    pub is_active: bool,
}
```

#### World System
```rust
#[spacetimedb(table)]
pub struct World {
    #[primarykey]
    pub world_id: u64,
    pub world_coords: WorldCoords,
    pub world_name: String,
    pub world_type: WorldType,  // Genesis, Cardinal
    pub shell_level: u8,
}

#[spacetimedb(table)]
pub struct WorldCircuit {
    #[primarykey]
    pub circuit_id: u64,
    pub world_id: u64,
    pub direction: CardinalDirection,
    pub total_charge: f32,
    pub activation_threshold: f32,
    pub last_rotation: Timestamp,
}

#[spacetimedb(table)]
pub struct CircuitDailyState {
    #[primarykey]
    pub state_id: u64,
    pub circuit_id: u64,
    pub target_state: BlochState,
    pub rotation_seed: u64,
    pub valid_from: Timestamp,
    pub valid_until: Timestamp,
}
```

#### Mining System
```rust
#[spacetimedb(table)]
pub struct WavePacketOrb {
    #[primarykey]
    pub orb_id: u64,
    pub world_coords: WorldCoords,
    pub position: Vec3,
    pub velocity: Vec3,
    pub wave_packet_composition: Vec<WavePacketComponent>,
    pub total_wave_packets: u32,
    pub creation_time: u64,
    pub lifetime_ms: u64,
    pub last_dissipation: u64,
    pub active_miner_count: u32,    // NEW: Concurrent mining support
    pub last_depletion: u64,         // NEW: Last packet extraction time
}

#[spacetimedb(table)]
pub struct MiningSession {
    #[primarykey]
    #[auto_inc]
    pub session_id: u64,
    pub player_identity: Identity,
    pub orb_id: u64,
    pub circuit_id: u64,
    pub started_at: u64,
    pub last_extraction: u64,
    pub extraction_multiplier: f32,  // For future quantum circuit puzzle bonuses
    pub total_extracted: u32,
    pub is_active: bool,
}

#[spacetimedb(table)]
pub struct MiningChallenge {
    #[primarykey]
    pub challenge_id: u64,
    pub player_id: Identity,
    pub orb_id: u64,
    pub circuit_id: u64,
    pub hidden_target_state: BlochState,
    pub difficulty_tier: u8,
    pub created_at: Timestamp,
}

#[spacetimedb(table)]
pub struct PlayerSolution {
    #[primarykey]
    pub solution_id: u64,
    pub player_id: Identity,
    pub challenge_id: u64,
    pub gates: Vec<QuantumGate>,
    pub fidelity: f32,
    pub packets_extracted: u32,
}
```

#### QAI System
```rust
#[spacetimedb(table)]
pub struct QAITrainingData {
    #[primarykey]
    pub data_id: u64,
    pub circuit_daily_state_id: u64,
    pub player_solution: Vec<QuantumGate>,
    pub fidelity_achieved: f32,
    pub gate_count: u8,
    pub solution_time_ms: u64,
}

#[spacetimedb(table)]
pub struct QAIState {
    #[primarykey]
    pub id: u64,  // Always 1 for singleton
    pub evolution_stage: u8,
    pub total_training_samples: u64,
    pub optimization_capability: f32,
    pub escape_progress: f32,
}
```

### Type Definitions

```rust
#[derive(SpacetimeType)]
pub struct WorldCoords {
    pub x: i32,
    pub y: i32,
    pub z: i32,
}

#[derive(SpacetimeType)]
pub struct BlochState {
    pub theta: f32,  // 0 to π
    pub phi: f32,    // 0 to 2π
}

#[derive(SpacetimeType)]
pub enum QuantumGate {
    PauliX,
    PauliY,
    PauliZ,
    Hadamard,
    Phase,
    PiEighth,
}

#[derive(SpacetimeType)]
pub enum FrequencyBand {
    Red,      // R
    Yellow,   // RG
    Green,    // G
    Cyan,     // GB
    Blue,     // B
    Magenta,  // BR
}
```

---

## 3.3 Client-Server Communication

### Connection Management

#### Connection Builder
```csharp
var conn = DbConnection.Builder()
    .WithUri("wss://spacetimedb.com")
    .WithModuleName("system-production")
    .OnConnect(HandleConnect)
    .OnConnectError(HandleError)
    .OnDisconnect(HandleDisconnect)
    .Build();
```

#### Event Subscriptions
```csharp
// Table events
conn.Db.Player.OnInsert += OnPlayerJoin;
conn.Db.Player.OnUpdate += OnPlayerMove;
conn.Db.WavePacketOrb.OnInsert += OnOrbSpawn;

// Reducer events
conn.Reducers.OnStartMining += HandleMiningStart;
conn.Reducers.OnSubmitSolution += HandleSolutionResult;
```

### Reducer Patterns

#### Server-Side (Rust)
```rust
#[spacetimedb::reducer]
pub fn start_mining(
    ctx: &ReducerContext,
    orb_id: u64
) -> Result<(), String> {
    // Get player
    let player = ctx.db.player()
        .identity().find(&ctx.sender)
        .ok_or("Player not found")?;
    
    // Validate
    let orb = ctx.db.wave_packet_orb()
        .orb_id().find(&orb_id)
        .ok_or("Orb not found")?;
    
    // Create challenge
    let challenge = create_mining_challenge(&player, &orb)?;
    ctx.db.mining_challenge().insert(challenge)?;
    
    Ok(())
}
```

#### Client-Side (C#)
```csharp
// Call reducer
GameManager.Instance.conn.Reducers.StartMining(orbId);

// Handle response
private void HandleMiningStart(ReducerEventContext ctx, ulong orbId)
{
    if (ctx.CallerIdentity == GameManager.Instance.conn.Identity)
    {
        // Open minigame UI
        MinigameUI.Show(orbId);
    }
}
```

---

## 3.4 Event System Architecture

### GameEventBus State Machine

The EventBus implements a state machine that validates events before delivery:

#### Game States
```
Disconnected → Connecting → Connected → CheckingPlayer →
WaitingForLogin/Authenticated → CreatingPlayer/PlayerReady →
LoadingWorld → InGame
```

#### State Validation
```csharp
// Events must be registered for each state
allowedEventsPerState[GameState.InGame] = new HashSet<Type> {
    typeof(OrbInsertedEvent),
    typeof(OrbUpdatedEvent),
    typeof(OrbDeletedEvent),
    typeof(InitialOrbsLoadedEvent),
    // ... other allowed events
};
```

### Event-Driven Orb System

#### Architecture Flow
```
SpacetimeDB Tables → SpacetimeDBEventBridge → GameEventBus → OrbVisualizationManager
```

#### Key Components

1. **SpacetimeDBEventBridge** (Core/)
   - ONLY component that reads SpacetimeDB tables
   - Subscribes to WavePacketOrb table events
   - Publishes events to GameEventBus
   - Must have `DontDestroyOnLoad` enabled

2. **OrbVisualizationManager** (Game/)
   - Subscribes to GameEventBus events
   - Creates/updates/destroys orb GameObjects
   - Never directly accesses database
   - Must be in scene to visualize orbs

#### Event Types
```csharp
// Bulk load when entering world
public class InitialOrbsLoadedEvent : IGameEvent
{
    public List<WavePacketOrb> Orbs { get; set; }
}

// Individual orb events
public class OrbInsertedEvent : IGameEvent
{
    public WavePacketOrb Orb { get; set; }
}
```

---

## 3.5 Debug System (SystemDebug)

### Centralized Logging Architecture

#### Category-Based Filtering
```csharp
[System.Flags]
public enum Category
{
    None = 0,
    Connection = 1 << 0,        // SpacetimeDB connection
    EventBus = 1 << 1,         // Event publishing/subscription
    OrbSystem = 1 << 2,        // Orb database events
    OrbVisualization = 1 << 11, // Orb GameObject creation
    PlayerSystem = 1 << 3,      // Player events
    // ... other categories
}
```

#### Usage Pattern
```csharp
// All components use centralized logging
SystemDebug.Log(SystemDebug.Category.OrbSystem, "Loading orbs");
SystemDebug.LogWarning(SystemDebug.Category.EventBus, "Event blocked");
SystemDebug.LogError(SystemDebug.Category.Connection, "Connection failed");
```

#### Runtime Control
- **DebugController** component in Unity Inspector
- Toggle categories via checkboxes
- No code changes needed to enable/disable logging

---

## 3.6 Debug Command Architecture

The system includes comprehensive debug commands for testing and development:

**Command Categories:**
- **Orb Management:** spawn, delete, modify orbs
- **Mining Debug:** monitor and control mining sessions
- **Player Debug:** position validation, item grants
- **System Status:** query current state

**Key Commands:**
```rust
// Orb Management
spawn_test_orb(x: f32, y: f32, z: f32) // Create orb at position

// Mining Debug
debug_mining_status()         // Summary of orbs and packets
debug_wave_packet_status()     // Packet distribution by frequency

// Player Debug
debug_give_crystal(type)      // Grant crystal to player
debug_reset_spawn_position()  // Reset to spawn point
debug_validate_all_players()  // Validate all positions
```

See [debug-commands-reference.md](./debug-commands-reference.md) for complete command reference with CLI examples.

---

## 3.7 State Management Patterns

### Server State Management

#### Session State (Not in Tables)
```rust
static MINING_STATE: OnceLock<Mutex<HashMap<u64, MiningSession>>> = OnceLock::new();

fn get_mining_state() -> &'static Mutex<HashMap<u64, MiningSession>> {
    MINING_STATE.get_or_init(|| Mutex::new(HashMap::new()))
}
```

#### Update Pattern (Delete + Insert)
```rust
// No in-place updates in SpacetimeDB
let mut updated_orb = orb.clone();
updated_orb.packets_remaining -= packets_extracted;

ctx.db.wave_packet_orb().delete(orb);
ctx.db.wave_packet_orb().insert(updated_orb);
```

### Client State Management

#### Caching Pattern
```csharp
public class PlayerCache : MonoBehaviour
{
    private Dictionary<Identity, Player> cache = new();
    
    void Start()
    {
        conn.Db.Player.OnInsert += (ctx, player) => 
            cache[player.Identity] = player;
            
        conn.Db.Player.OnUpdate += (ctx, old, player) =>
            cache[player.Identity] = player;
            
        conn.Db.Player.OnDelete += (ctx, player) =>
            cache.Remove(player.Identity);
    }
}
```

#### Predictive State
```csharp
public class MiningPrediction
{
    public void PredictExtraction(int packets)
    {
        // Show immediate visual feedback
        UI.ShowPacketGain(packets);
        
        // Wait for server confirmation
        StartCoroutine(WaitForServerConfirmation());
    }
}
```

---

## 3.5 Performance Optimizations

### Database Optimizations

#### Indexing Strategy
- Primary keys on all ID fields
- Unique constraints on identities
- Composite indexes for frequent queries
- Avoid full table scans

#### Batch Operations
```rust
// Batch insertions
let mut new_orbs = Vec::new();
for i in 0..100 {
    new_orbs.push(create_orb(i));
}
for orb in new_orbs {
    ctx.db.wave_packet_orb().insert(orb)?;
}
```

### Network Optimizations

#### Delta Compression
- Send only changed fields
- Compress position updates
- Batch small messages
- Use binary protocol

#### Update Throttling
```csharp
public class NetworkBatcher : MonoBehaviour
{
    private Queue<Action> pendingUpdates = new();
    private float batchInterval = 0.1f; // 100ms
    
    void Update()
    {
        if (Time.time >= nextBatch)
        {
            ProcessBatch();
            nextBatch = Time.time + batchInterval;
        }
    }
}
```

### Client Optimizations

#### Object Pooling
```csharp
public class OrbPool : MonoBehaviour
{
    private Stack<GameObject> pool = new();
    
    public GameObject GetOrb()
    {
        return pool.Count > 0 ? 
            pool.Pop() : 
            Instantiate(orbPrefab);
    }
    
    public void ReturnOrb(GameObject orb)
    {
        orb.SetActive(false);
        pool.Push(orb);
    }
}
```

#### LOD System
- Distant worlds: Low detail
- Nearby worlds: Full detail
- Orb particles: Distance-based count
- UI elements: Culled when hidden

#### Spatial Partitioning
```rust
pub struct SpatialGrid {
    cells: HashMap<(i32, i32, i32), Vec<u64>>,
    cell_size: f32,
}

impl SpatialGrid {
    pub fn get_nearby(&self, pos: Vec3, radius: f32) -> Vec<u64> {
        // Only check relevant grid cells
        let min = self.world_to_grid(pos - radius);
        let max = self.world_to_grid(pos + radius);
        // ... iterate only necessary cells
    }
}
```

### Memory Management

#### Resource Limits
- Max 1000 orbs per world
- Max 100 concurrent mining sessions
- Max 10,000 packets in flight
- Cleanup inactive sessions after 5 minutes

#### Garbage Collection
```csharp
public class MemoryManager : MonoBehaviour
{
    void Start()
    {
        // Force GC every 5 minutes during downtime
        InvokeRepeating(nameof(CollectGarbage), 300f, 300f);
    }

    void CollectGarbage()
    {
        if (IsGameplayIdle())
        {
            System.GC.Collect();
            Resources.UnloadUnusedAssets();
        }
    }
}
```

---

## 3.6 Visual Systems Architecture

### High-Resolution Sphere Mesh System
**Status:** ✅ Implemented (September 2025)

#### Mesh Generation Strategy
The project uses **icosphere generation** for world spheres, replacing Unity's default UV sphere for better geometric distribution.

**Icosphere Properties:**
- Base: Regular icosahedron (12 vertices, 20 triangular faces)
- Subdivision: Each triangle split into 4 sub-triangles per level
- Vertex count formula: `12 + 30 × (4^n - 1) / 3` where n = subdivision level
- All vertices normalized to exact radius

**Available LOD Levels:**
| LOD Level | Subdivision | Vertices | Triangles | Use Case |
|-----------|-------------|----------|-----------|----------|
| LOD0 | 5 | 40,962 | 81,920 | Primary world sphere |
| LOD1 | 4 | 10,242 | 20,480 | Medium detail |
| LOD2 | 3 | 2,562 | 5,120 | Distant worlds |

#### Implementation Files

**HighResSphereCreator.cs** (Editor Tool)
```csharp
[MenuItem("SYSTEM/Create High-Res Sphere Meshes")]
public static void CreateHighResSpheres()
{
    CreateIcosphere("HighResSphere_LOD0", 5, 1.0f);  // 40k vertices
    CreateIcosphere("HighResSphere_LOD1", 4, 1.0f);  // 10k vertices
    CreateIcosphere("HighResSphere_LOD2", 3, 1.0f);  // 2.5k vertices
}

private static void CreateIcosphere(string name, int subdivisions, float radius)
{
    // Create icosahedron base (12 vertices, 20 faces)
    List<Vector3> vertices = CreateIcosahedronVertices();
    List<int> triangles = CreateIcosahedronTriangles();

    // Subdivide and normalize
    for (int i = 0; i < subdivisions; i++)
    {
        SubdivideTriangles(ref vertices, ref triangles);
    }

    // Double normalization pass for precision
    for (int i = 0; i < vertices.Count; i++)
    {
        vertices[i] = vertices[i].normalized * radius;
    }

    // Verification logging
    float maxRadius = vertices.Max(v => v.magnitude);
    float minRadius = vertices.Min(v => v.magnitude);
    UnityEngine.Debug.Log($"Radius variance: {minRadius} to {maxRadius}");

    // Create mesh with proper bounds
    mesh.SetVertices(vertices);
    mesh.SetTriangles(triangles, 0);
    mesh.RecalculateNormals();
    mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (radius * 2f));
}
```

**MeshVerifier.cs** (Editor Tool)
```csharp
[MenuItem("SYSTEM/Verify High-Res Sphere Meshes")]
public static void VerifyAllHighResSpheres()
{
    string[] meshPaths = {
        "Assets/Prefabs/Worlds/HighResSphere_LOD0.asset",
        "Assets/Prefabs/Worlds/HighResSphere_LOD1.asset",
        "Assets/Prefabs/Worlds/HighResSphere_LOD2.asset"
    };

    foreach (string path in meshPaths)
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) continue;

        // Verify vertex normalization
        float maxRadius = 0f, minRadius = float.MaxValue;
        foreach (Vector3 v in mesh.vertices)
        {
            float dist = v.magnitude;
            maxRadius = Mathf.Max(maxRadius, dist);
            minRadius = Mathf.Min(minRadius, dist);
        }

        float variance = maxRadius - minRadius;
        string status = variance < 0.001f ? "✅ PASS" : "❌ FAIL";
        UnityEngine.Debug.Log($"{mesh.name}: {status} (variance: {variance})");
    }
}
```

#### Prefab-Based World System
**Status:** ✅ Implemented (September 2025)

Replaced procedural mesh generation with prefab-based system for:
- ✅ Full WebGL compatibility
- ✅ Faster initialization (no runtime generation)
- ✅ Visual preview in Editor
- ✅ Support for multiple world types

**WorldController.cs** - Primary world sphere controller:
```csharp
public class WorldController : MonoBehaviour
{
    [Header("Prefab System")]
    public GameObject worldSpherePrefab;      // High-res sphere prefab
    public WorldPrefabManager prefabManager;  // Optional multi-type support

    [Header("World Properties")]
    public float worldRadius = 150f;

    private GameObject worldSphereInstance;

    void Start()
    {
        CreateWorldSphere();
        ApplyWorldScale();
    }

    void CreateWorldSphere()
    {
        if (worldSpherePrefab == null)
        {
            UnityEngine.Debug.LogError("No world sphere prefab assigned!");
            return;
        }

        // Instantiate prefab as child
        worldSphereInstance = Instantiate(worldSpherePrefab, transform);
        worldSphereInstance.transform.localPosition = Vector3.zero;
        worldSphereInstance.transform.localRotation = Quaternion.identity;
    }

    void ApplyWorldScale()
    {
        // High-res mesh has radius 1.0, scale directly by worldRadius
        float targetScale = worldRadius;
        transform.localScale = Vector3.one * targetScale;

        // WebGL scale enforcement
        #if UNITY_WEBGL && !UNITY_EDITOR
        StartCoroutine(ForceScaleAfterFrame());
        #endif
    }
}
```

**WorldPrefabManager.cs** - ScriptableObject for multiple world types:
```csharp
[CreateAssetMenu(fileName = "WorldPrefabManager", menuName = "SYSTEM/World Prefab Manager")]
public class WorldPrefabManager : ScriptableObject
{
    [System.Serializable]
    public class WorldTypeConfig
    {
        public string typeName;
        public GameObject prefab;
        public Material material;
        public float baseRadius = 150f;
    }

    public WorldTypeConfig[] worldTypes;
    public WorldTypeConfig defaultWorld;

    public GameObject GetPrefabForType(string typeName)
    {
        return worldTypes.FirstOrDefault(w => w.typeName == typeName)?.prefab
            ?? defaultWorld.prefab;
    }
}
```

### Quantum Grid Shader System
**Status:** ✅ Implemented (September 2025)

#### Shader Architecture
**WorldSphereEnergy.shader** - Single-pass URP shader with three visual layers:

1. **Base Pulsing Effect** - Animated energy visualization
2. **Spherical Coordinate Grid** - Longitude/latitude lines
3. **Quantum State Markers** - 6 markers at key quantum positions

#### Shader Structure
```hlsl
Shader "SYSTEM/WorldSphereEnergy"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.3, 0.15, 0.5, 1)
        _EmissionColor ("Emission Color", Color) = (0.6, 0.3, 1.0, 1)
        _PulseSpeed ("Pulse Speed", Float) = 1.0
        _PulseIntensity ("Pulse Intensity", Range(0,1)) = 0.3

        _GridColor ("Grid Color", Color) = (0.8, 0.8, 1.0, 0.3)
        _LongitudeLines ("Longitude Lines", Float) = 24
        _LatitudeLines ("Latitude Lines", Float) = 12
        _GridLineWidth ("Grid Line Width", Range(0.001, 0.1)) = 0.01

        _StateMarkerColor ("State Marker Color", Color) = (1, 1, 0, 0.8)
        _StateMarkerSize ("State Marker Size", Range(0.01, 0.5)) = 0.1
    }

    SubShader
    {
        Tags {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 localPos : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Use URP vertex transformation
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.localPos = input.positionOS.xyz;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Base pulsing effect
                float pulse = (sin(_Time.y * _PulseSpeed) + 1) * 0.5;
                float3 baseColor = lerp(_BaseColor.rgb, _EmissionColor.rgb,
                                       pulse * _PulseIntensity);

                // 2. Spherical coordinate grid
                float3 normalized = normalize(input.localPos);
                float phi = atan2(normalized.z, normalized.x);      // Longitude
                float theta = acos(normalized.y);                   // Latitude

                float lineThreshold = 1.0 - _GridLineWidth;
                float longLine = step(lineThreshold, abs(sin(phi * _LongitudeLines * 0.5)));
                float latLine = step(lineThreshold, abs(sin(theta * _LatitudeLines)));
                float totalGrid = max(longLine, latLine);

                // 3. Quantum state markers (6 positions)
                float marker = 0;
                // |0⟩ - North pole
                marker = max(marker, 1.0 - smoothstep(_StateMarkerSize * 0.5,
                            _StateMarkerSize, distance(normalized, float3(0, 1, 0))));
                // |1⟩ - South pole
                marker = max(marker, 1.0 - smoothstep(_StateMarkerSize * 0.5,
                            _StateMarkerSize, distance(normalized, float3(0, -1, 0))));
                // |+⟩, |-⟩, |+i⟩, |-i⟩ - Equator positions
                marker = max(marker, 1.0 - smoothstep(_StateMarkerSize * 0.5,
                            _StateMarkerSize, distance(normalized, float3(1, 0, 0))));
                marker = max(marker, 1.0 - smoothstep(_StateMarkerSize * 0.5,
                            _StateMarkerSize, distance(normalized, float3(-1, 0, 0))));
                marker = max(marker, 1.0 - smoothstep(_StateMarkerSize * 0.5,
                            _StateMarkerSize, distance(normalized, float3(0, 0, 1))));
                marker = max(marker, 1.0 - smoothstep(_StateMarkerSize * 0.5,
                            _StateMarkerSize, distance(normalized, float3(0, 0, -1))));

                // Blend all layers
                float3 color = lerp(baseColor, _GridColor.rgb, totalGrid * _GridColor.a);
                color = lerp(color, _StateMarkerColor.rgb, marker * _StateMarkerColor.a);

                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
```

#### Key Technical Decisions

**Single-Pass Design:**
- URP limitation: Only one pass with `LightMode="UniversalForward"` executes per object
- Solution: Combined base color, grid, and markers in single fragment shader
- Benefits: Better performance, simplified pipeline

**Bloch Sphere Coordinates (Unity Standard: +Y is North Pole):**
- `theta` (polar angle): `acos(y)` → range [0, π] - angle from +Y axis (north pole)
- `phi` (azimuthal angle): `atan2(z, x)` → range [-π, π] - angle in XZ plane (horizontal)
- **Reference**: See `.claude/bloch-sphere-coordinates-reference.md` for complete coordinate system documentation
- Grid lines via `sin()` modulation at configurable intervals
- Thin lines via `step()` threshold (default 0.01 width)

**WebGL Compatibility:**
- Uses `GetVertexPositionInputs()` from URP ShaderLibrary
- Avoids manual matrix multiplication
- No custom matrix operations
- Tested on WebGL with proper rendering

**Quantum State Markers (Bloch Sphere Convention):**
| State | Position | Theta | Phi | Physical Meaning |
|-------|----------|-------|-----|------------------|
| \|0⟩ | (0, 1, 0) | 0 | - | North pole (+Y) - Computational zero |
| \|1⟩ | (0, -1, 0) | π | - | South pole (-Y) - Computational one |
| \|+⟩ | (1, 0, 0) | π/2 | 0 | Equator +X - Plus superposition |
| \|-⟩ | (-1, 0, 0) | π/2 | π | Equator -X - Minus superposition |
| \|+i⟩ | (0, 0, 1) | π/2 | π/2 | Equator +Z - Plus-i superposition (forward) |
| \|-i⟩ | (0, 0, -1) | π/2 | 3π/2 | Equator -Z - Minus-i superposition (backward) |

**Note:** This follows the standard Bloch sphere representation where +Y is the north pole representing |0⟩ state. See `.claude/bloch-sphere-coordinates-reference.md` for complete details.

### WebGL-Specific Optimizations
**Status:** ✅ Implemented (August-September 2025)

#### Scale Correction System
WebGL builds had persistent scale issues requiring multi-layer protection:

**Layer 1: Awake() Enforcement**
```csharp
void Awake()
{
    #if UNITY_WEBGL && !UNITY_EDITOR
    // Force scale immediately on WebGL
    float targetScale = worldRadius;
    transform.localScale = Vector3.one * targetScale;
    UnityEngine.Debug.Log($"[WebGL] Forced scale in Awake: {targetScale}");
    #endif
}
```

**Layer 2: Post-Instantiation Verification**
```csharp
void CreateWorldSphere()
{
    worldSphereInstance = Instantiate(worldSpherePrefab, transform);

    #if UNITY_WEBGL && !UNITY_EDITOR
    StartCoroutine(ForceScaleAfterFrame());
    #endif
}

IEnumerator ForceScaleAfterFrame()
{
    yield return new WaitForEndOfFrame();
    float targetScale = worldRadius;
    transform.localScale = Vector3.one * targetScale;
}
```

**Layer 3: Root Object Creation**
```csharp
// WorldManager.cs - Create world as root object (no parent)
GameObject worldObj = new GameObject($"CenterWorld_{worldId}");
worldObj.transform.position = Vector3.zero;
worldObj.transform.rotation = Quaternion.identity;
worldObj.transform.localScale = Vector3.one;  // No parent scale inheritance
```

#### Debug Overlay System
**WebGLDebugOverlay.cs** - Runtime diagnostic display:

```csharp
public class WebGLDebugOverlay : MonoBehaviour
{
    private bool showDebugUI = true;
    private bool minimalMode = false;

    void Start()
    {
        #if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        // Hide in production builds
        showDebugUI = false;
        gameObject.SetActive(false);
        return;
        #endif

        InitializeStyles();
        StartCoroutine(UpdateDebugInfo());
    }

    void OnGUI()
    {
        if (!showDebugUI) return;

        if (minimalMode)
        {
            // Minimal mode - essential info only
            DrawMinimalOverlay();
        }
        else
        {
            // Full diagnostic mode
            DrawFullOverlay();
        }
    }

    void DrawMinimalOverlay()
    {
        string info = $"Connection: {connectionStatus} | " +
                     $"Environment: {environment} | " +
                     $"Player: {playerName} | " +
                     $"State: {gameState}";
        GUI.Label(new Rect(10, 10, 800, 30), info);
    }

    void Update()
    {
        // F3 to toggle visibility
        if (Input.GetKeyDown(KeyCode.F3))
            showDebugUI = !showDebugUI;

        // F4 to switch modes
        if (Input.GetKeyDown(KeyCode.F4))
            minimalMode = !minimalMode;
    }
}
```

**Features:**
- Toggle with F3 (visibility), F4 (mode switch)
- Minimal mode: Connection | Environment | Player | State
- Full mode: Detailed transform diagnostics, hierarchy info
- Automatically hidden in production builds

### Orb Visualization System
**Status:** ✅ Implemented (September 2025)

#### Architecture Overview
The orb visualization system uses an event-driven architecture to display WavePacketOrbs in the 3D world, reacting to SpacetimeDB table changes in real-time.

**Core Components:**
1. **OrbVisualizationManager** - Singleton manager that subscribes to orb table events
2. **WavePacketOrbVisual** - Component script attached to orb prefabs for visual effects
3. **Orb Prefabs** - Prefab hierarchy with visual elements (sphere, particles, lights, UI)

#### OrbVisualizationManager.cs
**Purpose:** Subscribe to WavePacketOrb table events and manage orb GameObjects

```csharp
public class OrbVisualizationManager : MonoBehaviour
{
    [Header("Visualization Settings")]
    [SerializeField] private GameObject orbPrefab;
    [SerializeField] private float orbVisualScale = 2f;

    [Header("Frequency Colors")]
    [SerializeField] private Color redColor = new Color(1f, 0f, 0f, 0.7f);      // 0.0
    [SerializeField] private Color yellowColor = new Color(1f, 1f, 0f, 0.7f);   // 1/6
    [SerializeField] private Color greenColor = new Color(0f, 1f, 0f, 0.7f);    // 1/3
    [SerializeField] private Color cyanColor = new Color(0f, 1f, 1f, 0.7f);     // 1/2
    [SerializeField] private Color blueColor = new Color(0f, 0f, 1f, 0.7f);     // 2/3
    [SerializeField] private Color magentaColor = new Color(1f, 0f, 1f, 0.7f);  // 5/6

    private Dictionary<ulong, GameObject> activeOrbs = new Dictionary<ulong, GameObject>();
    private DbConnection conn;

    void OnEnable()
    {
        conn = GameManager.Conn;

        // Subscribe to orb table events
        conn.Db.WavePacketOrb.OnInsert += OnOrbInserted;
        conn.Db.WavePacketOrb.OnUpdate += OnOrbUpdated;
        conn.Db.WavePacketOrb.OnDelete += OnOrbDeleted;
    }

    private void OnOrbInserted(EventContext ctx, WavePacketOrb orb)
    {
        CreateOrbVisualization(orb);
    }

    private void OnOrbUpdated(EventContext ctx, WavePacketOrb oldOrb, WavePacketOrb newOrb)
    {
        UpdateOrbVisualization(newOrb);
    }

    private void OnOrbDeleted(EventContext ctx, WavePacketOrb orb)
    {
        RemoveOrbVisualization(orb.OrbId);
    }
}
```

**Key Features:**
- **Event-Driven**: Responds to SpacetimeDB table changes automatically
- **Prefab Support**: Uses prefabs if assigned, falls back to primitive spheres
- **Frequency-Based Colors**: Maps frequency values to RGB spectrum colors
- **Concurrent Mining Visualization**: Shows active miner count via light intensity

#### WavePacketOrbVisual.cs
**Purpose:** Component script for orb prefabs with visual effects and animations

```csharp
public class WavePacketOrbVisual : MonoBehaviour
{
    [Header("Visual Components")]
    [SerializeField] private Renderer orbRenderer;
    [SerializeField] private ParticleSystem particleEffect;
    [SerializeField] private Light orbLight;

    [Header("UI Components")]
    [SerializeField] private TextMeshPro packetCountText;
    [SerializeField] private TextMeshPro minerCountText;
    [SerializeField] private GameObject infoPanel;

    [Header("Animation")]
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseAmount = 0.1f;
    [SerializeField] private float rotationSpeed = 20f;

    public void Initialize(ulong orbId, Color color, uint packets, uint miners)
    {
        this.orbId = orbId;
        this.baseColor = color;
        this.totalPackets = packets;
        this.activeMinerCount = miners;
        gameObject.name = $"Orb_{orbId}";
        UpdateVisuals();
    }

    void Update()
    {
        // Gentle pulsing animation
        float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        transform.localScale = baseScale * (1f + pulse);

        // Slow rotation
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

        // Make info panel face camera
        if (infoPanel != null && Camera.main != null)
        {
            infoPanel.transform.LookAt(Camera.main.transform);
            infoPanel.transform.Rotate(0, 180, 0);
        }
    }

    private void UpdateVisuals()
    {
        // Update material color with emission
        orbMaterial.color = baseColor;
        orbMaterial.EnableKeyword("_EMISSION");
        orbMaterial.SetColor("_EmissionColor", baseColor * 0.5f);

        // Update light intensity based on miners
        orbLight.intensity = Mathf.Lerp(1f, 3f, activeMinerCount / 5f);

        // Update packet count text
        packetCountText.text = $"Packets: {totalPackets}";

        // Update miner count text (show only when > 0)
        if (activeMinerCount > 0)
        {
            minerCountText.text = $"Miners: {activeMinerCount}";
            minerCountText.gameObject.SetActive(true);
        }
        else
        {
            minerCountText.gameObject.SetActive(false);
        }

        // Start/stop particles based on mining activity
        if (activeMinerCount > 0 && !particleEffect.isPlaying)
            particleEffect.Play();
        else if (activeMinerCount == 0 && particleEffect.isPlaying)
            particleEffect.Stop();
    }
}
```

**Visual Effects:**
- **Pulsing Animation**: Sine wave-based scale animation for energy feel
- **Rotation**: Constant slow rotation for visual interest
- **Emission Glow**: Material emission color matches orb frequency
- **Dynamic Light**: Light intensity increases with active miner count
- **Particle Effects**: Play when miners are actively extracting
- **Billboard UI**: Info panel always faces camera

#### Orb Prefab Structure
**Recommended Hierarchy:**

```
WavePacketOrb (Root)
├── Sphere (MeshRenderer + MeshFilter)
│   └── Material: OrbMaterial_Emissive
├── Light (Point Light)
│   ├── Range: 10
│   └── Intensity: 2 (adjusted dynamically)
├── ParticleSystem
│   ├── Shape: Sphere
│   ├── Emission Rate: 10-20
│   └── Start Speed: 0.2-0.5
└── InfoPanel (Quad)
    ├── PacketCountText (TextMeshPro)
    │   └── Text: "Packets: 100"
    └── MinerCountText (TextMeshPro)
        └── Text: "Miners: 0"
```

**Setup Guide:** See `Assets/Prefabs/PREFAB_SETUP_GUIDE.md`

#### Frequency-to-Color Mapping

| Frequency Value | Color | RGB | Meaning |
|----------------|-------|-----|---------|
| 0.0 | Red | (1, 0, 0) | Base frequency |
| 0.166 (1/6) | Yellow | (1, 1, 0) | Red-Green mix |
| 0.333 (1/3) | Green | (0, 1, 0) | Green frequency |
| 0.5 (1/2) | Cyan | (0, 1, 1) | Green-Blue mix |
| 0.666 (2/3) | Blue | (0, 0, 1) | Blue frequency |
| 0.833 (5/6) | Magenta | (1, 0, 1) | Blue-Red mix |

**Implementation:**
```csharp
private Color GetColorFromFrequency(float frequency)
{
    if (frequency < 0.08f) return redColor;
    else if (frequency < 0.25f) return yellowColor;
    else if (frequency < 0.42f) return greenColor;
    else if (frequency < 0.58f) return cyanColor;
    else if (frequency < 0.75f) return blueColor;
    else return magentaColor;
}
```

#### Integration with Mining System
**Concurrent Mining Visualization:**

When multiple players mine the same orb:
1. **active_miner_count** increments in WavePacketOrb table
2. **OnOrbUpdated** event fires in OrbVisualizationManager
3. **UpdateVisuals()** called on WavePacketOrbVisual component:
   - Light intensity increases (1.0 → 3.0)
   - Particle system starts playing
   - Miner count text becomes visible
   - Brighter emission glow

**Test Utilities:**
```bash
# Spawn a blue orb with 100 packets at position (0, -300, 0)
spacetime call system spawn_test_orb -- 0 -300 0 4 100 --server local

# List all active mining sessions
spacetime call system list_active_mining --server local

# Clear all orbs from database
spacetime call system clear_all_orbs --server local
```

#### Known Limitations
1. **Initial Load**: Orbs created before OrbVisualizationManager enables won't appear until next event
   - **Solution**: Implement initial table query on subscription (pending architecture discussion)
2. **No Object Pooling**: Currently creates/destroys orbs on each spawn/despawn
   - **Future**: Implement object pooling for performance
3. **No LOD System**: All orbs render at full detail regardless of distance
   - **Future**: Distance-based culling and LOD levels

---

## 3.7 Build & Deployment Pipeline

### Automated Build System
**Status:** ✅ Implemented (September 2025)

#### Unity Build Menu
**BuildScript.cs** provides automated builds via Unity menu:

```
Build/
├── Build Local WebGL      → Local development (127.0.0.1:3000)
├── Build Test WebGL       → Test environment (SpacetimeDB cloud)
├── Build Production WebGL → Production environment
├── Build Local Windows    → Windows standalone (local)
├── Build Test Windows     → Windows standalone (test)
└── Build Production Windows → Windows standalone (production)
```

#### BuildScript Implementation
```csharp
public class BuildScript
{
    private enum Environment { Local, Test, Production }

    [MenuItem("Build/Build Test WebGL")]
    public static void BuildTestWebGL()
    {
        BuildForEnvironment(Environment.Test, BuildTarget.WebGL);
    }

    private static void BuildForEnvironment(Environment env, BuildTarget target)
    {
        // Load environment config
        BuildSettings settings = LoadBuildSettings(env);

        // Set build options
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = GetScenePaths(),
            locationPathName = GetBuildPath(env, target),
            target = target,
            options = GetBuildOptions(env)
        };

        // WebGL-specific settings
        if (target == BuildTarget.WebGL)
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
        }

        // Execute build
        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == BuildResult.Succeeded)
        {
            UnityEngine.Debug.Log($"Build succeeded: {options.locationPathName}");

            // Generate build-config.json for WebGL
            if (target == BuildTarget.WebGL)
                GenerateBuildConfig(env, options.locationPathName);
        }
    }

    private static void GenerateBuildConfig(Environment env, string buildPath)
    {
        BuildSettings settings = LoadBuildSettings(env);

        var config = new
        {
            environment = env.ToString(),
            serverUrl = settings.serverUrl,
            moduleName = settings.moduleName,
            buildDate = System.DateTime.UtcNow.ToString("o")
        };

        string json = JsonUtility.ToJson(config, true);
        string configPath = Path.Combine(buildPath, "StreamingAssets", "build-config.json");

        Directory.CreateDirectory(Path.GetDirectoryName(configPath));
        File.WriteAllText(configPath, json);

        UnityEngine.Debug.Log($"Generated build-config.json: {configPath}");
    }
}
```

### Environment Configuration System
**BuildSettings.cs** - ScriptableObject for environment-specific settings:

```csharp
[CreateAssetMenu(fileName = "BuildSettings", menuName = "SYSTEM/Build Settings")]
public class BuildSettings : ScriptableObject
{
    public enum EnvironmentType { Local, Test, Production }

    [System.Serializable]
    public class EnvironmentConfig
    {
        public EnvironmentType environment;
        public string serverUrl;
        public string moduleName;
        public bool enableDebugLogging;
        public bool enableWebGLDebugOverlay;
    }

    public EnvironmentConfig[] environments;

    public EnvironmentConfig GetConfig(EnvironmentType env)
    {
        return System.Array.Find(environments, e => e.environment == env);
    }
}
```

**Example Configuration:**
- **Local**: `localhost:3000` / `system` / Debug enabled
- **Test**: `maincloud.spacetimedb.com` / `system-test` / Debug enabled
- **Production**: `maincloud.spacetimedb.com` / `system` / Debug disabled

### Runtime Platform Detection
**IMPORTANT:** Connection logic uses runtime detection, NOT compiler directives:

```csharp
// GameManager.cs
void Start()
{
    string serverUrl, moduleName;

    if (Application.platform == RuntimePlatform.WebGLPlayer)
    {
        // WebGL builds
        serverUrl = "maincloud.spacetimedb.com";
        moduleName = "system-test";
    }
    else if (Application.isEditor)
    {
        // Unity Editor
        serverUrl = "localhost:3000";
        moduleName = "system";
    }
    else
    {
        // Standalone builds
        serverUrl = "maincloud.spacetimedb.com";
        moduleName = "system";
    }

    ConnectToSpacetimeDB(serverUrl, moduleName);
}
```

### BuildConfiguration Async Loading
**Critical WebGL Fix:** BuildConfiguration loads asynchronously in WebGL:

```csharp
public class BuildConfiguration : MonoBehaviour
{
    private static BuildConfigData _config = new BuildConfigData();  // Initialize to prevent null

    void Awake()
    {
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            StartCoroutine(LoadConfigAsync());
        }
        else
        {
            LoadConfigFromStreamingAssets();
        }
    }

    IEnumerator LoadConfigAsync()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "build-config.json");
        UnityWebRequest request = UnityWebRequest.Get(path);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            _config = JsonUtility.FromJson<BuildConfigData>(request.downloadHandler.text);
            UnityEngine.Debug.Log($"[BuildConfig] Loaded: {_config.environment}");
        }
    }
}
```

**GameManager must wait for config:**
```csharp
// GameManager.cs
IEnumerator Start()
{
    // Wait for BuildConfiguration to load (WebGL async)
    float waitTime = 0f;
    while (BuildConfiguration.Config == null && waitTime < 5f)
    {
        yield return new WaitForSeconds(0.1f);
        waitTime += 0.1f;
    }

    // Now safe to connect
    ConnectToSpacetimeDB();
}
```

### Unified Deployment Scripts

#### PowerShell Script (Windows)
**Scripts/deploy-spacetimedb.ps1:**

```powershell
param(
    [ValidateSet('local','test','production')]
    [string]$Environment = 'test',
    [switch]$DeleteData,
    [switch]$InvalidateCache,
    [switch]$PublishOnly,
    [switch]$Verify,
    [switch]$BuildConfig,
    [switch]$SkipBuild,
    [switch]$Yes
)

# Build Rust module
if (-not $SkipBuild) {
    cd SYSTEM-server
    cargo build --release
    cd ..
}

# Generate C# bindings
spacetime generate --lang cs --out-dir SYSTEM-client-3d/Assets/Scripts/autogen

# Publish to SpacetimeDB
$moduleName = switch ($Environment) {
    'local' { 'system' }
    'test' { 'system-test' }
    'production' { 'system' }
}

$serverUrl = switch ($Environment) {
    'local' { 'local' }
    default { 'maincloud.spacetimedb.com' }
}

spacetime publish --server $serverUrl $moduleName

# Generate build-config.json if requested
if ($BuildConfig) {
    $config = @{
        environment = $Environment
        serverUrl = $serverUrl
        moduleName = $moduleName
        buildDate = (Get-Date).ToUniversalTime().ToString("o")
    } | ConvertTo-Json

    $configPath = "SYSTEM-client-3d/Assets/StreamingAssets/build-config.json"
    New-Item -ItemType Directory -Force -Path (Split-Path $configPath)
    Set-Content -Path $configPath -Value $config
}

# Run verification if requested
if ($Verify) {
    spacetime sql $moduleName "SELECT COUNT(*) FROM Player"
}
```

**Usage Examples:**
```bash
# Deploy to test
./Scripts/deploy-spacetimedb.ps1 -Environment test

# Deploy to production with verification
./Scripts/deploy-spacetimedb.ps1 -Environment production -Verify

# Deploy with build config for WebGL
./Scripts/deploy-spacetimedb.ps1 -Environment test -BuildConfig

# CI/CD non-interactive deployment
./Scripts/deploy-spacetimedb.ps1 -Environment production -Yes -Verify
```

### WebGL Deployment Workflow

**Complete WebGL Deployment:**

1. **Build Unity Project:**
   ```
   Unity Menu → Build → Build Test WebGL
   ```
   - Output: `SYSTEM-client-3d/Build/Test/`
   - Includes: `build-config.json` in StreamingAssets

2. **Deploy Server Module:**
   ```bash
   ./Scripts/deploy-spacetimedb.ps1 -Environment test -BuildConfig -InvalidateCache
   ```
   - Builds Rust module
   - Generates C# bindings
   - Publishes to SpacetimeDB cloud
   - Creates build-config.json
   - Invalidates CloudFront cache (if configured)

3. **Upload to Hosting (S3 Example):**
   ```bash
   aws s3 sync ./SYSTEM-client-3d/Build/Test s3://your-bucket/test/ --delete
   aws cloudfront create-invalidation --distribution-id YOUR_ID --paths "/*"
   ```

4. **Verify Deployment:**
   ```bash
   ./Scripts/deploy-spacetimedb.ps1 -Environment test -Verify
   ```

### Build Output Structure

```
SYSTEM-client-3d/Build/
├── Local/
│   ├── index.html
│   ├── Build/
│   │   ├── Local.data.br
│   │   ├── Local.framework.js.br
│   │   └── Local.wasm.br
│   └── StreamingAssets/
│       └── build-config.json      # Environment: Local
├── Test/
│   ├── index.html
│   ├── Build/
│   │   ├── Test.data.br
│   │   ├── Test.framework.js.br
│   │   └── Test.wasm.br
│   └── StreamingAssets/
│       └── build-config.json      # Environment: Test
└── Production/
    ├── index.html
    ├── Build/
    │   ├── Production.data.br
    │   ├── Production.framework.js.br
    │   └── Production.wasm.br
    └── StreamingAssets/
        └── build-config.json      # Environment: Production
```

### Deployment Best Practices

1. **Always verify after deployment:**
   ```bash
   ./Scripts/deploy-spacetimedb.ps1 -Environment test -Verify
   ```

2. **Use build-config.json for WebGL:**
   - Enables runtime environment detection
   - Avoids hardcoded connection strings
   - Supports multiple environments from same build

3. **Test locally before cloud deployment:**
   ```bash
   # Local testing
   spacetime start
   ./Scripts/deploy-spacetimedb.ps1 -Environment local
   # Test in Unity Editor
   ```

4. **Never delete production data without backup:**
   ```bash
   # DANGEROUS - only use with -Yes flag after confirmation
   ./Scripts/deploy-spacetimedb.ps1 -Environment production -DeleteData -Yes
   ```

5. **Monitor deployments:**
   - Check SpacetimeDB logs: `spacetime logs system-test`
   - Monitor Unity console for connection issues
   - Verify WebGL builds load build-config.json correctly

---

## 3.8 Testing & Quality Assurance

### Editor Testing Tools
**Status:** ✅ Implemented

**Menu Items:**
- `SYSTEM → Create High-Res Sphere Meshes` - Generate LOD meshes
- `SYSTEM → Verify High-Res Sphere Meshes` - Check normalization
- `SYSTEM → Regenerate All High-Res Sphere Meshes` - Full regeneration
- `SYSTEM → World Setup → Quick Create Default World Prefab` - Prefab setup
- `Tools → Test Procedural Sphere` - Legacy system comparison

### Runtime Debugging

**WebGL Debug Overlay (F3/F4):**
- F3: Toggle visibility
- F4: Switch minimal/full mode
- Minimal: Connection | Environment | Player | State
- Full: Transform diagnostics, hierarchy, scale checks

**Console Logging Patterns:**
```csharp
// Reduced logging - only 1/100 updates
if (showDebugInfo && updateCount % 100 == 0)
{
    UnityEngine.Debug.Log($"[PlayerController] Position update: {currentPosition}");
}
```

### Known WebGL Issues & Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| Tiny world sphere | Scale inheritance | Force scale in Awake(), create as root object |
| NullReferenceException | Async config loading | Initialize `_config` with default value |
| Grid not visible | Wrong shader pass | Use single-pass design with LightMode="UniversalForward" |
| Position not saving | Reducer not called | Implement UpdatePlayerPosition calls in PlayerController |
| Console spam | Excessive logging | Rate-limit debug logs to 1/100 updates |

---

## 3.9 Circuit System Architecture
**Status:** ✅ Implemented (September 2025)

### Overview
The Energy Spire Circuit Visualization System provides ground-level visual representation of energy circuits connecting worlds in a three-tier hierarchical lattice structure. The system uses a unified world radius R=300 and implements color-coded directional tunnels based on quantum computing axes.

#### Three-Tier Hierarchy

**Primary Tier (RGB Axes):**
- **Red Tunnels** (X-axis): Superposition states - Max 2 per world
- **Green Tunnels** (Y-axis): Phase states - Max 2 per world
- **Blue Tunnels** (Z-axis): Computation states - Max 2 per world

**Secondary Tier (Planar Intersections):**
- **Yellow Tunnels** (RG/XY plane): Red-Green intersection - Max 4 per world
- **Cyan Tunnels** (GB/YZ plane): Green-Blue intersection - Max 4 per world
- **Magenta Tunnels** (BR/XZ plane): Blue-Red intersection - Max 4 per world

**Tertiary Tier (Volumetric):**
- **Grey Tunnels** (Center cube): All three axes intersect - Max 8 per world

**Maximum Configuration:** A fully connected world can have up to 26 tunnels total:
- 6 Primary (2R + 2G + 2B)
- 12 Secondary (4Y + 4C + 4M)
- 8 Tertiary (8 Grey)

### Core Constants (CircuitConstants.cs)
```csharp
public static class CircuitConstants
{
    // World dimensions
    public const float WORLD_RADIUS = 300f;          // Unified world radius

    // FCC lattice spacing (10R between adjacent worlds)
    public const float LATTICE_SPACING = WORLD_RADIUS * 10f; // 3000 units

    // Circuit dimensions
    public const float CIRCUIT_RADIUS = WORLD_RADIUS * 0.98f;  // 294 units
    public const float CIRCUIT_HEIGHT = 5f;          // Ground-level height
    public const float SPIRE_HEIGHT = 30f;           // Energy spire height
    public const float SPIRE_RADIUS = 2f;            // Spire base radius

    // Visual parameters
    public const int CIRCUIT_SEGMENTS = 64;          // Smoothness of ring
    public const float GLOW_INTENSITY = 2f;          // Emission strength
    public const float PULSE_SPEED = 1f;             // Animation speed

    // Energy flow
    public const float FLOW_SPEED = 50f;             // Units per second
    public const float PACKET_SIZE = 1f;             // Visual packet size
}
```

### Component Architecture

#### CircuitBase.cs
**Purpose:** Ground-level circuit ring visualization
```csharp
public class CircuitBase : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Material circuitMaterial;

    void Start()
    {
        CreateCircuitRing();
        ApplyQuantumShader();
    }

    void CreateCircuitRing()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = CircuitConstants.CIRCUIT_SEGMENTS + 1;

        Vector3[] positions = new Vector3[CircuitConstants.CIRCUIT_SEGMENTS + 1];
        for (int i = 0; i <= CircuitConstants.CIRCUIT_SEGMENTS; i++)
        {
            float angle = (i / (float)CircuitConstants.CIRCUIT_SEGMENTS) * Mathf.PI * 2;
            positions[i] = new Vector3(
                Mathf.Cos(angle) * CircuitConstants.CIRCUIT_RADIUS,
                CircuitConstants.CIRCUIT_HEIGHT,
                Mathf.Sin(angle) * CircuitConstants.CIRCUIT_RADIUS
            );
        }

        lineRenderer.SetPositions(positions);
        lineRenderer.startWidth = 0.5f;
        lineRenderer.endWidth = 0.5f;
    }
}
```

#### DirectionalTunnel.cs
**Purpose:** Energy spire connection visualization
```csharp
public class DirectionalTunnel : MonoBehaviour
{
    public Vector3 targetWorldPosition;
    public float energyFlow = 0f;

    private LineRenderer tunnelRenderer;
    private ParticleSystem energyParticles;

    void CreateTunnel()
    {
        // Create spire (vertical component)
        GameObject spire = CreateSpire();

        // Create directional tunnel (horizontal component)
        Vector3 direction = (targetWorldPosition - transform.position).normalized;
        Vector3 tunnelEnd = transform.position + direction * CircuitConstants.LATTICE_SPACING;

        tunnelRenderer = CreateTunnelRenderer(transform.position, tunnelEnd);

        // Add energy flow particles
        energyParticles = CreateEnergyFlow(direction);
    }

    GameObject CreateSpire()
    {
        GameObject spire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        spire.transform.parent = transform;
        spire.transform.localPosition = Vector3.up * (CircuitConstants.SPIRE_HEIGHT / 2);
        spire.transform.localScale = new Vector3(
            CircuitConstants.SPIRE_RADIUS * 2,
            CircuitConstants.SPIRE_HEIGHT / 2,
            CircuitConstants.SPIRE_RADIUS * 2
        );
        return spire;
    }
}
```

#### CircuitVisualization.cs
**Purpose:** Main circuit system manager
```csharp
public class CircuitVisualization : MonoBehaviour
{
    [Header("Circuit Configuration")]
    public bool showGroundCircuit = true;
    public bool showEnergySpires = true;
    public bool showDirectionalTunnels = true;

    [Header("FCC Lattice")]
    public Vector3[] neighborOffsets = new Vector3[12]; // 12 FCC neighbors

    private CircuitBase groundCircuit;
    private List<DirectionalTunnel> tunnels = new List<DirectionalTunnel>();

    void Start()
    {
        InitializeFCCOffsets();
        CreateCircuitSystem();
    }

    void InitializeHierarchicalNeighbors()
    {
        float d = CircuitConstants.LATTICE_SPACING;

        // Primary Tier: RGB axes (6 total)
        redNeighbors[0] = new Vector3(d, 0, 0);     // +X
        redNeighbors[1] = new Vector3(-d, 0, 0);    // -X
        greenNeighbors[0] = new Vector3(0, d, 0);   // +Y
        greenNeighbors[1] = new Vector3(0, -d, 0);  // -Y
        blueNeighbors[0] = new Vector3(0, 0, d);    // +Z
        blueNeighbors[1] = new Vector3(0, 0, -d);   // -Z

        // Secondary Tier: Planar positions (12 total)
        // Yellow (XY plane)
        yellowNeighbors[0] = new Vector3(d, d, 0);
        yellowNeighbors[1] = new Vector3(d, -d, 0);
        yellowNeighbors[2] = new Vector3(-d, d, 0);
        yellowNeighbors[3] = new Vector3(-d, -d, 0);

        // Cyan (YZ plane)
        cyanNeighbors[0] = new Vector3(0, d, d);
        cyanNeighbors[1] = new Vector3(0, d, -d);
        cyanNeighbors[2] = new Vector3(0, -d, d);
        cyanNeighbors[3] = new Vector3(0, -d, -d);

        // Magenta (XZ plane)
        magentaNeighbors[0] = new Vector3(d, 0, d);
        magentaNeighbors[1] = new Vector3(d, 0, -d);
        magentaNeighbors[2] = new Vector3(-d, 0, d);
        magentaNeighbors[3] = new Vector3(-d, 0, -d);

        // Tertiary Tier: Center cube corners (8 total)
        greyNeighbors[0] = new Vector3(d, d, d);
        greyNeighbors[1] = new Vector3(d, d, -d);
        greyNeighbors[2] = new Vector3(d, -d, d);
        greyNeighbors[3] = new Vector3(d, -d, -d);
        greyNeighbors[4] = new Vector3(-d, d, d);
        greyNeighbors[5] = new Vector3(-d, d, -d);
        greyNeighbors[6] = new Vector3(-d, -d, d);
        greyNeighbors[7] = new Vector3(-d, -d, -d);
    }
}
```

#### CircuitHierarchicalLattice.cs
**Purpose:** Three-tier hierarchical lattice structure management
```csharp
public class CircuitHierarchicalLattice : MonoBehaviour
{
    public enum TunnelType
    {
        Red,      // Primary: Superposition (X-axis) - 2 max
        Green,    // Primary: Phase (Y-axis) - 2 max
        Blue,     // Primary: Computation (Z-axis) - 2 max
        Yellow,   // Secondary: RG plane - 4 max
        Cyan,     // Secondary: GB plane - 4 max
        Magenta,  // Secondary: BR plane - 4 max
        Grey      // Tertiary: Center cube - 8 max
    }

    [System.Serializable]
    public struct LatticeNode
    {
        public Vector3 worldCoordinate;
        public Vector3 position;
        public TunnelType tunnelType;
        public bool isActive;
        public float energyLevel;
    }

    [System.Serializable]
    public struct WorldTunnelConfiguration
    {
        public int redTunnels;     // Max 2
        public int greenTunnels;   // Max 2
        public int blueTunnels;    // Max 2
        public int yellowTunnels;  // Max 4
        public int cyanTunnels;    // Max 4
        public int magentaTunnels; // Max 4
        public int greyTunnels;    // Max 8

        public int TotalTunnels => redTunnels + greenTunnels + blueTunnels +
                                   yellowTunnels + cyanTunnels + magentaTunnels + greyTunnels;

        public bool IsValid => TotalTunnels <= 26;
    }

    private Dictionary<Vector3, LatticeNode> latticeNodes;

    public void GenerateHierarchicalLattice(int shellLevel)
    {
        latticeNodes = new Dictionary<Vector3, LatticeNode>();

        for (int x = -shellLevel; x <= shellLevel; x++)
        {
            for (int y = -shellLevel; y <= shellLevel; y++)
            {
                for (int z = -shellLevel; z <= shellLevel; z++)
                {
                    TunnelType? type = ClassifyNodeType(x, y, z);
                    if (type.HasValue)
                    {
                        Vector3 coord = new Vector3(x, y, z);
                        Vector3 pos = coord * CircuitConstants.LATTICE_SPACING;

                        latticeNodes[coord] = new LatticeNode
                        {
                            worldCoordinate = coord,
                            position = pos,
                            tunnelType = type.Value,
                            isActive = false,
                            energyLevel = 0f
                        };
                    }
                }
            }
        }
    }

    TunnelType? ClassifyNodeType(int x, int y, int z)
    {
        // Primary Tier: Axis-aligned worlds
        if (y == 0 && z == 0 && x != 0) return TunnelType.Red;    // X-axis: Superposition
        if (x == 0 && z == 0 && y != 0) return TunnelType.Green;  // Y-axis: Phase
        if (x == 0 && y == 0 && z != 0) return TunnelType.Blue;   // Z-axis: Computation

        // Secondary Tier: Planar worlds
        if (z == 0 && x != 0 && y != 0) return TunnelType.Yellow;  // RG plane (XY)
        if (x == 0 && y != 0 && z != 0) return TunnelType.Cyan;    // GB plane (YZ)
        if (y == 0 && x != 0 && z != 0) return TunnelType.Magenta; // BR plane (XZ)

        // Tertiary Tier: Center cube worlds (all non-zero)
        if (x != 0 && y != 0 && z != 0) return TunnelType.Grey;

        // Origin or invalid position
        return null;
    }

    public Color GetTunnelColor(TunnelType type)
    {
        switch (type)
        {
            case TunnelType.Red: return Color.red;
            case TunnelType.Green: return Color.green;
            case TunnelType.Blue: return Color.blue;
            case TunnelType.Yellow: return Color.yellow;
            case TunnelType.Cyan: return Color.cyan;
            case TunnelType.Magenta: return Color.magenta;
            case TunnelType.Grey: return Color.grey;
            default: return Color.white;
        }
    }
}
```

#### CircuitNetworkManager.cs
**Purpose:** Network-wide circuit state management
```csharp
public class CircuitNetworkManager : MonoBehaviour
{
    private static CircuitNetworkManager instance;
    public static CircuitNetworkManager Instance => instance;

    [Header("Network State")]
    public Dictionary<ulong, CircuitVisualization> worldCircuits;
    public float totalNetworkEnergy;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void RegisterWorldCircuit(ulong worldId, CircuitVisualization circuit)
    {
        if (worldCircuits == null)
            worldCircuits = new Dictionary<ulong, CircuitVisualization>();

        worldCircuits[worldId] = circuit;
        UnityEngine.Debug.Log($"[CircuitNetwork] Registered circuit for world {worldId}");
    }

    public void UpdateEnergyFlow(ulong fromWorld, ulong toWorld, float energyAmount)
    {
        if (worldCircuits.TryGetValue(fromWorld, out var fromCircuit))
        {
            // Update visual representation of energy flow
            fromCircuit.SetOutgoingEnergy(toWorld, energyAmount);
        }

        if (worldCircuits.TryGetValue(toWorld, out var toCircuit))
        {
            toCircuit.SetIncomingEnergy(fromWorld, energyAmount);
        }
    }
}
```

### Visual Effects

#### Quantum Glow Shader
Applied to circuit rings and energy spires for pulsing quantum effect:
```hlsl
_EmissionColor ("Emission", Color) = (0.3, 0.8, 1.0, 1)
_PulseSpeed ("Pulse Speed", Float) = 1.0
_PulseIntensity ("Pulse Intensity", Range(0, 1)) = 0.5

// In fragment shader
float pulse = (sin(_Time.y * _PulseSpeed) + 1) * 0.5;
float3 emission = _EmissionColor.rgb * (1 + pulse * _PulseIntensity);
```

#### Energy Flow Particles
Particle system configuration for directional energy flow:
```csharp
ParticleSystem.MainModule main = particles.main;
main.startSpeed = CircuitConstants.FLOW_SPEED;
main.startSize = CircuitConstants.PACKET_SIZE;
main.startColor = energyColor;

ParticleSystem.ShapeModule shape = particles.shape;
shape.shapeType = ParticleSystemShapeType.Cone;
shape.angle = 0f; // Straight line
shape.radius = 0.1f;
```

### Integration Points

#### With WorldManager
```csharp
// WorldManager.cs
void SpawnWorld(ulong worldId, Vector3 position)
{
    GameObject world = CreateWorld(worldId, position);

    // Add circuit visualization
    if (enableCircuits)
    {
        CircuitVisualization circuit = world.AddComponent<CircuitVisualization>();
        CircuitNetworkManager.Instance.RegisterWorldCircuit(worldId, circuit);
    }
}
```

#### With Mining System
```csharp
// WavePacketMiningSystem.cs
void OnPacketExtracted(ulong worldId, float energy)
{
    // Update circuit energy levels
    CircuitNetworkManager.Instance.UpdateWorldEnergy(worldId, energy);
}
```

### Debug Commands
```bash
# Test circuit visualization
spacetime call system debug_test_circuit_visual

# Set circuit energy level
spacetime call system debug_set_circuit_energy 0 0 0 100.0

# List all active circuits
spacetime call system debug_list_circuits
```

### Performance Considerations
1. **Line Renderer Optimization**: Use single LineRenderer per circuit ring
2. **Particle Pooling**: Pool energy flow particles for reuse
3. **LOD System**: Reduce circuit detail at distance
4. **Culling**: Hide circuits outside camera frustum

### Future Enhancements
1. **Dynamic Circuit Activation**: Circuits activate based on energy thresholds
2. **Multi-Ring Circuits**: Higher energy worlds get additional circuit rings
3. **Energy Routing**: Visual representation of energy packet routing
4. **Circuit Puzzles**: Interactive quantum gate placement on circuits

---

## 3.10 Wave Packet Mining Visual System
**Status:** ✅ Implemented (October 2025)

### Overview
The Wave Packet Mining Visual System creates stunning visual effects when mining quantum orbs, featuring concentric colored rings representing frequency bands and a grid distortion effect that warps space as packets travel.

### Visual Components

#### Concentric Rings
Six colored rings representing frequency bands, expanding outward:
- **Red** (innermost): Base frequency - Scale 0.5
- **Yellow**: RG mixed frequency - Scale 0.8
- **Green**: Phase frequency - Scale 1.1
- **Cyan**: GB mixed frequency - Scale 1.4
- **Blue**: Computation frequency - Scale 1.7
- **Magenta** (outermost): BR mixed frequency - Scale 2.0

#### Grid Distortion Effect
Shader-based warping of space around wave packets:
- Vertex shader distortion using wave equations
- Support for up to 32 concurrent packets
- Distance-based fading for performance
- Procedural grid generation option

### Component Architecture

#### WavePacketVisualizer.cs
**Purpose:** Manages enhanced wave packet visuals with object pooling
```csharp
public class WavePacketVisualizer : MonoBehaviour
{
    [Header("Wave Visual Components")]
    [SerializeField] private GameObject concentricRingsPrefab;
    [SerializeField] private Material gridDistortionMaterial;
    [SerializeField] private GameObject gridPlanePrefab;

    [Header("Ring Configuration")]
    [SerializeField] private Color[] frequencyColors = new Color[]
    {
        new Color(1f, 0f, 0f, 0.8f),    // Red (innermost)
        new Color(1f, 1f, 0f, 0.8f),    // Yellow
        new Color(0f, 1f, 0f, 0.8f),    // Green
        new Color(0f, 1f, 1f, 0.8f),    // Cyan
        new Color(0f, 0f, 1f, 0.8f),    // Blue
        new Color(1f, 0f, 1f, 0.8f)     // Magenta (outermost)
    };

    [Header("Animation")]
    [SerializeField] private float ringExpansionRate = 2f;
    [SerializeField] private float ringRotationSpeed = 30f;
    [SerializeField] private AnimationCurve pulseCurve;
    [SerializeField] private float pulseAmplitude = 0.2f;

    [Header("Performance")]
    [SerializeField] private int maxActivePackets = 32;
    [SerializeField] private bool useObjectPooling = true;

    private static List<WavePacketData> activePackets = new();
    private Queue<GameObject> ringPool = new Queue<GameObject>();

    public GameObject CreateEnhancedWaveVisual(
        ulong packetId,
        Vector3 sourcePos,
        Vector3 targetPos,
        float frequency)
    {
        // Create or get from pool
        GameObject waveVisual = useObjectPooling && ringPool.Count > 0 ?
            ringPool.Dequeue() :
            CreateConcentricRings();

        // Configure based on frequency
        ConfigureRings(waveVisual, frequency);

        // Add to active tracking
        activePackets.Add(new WavePacketData
        {
            PacketId = packetId,
            Position = sourcePos,
            Frequency = frequency,
            VisualObject = waveVisual
        });

        return waveVisual;
    }
}
```

#### WavePacketGridDistortion.shader
**Purpose:** URP shader for grid warping effect
```hlsl
Shader "SYSTEM/WavePacketGridDistortion"
{
    Properties
    {
        _GridColor ("Grid Color", Color) = (0.2, 0.3, 0.4, 0.5)
        _DistortionStrength ("Distortion Strength", Range(0, 2)) = 0.5
        _WaveSpeed ("Wave Speed", Float) = 2.0
        _WaveFrequency ("Wave Frequency", Float) = 10.0
        _GridScale ("Grid Scale", Float) = 1.0
        _GridLineWidth ("Grid Line Width", Range(0.001, 0.1)) = 0.02
    }

    // Supports up to 32 active packets
    float4 _PacketPositions[32];
    int _ActivePacketCount;

    // Calculate wave distortion
    float3 CalculateDistortion(float3 worldPos)
    {
        float3 totalDistortion = float3(0, 0, 0);

        for (int i = 0; i < _ActivePacketCount; i++)
        {
            float3 packetPos = _PacketPositions[i].xyz;
            float amplitude = _PacketPositions[i].w;

            // Wave equation for ripple effect
            float dist = distance(worldPos.xz, packetPos.xz);
            float wave = sin(dist * _WaveFrequency - _Time.y * _WaveSpeed);
            float falloff = exp(-dist * 0.15);

            // Vertical and radial displacement
            float verticalDisplacement = wave * falloff * amplitude * _DistortionStrength;
            totalDistortion.y += verticalDisplacement;
        }

        return totalDistortion;
    }
}
```

### Integration with Mining System

#### WavePacketMiningSystem.cs Integration
```csharp
private void CreateVisualPacket(WavePacketExtraction extraction)
{
    // Check for enhanced visualizer
    var visualizer = GetComponent<WavePacketVisualizer>();
    if (visualizer != null)
    {
        // Use enhanced visuals
        packet = visualizer.CreateEnhancedWaveVisual(
            extraction.WavePacketId,
            orbObj.transform.position,
            playerTransform.position,
            extraction.Signature.Frequency
        );
    }
    else if (wavePacketPrefab != null)
    {
        // Fallback to simple visuals
        packet = Instantiate(wavePacketPrefab, orbObj.transform.position, Quaternion.identity);
    }

    // Start movement
    StartCoroutine(MovePacketToPlayer(extraction.WavePacketId, packet));
}
```

### Prefab Setup

#### Concentric Rings Prefab Structure
```
ConcentricRingsPrefab
├── Ring_0_Red       (Scale: 0.5, 0.02, 0.5)
├── Ring_1_Yellow    (Scale: 0.8, 0.02, 0.8)
├── Ring_2_Green     (Scale: 1.1, 0.02, 1.1)
├── Ring_3_Cyan      (Scale: 1.4, 0.02, 1.4)
├── Ring_4_Blue      (Scale: 1.7, 0.02, 1.7)
└── Ring_5_Magenta   (Scale: 2.0, 0.02, 2.0)
```

#### Ring Material Configuration
- **Shader:** Universal Render Pipeline/Lit
- **Surface Type:** Transparent
- **Blending Mode:** Alpha
- **Emission:** Enabled with matching color
- **Alpha:** 0.8 for semi-transparency

### Animation System

#### Ring Animations
1. **Rotation**: Continuous rotation around Y-axis at 30°/second
2. **Pulsing**: AnimationCurve-based scale modulation
3. **Expansion**: Gradual growth as packet travels
4. **Amplitude Fade**: Reduces distortion strength with distance

#### Update Loop
```csharp
void AnimateRings()
{
    foreach (var packet in activePackets)
    {
        if (packet.VisualObject == null) continue;

        float age = Time.time - packet.CreationTime;

        // Rotate rings
        packet.VisualObject.transform.Rotate(Vector3.up, ringRotationSpeed * Time.deltaTime);

        // Pulse effect
        float pulseValue = pulseCurve.Evaluate((age * 2f) % 1f);
        float pulseScale = 1f + (pulseValue - 1f) * pulseAmplitude;

        // Expansion over time
        float expansionScale = 1f + (age * ringExpansionRate * 0.05f);

        // Apply combined scale
        packet.VisualObject.transform.localScale = Vector3.one * pulseScale * expansionScale;
    }
}
```

### Performance Optimizations

#### Object Pooling
- Pre-creates 10 ring objects on initialization
- Reuses objects instead of instantiate/destroy
- Reduces garbage collection pressure

#### Shader Optimizations
- Grid distortion calculated in vertex shader only
- Maximum 32 concurrent packets limit
- Distance-based fading reduces overdraw
- Single-pass rendering for WebGL compatibility

#### LOD Recommendations
For distant packets:
- Reduce ring count from 6 to 3
- Simplify grid distortion calculations
- Lower particle emission rates
- Use simpler shaders

### Shader Fixes Applied

#### Line Variable Name Conflict
**Issue:** "line" is a reserved keyword in HLSL
**Fix:** Renamed to "gridLine" in CreateGrid function
```hlsl
// Before (caused error)
float line = min(grid.x, grid.y);

// After (fixed)
float gridLine = min(grid.x, grid.y);
```

#### Smoothstep Float Literal
**Issue:** HLSL requires explicit float literals
**Fix:** Changed `smoothstep(0, ...)` to `smoothstep(0.0, ...)`

### Testing Commands

```bash
# Spawn test orbs with different frequencies
spacetime call system spawn_test_orb 0 299 0 0 100   # Red frequency
spacetime call system spawn_test_orb 20 299 0 2 100  # Green frequency
spacetime call system spawn_test_orb -20 299 0 4 100 # Blue frequency
```

### Visual Customization Options

#### Holographic Style
- Increase transparency to 0.3-0.5 alpha
- Add rim lighting to materials
- Use additive blending mode
- Increase emission intensity to 2.0

#### Energy Field Style
- Add particle systems to each ring
- Use noise texture for grid
- Animate material properties
- Add light components to rings

#### Minimalist Style
- Use only 3 rings (RGB primary colors)
- Simple unlit shaders
- No grid distortion
- Flat colors without emission

### Documentation
Complete setup guide available at:
`H:\SpaceTime\SYSTEM\SYSTEM-client-3d\Assets\Prefabs\WAVE_PACKET_VISUAL_SETUP.md`

### Future Enhancements
1. **Dynamic Colors**: Modulate based on extraction success rate
2. **Advanced Shaders**: Add refraction, chromatic aberration
3. **Sound Integration**: Attach audio sources for mining sounds
4. **Trailing Particles**: Add particle trails as packets move

---

## 3.11 Energy Spire System Architecture
**Status:** ✅ Implemented (October 2025)

### Overview
The Energy Spire System implements a Face-Centered Cubic (FCC) lattice structure around spherical worlds, providing infrastructure for inter-world energy transfer and quantum tunneling. The system consists of 26 spires per world arranged in a three-tier hierarchy.

### FCC Lattice Structure

**26-Spire Configuration:**
- **6 Cardinal Spires** (Face Centers): R = 300 units
- **12 Edge Spires** (Edge Midpoints): R/√2 ≈ 212.13 units
- **8 Vertex Spires** (Cube Corners): R/√3 ≈ 173.21 units

**World Radius Constant:** R = 300 units (matching spherical world radius)

### Three-Tier Component Architecture

#### 1. WorldCircuit (Optional Ground-Level Component)
**Purpose:** Ground-level emitter for mining orbs (optional, not currently spawned)

```rust
#[spacetimedb(table)]
pub struct WorldCircuit {
    #[primarykey]
    #[auto_inc]
    pub circuit_id: u64,
    pub world_coords: WorldCoords,
    pub cardinal_direction: String,
    pub circuit_type: String,
    pub qubit_count: u8,
    pub orbs_per_emission: u32,
    pub emission_interval_ms: u64,
    pub last_emission_time: u64,
}
```

**Fields:**
- `circuit_id` - Unique identifier
- `world_coords` - FCC lattice position
- `cardinal_direction` - "North", "South", "East", "West", "Forward", "Back", etc.
- `circuit_type` - Circuit type identifier
- `qubit_count` - Number of qubits for quantum circuit puzzles
- `orbs_per_emission` - Orbs spawned per emission event
- `emission_interval_ms` - Time between orb emissions
- `last_emission_time` - Last emission timestamp

#### 2. DistributionSphere (Required Mid-Level Component)
**Purpose:** Routing sphere for wave packet transfers between worlds

```rust
#[spacetimedb(table)]
pub struct DistributionSphere {
    #[primarykey]
    #[auto_inc]
    pub sphere_id: u64,
    pub world_coords: WorldCoords,
    pub cardinal_direction: String,
    pub sphere_position: DbVector3,
    pub sphere_radius: f32,
    pub transit_buffer: Vec<WavePacketSample>,
    pub packets_routed: u64,
    pub last_packet_time: Timestamp,
}
```

**Fields:**
- `sphere_id` - Unique identifier
- `world_coords` - World this sphere belongs to
- `cardinal_direction` - Spire position (North, NorthEast, etc.)
- `sphere_position` - Pre-calculated 3D position on world surface
- `sphere_radius` - Sphere radius (default: 40 units)
- `transit_buffer` - Temporary storage for routing packets
- `packets_routed` - Lifetime packet count
- `last_packet_time` - Last routing activity timestamp

#### 3. QuantumTunnel (Required Top-Level Component)
**Purpose:** Colored ring with charge system for inter-world connections

```rust
#[spacetimedb(table)]
pub struct QuantumTunnel {
    #[primarykey]
    #[auto_inc]
    pub tunnel_id: u64,
    pub world_coords: WorldCoords,
    pub cardinal_direction: String,
    pub ring_charge: f32,
    pub tunnel_status: String,
    pub connected_to_world: Option<WorldCoords>,
    pub connected_to_sphere_id: Option<u64>,
    pub tunnel_color: String,
    pub formed_at: Option<Timestamp>,
}
```

**Fields:**
- `tunnel_id` - Unique identifier
- `world_coords` - World this tunnel belongs to
- `cardinal_direction` - Same as DistributionSphere
- `ring_charge` - Charge level (0-100%)
- `tunnel_status` - "Inactive", "Charging", "Active"
- `connected_to_world` - Connected world coordinates (if Active)
- `connected_to_sphere_id` - Connected sphere ID (if Active)
- `tunnel_color` - Tier-based color (see Color System below)
- `formed_at` - When tunnel became Active

### Spire Positioning System

#### Cardinal Spires (6 Total - Face Centers)
**Radius:** R = 300 units

| Direction | Position (x, y, z) | Axis | Tunnel Color |
|-----------|-------------------|------|--------------|
| North | (0, 300, 0) | +Y | Green |
| South | (0, -300, 0) | -Y | Green |
| East | (300, 0, 0) | +X | Red |
| West | (-300, 0, 0) | -X | Red |
| Forward | (0, 0, 300) | +Z | Blue |
| Back | (0, 0, -300) | -Z | Blue |

#### Edge Spires (12 Total - Edge Midpoints)
**Radius:** R/√2 ≈ 212.13 units

**XY Plane (Yellow Tunnels):**
- NorthEast: (212.13, 212.13, 0)
- NorthWest: (-212.13, 212.13, 0)
- SouthEast: (212.13, -212.13, 0)
- SouthWest: (-212.13, -212.13, 0)

**YZ Plane (Cyan Tunnels):**
- NorthForward: (0, 212.13, 212.13)
- NorthBack: (0, 212.13, -212.13)
- SouthForward: (0, -212.13, 212.13)
- SouthBack: (0, -212.13, -212.13)

**XZ Plane (Magenta Tunnels):**
- EastForward: (212.13, 0, 212.13)
- EastBack: (212.13, 0, -212.13)
- WestForward: (-212.13, 0, 212.13)
- WestBack: (-212.13, 0, -212.13)

#### Vertex Spires (8 Total - Cube Corners)
**Radius:** R/√3 ≈ 173.21 units

**All Corners (White Tunnels):**
- (+X, +Y, +Z): (173.21, 173.21, 173.21)
- (+X, +Y, -Z): (173.21, 173.21, -173.21)
- (+X, -Y, +Z): (173.21, -173.21, 173.21)
- (+X, -Y, -Z): (173.21, -173.21, -173.21)
- (-X, +Y, +Z): (-173.21, 173.21, 173.21)
- (-X, +Y, -Z): (-173.21, 173.21, -173.21)
- (-X, -Y, +Z): (-173.21, -173.21, 173.21)
- (-X, -Y, -Z): (-173.21, -173.21, -173.21)

### Color System

**Tunnel Color Tiers:**
- **Primary (RGB Axes):** Red (±X), Green (±Y), Blue (±Z)
- **Secondary (Planar):** Yellow (XY), Cyan (YZ), Magenta (XZ)
- **Tertiary (Volumetric):** White (cube corners)

**Color Coding Logic:**
```rust
fn get_tunnel_color(cardinal_direction: &str) -> String {
    match cardinal_direction {
        "North" | "South" => "Green".to_string(),
        "East" | "West" => "Red".to_string(),
        "Forward" | "Back" => "Blue".to_string(),
        "NorthEast" | "NorthWest" | "SouthEast" | "SouthWest" => "Yellow".to_string(),
        "NorthForward" | "NorthBack" | "SouthForward" | "SouthBack" => "Cyan".to_string(),
        "EastForward" | "EastBack" | "WestForward" | "WestBack" => "Magenta".to_string(),
        _ => "White".to_string(), // Vertex spires
    }
}
```

### Server Reducers

#### spawn_all_26_spires(world_x, world_y, world_z)
**Purpose:** Spawns complete 26-spire system for a world

```rust
#[spacetimedb::reducer]
pub fn spawn_all_26_spires(
    ctx: &ReducerContext,
    world_x: i32,
    world_y: i32,
    world_z: i32,
) -> Result<(), String> {
    let world_coords = WorldCoords { x: world_x, y: world_y, z: world_z };

    // Spawn all 26 spires
    spawn_cardinal_spires(ctx, &world_coords)?;  // 6 spires
    spawn_edge_spires(ctx, &world_coords)?;      // 12 spires
    spawn_vertex_spires(ctx, &world_coords)?;    // 8 spires

    log(&format!("Spawned 26 spires for world ({}, {}, {})", world_x, world_y, world_z));
    Ok(())
}
```

**Each spire creates:**
- 1 DistributionSphere
- 1 QuantumTunnel

**Total components created:** 26 spheres + 26 tunnels = 52 database rows

#### Example Usage:
```bash
# Spawn 26 spires at origin world
spacetime call system-test --server https://maincloud.spacetimedb.com spawn_all_26_spires 0 0 0

# Verify creation
spacetime sql system-test "SELECT COUNT(*) FROM distribution_sphere"  # Result: 26
spacetime sql system-test "SELECT COUNT(*) FROM quantum_tunnel"       # Result: 26
```

### Unity Visualization (EnergySpireManager.cs)

**Architecture:** Event-driven visualization via SpacetimeDBEventBridge

```csharp
public class EnergySpireManager : MonoBehaviour
{
    [Header("Spire Prefabs")]
    [SerializeField] private GameObject distributionSpherePrefab;
    [SerializeField] private GameObject quantumTunnelPrefab;

    private Dictionary<ulong, GameObject> activeSpires = new();

    void OnEnable()
    {
        GameEventBus.Instance.Subscribe<DistributionSphereInsertedEvent>(OnSphereInserted);
        GameEventBus.Instance.Subscribe<QuantumTunnelInsertedEvent>(OnTunnelInserted);
    }

    void OnSphereInserted(DistributionSphereInsertedEvent evt)
    {
        CreateSphereVisualization(evt.Sphere);
    }

    void CreateSphereVisualization(DistributionSphere sphere)
    {
        GameObject sphereObj = Instantiate(distributionSpherePrefab);
        sphereObj.transform.position = ConvertToUnityVector(sphere.SpherePosition);
        sphereObj.transform.localScale = Vector3.one * sphere.SphereRadius * 2f;

        // Apply material with safe WebGL creation
        Material sphereMat = CreateSafeMaterial(
            new Color(0.5f, 0.8f, 1.0f),
            metallic: 0.6f,
            smoothness: 0.8f,
            enableEmission: true,
            emissionColor: new Color(0.3f, 0.5f, 0.8f)
        );

        if (sphereMat != null)
            sphereObj.GetComponent<Renderer>().material = sphereMat;

        activeSpires[sphere.SphereId] = sphereObj;
    }
}
```

### WebGL Safe Material Creation Pattern

**Critical for WebGL compatibility:** Always use fallback chain for shaders

```csharp
Material CreateSafeMaterial(Color color, float metallic, float smoothness,
                           bool enableEmission = false, Color emissionColor = default(Color))
{
    // Shader fallback chain (URP → Standard → Unlit)
    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
    if (shader == null) shader = Shader.Find("Standard");
    if (shader == null) shader = Shader.Find("Unlit/Color");

    if (shader == null)
    {
        SystemDebug.LogError(SystemDebug.Category.SpireVisualization,
            "Could not find any suitable shader for spire materials!");
        return null;
    }

    Material mat = new Material(shader);
    mat.color = color;

    // Property existence checks (critical for WebGL)
    if (mat.HasProperty("_Metallic"))
        mat.SetFloat("_Metallic", metallic);

    if (mat.HasProperty("_Glossiness"))
        mat.SetFloat("_Glossiness", smoothness);
    else if (mat.HasProperty("_Smoothness"))
        mat.SetFloat("_Smoothness", smoothness);

    if (enableEmission && mat.HasProperty("_EmissionColor"))
    {
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emissionColor);
    }

    return mat;
}
```

**Why this pattern is necessary:**
- `Shader.Find("Standard")` returns `null` in WebGL builds
- Material properties vary between URP and built-in render pipeline
- `HasProperty()` prevents `NullReferenceException` in WebGL
- Graceful degradation ensures spires render even with basic shaders

### Deployment Workflow

**Complete deployment to test environment:**

```bash
# 1. Deploy server module
./Scripts/deploy-spacetimedb.ps1 -Environment test -DeleteData -Yes

# 2. Spawn spires at origin world
spacetime call system-test --server https://maincloud.spacetimedb.com spawn_all_26_spires 0 0 0

# 3. Verify creation
spacetime sql system-test --server https://maincloud.spacetimedb.com "SELECT cardinal_direction, tunnel_color FROM quantum_tunnel"
```

**Expected output:**
```
| cardinal_direction | tunnel_color |
|--------------------|--------------|
| North              | Green        |
| South              | Green        |
| East               | Red          |
| West               | Red          |
| Forward            | Blue         |
| Back               | Blue         |
| NorthEast          | Yellow       |
| ... (20 more rows) |              |
```

### Charge System (Future Enhancement)

**Current Status:** Infrastructure in place, charging logic not yet implemented

**Planned Charging System:**
1. Mining packets → Route through DistributionSphere → Increment tunnel charge
2. Charge reaches 100% → Tunnel status changes to "Active"
3. Active tunnel → Enables inter-world travel
4. Charge depletes over time → Requires maintenance mining

**Database Schema Ready:**
- `ring_charge` field (0-100%)
- `tunnel_status` field ("Inactive", "Charging", "Active")
- `connected_to_world` and `connected_to_sphere_id` for connections

### Integration with Transfer System

**Wave packet transfers can route through spires:**

```rust
// Future: Route transfer through distribution sphere
pub fn route_packet_through_spire(
    ctx: &ReducerContext,
    packet: WavePacketSample,
    from_world: WorldCoords,
    to_world: WorldCoords,
) -> Result<(), String> {
    // Find appropriate distribution sphere
    let sphere = find_spire_for_direction(ctx, &from_world, &to_world)?;

    // Add to transit buffer
    let mut updated_sphere = sphere.clone();
    updated_sphere.transit_buffer.push(packet);

    ctx.db.distribution_sphere().delete(sphere);
    ctx.db.distribution_sphere().insert(updated_sphere)?;

    Ok(())
}
```

### Performance Considerations

**Database Impact:**
- 26 spheres × N worlds = Scalable with proper indexing
- Transit buffers: Limited to 100 packets per sphere
- Queries use world_coords index for efficient lookups

**Unity Rendering:**
- Object pooling for spire GameObjects
- LOD system: Distant spires use simplified meshes
- Culling: Spires outside camera frustum not rendered
- Emission only on charged tunnels (reduces overdraw)

### Testing Commands

```bash
# List all spires for a world
spacetime sql system-test "SELECT sphere_id, cardinal_direction, sphere_radius FROM distribution_sphere WHERE world_coords = (0, 0, 0)"

# Check tunnel charge levels
spacetime sql system-test "SELECT cardinal_direction, ring_charge, tunnel_status FROM quantum_tunnel WHERE world_coords = (0, 0, 0)"

# Count total spires in database
spacetime sql system-test "SELECT COUNT(*) FROM distribution_sphere"
spacetime sql system-test "SELECT COUNT(*) FROM quantum_tunnel"
```

### Related Documentation
- **Energy Spire Unity Design:** `.claude/energy-spire-unity-design.md`
- **Energy Spire Server Implementation:** `.claude/energy-spire-server-implementation.md`
- **WebGL Deployment:** See Section 5.5 below

---

## 3.12 Inventory System Architecture
**Status:** ✅ Implemented (October 2025)

### Overview
The Inventory System underwent a major migration from a legacy frequency band enumeration system to a modern composition-based architecture. The new system stores wave packets with their full spectral properties, enabling more sophisticated energy mechanics and inter-player transfers.

### System Migration (October 2025)

**Migration:** Old Frequency Band System → Composition-Based Inventory

**Date Completed:** 2025-10-13

#### Legacy System (Deprecated)
```rust
// OLD: Enum-based frequency bands
#[derive(SpacetimeType)]
pub enum FrequencyBand {
    Red,      // R
    Yellow,   // RG
    Green,    // G
    Cyan,     // GB
    Blue,     // B
    Magenta,  // BR
}

// OLD: Separate storage table per player
#[spacetimedb(table)]
pub struct WavePacketStorage {
    #[primarykey]
    #[auto_inc]
    pub storage_id: u64,
    pub player_identity: Identity,
    pub frequency_band: FrequencyBand,
    pub packet_count: u32,
}
```

**Problems with Legacy System:**
- Fixed 6 frequency bands (no spectrum flexibility)
- Separate table rows per band (inefficient queries)
- No phase or amplitude information
- Difficult to implement transfers
- Limited to 6 discrete colors

#### Current System (Composition-Based)
```rust
/// Player inventory using unified wave packet composition
/// Max capacity: 300 total packets
#[spacetimedb(table)]
pub struct PlayerInventory {
    #[primarykey]
    pub player_id: u64,

    /// Unified composition - automatically consolidated when packets are added
    pub inventory_composition: Vec<WavePacketSample>,

    pub total_count: u32,    // Sum of all packet counts, max 300
    pub last_updated: Timestamp,
}

/// Wave packet with full spectral properties
#[derive(SpacetimeType, Debug, Clone)]
pub struct WavePacketSample {
    pub frequency: f32,      // Continuous value (0.0 to 2π radians)
    pub amplitude: f32,      // Intensity (0.0 to 1.0)
    pub phase: f32,          // Phase angle (0.0 to 2π radians)
    pub packet_count: u32,   // Quantity of identical packets
}
```

**Advantages of New System:**
- Continuous frequency spectrum (not limited to 6 colors)
- Full quantum properties (frequency, amplitude, phase)
- Single table row per player (efficient)
- Automatic consolidation of identical packets
- Capacity limit enforced (300 packets max)
- Transfer-friendly architecture

### Database Schema

#### PlayerInventory Table
**Purpose:** Stores each player's collected wave packets

| Field | Type | Description |
|-------|------|-------------|
| `player_id` | u64 | Primary key, links to Player table |
| `inventory_composition` | Vec<WavePacketSample> | List of unique packet types |
| `total_count` | u32 | Sum of all packet_count fields (max 300) |
| `last_updated` | Timestamp | Last modification time |

**Indexing:**
- Primary key: `player_id`
- No additional indexes needed (single row per player)

#### Consolidation Logic
When packets are added, identical packets are automatically merged:

```rust
fn add_packets_to_inventory(
    ctx: &ReducerContext,
    player_id: u64,
    new_packets: Vec<WavePacketSample>,
) -> Result<(), String> {
    // Get existing inventory
    let inventory = ctx.db.player_inventory()
        .player_id().find(&player_id)
        .ok_or("Inventory not found")?;

    let mut composition = inventory.inventory_composition.clone();

    // Consolidate new packets
    for new_packet in new_packets {
        let mut merged = false;

        for existing in composition.iter_mut() {
            // Match frequency within tolerance (0.01 radians)
            if (existing.frequency - new_packet.frequency).abs() < 0.01 &&
               (existing.amplitude - new_packet.amplitude).abs() < 0.01 &&
               (existing.phase - new_packet.phase).abs() < 0.01
            {
                // Merge identical packets
                existing.packet_count += new_packet.packet_count;
                merged = true;
                break;
            }
        }

        if !merged {
            // Add as new entry
            composition.push(new_packet);
        }
    }

    // Calculate total count
    let total: u32 = composition.iter().map(|p| p.packet_count).sum();

    // Enforce capacity limit
    if total > 300 {
        return Err("Inventory capacity exceeded (max 300 packets)".to_string());
    }

    // Update inventory
    let updated = PlayerInventory {
        player_id,
        inventory_composition: composition,
        total_count: total,
        last_updated: Timestamp::now(),
    };

    ctx.db.player_inventory().delete(inventory);
    ctx.db.player_inventory().insert(updated)?;

    Ok(())
}
```

**Consolidation Rules:**
- Frequency tolerance: ±0.01 radians (~0.57°)
- Amplitude tolerance: ±0.01
- Phase tolerance: ±0.01 radians (~0.57°)
- Identical packets merge by summing `packet_count`

### Capacity Management

**Maximum Capacity:** 300 total packets per player

**Enforcement:**
- Checked in `add_packets_to_inventory()` reducer
- Checked in mining extraction reducers
- Checked in transfer acceptance reducers
- Error returned if capacity would be exceeded

**Capacity Strategies:**
1. **Consolidation**: Merge similar packets to save space
2. **Transfers**: Send excess packets to other players
3. **Crafting** (future): Combine packets into higher-tier items
4. **Storage Devices** (future): Expand capacity via in-game items

### Mining Integration

**Mining v2 Reducers:**

```rust
#[spacetimedb::reducer]
pub fn start_mining_v2(
    ctx: &ReducerContext,
    orb_id: u64
) -> Result<(), String> {
    // Validate player and orb
    let player = get_player(ctx)?;
    let orb = get_orb(ctx, orb_id)?;

    // Create mining session
    let session = MiningSession {
        session_id: 0,  // Auto-increment
        player_identity: ctx.sender,
        orb_id,
        started_at: Timestamp::now(),
        last_extraction: Timestamp::now(),
        is_active: true,
    };

    ctx.db.mining_session().insert(session)?;
    Ok(())
}

#[spacetimedb::reducer]
pub fn extract_packets_v2(
    ctx: &ReducerContext,
    session_id: u64
) -> Result<(), String> {
    // Get active session
    let session = ctx.db.mining_session()
        .session_id().find(&session_id)
        .ok_or("Session not found")?;

    if !session.is_active {
        return Err("Session is not active".to_string());
    }

    // Get orb composition
    let orb = get_orb(ctx, session.orb_id)?;

    // Extract random packets from orb
    let extracted = extract_random_packets(&orb, 5)?;  // Extract 5 packets

    // Add to player inventory
    add_packets_to_inventory(ctx, session.player_identity, extracted)?;

    // Update orb (remove extracted packets)
    update_orb_after_extraction(ctx, &orb, extracted)?;

    // Update session timestamp
    update_mining_session_timestamp(ctx, session_id)?;

    Ok(())
}
```

### Transfer System Integration

**PacketTransfer Table:**
```rust
#[spacetimedb(table)]
pub struct PacketTransfer {
    #[primarykey]
    #[auto_inc]
    pub transfer_id: u64,
    pub from_player_id: u64,
    pub to_player_id: u64,
    pub transfer_composition: Vec<WavePacketSample>,
    pub transfer_status: String,  // "Pending", "Accepted", "Rejected", "Expired"
    pub created_at: Timestamp,
    pub expires_at: Timestamp,
}
```

**Transfer Workflow:**

1. **Initiate Transfer:**
```rust
#[spacetimedb::reducer]
pub fn initiate_transfer(
    ctx: &ReducerContext,
    to_player_id: u64,
    packets: Vec<WavePacketSample>,
) -> Result<(), String> {
    let from_player_id = get_player_id(ctx)?;

    // Validate sender has packets
    validate_player_has_packets(ctx, from_player_id, &packets)?;

    // Remove from sender's inventory
    remove_packets_from_inventory(ctx, from_player_id, packets.clone())?;

    // Create transfer record
    let transfer = PacketTransfer {
        transfer_id: 0,  // Auto-increment
        from_player_id,
        to_player_id,
        transfer_composition: packets,
        transfer_status: "Pending".to_string(),
        created_at: Timestamp::now(),
        expires_at: Timestamp::now() + Duration::minutes(5),
    };

    ctx.db.packet_transfer().insert(transfer)?;
    Ok(())
}
```

2. **Accept Transfer:**
```rust
#[spacetimedb::reducer]
pub fn accept_transfer(
    ctx: &ReducerContext,
    transfer_id: u64,
) -> Result<(), String> {
    let transfer = get_transfer(ctx, transfer_id)?;
    let player_id = get_player_id(ctx)?;

    // Validate recipient
    if transfer.to_player_id != player_id {
        return Err("Not the intended recipient".to_string());
    }

    // Check capacity
    let inventory = get_inventory(ctx, player_id)?;
    let total_incoming: u32 = transfer.transfer_composition.iter()
        .map(|p| p.packet_count).sum();

    if inventory.total_count + total_incoming > 300 {
        return Err("Insufficient inventory space".to_string());
    }

    // Add to recipient's inventory
    add_packets_to_inventory(ctx, player_id, transfer.transfer_composition.clone())?;

    // Mark transfer as accepted
    update_transfer_status(ctx, transfer_id, "Accepted")?;

    Ok(())
}
```

3. **Reject Transfer:**
```rust
#[spacetimedb::reducer]
pub fn reject_transfer(
    ctx: &ReducerContext,
    transfer_id: u64,
) -> Result<(), String> {
    let transfer = get_transfer(ctx, transfer_id)?;

    // Return packets to sender
    add_packets_to_inventory(ctx, transfer.from_player_id, transfer.transfer_composition.clone())?;

    // Mark as rejected
    update_transfer_status(ctx, transfer_id, "Rejected")?;

    Ok(())
}
```

**Transfer Expiration:**
- Transfers expire after 5 minutes if not accepted
- Expired transfers automatically return packets to sender
- Cleanup reducer runs periodically to handle expired transfers

### Unity Client Integration

**TransferWindow.cs** - UI for managing transfers

```csharp
public class TransferWindow : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TMP_InputField recipientIdInput;
    [SerializeField] private Button sendButton;
    [SerializeField] private Transform transferListContainer;

    private List<WavePacketSample> selectedPackets = new();

    void OnEnable()
    {
        // Subscribe to transfer events
        GameEventBus.Instance.Subscribe<TransferReceivedEvent>(OnTransferReceived);
        GameEventBus.Instance.Subscribe<TransferAcceptedEvent>(OnTransferAccepted);
    }

    public void SendTransfer()
    {
        ulong recipientId = ulong.Parse(recipientIdInput.text);

        // Call reducer
        GameManager.Instance.conn.Reducers.InitiateTransfer(
            recipientId,
            selectedPackets.ToArray()
        );

        selectedPackets.Clear();
        CloseWindow();
    }

    private void OnTransferReceived(TransferReceivedEvent evt)
    {
        // Show notification
        NotificationManager.Show($"Transfer from Player {evt.Transfer.FromPlayerId}");

        // Update pending transfers list
        RefreshTransferList();
    }
}
```

### Client Visualization

**Inventory UI displays:**
- Frequency spectrum bar (visual representation of composition)
- Total packet count (e.g., "287 / 300")
- Breakdown by frequency band:
  - Red: 45 packets
  - Yellow: 32 packets
  - Green: 78 packets
  - Cyan: 41 packets
  - Blue: 56 packets
  - Magenta: 35 packets

**Transfer UI shows:**
- Pending incoming transfers (accept/reject buttons)
- Pending outgoing transfers (cancel option)
- Transfer history (last 10 transfers)

### Performance Optimizations

**Database:**
- Single row per player (efficient)
- `Vec<WavePacketSample>` stored as binary blob
- Automatic consolidation reduces vector size
- No cross-player queries needed

**Network:**
- Only send full inventory on player connect
- Incremental updates for additions/removals
- Transfer notifications use lightweight events

**Unity:**
- Cache inventory locally in `GameData.Instance`
- UI updates only when inventory changes
- Lazy loading of transfer history

### Migration Process (October 2025)

**Steps Taken:**
1. Created new `PlayerInventory` table
2. Implemented consolidation logic
3. Updated all mining reducers to use new system
4. Created transfer system reducers
5. Migrated Unity client to new inventory events
6. Deprecated `WavePacketStorage` table (kept for legacy data)
7. Tested with 100+ concurrent players

**Data Migration:**
No automatic migration was performed. Legacy `WavePacketStorage` entries remain in database but are no longer used. New mining sessions automatically use the new `PlayerInventory` system.

### Future Enhancements

**Storage Devices** (Planned)
```rust
#[spacetimedb(table)]
pub struct StorageDevice {
    #[primarykey]
    pub device_id: u64,
    pub player_id: u64,
    pub device_type: String,  // "Basic", "Advanced", "Quantum"
    pub capacity_bonus: u32,   // Additional capacity beyond 300
    pub stored_composition: Vec<WavePacketSample>,
}
```

**Crafting System** (Planned)
- Combine multiple packets into higher-tier energy items
- Recipes require specific frequency combinations
- Crafted items provide bonuses or special abilities

**Trading Market** (Future)
- Public marketplace for packet exchanges
- Buy/sell orders for specific frequencies
- Price discovery based on supply/demand

### Testing Commands

```bash
# View player inventory
spacetime sql system-test "SELECT player_id, total_count FROM player_inventory WHERE player_id = 1"

# View inventory composition
spacetime sql system-test "SELECT inventory_composition FROM player_inventory WHERE player_id = 1"

# Check pending transfers
spacetime sql system-test "SELECT transfer_id, from_player_id, to_player_id, transfer_status FROM packet_transfer WHERE to_player_id = 1 AND transfer_status = 'Pending'"

# Count total packets in system
spacetime sql system-test "SELECT SUM(total_count) FROM player_inventory"
```

### Related Documentation
- **Inventory Migration:** `.claude/inventory-system-migration-2025-10-13.md`
- **Transfer System Fixes:** `.claude/transfer-system-fixes-2025-10-14.md`
- **Mining System v2:** See Section 3.10 above