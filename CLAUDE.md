# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

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

# Test server functions
spacetime call system debug_test_quanta_emission
spacetime call system debug_quanta_status
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

#### Event System
The project uses a custom EventBus for decoupled communication:
- `GameEventBus.Instance.Publish<EventType>(eventData)`
- `GameEventBus.Instance.Subscribe<EventType>(handler)`
- Events bridge SpacetimeDB callbacks to Unity systems

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
│   │   │   │   ├── CenterWorldController.cs # Main world sphere controller (prefab-based)
│   │   │   │   ├── PrefabWorldController.cs # Standalone prefab world controller
│   │   │   │   ├── WorldPrefabManager.cs # ScriptableObject for world prefabs
│   │   │   │   └── ProceduralSphereGenerator.cs # [DEPRECATED] Old procedural generation
│   │   │   ├── Debug/
│   │   │   │   ├── WebGLDebugOverlay.cs # Debug overlay for WebGL builds
│   │   │   │   ├── WorldCollisionTester.cs # Collision testing utility
│   │   │   │   └── CameraDebugger.cs    # Camera debugging utility
│   │   │   ├── WavePacketMiningSystem.cs # Mining mechanics
│   │   │   ├── GameEventBus.cs          # Event system with state machine
│   │   │   ├── SpacetimeDBEventBridge.cs # SpacetimeDB → EventBus
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

## Recent Architecture Changes

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

### WebGL Build Connection Issues
- WebGL builds should connect to `maincloud.spacetimedb.com/system-test`
- Uses runtime detection: `Application.platform == RuntimePlatform.WebGLPlayer`
- Do NOT use compiler directives (#if UNITY_WEBGL) for connection logic
- **NullReferenceException in WebGL**: BuildConfiguration loads asynchronously in WebGL. Always initialize `_config` with default value to prevent null access

### Mining Not Working
- Check crystal is equipped (GameData.Instance.SelectedCrystal)
- Verify distance to orb (max 30 units)
- Ensure orb has matching frequency packets
- Check server logs for validation errors

### Build Errors After Server Changes
Auto-generated code may be out of sync. Run `./rebuild.ps1` to regenerate bindings.

## Recent Improvements

### Prefab-Based World System (Replaces Procedural Generation)
- **NEW**: Transitioned from procedural mesh generation to prefab-based world spheres for WebGL compatibility
- `CenterWorldController` now exclusively uses prefab system with automatic fallback
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
- WorldSpawnSystem updated to support both `CenterWorldController` and `PrefabWorldController`

### World System Integration
- `WorldSpawnSystem` now automatically detects and works with either world controller type
- Unified world access methods for radius, center position, and surface calculations
- Dynamic world type switching at runtime via `CenterWorldController.SwitchWorldType()`
- Runtime material and radius changes supported
- Full collision system compatibility with prefab-based worlds