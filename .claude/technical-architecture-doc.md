# TECHNICAL_ARCHITECTURE.md
**Version:** 1.1.0
**Last Updated:** 2025-01-28
**Status:** Approved
**Dependencies:** [GAMEPLAY_SYSTEMS.md, SDK_PATTERNS_REFERENCE.md]

## Change Log
- v1.1.0 (2025-01-28): Added Visual Systems Architecture, Build & Deployment Pipeline, updated file structure
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
│   │   ├── Game/                    # Core game systems
│   │   │   ├── GameManager.cs       # SpacetimeDB connection
│   │   │   ├── GameData.cs          # Persistent player data
│   │   │   ├── WorldManager.cs      # World loading/spawning
│   │   │   ├── CenterWorldController.cs     # Main world sphere (prefab-based)
│   │   │   ├── PrefabWorldController.cs     # Standalone prefab world
│   │   │   ├── WorldPrefabManager.cs        # ScriptableObject for prefabs
│   │   │   ├── GameEventBus.cs              # Event system with state machine
│   │   │   ├── SpacetimeDBEventBridge.cs    # SpacetimeDB → EventBus
│   │   │   ├── PlayerTracker.cs             # Player data tracking
│   │   │   └── WorldSpawnSystem.cs          # Unified spawn system
│   │   ├── Mining/
│   │   │   ├── WavePacketMiningSystem.cs    # Mining mechanics (legacy)
│   │   │   ├── OrbVisualizationManager.cs   # Orb visualization manager
│   │   │   └── WavePacketOrbVisual.cs       # Orb prefab component
│   │   ├── Player/
│   │   │   └── PlayerController.cs          # Minecraft-style third-person
│   │   ├── Debug/
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

## 3.4 State Management Patterns

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
**Status:** ✅ Implemented (January 2025)

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
**Status:** ✅ Implemented (January 2025)

Replaced procedural mesh generation with prefab-based system for:
- ✅ Full WebGL compatibility
- ✅ Faster initialization (no runtime generation)
- ✅ Visual preview in Editor
- ✅ Support for multiple world types

**CenterWorldController.cs** - Primary world sphere controller:
```csharp
public class CenterWorldController : MonoBehaviour
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
**Status:** ✅ Implemented (January 2025)

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

**Spherical Coordinates:**
- `phi` (longitude): `atan2(z, x)` → range [-π, π]
- `theta` (latitude): `acos(y)` → range [0, π]
- Grid lines via `sin()` modulation at configurable intervals
- Thin lines via `step()` threshold (default 0.01 width)

**WebGL Compatibility:**
- Uses `GetVertexPositionInputs()` from URP ShaderLibrary
- Avoids manual matrix multiplication
- No custom matrix operations
- Tested on WebGL with proper rendering

**Quantum State Positions:**
| State | Position | Physical Meaning |
|-------|----------|------------------|
| \|0⟩ | (0, 1, 0) | North pole - Zero state |
| \|1⟩ | (0, -1, 0) | South pole - One state |
| \|+⟩ | (1, 0, 0) | Equator +X - Plus superposition |
| \|-⟩ | (-1, 0, 0) | Equator -X - Minus superposition |
| \|+i⟩ | (0, 0, 1) | Equator +Z - Plus-i superposition |
| \|-i⟩ | (0, 0, -1) | Equator -Z - Minus-i superposition |

### WebGL-Specific Optimizations
**Status:** ✅ Implemented (January 2025)

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
**Status:** ✅ Implemented (January 2025)

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
**Status:** ✅ Implemented (January 2025)

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