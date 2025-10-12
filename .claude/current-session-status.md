# Current Session Status

**Date:** 2025-10-12
**Status:** ✅ RESOLVED - Tab Key Cursor Unlock Fix
**Priority:** MEDIUM → COMPLETE

---

## Previous Session (Archived)

**Date:** 2025-01-06
**Status:** ✅ RESOLVED - Mining Packet Freeze Fixed
**Priority:** HIGH → COMPLETE

## Latest Session: Tab Key Cursor Unlock (2025-10-12)

### Problem (RESOLVED)
**Issue:** Camera continued moving after pressing Tab to unlock cursor for UI interaction

**Symptom:**
- User presses Tab to unlock cursor and interact with UI
- Cursor becomes visible and unlocked
- Moving mouse still rotates camera and changes pitch
- Both `enableMouseLook` and `inputEnabled` showed as `true` even after being set to `false`

### Solution Implemented
**Root Cause:** Script execution order timing - `CursorController.Start()` ran before `PlayerController` spawned dynamically

**Fix Applied:**
1. **Lazy-find pattern** in `CursorController.UnlockCursor()` - checks for null and re-finds PlayerController
2. **Dual-flag input gating** - Added `inputEnabled` flag alongside `enableMouseLook`
3. **Input callback gating** - `OnLook()` now checks `inputEnabled` before processing input
4. **OnEnable() protection** - Prevents Unity lifecycle from re-enabling disabled input actions

**Files Modified:**
- `CursorController.cs` - Added lazy-find pattern and improved error handling
- `PlayerController.cs` - Added `inputEnabled` flag, modified `OnLook()`, `HandleMouseInput()`, `OnEnable()`, `SetInputEnabled()`
- `CLAUDE.md` - Updated documentation with input system architecture and troubleshooting

**Documentation Updates:**
- Added "Input System and Cursor Control" section to CLAUDE.md
- Added troubleshooting entry for cursor unlock issues
- Added detailed "Recent Improvements" entry explaining the fix
- Noted future improvement: Consider event-based pattern using GameEventBus

---

## Previous Session: Mining Packet Freeze (2025-01-06)

### Problem (RESOLVED)

**Issue:** Frame freeze/stutter when mining packets are created in WorldScene (but NOT in TestScene)

**Symptom:**
- User reports: "when the mining mesh is created at the orb the entire game freezes for a fraction of a second"
- Happens every time a new mining packet is extracted (every 2 seconds during active mining)
- Does NOT happen in TestScene when spawning test orbs

## Investigation Progress

### What We've Done

1. **Ruled Out: Shader Compilation**
   - Created `ShaderPrewarmer.cs` to pre-compile shaders at scene start
   - Still stutters on every packet creation
   - **Conclusion:** Not shader compilation

2. **Added Performance Profiling**
   - Added `System.Diagnostics.Stopwatch` timing to:
     - `WavePacketMeshGenerator.cs` - Mesh generation breakdown
     - `WavePacketDisplay.cs` - Initialization and refresh
     - `WavePacketVisual.cs` - Component setup and reflection

3. **Timing Results (from user's screenshot)**
   ```
   [WavePacketVisual] Awake: 0ms | Load: 0ms | Create: 0ms | Reflection: 0ms
   [WavePacketDisplay] Awake: 0ms | InitializeVisual: 0ms
   [WavePacketDisplay] CreateMaterial: 0ms
   [MeshGen] Total: 0ms | Vertices: 0ms | Triangles: 0ms | SetData: 0ms | Resolution: 32
   [WavePacketDisplay] RefreshVisualization total: 1ms
   ```

4. **Key Insight: Timing Shows 0ms But Freeze Still Happens**
   - All C# code executes in <1ms
   - **Freeze must be happening OUTSIDE timed code**
   - Most likely: Unity's GPU mesh upload in `targetMeshFilter.mesh = mesh`

5. **Latest Update: Added Mesh Assignment Timing**
   - Split `RefreshVisualization()` timing into:
     - `MeshGen` - C# mesh generation
     - `MeshAssign` - `targetMeshFilter.mesh = mesh` (GPU upload)

6. **✅ SOLUTION FOUND AND IMPLEMENTED**
   - **Root Cause:** Fallback loading logic in `WavePacketVisual.Awake()` was loading `WavePacketSettings_Default` (High/128) instead of using prefab's assigned `WavePacketSettings_Mining` (Low/32)
   - **Fix:** Removed fallback loading logic - prefab assignments are correct
   - **Result:** Stutter completely eliminated! Mesh generation now <1ms
   - **File Modified:** `Assets/Scripts/Game/WavePacketVisual.cs` (lines 48-59)

## Technical Details

### Settings Configuration
- Mining packet uses `WavePacketSettings_Mining` (assigned in prefab)
- Quality set to **Low** (32x32 grid = 2,178 vertices)
- No Resources.Load() happening (settings pre-assigned)

### Mesh Generation Stats
- **Low Quality:** 32x32 grid
  - Vertices: (33)² × 2 faces = 2,178 vertices
  - Triangles: 32² × 2 × 2 = 4,096 triangles
- C# generation time: **0ms** (very fast)

### Execution Flow
```
Mining extraction →
ExtractionVisualController.SpawnFlyingPacket() →
Instantiate(extractedPacketPrefab) →
WavePacketVisual.Awake() → [0ms]
  └→ WavePacketDisplay.Awake() → [0ms]
      └→ InitializeVisual() → CreateMaterial() [0ms]
      └→ RefreshVisualization()
          ├→ WavePacketMeshGenerator.GenerateWavePacketMesh() [0ms]
          └→ targetMeshFilter.mesh = mesh [??? FREEZE HERE ???]
```

## Hypothesis

**Primary Hypothesis:** GPU mesh upload bottleneck
- `targetMeshFilter.mesh = mesh` triggers Unity internal operations:
  1. Mesh validation
  2. GPU buffer creation
  3. Vertex/index upload to GPU
  4. Mesh bounds calculation
  5. Rendering pipeline updates
- These operations block the main thread

**Why TestScene doesn't freeze:**
- TestScene spawns orb during scene initialization (low activity)
- WorldScene spawns during active gameplay (player moving, camera rendering, physics running)
- Same operation, different perceived impact

## Next Steps

### Immediate (Awaiting User Test)
1. **Test new timing code** - Check if `MeshAssign` shows the freeze duration
2. **Confirm bottleneck** - If MeshAssign is 50-100ms, we've found it

### Solution Options (Once Confirmed)

#### Option 1: Mesh Caching (Recommended)
Since mining packets of the same frequency/composition always look identical:
```csharp
// Create WavePacketMeshCache.cs (static cache)
public static Mesh GetOrCreateMesh(WavePacketSample[] samples, WavePacketSettings settings)
{
    string key = GenerateKey(samples, settings);
    if (meshCache.TryGetValue(key, out Mesh cached))
        return cached; // Reuse existing mesh

    Mesh newMesh = GenerateWavePacketMesh(samples, settings);
    meshCache[key] = newMesh;
    return newMesh;
}
```
- First packet: Generate + upload (freeze once)
- All future packets: Reuse cached mesh (no freeze)

#### Option 2: Async Mesh Upload (Unity 2022+)
```csharp
mesh.UploadMeshData(false); // Keep mesh data in memory for async upload
```

#### Option 3: Simpler Mesh for Mining Packets
- Use ultra-low res mesh for flying packets (16x16 or 8x8)
- Only use high-res for static orbs

#### Option 4: Object Pooling
- Pre-create packet GameObjects at scene start
- Reuse from pool instead of Instantiate()

## Files Modified (This Session)

### Performance Profiling Code Added
1. **WavePacketMeshGenerator.cs**
   - Added `using System.Diagnostics`
   - Added timers for: Total, Vertices, Triangles, SetData
   - Log format: `[MeshGen] Total: Xms | Vertices: Xms | Triangles: Xms | SetData: Xms`

2. **WavePacketDisplay.cs**
   - Added `using System.Diagnostics`
   - Added timers for: Awake, InitializeVisual, CreateMaterial, RefreshVisualization
   - Added MeshGen vs MeshAssign split timing
   - Log format: `[WavePacketDisplay] RefreshVisualization | Total: Xms | MeshGen: Xms | MeshAssign: Xms`

3. **WavePacketVisual.cs**
   - Added `using System.Diagnostics`
   - Added timers for: Awake, Load, Create, Reflection
   - Log format: `[WavePacketVisual] Awake: Xms | Load: Xms | Create: Xms | Reflection: Xms`

4. **ShaderPrewarmer.cs** (Created - may not be needed)
   - Location: `Assets/Scripts/Debug/ShaderPrewarmer.cs`
   - Pre-warms shaders at scene start to avoid runtime compilation
   - Can be disabled if not the issue

## Related Files

### Core System Files
- `ExtractionVisualController.cs` - Spawns flying packets
- `WavePacketMeshGenerator.cs` - Generates mesh geometry
- `WavePacketDisplay.cs` - Displays mesh on GameObject
- `WavePacketVisual.cs` - High-level visual component

### Settings
- `WavePacketSettings_Mining.asset` - Mining packet settings (Low quality, 32x32)

### Prefabs
- `Assets/Prefabs/WavePacket/Mining.prefab` - Mining packet prefab

## User Constraints

1. **Don't override settings** - "don't put in code overrides for the settings, that makes the settings pointless"
2. **Quality already set to Low** - "i have set the quality to low already for the mining mesh generation"
3. **TestScene works fine** - "i don't see a similar spike in the test scene"
4. **Analyze before changes** - "lets analyze the code a bit more before you make changes"

## Session Continuation Point

**When you start the next session:**

1. **First:** Ask user for updated timing logs showing MeshAssign timing
2. **If MeshAssign shows freeze (e.g., 50ms+):**
   - Confirm GPU upload is the bottleneck
   - Implement mesh caching solution
3. **If MeshAssign is still 0ms:**
   - Freeze is happening even later (possibly in Unity's rendering pipeline)
   - May need Unity Profiler data
   - Consider using Unity's built-in Profiler API

## Debug Commands for Testing

```bash
# Clear all orbs
spacetime sql system "DELETE FROM wave_packet_orb"

# Spawn test orb with green frequency (2.094 radians)
spacetime call system spawn_test_orb 0.0 310.0 0.0

# Check mining status
spacetime call system debug_mining_status

# Check wave packet distribution
spacetime call system debug_wave_packet_status
```

## Relevant Documentation
- See `CLAUDE.md` - Architecture overview
- See `.claude/debug-commands-reference.md` - Server debug commands
- See project instructions for frequency system (radians, 0-2π range)
