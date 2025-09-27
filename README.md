# SYSTEM - Multiplayer Wave Packet Mining Game

> A 3D multiplayer exploration and mining game built with Unity and SpacetimeDB, featuring real-time synchronization and server-authoritative gameplay.

## 🎮 Overview

**SYSTEM** is a multiplayer game where players explore spherical worlds, mine quantum wave packets, and collaborate in a persistent universe. Players use frequency-matched crystals to extract energy from quantum orbs scattered across alien landscapes.

### Game Controls

| Input | Action |
|-------|--------|
| **WASD** | Move character forward/back/strafe (relative to camera facing) |
| **Mouse X** | Rotate character left/right around sphere normal |
| **Mouse Y** | Tilt camera up/down (pitch) |
| **Mouse Left** | Interact / Mine |
| **Tab** | Toggle inventory |
| **Escape** | Menu / Settings |

The game uses **Minecraft-style third-person controls** where:
- Mouse X rotates the character around the sphere's surface
- Mouse Y adjusts camera pitch for looking up/down
- Movement is relative to character facing direction
- Camera follows behind character at ~6 units distance

### Key Features

- **Real-time Multiplayer** - Server-authoritative gameplay with instant synchronization
- **Wave Packet Mining** - Unique frequency-based mining mechanics with 6 crystal types
- **Spherical Worlds** - Explore interconnected planets with gravity-based movement
- **Quantum Visualization** - Custom shader with pulsing grid and quantum state markers
- **High-Fidelity Graphics** - Smooth icosphere meshes with 5K+ triangles for perfect spheres
- **Cross-platform** - WebGL for browsers, standalone for Windows/Mac/Linux
- **Multi-environment** - Separate Local, Test, and Production environments

### Technical Stack

| Component | Technology | Purpose |
|-----------|------------|---------|
| **Client** | Unity 2022.3+ LTS | 3D game engine and rendering |
| **Backend** | SpacetimeDB | Real-time database and server logic |
| **Server Logic** | Rust | High-performance game state management |
| **Networking** | WebSocket | Low-latency client-server communication |
| **Build System** | Unity BuildScript | Automated multi-environment builds |

## 🚀 Getting Started

### Prerequisites

- **Unity** 2022.3 LTS or later ([Download](https://unity.com/download))
- **Rust** 1.70+ with cargo ([Install](https://rustup.rs/))
- **SpacetimeDB CLI** ([Install Guide](https://spacetimedb.com/docs/getting-started))
- **Git** for version control
- **PowerShell** (Windows) or Bash (Mac/Linux) for scripts

### Quick Setup

```bash
# 1. Clone the repository
git clone https://github.com/yourusername/SYSTEM.git
cd SYSTEM

# 2. Install SpacetimeDB CLI
curl -sSf https://install.spacetimedb.com | sh

# 3. Start local SpacetimeDB server
spacetime start

# 4. Build and deploy server module
cd SYSTEM-server
./rebuild.ps1  # Windows
./rebuild.sh   # Mac/Linux

# 5. Open Unity project
# Open Unity Hub and add SYSTEM-client-3d folder
# Open the project with Unity 2022.3+

# 6. Play in Unity Editor
# Press Play - connects to localhost:3000 automatically
```

### First Run Checklist

✅ SpacetimeDB server running (`spacetime status`)  
✅ Server module deployed (check `spacetime list`)  
✅ Unity project open in 2022.3+  
✅ Login scene active  
✅ Console shows "Connected to SpacetimeDB!"

## 💻 Development Workflow

### Local Development

#### Starting the Server
```bash
# Terminal 1: SpacetimeDB
spacetime start

# Terminal 2: Build & Deploy
cd SYSTEM-server
cargo build --release
spacetime publish --server local system
```

#### Unity Development
1. Open `SYSTEM-client-3d` in Unity
2. Load the **Login** scene
3. Press Play - automatically connects to `localhost:3000`
4. Create account or login to start playing

#### Testing Multiplayer Locally
1. Build for Windows: `Build → Build Local Windows`
2. Run the built executable
3. Also run in Unity Editor
4. Both instances connect to same local server

### Building for Different Environments

The project includes a comprehensive build system accessible from Unity's menu bar:

#### WebGL Builds
- **Build → Build Local WebGL** - Connects to `localhost:3000`
- **Build → Build Test WebGL** - Connects to test server
- **Build → Build Production WebGL** - Connects to production server

#### Standalone Builds
- **Build → Build Local Windows** - Local development build
- **Build → Build Test Windows** - Test environment build
- **Build → Build Production Windows** - Production build

Each build outputs to its own directory:
```
SYSTEM-client-3d/Build/
├── Local/          # localhost:3000
├── Test/           # maincloud.spacetimedb.com/system-test
└── Production/     # maincloud.spacetimedb.com/system
```

### Deployment

The project includes a unified deployment system for managing SpacetimeDB across all environments.

#### Quick Deploy Commands

```powershell
# Deploy to test environment
./Scripts/deploy-spacetimedb.ps1 -Environment test

# Deploy to production with verification
./Scripts/deploy-spacetimedb.ps1 -Environment production -Verify

# Deploy with database reset (WARNING: deletes all data)
./Scripts/deploy-spacetimedb.ps1 -Environment test -DeleteData -Yes

# Deploy with cache invalidation
./Scripts/deploy-spacetimedb.ps1 -Environment production -InvalidateCache
```

#### Deployment Options

| Option | Description |
|--------|-------------|
| `-Environment` | Target environment: `local`, `test`, or `production` |
| `-DeleteData` | Complete database wipe (equivalent to `spacetime publish -c`) |
| `-InvalidateCache` | Clear CloudFront cache after deployment |
| `-PublishOnly` | Deploy module without data operations |
| `-Verify` | Run post-deployment verification tests |
| `-BuildConfig` | Generate build-config.json for WebGL builds |
| `-SkipBuild` | Skip Rust compilation (use existing build) |
| `-Yes` | Non-interactive mode for CI/CD |

#### Unity Editor Deployment

From Unity's menu bar:
- **SYSTEM → Deploy → Deploy to Local** - Deploy to localhost
- **SYSTEM → Deploy → Deploy to Test** - Deploy to test server
- **SYSTEM → Deploy → Deploy to Production** - Deploy to production
- **SYSTEM → Deploy → Verify Current Deployment** - Check deployment status

#### Complete WebGL Deployment Flow

```powershell
# 1. Build WebGL in Unity
# Build → Build Test WebGL

# 2. Deploy server and generate config
./Scripts/deploy-spacetimedb.ps1 -Environment test -BuildConfig -InvalidateCache

# 3. Upload WebGL build to S3 (if using AWS)
aws s3 sync ./SYSTEM-client-3d/Build/Test s3://your-bucket/ --delete
```

## 🏗️ Architecture

### Client-Server Communication

```
Unity Client                    SpacetimeDB Server
     │                                │
     ├──[Reducer Call]───────────────>│
     │  (StartMining)                 │ Validate & Execute
     │                                ├──> Update Tables
     │<──────[Table Updates]──────────│
     │   (Player, WavePacket)         │
     ├──> Update Local State          │
     └──> Render Changes              │
```

### Event-Driven Architecture

The client uses a sophisticated event system for decoupled communication:

```
SpacetimeDB ──> EventBridge ──> GameEventBus ──> Components
                                      │
                  ┌───────────────────┼───────────────────┐
                  │                   │                   │
            PlayerTracker      WorldManager         UIController
            (Data Layer)      (Visual Layer)      (Interface)
```

### Player Management Flow

1. **PlayerTracker** receives SpacetimeDB updates
2. Publishes events via GameEventBus
3. **WorldManager** subscribes to events
4. Spawns/updates/removes visual GameObjects
5. Maintains separation between data and visuals

### Key Components

#### Server (Rust)
- **`lib.rs`** - Main module with all game logic
- **Tables** - Player, World, WavePacket, etc.
- **Reducers** - Game actions (login, move, mine, etc.)
- **Scheduled** - Tick functions for game updates

#### Client (Unity)
| Script | Purpose |
|--------|---------|
| **GameManager** | SpacetimeDB connection & session management |
| **PlayerTracker** | Monitors player table, publishes events |
| **WorldManager** | Spawns/manages player GameObjects |
| **GameEventBus** | Central event system with state machine |
| **SpacetimeDBEventBridge** | Converts DB events to game events |
| **WavePacketMiningSystem** | Mining mechanics and visuals |
| **BuildConfiguration** | Runtime environment configuration |

## 🌍 Environment Configuration

### Local Development
- **Server**: `http://localhost:3000`
- **Module**: `system`
- **Use Case**: Development and testing
- **Features**: Debug logging, development build

### Test Environment
- **Server**: `https://maincloud.spacetimedb.com`
- **Module**: `system-test`
- **Use Case**: QA and staging
- **Features**: Public testing, debug logging

### Production
- **Server**: `https://maincloud.spacetimedb.com`
- **Module**: `system`
- **Use Case**: Live game
- **Features**: Optimized build, minimal logging

### Configuration Files

Build configuration is stored in `StreamingAssets/build-config.json`:
```json
{
    "environment": "test",
    "serverUrl": "https://maincloud.spacetimedb.com",
    "moduleName": "system-test",
    "enableDebugLogging": true,
    "developmentBuild": true
}
```

## 🔧 Troubleshooting

### Connection Issues

**Problem**: "Cannot connect to SpacetimeDB"
```bash
# Check server is running
spacetime status

# Check module is published
spacetime list

# Restart server
spacetime stop
spacetime start
```

**Problem**: "WebGL build connects to wrong server"
- Rebuild with correct environment: `Build → Build [Environment] WebGL`
- Check `Build/[Environment]/StreamingAssets/build-config.json`

### Player Spawning Issues

**Problem**: "Player not appearing"
```csharp
// Check in Unity console:
// 1. PlayerTracker should log: "Player joined"
// 2. WorldManager should log: "Spawning player"
// 3. Check player prefab exists in Resources/Prefabs/
```

**Problem**: "Multiple players spawning"
- Ensure only one GameManager exists (DontDestroyOnLoad)
- Check for duplicate PlayerTracker components

### Control Issues

**Problem**: "Mouse not rotating character"
- Check `PlayerController.mouseSensitivity` (default: 0.5)
- Check rotation scale in `HandleMouseRotation()` (default: 0.05f for X, 0.02f for Y)
- Verify `PlayerController.enableMouseLook` is true
- Ensure cursor is locked (should be invisible during play)
- Check if Rigidbody is blocking rotation (see logs for "[ROTATION] Using Rigidbody")
- Network sync might be overriding rotation - check for "[NETWORK]" warnings in logs

**Problem**: "Rotation too sensitive/fast"
- Reduce `mouseSensitivity` in Inspector (try 0.1-0.3)
- In PlayerController.HandleMouseRotation(), adjust:
  - `rotationScaleX` from 0.05f → 0.02f or lower
  - `rotationScaleY` from 0.02f → 0.01f or lower
- Remember: 1° per frame = 60°/second at 60fps

**Problem**: "WASD not working"
- Verify PlayerInputActions is enabled in PlayerController
- Check Input System package is installed
- Movement is relative to character facing (not camera)
- Regenerate PlayerInputActions.cs from the .inputactions asset if needed

**Problem**: "Camera not following character properly"
- Check CameraManager has `useOrbitalCamera = true`
- Verify CameraManager.Instance exists
- Camera distance/height can be adjusted in CameraManager Inspector
- Ensure camera target is set to local player

### Build Issues

**Problem**: "Build fails with missing references"
```bash
# Regenerate bindings
cd SYSTEM-server
./rebuild.ps1  # This regenerates C# bindings
```

**Problem**: "Scripting defines not working"
- Check BuildSettings.cs configuration
- Verify environment-specific defines in Player Settings

### Common Server Errors

| Error | Solution |
|-------|----------|
| "Address already in use" | Another SpacetimeDB instance running, stop it |
| "Module not found" | Run `spacetime publish` to deploy module |
| "Reducer failed" | Check server logs: `spacetime logs system` |
| "Connection refused" | Start SpacetimeDB: `spacetime start` |

## 🤝 Contributing

### Development Principles

1. **Event-Driven Architecture**
   - Use GameEventBus for component communication
   - Keep data layer (Tracker) separate from visuals (Manager)
   - Publish events for state changes

2. **Server Authority**
   - All game logic validated server-side
   - Client is purely for visualization and input
   - Never trust client-submitted data

3. **Code Organization**
   ```
   Scripts/
   ├── Core/           # GameManager, EventBus
   ├── Players/        # PlayerTracker, PlayerController
   ├── World/          # WorldManager, WorldCircuit
   ├── Mining/         # WavePacketMiningSystem
   └── UI/             # LoginUIController, HUD
   ```

4. **SpacetimeDB Patterns**
   - Use `Iter()` for table iteration (no LINQ)
   - Handle all reducer callbacks
   - Subscribe to relevant tables only

### Adding New Features

1. **Server-side** (Rust):
   ```rust
   // 1. Add table in lib.rs
   #[table(public)]
   pub struct NewFeature {
       #[primary_key]
       pub id: u64,
       pub data: String,
   }
   
   // 2. Add reducer
   #[reducer]
   pub fn use_new_feature(ctx: &EventContext, id: u64) {
       // Implementation
   }
   ```

2. **Client-side** (Unity):
   ```csharp
   // 1. Regenerate bindings: ./rebuild.ps1
   
   // 2. Add event in GameEvents.cs
   public class NewFeatureEvent : IGameEvent {
       public ulong Id { get; set; }
   }
   
   // 3. Bridge in SpacetimeDBEventBridge.cs
   conn.Db.NewFeature.OnInsert += OnNewFeature;
   
   // 4. Handle in component
   GameEventBus.Instance.Subscribe<NewFeatureEvent>(OnNewFeature);
   ```

### Testing Guidelines

- **Unit Tests**: Test individual components in isolation
- **Integration Tests**: Test SpacetimeDB communication
- **Multiplayer Tests**: Always test with 2+ clients
- **Environment Tests**: Test all three environments

### Code Style

- **C# (Unity)**: Follow Microsoft C# conventions
- **Rust (Server)**: Follow Rust standard style (rustfmt)
- **Comments**: Document complex logic and public APIs
- **Events**: Name clearly (PlayerJoinedEvent, not PJE)

## 🎨 Visual Features

### High-Resolution World Spheres

The project uses custom-generated icosphere meshes for perfectly smooth world spheres:

- **LOD System**: Three levels of detail (1K, 5K, 20K triangles)
- **Default**: 5,120 triangles (subdivision 4) - excellent quality/performance balance
- **Generator**: Editor tool at `SYSTEM → Create High-Res Sphere Mesh`
- **Auto-generation**: Meshes created automatically on first load

### Quantum Grid Shader

Custom URP shader (`WorldSphereEnergy`) for quantum visualization:

**Features**:
- Pulsing base color with configurable speed and intensity
- Thin grid lines (adjustable, default ~1 unit wide on 300-unit sphere)
- 6 quantum state markers:
  - |0⟩ state at north pole
  - |1⟩ state at south pole
  - |+⟩ and |-⟩ states on X-axis equator
  - |+i⟩ and |-i⟩ states on Z-axis equator

**Technical**:
- Single-pass rendering for performance
- Spherical coordinate-based grid (phi/theta)
- WebGL-compatible with proper URP transformation
- Configurable via material properties

### WebGL Optimizations

- **Scale Fixes**: Multiple layers of protection prevent tiny world rendering
- **Debug Overlay**: Development-only UI (hidden in production builds)
  - Toggle with F3 (visibility) and F4 (mode)
  - Shows: Connection | Environment | Player | State
- **Diagnostics**: Comprehensive logging for transform hierarchy and scaling issues

## 📚 Additional Resources

- [SpacetimeDB Documentation](https://spacetimedb.com/docs)
- [Unity Multiplayer Guide](https://docs.unity3d.com/Manual/UNet.html)
- [Project Design Document](./Documentation/DESIGN.md)
- [CLAUDE.md](./CLAUDE.md) - AI assistant instructions and development guide

## 📝 License

This project is proprietary. All rights reserved.

## 🙏 Acknowledgments

- Built with [SpacetimeDB](https://spacetimedb.com) by Clockwork Labs
- Powered by [Unity](https://unity.com) game engine
- Event system inspired by modern game architectures

---

**Need Help?** Check [Troubleshooting](#-troubleshooting) or open an issue on GitHub.