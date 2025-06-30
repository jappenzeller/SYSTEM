// SYSTEM Server - Wave Packet Metaverse with Wave Mining
// Single file architecture for clean compilation

use spacetimedb::{Identity, ReducerContext, Table, Timestamp, SpacetimeType};
use std::f32::consts::PI;
use std::collections::HashMap;
use std::sync::{Mutex, OnceLock};

// ============================================================================
// Core Type Definitions
// ============================================================================

#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct WorldCoords {
    pub x: i32,
    pub y: i32,
    pub z: i32,
}

#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq)]
pub struct DbVector3 {
    pub x: f32,
    pub y: f32,
    pub z: f32,
}

impl DbVector3 {
    pub fn new(x: f32, y: f32, z: f32) -> Self {
        DbVector3 { x, y, z }
    }
    
    pub fn zero() -> Self {
        DbVector3 { x: 0.0, y: 0.0, z: 0.0 }
    }
    
    pub fn magnitude(&self) -> f32 {
        (self.x * self.x + self.y * self.y + self.z * self.z).sqrt()
    }
    
    pub fn distance_to(&self, other: &DbVector3) -> f32 {
        let dx = self.x - other.x;
        let dy = self.y - other.y;
        let dz = self.z - other.z;
        (dx * dx + dy * dy + dz * dz).sqrt()
    }
}

// ============================================================================
// Authentication & Account Tables
// ============================================================================

#[spacetimedb::table(name = user_account, public)]
#[derive(Debug, Clone)]
pub struct UserAccount {
    #[primary_key]
    #[auto_inc]
    pub account_id: u64,
    #[unique]
    pub username: String,
    pub password_hash: String,
    pub created_at: Timestamp,
    pub last_login: Option<Timestamp>,
}

#[spacetimedb::table(name = account_identity, public)]
#[derive(Debug, Clone)]
pub struct AccountIdentity {
    #[primary_key]
    pub identity: Identity,
    pub account_id: u64,
}

// ============================================================================
// Player Tables
// ============================================================================

#[spacetimedb::table(name = player, public)]
#[derive(Debug, Clone)]
pub struct Player {
    #[primary_key]
    pub identity: Identity,
    #[auto_inc]
    #[unique]
    pub player_id: u64,
    pub name: String,
    pub current_world: WorldCoords,
    pub position: DbVector3,
    pub rotation: DbVector3,
    pub last_update: Timestamp,
}

#[spacetimedb::table(name = logged_out_player, public)]
#[derive(Debug, Clone)]
pub struct LoggedOutPlayer {
    #[primary_key]
    pub identity: Identity,
    pub player_id: u64,
    pub name: String,
    pub logout_time: Timestamp,
}

// ============================================================================
// World System Tables
// ============================================================================

#[spacetimedb::table(name = world, public)]
#[derive(Debug, Clone)]
pub struct World {
    #[primary_key]
    pub world_coords: WorldCoords,
    pub world_name: String,
    pub world_type: String,
    pub shell_level: u8,
}

#[spacetimedb::table(name = world_circuit, public)]
#[derive(Debug, Clone)]
pub struct WorldCircuit {
    #[primary_key]
    pub world_coords: WorldCoords,
    #[auto_inc]
    pub circuit_id: u64,
    pub qubit_count: u8,
    pub emission_interval_ms: u64,
    pub orbs_per_emission: u32,
    pub last_emission_time: u64,
}

#[spacetimedb::table(name = game_settings, public)]
#[derive(Debug, Clone)]
pub struct GameSettings {
    #[primary_key]
    pub setting_key: String,
    pub value_type: String,
    pub value: String,
    pub description: String,
}

// ============================================================================
// Wave Packet System Types
// ============================================================================

#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq)]
pub struct WavePacketSignature {
    pub frequency: f32,
    pub resonance: f32,
    pub flux_pattern: u16,
}

impl WavePacketSignature {
    pub fn new(frequency: f32, resonance: f32, flux_pattern: u16) -> Self {
        WavePacketSignature { frequency, resonance, flux_pattern }
    }
    
    pub fn matches_crystal(&self, crystal: &CrystalType) -> bool {
        let (center, tolerance) = crystal.get_radian_range();
        let packet_radian = self.frequency * 2.0 * PI;
        let diff = (packet_radian - center).abs();
        
        // Handle wrap-around
        let normalized_diff = if diff > PI { 2.0 * PI - diff } else { diff };
        normalized_diff <= tolerance
    }
    
    pub fn to_color_string(&self) -> String {
        let radian = self.frequency * 2.0 * PI;
        if radian < PI / 6.0 || radian > 11.0 * PI / 6.0 { "Red" }
        else if radian < PI / 2.0 { "Yellow" }
        else if radian < 5.0 * PI / 6.0 { "Green" }
        else if radian < 7.0 * PI / 6.0 { "Cyan" }
        else if radian < 3.0 * PI / 2.0 { "Blue" }
        else { "Magenta" }.to_string()
    }
}

#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq)]
pub struct WavePacketSample {
    pub signature: WavePacketSignature,
    pub amount: u32,
}

#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum FrequencyBand {
    Red,
    Yellow,
    Green,
    Cyan,
    Blue,
    Magenta,
}

// ============================================================================
// Mining System Types
// ============================================================================

#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum CrystalType {
    Red,    // 0 radians
    Green,  // 2π/3 radians  
    Blue,   // 4π/3 radians
}

impl CrystalType {
    pub fn get_radian_range(&self) -> (f32, f32) {
        let center = match self {
            CrystalType::Red => 0.0,
            CrystalType::Green => 2.0 * PI / 3.0,
            CrystalType::Blue => 4.0 * PI / 3.0,
        };
        (center, PI / 6.0) // center and tolerance
    }
    
    pub fn to_frequency(&self) -> f32 {
        match self {
            CrystalType::Red => 0.2,    // Middle of red band
            CrystalType::Green => 0.575, // Middle of green band
            CrystalType::Blue => 0.725,  // Middle of blue band
        }
    }
}

// ============================================================================
// Wave Packet System Tables
// ============================================================================

#[spacetimedb::table(name = wave_packet_orb, public)]
#[derive(Debug, Clone)]
pub struct WavePacketOrb {
    #[primary_key]
    #[auto_inc]
    pub orb_id: u64,
    pub world_coords: WorldCoords,
    pub position: DbVector3,
    pub velocity: DbVector3,
    pub wave_packet_composition: Vec<WavePacketSample>, // Multiple frequencies in one orb
    pub total_wave_packets: u32,
    pub creation_time: u64,
    pub lifetime_ms: u32,
    pub last_dissipation: u64,
}

#[spacetimedb::table(name = wave_packet_storage, public)]
#[derive(Debug, Clone)]
pub struct WavePacketStorage {
    #[primary_key]
    #[auto_inc]
    pub storage_id: u64,
    pub owner_type: String,
    pub owner_id: u64,
    pub frequency_band: FrequencyBand,
    pub total_wave_packets: u32,
    pub signature_samples: Vec<WavePacketSample>,
    pub last_update: u64,
}

#[spacetimedb::table(name = player_crystal, public)]
#[derive(Debug, Clone)]
pub struct PlayerCrystal {
    #[primary_key]
    pub player_id: u64,
    pub crystal_type: CrystalType,
    pub slot_count: u8, // 1 for free, 2 for paid
    pub chosen_at: u64,
}

// Add this table to communicate extractions to client
#[spacetimedb::table(name = wave_packet_extraction, public)]
#[derive(Debug, Clone)]
pub struct WavePacketExtraction {
    #[primary_key]
    pub extraction_id: u64,
    pub player_id: u64,
    pub wave_packet_id: u64,
    pub signature: WavePacketSignature,
    pub departure_time: u64,
    pub expected_arrival: u64,
}

// ============================================================================
// Mining System State (In-Memory)
// ============================================================================

static MINING_STATE: OnceLock<Mutex<HashMap<u64, MiningSession>>> = OnceLock::new();
static WAVE_PACKET_COUNTER: OnceLock<Mutex<u64>> = OnceLock::new();

fn get_mining_state() -> &'static Mutex<HashMap<u64, MiningSession>> {
    MINING_STATE.get_or_init(|| Mutex::new(HashMap::new()))
}

fn get_wave_packet_counter() -> &'static Mutex<u64> {
    WAVE_PACKET_COUNTER.get_or_init(|| Mutex::new(0))
}

struct MiningSession {
    player_id: u64,
    orb_id: u64,
    crystal_type: CrystalType,
    last_extraction: u64,
    pending_wave_packets: Vec<PendingWavePacket>,
}

struct PendingWavePacket {
    wave_packet_id: u64,
    signature: WavePacketSignature,
    departure_time: u64,
    expected_arrival: u64,
}

impl Clone for PendingWavePacket {
    fn clone(&self) -> Self {
        PendingWavePacket {
            wave_packet_id: self.wave_packet_id,
            signature: self.signature,
            departure_time: self.departure_time,
            expected_arrival: self.expected_arrival,
        }
    }
}

fn get_next_wave_packet_id() -> u64 {
    let mut counter = get_wave_packet_counter().lock().unwrap();
    *counter += 1;
    *counter
}

// ============================================================================
// Initialization & World Setup
// ============================================================================

fn init_game_world(ctx: &ReducerContext) -> Result<(), String> {
    // Check if both worlds and circuits exist
    let world_count = ctx.db.world().iter().count();
    let circuit_count = ctx.db.world_circuit().iter().count();
    
    if world_count > 0 && circuit_count > 0 {
        log::info!("Game world already initialized ({} worlds, {} circuits), skipping...", 
                  world_count, circuit_count);
        return Ok(());
    }
    
    // If we have partial data, log a warning
    if world_count > 0 || circuit_count > 0 {
        log::warn!("Partial world data detected: {} worlds, {} circuits. Skipping initialization to preserve data.", 
                  world_count, circuit_count);
        log::warn!("Use debug_reset_world to clear and reinitialize if needed.");
        return Ok(());
    }
    
    log::info!("Initializing game world...");
    
    // Create center world (0,0,0)
    let center_world = World {
        world_coords: WorldCoords { x: 0, y: 0, z: 0 },
        world_name: "Origin".to_string(),
        world_type: "standard".to_string(),
        shell_level: 0,
    };
    ctx.db.world().insert(center_world);
    
    // Create world circuit for center world
    let circuit = WorldCircuit {
        world_coords: WorldCoords { x: 0, y: 0, z: 0 },
        circuit_id: 0,
        qubit_count: 6,
        emission_interval_ms: 10000, // 10 seconds
        orbs_per_emission: 3,
        last_emission_time: 0,
    };
    ctx.db.world_circuit().insert(circuit);
    
    // Add game settings
    add_default_game_settings(ctx)?;
    
    log::info!("Game world initialized successfully!");
    Ok(())
}

fn add_default_game_settings(ctx: &ReducerContext) -> Result<(), String> {
    let settings = vec![
        ("mining_range", "float", "30.0", "Maximum range for mining wave packets"),
        ("extraction_interval_ms", "int", "2000", "Time between wave packet extractions"),
        ("wave_packet_speed", "float", "5.0", "Speed of wave packets in units per second"),
        ("dissipation_tau_ms", "int", "10000", "Time constant for wave packet dissipation"),
        ("orb_lifetime_ms", "int", "300000", "Default lifetime for wave packet orbs (5 minutes)"),
    ];
    
    for (key, value_type, value, desc) in settings {
        let setting = GameSettings {
            setting_key: key.to_string(),
            value_type: value_type.to_string(),
            value: value.to_string(),
            description: desc.to_string(),
        };
        ctx.db.game_settings().insert(setting);
    }
    
    Ok(())
}

// ============================================================================
// Helper Functions
// ============================================================================

fn simple_random(seed: u64) -> f32 {
    let mut x = seed;
    x ^= x >> 12;
    x ^= x << 25;
    x ^= x >> 27;
    ((x.wrapping_mul(0x2545F4914F6CDD1D)) >> 32) as f32 / u32::MAX as f32
}

fn hash_password(password: &str) -> String {
    // In production, use proper password hashing like bcrypt
    format!("hashed_{}", password)
}

fn get_frequency_band(frequency: f32) -> FrequencyBand {
    let radian = frequency * 2.0 * PI;
    
    if radian < PI / 6.0 || radian > 11.0 * PI / 6.0 {
        FrequencyBand::Red
    } else if radian < PI / 2.0 {
        FrequencyBand::Yellow
    } else if radian < 5.0 * PI / 6.0 {
        FrequencyBand::Green
    } else if radian < 7.0 * PI / 6.0 {
        FrequencyBand::Cyan
    } else if radian < 3.0 * PI / 2.0 {
        FrequencyBand::Blue
    } else {
        FrequencyBand::Magenta
    }
}

// ============================================================================
// Authentication Reducers
// ============================================================================

#[spacetimedb::reducer]
pub fn register_account(
    ctx: &ReducerContext,
    username: String,
    password: String,
) -> Result<(), String> {
    if username.len() < 3 || username.len() > 20 {
        return Err("Username must be between 3 and 20 characters".to_string());
    }
    
    if ctx.db.user_account().username().find(&username).is_some() {
        return Err("Username already taken".to_string());
    }
    
    let account = UserAccount {
        account_id: 0,
        username: username.clone(),
        password_hash: hash_password(&password),
        created_at: ctx.timestamp,
        last_login: None,
    };
    
    ctx.db.user_account().insert(account);
    log::info!("Account registered: {}", username);
    Ok(())
}

#[spacetimedb::reducer]
pub fn login(
    ctx: &ReducerContext,
    username: String,
    password: String,
) -> Result<(), String> {
    let account = ctx.db.user_account()
        .username()
        .find(&username)
        .ok_or("Invalid username or password")?;
    
    if account.password_hash != hash_password(&password) {
        return Err("Invalid username or password".to_string());
    }
    
    let identity_link = AccountIdentity {
        identity: ctx.sender,
        account_id: account.account_id,
    };
    ctx.db.account_identity().insert(identity_link);
    
    let mut updated_account = account.clone();
    updated_account.last_login = Some(ctx.timestamp);
    ctx.db.user_account().delete(account);
    ctx.db.user_account().insert(updated_account);
    
    log::info!("User {} logged in", username);
    Ok(())
}

// ============================================================================
// Player Management Reducers
// ============================================================================

#[spacetimedb::reducer]
pub fn create_player(ctx: &ReducerContext, player_name: String) -> Result<(), String> {
    if player_name.len() < 3 || player_name.len() > 20 {
        return Err("Player name must be between 3 and 20 characters".to_string());
    }
    
    if ctx.db.player().identity().find(&ctx.sender).is_some() {
        return Err("Player already exists for this identity".to_string());
    }
    
    if let Some(logged_out) = ctx.db.logged_out_player().identity().find(&ctx.sender) {
        let player = Player {
            identity: ctx.sender,
            player_id: logged_out.player_id,
            name: logged_out.name.clone(),
            current_world: WorldCoords { x: 0, y: 0, z: 0 },
            position: DbVector3::zero(),
            rotation: DbVector3::zero(),
            last_update: ctx.timestamp,
        };
        
        ctx.db.player().insert(player);
        ctx.db.logged_out_player().delete(logged_out);
        log::info!("Player {} restored from logout", player_name);
        return Ok(());
    }
    
    let new_player = Player {
        identity: ctx.sender,
        player_id: 0,
        name: player_name.clone(),
        current_world: WorldCoords { x: 0, y: 0, z: 0 },
        position: DbVector3::new(0.0, 10.0, 0.0),
        rotation: DbVector3::zero(),
        last_update: ctx.timestamp,
    };
    
    ctx.db.player().insert(new_player);
    log::info!("New player created: {}", player_name);
    Ok(())
}

#[spacetimedb::reducer]
pub fn update_player_position(
    ctx: &ReducerContext,
    world_coords: WorldCoords,
    position: DbVector3,
    rotation: DbVector3,
) -> Result<(), String> {
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    let mut updated_player = player.clone();
    updated_player.current_world = world_coords;
    updated_player.position = position;
    updated_player.rotation = rotation;
    updated_player.last_update = ctx.timestamp;
    
    ctx.db.player().delete(player);
    ctx.db.player().insert(updated_player);
    
    Ok(())
}

#[spacetimedb::reducer]
pub fn choose_starting_crystal(ctx: &ReducerContext, crystal_type: CrystalType) -> Result<(), String> {
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    if ctx.db.player_crystal().player_id().find(&player.player_id).is_some() {
        return Err("You already have a crystal".to_string());
    }
    
    let crystal = PlayerCrystal {
        player_id: player.player_id,
        crystal_type,
        slot_count: 1,
        chosen_at: ctx.timestamp
            .duration_since(Timestamp::UNIX_EPOCH)
            .expect("Valid timestamp")
            .as_millis() as u64,
    };
    
    ctx.db.player_crystal().insert(crystal);
    
    log::info!(
        "Player {} chose {} crystal",
        player.name,
        match crystal_type {
            CrystalType::Red => "Red",
            CrystalType::Green => "Green",
            CrystalType::Blue => "Blue",
        }
    );
    
    Ok(())
}

// ============================================================================
// Wave Packet Emission
// ============================================================================

#[spacetimedb::reducer]
pub fn emit_wave_packet_orb(
    ctx: &ReducerContext,
    world_coords: WorldCoords,
    circuit_position: DbVector3,
) -> Result<(), String> {
    let shell_level = ((world_coords.x.abs() + world_coords.y.abs() + world_coords.z.abs()) / 3) as u8;
    
    let composition = generate_wave_packet_composition(shell_level, 100);
    
    let spawn_offset = DbVector3 {
        x: (simple_random(ctx.timestamp.as_micros() as u64) - 0.5) * 50.0,
        y: -20.0,
        z: (simple_random(ctx.timestamp.as_micros() as u64 + 1) - 0.5) * 50.0,
    };
    
    let position = DbVector3 {
        x: circuit_position.x + spawn_offset.x,
        y: circuit_position.y + spawn_offset.y,
        z: circuit_position.z + spawn_offset.z,
    };
    
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    let orb = WavePacketOrb {
        orb_id: 0,
        world_coords,
        position,
        velocity: DbVector3::zero(),
        wave_packet_composition: composition,
        total_wave_packets: 100,
        creation_time: current_time,
        lifetime_ms: 300000, // 5 minutes
        last_dissipation: current_time,
    };
    
    ctx.db.wave_packet_orb().insert(orb);
    Ok(())
}

fn generate_wave_packet_composition(shell_level: u8, total_packets: u32) -> Vec<WavePacketSample> {
    let mut composition = Vec::new();
    let seed = shell_level as u64 * 12345;
    
    match shell_level {
        0 => {
            // Shell 0: Primarily R, G, B (80%), with 10% bleed from shell 1
            let primary_each = (total_packets as f32 * 0.8 / 3.0) as u32;
            let bleed_each = (total_packets as f32 * 0.1 / 3.0) as u32;
            
            // Primary colors
            composition.push(WavePacketSample {
                signature: WavePacketSignature::new(0.0, 0.8 + simple_random(seed) * 0.2, seed as u16),
                amount: primary_each,
            });
            composition.push(WavePacketSample {
                signature: WavePacketSignature::new(0.333, 0.8 + simple_random(seed + 1) * 0.2, (seed + 1) as u16),
                amount: primary_each,
            });
            composition.push(WavePacketSample {
                signature: WavePacketSignature::new(0.667, 0.8 + simple_random(seed + 2) * 0.2, (seed + 2) as u16),
                amount: primary_each,
            });
            
            // Bleed from shell 1
            composition.push(WavePacketSample {
                signature: WavePacketSignature::new(0.167, 0.6 + simple_random(seed + 3) * 0.2, (seed + 3) as u16),
                amount: bleed_each,
            });
            composition.push(WavePacketSample {
                signature: WavePacketSignature::new(0.5, 0.6 + simple_random(seed + 4) * 0.2, (seed + 4) as u16),
                amount: bleed_each,
            });
            composition.push(WavePacketSample {
                signature: WavePacketSignature::new(0.833, 0.6 + simple_random(seed + 5) * 0.2, (seed + 5) as u16),
                amount: bleed_each,
            });
        },
        1 => {
            // Shell 1: Primarily RG, GB, BR (80%), with 10% bleed from shell 0
            let primary_each = (total_packets as f32 * 0.8 / 3.0) as u32;
            let bleed_each = (total_packets as f32 * 0.1 / 3.0) as u32;
            
            // Primary colors (secondary colors)
            composition.push(WavePacketSample {
                signature: WavePacketSignature::new(0.167, 0.8 + simple_random(seed) * 0.2, seed as u16),
                amount: primary_each,
            });
            composition.push(WavePacketSample {
                signature: WavePacketSignature::new(0.5, 0.8 + simple_random(seed + 1) * 0.2, (seed + 1) as u16),
                amount: primary_each,
            });
            composition.push(WavePacketSample {
                signature: WavePacketSignature::new(0.833, 0.8 + simple_random(seed + 2) * 0.2, (seed + 2) as u16),
                amount: primary_each,
            });
            
            // Bleed from shell 0
            composition.push(WavePacketSample {
                signature: WavePacketSignature::new(0.0, 0.6 + simple_random(seed + 3) * 0.2, (seed + 3) as u16),
                amount: bleed_each,
            });
            composition.push(WavePacketSample {
                signature: WavePacketSignature::new(0.333, 0.6 + simple_random(seed + 4) * 0.2, (seed + 4) as u16),
                amount: bleed_each,
            });
            composition.push(WavePacketSample {
                signature: WavePacketSignature::new(0.667, 0.6 + simple_random(seed + 5) * 0.2, (seed + 5) as u16),
                amount: bleed_each,
            });
        },
        _ => {
            // Higher shells: Mixed composition
            let packets_per_color = total_packets / 6;
            for i in 0..6 {
                composition.push(WavePacketSample {
                    signature: WavePacketSignature::new(
                        i as f32 / 6.0,
                        0.5 + simple_random(seed + i as u64) * 0.5,
                        (seed + i as u64) as u16
                    ),
                    amount: packets_per_color,
                });
            }
        }
    }
    
    composition
}

// ============================================================================
// Mining System Reducers
// ============================================================================

#[spacetimedb::reducer]
pub fn start_mining(ctx: &ReducerContext, orb_id: u64) -> Result<(), String> {
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    let crystal = ctx.db.player_crystal()
        .player_id()
        .find(&player.player_id)
        .ok_or("You need a crystal to mine")?;
    
    let orb = ctx.db.wave_packet_orb()
        .orb_id()
        .find(&orb_id)
        .ok_or("Orb not found")?;
    
    // Validate same world
    if player.current_world != orb.world_coords {
        return Err("Orb is in a different world".to_string());
    }
    
    // Validate range (30 units)
    let distance = player.position.distance_to(&orb.position);
    if distance > 30.0 {
        return Err(format!("Orb is too far away ({:.1} units, max 30)", distance));
    }
    
    // Check if orb has matching wave packets
    let has_matching = orb.wave_packet_composition.iter()
        .any(|sample| sample.signature.matches_crystal(&crystal.crystal_type));
    
    if !has_matching {
        return Err("This orb doesn't contain wave packets matching your crystal".to_string());
    }
    
    // Stop any existing mining
    {
        let mut mining_state = get_mining_state().lock().unwrap();
        mining_state.remove(&player.player_id);
    }
    
    // Start new mining session
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    let session = MiningSession {
        player_id: player.player_id,
        orb_id,
        crystal_type: crystal.crystal_type,
        last_extraction: current_time - 2000, // Allow immediate first extraction
        pending_wave_packets: Vec::new(),
    };
    
    {
        let mut mining_state = get_mining_state().lock().unwrap();
        mining_state.insert(player.player_id, session);
    }
    
    log::info!(
        "Player {} started mining orb {} with {} crystal",
        player.name,
        orb_id,
        match crystal.crystal_type {
            CrystalType::Red => "Red",
            CrystalType::Green => "Green",
            CrystalType::Blue => "Blue",
        }
    );
    
    Ok(())
}

#[spacetimedb::reducer]
pub fn stop_mining(ctx: &ReducerContext) -> Result<(), String> {
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    let had_session = {
        let mut mining_state = get_mining_state().lock().unwrap();
        mining_state.remove(&player.player_id).is_some()
    };
    
    if had_session {
        log::info!("Player {} stopped mining", player.name);
        Ok(())
    } else {
        Err("You are not currently mining".to_string())
    }
}

// Add this new reducer for client-driven extraction
#[spacetimedb::reducer]
pub fn extract_wave_packet(ctx: &ReducerContext, orb_id: u64) -> Result<(), String> {
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    // Get mining session
    let mut mining_state = get_mining_state().lock().unwrap();
    let session = mining_state.get_mut(&player.player_id)
        .ok_or("You are not currently mining")?;
    
    // Validate orb ID matches
    if session.orb_id != orb_id {
        return Err("Invalid orb ID".to_string());
    }
    
    // Check cooldown (client should handle this, but double-check)
    if current_time < session.last_extraction + 2000 {
        return Err("Extraction on cooldown".to_string());
    }
    
    // Get fresh orb reference
    let orb = ctx.db.wave_packet_orb()
        .orb_id()
        .find(&orb_id)
        .ok_or("Orb not found")?;
    
    // Validate range
    let distance = player.position.distance_to(&orb.position);
    if distance > 30.0 {
        // Stop mining if out of range
        drop(mining_state);
        stop_mining_internal(ctx, player.player_id)?;
        return Err("Out of mining range".to_string());
    }
    
    // Find matching wave packet
    let matching_sample = orb.wave_packet_composition.iter()
        .find(|s| s.signature.matches_crystal(&session.crystal_type) && s.amount > 0)
        .ok_or("No matching wave packets available")?;
    
    // Create pending wave packet
    let wave_packet_id = get_next_wave_packet_id();
    let flight_time = (distance / 5.0 * 1000.0) as u64; // 5 units/sec = ms flight time
    
    let pending = PendingWavePacket {
        wave_packet_id,
        signature: matching_sample.signature,
        departure_time: current_time,
        expected_arrival: current_time + flight_time,
    };
    
    // Update session
    session.pending_wave_packets.push(pending.clone());
    session.last_extraction = current_time;
    
    // Reduce orb wave packets
    let mut updated_orb = orb.clone();
    for comp in &mut updated_orb.wave_packet_composition {
        if comp.signature == matching_sample.signature && comp.amount > 0 {
            comp.amount -= 1;
            break;
        }
    }
    updated_orb.total_wave_packets = updated_orb.wave_packet_composition.iter()
        .map(|s| s.amount)
        .sum();
    
    ctx.db.wave_packet_orb().delete(orb);
    ctx.db.wave_packet_orb().insert(updated_orb);
    
    log::info!(
        "Player {} extracted wave packet {} ({}) - flight time: {}ms",
        player.name,
        wave_packet_id,
        pending.signature.to_color_string(),
        flight_time
    );
    
    // Return packet info to client through a table insert
    // This notifies the client about the extracted packet
    ctx.db.wave_packet_extraction().insert(WavePacketExtraction {
        extraction_id: wave_packet_id, // Use packet ID as extraction ID
        player_id: player.player_id,
        wave_packet_id,
        signature: pending.signature,
        departure_time: current_time,
        expected_arrival: current_time + flight_time,
    });
    
    Ok(())
}

// Add helper function for internal stop mining
fn stop_mining_internal(ctx: &ReducerContext, player_id: u64) -> Result<(), String> {
    let mut mining_state = get_mining_state().lock().unwrap();
    mining_state.remove(&player_id);
    Ok(())
}

#[spacetimedb::reducer]
pub fn capture_wave_packet(
    ctx: &ReducerContext,
    wave_packet_id: u64,
) -> Result<(), String> {
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    let mut mining_state = get_mining_state().lock().unwrap();
    let session = mining_state.get_mut(&player.player_id)
        .ok_or("You are not currently mining")?;
    
    // Find and validate the wave packet
    let wave_packet_index = session.pending_wave_packets.iter()
        .position(|w| w.wave_packet_id == wave_packet_id)
        .ok_or("Invalid wave packet ID")?;
    
    let wave_packet = session.pending_wave_packets[wave_packet_index].clone();
    
    // Remove from pending regardless of timing (client decides when to capture)
    session.pending_wave_packets.remove(wave_packet_index);
    
    // Add to player storage
    add_wave_packets_to_storage(
        ctx,
        "player".to_string(),
        player.player_id,
        wave_packet.signature,
        1, // Single wave packet
        0, // Source shell (could track from orb)
    )?;
    
    // Remove extraction notification if it exists
    if let Some(extraction) = ctx.db.wave_packet_extraction()
        .extraction_id()
        .find(&wave_packet_id) {
        ctx.db.wave_packet_extraction().delete(extraction);
    }
    
    log::info!(
        "Player {} captured wave packet {} ({})",
        player.name,
        wave_packet_id,
        wave_packet.signature.to_color_string()
    );
    
    Ok(())
}

#[spacetimedb::reducer]
pub fn collect_wave_packet_orb(ctx: &ReducerContext, orb_id: u64) -> Result<(), String> {
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    let orb = ctx.db.wave_packet_orb()
        .orb_id()
        .find(&orb_id)
        .ok_or("Orb not found")?;
    
    // Validate same world
    if player.current_world != orb.world_coords {
        return Err("Orb is in a different world".to_string());
    }
    
    // Validate range (10 units for collection)
    let distance = player.position.distance_to(&orb.position);
    if distance > 10.0 {
        return Err(format!("Too far from orb ({:.1} units, max 10)", distance));
    }
    
    // Transfer all wave packets to player storage
    for sample in &orb.wave_packet_composition {
        if sample.amount > 0 {
            add_wave_packets_to_storage(
                ctx,
                "player".to_string(),
                player.player_id,
                sample.signature,
                sample.amount,
                ((orb.world_coords.x.abs() + orb.world_coords.y.abs() + orb.world_coords.z.abs()) / 3) as u8,
            )?;
        }
    }
    
    // Delete the orb
    ctx.db.wave_packet_orb().delete(orb);
    
    log::info!(
        "Player {} collected orb {} with {} wave packets",
        player.name,
        orb_id,
        orb.total_wave_packets
    );
    
    Ok(())
}

fn add_wave_packets_to_storage(
    ctx: &ReducerContext,
    owner_type: String,
    owner_id: u64,
    signature: WavePacketSignature,
    amount: u32,
    source_shell: u8,
) -> Result<(), String> {
    let frequency_band = get_frequency_band(signature.frequency);
    
    // Find existing storage for this frequency band
    let existing_storage = ctx.db.wave_packet_storage()
        .iter()
        .find(|s| s.owner_type == owner_type && s.owner_id == owner_id && s.frequency_band == frequency_band);
    
    if let Some(storage) = existing_storage {
        let mut updated_storage = storage.clone();
        
        // Find or add the signature sample
        let mut found = false;
        for sample in &mut updated_storage.signature_samples {
            if sample.signature == signature {
                sample.amount += amount;
                found = true;
                break;
            }
        }
        
        if !found {
            updated_storage.signature_samples.push(WavePacketSample { signature, amount });
        }
        
        updated_storage.total_wave_packets += amount;
        updated_storage.last_update = ctx.timestamp
            .duration_since(Timestamp::UNIX_EPOCH)
            .expect("Valid timestamp")
            .as_millis() as u64;
        
        ctx.db.wave_packet_storage().delete(storage);
        ctx.db.wave_packet_storage().insert(updated_storage);
    } else {
        // Create new storage entry
        let sample = WavePacketSample { signature, amount };
        let new_storage = WavePacketStorage {
            storage_id: 0,
            owner_type,
            owner_id,
            frequency_band,
            total_wave_packets: amount,
            signature_samples: vec![sample],
            last_update: ctx.timestamp
                .duration_since(Timestamp::UNIX_EPOCH)
                .expect("Valid timestamp")
                .as_millis() as u64,
        };
        
        ctx.db.wave_packet_storage().insert(new_storage);
    }
    
    Ok(())
}

// ============================================================================
// Game Loop & Tick Processing
// ============================================================================

#[spacetimedb::reducer]
pub fn tick(ctx: &ReducerContext) -> Result<(), String> {
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    // Process circuits and emit wave packet orbs
    for circuit in ctx.db.world_circuit().iter() {
        if current_time >= circuit.last_emission_time + circuit.emission_interval_ms {
            process_circuit_emission(ctx, &circuit)?;
            
            let mut updated_circuit = circuit.clone();
            updated_circuit.last_emission_time = current_time;
            ctx.db.world_circuit().delete(circuit);
            ctx.db.world_circuit().insert(updated_circuit);
        }
    }
    
    // Process orb dissipation
    process_orb_dissipation(ctx)?;
    
    // Client-driven mining - NO automatic processing
    // Removed: process_mining_sessions(ctx)?;
    
    // Clean up expired wave packet orbs
    cleanup_expired_wave_packet_orbs(ctx)?;
    
    // Clean up old extraction notifications
    cleanup_old_extractions(ctx)?;
    
    Ok(())
}

fn process_circuit_emission(ctx: &ReducerContext, circuit: &WorldCircuit) -> Result<(), String> {
    let circuit_position = DbVector3::new(0.0, 100.0, 0.0);
    
    for _ in 0..circuit.orbs_per_emission {
        emit_wave_packet_orb(ctx, circuit.world_coords, circuit_position)?;
    }
    
    log::info!(
        "Emitted {} wave packet orbs from circuit at ({},{},{})",
        circuit.orbs_per_emission,
        circuit.world_coords.x,
        circuit.world_coords.y, 
        circuit.world_coords.z
    );
    
    Ok(())
}

fn process_orb_dissipation(ctx: &ReducerContext) -> Result<(), String> {
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    let tau_ms = 10000u64; // 10 seconds per wave packet
    
    for orb in ctx.db.wave_packet_orb().iter() {
        if orb.total_wave_packets == 0 {
            continue;
        }
        
        let time_since_last = current_time - orb.last_dissipation;
        if time_since_last < 1000 { // Check every second
            continue;
        }
        
        // Calculate dissipation probability
        let dissipation_prob = 1.0 - (-1.0f32 * time_since_last as f32 / tau_ms as f32).exp();
        let seed = current_time.wrapping_add(orb.orb_id);
        
        if simple_random(seed) < dissipation_prob {
            let mut updated_orb = orb.clone();
            
            // Remove one random wave packet
            let total_wave_packets: u32 = updated_orb.wave_packet_composition.iter()
                .map(|s| s.amount)
                .sum();
            
            if total_wave_packets > 0 {
                let random_index = (simple_random(seed.wrapping_add(1)) * total_wave_packets as f32) as u32;
                let mut cumulative = 0u32;
                
                for sample in &mut updated_orb.wave_packet_composition {
                    cumulative += sample.amount;
                    if random_index < cumulative && sample.amount > 0 {
                        sample.amount -= 1;
                        break;
                    }
                }
                
                updated_orb.total_wave_packets = updated_orb.wave_packet_composition.iter()
                    .map(|s| s.amount)
                    .sum();
                updated_orb.last_dissipation = current_time;
                
                ctx.db.wave_packet_orb().delete(orb);
                ctx.db.wave_packet_orb().insert(updated_orb);
            }
        }
    }
    
    Ok(())
}

fn cleanup_expired_wave_packet_orbs(ctx: &ReducerContext) -> Result<(), String> {
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    let expired_orbs: Vec<_> = ctx.db.wave_packet_orb()
        .iter()
        .filter(|orb| current_time > orb.creation_time + orb.lifetime_ms as u64 || orb.total_wave_packets == 0)
        .collect();
    
    let expired_count = expired_orbs.len();
    
    for orb in expired_orbs {
        ctx.db.wave_packet_orb().delete(orb);
    }
    
    if expired_count > 0 {
        log::info!("Cleaned up {} expired wave packet orbs", expired_count);
    }
    
    Ok(())
}

// Add cleanup for extraction notifications
fn cleanup_old_extractions(ctx: &ReducerContext) -> Result<(), String> {
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    // Remove extractions older than 10 seconds
    let old_extractions: Vec<_> = ctx.db.wave_packet_extraction()
        .iter()
        .filter(|e| current_time > e.expected_arrival + 10000)
        .collect();
    
    for extraction in old_extractions {
        ctx.db.wave_packet_extraction().delete(extraction);
    }
    
    Ok(())
}

// ============================================================================
// Debug Reducers
// ============================================================================

#[spacetimedb::reducer]
pub fn init(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("Manual initialization requested...");
    init_game_world(ctx)?;
    tick(ctx)?;
    log::info!("Initialization complete!");
    Ok(())
}

#[spacetimedb::reducer]
pub fn debug_give_crystal(
    ctx: &ReducerContext,
    player_id: u64,
    crystal_type: CrystalType,
) -> Result<(), String> {
    let player = ctx.db.player()
        .player_id()
        .find(&player_id)
        .ok_or("Player not found")?;
    
    // Remove existing crystal if any
    if let Some(existing) = ctx.db.player_crystal().player_id().find(&player_id) {
        ctx.db.player_crystal().delete(existing);
    }
    
    let crystal = PlayerCrystal {
        player_id,
        crystal_type,
        slot_count: 1,
        chosen_at: ctx.timestamp
            .duration_since(Timestamp::UNIX_EPOCH)
            .expect("Valid timestamp")
            .as_millis() as u64,
    };
    
    ctx.db.player_crystal().insert(crystal);
    
    log::info!(
        "DEBUG: Gave {} crystal to player {}",
        match crystal_type {
            CrystalType::Red => "Red",
            CrystalType::Green => "Green",
            CrystalType::Blue => "Blue",
        },
        player.name
    );
    
    Ok(())
}

#[spacetimedb::reducer]
pub fn debug_mining_status(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("=== MINING STATUS ===");
    
    let mining_state = get_mining_state().lock().unwrap();
    log::info!("Active mining sessions: {}", mining_state.len());
    
    for (player_id, session) in mining_state.iter() {
        if let Some(player) = ctx.db.player().player_id().find(player_id) {
            log::info!(
                "  Player {}: mining orb {}, {} pending wave packets",
                player.name,
                session.orb_id,
                session.pending_wave_packets.len()
            );
        }
    }
    
    Ok(())
}

#[spacetimedb::reducer]
pub fn debug_wave_packet_status(ctx: &ReducerContext) -> Result<(), String> {
    let orb_count = ctx.db.wave_packet_orb().iter().count();
    let storage_count = ctx.db.wave_packet_storage().iter().count();
    
    log::info!("=== WAVE PACKET SYSTEM STATUS ===");
    log::info!("Active wave packet orbs: {}", orb_count);
    log::info!("Storage entries: {}", storage_count);
    
    let mut orbs_by_world = std::collections::HashMap::new();
    for orb in ctx.db.wave_packet_orb().iter() {
        *orbs_by_world.entry(orb.world_coords).or_insert(0) += 1;
    }
    
    for (coords, count) in orbs_by_world {
        log::info!("  World ({},{},{}): {} orbs", coords.x, coords.y, coords.z, count);
    }
    
    let mut wave_packets_by_band = std::collections::HashMap::new();
    for storage in ctx.db.wave_packet_storage().iter() {
        *wave_packets_by_band.entry(storage.frequency_band).or_insert(0) += storage.total_wave_packets;
    }
    
    log::info!("\nWave packets stored by frequency band:");
    for (band, total) in wave_packets_by_band {
        log::info!("  {:?}: {} wave packets", band, total);
    }
    
    Ok(())
}

// ============================================================================
// Connection Lifecycle
// ============================================================================

#[spacetimedb::reducer]
pub fn connect(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("New connection from identity: {:?}", ctx.sender);
    tick(ctx)?;
    Ok(())
}

#[spacetimedb::reducer]
pub fn disconnect(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("Disconnection from identity: {:?}", ctx.sender);
    
    if let Some(player) = ctx.db.player().identity().find(&ctx.sender) {
        // Stop any active mining
        {
            let mut mining_state = get_mining_state().lock().unwrap();
            mining_state.remove(&player.player_id);
        }
        
        ctx.db.logged_out_player().insert(LoggedOutPlayer {
            identity: player.identity,
            player_id: player.player_id,
            name: player.name.clone(),
            logout_time: ctx.timestamp,
        });
        
        ctx.db.player().delete(player);
        log::info!("Player logged out and saved for later restoration");
    }
    
    if let Some(link) = ctx.db.account_identity().identity().find(&ctx.sender) {
        ctx.db.account_identity().delete(link);
    }
    
    Ok(())
}