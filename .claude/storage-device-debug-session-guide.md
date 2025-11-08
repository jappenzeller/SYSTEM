# Storage Device Placement - Live Debug Session Guide

**Date:** 2025-10-26
**Feature:** Storage device placement with R key

## Prerequisites Checklist

### Unity Scene Setup
- [ ] WorldScene.unity is open
- [ ] `StorageDeviceManager` component added to scene hierarchy
- [ ] `SpacetimeDBEventBridge` is present and active
- [ ] `DebugController` has `StorageSystem` category ENABLED
- [ ] Player prefab has `StorageDevicePlacement` component attached
- [ ] Unity Console is visible and clear

### Server Setup
- [ ] SpacetimeDB server is running (`spacetime start` or cloud instance)
- [ ] Server logs are visible (terminal window)
- [ ] Enhanced reducer code is deployed (see below for manual update)
- [ ] Player is logged in and in WorldScene

### Server Code Update (Manual)
The enhanced debug logging needs to be manually copied into `lib.rs`:

**Location:** `SYSTEM-server/src/lib.rs` around line 3634

**Replace the `create_storage_device` function with:**
See: `SYSTEM-server/src/create_storage_device_enhanced.rs` for the full enhanced version

**Key Changes:**
- Box drawing characters for visual separation
- Step-by-step logging (Step 1, Step 2, Step 3, Step 4)
- ✅/❌ emoji indicators for success/failure
- Detailed parameter logging
- Player identity and world coordinate logging

---

## Debug Output Flow

### Expected Flow (Success Path)

#### 1. **CLIENT: Player Presses E Key**
```
[StorageSystem] ========== R KEY PRESSED ==========
[StorageSystem] Player position: (x, y, z), Forward: (fx, fy, fz)
[StorageSystem] Calculated placement position: (px, py, pz)
[StorageSystem] ✓ Position valid - proceeding with placement
[StorageSystem] 🔧 Calling server reducer: CreateStorageDevice
[StorageSystem]    Device Name: 'Storage Device #1'
[StorageSystem]    Position: (px, py, pz)
[StorageSystem] ✅ Reducer call sent to server - waiting for response...
```

#### 2. **SERVER: Reducer Execution**
```
════════════════════════════════════════════════════════════
CREATE_STORAGE_DEVICE REDUCER CALLED
════════════════════════════════════════════════════════════
Parameters:
  Position: (px, py, pz)
  Device Name: 'Storage Device #1'
  Sender Identity: Identity([...])
Step 1: Finding player by identity...
  ✅ Player found: ID=123, Name='YourPlayerName'
  Player World: (0,0,0)
Step 2: Checking device limit (max 10 per player)...
  Current device count: 0/10
Step 3: Creating StorageDevice struct...
Step 4: Inserting into storage_device table...
════════════════════════════════════════════════════════════
✅ STORAGE DEVICE CREATED SUCCESSFULLY
════════════════════════════════════════════════════════════
Device ID: 1
Device Name: 'Storage Device #1'
Owner: YourPlayerName (player_id=123)
Position: (px, py, pz)
World: (0,0,0)
Total devices for player: 1
════════════════════════════════════════════════════════════
```

#### 3. **CLIENT: SpacetimeDB Event → Visualization**
```
[StorageSystem] [StorageDeviceManager] Device inserted: 1 - Storage Device #1
[StorageSystem] [StorageDeviceManager] Creating device visualization for orb 1
[StorageSystem] [StorageDeviceManager] No prefab assigned, using primitive cube
[StorageSystem] [StorageDeviceManager] Created device visualization for 1 at (px, py, pz)
[StorageSystem] [StorageDeviceManager] Device 1: 0/6000 packets (0%) - Color: RGBA(...)
```

#### 4. **SCENE: Device Appears**
- Cube GameObject appears 5 units in front of player
- Name: `StorageDevice_1_Storage Device #1`
- Scale: 3 units
- Color: Dark blue (empty)
- Has SphereCollider (radius 0.5, trigger=true)

---

## Error Scenarios & Expected Output

### Error 1: Not Connected to Server
```
[StorageSystem] ========== R KEY PRESSED ==========
[StorageSystem] ❌ Cannot place device - not connected to server
```
**Fix:** Check GameManager connection status

### Error 2: Too Close to Another Object
```
[StorageSystem] ========== R KEY PRESSED ==========
[StorageSystem] Player position: (x, y, z), Forward: (fx, fy, fz)
[StorageSystem] Calculated placement position: (px, py, pz)
[StorageSystem] Found 2 objects within 1 units:
[StorageSystem]   - StorageDevice_1_Storage Device #1 at distance 0.50
[StorageSystem]   - Orb_123 at distance 0.80
[StorageSystem] ❌ PLACEMENT BLOCKED - too close to another object (min distance: 1 unit)
```
**Fix:** Move player to a different location

### Error 3: Player Not Found (Server)
```
════════════════════════════════════════════════════════════
CREATE_STORAGE_DEVICE REDUCER CALLED
════════════════════════════════════════════════════════════
...
Step 1: Finding player by identity...
  ❌ Player not found for identity Identity([...])
```
**Fix:** Ensure player is logged in and session exists

### Error 4: Device Limit Reached (Server)
```
...
Step 2: Checking device limit (max 10 per player)...
  Current device count: 10/10
  ❌ Device limit reached! Player 123 already has 10 devices
```
**Fix:** Delete existing devices or use different player

### Error 5: GameManager.Conn is Null
```
[StorageSystem] ========== R KEY PRESSED ==========
...
[StorageSystem] 🔧 Calling server reducer: CreateStorageDevice
[StorageSystem] ❌ GameManager.Conn is NULL!
```
**Fix:** Check GameManager initialization and connection state

### Error 6: No E Key Response
**Symptoms:** Nothing happens when pressing E

**Debug Steps:**
1. Check console for `[StorageDevicePlacement] Input system configured - press R to place device`
   - If missing → Component not initialized
2. Check `[StorageDevicePlacement] Attached to local player - placement enabled`
   - If says "remote player" → Component on wrong player instance
3. Check if component is on player at all:
   ```
   [StorageDevicePlacement] No PlayerController found on this GameObject
   ```
   - If this → Component on wrong GameObject

### Error 7: StorageDeviceManager Not Loading Devices
**Symptoms:** Server logs show device created, but nothing appears in scene

**Debug Steps:**
1. Check GameEventBus subscription:
   ```
   [StorageDeviceManager] Subscribed to GameEventBus device events
   ```
2. Check for event handling:
   ```
   [StorageDeviceManager] Device inserted: 1 - Storage Device #1
   ```
3. If event received but no GameObject → Check prefab assignment or material creation

---

## Verification SQL Queries

### Check All Storage Devices
```bash
spacetime sql system "SELECT * FROM storage_device"
```

### Check Devices for Specific Player
```bash
spacetime sql system "SELECT device_id, device_name, position, owner_player_id FROM storage_device WHERE owner_player_id = YOUR_PLAYER_ID"
```

### Count Devices Per Player
```bash
spacetime sql system "SELECT owner_player_id, COUNT(*) as device_count FROM storage_device GROUP BY owner_player_id"
```

### Delete All Devices (Reset)
```bash
spacetime sql system "DELETE FROM storage_device"
```

---

## Manual Testing Checklist

### Test 1: Basic Placement
- [ ] Press R key
- [ ] See "========== R KEY PRESSED ==========" in Unity console
- [ ] See server reducer called in server logs
- [ ] See "✅ STORAGE DEVICE CREATED SUCCESSFULLY" in server logs
- [ ] See device GameObject appear in scene
- [ ] Device appears ~5 units in front of player
- [ ] Device is named "StorageDevice_1_Storage Device #1"

### Test 2: Proximity Blocking
- [ ] Place first device successfully
- [ ] Move player < 1 unit away
- [ ] Press E again
- [ ] See "Found N objects within 1 units:" message
- [ ] See "❌ PLACEMENT BLOCKED" message
- [ ] No new device created

### Test 3: Proximity Success After Moving
- [ ] After Test 2, move player > 1 unit away
- [ ] Press E
- [ ] Device #2 created successfully
- [ ] Both devices visible in scene

### Test 4: Multiple Devices
- [ ] Place 5 devices at different locations
- [ ] Verify all have unique IDs
- [ ] Verify names increment (Device #1, #2, #3, #4, #5)
- [ ] SQL query shows 5 devices

### Test 5: Logout/Login Persistence
- [ ] Place 2-3 devices
- [ ] Note their positions
- [ ] Logout (return to Login scene)
- [ ] Login again
- [ ] Devices reappear at same positions
- [ ] StorageDeviceManager loads from database

### Test 6: World Transition
- [ ] Place device in world (0,0,0)
- [ ] Transition to different world (if multi-world supported)
- [ ] Device should NOT appear in new world
- [ ] Return to world (0,0,0)
- [ ] Device reappears

---

## Common Issues & Fixes

| Issue | Symptom | Fix |
|-------|---------|-----|
| No debug output | Nothing in console | Enable StorageSystem category in DebugController |
| R key does nothing | No "R KEY PRESSED" log | Check PlayerInputActions enabled, component on player |
| Reducer not called | Client logs but no server logs | Check GameManager.IsConnected(), verify Conn.Reducers exists |
| Device created but invisible | Server success but no GameObject | Add StorageDeviceManager to WorldScene |
| All placements blocked | Always sees "PLACEMENT BLOCKED" | Check Physics layers, verify collider radii |
| Multiple devices at same spot | Proximity check not working | Verify colliders added to visualizations |

---

## Debug Session Steps

### Step-by-Step Live Debug

1. **Preparation (Before Starting Unity)**
   ```bash
   # Terminal 1: Start server with logging
   cd SYSTEM-server
   spacetime start

   # Terminal 2: Watch server logs
   spacetime logs system --follow
   ```

2. **Unity Setup**
   - Open WorldScene
   - Verify StorageDeviceManager in hierarchy
   - Enable StorageSystem debug category
   - Clear console (Ctrl+Shift+C)

3. **Play Mode**
   - Start game
   - Login with player account
   - Enter WorldScene
   - Look for initialization logs:
     ```
     [StorageDevicePlacement] Attached to local player - placement enabled
     [StorageDevicePlacement] Input system configured - press R to place device
     [StorageDeviceManager] Subscribed to GameEventBus device events
     ```

4. **First Placement Test**
   - Position player in open area
   - **YOU SAY:** "Pressing R now..."
   - Press R
   - **I'LL SAY:** Read logs aloud and diagnose issues

5. **Iterative Testing**
   - For each test, announce action
   - Review both client AND server logs together
   - Verify visual result in scene
   - Check SQL database state

6. **Issue Resolution**
   - If something fails, we'll trace through:
     1. Input handler (OnInteractPressed)
     2. Position calculation
     3. Proximity check
     4. Reducer call
     5. Server execution
     6. Database insert
     7. Event propagation
     8. Visualization creation

---

## Ready for Live Debug?

**When you're ready, tell me:**
1. "Server is running and I can see logs"
2. "Unity is in Play mode"
3. "I'm in WorldScene as the local player"
4. "Console is clear and StorageSystem is enabled"
5. "I have StorageDeviceManager in the scene" (or need help adding it)

**Then we'll start with:** "Pressing R in 3... 2... 1..."

And I'll guide you through diagnosing every log line! 🚀
