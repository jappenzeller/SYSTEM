# SpacetimeDB Schema Diagram

```mermaid
erDiagram
    %% Core Data Types
    WorldCoords {
        i8 x
        i8 y  
        i8 z
    }
    
    DbVector3 {
        f32 x
        f32 y
        f32 z
    }
    
    EnergyType {
        enum Red
        enum Green
        enum Blue
    }

    %% Main Tables
    World {
        WorldCoords world_coords PK
        u8 shell_level
        f32 radius
        u8 circuit_qubit_count
        string status
    }
    
    Player {
        Identity identity PK
        u32 player_id UK
        string name
        WorldCoords current_world
        DbVector3 position
        f32 energy_red
        f32 energy_green
        f32 energy_blue
    }
    
    EnergyPuddle {
        u64 puddle_id PK
        WorldCoords world_coords
        DbVector3 position
        EnergyType energy_type
        f32 current_amount
        f32 max_amount
    }
    
    EnergyOrb {
        u64 orb_id PK
        WorldCoords world_coords
        DbVector3 position
        DbVector3 velocity
        EnergyType energy_type
        f32 energy_amount
        u64 creation_time
    }
    
    WorldCircuit {
        WorldCoords world_coords PK
        u8 qubit_count
        u64 emission_interval_ms
        u32 orbs_per_emission
        u64 last_emission_time
    }
    
    MinerDevice {
        u64 miner_id PK
        Identity owner_identity
        WorldCoords world_coords
        DbVector3 position
        u64 target_puddle_id
        f32 efficiency_bonus
        f32 energy_red
        f32 energy_green
        f32 energy_blue
        f32 storage_capacity
    }
    
    StorageDevice {
        u64 storage_id PK
        Identity owner_identity
        WorldCoords world_coords
        DbVector3 position
        f32 energy_red
        f32 energy_green
        f32 energy_blue
        f32 storage_capacity
    }
    
    EnergyTransfer {
        u64 transfer_id PK
        string from_device_type
        u64 from_device_id
        string to_device_type
        u64 to_device_id
        EnergyType energy_type
        f32 transfer_rate
        bool is_continuous
        Vec-u64 route_spheres
        f32 total_cost_per_unit
    }
    
    DistributionSphere {
        u64 sphere_id PK
        WorldCoords world_coords
        DbVector3 position
        f32 coverage_radius
        u64 tunnel_id "nullable"
        f32 energy_red
        f32 energy_green
        f32 energy_blue
        f32 buffer_capacity
    }
    
    Tunnel {
        u64 tunnel_id PK
        WorldCoords from_world
        WorldCoords to_world
        f32 activation_progress
        f32 activation_threshold
        string status
        f32 transfer_cost_multiplier
    }
    
    DeviceConnection {
        u64 connection_id PK
        u64 device_id
        string device_type
        u64 sphere_id
        f32 connection_strength
    }
    
    GameSettings {
        u32 id PK
        u64 tick_ms
        u32 max_players
    }
    
    TickTimer {
        u64 scheduled_id PK
        ScheduleAt scheduled_at
    }
    
    LoggedOutPlayer {
        Identity identity PK
        Player player_data
    }

    %% Relationships
    World ||--|| WorldCircuit : "has circuit"
    World ||--o{ Player : "contains players"
    World ||--o{ EnergyPuddle : "has puddles"
    World ||--o{ EnergyOrb : "has falling orbs"
    World ||--o{ MinerDevice : "has miners"
    World ||--o{ StorageDevice : "has storage"
    World ||--o{ DistributionSphere : "has distribution spheres"
    
    Player ||--o{ MinerDevice : "owns miners"
    Player ||--o{ StorageDevice : "owns storage"
    Player ||--o{ LoggedOutPlayer : "logged out state"
    
    WorldCircuit ||--o{ EnergyOrb : "emits orbs"
    EnergyOrb ||--o{ EnergyPuddle : "creates puddles"
    
    MinerDevice }o--o| EnergyPuddle : "targets puddle"
    
    DistributionSphere }o--o| Tunnel : "positioned at tunnel"
    DistributionSphere ||--o{ DeviceConnection : "connects devices"
    
    DeviceConnection }o--|| MinerDevice : "connects miner"
    DeviceConnection }o--|| StorageDevice : "connects storage"
    DeviceConnection }o--|| Player : "connects player"
    
    Tunnel }o--|| World : "connects from world"
    Tunnel }o--|| World : "connects to world"
    
    EnergyTransfer }o--o{ DistributionSphere : "routes through spheres"
    
    TickTimer ||--o{ WorldCircuit : "triggers emissions"
    TickTimer ||--o{ DeviceConnection : "updates connections"
```

## Table Descriptions

### Core Gameplay Tables

**World** - Defines each spherical world in the metaverse
- Center world: `(0,0,0)` with radius 100, shell level 0
- Future: Shell 1 worlds at `±1` coordinates with 2 qubits

**Player** - Active players in the game
- Spawns at random position on sphere surface
- Tracks energy inventory (red, green, blue)
- `current_world` determines which sphere they're on

**EnergyPuddle** - Static energy deposits on world surfaces
- Created when energy orbs hit the ground
- Players and miners can extract energy from these
- Each puddle has a specific energy type and amount

**EnergyOrb** - Dynamic energy falling from world circuits
- Falls from center of sphere outward to surface
- Creates puddles on impact
- Emitted every 5 seconds (6 orbs per emission)

### World Systems

**WorldCircuit** - Quantum gate at center of each world
- Periodically emits energy orbs
- 1 qubit for center world, more for outer shells
- Future: Players can solve quantum circuits for bonuses

### Energy Distribution Network

**DistributionSphere** - Floating energy routers at tunnel entrances
- Each tunnel has one distribution sphere
- Provides free local transfers within coverage radius
- Acts as buffer storage for cross-world transfers
- Handles routing between connected devices

**Tunnel** - Connections between worlds in the 3×3×3 grid
- Links outer worlds to center world initially
- Requires player activity near tunnel to activate
- Higher transfer costs for cross-tunnel energy movement
- Foundation for metaverse expansion

**DeviceConnection** - Links devices to distribution spheres
- Automatically connects devices within sphere coverage
- Determines which sphere handles device transfers
- Enables the "set transfer rule" abstraction layer

**EnergyTransfer** - Now routes through distribution network
- Local transfers: Device → Sphere → Device (free)
- Cross-world transfers: Device → Sphere → Tunnel → Sphere → Device (costly)
- Handles pathfinding through sphere network

### Player Infrastructure

**MinerDevice** - Automated energy collectors
- Players can build these near puddles
- Efficiency affected by quantum circuit solving
- Has storage capacity for collected energy

**StorageDevice** - Energy storage containers
- Large capacity for energy stockpiling
- Connected via energy distribution network
- Foundation for logistics and trading

**EnergyTransfer** - Active energy movements
- Handles automated transfers between devices
- Supports continuous flows (e.g., miner → storage)
- Foundation for distribution sphere network

### System Tables

**GameSettings** - Global game configuration
**TickTimer** - Scheduled events (10Hz tick rate)
**LoggedOutPlayer** - Preserves player state across disconnections

## Data Flow

1. **WorldCircuit** emits **EnergyOrb** every 5 seconds
2. **EnergyOrb** falls to surface and creates **EnergyPuddle**
3. **Player** or **MinerDevice** extracts energy from **EnergyPuddle**
4. **Devices** auto-connect to nearest **DistributionSphere** via **DeviceConnection**
5. **EnergyTransfer** routes through **DistributionSphere** network:
   - Local: Device → Sphere → Device (free)
   - Cross-world: Device → Sphere → **Tunnel** → Sphere → Device (costly)
6. **Player** builds logistics networks and activates new **Tunnels**
7. New **Worlds** become available as **Tunnels** activate