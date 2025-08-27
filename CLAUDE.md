# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SYSTEM is a multiplayer wave packet mining game built with Unity and SpacetimeDB. Players explore persistent worlds, extracting energy from quantum orbs using frequency-matched crystals. The project uses a client-server architecture with Unity for the frontend and Rust/SpacetimeDB for the authoritative backend.

## Essential Commands

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

# Build Unity WebGL (from project root)
./Deploy-UnityWebGL.ps1

# Deploy to AWS S3 (with CloudFront invalidation)
./Deploy-Complete.ps1 -InvalidateCache

# Setup test environment S3 bucket
./Setup-TestBucket.ps1
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
│   └── src/lib.rs                   # All server logic, reducers, tables
├── SYSTEM-client-3d/
│   ├── Assets/
│   │   ├── Scripts/
│   │   │   ├── GameManager.cs           # SpacetimeDB connection
│   │   │   ├── GameData.cs              # Persistent player data
│   │   │   ├── WorldManager.cs          # World loading/spawning (no longer uses PlayerSubscriptionController)
│   │   │   ├── WavePacketMiningSystem.cs # Mining mechanics
│   │   │   ├── GameEventBus.cs          # Event system with state machine
│   │   │   ├── SpacetimeDBEventBridge.cs # SpacetimeDB → EventBus
│   │   │   ├── BuildSettings.cs         # ScriptableObject for environment configs
│   │   │   ├── WebGLDebugOverlay.cs     # Debug overlay for WebGL builds
│   │   │   ├── CameraDebugger.cs        # Camera debugging utility
│   │   │   └── autogen/
│   │   │       └── SpacetimeDBClient.g.cs # Auto-generated from server
│   │   └── Editor/
│   │       └── BuildScript.cs           # Automated build system
│   └── WebBuild/                        # WebGL build output
├── Deploy-UnityWebGL.ps1                # Deploy to S3
├── Deploy-Complete.ps1                  # Deploy with CloudFront
└── Setup-TestBucket.ps1                 # Create test S3 bucket
```

## Common Development Tasks

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
- **CameraDebugger**: Helps diagnose camera and player tracking issues
- Both tools provide detailed logging without requiring console access

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

### Mining Not Working
- Check crystal is equipped (GameData.Instance.SelectedCrystal)
- Verify distance to orb (max 30 units)
- Ensure orb has matching frequency packets
- Check server logs for validation errors

### Build Errors After Server Changes
Auto-generated code may be out of sync. Run `./rebuild.ps1` to regenerate bindings.