pub mod math;

use spacetimedb::{Identity, SpacetimeType, ReducerContext, Table, ScheduleAt};
use spacetimedb::rand::Rng;
use std::time::Duration;

// 3D vector for positions in the spherical world
#[derive(SpacetimeType, Debug, Clone, Copy)]
pub struct DbVector3 {
    pub x: f32,
    pub y: f32,
    pub z: f32,
}

impl DbVector3 {
    pub fn new(x: f32, y: f32, z: f32) -> Self {
        Self { x, y, z }
    }

    pub fn magnitude(&self) -> f32 {
        (self.x * self.x + self.y * self.y + self.z * self.z).sqrt()
    }

    pub fn normalized(self) -> DbVector3 {
        let mag = self.magnitude();
        if mag > 0.0 {
            DbVector3::new(self.x / mag, self.y / mag, self.z / mag)
        } else {
            DbVector3::new(0.0, 0.0, 0.0)
        }
    }
}

// Add multiplication operator for DbVector3
impl std::ops::Mul<f32> for DbVector3 {
    type Output = DbVector3;

    fn mul(self, scalar: f32) -> DbVector3 {
        DbVector3::new(self.x * scalar, self.y * scalar, self.z * scalar)
    }
}

// World coordinates as a proper SpacetimeDB type
#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq)]
pub struct WorldCoords {
    pub x: i8,
    pub y: i8,
    pub z: i8,
}

impl WorldCoords {
    pub fn new(x: i8, y: i8, z: i8) -> Self {
        Self { x, y, z }
    }
    
    pub fn center() -> Self {
        Self { x: 0, y: 0, z: 0 }
    }
}

// Energy types - start simple with RGB
#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq)]
pub enum EnergyType {
    Red,
    Green,
    Blue,
}

// World definition - for now just the center world
#[spacetimedb::table(name = world, public)]
#[derive(Debug, Clone)]
pub struct World {
    #[primary_key]
    pub world_coords: WorldCoords,        // WorldCoords instead of tuple
    pub shell_level: u8,                  // 0 for center
    pub radius: f32,                      // Sphere radius
    pub circuit_qubit_count: u8,          // 1 for center world
    pub status: String,                   // "Active", "Potential", etc.
}

// Player with 3D position on sphere surface
#[spacetimedb::table(name = player, public)]
#[derive(Debug, Clone)]
pub struct Player {
    #[primary_key]
    pub identity: Identity,
    #[unique]
    #[auto_inc]
    pub player_id: u32,
    pub name: String,
    pub current_world: WorldCoords,       // Which world they're in
    pub position: DbVector3,              // Position on sphere surface
    pub energy_red: f32,                  // Inventory counts
    pub energy_green: f32,
    pub energy_blue: f32,
}

// Energy puddles on the world surface
#[spacetimedb::table(name = energy_puddle, public)]
#[derive(Debug, Clone)]
pub struct EnergyPuddle {
    #[primary_key]
    #[auto_inc]
    pub puddle_id: u64,
    pub world_coords: WorldCoords,        // Which world
    pub position: DbVector3,              // Position on sphere surface
    pub energy_type: EnergyType,
    pub current_amount: f32,
    pub max_amount: f32,
}

// Player-built miners
#[spacetimedb::table(name = miner_device, public)]
#[derive(Debug, Clone)]
pub struct MinerDevice {
    #[primary_key]
    #[auto_inc]
    pub miner_id: u64,
    pub owner_identity: Identity,
    pub world_coords: WorldCoords,
    pub position: DbVector3,
    pub target_puddle_id: Option<u64>,  // Which puddle it's mining
    pub efficiency_bonus: f32,          // 1.0 = 100%, 1.5 = 150% with quantum bonus
    pub energy_red: f32,                // Miner's storage
    pub energy_green: f32,
    pub energy_blue: f32,
    pub storage_capacity: f32,
}

// Storage devices
#[spacetimedb::table(name = storage_device, public)]
#[derive(Debug, Clone)]
pub struct StorageDevice {
    #[primary_key]
    #[auto_inc]
    pub storage_id: u64,
    pub owner_identity: Identity,
    pub world_coords: WorldCoords,
    pub position: DbVector3,
    pub energy_red: f32,
    pub energy_green: f32,
    pub energy_blue: f32,
    pub storage_capacity: f32,
}

// Active energy transfers between devices
#[spacetimedb::table(name = energy_transfer, public)]
#[derive(Debug, Clone)]
pub struct EnergyTransfer {
    #[primary_key]
    #[auto_inc]
    pub transfer_id: u64,
    pub from_device_type: String,     // "miner", "storage", "player"
    pub from_device_id: u64,
    pub to_device_type: String,
    pub to_device_id: u64,
    pub energy_type: EnergyType,
    pub transfer_rate: f32,           // energy per second
    pub is_continuous: bool,
}

// World circuit configuration (simplified for phase 1)
#[spacetimedb::table(name = world_circuit, public)]
#[derive(Debug, Clone)]
pub struct WorldCircuit {
    #[primary_key]
    pub world_coords: WorldCoords,
    pub qubit_count: u8,
    pub emission_interval_ms: u64,    // How often it emits energy orbs
    pub orbs_per_emission: u32,       // How many orbs per emission
    pub last_emission_time: u64,      // Timestamp of last emission
}

// Active energy orbs falling from circuit to ground
#[spacetimedb::table(name = energy_orb, public)]
#[derive(Debug, Clone)]
pub struct EnergyOrb {
    #[primary_key]
    #[auto_inc]
    pub orb_id: u64,
    pub world_coords: WorldCoords,
    pub position: DbVector3,          // Current position
    pub velocity: DbVector3,          // Fall direction/speed
    pub energy_type: EnergyType,
    pub energy_amount: f32,
    pub creation_time: u64,
}

/// Timer for scheduled events
#[spacetimedb::table(name = tick_timer, scheduled(tick))]
pub struct TickTimer {
    #[primary_key]
    #[auto_inc]
    pub scheduled_id: u64,
    pub scheduled_at: spacetimedb::ScheduleAt,
}

#[spacetimedb::table(name = game_settings, public)]
pub struct GameSettings {
    #[primary_key]
    id: u32,
    tick_ms: u64,
    max_players: u32,
}

#[spacetimedb::table(name = logged_out_player)]
#[derive(Debug, Clone)]
pub struct LoggedOutPlayer {
    #[primary_key]
    identity: Identity,
    player_data: Player,
}

// Initialize the game world
#[spacetimedb::reducer(init)]
pub fn init(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("Initializing quantum metaverse...");
    
    // Create game settings
    if ctx.db.game_settings().iter().next().is_none() {
        ctx.db.game_settings().insert(GameSettings {
            id: 0,
            tick_ms: 100,  // 100ms ticks = 10hz
            max_players: 100,
        });
    }
    
    // Create the center world (0,0,0)
    ctx.db.world().insert(World {
        world_coords: WorldCoords::center(),
        shell_level: 0,
        radius: 100.0,  // 100 unit radius sphere
        circuit_qubit_count: 1,
        status: "Active".to_string(),
    });
    
    // Create the world circuit for center world
    ctx.db.world_circuit().insert(WorldCircuit {
        world_coords: WorldCoords::center(),
        qubit_count: 1,
        emission_interval_ms: 5000,  // Emit energy every 5 seconds
        orbs_per_emission: 6,        // 6 orbs per emission (2 of each color)
        last_emission_time: 0,
    });
    
    // Start the tick timer
    ctx.db.tick_timer().try_insert(TickTimer {
        scheduled_id: 0,
        scheduled_at: ScheduleAt::Interval(Duration::from_millis(100).into()),
    })?;
    
    log::info!("Center world initialized at (0,0,0) with radius 100");
    Ok(())
}

// Main game tick - handles orb physics and circuit emissions
#[spacetimedb::reducer]
pub fn tick(ctx: &ReducerContext, _timer: TickTimer) -> Result<(), String> {
    // Use a simple timestamp approach - SpacetimeDB handles timing internally
    let current_time = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .unwrap()
        .as_millis() as u64;
    
    // Update falling orbs
    update_falling_orbs(ctx, current_time)?;
    
    // Check for circuit emissions
    emit_energy_orbs(ctx, current_time)?;
    
    Ok(())
}

fn update_falling_orbs(ctx: &ReducerContext, current_time: u64) -> Result<(), String> {
    let orbs: Vec<EnergyOrb> = ctx.db.energy_orb().iter().collect();
    
    for orb in orbs {
        let fall_time = (current_time - orb.creation_time) as f32 / 1000.0; // seconds
        let new_position = DbVector3::new(
            orb.position.x + orb.velocity.x * fall_time,
            orb.position.y + orb.velocity.y * fall_time,
            orb.position.z + orb.velocity.z * fall_time,
        );
        
        // Check if orb hit the ground (sphere surface)
        if new_position.magnitude() <= 100.0 {  // Hit the sphere surface
            // Create a puddle at impact point
            create_puddle_from_orb(ctx, &orb, new_position)?;
            // Remove the orb
            ctx.db.energy_orb().delete(orb);
        } else {
            // Update orb position - clone data before deleting
            let orb_data = orb.clone();
            ctx.db.energy_orb().delete(orb);
            let updated_orb = EnergyOrb {
                orb_id: 0, // Will get new auto_inc ID
                world_coords: orb_data.world_coords,
                position: new_position,
                velocity: orb_data.velocity,
                energy_type: orb_data.energy_type,
                energy_amount: orb_data.energy_amount,
                creation_time: orb_data.creation_time,
            };
            ctx.db.energy_orb().insert(updated_orb);
        }
    }
    
    Ok(())
}

fn emit_energy_orbs(ctx: &ReducerContext, current_time: u64) -> Result<(), String> {
    let circuits: Vec<WorldCircuit> = ctx.db.world_circuit().iter().collect();
    
    for circuit in circuits {
        if current_time - circuit.last_emission_time >= circuit.emission_interval_ms {
            // Time to emit new orbs
            emit_orbs_for_circuit(ctx, &circuit, current_time)?;
            
            // Update last emission time - clone data before deleting
            let circuit_data = circuit.clone();
            ctx.db.world_circuit().delete(circuit);
            let updated_circuit = WorldCircuit {
                world_coords: circuit_data.world_coords,
                qubit_count: circuit_data.qubit_count,
                emission_interval_ms: circuit_data.emission_interval_ms,
                orbs_per_emission: circuit_data.orbs_per_emission,
                last_emission_time: current_time,
            };
            ctx.db.world_circuit().insert(updated_circuit);
        }
    }
    
    Ok(())
}

fn emit_orbs_for_circuit(ctx: &ReducerContext, circuit: &WorldCircuit, current_time: u64) -> Result<(), String> {
    let energy_types = [EnergyType::Red, EnergyType::Green, EnergyType::Blue];
    
    for i in 0..circuit.orbs_per_emission {
        let energy_type = energy_types[(i as usize) % energy_types.len()];
        
        // Random direction from center of sphere outward using SpacetimeDB's RNG
        let theta = ctx.rng().gen::<f32>() * 2.0 * std::f32::consts::PI;
        let phi = ctx.rng().gen::<f32>() * std::f32::consts::PI;
        
        let direction = DbVector3::new(
            phi.sin() * theta.cos(),
            phi.sin() * theta.sin(),
            phi.cos(),
        );
        
        // Start at center, move outward
        let start_position = DbVector3::new(0.0, 0.0, 0.0);
        let velocity = direction * 20.0; // 20 units per second fall speed
        
        ctx.db.energy_orb().insert(EnergyOrb {
            orb_id: 0, // auto_inc
            world_coords: circuit.world_coords,
            position: start_position,
            velocity,
            energy_type,
            energy_amount: 10.0,
            creation_time: current_time,
        });
    }
    
    log::info!("Emitted {} energy orbs for world {:?}", circuit.orbs_per_emission, circuit.world_coords);
    Ok(())
}

fn create_puddle_from_orb(ctx: &ReducerContext, orb: &EnergyOrb, impact_position: DbVector3) -> Result<(), String> {
    // Normalize position to sphere surface
    let surface_position = impact_position.normalized() * 100.0;
    
    ctx.db.energy_puddle().insert(EnergyPuddle {
        puddle_id: 0, // auto_inc
        world_coords: orb.world_coords,
        position: surface_position,
        energy_type: orb.energy_type,
        current_amount: orb.energy_amount,
        max_amount: orb.energy_amount,
    });
    
    Ok(())
}

// Connection handling
#[spacetimedb::reducer(client_connected)]
pub fn connect(ctx: &ReducerContext) -> Result<(), String> {
    // Restore logged out player if exists
    if let Some(logged_out) = ctx.db.logged_out_player().identity().find(&ctx.sender) {
        ctx.db.player().insert(logged_out.player_data);
        ctx.db.logged_out_player().identity().delete(&ctx.sender);
        log::info!("Restored logged out player: {}", ctx.sender);
    }
    
    log::info!("Client connected: {}. Waiting for enter_game.", ctx.sender);
    Ok(())
}

#[spacetimedb::reducer(client_disconnected)]
pub fn disconnect(ctx: &ReducerContext) -> Result<(), String> {
    if let Some(player) = ctx.db.player().identity().find(&ctx.sender) {
        let logged_out = LoggedOutPlayer {
            identity: ctx.sender,
            player_data: player.clone(),
        };
        ctx.db.logged_out_player().insert(logged_out);
        ctx.db.player().identity().delete(&ctx.sender);
        log::info!("Player disconnected: {}", player.name);
    }
    Ok(())
}

#[spacetimedb::reducer]
pub fn enter_game(ctx: &ReducerContext, name: String) -> Result<(), String> {
    if name.trim().is_empty() || name.len() > 32 {
        return Err("Invalid name".to_string());
    }

    // Check if player already exists
    if let Some(player) = ctx.db.player().identity().find(ctx.sender) {
        // Update existing player's name - clone data before deleting
        let player_data = player.clone();
        ctx.db.player().delete(player);
        let updated_player = Player {
            identity: player_data.identity,
            player_id: player_data.player_id,
            name,
            current_world: player_data.current_world,
            position: player_data.position,
            energy_red: player_data.energy_red,
            energy_green: player_data.energy_green,
            energy_blue: player_data.energy_blue,
        };
        ctx.db.player().insert(updated_player);
        return Ok(());
    }

    // Create new player at random position on sphere surface
    let theta = ctx.rng().gen::<f32>() * 2.0 * std::f32::consts::PI;
    let phi = ctx.rng().gen::<f32>() * std::f32::consts::PI;
    
    let spawn_position = DbVector3::new(
        100.0 * phi.sin() * theta.cos(),  // On sphere surface (radius 100)
        100.0 * phi.sin() * theta.sin(),
        100.0 * phi.cos(),
    );

    let new_player = Player {
        identity: ctx.sender,
        player_id: 0, // auto_inc
        name,
        current_world: WorldCoords::center(),  // Start in center world
        position: spawn_position,
        energy_red: 0.0,
        energy_green: 0.0,
        energy_blue: 0.0,
    };
    
    ctx.db.player().try_insert(new_player)?;
    log::info!("Created new player at position {:?}", spawn_position);
    
    Ok(())
}