# Claude Code Prompt: Create Comprehensive README for SYSTEM Project

## Project Context

I need a complete README.md file for **SYSTEM**, a Unity multiplayer game using SpacetimeDB backend. The project has evolved significantly and needs comprehensive documentation covering architecture, development workflow, and deployment.

## Project Overview

**SYSTEM** is a 3D multiplayer game built with:
- **Frontend**: Unity 2022.3+ (C#)
- **Backend**: SpacetimeDB with Rust modules
- **Architecture**: Event-driven client-server with real-time synchronization
- **Deployment**: Multi-environment WebGL + standalone builds

## Key Project Features

### Core Gameplay
- **Multiplayer world exploration** on spherical planet surfaces
- **Wave packet mining system** with frequency-based crystals
- **Real-time player tracking** and spatial queries
- **World-based player spawning** and management

### Technical Architecture
- **SpacetimeDB integration** for server-authoritative gameplay
- **Event-driven player management** (PlayerTracker → WorldManager)
- **Component-based architecture** with clean separation of concerns
- **Multi-environment build system** (Local/Test/Production)

## Current Project Structure

```
SYSTEM/
├── SYSTEM-server/           # Rust SpacetimeDB module
│   ├── src/lib.rs          # Main server logic & reducers
│   └── Cargo.toml          # Rust dependencies
├── SYSTEM-client-3d/        # Unity client
│   ├── Assets/Scripts/     # Game logic
│   ├── Assets/Scenes/      # Game scenes
│   ├── Assets/Prefabs/     # Player & world prefabs
│   └── Build/              # Build outputs
│       ├── Local/          # Local development builds
│       ├── Test/           # Test environment builds
│       └── Production/     # Production builds
└── Documentation/          # Project docs & design
```

## Development Workflow Features

### Build System
- **Environment-specific builds**: Local/Test/Production with different server connections
- **Automated build scripts**: Unity Editor integration with custom build menu
- **Multiple platform targets**: WebGL for web deployment, Windows for local testing

### SpacetimeDB Integration
- **Local development**: Connects to localhost:3000 SpacetimeDB server
- **Test environment**: Connects to maincloud.spacetimedb.com/system-test
- **Production**: Connects to maincloud.spacetimedb.com/system

### Player Management
- **Event-driven architecture**: PlayerTracker handles data, WorldManager handles visuals
- **Real-time synchronization**: Position updates, world transitions, player spawning
- **Spatial queries**: Proximity detection, nearby player tracking

## README Requirements

Please create a comprehensive README.md that includes:

### 1. Project Overview & Description
- What SYSTEM is and core gameplay concepts
- Technical stack and architecture overview
- Key features and multiplayer capabilities

### 2. Getting Started / Quick Setup
- Prerequisites (Unity version, Rust, SpacetimeDB CLI)
- Clone and setup instructions
- Local development server setup
- First time run instructions

### 3. Development Workflow
- **Local Development**: How to run locally with SpacetimeDB
- **Building**: Using the Unity build menu for different environments  
- **Testing**: How to test multiplayer locally and on test server
- **Deployment**: Environment-specific deployment process

### 4. Architecture Documentation
- **Client-Server Architecture**: Unity ↔ SpacetimeDB communication
- **Event System**: PlayerTracker → WorldManager event flow
- **Player Management**: How multiplayer players are tracked and synchronized
- **Build System**: Multi-environment build configuration

### 5. Environment Configuration
- **Local**: Development setup with local SpacetimeDB
- **Test**: Public test environment configuration
- **Production**: Production environment setup

### 6. Key Scripts & Components
- **Server**: Main Rust reducers and table definitions
- **Client**: Core Unity scripts (GameManager, PlayerTracker, WorldManager, etc.)
- **Build Scripts**: Environment-specific build automation

### 7. Troubleshooting
- Common issues and solutions
- Player spawning/tracking problems
- Connection and environment issues
- Build and deployment problems

### 8. Contributing / Development Notes
- Code organization principles
- Event-driven architecture patterns
- SpacetimeDB best practices used in the project

## Tone & Style
- **Technical but accessible**: Readable for developers
- **Practical focus**: Emphasize actionable instructions
- **Well-organized**: Clear sections with logical flow
- **Professional**: Suitable for project documentation or sharing

Create a README that serves as both introduction to new developers and comprehensive reference for ongoing development.