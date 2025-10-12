# SDK_PATTERNS_REFERENCE.md
**Version:** 1.1.0
**Last Updated:** 2025-01-28
**Status:** Approved
**Dependencies:** None (Reference Document)

## Change Log
- v1.1.0 (2025-01-28): Added Event-Driven Architecture patterns and Debug System patterns
- v1.0.0 (2024-12-19): Consolidated from spacetimedb_rust and csharp pattern docs

---

## 4.1 SpacetimeDB Rust Patterns

### Reducer Requirements

#### ✅ CORRECT Reducer Signatures
```rust
#[spacetimedb::reducer]
pub fn my_reducer(ctx: &ReducerContext) -> Result<(), String> {
    // Simple reducer
    Ok(())
}

#[spacetimedb::reducer]
pub fn with_params(
    ctx: &ReducerContext,
    param1: u64,
    param2: String,
) -> Result<(), String> {
    // Parameters must implement SpacetimeType
    Ok(())
}
```

#### ❌ INCORRECT Patterns
```rust
// Wrong: Mutable references not allowed
#[spacetimedb::reducer]
pub fn bad_reducer(
    ctx: &ReducerContext,
    session: &mut MiningSession,  // ❌ Invalid
) -> Result<(), String>

// Wrong: Complex borrowed types
#[spacetimedb::reducer]
pub fn bad_reducer2(
    ctx: &ReducerContext,
    data: &ComplexStruct,  // ❌ Invalid
) -> Result<(), String>

// Wrong: Async not supported
#[spacetimedb::reducer]
pub async fn bad_reducer3(  // ❌ No async
    ctx: &ReducerContext,
) -> Result<(), String>
```

### Table Operations

#### Finding Records
```rust
// ✅ CORRECT: Find with supported types
let player = ctx.db.player()
    .identity().find(&identity);  // Identity supported
let account = ctx.db.account()
    .username().find(&username);   // String supported

// ❌ WRONG: Complex types don't implement FilterableValue
let world = ctx.db.world()
    .world_coords().find(&coords); // WorldCoords not supported

// ✅ CORRECT: Use iteration for complex types
let world = ctx.db.world()
    .iter()
    .find(|w| w.world_coords == coords);
```

#### Delete Operations
```rust
// ❌ WRONG: delete() expects owned value
let session: &PlayerSession = get_session();
ctx.db.player_session().delete(session); // Error: expected owned

// ✅ CORRECT: Clone or move
ctx.db.player_session().delete(session.clone());
```

#### Update Pattern
```rust
// SpacetimeDB has no in-place updates
// ✅ CORRECT: Delete + Insert
let mut updated = original.clone();
updated.field = new_value;

ctx.db.my_table().delete(original);
ctx.db.my_table().insert(updated);
```

### Type Requirements

#### Hash Trait for HashMap Keys
```rust
// ❌ WRONG: Missing Hash
#[derive(SpacetimeType, Clone, PartialEq, Eq)]
pub enum MyEnum { A, B, C }

let mut map = HashMap::new();
map.insert(MyEnum::A, 42);  // Error: Hash not implemented

// ✅ CORRECT: Include Hash
#[derive(SpacetimeType, Clone, PartialEq, Eq, Hash)]
pub enum MyEnum { A, B, C }
```

### State Management

#### Static State Pattern
```rust
// For complex state not in tables
static GAME_STATE: OnceLock<Mutex<GameState>> = OnceLock::new();

fn get_game_state() -> &'static Mutex<GameState> {
    GAME_STATE.get_or_init(|| Mutex::new(GameState::default()))
}

#[spacetimedb::reducer]
pub fn use_state(ctx: &ReducerContext) -> Result<(), String> {
    let mut state = get_game_state().lock().unwrap();
    state.update();
    Ok(())
}
```

---

## 4.2 SpacetimeDB C# Patterns

### Connection Patterns

#### ✅ CORRECT Connection Building
```csharp
var conn = DbConnection.Builder()
    .WithUri("http://localhost:3000")
    .WithModuleName("my_module")
    .OnConnect((connection, identity, token) => { })
    .OnConnectError(error => { })
    .OnDisconnect((connection, error) => { })
    .Build();
```

#### ❌ INCORRECT Patterns
```csharp
// Wrong: These methods don't exist
conn.Connect(host);           // ❌ No Connect method
conn.RemoteTables.Player;     // ❌ No RemoteTables
DbConnection.Builder()
    .WithCredentials(token);   // ❌ No WithCredentials
```

### Table Access

#### ✅ CORRECT Iteration
```csharp
// Use Iter() for enumeration
foreach (var player in conn.Db.Player.Iter())
{
    if (player.Identity == targetIdentity)
        return player;
}

// Use index accessors for unique columns
var player = conn.Db.Player.Identity.Find(identity);
```

#### ❌ INCORRECT Patterns
```csharp
// Wrong: No LINQ support
var players = conn.Db.Player.Where(p => p.Active);  // ❌

// Wrong: Direct enumeration
foreach (var player in conn.Db.Player) { }  // ❌ Need Iter()

// Wrong: Find with predicate
var player = conn.Db.Player.Find(p => p.Name == "Bob"); // ❌
```

### Event Handlers

#### Reducer Events
```csharp
// ✅ CORRECT: ReducerEventContext + direct arguments
conn.Reducers.OnStartMining += HandleStartMining;

private void HandleStartMining(
    ReducerEventContext ctx, 
    ulong orbId  // Direct arguments, not wrapped
)
{
    // Note: NO .Status or .Message on ctx
    Debug.Log($"Mining started: {orbId}");
}
```

#### Table Events
```csharp
// ✅ CORRECT: EventContext + row data
conn.Db.Player.OnInsert += (EventContext ctx, Player player) =>
{
    Debug.Log($"Player joined: {player.Name}");
};

conn.Db.Player.OnUpdate += (ctx, oldPlayer, newPlayer) =>
{
    Debug.Log($"Player updated: {newPlayer.Name}");
};
```

### Unity Integration

#### MonoBehaviour Pattern
```csharp
public class SpacetimeManager : MonoBehaviour
{
    private DbConnection conn;
    
    void Start()
    {
        InitConnection();
        SubscribeEvents();
    }
    
    void OnDestroy()
    {
        // ✅ ALWAYS unsubscribe
        if (conn != null)
        {
            conn.Db.Player.OnInsert -= HandlePlayerInsert;
            conn.Reducers.OnStartMining -= HandleMining;
        }
    }
}
```

#### Coroutine Connection
```csharp
IEnumerator ConnectToSpacetimeDB()
{
    bool connected = false;
    
    conn = DbConnection.Builder()
        .WithUri(serverUrl)
        .OnConnect((connection, identity, token) => {
            connected = true;
        })
        .Build();
    
    // Wait for connection
    float timeout = 5f;
    while (!connected && timeout > 0)
    {
        timeout -= Time.deltaTime;
        yield return null;
    }
    
    if (connected)
        Debug.Log("Connected!");
    else
        Debug.LogError("Connection timeout!");
}
```

---

## 4.3 Unity Integration Patterns

### Singleton Pattern
```csharp
// Game uses singleton pattern consistently
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public DbConnection conn { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}

// Usage
GameManager.Instance.conn.Reducers.StartMining(orbId);
```

### Caching Pattern
```csharp
public class DataCache : MonoBehaviour
{
    private Dictionary<ulong, WavePacketOrb> orbCache = new();
    
    void Start()
    {
        var conn = GameManager.Instance.conn;
        
        conn.Db.WavePacketOrb.OnInsert += (ctx, orb) =>
            orbCache[orb.OrbId] = orb;
            
        conn.Db.WavePacketOrb.OnDelete += (ctx, orb) =>
            orbCache.Remove(orb.OrbId);
    }
    
    public WavePacketOrb GetOrb(ulong id) =>
        orbCache.TryGetValue(id, out var orb) ? orb : null;
}
```

### Object Pooling
```csharp
public class OrbPoolManager : MonoBehaviour
{
    private Queue<GameObject> pool = new();
    public GameObject orbPrefab;
    
    public GameObject GetOrb()
    {
        if (pool.Count > 0)
        {
            var orb = pool.Dequeue();
            orb.SetActive(true);
            return orb;
        }
        return Instantiate(orbPrefab);
    }
    
    public void ReturnOrb(GameObject orb)
    {
        orb.SetActive(false);
        pool.Enqueue(orb);
    }
}
```

---

## 4.4 Event-Driven Architecture Patterns

### GameEventBus with State Machine
```csharp
// ✅ CORRECT: State-validated event publishing
public class SpacetimeDBEventBridge : MonoBehaviour
{
    void OnOrbInsert(EventContext ctx, WavePacketOrb orb)
    {
        // Bridge database event to GameEventBus
        GameEventBus.Instance.Publish(new OrbInsertedEvent { Orb = orb });
    }
}

// ❌ INCORRECT: Direct database access from visualization
public class OrbVisualizationManager : MonoBehaviour
{
    void Update()
    {
        // Never directly query database from visualization components
        foreach (var orb in conn.Db.WavePacketOrb.Iter()) // ❌ Wrong
        {
            CreateOrbGameObject(orb);
        }
    }
}
```

### Event State Validation
```csharp
// Events must be registered for allowed states
allowedEventsPerState[GameState.PlayerReady] = new HashSet<Type> {
    typeof(InitialOrbsLoadedEvent),
    typeof(OrbInsertedEvent),
    typeof(OrbUpdatedEvent),
    typeof(OrbDeletedEvent)
};

// Events published in wrong state will be rejected
// Check console for: "Event X not allowed in state Y"
```

### Component Requirements
```csharp
// Required scene components for orb visualization
void Awake()
{
    // Both components must exist in scene
    if (FindObjectOfType<SpacetimeDBEventBridge>() == null)
        Debug.LogError("Missing SpacetimeDBEventBridge!");

    if (FindObjectOfType<OrbVisualizationManager>() == null)
        Debug.LogError("Missing OrbVisualizationManager!");

    // Bridge must persist across scenes
    DontDestroyOnLoad(gameObject);
}
```

---

## 4.5 Debug System Patterns

### Centralized Logging (SystemDebug)
```csharp
// ✅ CORRECT: Use SystemDebug for all logging
SystemDebug.Log(SystemDebug.Category.OrbSystem, "Loading orbs");
SystemDebug.LogWarning(SystemDebug.Category.EventBus, "Event blocked");

// ❌ INCORRECT: Direct Debug.Log calls
Debug.Log("Loading orbs"); // ❌ Not filterable
UnityEngine.Debug.LogWarning("Event blocked"); // ❌ Always visible
```

### Category-Based Control
```csharp
// Runtime control via DebugController component
public class DebugController : MonoBehaviour
{
    [SerializeField] private bool orbSystem = false;
    [SerializeField] private bool orbVisualization = false;
    [SerializeField] private bool eventBus = false;
    // ... 12 total categories

    void OnValidate()
    {
        // Updates SystemDebug categories in real-time
        ApplySettings();
    }
}
```

### Debug Categories
```csharp
[System.Flags]
public enum Category
{
    Connection = 1 << 0,        // SpacetimeDB connection
    EventBus = 1 << 1,         // Event publishing
    OrbSystem = 1 << 2,        // Orb database events
    OrbVisualization = 1 << 11, // Orb GameObjects (separate!)
    PlayerSystem = 1 << 3,      // Player events
    WorldSystem = 1 << 4,       // World loading
    Mining = 1 << 5,           // Mining mechanics
    Session = 1 << 6,          // Login/logout
    Subscription = 1 << 7,      // Table subscriptions
    Reducer = 1 << 8,          // Reducer calls
    Network = 1 << 9,          // Network sync
    Performance = 1 << 10       // Performance metrics
}
```

---

## 4.6 Common Pitfalls & Solutions

### Database Pitfalls

#### 1. No Async/Await in Reducers
```rust
// ❌ WRONG
#[spacetimedb::reducer]
pub async fn bad_reducer(ctx: &ReducerContext) -> Result<(), String>

// ✅ CORRECT - Synchronous only
#[spacetimedb::reducer]
pub fn good_reducer(ctx: &ReducerContext) -> Result<(), String>
```

#### 2. Partial Moves
```rust
// ❌ WRONG - Partial move of String field
let player = Player {
    name: logged_out.name,  // String moved
};
ctx.db.logged_out_player().delete(logged_out); // Error

// ✅ CORRECT - Clone the field
let player = Player {
    name: logged_out.name.clone(),
};
```

### Client Pitfalls

#### 1. Null Reference on Tables
```csharp
// ❌ WRONG - No null check
var player = conn.Db.Player.Identity.Find(identity);
player.Name = "New";  // Might be null!

// ✅ CORRECT - Always check
var player = conn.Db.Player.Identity.Find(identity);
if (player != null)
{
    // Safe to use
}
```

#### 2. Memory Leaks from Events
```csharp
// ❌ WRONG - Not unsubscribing
void Start()
{
    conn.Db.Player.OnInsert += HandleInsert;
}
// Memory leak - handler never removed

// ✅ CORRECT - Unsubscribe in OnDestroy
void OnDestroy()
{
    if (conn != null)
        conn.Db.Player.OnInsert -= HandleInsert;
}
```

#### 3. Immediate Data Assumption
```csharp
// ❌ WRONG - Data not ready immediately
conn = DbConnection.Builder().Build();
var players = conn.Db.Player.Iter(); // Empty!

// ✅ CORRECT - Wait for sync
bool dataReady = false;
conn.OnConnect += (connection, identity, token) => {
    StartCoroutine(WaitForInitialSync());
};

IEnumerator WaitForInitialSync()
{
    yield return new WaitForSeconds(0.5f);
    dataReady = true;
}
```

---

## 4.5 Testing Strategies

### Local Testing Setup
```bash
# Start local SpacetimeDB
spacetime start

# Generate code
spacetime generate --lang=rust --out-dir=src/autogen
spacetime generate --lang=csharp --out-dir=Assets/Scripts/autogen

# Publish module
spacetime publish my_module --clear-database

# Watch logs
spacetime logs my_module -f
```

### Debug Logging

#### Rust Side
```rust
log::info!("=== REDUCER START ===");
log::debug!("Player: {:?}, Orb: {}", player, orb_id);
log::warn!("Unexpected state");
log::error!("Critical: {}", error);
```

#### Unity Side
```csharp
#if UNITY_EDITOR
Debug.Log($"[MINING] Started on orb {orbId}");
Debug.LogWarning($"[NETWORK] High latency: {ping}ms");
Debug.LogError($"[ERROR] Failed to connect: {error}");
#endif
```

### Performance Testing
```csharp
public class PerformanceMonitor : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(MonitorPerformance());
    }
    
    IEnumerator MonitorPerformance()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            
            var orbCount = conn.Db.WavePacketOrb.Count();
            var playerCount = conn.Db.Player.Count();
            var fps = 1f / Time.deltaTime;
            
            Debug.Log($"[PERF] FPS:{fps:F1} Orbs:{orbCount} Players:{playerCount}");
        }
    }
}
```

### Mock Testing
```csharp
#if UNITY_EDITOR
public static class MockData
{
    public static WavePacketOrb CreateMockOrb(ulong id)
    {
        return new WavePacketOrb
        {
            OrbId = id,
            Position = Random.insideUnitSphere * 100f,
            Frequency = (FrequencyBand)Random.Range(0, 6),
            PacketsRemaining = 100
        };
    }
}
#endif
```

---

## 4.6 Visual System Patterns

### Shader Development (URP)

#### ✅ CORRECT URP Shader Structure
```hlsl
Shader "SYSTEM/MyShader"
{
    Properties { }

    SubShader
    {
        Tags {
            "RenderPipeline"="UniversalPipeline"
            "LightMode"="UniversalForward"  // Critical for URP
        }

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Use URP transformation functions
            VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = posInputs.positionCS;
            ENDHLSL
        }
    }
}
```

#### ❌ INCORRECT Patterns
```hlsl
// Wrong: Reserved keywords
float line = min(grid.x, grid.y);  // ❌ "line" is reserved
float gridLine = min(grid.x, grid.y);  // ✅ Use different name

// Wrong: Missing float literals
smoothstep(0, width, value);  // ❌ HLSL needs decimals
smoothstep(0.0, width, value);  // ✅ Explicit floats
```

### Component-Based Visual Systems

#### ✅ CORRECT Visual Component Pattern
```csharp
public class WavePacketVisualizer : MonoBehaviour
{
    [Header("Visual Components")]
    [SerializeField] private GameObject prefab;
    [SerializeField] private Material material;

    [Header("Performance")]
    [SerializeField] private bool useObjectPooling = true;
    private Queue<GameObject> pool = new Queue<GameObject>();

    void Start()
    {
        if (useObjectPooling)
            InitializePool();
    }

    GameObject GetVisualObject()
    {
        return useObjectPooling && pool.Count > 0 ?
            pool.Dequeue() :
            Instantiate(prefab);
    }
}
```

#### Object Pooling Pattern
```csharp
// ✅ CORRECT: Pre-create and reuse
private void InitializePool()
{
    for (int i = 0; i < poolSize; i++)
    {
        var obj = Instantiate(prefab);
        obj.SetActive(false);
        pool.Enqueue(obj);
    }
}

// ❌ WRONG: Instantiate/Destroy every time
void CreateEffect()
{
    var effect = Instantiate(prefab);  // ❌ GC pressure
    Destroy(effect, 2f);
}
```

### Prefab System Patterns

#### ✅ CORRECT Prefab-Based World System
```csharp
public class WorldController : MonoBehaviour
{
    [Header("Prefab System")]
    public GameObject worldSpherePrefab;  // Assign in Inspector

    void Start()
    {
        if (worldSpherePrefab == null)
        {
            Debug.LogError("No prefab assigned!");
            return;
        }

        // Create from prefab
        var world = Instantiate(worldSpherePrefab, transform);
        ApplyWorldScale();
    }
}
```

#### WebGL-Specific Patterns
```csharp
// ✅ CORRECT: Runtime platform detection
if (Application.platform == RuntimePlatform.WebGLPlayer)
{
    // WebGL-specific code
    StartCoroutine(LoadConfigAsync());
}

// ❌ WRONG: Compiler directives for connection logic
#if UNITY_WEBGL
    serverUrl = "cloud.server.com";  // ❌ Won't work in builds
#endif
```

### Animation Patterns

#### ✅ CORRECT Animation Update
```csharp
void Update()
{
    // Rotation animation
    transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

    // Pulse animation
    float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
    transform.localScale = baseScale * (1f + pulse);
}
```

#### Material Property Animation
```csharp
// ✅ CORRECT: Cache property IDs
private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

void UpdateMaterial()
{
    material.SetColor(EmissionColorID, color * intensity);
}

// ❌ WRONG: String lookups every frame
void Update()
{
    material.SetColor("_EmissionColor", color);  // ❌ Slow
}
```

### LOD and Performance Patterns

#### Distance-Based LOD
```csharp
void UpdateLOD()
{
    float distance = Vector3.Distance(transform.position, Camera.main.transform.position);

    if (distance < 50f)
        SetHighDetail();
    else if (distance < 100f)
        SetMediumDetail();
    else
        SetLowDetail();
}
```

#### Culling Pattern
```csharp
void OnBecameInvisible()
{
    // Disable expensive effects
    particleSystem.Stop();
    enabled = false;
}

void OnBecameVisible()
{
    // Re-enable when visible
    particleSystem.Play();
    enabled = true;
}
```