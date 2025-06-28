// simple_energy.rs - Simplified energy system with just frequency parameter
use spacetimedb::{SpacetimeType, Identity, ReducerContext, Table, Timestamp};
use spacetimedb::rand::Rng;
use crate::{world, player, WorldCoords, DbVector3};
use std::f32::consts::PI;

// ============================================================================
// Simple Energy Signature - Just Frequency for Now
// ============================================================================

#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq)]
pub struct SimpleEnergySignature {
    pub frequency: f32,  // 0.0-1.0: The only parameter that matters for now
}

impl SimpleEnergySignature {
    /// Generate a simple energy signature based on shell level
    pub fn generate_simple(shell_level: u8, seed: u64) -> Self {
        use spacetimedb::rand::{Rng, SeedableRng};
        use spacetimedb::rand::rngs::StdRng;
        
        let mut rng = StdRng::seed_from_u64(seed);
        
        // Each shell has its own frequency range
        let (min_freq, max_freq) = match shell_level {
            0 => (0.0, 0.3),   // Center: Red/Orange energies
            1 => (0.2, 0.5),   // Shell 1: Orange/Yellow energies  
            2 => (0.4, 0.7),   // Shell 2: Yellow/Green energies
            3 => (0.6, 0.9),   // Shell 3: Green/Blue energies
            _ => (0.8, 1.0),   // Outer shells: Blue/Violet energies
        };
        
        SimpleEnergySignature {
            frequency: rng.gen_range(min_freq..max_freq)
        }
    }
    
    /// Get the color name for UI display
    pub fn color_name(&self) -> &'static str {
        match self.frequency {
            f if f < 0.15 => "Deep Red",
            f if f < 0.3  => "Red", 
            f if f < 0.45 => "Orange",
            f if f < 0.6  => "Yellow",
            f if f < 0.75 => "Green",
            f if f < 0.9  => "Blue",
            _ => "Violet"
        }
    }
    
    /// Get broad frequency band for grouping similar energies
    pub fn frequency_band(&self) -> FrequencyBand {
        match self.frequency {
            f if f < 0.15 => FrequencyBand::DeepRed,
            f if f < 0.3  => FrequencyBand::Red,
            f if f < 0.45 => FrequencyBand::Orange,
            f if f < 0.6  => FrequencyBand::Yellow,
            f if f < 0.75 => FrequencyBand::Green,
            f if f < 0.9  => FrequencyBand::Blue,
            _ => FrequencyBand::Violet
        }
    }
    
    /// Calculate a hash for this signature (used for discovery tracking)
    pub fn calculate_hash(&self) -> u64 {
        // Simple hash based on frequency rounded to 3 decimal places
        ((self.frequency * 1000.0) as u64) << 32
    }
}

// ============================================================================
// Simple Frequency Bands for UI Grouping
// ============================================================================

#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq)]
pub enum FrequencyBand {
    DeepRed,    // 0.0-0.15
    Red,        // 0.15-0.3
    Orange,     // 0.3-0.45
    Yellow,     // 0.45-0.6
    Green,      // 0.6-0.75
    Blue,       // 0.75-0.9
    Violet,     // 0.9-1.0
}

impl FrequencyBand {
    pub fn display_name(&self) -> &'static str {
        match self {
            FrequencyBand::DeepRed => "Deep Red",
            FrequencyBand::Red => "Red",
            FrequencyBand::Orange => "Orange", 
            FrequencyBand::Yellow => "Yellow",
            FrequencyBand::Green => "Green",
            FrequencyBand::Blue => "Blue",
            FrequencyBand::Violet => "Violet",
        }
    }
    
    pub fn color_code(&self) -> (f32, f32, f32) {
        match self {
            FrequencyBand::DeepRed => (0.8, 0.1, 0.1),
            FrequencyBand::Red => (1.0, 0.2, 0.2),
            FrequencyBand::Orange => (1.0, 0.6, 0.2),
            FrequencyBand::Yellow => (1.0, 1.0, 0.2),
            FrequencyBand::Green => (0.2, 1.0, 0.2),
            FrequencyBand::Blue => (0.2, 0.2, 1.0),
            FrequencyBand::Violet => (0.8, 0.2, 1.0),
        }
    }
}

// ============================================================================
// Database Tables for Simple Energy System
// ============================================================================

#[spacetimedb::table(name = simple_energy_orb, public)]
#[derive(Clone)]
pub struct SimpleEnergyOrb {
    #[primary_key]
    #[auto_inc]
    pub orb_id: u64,
    pub world_coords: WorldCoords,
    pub position: DbVector3,
    pub velocity: DbVector3,
    pub energy_signature: SimpleEnergySignature,
    pub quantum_count: u32,
    pub creation_time: u64,
}

#[spacetimedb::table(name = simple_energy_storage, public)]
#[derive(Clone)]
pub struct SimpleEnergyStorage {
    #[primary_key]
    #[auto_inc]
    pub storage_id: u64,
    pub owner_type: String,     // "player", "device", etc.
    pub owner_id: u64,          // Player ID or device ID
    pub energy_signature: SimpleEnergySignature,
    pub quantum_count: u32,
    pub last_update: u64,
}

#[spacetimedb::table(name = simple_energy_discovery, public)]
#[derive(Clone)]
pub struct SimpleEnergyDiscovery {
    #[primary_key]
    pub signature_hash: u64,
    pub signature: SimpleEnergySignature,
    pub display_name: String,
    pub discovered_by: Identity,
    pub discovery_time: u64,
    pub shell_origin: u8,
    pub discovery_count: u32,
}

// ============================================================================
// Simple Energy Generation Functions
// ============================================================================

#[spacetimedb::reducer]
pub fn emit_simple_energy_orb(
    ctx: &ReducerContext,
    world_coords: WorldCoords,
    circuit_position: DbVector3,
) -> Result<(), String> {
    // Get world info for shell level - use iter() and find instead of indexed lookup
    let world = ctx.db.world().iter()
        .find(|w| w.world_coords == world_coords)
        .ok_or("World not found")?;
    
    // Generate unique seed
    let seed = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64
        ^ ((world_coords.x as u64) << 16)
        ^ ((world_coords.y as u64) << 8)
        ^ (world_coords.z as u64);
    
    // Generate simple energy signature based on shell level
    let signature = SimpleEnergySignature::generate_simple(world.shell_level, seed);
    
    // Calculate orb trajectory (volcano-style emission)
    let angle = ctx.rng().gen::<f32>() * 2.0 * PI;
    let horizontal_speed = 15.0 + ctx.rng().gen::<f32>() * 10.0;
    let vertical_speed = 20.0 + ctx.rng().gen::<f32>() * 15.0;
    
    let velocity = DbVector3::new(
        angle.cos() * horizontal_speed,
        vertical_speed,
        angle.sin() * horizontal_speed,
    );
    
    // Create the energy orb
    let orb = SimpleEnergyOrb {
        orb_id: 0,
        world_coords,
        position: circuit_position,
        velocity,
        energy_signature: signature,
        quantum_count: 100, // Standard orb contains 100 quanta
        creation_time: ctx.timestamp
            .duration_since(Timestamp::UNIX_EPOCH)
            .expect("Valid timestamp")
            .as_millis() as u64,
    };
    
    // Store values for logging before moving orb
    let orb_quantum_count = orb.quantum_count;
    
    ctx.db.simple_energy_orb().insert(orb);
    
    // Track discovery if this is a new signature
    check_and_record_simple_discovery(ctx, &signature, world.shell_level)?;
    
    log::info!(
        "Emitted simple energy orb in world ({},{},{}) with {} energy at frequency {}",
        world_coords.x, world_coords.y, world_coords.z,
        orb_quantum_count, signature.frequency
    );
    
    Ok(())
}

#[spacetimedb::reducer]
pub fn collect_simple_energy_orb(
    ctx: &ReducerContext,
    orb_id: u64,
    collector_player_id: u32,
) -> Result<(), String> {
    // Find the orb - use iter() and find instead of indexed lookup
    let orb = ctx.db.simple_energy_orb().iter()
        .find(|o| o.orb_id == orb_id)
        .ok_or("Energy orb not found")?;
    
    // Check if player exists - use iter() and find instead of indexed lookup
    let player = ctx.db.player().iter()
        .find(|p| p.player_id == collector_player_id)
        .ok_or("Player not found")?;
    
    // Verify player is the sender
    if player.identity != ctx.sender {
        return Err("Not your player".to_string());
    }
    
    // Add energy to player's storage
    add_energy_to_storage(
        ctx,
        "player".to_string(),
        collector_player_id as u64,
        orb.energy_signature,
        orb.quantum_count,
    )?;
    
    // Store values before deleting orb
    let orb_quantum_count = orb.quantum_count;
    let orb_signature = orb.energy_signature;
    
    // Remove the orb
    ctx.db.simple_energy_orb().delete(orb);
    
    log::info!(
        "Player {} collected {} {} energy",
        player.name, orb_quantum_count, orb_signature.color_name()
    );
    
    Ok(())
}

// ============================================================================
// Storage Management Functions
// ============================================================================

pub fn add_energy_to_storage(
    ctx: &ReducerContext,
    owner_type: String,
    owner_id: u64,
    signature: SimpleEnergySignature,
    amount: u32,
) -> Result<(), String> {
    // Check if we already have storage for this exact signature
    let existing_storage = ctx.db.simple_energy_storage().iter()
        .find(|storage| {
            storage.owner_type == owner_type &&
            storage.owner_id == owner_id &&
            storage.energy_signature == signature
        });
    
    if let Some(storage) = existing_storage {
        // Add to existing storage
        let mut updated_storage = storage.clone();
        updated_storage.quantum_count += amount;
        updated_storage.last_update = ctx.timestamp
            .duration_since(Timestamp::UNIX_EPOCH)
            .expect("Valid timestamp")
            .as_millis() as u64;
        
        ctx.db.simple_energy_storage().delete(storage);
        ctx.db.simple_energy_storage().insert(updated_storage);
    } else {
        // Create new storage entry
        let new_storage = SimpleEnergyStorage {
            storage_id: 0,
            owner_type,
            owner_id,
            energy_signature: signature,
            quantum_count: amount,
            last_update: ctx.timestamp
                .duration_since(Timestamp::UNIX_EPOCH)
                .expect("Valid timestamp")
                .as_millis() as u64,
        };
        
        ctx.db.simple_energy_storage().insert(new_storage);
    }
    
    Ok(())
}

pub fn remove_energy_from_storage(
    ctx: &ReducerContext,
    owner_type: String,
    owner_id: u64,
    signature: SimpleEnergySignature,
    amount: u32,
) -> Result<(), String> {
    // Find storage for this signature
    let storage = ctx.db.simple_energy_storage().iter()
        .find(|storage| {
            storage.owner_type == owner_type &&
            storage.owner_id == owner_id &&
            storage.energy_signature == signature
        })
        .ok_or("Energy storage not found")?;
    
    if storage.quantum_count < amount {
        return Err("Insufficient energy in storage".to_string());
    }
    
    // Update or delete storage
    if storage.quantum_count == amount {
        // Remove entire storage entry
        ctx.db.simple_energy_storage().delete(storage);
    } else {
        // Reduce amount
        let mut updated_storage = storage.clone();
        updated_storage.quantum_count -= amount;
        updated_storage.last_update = ctx.timestamp
            .duration_since(Timestamp::UNIX_EPOCH)
            .expect("Valid timestamp")
            .as_millis() as u64;
        
        ctx.db.simple_energy_storage().delete(storage);
        ctx.db.simple_energy_storage().insert(updated_storage);
    }
    
    Ok(())
}

// ============================================================================
// Discovery System
// ============================================================================

fn check_and_record_simple_discovery(
    ctx: &ReducerContext,
    signature: &SimpleEnergySignature,
    shell_level: u8,
) -> Result<(), String> {
    let signature_hash = signature.calculate_hash();
    
    if let Some(discovery) = ctx.db.simple_energy_discovery().iter()
        .find(|d| d.signature_hash == signature_hash) {
        // Update existing discovery
        let mut updated_discovery = discovery.clone();
        updated_discovery.discovery_count += 1;
        ctx.db.simple_energy_discovery().delete(discovery);
        ctx.db.simple_energy_discovery().insert(updated_discovery);
    } else {
        // New discovery!
        let discovery = SimpleEnergyDiscovery {
            signature_hash,
            signature: *signature,
            display_name: format!("{} Energy", signature.color_name()),
            discovered_by: ctx.sender,
            discovery_time: ctx.timestamp
                .duration_since(Timestamp::UNIX_EPOCH)
                .expect("Valid timestamp")
                .as_millis() as u64,
            shell_origin: shell_level,
            discovery_count: 1,
        };
        
        let discovery_name = discovery.display_name.clone();
        ctx.db.simple_energy_discovery().insert(discovery);
        
        log::info!(
            "NEW ENERGY DISCOVERED: {} by player from shell {}",
            discovery_name, shell_level
        );
    }
    
    Ok(())
}

// ============================================================================
// Query Functions for Client
// ============================================================================

#[spacetimedb::reducer]
pub fn get_player_energy_inventory(
    _ctx: &ReducerContext,
    player_id: u32,
) -> Result<(), String> {
    // This reducer will be called by client to trigger inventory updates
    // The actual data will be sent via subscriptions
    
    log::info!("Energy inventory requested for player {}", player_id);
    Ok(())
}

// ============================================================================
// Debug and Testing Functions
// ============================================================================

#[spacetimedb::reducer]
pub fn debug_spawn_simple_energy_orb(
    ctx: &ReducerContext,
    world_coords: WorldCoords,
    frequency: f32,
    quantum_count: u32,
) -> Result<(), String> {
    // Debug function to manually spawn energy orbs for testing
    let signature = SimpleEnergySignature { frequency };
    let position = DbVector3::new(0.0, 50.0, 0.0); // Spawn above center
    let velocity = DbVector3::new(
        (ctx.rng().gen::<f32>() - 0.5) * 20.0,
        -10.0,
        (ctx.rng().gen::<f32>() - 0.5) * 20.0,
    );
    
    let orb = SimpleEnergyOrb {
        orb_id: 0,
        world_coords,
        position,
        velocity,
        energy_signature: signature,
        quantum_count,
        creation_time: ctx.timestamp
            .duration_since(Timestamp::UNIX_EPOCH)
            .expect("Valid timestamp")
            .as_millis() as u64,
    };
    
    ctx.db.simple_energy_orb().insert(orb);
    
    log::info!(
        "Debug spawned {} energy orb with {} quanta",
        signature.color_name(),
        quantum_count
    );
    
    Ok(())
}

#[spacetimedb::reducer] 
pub fn debug_simple_energy_system_status(ctx: &ReducerContext) -> Result<(), String> {
    let orb_count = ctx.db.simple_energy_orb().iter().count();
    let storage_count = ctx.db.simple_energy_storage().iter().count();
    let discovery_count = ctx.db.simple_energy_discovery().iter().count();
    
    log::info!(
        "Simple Energy System Status: {} orbs, {} storage entries, {} discoveries",
        orb_count, storage_count, discovery_count
    );
    
    Ok(())
}