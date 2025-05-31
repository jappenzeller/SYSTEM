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

// Energy types - expandable for different shells
#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq)]
pub enum EnergyType {
    Red,
    Green,
    Blue,
    // Shell 1 colors (will be added later)
    Cyan,    // Green + Blue
    Magenta, // Red + Blue  
    Yellow,  // Red + Green
}

// Generic energy storage entry
#[derive(SpacetimeType, Debug, Clone)]
pub struct EnergyAmount {
    pub energy_type: EnergyType,
    pub amount: f32,
}

// Energy storage table for devices and players
#[spacetimedb::table(name = energy_storage, public)]
#[derive(Debug, Clone)]
pub struct EnergyStorage {
    #[primary_key]
    #[auto_inc]
    pub storage_entry_id: u64,
    pub owner_type: String,           // "player", "miner", "storage", "sphere"
    pub owner_id: u64,               // player_id, miner_id, etc.
    pub energy_type: EnergyType,
    pub amount: f32,
    pub capacity: f32,               // Max amount for this energy type
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
    pub current_world: WorldCoords,   // Which world they're in
    pub position: DbVector3,          // Position on sphere surface
    pub inventory_capacity: f32,      // Total energy capacity
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
    pub storage_capacity: f32,          // Total energy storage capacity
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
    pub storage_capacity: f32,          // Total energy storage capacity
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
    pub route_spheres: Vec<u64>,      // Sphere IDs for routing path
    pub total_cost_per_unit: f32,     // Cost for cross-world transfers
}

// Distribution spheres for energy logistics network
#[spacetimedb::table(name = distribution_sphere, public)]
#[derive(Debug, Clone)]
pub struct DistributionSphere {
    #[primary_key]
    #[auto_inc]
    pub sphere_id: u64,
    pub world_coords: WorldCoords,
    pub position: DbVector3,          // Position in 3D space
    pub coverage_radius: f32,         // Range for device connections
    pub tunnel_id: Option<u64>,       // Associated tunnel (None for world center)
    pub buffer_capacity: f32,         // Total energy buffer capacity
}

// Tunnels connecting worlds in the metaverse
#[spacetimedb::table(name = tunnel, public)]
#[derive(Debug, Clone)]
pub struct Tunnel {
    #[primary_key]
    #[auto_inc]
    pub tunnel_id: u64,
    pub from_world: WorldCoords,      // Always center world initially
    pub to_world: WorldCoords,        // Target world coordinates
    pub activation_progress: f32,     // 0.0 to 1.0
    pub activation_threshold: f32,    // Energy required to activate
    pub status: String,               // "Potential", "Activating", "Active"
    pub transfer_cost_multiplier: f32, // Cost for cross-tunnel transfers
}

// Device connections to distribution spheres
#[spacetimedb::table(name = device_connection, public)]
#[derive(Debug, Clone)]
pub struct DeviceConnection {
    #[primary_key]
    #[auto_inc]
    pub connection_id: u64,
    pub device_id: u64,
    pub device_type: String,          // "miner", "storage", "player"
    pub sphere_id: u64,
    pub connection_strength: f32,     // Distance-based connection quality
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
    
    // Create central distribution sphere for the center world
    ctx.db.distribution_sphere().insert(DistributionSphere {
        sphere_id: 0, // auto_inc
        world_coords: WorldCoords::center(),
        position: DbVector3::new(0.0, 120.0, 0.0), // Floating above center
        coverage_radius: 150.0,      // Covers most of the world
        tunnel_id: None,             // Central sphere, no tunnel
        buffer_capacity: 1000.0,
    });
    
    // Create potential tunnels to Shell 1 worlds (26 tunnels)
    create_shell1_tunnels(ctx)?;
    
    // Start the tick timer
    ctx.db.tick_timer().try_insert(TickTimer {
        scheduled_id: 0,
        scheduled_at: ScheduleAt::Interval(Duration::from_millis(100).into()),
    })?;
    
    log::info!("Center world initialized at (0,0,0) with radius 100");
    Ok(())
}

// Create all 26 potential tunnels from center to Shell 1 worlds
fn create_shell1_tunnels(ctx: &ReducerContext) -> Result<(), String> {
    for x in -1..=1 {
        for y in -1..=1 {
            for z in -1..=1 {
                // Skip the center world (0,0,0)
                if x == 0 && y == 0 && z == 0 {
                    continue;
                }
                
                let target_world = WorldCoords::new(x, y, z);
                
                // Create tunnel
                let tunnel = Tunnel {
                    tunnel_id: 0, // auto_inc
                    from_world: WorldCoords::center(),
                    to_world: target_world,
                    activation_progress: 0.0,
                    activation_threshold: 500.0, // 500 energy units needed to activate
                    status: "Potential".to_string(),
                    transfer_cost_multiplier: 2.0, // 2x cost for cross-world transfers
                };
                ctx.db.tunnel().insert(tunnel);
                
                // Create potential world
                let potential_world = World {
                    world_coords: target_world,
                    shell_level: 1,
                    radius: 80.0, // Slightly smaller than center world
                    circuit_qubit_count: 2, // 2 qubits for Shell 1
                    status: "Potential".to_string(),
                };
                ctx.db.world().insert(potential_world);
            }
        }
    }
    
    log::info!("Created 26 potential tunnels to Shell 1 worlds");
    Ok(())
}

// Main game tick - handles orb physics, circuit emissions, and device connections
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
    
    // Update device connections to distribution spheres
    update_device_connections(ctx)?;
    
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
            inventory_capacity: player_data.inventory_capacity,
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
        inventory_capacity: 100.0,             // Starting inventory capacity
    };
    
    // Insert player and get the generated ID
    ctx.db.player().try_insert(new_player.clone())?;
    
    // Find the inserted player to get the actual player_id (auto_inc generates it)
    let inserted_player = ctx.db.player().identity().find(ctx.sender)
        .ok_or("Failed to find inserted player")?;
    
    // Initialize player's energy storage (start with empty inventory)
    for energy_type in [EnergyType::Red, EnergyType::Green, EnergyType::Blue] {
        ctx.db.energy_storage().insert(EnergyStorage {
            storage_entry_id: 0, // auto_inc
            owner_type: "player".to_string(),
            owner_id: inserted_player.player_id as u64,
            energy_type,
            amount: 0.0,
            capacity: 33.0, // Divide capacity among energy types
        });
    }
    log::info!("Created new player at position {:?}", spawn_position);
    
    Ok(())
}

// Update device connections to distribution spheres
fn update_device_connections(ctx: &ReducerContext) -> Result<(), String> {
    // Get all distribution spheres
    let spheres: Vec<DistributionSphere> = ctx.db.distribution_sphere().iter().collect();
    
    // Connect miners to spheres
    let miners: Vec<MinerDevice> = ctx.db.miner_device().iter().collect();
    for miner in miners {
        ensure_device_connected(ctx, miner.miner_id, "miner", &miner.world_coords, &miner.position, &spheres)?;
    }
    
    // Connect storage devices to spheres
    let storage_devices: Vec<StorageDevice> = ctx.db.storage_device().iter().collect();
    for storage in storage_devices {
        ensure_device_connected(ctx, storage.storage_id, "storage", &storage.world_coords, &storage.position, &spheres)?;
    }
    
    // Connect players to spheres
    let players: Vec<Player> = ctx.db.player().iter().collect();
    for player in players {
        ensure_device_connected(ctx, player.player_id as u64, "player", &player.current_world, &player.position, &spheres)?;
    }
    
    Ok(())
}

// Ensure a device is connected to the nearest distribution sphere
fn ensure_device_connected(
    ctx: &ReducerContext,
    device_id: u64,
    device_type: &str,
    world_coords: &WorldCoords,
    position: &DbVector3,
    spheres: &[DistributionSphere],
) -> Result<(), String> {
    // Find the nearest sphere in the same world
    let mut nearest_sphere: Option<&DistributionSphere> = None;
    let mut nearest_distance = f32::MAX;
    
    for sphere in spheres {
        if sphere.world_coords == *world_coords {
            let distance = distance_3d(position, &sphere.position);
            if distance <= sphere.coverage_radius && distance < nearest_distance {
                nearest_distance = distance;
                nearest_sphere = Some(sphere);
            }
        }
    }
    
    // Check if device is already connected
    let existing_connections: Vec<DeviceConnection> = ctx.db.device_connection()
        .iter()
        .filter(|conn| conn.device_id == device_id && conn.device_type == device_type)
        .collect();
    
    if let Some(sphere) = nearest_sphere {
        // Check if already connected to this sphere
        let already_connected = existing_connections
            .iter()
            .any(|conn| conn.sphere_id == sphere.sphere_id);
            
        if !already_connected {
            // Remove old connections
            for old_conn in existing_connections {
                ctx.db.device_connection().delete(old_conn);
            }
            
            // Create new connection
            let connection_strength = 1.0 - (nearest_distance / sphere.coverage_radius);
            ctx.db.device_connection().insert(DeviceConnection {
                connection_id: 0, // auto_inc
                device_id,
                device_type: device_type.to_string(),
                sphere_id: sphere.sphere_id,
                connection_strength,
            });
        }
    } else {
        // Device is out of range, remove any existing connections
        for old_conn in existing_connections {
            ctx.db.device_connection().delete(old_conn);
        }
    }
    
    Ok(())
}

// Helper function to calculate 3D distance
fn distance_3d(pos1: &DbVector3, pos2: &DbVector3) -> f32 {
    let dx = pos1.x - pos2.x;
    let dy = pos1.y - pos2.y;
    let dz = pos1.z - pos2.z;
    (dx * dx + dy * dy + dz * dz).sqrt()
}

// Reducer for players to activate tunnels by spending energy near them
#[spacetimedb::reducer]
pub fn activate_tunnel(ctx: &ReducerContext, tunnel_id: u64, energy_amount: f32) -> Result<(), String> {
    // Find the player
    let player = ctx.db.player().identity().find(ctx.sender)
        .ok_or("Player not found")?;
    
    // Find the tunnel
    let tunnels: Vec<Tunnel> = ctx.db.tunnel().iter().collect();
    let tunnel = tunnels.iter()
        .find(|t| t.tunnel_id == tunnel_id)
        .ok_or("Tunnel not found")?;
    
    if tunnel.status != "Potential" && tunnel.status != "Activating" {
        return Err("Tunnel cannot be activated".to_string());
    }
    
    // Check if player has enough energy (simplified - just check red energy for now)
    let player_storage: Vec<EnergyStorage> = ctx.db.energy_storage()
        .iter()
        .filter(|storage| storage.owner_type == "player" && storage.owner_id == player.player_id as u64 && storage.energy_type == EnergyType::Red)
        .collect();
    
    let current_red_energy = player_storage.first().map(|s| s.amount).unwrap_or(0.0);
    if current_red_energy < energy_amount {
        return Err("Not enough energy".to_string());
    }
    
    // Update player energy storage
    if let Some(storage) = player_storage.first() {
        let storage_data = storage.clone();
        ctx.db.energy_storage().delete(storage.clone());
        let updated_storage = EnergyStorage {
            amount: storage_data.amount - energy_amount,
            ..storage_data
        };
        ctx.db.energy_storage().insert(updated_storage);
    }
    
    // Update tunnel progress
    let tunnel_data = tunnel.clone();
    ctx.db.tunnel().delete(tunnel.clone());
    let new_progress = tunnel_data.activation_progress + energy_amount;
    
    let (new_status, should_create_world) = if new_progress >= tunnel_data.activation_threshold {
        ("Active".to_string(), true)
    } else {
        ("Activating".to_string(), false)
    };
    
    let updated_tunnel = Tunnel {
        activation_progress: new_progress.min(tunnel_data.activation_threshold),
        status: new_status,
        ..tunnel_data
    };
    
    // Store values for logging before moving
    let progress_for_log = updated_tunnel.activation_progress;
    let threshold_for_log = updated_tunnel.activation_threshold;
    
    ctx.db.tunnel().insert(updated_tunnel);
    
    // If tunnel is now active, create the target world and its systems
    if should_create_world {
        let tunnel_for_world_creation = Tunnel {
            activation_progress: progress_for_log,
            status: if should_create_world { "Active".to_string() } else { "Activating".to_string() },
            ..tunnel_data
        };
        activate_target_world(ctx, &tunnel_for_world_creation)?;
    }
    
    log::info!("Player activated tunnel {} with {} energy. Progress: {}/{}",
        tunnel_id, energy_amount, progress_for_log, threshold_for_log);
    
    Ok(())
}

// Create the target world when a tunnel is activated
fn activate_target_world(ctx: &ReducerContext, tunnel: &Tunnel) -> Result<(), String> {
    // Find and update world status to Active
    let worlds: Vec<World> = ctx.db.world().iter().collect();
    let potential_world = worlds.iter()
        .find(|w| w.world_coords.x == tunnel.to_world.x && 
                  w.world_coords.y == tunnel.to_world.y && 
                  w.world_coords.z == tunnel.to_world.z)
        .ok_or("Target world not found")?;
    
    let world_data = potential_world.clone();
    ctx.db.world().delete(potential_world.clone());
    let active_world = World {
        status: "Active".to_string(),
        ..world_data
    };
    ctx.db.world().insert(active_world);
    
    // Create world circuit for the new world
    ctx.db.world_circuit().insert(WorldCircuit {
        world_coords: tunnel.to_world,
        qubit_count: 2, // Shell 1 worlds have 2 qubits
        emission_interval_ms: 4000, // Slightly faster emission
        orbs_per_emission: 8, // More orbs per emission
        last_emission_time: 0,
    });
    
    // Create distribution sphere at tunnel entrance
    let tunnel_entrance_position = DbVector3::new(
        tunnel.to_world.x as f32 * 20.0, // Offset from world center
        110.0, // Floating above surface
        tunnel.to_world.z as f32 * 20.0,
    );
    
    ctx.db.distribution_sphere().insert(DistributionSphere {
        sphere_id: 0, // auto_inc
        world_coords: tunnel.to_world,
        position: tunnel_entrance_position,
        coverage_radius: 120.0, // Smaller coverage than center world
        tunnel_id: Some(tunnel.tunnel_id),
        buffer_capacity: 500.0, // Smaller buffer than center
    });
    
    log::info!("Activated world {:?} with circuit and distribution sphere", tunnel.to_world);
    Ok(())
}

// Helper function to get energy amount for an owner
fn get_energy_amount(ctx: &ReducerContext, owner_type: &str, owner_id: u64, energy_type: EnergyType) -> f32 {
    ctx.db.energy_storage()
        .iter()
        .find(|storage| {
            storage.owner_type == owner_type &&
            storage.owner_id == owner_id &&
            storage.energy_type == energy_type
        })
        .map(|storage| storage.amount)
        .unwrap_or(0.0)
}

// Helper function to add energy to an owner's storage
fn add_energy(ctx: &ReducerContext, owner_type: &str, owner_id: u64, energy_type: EnergyType, amount: f32) -> Result<(), String> {
    // Find existing storage entry
    let existing_storage: Vec<EnergyStorage> = ctx.db.energy_storage()
        .iter()
        .filter(|storage| {
            storage.owner_type == owner_type &&
            storage.owner_id == owner_id &&
            storage.energy_type == energy_type
        })
        .collect();
    
    if let Some(storage) = existing_storage.first() {
        // Update existing entry
        let new_amount = (storage.amount + amount).min(storage.capacity);
        let storage_data = storage.clone();
        ctx.db.energy_storage().delete(storage.clone());
        let updated_storage = EnergyStorage {
            amount: new_amount,
            ..storage_data
        };
        ctx.db.energy_storage().insert(updated_storage);
    } else {
        // Create new storage entry with default capacity
        let default_capacity = match owner_type {
            "player" => 33.0,
            "miner" => 50.0,
            "storage" => 200.0,
            "sphere" => 1000.0,
            _ => 100.0,
        };
        
        ctx.db.energy_storage().insert(EnergyStorage {
            storage_entry_id: 0, // auto_inc
            owner_type: owner_type.to_string(),
            owner_id,
            energy_type,
            amount: amount.min(default_capacity),
            capacity: default_capacity,
        });
    }
    
    Ok(())
}

// Helper function to remove energy from an owner's storage
fn remove_energy(ctx: &ReducerContext, owner_type: &str, owner_id: u64, energy_type: EnergyType, amount: f32) -> Result<bool, String> {
    let existing_storage: Vec<EnergyStorage> = ctx.db.energy_storage()
        .iter()
        .filter(|storage| {
            storage.owner_type == owner_type &&
            storage.owner_id == owner_id &&
            storage.energy_type == energy_type
        })
        .collect();
    
    if let Some(storage) = existing_storage.first() {
        if storage.amount >= amount {
            let storage_data = storage.clone();
            ctx.db.energy_storage().delete(storage.clone());
            let updated_storage = EnergyStorage {
                amount: storage_data.amount - amount,
                ..storage_data
            };
            ctx.db.energy_storage().insert(updated_storage);
            Ok(true)
        } else {
            Ok(false) // Not enough energy
        }
    } else {
        Ok(false) // No storage entry exists
    }
}