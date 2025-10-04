# Current System Status & Next Steps
**Date:** 2025-10-03
**Last Updated:** October 3, 2025

## Current Working Systems

### ✅ Fully Functional
- **SpacetimeDB Connection**: Local and cloud deployment working
- **Player System**: Login, registration, position persistence
- **World System**: Prefab-based world spheres with proper scaling
- **Player Movement**: Minecraft-style third-person controls on spherical worlds
- **Orb System**: Event-driven visualization architecture
  - SpacetimeDBEventBridge → GameEventBus → OrbVisualizationManager
  - Orbs spawn and render correctly
  - Database events properly propagated
- **Build System**: Automated builds for Local/Test/Production
- **Deployment**: Unified PowerShell script with all environments
- **Debug System**: SystemDebug with category-based filtering

### ⚠️ Partially Working
- **Wave Packet Mining System**:
  - ✅ Mining sessions start/stop
  - ✅ Wave packet extraction from orbs
  - ✅ Visual prefab loaded from Resources (ConcentricRingsPrefab)
  - ✅ **Selective mining IMPLEMENTED** - Crystal type filtering working (Oct 3, 2025)
  - ❌ Wave packet visuals need testing

## ✅ FIXED: Selective Mining (October 3, 2025)

### Problem (RESOLVED)
The `MiningSession` table (used by `start_mining_v2`) was missing the `crystal_type` field:

```rust
// CURRENT - Missing crystal_type!
pub struct MiningSession {
    pub session_id: u64,
    pub player_identity: Identity,
    pub orb_id: u64,
    pub circuit_id: u64,
    pub started_at: u64,
    pub last_extraction: u64,
    pub extraction_multiplier: f32,
    pub total_extracted: u32,
    pub is_active: bool,
}
```

The old `MiningSessionLegacy` has it, but v2 doesn't:
```rust
// OLD SYSTEM - Has crystal_type
pub struct MiningSessionLegacy {
    pub player_id: u64,
    pub orb_id: u64,
    pub crystal_type: CrystalType,  // <-- This!
    pub started_at: u64,
    pub last_packet_time: u64,
    pub pending_wave_packets: Vec<PendingWavePacket>,
}
```

### Impact
- Players can extract ALL wave packets regardless of crystal color
- Red crystal should only extract: Red, Yellow, Magenta
- Green crystal should only extract: Green, Yellow, Cyan
- Blue crystal should only extract: Blue, Cyan, Magenta
- Currently: No filtering happens

### Solution Implemented ✅
1. ✅ Added `crystal_type: CrystalType` to `MiningSession` table (line 330 in lib.rs)
2. ✅ Updated `start_mining_v2` reducer to accept crystal type parameter (line 2273)
3. ✅ Added extraction logic to filter by crystal type (lines 2398-2410 in lib.rs)
4. ✅ Updated client to pass crystal type when calling `StartMiningV2` (line 294 in WavePacketMiningSystem.cs)
5. ✅ Rebuilt server and regenerated client bindings
6. ✅ Published to local server (database reset required for schema change)

## Wave Packet Visual System

### Current Setup
- **Prefab**: `ConcentricRingsPrefab.prefab` copied to `Resources/WavePacketVisual.prefab`
- **Loading**: `WavePacketMiningSystem.Awake()` loads from Resources
- **Issue**: Warning was "No wave packet visual prefab found" - now fixed

### Status
- Prefab should now load correctly
- Need to test in-game to verify visuals appear
- May need to tune visual appearance

## Recent Architecture Changes

### Event-Driven Orb System (January 2025)
```
SpacetimeDB → SpacetimeDBEventBridge → GameEventBus → OrbVisualizationManager
```
- Clean separation of concerns
- Only SpacetimeDBEventBridge reads database
- All other systems use events
- State machine prevents invalid event timing

### Resources Folder Pattern
- Runtime-added components can't have Inspector references
- Solution: Load prefabs from `Assets/Resources/` folder
- Pattern: `Resources.Load<GameObject>("PrefabName")`

### Build Configuration
- `BuildSettings` ScriptableObject for environment configs
- Runtime platform detection for WebGL
- Async config loading with null safety

## Next Steps (Priority Order)

### 1. ✅ COMPLETED: Fix Selective Mining
**Completed**: October 3, 2025
**Changes**:
- ✅ Added `crystal_type` field to `MiningSession` table in `lib.rs`
- ✅ Updated `start_mining_v2` reducer signature: `orb_id: u64, crystal_type: CrystalType`
- ✅ Added crystal filtering logic to extraction (lines 2398-2410)
- ✅ Updated client `StartMiningV2` call to pass `GameData.Instance.SelectedCrystal`
- ✅ Rebuilt and deployed to local server
- ⏳ Need to test with different crystals on mixed orbs

### 2. Test Wave Packet Visuals
**Why**: Verify fix worked
**Tasks**:
- [ ] Start game in Unity
- [ ] Select crystal color in UI
- [ ] Approach orb (within 30 units)
- [ ] Press E to mine
- [ ] Verify ConcentricRings visual appears and travels to player
- [ ] Tune visual appearance if needed

### 3. Verify Frequency Filtering
**Why**: Ensure only matching frequencies are extracted
**Tasks**:
- [ ] Create test orb with mixed frequencies: `spawn_test_orb X Y Z FREQ COUNT`
- [ ] Mine with red crystal - should only get R/Y/M packets
- [ ] Mine with green crystal - should only get G/Y/C packets
- [ ] Mine with blue crystal - should only get B/C/M packets
- [ ] Check server logs for "No matching packets" when orb has wrong frequencies

### 4. Clean Up Legacy Mining Code
**Why**: Remove deprecated dual system
**Tasks**:
- [ ] Remove `MiningSessionLegacy` struct and `MINING_STATE` once v2 proven working
- [ ] Remove `start_mining` (old reducer) once v2 has crystal type
- [ ] Clean up dual event handlers in `WavePacketMiningSystem.cs`
- [ ] Update documentation to remove references to old system

### 5. Polish Wave Packet Visuals
**Why**: Better player experience
**Tasks**:
- [ ] Review `ConcentricRingsPrefab` appearance
- [ ] Adjust colors based on frequency bands
- [ ] Tune animation speed/scale
- [ ] Add particle effects on capture
- [ ] Test performance with multiple simultaneous packets

## File References

### Server (Rust)
- **Main Logic**: [lib.rs](../SYSTEM-server/src/lib.rs)
  - Line 324: `MiningSession` table definition
  - Line 2269: `start_mining_v2` reducer
  - Line 1376-1384: Crystal filtering logic (in old system)

### Client (Unity)
- **Mining System**: [WavePacketMiningSystem.cs](../SYSTEM-client-3d/Assets/Scripts/WavePacketMiningSystem.cs)
  - Line 70: Awake - Resources.Load for prefab
  - Line 291: StartMiningV2 call (needs crystal param)
  - Line 651: Visual prefab warning (should be fixed now)

- **Prefabs**:
  - [Resources/WavePacketVisual.prefab](../SYSTEM-client-3d/Assets/Resources/WavePacketVisual.prefab)
  - [Prefabs/ConcentricRingsPrefab.prefab](../SYSTEM-client-3d/Assets/Prefabs/ConcentricRingsPrefab.prefab)

### Documentation
- [CLAUDE.md](../CLAUDE.md) - Main project guide
- [implementation-roadmap-doc.md](./implementation-roadmap-doc.md) - Sprint progress
- [mining-system-reconciliation.md](./mining-system-reconciliation.md) - Mining v1 vs v2

## Testing Commands

### Local Server Commands
```bash
# Start server
cd SYSTEM-server
spacetime start

# Rebuild and deploy
./rebuild.ps1

# Spawn test orbs
spacetime call system spawn_test_orb -- -10.0 300.0 -5.0 0 100  # Red orb
spacetime call system spawn_test_orb -- 10.0 300.0 5.0 2 100   # Green orb
spacetime call system spawn_test_orb -- 0.0 300.0 10.0 4 100   # Blue orb

# Check mining status
spacetime call system debug_mining_status
spacetime call system debug_wave_packet_status

# Query mining sessions
spacetime sql system "SELECT * FROM mining_session WHERE is_active = true"

# Check orb composition
spacetime sql system "SELECT orb_id, total_wave_packets FROM wave_packet_orb"
```

### Frequency Band Reference
- 0 = Red (0.0)
- 1 = Yellow (1/6)
- 2 = Green (1/3)
- 3 = Cyan (1/2)
- 4 = Blue (2/3)
- 5 = Magenta (5/6)

### Crystal Type Mapping
- **Red Crystal**: Extracts Red + Yellow + Magenta
- **Green Crystal**: Extracts Green + Yellow + Cyan
- **Blue Crystal**: Extracts Blue + Cyan + Magenta

## Known Issues

### High Priority
1. **Selective mining not working** - MiningSession missing crystal_type field
2. **Wave packet visuals untested** - Need to verify ConcentricRings appear

### Medium Priority
3. **Dual mining systems** - Legacy and v2 both exist, causing confusion
4. **No crystal selection UI** - Players can't easily switch crystals (GameData.SelectedCrystal)

### Low Priority
5. **Visual tuning** - Wave packet appearance may need polish
6. **Performance** - Object pooling for packets exists but untested at scale

## Design Decisions

### Why start_mining_v2?
- **Concurrent mining**: Multiple players can mine same orb
- **Database-backed**: Sessions survive server restart
- **Cleaner architecture**: No in-memory state
- **Problem**: Lost crystal_type in migration from v1

### Why Resources Folder?
- Components added via `AddComponent` at runtime
- Can't set Inspector references
- Resources.Load is standard Unity pattern for runtime loading
- Alternative would be ScriptableObject config

### Why Event-Driven Orb System?
- **Decoupling**: SpacetimeDB changes don't break visualization
- **State safety**: GameEventBus enforces valid state transitions
- **Debugging**: Clear event flow in logs
- **Flexibility**: Easy to add new visualizations

## Success Metrics

### When Is Mining "Done"?
- [ ] Selective mining works with all 3 crystal types
- [ ] Wave packets visually travel from orb to player
- [ ] Correct frequencies extracted based on crystal
- [ ] Multiple players can mine concurrently
- [ ] Server logs show proper filtering
- [ ] No console errors or warnings
- [ ] Performance stable with 5+ active miners

## Architecture Notes

### Mining Flow (Should Be)
1. Player selects crystal color in UI → `GameData.Instance.SelectedCrystal`
2. Player presses E near orb → `WavePacketMiningSystem.StartMining()`
3. Client calls `conn.Reducers.StartMiningV2(orbId, crystalType)` ⚠️ **Needs crystal param**
4. Server validates and creates `MiningSession` with crystal type ⚠️ **Needs field**
5. Server extracts packets matching crystal ⚠️ **Needs filter logic**
6. `WavePacketExtraction` inserted → `HandleWavePacketExtracted` on client
7. Visual packet spawns and flies to player ✅ **Should work now**
8. Packet reaches player → inventory updated

### Current vs Needed

**Current:**
```csharp
conn.Reducers.StartMiningV2(currentOrbId);  // No crystal!
```

**Needed:**
```csharp
var crystal = GameData.Instance.SelectedCrystal;
conn.Reducers.StartMiningV2(currentOrbId, crystal);
```

**Server Current:**
```rust
pub fn start_mining_v2(ctx: &ReducerContext, orb_id: u64)
```

**Server Needed:**
```rust
pub fn start_mining_v2(ctx: &ReducerContext, orb_id: u64, crystal_type: CrystalType)
```

---

**End of Status Document**
