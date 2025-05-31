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
    
    Player ||--o{ MinerDevice : "owns miners"
    Player ||--o{ StorageDevice : "owns storage"
    Player ||--o{ LoggedOutPlayer : "logged out state"
    
    WorldCircuit ||--o{ EnergyOrb : "emits orbs"
    EnergyOrb ||--o{ EnergyPuddle : "creates puddles"
    
    MinerDevice }o--|| EnergyPuddle : "targets puddle"
    
    EnergyTransfer }o--|| MinerDevice : "from miner"
    EnergyTransfer }o--|| StorageDevice : "to storage"
    EnergyTransfer }o--|| Player : "to/from player"
    
    TickTimer ||--o{ WorldCircuit : "triggers emissions"
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
4. **EnergyTransfer** moves energy between devices
5. **Player** uses energy for building and trading