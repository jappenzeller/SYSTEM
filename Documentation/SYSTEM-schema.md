# SpacetimeDB Schema Diagram

```mermaid
erDiagram
    %% Core Data Types
    WorldCoords {
        i32 x
        i32 y  
        i32 z
    }
    
    DbVector3 {
        f32 x
        f32 y
        f32 z
    }
    
    %% Wave Packet Types
    WavePacketSignature {
        f32 frequency "0.0-1.0 normalized"
        f32 resonance "amplitude equivalent"
        u16 flux_pattern "phase/coherence"
    }
    
    WavePacketSample {
        WavePacketSignature signature
        u32 amount
    }
    
    CrystalType {
        enum Red "freq ~0.2"
        enum Green "freq ~0.575"
        enum Blue "freq ~0.725"
    }
    
    FrequencyBand {
        enum Red "0.0-0.167"
        enum Yellow "0.167-0.333"
        enum Green "0.333-0.500"
        enum Cyan "0.500-0.667"
        enum Blue "0.667-0.833"
        enum Magenta "0.833-1.0"
    }

    %% Main Tables
    World {
        WorldCoords world_coords PK
        string world_name
        string world_type
        u8 shell_level
    }
    
    Player {
        Identity identity PK
        u64 player_id UK "auto-inc"
        string name
        WorldCoords current_world
        DbVector3 position
        DbQuaternion rotation
        u64 last_position_update
    }
    
    WavePacketOrb {
        u64 orb_id PK "auto-inc"
        WorldCoords world_coords
        DbVector3 position
        DbVector3 velocity
        Vec-WavePacketSample wave_packet_composition
        u32 total_wave_packets
        u64 creation_time
        u32 lifetime_ms
        u64 last_dissipation
    }
    
    WavePacketStorage {
        u64 storage_id PK "auto-inc"
        string owner_type "player/vessel/etc"
        u64 owner_id
        FrequencyBand frequency_band
        u32 total_wave_packets
        Vec-WavePacketSample signature_samples
        u64 last_update
    }
    
    PlayerCrystal {
        u64 player_id PK
        CrystalType crystal_type
        u8 slot_count "1=free, 2=paid"
        u64 chosen_at
    }
    
    WavePacketExtraction {
        u64 extraction_id PK
        u64 player_id
        u64 wave_packet_id
        WavePacketSignature signature
        u64 departure_time
        u64 expected_arrival
    }
    
    WorldCircuit {
        WorldCoords world_coords PK
        u64 circuit_id UK
        u8 qubit_count
        u64 emission_interval_ms
        u32 orbs_per_emission
        u64 last_emission_time
    }
    
    GameSettings {
        u32 id PK
        u64 tick_ms
        u32 max_players
    }
    
    LoggedOutPlayer {
        Identity identity PK
        u64 player_id
        string name
        Timestamp logout_time
    }
    
    Account {
        Identity identity PK
        string username UK
        u64 created_at
    }

    %% Relationships
    World ||--|| WorldCircuit : "has circuit"
    World ||--o{ Player : "contains players"
    World ||--o{ WavePacketOrb : "has orbs"
    
    Player ||--o| PlayerCrystal : "has crystal"
    Player ||--o{ WavePacketStorage : "owns storage"
    Player ||--o{ WavePacketExtraction : "active extractions"
    
    PlayerCrystal }o--|| CrystalType : "is type"
    
    WavePacketOrb ||--o{ WavePacketSample : "contains packets"
    WavePacketStorage ||--o{ WavePacketSample : "stores packets"
    
    WavePacketExtraction }o--|| Player : "belongs to"
    WavePacketExtraction }o--|| WavePacketSignature : "extracts"
    
    WorldCircuit ||--o{ WavePacketOrb : "emits orbs"
    
    LoggedOutPlayer }o--|| Player : "preserves data"
    Account ||--|| Player : "owns player"
```

## Table Descriptions

### Core Gameplay Tables

**World** - Defines each spherical world in the metaverse
- Center world: `(0,0,0)` with shell level 0
- Outer worlds: Shell 1+ at various coordinates
- Each world has unique properties and emission patterns

**Player** - Active players in the game
- Tracks position, rotation, and current world
- Identity links to Account for persistent data
- Position updates tracked with timestamps

**Account** - Persistent player accounts
- Username is unique across the game
- Identity persists across sessions
- Created timestamp for account age

### Wave Packet System

**WavePacketOrb** - Energy containers that spawn in worlds
- Contains multiple wave packet types (composition)
- Falls with velocity, dissipates over time
- Total packets decrease as mined or dissipated
- Visual color based on weighted packet composition

**WavePacketSignature** - Unique identifier for each packet type
- Frequency (0.0-1.0): Determines color and crystal matching
- Resonance: Amplitude/intensity of the packet
- Flux Pattern: Phase information for future mechanics

**WavePacketStorage** - Player inventory for wave packets
- Organized by frequency bands (6 colors)
- Tracks individual signature samples within each band
- Supports multiple owner types (players, vessels, etc.)

**PlayerCrystal** - Mining tool selection
- Players choose Red, Green, or Blue starter crystal
- Each crystal extracts packets within ±π/6 radians
- Slot count: 1 for free players, 2 for paid

**WavePacketExtraction** - Active mining notifications
- Server creates when packet extracted from orb
- Client uses for visual animation timing
- Tracks flight time based on distance

### World Systems

**WorldCircuit** - Quantum emitter at world center
- Periodically emits wave packet orbs
- Emission rate and quantity configurable
- Future: Players solve circuits for bonuses

### System Tables

**GameSettings** - Global configuration
**LoggedOutPlayer** - Preserves state across disconnections

## Mining System Flow

1. **Player** approaches **WavePacketOrb** within 30 units
2. **Player** toggles mining with equipped **PlayerCrystal**
3. Client calls `extract_wave_packet` every 2 seconds
4. Server validates and creates **WavePacketExtraction** entry
5. Client animates packet travel (distance/5 seconds)
6. On arrival, client calls `capture_wave_packet`
7. Server adds to **WavePacketStorage**

## Key Design Decisions

### Frequency System
- 6-color system based on normalized radians
- Each color covers ±π/6 range for crystal matching
- Shell-based rarity (Shell 0: RGB, Shell 1: RG/GB/BR)

### Client-Driven Extraction
- Client controls extraction timing (every 2 seconds)
- Server validates all operations
- Unique packet IDs prevent exploitation
- Visual feedback synchronized with server state

### Storage Organization
- Packets grouped by frequency band
- Individual signatures preserved for crafting
- Efficient querying by color type

### Future Expansions
- Multiple crystal slots for paid players
- Crafting system (2:1 color combinations)
- Trading between players
- Vessel-based automated mining
- Cross-world energy distribution