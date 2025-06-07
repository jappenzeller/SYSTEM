pub mod math;

use spacetimedb::{Identity, SpacetimeType, ReducerContext, Table, ScheduleAt, Timestamp};
use spacetimedb::rand::Rng;
use std::time::Duration;

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
    pub rotation: DbQuaternion,       // Player's rotation
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

#[spacetimedb::reducer(init)]
pub fn init(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("Initializing quantum metaverse with surface circuit...");
    
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
    
    // Create the world circuit for center world - positioned on surface like a volcano
    ctx.db.world_circuit().insert(WorldCircuit {
        world_coords: WorldCoords::center(),
        qubit_count: 1,
        emission_interval_ms: 30000,  // Emit energy every 30 seconds
        orbs_per_emission: 4,        // 4 orbs per emission
        last_emission_time: 0,
    });
    
    // Create 26 evenly distributed distribution spheres
    create_distribution_spheres(ctx)?;
    
    // Start the tick timer
    ctx.db.tick_timer().try_insert(TickTimer {
        scheduled_id: 0,
        scheduled_at: ScheduleAt::Interval(Duration::from_millis(100).into()),
    })?;
    
    log::info!("Center world initialized with 26 distribution spheres");
    Ok(())
}

// Helper function to create 26 evenly distributed spheres
fn create_distribution_spheres(ctx: &ReducerContext) -> Result<(), String> {
    let sphere_height = 120.0; // 20 units above the world surface (100 + 20)
    let coverage_radius = 20.0;
    let buffer_capacity = 100.0; // Smaller capacity for individual spheres
    
    let mut sphere_positions = Vec::new();
    
    // 1. Add 6 face centers (cardinal directions)
    sphere_positions.push(DbVector3::new(sphere_height, 0.0, 0.0));   // +X
    sphere_positions.push(DbVector3::new(-sphere_height, 0.0, 0.0));  // -X
    sphere_positions.push(DbVector3::new(0.0, sphere_height, 0.0));   // +Y
    sphere_positions.push(DbVector3::new(0.0, -sphere_height, 0.0));  // -Y
    sphere_positions.push(DbVector3::new(0.0, 0.0, sphere_height));   // +Z
    sphere_positions.push(DbVector3::new(0.0, 0.0, -sphere_height));  // -Z
    
    // 2. Add 8 vertices (corners of cube)
    let corner = sphere_height / (3.0_f32).sqrt(); // Normalize to sphere surface
    for x in [-1.0, 1.0].iter() {
        for y in [-1.0, 1.0].iter() {
            for z in [-1.0, 1.0].iter() {
                sphere_positions.push(DbVector3::new(
                    corner * x,
                    corner * y,
                    corner * z,
                ));
            }
        }
    }
    
    // 3. Add 12 edge centers
    let edge = sphere_height / (2.0_f32).sqrt(); // Normalize to sphere surface
    
    // X-axis aligned edges
    sphere_positions.push(DbVector3::new(edge, edge, 0.0));
    sphere_positions.push(DbVector3::new(edge, -edge, 0.0));
    sphere_positions.push(DbVector3::new(-edge, edge, 0.0));
    sphere_positions.push(DbVector3::new(-edge, -edge, 0.0));
    
    // Y-axis aligned edges
    sphere_positions.push(DbVector3::new(edge, 0.0, edge));
    sphere_positions.push(DbVector3::new(edge, 0.0, -edge));
    sphere_positions.push(DbVector3::new(-edge, 0.0, edge));
    sphere_positions.push(DbVector3::new(-edge, 0.0, -edge));
    
    // Z-axis aligned edges
    sphere_positions.push(DbVector3::new(0.0, edge, edge));
    sphere_positions.push(DbVector3::new(0.0, edge, -edge));
    sphere_positions.push(DbVector3::new(0.0, -edge, edge));
    sphere_positions.push(DbVector3::new(0.0, -edge, -edge));
    
    // Create distribution spheres at each position
    for (index, position) in sphere_positions.iter().enumerate() {
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

// UPDATED TICK WITH VOLCANO PHYSICS
#[spacetimedb::reducer]
pub fn tick(ctx: &ReducerContext, _timer: TickTimer) -> Result<(), String> {
    let orb_count = ctx.db.energy_orb().count();
  //  log::info!("Tick working! Current orb count: {}", orb_count);
    
    // Update existing orbs with gravity physics
    update_falling_orbs_with_gravity(ctx)?;
    
    // Emit new orbs from circuit if it's time
    emit_energy_orbs_volcano_style(ctx)?;
    
    Ok(())
}

// Volcano-style orb emission from surface circuit
fn emit_energy_orbs_volcano_style(ctx: &ReducerContext) -> Result<(), String> {
    let circuits: Vec<WorldCircuit> = ctx.db.world_circuit().iter().collect();
    
    for circuit in circuits {
        // Simple time check - emit every ~30 ticks (3 seconds at 10hz)
        let current_time = ctx.timestamp.duration_since(Timestamp::UNIX_EPOCH)
            .expect("Valid timestamp")
            .as_millis() as u64;
            
        if circuit.last_emission_time == 0 || 
           (current_time - circuit.last_emission_time) > circuit.emission_interval_ms {
            
            log::info!("Emitting volcano-style orbs from surface circuit!");
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
    
    // Circuit is located at north pole of sphere
    let circuit_position = DbVector3::new(0.0, 100.0, 0.0);
    
    for i in 0..circuit.orbs_per_emission {
        let energy_type = energy_types[(i as usize) % energy_types.len()];
        
        // Create volcano-like emission pattern
        // Random angle around the circuit
        let angle = ctx.rng().gen::<f32>() * 2.0 * std::f32::consts::PI;
        
        // Upward and outward velocity (like volcano projectile)
        let horizontal_speed = 15.0 + ctx.rng().gen::<f32>() * 10.0; // 15-25 units/sec
        let vertical_speed = 20.0 + ctx.rng().gen::<f32>() * 15.0;   // 20-35 units/sec upward
        
        let velocity = DbVector3::new(
            angle.cos() * horizontal_speed,  // X velocity (outward)
            vertical_speed,                  // Y velocity (upward from surface)
            angle.sin() * horizontal_speed,  // Z velocity (outward)
        );
        
        // Start orbs slightly above the circuit to prevent them from being on the exact surface
        let spawn_offset = DbVector3::new(
            angle.cos() * 2.0,  // Small offset in X
            5.0,                // 5 units above the circuit
            angle.sin() * 2.0,  // Small offset in Z
        );
        
        let spawn_position = circuit_position + spawn_offset;
        
        ctx.db.energy_orb().insert(EnergyOrb {
            orb_id: 0, // auto_inc
            world_coords: circuit.world_coords,
            position: spawn_position, // Start slightly above circuit
            velocity,
            energy_type,
            energy_amount: 10.0,
            creation_time: ctx.timestamp.duration_since(Timestamp::UNIX_EPOCH)
                .expect("Valid timestamp")
                .as_millis() as u64,
        });
    }
    
    log::info!("Emitted {} energy orbs from surface circuit (volcano effect)", circuit.orbs_per_emission);
    Ok(())
}

// Realistic gravity-based physics for projectiles
fn update_falling_orbs_with_gravity(ctx: &ReducerContext) -> Result<(), String> {
    let orbs: Vec<EnergyOrb> = ctx.db.energy_orb().iter().collect();
    
    for orb in orbs {
        // Apply gravity (acceleration toward sphere center)
        let gravity_strength = 30.0; // Stronger gravity for more dramatic effect
        let center = DbVector3::new(0.0, 0.0, 0.0);
        let direction_to_center = (center - orb.position).normalized();
        let gravity_acceleration = direction_to_center * gravity_strength * 0.1; // 0.1 sec timestep
        
        // Update velocity with gravity
        let new_velocity = orb.velocity + gravity_acceleration;
        
        // Update position with velocity
        let new_position = orb.position + new_velocity * 0.1; // 0.1 sec timestep
        
        // Check if orb hit the surface
        if new_position.magnitude() <= 100.5 { // Slight buffer for surface detection
            // Hit the surface - create puddle
            let surface_position = new_position.normalized() * 100.0;
            create_puddle_from_orb(ctx, &orb, surface_position)?;
            
            // Save orb_id before moving orb into delete
            let orb_id = orb.orb_id;
            ctx.db.energy_orb().delete(orb);
            log::info!("Orb {} hit surface and created puddle", orb_id);
        } else {
            // Update orb with new position and velocity
            let orb_data = orb.clone();
            ctx.db.energy_orb().delete(orb);
            let updated_orb = EnergyOrb {
                orb_id: orb_data.orb_id,
                world_coords: orb_data.world_coords,
                position: new_position,
                velocity: new_velocity, // Updated velocity with gravity
                energy_type: orb_data.energy_type,
                energy_amount: orb_data.energy_amount,
                creation_time: orb_data.creation_time,
            };
            ctx.db.energy_orb().insert(updated_orb);
        }
    }
    
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
            rotation: player_data.rotation, // Ensure rotation is carried over
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
        rotation: DbQuaternion { // Default rotation (e.g., looking forward)
            x: 0.0,
            y: 0.0,
            z: 0.0,
            w: 1.0,
        },
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

/// Reducer to update a player's position and rotation.
/// Called by the client when the player moves.
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

    // Attempt to find the player by their identity
    if let Some(player_to_update) = ctx.db.player().identity().find(&sender_identity) {
        let mut updated_player = player_to_update.clone();
        // Update the position and rotation fields
        updated_player.position = DbVector3 { x: pos_x, y: pos_y, z: pos_z };
        updated_player.rotation = DbQuaternion { x: rot_x, y: rot_y, z: rot_z, w: rot_w };

        // Persist the changes to the Player table
        // SpacetimeDB's `update` typically requires the full new state of the row.
        // We achieve this by deleting the old and inserting the modified clone.
        ctx.db.player().delete(player_to_update);
        ctx.db.player().insert(updated_player);

    } else {
        log::warn!("Attempted to update position for non-existent player with identity: {:?}", sender_identity);
    }
    Ok(())
}
// Simplified reducer for tunnel activation
#[spacetimedb::reducer]
pub fn activate_tunnel(_ctx: &ReducerContext, tunnel_id: u64, energy_amount: f32) -> Result<(), String> {
    log::info!("Tunnel activation attempted: {} with {} energy", tunnel_id, energy_amount);
    // For now, just log - we'll implement this later
    Ok(())
}

/// Reducer to query and log all player locations.
/// This is a conceptual example; in a real game, you'd likely use this data
/// for specific game logic rather than just logging.
#[spacetimedb::reducer]
pub fn log_all_player_locations(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("Querying all player locations:");
    for player in ctx.db.player().iter() {
        log::info!(
            "Player ID: {:?}, Name: {}, World: {:?}, Position: {:?}, Rotation: {:?}",
            player.identity, // or player.player_id
            player.name,
            player.current_world,
            player.position,
            player.rotation // Added player rotation here
        );
    }
    Ok(())
}