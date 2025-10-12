# Performance Investigation Log

## Issue: Mining Packet Creation Freeze

**Date Started:** 2025-01-06
**Status:** In Progress
**Severity:** High - Affects gameplay UX

---

## Timeline of Investigation

### Initial Report
**User Description:** "in the world scene the new mesh creation is very slow compared to the test scene. when the mining mesh is created at the orb the entire game freezes for a fraction of a second"

**Environment:**
- WorldScene: Freeze on every packet extraction
- TestScene: No freeze with same prefab
- Settings: WavePacketSettings_Mining (Low quality, 32x32 grid)

---

### Hypothesis 1: Shader Compilation ❌

**Theory:** First `new Material(shader)` causes GPU shader compilation freeze

**Test:**
- Created `ShaderPrewarmer.cs` to pre-compile shaders at scene start
- Instantiates dummy packet off-screen, waits 2 frames, destroys it

**Result:** Still freezes on every packet creation

**Conclusion:** NOT shader compilation (shader compiles once and is cached)

---

### Hypothesis 2: Mesh Generation CPU Time ❌

**Theory:** Mesh generation (vertices, triangles, Gaussian calculations) is slow

**Test:** Added Stopwatch timing to:
```csharp
// WavePacketMeshGenerator.cs
[MeshGen] Total: Xms | Vertices: Xms | Triangles: Xms | SetData: Xms | Resolution: 32

// WavePacketDisplay.cs
[WavePacketDisplay] Awake: Xms | InitializeVisual: Xms
[WavePacketDisplay] CreateMaterial: Xms
[WavePacketDisplay] RefreshVisualization total: Xms

// WavePacketVisual.cs
[WavePacketVisual] Awake: Xms | Load: Xms | Create: Xms | Reflection: Xms
```

**Result (from user logs):**
```
[WavePacketVisual] Awake: 0ms | Load: 0ms | Create: 0ms | Reflection: 0ms
[WavePacketDisplay] Awake: 0ms | InitializeVisual: 0ms
[MeshGen] Total: 0ms | Vertices: 0ms | Triangles: 0ms | SetData: 0ms | Resolution: 32
[WavePacketDisplay] RefreshVisualization total: 1ms
```

**Conclusion:** Mesh generation is FAST (<1ms). Not the bottleneck.

---

### Hypothesis 3: GPU Mesh Upload (Current) 🔍

**Theory:** `targetMeshFilter.mesh = mesh` triggers slow GPU upload

**Reasoning:**
- All C# code runs in 0-1ms
- Freeze still happens
- Must be happening in Unity internal operations AFTER our code

**Unity Mesh Assignment Operations:**
1. Mesh validation
2. GPU buffer allocation
3. Vertex/index data upload to GPU
4. Mesh bounds recalculation
5. Rendering pipeline updates
6. Scene batching updates

**Test:** Added split timing for mesh assignment:
```csharp
Stopwatch meshGenTimer = Stopwatch.StartNew();
Mesh mesh = WavePacketMeshGenerator.GenerateWavePacketMesh(...);
meshGenTimer.Stop();

Stopwatch meshAssignTimer = Stopwatch.StartNew();
targetMeshFilter.mesh = mesh;
meshAssignTimer.Stop();

// Log: MeshGen: Xms | MeshAssign: Xms
```

**Status:** Awaiting user test results

**Expected:** MeshAssign will show 50-100ms (the freeze duration)

---

## Technical Analysis

### Mesh Statistics
```
Low Quality (32x32):
- Vertices: (33)² × 2 = 2,178
- Triangles: 32² × 2 × 2 = 4,096
- Colors: 2,178 (per-vertex)
- Normals: 2,178 (per-vertex)
- Total data: ~90KB per mesh
```

### Why TestScene vs WorldScene Difference?

**TestScene:**
- Spawns orb during scene initialization
- Low activity: No player movement, minimal rendering
- GPU not under load
- Same 50ms operation feels instant

**WorldScene:**
- Spawns during active gameplay
- High activity: Player moving, camera rendering, physics
- GPU already under load
- Same 50ms operation is very noticeable

**Analogy:** Taking a breath while resting vs. while running

---

## Solution Options (Pending Confirmation)

### Option 1: Mesh Caching ⭐ RECOMMENDED
**Strategy:** Cache meshes by composition, reuse identical meshes

**Implementation:**
```csharp
// WavePacketMeshCache.cs
private static Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

public static Mesh GetOrCreateMesh(WavePacketSample[] samples, WavePacketSettings settings)
{
    string key = GenerateKey(samples, settings);

    if (meshCache.TryGetValue(key, out Mesh cached))
        return cached; // Instant, no GPU upload

    Mesh newMesh = WavePacketMeshGenerator.GenerateWavePacketMesh(samples, settings);
    meshCache[key] = newMesh;
    return newMesh; // First time freeze, then cached
}

private static string GenerateKey(WavePacketSample[] samples, WavePacketSettings settings)
{
    // Key: resolution_frequency1xcount1_frequency2xcount2
    // Example: "32_2.094x1" for Low quality, 1 green packet
}
```

**Benefits:**
- First packet of each type: One-time freeze
- All future identical packets: Zero freeze (reuse cached mesh)
- No settings override (respects user's constraint)
- Mining packets are repetitive (same frequency), perfect for caching

**Example:**
```
Extract green packet #1: Generate + upload → 50ms freeze
Extract green packet #2: Reuse cached mesh → 0ms
Extract green packet #3: Reuse cached mesh → 0ms
...
```

---

### Option 2: Reduce Mesh Complexity
**Strategy:** Use simpler mesh for flying packets

**Current:** 32x32 = 2,178 vertices
**Proposed:** 16x16 = 578 vertices (75% reduction)
**Or:** 8x8 = 162 vertices (93% reduction)

**Trade-off:**
- Less visual quality for flying packets
- Faster GPU upload
- Keep high-res for static orbs

---

### Option 3: Async Mesh Upload (Unity 2022+)
**Strategy:** Use `Mesh.UploadMeshData(false)` to keep mesh readable

```csharp
mesh.UploadMeshData(false); // Marks for async GPU upload
targetMeshFilter.mesh = mesh; // Won't block as much
```

**Caveat:** Still may block if GPU is busy

---

### Option 4: Object Pooling
**Strategy:** Pre-create packet objects at scene start

```csharp
// WorldManager.Start()
for (int i = 0; i < 10; i++) {
    GameObject packet = Instantiate(miningPacketPrefab);
    packet.SetActive(false);
    packetPool.Add(packet);
}

// When extracting
GameObject packet = packetPool.GetInactive();
packet.SetActive(true);
// Position and configure...
```

**Benefits:**
- Mesh uploaded during scene load
- Extraction just activates existing object
- No runtime instantiation cost

**Drawback:**
- Pre-allocation memory cost
- Complex pool management

---

## Code Changes Made

### Files Modified with Timing
1. `WavePacketMeshGenerator.cs` - Lines 1-140
   - Added `using System.Diagnostics`
   - Timers around vertex loops, triangle loops, mesh.Set*() calls

2. `WavePacketDisplay.cs` - Lines 1-195
   - Added `using System.Diagnostics`
   - Timers in Awake(), InitializeVisual(), CreateMaterial(), RefreshVisualization()
   - Split MeshGen vs MeshAssign timing

3. `WavePacketVisual.cs` - Lines 1-145
   - Added `using System.Diagnostics`
   - Timers for Load, Create, Reflection operations

### Files Created
1. `ShaderPrewarmer.cs` (May not be needed)
   - Location: `Assets/Scripts/Debug/ShaderPrewarmer.cs`
   - Pre-warms shaders to rule out compilation

---

## Next Session Action Items

### 1. Get Updated Timing Data
Ask user: "Can you test again and share the new logs showing MeshAssign timing?"

### 2. If MeshAssign Shows Freeze (50ms+)
✅ Confirms GPU upload bottleneck
→ Implement mesh caching (Option 1)

### 3. If MeshAssign Still 0ms
❓ Freeze is even later in pipeline
→ Need Unity Profiler data
→ Consider Unity Profiler API markers:
```csharp
Profiling.Profiler.BeginSample("MeshAssign");
targetMeshFilter.mesh = mesh;
Profiling.Profiler.EndSample();
```

### 4. Implementation Path (Mesh Caching)
1. Create `WavePacketMeshCache.cs`
2. Update `WavePacketDisplay.RefreshVisualization()` to use cache
3. Test: First extraction should freeze, subsequent should not
4. Add cache clearing on scene unload

---

## References
- User screenshot: Console showing all 0ms timings
- Resolution: 32x32 (Low quality from WavePacketSettings_Mining)
- Frequency system: Radians (0-2π), Green = 2.094
- No Resources.Load() - settings assigned in prefab
