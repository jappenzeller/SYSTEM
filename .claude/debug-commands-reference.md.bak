# Debug Commands Reference
**Version:** 1.0.0
**Last Updated:** 2025-09-29
**Status:** Active
**Dependencies:** [TECHNICAL_ARCHITECTURE.md, GAMEPLAY_SYSTEMS.md]

## Overview
Complete reference for all debug commands available in the SYSTEM server module. These commands are for testing and development only.

## Command Syntax
```bash
spacetime call <module> <reducer> <args>
```

Note: Float arguments must include decimal notation (use `10.0` not `10` for f32 parameters)

## Orb Management Commands

### spawn_test_orb
Creates a wave packet orb at specified position with given parameters.

**Parameters:**
- `x: f32` - X coordinate position
- `y: f32` - Y coordinate position
- `z: f32` - Z coordinate position

**Examples:**
```bash
# Spawn orb at origin
spacetime call system spawn_test_orb 0.0 0.0 0.0

# Spawn orb at (50, 310, 50) - just above world surface
spacetime call system spawn_test_orb 50.0 310.0 50.0

# Spawn orb at north pole of world
spacetime call system spawn_test_orb 0.0 310.0 0.0
```

**Note:** The current implementation may not support frequency and packet_count parameters directly. Check server logs for actual orb properties.

### Clear All Orbs (SQL Method)
Removes all orbs from the database. Use with caution.

**SQL Command:**
```bash
spacetime sql system "DELETE FROM wave_packet_orb"
```

⚠️ **WARNING:** This permanently deletes ALL orbs from the database.

## Mining Debug Commands

### debug_mining_status
Provides summary statistics of current mining state.

**Example:**
```bash
spacetime call system debug_mining_status
```

**Output includes:**
- Active mining sessions count
- Total players mining
- Summary statistics

### debug_wave_packet_status
Shows wave packet distribution by frequency band in storage.

**Example:**
```bash
spacetime call system debug_wave_packet_status
```

**Output includes:**
- Packet counts by frequency (Red, Yellow, Green, Cyan, Blue, Magenta)
- Total packets in storage
- Distribution percentages

## Player Debug Commands

### debug_give_crystal
Gives a crystal of specified type to the calling player.

**Parameters:**
- `crystal_type: CrystalType` - Type of crystal to give

**Example:**
```bash
# Give a crystal (check server code for valid crystal types)
spacetime call system debug_give_crystal <crystal_type>
```

### debug_reset_spawn_position
Resets calling player's position to spawn point.

**Example:**
```bash
spacetime call system debug_reset_spawn_position
```

**Output:**
- Confirms position reset
- Shows new coordinates

### debug_test_spawn_positions
Tests spawn position calculations for various world configurations.

**Example:**
```bash
spacetime call system debug_test_spawn_positions
```

**Output:**
- Tests multiple world coordinates
- Validates spawn calculations
- Shows expected vs actual positions

### debug_validate_all_players
Validates and corrects all player positions in the database.

**Example:**
```bash
spacetime call system debug_validate_all_players
```

**Output:**
- Number of players checked
- Invalid positions found
- Corrections applied

## Legacy Test Commands (if available)

### debug_test_quanta_emission
Tests quantum emission system.

**Example:**
```bash
spacetime call system debug_test_quanta_emission
```

### debug_quanta_status
Checks quantum system status.

**Example:**
```bash
spacetime call system debug_quanta_status
```

## Quick Test Scenarios

### Scenario 1: Test Basic Orb Creation
```bash
# 1. Clear existing orbs
spacetime sql system "DELETE FROM wave_packet_orb"

# 2. Spawn test orbs at different heights
spacetime call system spawn_test_orb 0.0 310.0 0.0    # Just above surface
spacetime call system spawn_test_orb 50.0 350.0 0.0   # Higher up
spacetime call system spawn_test_orb -50.0 310.0 0.0  # Different position

# 3. Check mining status
spacetime call system debug_mining_status
```

### Scenario 2: Test Player Positioning
```bash
# 1. Check all player positions
spacetime call system debug_validate_all_players

# 2. Test spawn calculations
spacetime call system debug_test_spawn_positions

# 3. Reset your position if needed
spacetime call system debug_reset_spawn_position
```

### Scenario 3: Test Mining System
```bash
# 1. Create orbs for mining
spacetime call system spawn_test_orb 0.0 310.0 0.0
spacetime call system spawn_test_orb 30.0 310.0 0.0

# 2. Give yourself a crystal
spacetime call system debug_give_crystal <type>

# 3. Check wave packet status
spacetime call system debug_wave_packet_status

# 4. Monitor mining
spacetime call system debug_mining_status
```

## Server Environments

### Local Development
```bash
# Default local server
spacetime call system <command>

# Explicit local server
spacetime call system <command> --server http://localhost:3000
```

### Test Environment
```bash
spacetime call system <command> --server https://maincloud.spacetimedb.com/system-test
```

### Production (Use with extreme caution!)
```bash
spacetime call system <command> --server https://maincloud.spacetimedb.com/system
```

## Monitoring and Logs

### View Real-time Logs
```bash
# Follow logs in real-time
spacetime logs system --follow

# View last 100 lines
spacetime logs system -n 100
```

### Check Module Status
```bash
# Get module info
spacetime describe system
```

## Important Notes

### World Coordinates
- World radius R = 300 units
- Player at north pole: (0, 300, 0)
- Orbs typically spawn at y = 310 (just above surface)

### Frequency Values
When frequency parameters are supported:
- 0 = Red (520-700 THz)
- 1 = Yellow (510-520 THz)
- 2 = Green (480-510 THz)
- 3 = Cyan (470-480 THz)
- 4 = Blue (430-470 THz)
- 5 = Magenta (380-430 THz)

### Float Parameters
Always use decimal notation for float parameters:
- ✅ Correct: `10.0`, `-300.0`, `0.0`
- ❌ Wrong: `10`, `-300`, `0`

## Safety Guidelines

⚠️ **Development Best Practices:**
1. Always test on local server first
2. Back up data before using destructive commands
3. Monitor server logs when testing
4. Document unexpected behavior
5. Never use debug commands in production unless absolutely necessary

⚠️ **Destructive Commands:**
- `DELETE FROM wave_packet_orb` - Removes ALL orbs
- `debug_validate_all_players` - May move players
- `debug_reset_spawn_position` - Moves player to spawn

## Troubleshooting

### Command Not Found
```bash
# List all available reducers
spacetime describe system | grep -i reducer
```

### Invalid Arguments
- Check parameter types match expected (f32, u32, etc.)
- Use decimal notation for floats
- Verify enum values are valid

### No Response
- Check server is running: `spacetime status`
- Verify connection: `spacetime server ping`
- Check logs for errors: `spacetime logs system`

## See Also
- [GAMEPLAY_SYSTEMS.md](./gameplay-systems-doc.md) - Mining system details
- [TECHNICAL_ARCHITECTURE.md](./technical-architecture-doc.md) - Database schema
- [CLAUDE.md](../CLAUDE.md) - Project commands reference