pub mod math;

use spacetimedb::{Identity, SpacetimeType, ReducerContext, Table, Timestamp};
use spacetimedb::rand::Rng;
use sha2::{Sha256, Digest};

// Add to your main lib.rs
mod simple_energy;
pub use simple_energy::*;

// 3D vector for positions in the spherical world
#[derive(SpacetimeType, Debug, Clone, Copy)]
pub struct DbVector3 {
    pub x: f32,
    pub y: f32,
    pub z: f32,
}

// Quaternion for 3D rotations
#[derive(SpacetimeType, Debug, Clone, Copy)]
pub struct DbQuaternion {
    pub x: f32,
    pub y: f32,
    pub z: f32,
    pub w: f32,
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

impl std::ops::Sub<DbVector3> for DbVector3 {
    type Output = DbVector3;

    fn sub(self, other: DbVector3) -> DbVector3 {
        DbVector3::new(self.x - other.x, self.y - other.y, self.z - other.z)
    }
}

impl std::ops::Add<DbVector3> for DbVector3 {
    type Output = DbVector3;

    fn add(self, other: DbVector3) -> DbVector3 {
        DbVector3::new(self.x + other.x, self.y + other.y, self.z + other.z)
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
    pub capacity: f32,               // Max storage for this energy type
}

// Player table
#[spacetimedb::table(name = player, public)]
#[derive(Debug, Clone)]
pub struct Player {
    #[primary_key]
    pub identity: Identity,          // Unique identifier for the connection
    #[unique]
    pub player_id: u32,              // Unique player ID
    pub name: String,
    pub current_world: WorldCoords,   // Which world they're in
    pub position: DbVector3,          // Position in the world
    pub rotation: DbQuaternion,      // Player rotation
    pub inventory_capacity: f32,
}

// World representation
#[spacetimedb::table(name = world, public)]
#[derive(Debug, Clone)]
pub struct World {
    #[primary_key]
    pub world_coords: WorldCoords,
    pub shell_level: u8,             // 0 = center, 1-10 = shells
    pub radius: f32,                 // Sphere radius
    pub circuit_qubit_count: u8,     // Circuit complexity for this world
    pub status: String,              // "active", "potential", etc.
}

// Mining devices for collecting energy
#[spacetimedb::table(name = miner_device, public)]
#[derive(Debug, Clone)]
pub struct MinerDevice {
    #[primary_key]
    #[auto_inc]
    pub miner_id: u64,
    pub owner_identity: Identity,
    pub world_coords: WorldCoords,
    pub position: DbVector3,
    pub target_puddle_id: Option<u64>,  // Which puddle it's targeting
    pub efficiency_bonus: f32,          // Upgrade bonus
}

// Energy puddles on ground
#[spacetimedb::table(name = energy_puddle, public)]
#[derive(Debug, Clone)]
pub struct EnergyPuddle {
    #[primary_key]
    #[auto_inc]
    pub puddle_id: u64,
    pub world_coords: WorldCoords,
    pub position: DbVector3,
    pub energy_type: EnergyType,
    pub current_amount: f32,
    pub max_amount: f32,
}

// Storage devices for holding energy
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

// Game settings for configurable parameters
#[spacetimedb::table(name = game_settings, public)]
#[derive(Debug, Clone)]
pub struct GameSettings {
    #[primary_key]
    pub setting_key: String,
    pub value_type: String,           // "int", "float", "string", "bool"
    pub value: String,               // Stored as string, parsed based on type
    pub description: String,
}

// Track logged out players for re-authentication
#[spacetimedb::table(name = logged_out_player, public)]
#[derive(Debug, Clone)]
pub struct LoggedOutPlayer {
    #[primary_key]
    pub identity: Identity,
    pub player_id: u32,
    pub name: String,
    pub logout_time: Timestamp,
}

// User accounts with secure authentication
#[spacetimedb::table(name = user_account, public)]
#[derive(Debug, Clone)]
pub struct UserAccount {
    #[primary_key]
    #[auto_inc]
    pub account_id: u64,
    #[unique]
    pub username: String,
    pub password_hash: String,        // SHA-256 hash
    pub created_at: Timestamp,
    pub last_login: Option<Timestamp>,
}

// Link between account and current identity
#[spacetimedb::table(name = account_identity, public)]
#[derive(Debug, Clone)]
pub struct AccountIdentity {
    #[primary_key]
    pub identity: Identity,
    pub account_id: u64,
}

// ============================================================================
// Authentication Functions
// ============================================================================

fn hash_password(password: &str) -> String {
    let mut hasher = Sha256::new();
    hasher.update(password.as_bytes());
    hex::encode(hasher.finalize())
}

// ============================================================================
// Initialization Reducer
// ============================================================================

#[spacetimedb::reducer]
pub fn init_game_world(ctx: &ReducerContext) -> Result<(), String> {
    // Check if already initialized
    if ctx.db.world().count() > 0 {
        log::info!("Game world already initialized");
        return Ok(());
    }

    log::info!("Initializing game world...");

    // Create center world
    ctx.db.world().insert(World {
        world_coords: WorldCoords::center(),
        shell_level: 0,
        radius: 300.0,
        circuit_qubit_count: 4,
        status: "active".to_string(),
    });

    // Create world circuit at center
    ctx.db.world_circuit().insert(WorldCircuit {
        world_coords: WorldCoords::center(),
        qubit_count: 4,
        emission_interval_ms: 3000,  // Every 3 seconds
        orbs_per_emission: 3,
        last_emission_time: 0,
    });

    // Create initial distribution spheres around center world
    create_distribution_spheres(ctx)?;

    log::info!("Game world initialized successfully!");
    Ok(())
}

// Create distribution spheres in geometric pattern
fn create_distribution_spheres(ctx: &ReducerContext) -> Result<(), String> {
    let sphere_orbit_radius = 400.0;  // Orbit around world
    let coverage_radius = 150.0;
    let buffer_capacity = 1000.0;
    
    // Create 26 spheres in cube pattern (6 faces + 8 corners + 12 edges)
    let mut sphere_positions: Vec<DbVector3> = Vec::new();
    
    // 1. Add 6 face centers (along each axis)
    sphere_positions.push(DbVector3::new(sphere_orbit_radius, 0.0, 0.0));
    sphere_positions.push(DbVector3::new(-sphere_orbit_radius, 0.0, 0.0));
    sphere_positions.push(DbVector3::new(0.0, sphere_orbit_radius, 0.0));
    sphere_positions.push(DbVector3::new(0.0, -sphere_orbit_radius, 0.0));
    sphere_positions.push(DbVector3::new(0.0, 0.0, sphere_orbit_radius));
    sphere_positions.push(DbVector3::new(0.0, 0.0, -sphere_orbit_radius));
    
    // 2. Add 8 vertices (corners of cube)
    let corner_component = sphere_orbit_radius / (3.0_f32).sqrt(); // Distribute along sphere
    for x in [-1.0, 1.0].iter() {
        for y in [-1.0, 1.0].iter() {
            for z in [-1.0, 1.0].iter() {
                sphere_positions.push(DbVector3::new(
                    corner_component * x,
                    corner_component * y,
                    corner_component * z,
                ));
            }
        }
    }
    
    // 3. Add 12 edge centers
    let edge_component = sphere_orbit_radius / (2.0_f32).sqrt(); // Distribute along sphere
    
    // X-axis aligned edges
    sphere_positions.push(DbVector3::new(edge_component, edge_component, 0.0));
    sphere_positions.push(DbVector3::new(edge_component, -edge_component, 0.0));
    sphere_positions.push(DbVector3::new(-edge_component, edge_component, 0.0));
    sphere_positions.push(DbVector3::new(-edge_component, -edge_component, 0.0));
    
    // Y-axis aligned edges
    sphere_positions.push(DbVector3::new(edge_component, 0.0, edge_component));
    sphere_positions.push(DbVector3::new(edge_component, 0.0, -edge_component));
    sphere_positions.push(DbVector3::new(-edge_component, 0.0, edge_component));
    sphere_positions.push(DbVector3::new(-edge_component, 0.0, -edge_component));
    
    // Z-axis aligned edges
    sphere_positions.push(DbVector3::new(0.0, edge_component, edge_component));
    sphere_positions.push(DbVector3::new(0.0, edge_component, -edge_component));
    sphere_positions.push(DbVector3::new(0.0, -edge_component, edge_component));
    sphere_positions.push(DbVector3::new(0.0, -edge_component, -edge_component));
    
    // Create distribution spheres at each position
    for (_index, position) in sphere_positions.iter().enumerate() {
        ctx.db.distribution_sphere().insert(DistributionSphere {
            sphere_id: 0, // auto_inc
            world_coords: WorldCoords::center(),
            position: *position,
            coverage_radius,
            tunnel_id: None, // Not associated with tunnels yet
            buffer_capacity,
        });
    }
    
    log::info!("Created {} distribution spheres", sphere_positions.len());
    Ok(())
}

// ============================================================================
// Tick System - Client Authoritative Version
// ============================================================================

#[spacetimedb::reducer]
pub fn tick(ctx: &ReducerContext) -> Result<(), String> {
    // Only emit new orbs - no physics or lifecycle management
    emit_energy_orbs_volcano_style(ctx)?;
    
    Ok(())
}

// Volcano-style orb emission from surface circuit
fn emit_energy_orbs_volcano_style(ctx: &ReducerContext) -> Result<(), String> {
    let circuits: Vec<WorldCircuit> = ctx.db.world_circuit().iter().collect();
    
    for circuit in circuits {
        let current_time = ctx.timestamp.duration_since(Timestamp::UNIX_EPOCH)
            .expect("Valid timestamp")
            .as_millis() as u64;
            
        if circuit.last_emission_time == 0 || 
           (current_time - circuit.last_emission_time) > circuit.emission_interval_ms {
            
            emit_orbs_for_circuit_volcano(ctx, &circuit)?;
            
            // Update last emission time
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

// Create volcano-style projectile emission
fn emit_orbs_for_circuit_volcano(ctx: &ReducerContext, circuit: &WorldCircuit) -> Result<(), String> {
    let energy_types = [EnergyType::Red, EnergyType::Green, EnergyType::Blue];

    let mut world_radius = 300.0;
    for world_entry in ctx.db.world().iter() {
        if world_entry.world_coords == circuit.world_coords {
            world_radius = world_entry.radius;
            break;
        }
    }
    
    let circuit_position = DbVector3::new(0.0, world_radius, 0.0);
    
    for i in 0..circuit.orbs_per_emission {
        let energy_type = energy_types[(i as usize) % energy_types.len()];
        
        let angle = ctx.rng().gen::<f32>() * 2.0 * std::f32::consts::PI;
        let horizontal_speed = 15.0 + ctx.rng().gen::<f32>() * 10.0;
        let vertical_speed = 20.0 + ctx.rng().gen::<f32>() * 15.0;
        
        let velocity = DbVector3::new(
            angle.cos() * horizontal_speed,
            vertical_speed,
            angle.sin() * horizontal_speed,
        );
        
        let spawn_offset = DbVector3::new(
            angle.cos() * 2.0,
            5.0,
            angle.sin() * 2.0,
        );
        
        let spawn_position = circuit_position + spawn_offset;
        
        ctx.db.energy_orb().insert(EnergyOrb {
            orb_id: 0, // auto_inc
            world_coords: circuit.world_coords,
            position: spawn_position,
            velocity, // This signals the client to start a falling trajectory
            energy_type,
            energy_amount: 10.0,
            creation_time: ctx.timestamp.duration_since(Timestamp::UNIX_EPOCH)
                .expect("Valid timestamp")
                .as_millis() as u64,
        });
    }
    
    Ok(())
}

// ============================================================================
// Client Authoritative Orb Landing
// ============================================================================

#[spacetimedb::reducer]
pub fn report_orb_landing(
    ctx: &ReducerContext, 
    orb_id: u64, 
    landing_position: DbVector3
) -> Result<(), String> {
    // Find the orb
    let orb = ctx.db.energy_orb()
        .orb_id()
        .find(&orb_id)
        .ok_or("Orb not found")?;
    
    // Create puddle at reported position
    create_puddle_from_orb(ctx, &orb, landing_position)?;
    
    // Delete the orb
    ctx.db.energy_orb().delete(orb);
    
    log::info!("Orb {} landed at position {:?}", orb_id, landing_position);
    Ok(())
}

fn create_puddle_from_orb(ctx: &ReducerContext, orb: &EnergyOrb, impact_position: DbVector3) -> Result<(), String> {
    let existing_puddles: Vec<EnergyPuddle> = ctx.db.energy_puddle()
        .iter()
        .filter(|p| p.world_coords == orb.world_coords)
        .collect();
    
    let merge_radius = 25.0;
    
    for existing_puddle in existing_puddles {
        if existing_puddle.energy_type == orb.energy_type {
            let distance = (existing_puddle.position - impact_position).magnitude();
            if distance <= merge_radius {
                let puddle_data = existing_puddle.clone();
                ctx.db.energy_puddle().delete(existing_puddle);
                let updated_puddle = EnergyPuddle {
                    puddle_id: puddle_data.puddle_id,
                    world_coords: puddle_data.world_coords,
                    position: puddle_data.position,
                    energy_type: puddle_data.energy_type,
                    current_amount: (puddle_data.current_amount + orb.energy_amount).min(puddle_data.max_amount),
                    max_amount: puddle_data.max_amount,
                };
                ctx.db.energy_puddle().insert(updated_puddle);
                return Ok(());
            }
        }
    }
    
    ctx.db.energy_puddle().insert(EnergyPuddle {
        puddle_id: 0, // auto_inc
        world_coords: orb.world_coords,
        position: impact_position,
        energy_type: orb.energy_type,
        current_amount: orb.energy_amount,
        max_amount: 100.0,
    });
    
    Ok(())
}

// ============================================================================
// Authentication Reducers
// ============================================================================

#[spacetimedb::reducer]
pub fn register_account(ctx: &ReducerContext, username: String, password: String) -> Result<(), String> {
    if username.len() < 3 || username.len() > 20 {
        return Err("Username must be between 3 and 20 characters".to_string());
    }
    
    if password.len() < 6 {
        return Err("Password must be at least 6 characters".to_string());
    }
    
    // Check if username already exists
    if ctx.db.user_account().username().find(&username).is_some() {
        return Err("Username already taken".to_string());
    }
    
    let password_hash = hash_password(&password);
    
    ctx.db.user_account().insert(UserAccount {
        account_id: 0, // auto_inc
        username: username.clone(),
        password_hash,
        created_at: ctx.timestamp,
        last_login: None,
    });
    
    log::info!("New account registered: {}", username);
    Ok(())
}

#[spacetimedb::reducer]
pub fn login(ctx: &ReducerContext, username: String, password: String) -> Result<(), String> {
    let account = ctx.db.user_account()
        .username()
        .find(&username)
        .ok_or("Invalid username or password".to_string())?;
    
    let password_hash = hash_password(&password);
    if account.password_hash != password_hash {
        return Err("Invalid username or password".to_string());
    }
    
    // Check if already logged in
    if let Some(existing_link) = ctx.db.account_identity()
        .iter()
        .find(|link| link.account_id == account.account_id) {
        // Remove old identity link
        ctx.db.account_identity().delete(existing_link);
    }
    
    // Create new identity link
    ctx.db.account_identity().insert(AccountIdentity {
        identity: ctx.sender,
        account_id: account.account_id,
    });
    
    // Update last login
    let mut updated_account = account.clone();
    updated_account.last_login = Some(ctx.timestamp);
    ctx.db.user_account().delete(account);
    ctx.db.user_account().insert(updated_account);
    
    log::info!("User {} logged in with identity {:?}", username, ctx.sender);
    Ok(())
}

// ============================================================================
// Player Management Reducers
// ============================================================================

#[spacetimedb::reducer]
pub fn enter_game(ctx: &ReducerContext, player_name: String) -> Result<(), String> {
    // Check if user is authenticated
    let account_link = ctx.db.account_identity()
        .iter()
        .find(|link| link.identity == ctx.sender)
        .ok_or("Not authenticated. Please login first.".to_string())?;
    
    let account = ctx.db.user_account()
        .account_id()
        .find(&account_link.account_id)
        .ok_or("Account not found".to_string())?;
    
    if player_name.trim().is_empty() || player_name.len() > 32 {
        return Err("Player name must be between 1 and 32 characters".to_string());
    }
    
    // Check if this account already has a player
    if let Some(existing_player) = ctx.db.player().iter()
        .find(|p| p.identity == ctx.sender) {
        // Update name if different
        if existing_player.name != player_name {
            let mut updated_player = existing_player.clone();
            updated_player.name = player_name.clone();
            ctx.db.player().delete(existing_player);
            ctx.db.player().insert(updated_player);
            log::info!("Player '{}' updated name for account: {}", player_name, account.username);
        } else {
            log::info!("Player '{}' re-entered game for account: {}", player_name, account.username);
        }
        return Ok(());
    }
    
    // Check if this was a previously logged out player
    if let Some(logged_out) = ctx.db.logged_out_player().identity().find(&ctx.sender) {
        // Restore the player
        ctx.db.player().insert(Player {
            identity: ctx.sender,
            player_id: logged_out.player_id,
            name: player_name.clone(),
            current_world: WorldCoords::center(),
            position: DbVector3::new(0.0, 302.0, 0.0), // Start above world surface
            rotation: DbQuaternion { x: 0.0, y: 0.0, z: 0.0, w: 1.0 },
            inventory_capacity: 100.0,
        });
        
        // Remove from logged out table
        ctx.db.logged_out_player().delete(logged_out);
        
        log::info!("Restored player '{}' for account: {}", player_name, account.username);
        return Ok(());
    }
    
    // Create new player
    let player_id = ctx.db.player().count() as u32 + 1;
    
    ctx.db.player().insert(Player {
        identity: ctx.sender,
        player_id,
        name: player_name.clone(),
        current_world: WorldCoords::center(),
        position: DbVector3::new(0.0, 302.0, 0.0), // Start above world surface
        rotation: DbQuaternion { x: 0.0, y: 0.0, z: 0.0, w: 1.0 },
        inventory_capacity: 100.0,
    });
    
    log::info!("Created player '{}' for account: {}", player_name, account.username);
    
    Ok(())
}

#[spacetimedb::reducer]
pub fn update_player_position(
    ctx: &ReducerContext,
    pos_x: f32,
    pos_y: f32,
    pos_z: f32,
    rot_x: f32,
    rot_y: f32,
    rot_z: f32,
    rot_w: f32,
) -> Result<(), String> {
    let sender_identity = ctx.sender;

    if let Some(player_to_update) = ctx.db.player().identity().find(&sender_identity) {
        let mut updated_player = player_to_update.clone();
        updated_player.position = DbVector3 { x: pos_x, y: pos_y, z: pos_z };
        updated_player.rotation = DbQuaternion { x: rot_x, y: rot_y, z: rot_z, w: rot_w };

        ctx.db.player().delete(player_to_update);
        ctx.db.player().insert(updated_player);

    } else {
        log::warn!("Attempted to update position for non-existent player with identity: {:?}", sender_identity);
    }
    Ok(())
}

// ============================================================================
// Utility Reducers
// ============================================================================

#[spacetimedb::reducer]
pub fn activate_tunnel(_ctx: &ReducerContext, tunnel_id: u64, energy_amount: f32) -> Result<(), String> {
    log::info!("Tunnel activation attempted: {} with {} energy", tunnel_id, energy_amount);
    // For now, just log - we'll implement this later
    Ok(())
}

#[spacetimedb::reducer]
pub fn log_all_player_locations(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("Querying all player locations:");
    for player in ctx.db.player().iter() {
        log::info!(
            "Player ID: {:?}, Name: {}, World: {:?}, Position: {:?}, Rotation: {:?}",
            player.identity,
            player.name,
            player.current_world,
            player.position,
            player.rotation
        );
    }
    Ok(())
}

// ============================================================================
// Connection Lifecycle Reducers
// ============================================================================

#[spacetimedb::reducer]
pub fn connect(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("New connection from identity: {:?}", ctx.sender);
    
    // Initialize world if not already done
    init_game_world(ctx)?;
    
    // Call tick to start emission cycle
    tick(ctx)?;
    
    Ok(())
}

#[spacetimedb::reducer]
pub fn disconnect(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("Disconnection from identity: {:?}", ctx.sender);
    
    // Save player state for later restoration
    if let Some(player) = ctx.db.player().identity().find(&ctx.sender) {
        ctx.db.logged_out_player().insert(LoggedOutPlayer {
            identity: player.identity,
            player_id: player.player_id,
            name: player.name.clone(),
            logout_time: ctx.timestamp,
        });
        
        // Remove from active players
        ctx.db.player().delete(player);
        
        log::info!("Player logged out and saved for later restoration");
    }
    
    // Remove identity link if exists
    if let Some(link) = ctx.db.account_identity().identity().find(&ctx.sender) {
        ctx.db.account_identity().delete(link);
    }
    
    Ok(())
}