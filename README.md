# SYSTEM

A multiplayer wave packet mining game built with Unity and SpacetimeDB where players explore interconnected worlds, extracting energy from quantum orbs using frequency-matched crystals.

## Overview

In SYSTEM, players mine "wave packets" - quantum energy units with unique frequency signatures - from orbs scattered across persistent worlds. Using crystals that resonate with specific frequencies, players must strategically extract matching packets while managing distance, timing, and competition from other miners.

### Key Features

- **Real-time Multiplayer** - See and compete with other players in shared persistent worlds
- **Physics-Based Mining** - Extract wave packets using frequency-matched crystals with realistic travel times
- **Dynamic World System** - Explore interconnected worlds with unique properties and resources
- **Visual Feedback** - Watch wave packets travel from orbs to your position with particle effects
- **Persistent State** - All game state maintained by SpacetimeDB backend

## Getting Started

### Prerequisites

- Unity 2022.3 LTS or later
- SpacetimeDB CLI (for running local server)
- Git

### Installation

1. Clone the repository:
```bash
git clone https://github.com/yourusername/system.git
cd system
```

2. Start the SpacetimeDB server:
```bash
cd SYSTEM-server
spacetime start
```

3. Open the Unity project:
   - Open Unity Hub
   - Add project from `SYSTEM-client-3d` folder
   - Open with Unity 2022.3+

4. Configure connection:
   - In Unity, find the Login scene
   - Default connects to `localhost:3000`
   - For cloud deployment, update the server URL

### Quick Start Guide

1. **Create Account** - Enter a username and click "Create Account"
2. **Choose Crystal** - Select Red, Green, or Blue starting crystal
3. **Find Orbs** - Look for glowing spheres in the world
4. **Start Mining** - Get within 30 units and press the mining button
5. **Collect Packets** - Watch packets travel to you and get automatically collected

## Game Mechanics

### Wave Packets & Frequencies

The game uses a 6-color frequency system:
- **Red (R)** - 0.000 frequency (Shell 0)
- **Yellow (RG)** - 0.167 frequency (Shell 1)  
- **Green (G)** - 0.333 frequency (Shell 0)
- **Cyan (GB)** - 0.500 frequency (Shell 1)
- **Blue (B)** - 0.667 frequency (Shell 0)
- **Magenta (BR)** - 0.833 frequency (Shell 1)

### Mining Process

1. **Approach** - Get within 30 units of an orb
2. **Activate** - Toggle mining mode (not hold)
3. **Extract** - Packets extracted every 2 seconds
4. **Transport** - Packets travel at 5 units/second
5. **Capture** - Automatic on arrival

### Distance Dynamics

- **10 units away**: 2s flight time (1 packet in flight)
- **20 units away**: 4s flight time (2 packets in flight)
- **30 units away**: 6s flight time (3 packets in flight)

## Project Structure

```
SYSTEM/
├── SYSTEM-server/          # Rust/SpacetimeDB backend
│   └── src/lib.rs         # All server logic
├── SYSTEM-client-3d/       # Unity client
│   └── Assets/Scripts/    # Game scripts
├── docs/                  # Documentation
└── README.md             # This file
```

### Key Components

**Server (SpacetimeDB)**
- Authoritative game state
- Mining validation
- World generation
- Player management

**Client (Unity)**
- 3D visualization
- Input handling
- Predictive movement
- Effect systems

## Development

### Building from Source

**Server:**
```bash
cd SYSTEM-server
spacetime build
```

**Client:**
1. Open in Unity 2022.3+
2. File → Build Settings
3. Select target platform
4. Build

### Architecture Overview

The game uses a client-server architecture with SpacetimeDB as the authoritative backend:

- **Server Authority** - All game logic validated server-side
- **Client Prediction** - Immediate visual feedback with reconciliation
- **Event Sourcing** - All state changes tracked through events
- **Subscription Model** - Efficient real-time data synchronization

### Key Systems

1. **Mining System** - Client-driven extraction with server validation
2. **World System** - Coordinate-based world addressing and navigation
3. **Subscription System** - Automated data synchronization
4. **Event System** - Type-safe decoupled communication

## Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

### Development Guidelines

1. **Search First** - Check existing code before creating new components
2. **Follow Patterns** - Use established architectural patterns
3. **Test Thoroughly** - Ensure changes work in multiplayer
4. **Document Changes** - Update relevant documentation

## Troubleshooting

### Common Issues

**Connection Failed**
- Ensure SpacetimeDB server is running
- Check firewall settings
- Verify server URL matches

**No Orbs Visible**
- Wait for world circuit to emit orbs
- Check if in correct world
- Verify subscription is active

**Mining Not Working**
- Ensure you have a crystal equipped
- Check distance to orb (max 30 units)
- Verify orb has matching packets

## Roadmap

### Planned Features

- **Crafting System** - Combine wave packets into higher frequencies
- **Trading** - Player-to-player packet exchange
- **World Tunnels** - Travel between worlds
- **Crystal Upgrades** - Enhance mining capabilities
- **Guilds** - Team-based gameplay

### Technical Improvements

- Instanced rendering for performance
- Predictive networking
- Cross-platform support
- Mod support

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with [SpacetimeDB](https://spacetimedb.com)
- Inspired by physics and quantum mechanics
- Community contributions and feedback

## Contact

- Discord: [Join our server](https://discord.gg/yourinvite)
- Issues: [GitHub Issues](https://github.com/yourusername/system/issues)
- Email: team@systemgame.example

---

**Current Version**: Alpha 0.1.0  
**Status**: In Development  
**Platform**: Windows, Mac, Linux (planned)