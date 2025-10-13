# Energy Spire System - Server Implementation Summary

**Date:** 2025-10-12
**Status:** ✅ COMPLETE - Server-side implementation finished

## Overview

Implemented the three-tier Energy Spire system in SpacetimeDB with separate tables for circuit bases, distribution spheres, and quantum tunnels. The system supports the main 6 cardinal directions (N/S/E/W/Forward/Back) with routing, charge accumulation, and eventual tunnel formation.

## Database Schema

### 1. WorldCircuit (Circuit Base - Optional Component)
Ground-level component that emits orbs for mining.

```rust
pub struct WorldCircuit {
    circuit_id: u64,                    // Primary key, auto-increment
    world_coords: WorldCoords,          // FCC lattice position
    cardinal_direction: String,         // "North", "South", "East", "West", "Forward", "Back"
    circuit_type: String,               // Circuit type identifier
    qubit_count: u8,                    // Number of qubits
    orbs_per_emission: u32,             // Orbs spawned per emission
    emission_interval_ms: u64,          // Time between emissions
    last_emission_time: u64,            // Last emission timestamp
}
```

### 2. DistributionSphere (Routing Node - Required Component)
Mid-level routing sphere for packet transfers.

```rust
pub struct DistributionSphere {
    sphere_id: u64,                     // Primary key, auto-increment
    world_coords: WorldCoords,          // FCC lattice position
    cardinal_direction: String,         // Cardinal direction
    sphere_position: DbVector3,         // Pre-calculated 3D position on sphere surface
    sphere_radius: u8,                  // Sphere radius (default: 40 units)
    packets_routed: u64,                // Lifetime packet routing count
    last_packet_time: Timestamp,        // Last packet routed timestamp
}
```

### 3. QuantumTunnel (Ring Assembly - Required Component)
Top-level ring that accumulates charge and forms inter-world tunnels.

```rust
pub struct QuantumTunnel {
    tunnel_id: u64,                     // Primary key, auto-increment
    world_coords: WorldCoords,          // FCC lattice position
    cardinal_direction: String,         // Same as DistributionSphere
    ring_charge: f32,                   // Charge level (0-100)
    tunnel_status: String,              // "Inactive", "Charging", "Active"
    connected_to_world: Option<WorldCoords>,  // Connected world (if Active)
    connected_to_sphere_id: Option<u64>,      // Connected sphere ID (if Active)
    tunnel_color: String,               // Tier-based color (Red/Green/Blue)
    formed_at: Option<Timestamp>,       // When tunnel became Active
}
```

## Cardinal Positioning System

**World Radius:** R = 300 units

### Main 6 Cardinal Directions

| Direction | Position (x, y, z) | Axis | Tunnel Color |
|-----------|-------------------|------|--------------|
| **North** | (0, 300, 0) | +Y | Green |
| **South** | (0, -300, 0) | -Y | Green |
| **East** | (300, 0, 0) | +X | Red |
| **West** | (-300, 0, 0) | -X | Red |
| **Forward** | (0, 0, 300) | +Z | Blue |
| **Back** | (0, 0, -300) | -Z | Blue |

### Tunnel Color Tiers

- **Primary Tier (RGB Axes):** Red (±X), Green (±Y), Blue (±Z)
- **Secondary Tier (Planar):** Yellow (XY), Cyan (YZ), Magenta (XZ) - *not yet implemented*
- **Tertiary Tier (Volumetric):** Grey (center cube) - *not yet implemented*

## Reducers

### spawn_main_spires(world_x, world_y, world_z)
Spawns the main 6 energy spires for a world.

- Creates **DistributionSphere** for each cardinal direction
- Creates **QuantumTunnel** for each cardinal direction
- Initializes all fields with proper defaults
- Sets tunnel colors based on cardinal axis

**Example:**
```bash
spacetime call system spawn_main_spires 0 0 0
```

### spawn_circuit_at_spire(world_x, world_y, world_z, cardinal_direction, ...)
Adds an optional circuit component at a spire location.

- Verifies DistributionSphere exists at location
- Creates WorldCircuit with specified parameters
- Circuit emits orbs for mining

**Example:**
```bash
spacetime call system spawn_circuit_at_spire 0 0 0 "North" "EntanglementCircuit" 2 3 10000
```

### initiate_transfer(packet_count, destination_device_id)
Initiates packet transfer from player to storage device.

- Finds nearest DistributionSphere to player
- Finds nearest DistributionSphere to storage device
- Builds routing path through spheres
- Deducts packets from player inventory
- Creates PacketTransfer record

### complete_transfer(transfer_id)
Completes a packet transfer (called by client after visualization).

- Updates **DistributionSphere.packets_routed** statistics
- Increments **QuantumTunnel.ring_charge** by 1.0 per transfer
- Updates tunnel status to "Charging" at 100% charge
- Adds packets to storage device

## Helper Functions

### get_cardinal_position(direction: &str) -> DbVector3
Calculates 3D position on world sphere for a cardinal direction.

### get_tunnel_color(direction: &str) -> String
Returns tier-based color for a cardinal direction.

### find_nearest_spire(world_coords, position) -> DistributionSphere
Finds the nearest distribution sphere to a given position on a world.

## Migration from EnergySpire

The old `EnergySpire` table has been marked as **DEPRECATED** but kept for backwards compatibility. New code should use:
- **DistributionSphere** for routing and packet statistics
- **QuantumTunnel** for charge accumulation and tunnel formation

## Compilation Status

✅ **SUCCESS** - Compiles cleanly with only minor warnings:
- Unused variable `ctx` in `debug_test_spawn_positions`
- Unused function `hash_password`

## Next Steps

1. ✅ C# bindings regenerated for Unity
2. ⏳ Create Unity `EnergySpireManager` visualization system
3. ⏳ Implement WavePacketDisplay routing visualization
4. ⏳ Test complete flow: spawn → mine → transfer → charge

## Files Modified

- `H:/SpaceTime/SYSTEM/SYSTEM-server/src/lib.rs`
  - Added DistributionSphere table (line ~239)
  - Added QuantumTunnel table (line ~256)
  - Updated WorldCircuit with cardinal_direction field (line ~210)
  - Added spawn_main_spires reducer (line ~3592)
  - Added spawn_circuit_at_spire reducer (line ~3667)
  - Updated complete_transfer reducer (line ~3412)
  - Updated find_nearest_spire helper (line ~3248)

## Testing Commands

```bash
# Spawn main 6 spires on origin world
spacetime call system spawn_main_spires 0 0 0

# Add a circuit at North position
spacetime call system spawn_circuit_at_spire 0 0 0 "North" "TestCircuit" 2 3 10000

# View distribution spheres
spacetime sql system "SELECT * FROM distribution_sphere WHERE world_coords = (0,0,0)"

# View quantum tunnels
spacetime sql system "SELECT sphere_id, cardinal_direction, ring_charge, tunnel_status FROM quantum_tunnel WHERE world_coords = (0,0,0)"

# Check tunnel charges
spacetime sql system "SELECT cardinal_direction, ring_charge, tunnel_color FROM quantum_tunnel ORDER BY ring_charge DESC"
```
