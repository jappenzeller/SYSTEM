# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 🟢 CURRENT SESSION STATUS

**Last Completed:** WebGL Deployment & Energy Spire Implementation
**Status:** ✅ COMPLETE - Test environment deployed with 26 spires
**Date:** 2025-10-18

📋 **See:** `.claude/current-session-status.md` for detailed session documentation
📊 **See:** `.claude/documentation-sync-2025-10-18.md` for synchronization analysis

**Previous Sessions:**
- Bloch sphere coordinate system standardization (2025-10-12) - ✅ COMPLETE
- Tab key cursor unlock fix (2025-10-12) - ✅ RESOLVED

---

## Project Overview

SYSTEM is a multiplayer wave packet mining game built with Unity and SpacetimeDB. Players explore persistent worlds, extracting energy from quantum orbs using frequency-matched crystals. The project uses a client-server architecture with Unity for the frontend and Rust/SpacetimeDB for the authoritative backend.

## Essential Commands

### Unified Deployment System
```bash
# Deploy to test environment
./Scripts/deploy-spacetimedb.ps1 -Environment test

# Deploy to production with verification
./Scripts/deploy-spacetimedb.ps1 -Environment production -Verify

# Deploy with database reset (WARNING: deletes all data)
./Scripts/deploy-spacetimedb.ps1 -Environment test -DeleteData -Yes

# Deploy with cache invalidation
./Scripts/deploy-spacetimedb.ps1 -Environment production -InvalidateCache

# Deploy for WebGL with build config
./Scripts/deploy-spacetimedb.ps1 -Environment test -BuildConfig -InvalidateCache

# CI/CD deployment (non-interactive)
./Scripts/deploy-spacetimedb.ps1 -Environment production -Yes -Verify

# Unix/Linux/macOS deployment
./Scripts/deploy-spacetimedb.sh --environment test --verify
```

### Deployment Options
- `-Environment [local|test|production]` - Target environment
- `-DeleteData` - Complete database wipe
- `-InvalidateCache` - Clear CloudFront cache
- `-PublishOnly` - Deploy module without data operations
- `-Verify` - Run post-deployment verification
- `-BuildConfig` - Generate build-config.json for WebGL
- `-SkipBuild` - Skip Rust compilation
- `-Yes` - Non-interactive mode

### Server Development
```bash
# Start SpacetimeDB server (from SYSTEM-server/)
spacetime start

# Build and deploy server module (from SYSTEM-server/)
./rebuild.ps1  # Builds, generates bindings, and deploys to local

# Alternative rebuild steps:
spacetime delete system --server local
cargo build --release
spacetime generate --lang cs --out-dir ../SYSTEM-client-3d/Assets/scripts/autogen
spacetime publish --server local system

# Test server functions - Debug Commands
spacetime call system spawn_test_orb 0.0 310.0 0.0  # Create orb at north pole
spacetime call system debug_mining_status            # Check mining status
spacetime call system debug_wave_packet_status       # Check packet distribution
spacetime sql system "DELETE FROM wave_packet_orb"   # Clear all orbs

# For complete debug commands, see .claude/debug-commands-reference.md
```

### Unity Development
```bash
# Unity version: 2022.3 LTS or later
# Open project from SYSTEM-client-3d folder

# Unity Editor Deployment Menu:
# SYSTEM → Deploy → Deploy to Local
# SYSTEM → Deploy → Deploy to Test
# SYSTEM → Deploy → Deploy to Production
# SYSTEM → Deploy → Verify Current Deployment

# WebGL deployment with S3 (example)
# 1. Build in Unity: Build → Build Test WebGL
# 2. Deploy server: ./Scripts/deploy-spacetimedb.ps1 -Environment test -BuildConfig
# 3. Upload to S3: aws s3 sync ./SYSTEM-client-3d/Build/Test s3://your-bucket/
```

### Unity Build Menu
The project includes automated build scripts accessible from Unity's menu bar:
- **Build → Build Local WebGL** - Builds for local development (127.0.0.1:3000)
- **Build → Build Test WebGL** - Builds for test environment (SpacetimeDB cloud test)
- **Build → Build Production WebGL** - Builds for production (SpacetimeDB cloud)
- Same options available for Windows builds

## Architecture Overview

### Client-Server Communication Flow
1. **Unity Client** → Calls reducer functions → **SpacetimeDB Server**
2. **Server** → Validates, updates state → Broadcasts to subscribed clients
3. **Clients** → Receive table updates via subscriptions → Update visual state

### Key Architectural Patterns

#### Event System (GameEventBus)
The project uses a custom EventBus with state machine for decoupled communication:
- `GameEventBus.Instance.Publish<EventType>(eventData)` - Publishes events with state validation
- `GameEventBus.Instance.Subscribe<EventType>(handler)` - Subscribes to event types
- Events bridge SpacetimeDB callbacks to Unity systems

**State Machine Flow**:
```
Disconnected → Connecting → Connected → CheckingPlayer → WaitingForLogin/PlayerReady → LoadingWorld → InGame
```

**Key States**:
- `Disconnected` - No connection to server
- `Connecting` - Attempting to connect
- `Connected` - Connected but not subscribed
- `CheckingPlayer` - Checking for existing player
- `WaitingForLogin` - No player found, waiting for login
- `Authenticating` - Login in progress
- `Authenticated` - Logged in, checking for player
- `CreatingPlayer` - Creating new player
- `PlayerReady` - Player exists and ready
- `LoadingWorld` - Loading world data
- `InGame` - Fully loaded and playing

**Important**: When adding new event types, they must be added to `allowedEventsPerState` in GameEventBus.cs for the appropriate states. Events published in wrong states will be rejected with warning: "Event X not allowed in state Y"

#### Debug System (SystemDebug)
Centralized debug logging with category-based filtering:
```csharp
// Usage
SystemDebug.Log(SystemDebug.Category.OrbSystem, "Message");
SystemDebug.LogWarning(SystemDebug.Category.Connection, "Warning");
SystemDebug.LogError(SystemDebug.Category.EventBus, "Error");

// Categories (controlled via DebugController component in Unity):
- Connection         // SpacetimeDB connection events
- EventBus          // Event publishing/subscription
- OrbSystem         // Orb database events and loading
- OrbVisualization  // Orb GameObject creation and rendering
- PlayerSystem      // Player events and tracking
- WorldSystem       // World loading and transitions
- Mining            // Mining system events
- Session           // Login/logout/session management
- Subscription      // Table subscriptions
- Reducer           // Reducer calls and responses
- Network           // Network traffic and sync
- Performance       // Performance metrics
```

#### SpacetimeDB Integration
```csharp
// Reducers are called like this
conn.Reducer.StartMining(orbId);

// Table iterations (no LINQ support)
foreach (var player in conn.Db.Player.Iter())
{
    // Process player
}

// Event handlers receive direct arguments
private void OnStartMining(ReducerEventContext ctx, ulong orbId)
{
    // Handle mining started
}
```

#### Singleton Pattern
- `GameManager.Instance` - Connection management
- `GameData.Instance` - Persistent player data
- `WorldManager.Instance` - World state management
- Always check `Instance != null` before use

## Critical Project Structure

```
SYSTEM/
├── SYSTEM-server/
│   └── src/lib.rs                       # All server logic, reducers, tables
├── SYSTEM-client-3d/
│   ├── Assets/
│   │   ├── Scripts/
│   │   │   ├── Game/
│   │   │   │   ├── GameManager.cs       # SpacetimeDB connection
│   │   │   │   ├── GameData.cs          # Persistent player data
│   │   │   │   ├── WorldManager.cs      # World loading/spawning
│   │   │   │   ├── WorldController.cs # Main world sphere controller (prefab-based)
│   │   │   │   ├── PrefabWorldController.cs # Standalone prefab world controller
│   │   │   │   ├── WorldPrefabManager.cs # ScriptableObject for world prefabs
│   │   │   │   ├── OrbVisualizationManager.cs # Orb GameObject creation and rendering
│   │   │   │   └── ProceduralSphereGenerator.cs # [DEPRECATED] Old procedural generation
│   │   │   ├── Debug/
│   │   │   │   ├── SystemDebug.cs       # Centralized debug logging system
│   │   │   │   ├── DebugController.cs   # Unity component for debug categories
│   │   │   │   ├── WebGLDebugOverlay.cs # Debug overlay for WebGL builds
│   │   │   │   ├── WorldCollisionTester.cs # Collision testing utility
│   │   │   │   └── CameraDebugger.cs    # Camera debugging utility
│   │   │   ├── Core/
│   │   │   │   ├── GameEventBus.cs      # Event system with state machine
│   │   │   │   └── SpacetimeDBEventBridge.cs # SpacetimeDB → EventBus bridge
│   │   │   ├── WavePacketMiningSystem.cs # Mining mechanics
│   │   │   ├── BuildSettings.cs         # ScriptableObject for environment configs
│   │   │   ├── WorldSpawnSystem.cs      # Unified spawn system for all world types
│   │   │   └── autogen/
│   │   │       └── SpacetimeDBClient.g.cs # Auto-generated from server
│   │   └── Editor/
│   │       ├── BuildScript.cs           # Automated build system
│   │       └── WorldPrefabSetupEditor.cs # Editor tools for prefab creation
│   └── Build/                           # Build outputs
│       ├── Local/                       # Local development builds
│       ├── Test/                        # Test environment builds
│       └── Production/                  # Production builds
└── Scripts/
    ├── deploy-spacetimedb.ps1          # Windows unified deployment script
    ├── deploy-spacetimedb.sh           # Unix/Linux/macOS deployment script
    ├── DeploymentConfig.cs              # Unity deployment configuration
    └── post-deploy-verify.sql          # SQL verification queries
```

## Common Development Tasks

### Setting Up World Spheres (Prefab System)
1. Quick setup: Menu → `SYSTEM → World Setup → Quick Create Default World Prefab`
2. In CenterWorld GameObject, assign the created prefab to `worldSpherePrefab` field
3. The system automatically handles scaling, materials, and collision
4. For multiple world types, create a `WorldPrefabManager` asset and configure variants

### Adding New Server Functionality
1. Add tables/reducers in `SYSTEM-server/src/lib.rs`
2. Run `./rebuild.ps1` to regenerate bindings
3. Update `SpacetimeDBEventBridge.cs` to handle new events
4. Create/update Unity components to respond to events

### Debugging Connection Issues
- Check SpacetimeDB is running: `spacetime status`
- Verify connection URL in Unity Login scene (default: `localhost:3000`)
- Monitor Unity console for connection events
- Check `GameManager.IsConnected()` status

### Testing Mining Mechanics
1. Create player account in Unity
2. Select crystal color (R/G/B)
3. Approach orb within 30 units
4. Toggle mining with input action
5. Watch wave packets travel and get captured

## Important Technical Details

### Orb System Architecture
The orb visualization system uses a clean event-driven architecture:

1. **SpacetimeDBEventBridge** (Core/)
   - ONLY component that interacts with SpacetimeDB tables
   - Subscribes to WavePacketOrb table events (OnInsert, OnUpdate, OnDelete)
   - Publishes GameEventBus events for other systems to consume
   - Must be in scene with `DontDestroyOnLoad` enabled

2. **OrbVisualizationManager** (Game/)
   - Subscribes to GameEventBus orb events
   - Creates/updates/destroys orb GameObjects based on events
   - Never directly accesses SpacetimeDB
   - Must be in scene to visualize orbs

3. **Event Flow**:
   ```
   SpacetimeDB → SpacetimeDBEventBridge → GameEventBus → OrbVisualizationManager
   ```

4. **Key Events**:
   - `InitialOrbsLoadedEvent` - Bulk load existing orbs when entering world
   - `OrbInsertedEvent` - New orb created
   - `OrbUpdatedEvent` - Orb properties changed
   - `OrbDeletedEvent` - Orb removed

### Wave Packet System
- 6 base frequencies: Red(0), Yellow(1/6), Green(1/3), Cyan(1/2), Blue(2/3), Magenta(5/6)
- Mining range: 30 units maximum
- Extraction rate: 1 packet per 2 seconds
- Travel speed: 5 units/second
- Server validates all captures

### SpacetimeDB Patterns to Remember
- **No LINQ on tables** - Use `Iter()` and manual iteration
- **No Status property on reducers** - Check database state for validation
- **Event handlers get direct arguments** - Not wrapped in event objects
- **Server is authoritative** - All game logic validated server-side

### Unity-Specific Considerations
- Uses Universal Render Pipeline (URP)
- Input System package for controls
- UI Toolkit for login interface
- Cinemachine for camera control
- All network state through SpacetimeDB

### Input System and Cursor Control
- **CursorController** (Debug/) - Handles Tab key to toggle cursor lock for UI interaction
- **PlayerController** - Processes mouse input for camera and character control
- **Known Issue**: Script execution order can cause timing issues with dynamic player spawning
  - CursorController uses **lazy finding** pattern - finds PlayerController on-demand if null
  - This handles cases where player spawns after CursorController.Start()
  - **Future Improvement**: Consider migrating to event-based pattern using GameEventBus for better decoupling
- **Input Flags**:
  - `enableMouseLook` - High-level flag for mouse look (can be toggled by CursorController)
  - `inputEnabled` - Low-level flag that gates Input System callbacks (prevents stale input)
  - Both flags must be true for camera movement to occur

### Player Control System (Minecraft-Style Third-Person)
- **Movement**: WASD keys for character movement relative to facing direction
- **Camera Control**: Mouse for character rotation and camera pitch
  - Mouse horizontal (X) → Rotates character left/right around sphere surface normal
  - Mouse vertical (Y) → Tilts camera up/down (pitch only, character stays level)
- **Input Configuration**: Uses Unity Input System with PlayerInputActions
  - Move action: WASD as 2D Vector composite binding
  - Look action: Mouse delta for camera control
- **Camera System**: Third-person orbital camera that follows behind character
  - Camera stays ~6 units behind, ~2.5 units above character
  - Smooth following with configurable lag (smoothTime)
  - Vertical pitch control independent of character orientation
  - Respects spherical world geometry

### Sensitivity Tuning (Updated December 2024)
- **Base Sensitivity**: `mouseSensitivity = 0.5f`, `verticalSensitivity = 0.2f`
- **Rotation Multipliers**: Additional multipliers in HandleMouseRotation()
  - `horizontalMultiplier = 2.0f` (responsive horizontal rotation)
  - `verticalMultiplier = 1.0f` (responsive vertical pitch)
- **Effective Rotation Speed**: ~60-120°/second horizontal at typical mouse speeds
- **Tuning Guide**: Adjust base sensitivity first (0.1-1.0), then fine-tune multipliers

## Recent Architecture Changes

### Control System Implementation (December 2024)
- **Minecraft-Style Third-Person Controls**: Simplified implementation
  - Mouse X directly rotates character around sphere normal
  - Mouse Y controls camera orbital pitch
  - WASD movement relative to character facing
  - Camera follows behind with fast, responsive smoothing
- **Simplified Rotation System**:
  - Clean RotateAround() implementation for spherical worlds
  - LateUpdate enforces local rotation when syncRotationFromServer = false
  - Removed complex protection systems in favor of simple flag-based control
  - Increased rotation multipliers from 0.05f → 2.0f for proper responsiveness
- **Network Sync Control**:
  - Simple flag: `syncRotationFromServer = false` for local player
  - Server updates skip rotation for local player
  - Other players always sync from server
- **Camera System (No Cinemachine)**:
  - Direct camera control without Cinemachine overhead
  - Fast exponential smoothing for responsive following
  - Orbital camera with pitch control
  - Clean separation of concerns
- **Camera Manager Updates**:
  - Implemented orbital third-person camera
  - Smooth following with Vector3.SmoothDamp
  - Collision detection for camera obstruction
  - SetCameraPitch for vertical look control
- **PlayerController Optimization**:
  - Separated mouse input handling for rotation and pitch
  - Character rotation via RotateAround() on sphere surface normal
  - Movement calculations relative to character forward/right vectors
  - Transform change tracking for debugging rotation issues

### Player Event System and WebGL Fixes
- **PlayerTracker**: New dedicated system for player data tracking and spatial queries
  - Handles player join/leave events with proper event firing
  - Provides proximity detection and spatial queries
  - Uses coroutine-based delayed initialization for WebGL compatibility
  - Separates data tracking from GameObject management (handled by WorldManager)
- **WebGL Initialization Timing**: Added delayed initialization patterns for singletons
  - GameEventBus, GameData, and GameManager all have WebGL-specific initialization checks
  - PlayerTracker and WorldManager use coroutines to wait for dependencies
- **BuildConfiguration Async Loading Fix**: Critical fix for WebGL NullReferenceException
  - Initialize `_config` with `new BuildConfigData()` to prevent null access
  - WebGL loads config asynchronously via UnityWebRequest
  - Added wait time in GameManager for config to load in WebGL builds

### Login and Scene Transition Flow (CRITICAL)
- **Registration**: HandleRegister() now calls RegisterAccount reducer and auto-logs in after creation
- **Username Storage**: GameData.Username must be set BEFORE session creation for proper event handling
- **State Flow**: `Connected` → `CheckingPlayer` → `WaitingForLogin`/`PlayerReady` → `LoadingWorld` → `InGame`
- **Scene Transition**: LoginUIController publishes `WorldLoadStartedEvent` and `WorldLoadedEvent` when player is ready
- **Fixed**: WorldLoadedEvent is now allowed in PlayerReady state (was blocking scene transitions)
- **WebGL Connection**: Uses runtime platform detection (`Application.platform == RuntimePlatform.WebGLPlayer`)
  - WebGL → maincloud.spacetimedb.com/system-test
  - Editor → localhost:3000/system
  - Standalone → maincloud.spacetimedb.com/system

### State Machine Updates
- GameEventBus now allows transition from `InGame` back to `PlayerReady` state
- Added scene loading events (`SceneLoadStartedEvent`, `SceneLoadedEvent`, `SceneLoadCompletedEvent`) to `PlayerReady` state
- WorldManager automatically transitions to `InGame` state after loading world
- **Important**: WorldLoadedEvent is allowed in PlayerReady state to enable scene transitions
- All events now log when published for better debugging

### WorldManager Simplification
- Removed dependency on `PlayerSubscriptionController`
- Now directly queries SpacetimeDB for players using `GameManager.Conn.Db.Player.Iter()`
- Improved player spawning with better debug logging

### Build System Enhancements
- Added `BuildSettings` ScriptableObject for managing environments (Local, Test, Production)
- Automated build scripts with environment-specific configurations
- WebGL builds now have full exception support with stack traces
- **IMPORTANT**: Connection settings use runtime platform detection, NOT compiler directives

### Debugging Tools
- **WebGLDebugOverlay**: Shows real-time system state in WebGL builds
  - Minimal mode (F3 to toggle visibility, F4 to switch modes)
  - Shows: Connection status | Environment | Player name | Game state
  - Simplified from verbose debug output to essential information only
- **BuildConfigDebugger**: Debug tool for build configuration loading
  - Disabled by default (enable showDebugUI in inspector)
  - Shows loaded environment, server URL, and module name
- **CameraDebugger**: Helps diagnose camera and player tracking issues
- All debug components now use minimal logging to reduce console noise

## Development Guidelines

1. **Always search existing code before creating new components**
2. **Follow established event patterns (GameEventBus)**
3. **Keep server logic in Rust, client is visualization only**
4. **Test multiplayer scenarios with multiple Unity instances**
5. **Use object pooling for performance (already implemented for packets)**
6. **Never commit SpacetimeDB credentials or keys**
7. **Use BuildSettings for environment-specific configurations**
8. **Add debug components when troubleshooting WebGL builds**

## Troubleshooting

### Module Not Found
Run `./rebuild.ps1` from SYSTEM-server directory

### Unity Can't Connect
- Ensure SpacetimeDB is running locally
- Check firewall isn't blocking port 3000
- Verify connection string in Login scene

### Login/Registration Issues
- **Register button not working**: Check if state is stuck in `Connecting` (should be `WaitingForLogin`)
- **Scene not transitioning**: Verify WorldLoadedEvent is published after LocalPlayerReadyEvent
- **Username not set**: Ensure GameData.Username is set BEFORE session creation
- **State stuck**: Check GameEventBus logs for event validation failures

### Scene Transition Not Working
1. Check EventBus state progression in console logs
2. Verify `WorldLoadStartedEvent` and `WorldLoadedEvent` are published
3. Ensure state reaches `InGame` (SceneTransitionManager listens for this)
4. Check that `WorldLoadedEvent` is allowed in `PlayerReady` state

### Control System Issues
- **Mouse not rotating character**:
  - Check `PlayerController.mouseSensitivity` (default: 0.5)
  - Check rotation scale in `HandleMouseRotation()` (default: 0.05f for X, 0.02f for Y)
  - Verify `PlayerController.enableMouseLook` is true
  - Ensure cursor is locked (should be invisible during play)
  - Check if Rigidbody is blocking rotation (see logs for "[ROTATION] Using Rigidbody")
  - Network sync might be overriding rotation - check for "[NETWORK]" warnings in logs
- **Camera still moves after pressing Tab to unlock cursor**:
  - Verify CursorController found PlayerController (check console for "[CursorController] Found PlayerController")
  - Both `enableMouseLook` and `inputEnabled` must be false to stop camera movement
  - If PlayerController spawns dynamically, CursorController uses lazy-find pattern to locate it
  - Check that `OnLook()` callback is gating input with `inputEnabled` flag
- **Rotation too sensitive/fast**:
  - Reduce `mouseSensitivity` in Inspector (try 0.1-0.3)
  - In PlayerController.HandleMouseRotation(), adjust:
    - `rotationScaleX` from 0.05f → 0.02f or lower
    - `rotationScaleY` from 0.02f → 0.01f or lower
  - Remember: 1° per frame = 60°/second at 60fps
- **WASD not working**:
  - Verify PlayerInputActions is enabled in PlayerController
  - Check Input System package is installed
  - Movement is relative to character facing (not camera)
  - Regenerate PlayerInputActions.cs from the .inputactions asset if needed
- **Camera not following character properly**:
  - Check CameraManager has `useOrbitalCamera = true`
  - Verify CameraManager.Instance exists
  - Camera distance/height can be adjusted in CameraManager Inspector
  - Ensure camera target is set to local player
- **Character rotating around wrong axis**:
  - Verify sphere up vector calculation (should be `position.normalized`)
  - Check character's transform.up is aligned with sphere surface
  - Rotation should be around sphere normal, not world Y-axis

### WebGL Build Connection Issues
- WebGL builds should connect to `maincloud.spacetimedb.com/system-test`
- Uses runtime detection: `Application.platform == RuntimePlatform.WebGLPlayer`
- Do NOT use compiler directives (#if UNITY_WEBGL) for connection logic
- **NullReferenceException in WebGL**: BuildConfiguration loads asynchronously in WebGL. Always initialize `_config` with default value to prevent null access

### Orbs Not Appearing in Scene
1. **Check Required Components**:
   - Ensure `SpacetimeDBEventBridge` component is in scene
   - Ensure `OrbVisualizationManager` component is in scene
   - Both should have `DontDestroyOnLoad` if scene changes occur

2. **Check Debug Output** (Enable via DebugController):
   - Enable `OrbSystem` category - Should see database loading events
   - Enable `OrbVisualization` category - Should see GameObject creation
   - Enable `EventBus` category - Should see event publishing/handling

3. **Common Issues**:
   - **"Event not allowed in state"**: Add event type to `allowedEventsPerState` in GameEventBus.cs for appropriate GameState
   - **No orbs in database**: Check server has orbs in current world coordinates
   - **Events not delivered**: Verify GameEventBus state machine is in correct state (PlayerReady/LoadingWorld/InGame)
   - **Missing subscriptions**: Check OrbVisualizationManager OnEnable is subscribing to events

4. **Debug Flow**:
   ```
   [OrbSystem] Loading orbs for player's current world
   [OrbSystem] Found orb X at world (0,0,0)
   [OrbSystem] Publishing InitialOrbsLoadedEvent with N orbs
   [EventBus] Executing 1 handlers for InitialOrbsLoadedEvent
   [OrbVisualization] Loading N initial orbs
   [OrbVisualization] Creating orb visualization for orb X
   ```

### Mining Not Working
- Check crystal is equipped (GameData.Instance.SelectedCrystal)
- Verify distance to orb (max 30 units)
- Ensure orb has matching frequency packets
- Check server logs for validation errors

### Build Errors After Server Changes
Auto-generated code may be out of sync. Run `./rebuild.ps1` to regenerate bindings.

### WebGL Template Variables Not Replacing
**Symptom:** Literal `%UNITY_WEB_NAME%` appears in browser title or page instead of actual product name

**Cause:** Unity's WebGL build pipeline doesn't automatically process template variables in custom templates

**Solution:**
1. Verify `WebGLTemplatePostProcessor.cs` exists in `Assets/Editor/` folder
2. Check it implements `IPostprocessBuildWithReport` interface
3. Rebuild WebGL project completely (not just refresh)
4. Check Unity console for "[WebGLTemplatePostProcessor]" log messages
5. Template should include thumbnail.png and favicon.ico

**Files Involved:**
- `Assets/Editor/WebGLTemplatePostProcessor.cs` - Post-build variable replacement
- `Assets/WebGLTemplates/DarkTheme/index.html` - Template file

### Shader Null Reference in WebGL
**Symptom:** `NullReferenceException: Value cannot be null. Parameter name: shader`

**Common Causes:**
- `Shader.Find("Standard")` returns null in WebGL builds
- Material created without null check
- Shader not included in Always Included Shaders list
- URP shader name differences between platforms

**Solutions:**
1. **Use shader fallback chain:**
   ```csharp
   Shader shader = Shader.Find("Universal Render Pipeline/Lit");
   if (shader == null) shader = Shader.Find("Standard");
   if (shader == null) shader = Shader.Find("Unlit/Color");
   if (shader == null) { /* handle error */ }
   ```

2. **Add null checks before creating materials:**
   ```csharp
   if (shader != null) {
       Material mat = new Material(shader);
   }
   ```

3. **Add shader to Always Included Shaders:**
   - Edit → Project Settings → Graphics
   - Find "Always Included Shaders" list
   - Add your shader to the list

4. **Check property existence before setting:**
   ```csharp
   if (mat.HasProperty("_Metallic"))
       mat.SetFloat("_Metallic", value);
   ```

**Pattern:** See `EnergySpireManager.CreateSafeMaterial()` for complete implementation

### Energy Spires Not Appearing / Rendering Magenta
**Symptom:** Spires show as bright magenta or don't render at all in WebGL

**Causes:**
- Missing material/shader (magenta = Unity's missing material indicator)
- Shader not compatible with WebGL
- Material properties not supported by current shader
- Shader not in Always Included Shaders list

**Solutions:**
1. Verify safe material creation pattern is being used
2. Check shader fallback chain is working correctly
3. Enable `SystemDebug.Category.SpireVisualization` for detailed logs
4. Verify shader is in Always Included Shaders (Project Settings → Graphics)
5. Test shader in actual WebGL build (Editor doesn't catch all issues)

**Debug Steps:**
```csharp
// Enable spire visualization debugging
SystemDebug.Log(SystemDebug.Category.SpireVisualization, "Creating spire material");

// Check what shader was found
if (shader != null)
    Debug.Log($"Using shader: {shader.name}");
else
    Debug.LogError("Shader.Find returned null!");
```

### Development Console Showing in WebGL
**Symptom:** Purple "Development Console" panel visible in game showing errors/warnings

**Cause:** `BuildOptions.Development` flag enables Unity's on-screen development console overlay

**Solution:**
Add to build script before WebGL build:
```csharp
PlayerSettings.WebGL.showDiagnostics = false;
```

**File:** `Assets/Editor/BuildScript.cs` (line 118)

### Wave Packet Shader Missing in WebGL
**Symptom:** Wave packet mining visualization fails with shader not found error

**Causes:**
- Shader not included in Always Included Shaders list
- Missing WebGL-specific shader pragmas
- Shader compilation issues on WebGL platform

**Solutions:**
1. **Add shader to Always Included Shaders:**
   - ProjectSettings → GraphicsSettings.asset
   - Add shader GUID to always included list

2. **Add WebGL compatibility pragmas to shader:**
   ```hlsl
   #pragma target 3.0    // WebGL2 support
   #pragma glsl          // Explicit GLSL compilation
   ```

3. **Test in actual WebGL build** - Editor doesn't catch shader compilation issues

**Files:**
- `ProjectSettings/GraphicsSettings.asset` - Shader inclusion
- `Assets/Shaders/WavePacketDisc.shader` - Add WebGL pragmas

## Recent Improvements

### Prefab-Based World System (Replaces Procedural Generation)
- **NEW**: Transitioned from procedural mesh generation to prefab-based world spheres for WebGL compatibility
- `WorldController` now exclusively uses prefab system with automatic fallback
- `PrefabWorldController` provides standalone prefab-based world implementation
- `WorldPrefabManager` ScriptableObject for managing multiple world types and materials
- Editor tools in menu: `SYSTEM → World Setup` for easy prefab creation
- Benefits:
  - Full WebGL compatibility (no procedural mesh issues)
  - Faster initialization (no runtime mesh generation)
  - Visual preview in Unity Editor before runtime
  - Support for multiple world types with easy runtime switching
  - Better performance on mobile and web platforms

### Deprecated: Procedural Sphere Generation
- `ProceduralSphereGenerator` is now marked as `[Obsolete]` - use prefab system instead
- Kept for backward compatibility but should not be used in new code
- Test utilities still available in Editor menu for comparison: Tools → Test Procedural Sphere

### Player Disconnect Handling
- Server now automatically removes players when they disconnect using `__identity_disconnected__` reducer
- Players are moved to `LoggedOutPlayer` table for history tracking
- Fixed issue where players remained in world after closing browser/disconnecting

### Position Persistence System
- Player positions, rotations, and world locations are now saved when logging out
- `LoggedOutPlayer` table stores: `last_world`, `last_position`, `last_rotation`
- When players log back in, they spawn at their last saved position
- Client checks for saved position and uses it instead of default spawn point
- Debug logging tracks position save/restore throughout the flow
- Fallback to default spawn only for brand new players
- **Fixed**: PlayerController now properly sends position updates to server via `UpdatePlayerPosition` reducer
- Position updates sent every 0.1 seconds when player moves (was TODO, now implemented)
- **Fixed**: Console spam from position updates - removed redundant LocalPlayerChanged events on position updates
- Debug logging reduced to 1/100 updates (controlled by showDebugInfo flag)

### Namespace Conflict Resolution
- **Fixed**: Debug namespace conflicts between `SYSTEM.Debug` and `UnityEngine.Debug`
- All Debug.Log calls now use fully qualified `UnityEngine.Debug` to prevent compilation errors
- Affected files in `SYSTEM.Game` and `SYSTEM.Editor` namespaces now compile correctly
- WorldSpawnSystem updated to support both `WorldController` and `PrefabWorldController`

### Orb Visualization System (December 2024)
- **Implemented**: Event-driven architecture for orb visualization
- **Fixed**: Orbs not appearing due to GameEventBus state machine blocking events
- **Added**: `OrbVisualization` debug category separate from `OrbSystem`
- **Solution**: Added orb events to `allowedEventsPerState` for PlayerReady, LoadingWorld, and InGame states
- **Architecture**: SpacetimeDBEventBridge is the ONLY component that reads from database, publishes events for visualization
- **Required Components**: Both SpacetimeDBEventBridge and OrbVisualizationManager must be in scene

### Debug System Improvements (December 2024)
- **Implemented**: SystemDebug centralized logging with 12 categories
- **Added**: DebugController Unity component for runtime control of debug output
- **Fixed**: Compile errors from malformed debug comment syntax (PowerShell script issue)
- **Pattern**: All components now use `SystemDebug.Log(Category, message)` instead of direct Debug.Log
- **Categories**: Connection, EventBus, OrbSystem, OrbVisualization, PlayerSystem, WorldSystem, Mining, Session, Subscription, Reducer, Network, Performance

### Cursor Control and Input System Fix (October 2025)
- **Fixed**: Camera continued moving after pressing Tab to unlock cursor
- **Root Cause**: Script execution order timing - CursorController.Start() ran before PlayerController spawned
- **Solution**: Implemented lazy-find pattern in CursorController.UnlockCursor()
  - Checks for null PlayerController and re-finds it on-demand
  - Handles dynamic player spawning scenarios
- **Input Gating**: Added dual-flag system to prevent stale input
  - `enableMouseLook` - High-level toggle for mouse look functionality
  - `inputEnabled` - Low-level gate in Input System callbacks (OnLook, OnMove)
  - Both must be true for camera movement to occur
- **OnEnable() Fix**: Prevented automatic re-enabling of input actions when disabled
  - OnEnable() now checks `inputEnabled` flag before enabling input actions
  - Prevents Unity's lifecycle from overriding manual disable calls
- **Future Improvement**: Consider event-based pattern using GameEventBus instead of FindFirstObjectByType for better decoupling

### WebGL Template Variable System (October 2025)
- **Problem Solved**: Unity WebGL builds showed literal `%UNITY_WEB_NAME%` instead of product name
- **Solution**: Created `WebGLTemplatePostProcessor.cs` post-build processor
  - Automatically replaces all Unity template variables after build
  - Implements `IPostprocessBuildWithReport` interface
  - Runs after WebGL build completes, modifies index.html
- **Template Assets Added**:
  - `thumbnail.png` - Template preview in build folder
  - `favicon.ico` - Browser tab icon
- **Variables Replaced**: Product name, company name, Unity version, screen dimensions, loader URLs
- **Files**: `Assets/Editor/WebGLTemplatePostProcessor.cs`, template assets in `WebGLTemplates/DarkTheme/`
- **Commit**: `7a3e284`

### Shader WebGL Compatibility (October 2025)
- **Problem Solved**: Shaders not available in WebGL builds, causing visualization failures
- **Solutions Implemented**:
  1. **Always Included Shaders** - Added WavePacketDisc shader to GraphicsSettings
  2. **WebGL Pragmas** - Added `#pragma target 3.0` and `#pragma glsl` to shaders
  3. **Safe Material Creation** - Created `CreateSafeMaterial()` pattern with fallback chain
- **Shader Fallback Pattern**:
  ```csharp
  Shader shader = Shader.Find("Universal Render Pipeline/Lit");
  if (shader == null) shader = Shader.Find("Standard");
  if (shader == null) shader = Shader.Find("Unlit/Color");
  ```
- **Property Safety**: Use `HasProperty()` checks before setting material properties
- **Files**: `GraphicsSettings.asset`, `WavePacketDisc.shader`, `EnergySpireManager.cs`
- **Pattern**: See `EnergySpireManager.CreateSafeMaterial()` for complete implementation
- **Commit**: `7a3e284` and `e702d12`

### Energy Spire System (October 2025)
- **Implemented**: 26-spire Face-Centered Cubic (FCC) lattice structure around spherical worlds
- **Architecture**:
  - **6 Cardinal Spires** (R = 300) - Face centers on X/Y/Z axes
  - **12 Edge Spires** (R/√2 ≈ 212.13) - Edge midpoints on XY/YZ/XZ planes
  - **8 Vertex Spires** (R/√3 ≈ 173.21) - Cube corners
- **Components per Spire**:
  - **DistributionSphere** - Mid-level routing sphere (radius 40 units)
  - **QuantumTunnel** - Top-level colored ring with charge system (0-100%)
  - **WorldCircuit** - Ground-level emitter (optional, not currently used)
- **Color System**:
  - Cardinal (6): Green (Y-axis), Red (X-axis), Blue (Z-axis)
  - Edge (12): Yellow (XY plane), Cyan (YZ plane), Magenta (XZ plane)
  - Vertex (8): White (all corners)
- **Server Reducer**: `spawn_all_26_spires(world_x, world_y, world_z)`
- **Database Tables**: `distribution_sphere`, `quantum_tunnel`
- **WebGL Fix**: Safe material creation prevents shader null reference errors
- **Visualization**: Event-driven via `SpacetimeDBEventBridge` → `EnergySpireManager`
- **Files**: `SYSTEM-server/src/lib.rs`, `EnergySpireManager.cs`
- **Commit**: `7a3e284`, `e702d12`

### Development Console Removal (October 2025)
- **Problem**: Purple "Development Console" panel showing shader errors in WebGL builds
- **Cause**: `BuildOptions.Development` flag enabled on-screen console overlay
- **Solution**: Added `PlayerSettings.WebGL.showDiagnostics = false` to build script
- **File**: `Assets/Editor/BuildScript.cs` (line 118)
- **Benefit**: Clean game view in WebGL without intrusive debug console
- **Commit**: `7a3e284`

### World System Integration
- `WorldSpawnSystem` now automatically detects and works with either world controller type
- Unified world access methods for radius, center position, and surface calculations
- Dynamic world type switching at runtime via `WorldController.SwitchWorldType()`
- Runtime material and radius changes supported
- Full collision system compatibility with prefab-based worlds

### High-Resolution World Sphere Mesh (January 2025)
- **High-Res Icosphere Generator**: Editor tool to create optimized sphere meshes
  - Menu: `SYSTEM → Create High-Res Sphere Mesh`
  - LOD 0: 20,480 triangles (subdivision 5) - Close-up quality
  - LOD 1: 5,120 triangles (subdivision 4) - Recommended default ⭐
  - LOD 2: 1,280 triangles (subdivision 3) - Far distance
  - Custom option for specific requirements
- **Automatic Prefab Update**: Auto-generates meshes and updates world prefab on first load
- **Proper Scaling**: Fixed double-scaling issue (radius 1.0 mesh, not 0.5)
  - `WorldController` and `PrefabWorldController` scale directly by worldRadius
  - Removed old `* 2f` scaling factor for Unity's default sphere
- **Benefits**:
  - Perfectly smooth sphere appearance
  - Clean grid line rendering
  - Better visual quality for quantum state markers
  - Good performance on all platforms including WebGL

### Quantum Grid Shader (January 2025)
- **WorldSphereEnergy Shader**: Custom URP shader for quantum visualization
  - Pulsing base color with configurable speed and intensity
  - Thin grid lines using spherical coordinates (phi/theta)
  - 6 quantum state markers at key positions:
    - |0⟩ North pole (+Y), |1⟩ South pole (-Y)
    - |+⟩ / |-⟩ on X-axis equator
    - |+i⟩ / |-i⟩ on Z-axis equator (forward/backward)
  - Adjustable grid line width (default: 0.01 for ~1 unit wide lines)
  - Adjustable marker size (default: 0.03)
  - **Coordinate system**: Standard Bloch sphere with +Y as north pole (see `.claude/bloch-sphere-coordinates-reference.md`)
- **WebGL Compatibility**:
  - Uses proper URP transformation functions (`GetVertexPositionInputs`)
  - Single-pass rendering (URP only executes one `UniversalForward` pass)
  - No texture dependencies to avoid sampler type errors
- **Performance**: Minimal fragment shader complexity, runs smoothly on WebGL

### WebGL Build Fixes (January 2025)
- **Scale Correction**: Multiple layers of protection for tiny world issue
  - Forced scale in `WorldController.Awake()` for WebGL builds
  - Mesh-aware scaling based on actual mesh bounds
  - Post-instantiation scale forcing in `WorldManager`
  - World created as root object (no parent) to avoid scale inheritance
- **Transform Diagnostics**: Comprehensive logging for debugging
  - Position, scale, and hierarchy logging
  - Duplicate world detection and cleanup
  - Bounds visualization with red line (WebGL only)
  - Test cyan sphere for rendering verification
- **Debug UI Control**: `WebGLDebugOverlay` hidden in production
  - Only visible in Editor or Development builds
  - Toggle with F3 (hide/show), F4 (minimal/normal mode)
  - Shows: Connection | Environment | Player | State

### Shader Architecture
- **Single-Pass Design**: Combined base color and grid in one fragment shader
  - URP only executes one pass per object with `LightMode="UniversalForward"`
  - Grid and markers blended with base pulsing color
  - Uses `lerp()` for smooth color transitions
- **Proper URP Integration**:
  - Includes `Core.hlsl` for full URP functionality
  - Uses `GetVertexPositionInputs()` instead of manual matrix multiplication
  - Properties in `CBUFFER_START(UnityPerMaterial)` for SRP batching
  - Correct handling of transform matrices across all platforms