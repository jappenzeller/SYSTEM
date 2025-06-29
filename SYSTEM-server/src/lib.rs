// SYSTEM Server - Quantum Metaverse with Quanta Processing
// Single file architecture for clean compilation

use spacetimedb::{Identity, ReducerContext, Table, Timestamp, SpacetimeType};
use std::f32::consts::PI;
use std::collections::HashMap;
use std::sync::Mutex;

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
// Quanta System Types
// ============================================================================

#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq)]
pub struct QuantaSignature {
    pub frequency: f32,
    pub resonance: f32,
    pub flux_pattern: u16,
}

impl QuantaSignature {
    pub fn get_frequency_band(&self) -> FrequencyBand {
        match self.frequency {
            f if f < 0.1 => FrequencyBand::Infrared,
            f if f < 0.3 => FrequencyBand::Red,
            f if f < 0.4 => FrequencyBand::Orange,
            f if f < 0.5 => FrequencyBand::Yellow,
            f if f < 0.65 => FrequencyBand::Green,
            f if f < 0.8 => FrequencyBand::Blue,
            f if f < 0.95 => FrequencyBand::Violet,
            _ => FrequencyBand::Ultraviolet,
        }
    }
    
    pub fn to_color_string(&self) -> &'static str {
        match self.get_frequency_band() {
            FrequencyBand::Infrared => "Infrared",
            FrequencyBand::Red => "Red",
            FrequencyBand::Orange => "Orange",
            FrequencyBand::Yellow => "Yellow",
            FrequencyBand::Green => "Green",
            FrequencyBand::Blue => "Blue",
            FrequencyBand::Violet => "Violet",
            FrequencyBand::Ultraviolet => "Ultraviolet",
        }
    }
    
    pub fn to_radians(&self) -> f32 {
        self.frequency * 2.0 * PI
    }
    
    pub fn matches_crystal(&self, crystal: &CrystalType) -> bool {
        let radian = self.to_radians();
        let (center, _) = crystal.get_radian_range();
        let diff = (radian - center).abs();
        let wrapped_diff = if diff > PI { 2.0 * PI - diff } else { diff };
        wrapped_diff <= PI / 6.0
    }
}

#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum FrequencyBand {
    Infrared,    // 0.0-0.1   (IR)
    Red,         // 0.1-0.3   (Red)
    Orange,      // 0.3-0.4   (Orange)
    Yellow,      // 0.4-0.5   (Yellow)
    Green,       // 0.5-0.65  (Green)
    Blue,        // 0.65-0.8  (Blue)
    Violet,      // 0.8-0.95  (Violet)
    Ultraviolet, // 0.95-1.0  (UV)
}

#[derive(SpacetimeType, Debug, Clone)]
pub struct QuantaSample {
    pub signature: QuantaSignature,
    pub amount: u32,
    pub source_shell: u8,
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
// Quanta System Tables
// ============================================================================

#[spacetimedb::table(name = quanta_orb, public)]
#[derive(Debug, Clone)]
pub struct QuantaOrb {
    #[primary_key]
    #[auto_inc]
    pub orb_id: u64,
    pub world_coords: WorldCoords,
    pub position: DbVector3,
    pub velocity: DbVector3,
    pub quanta_composition: Vec<QuantaSample>, // Multiple frequencies in one orb
    pub total_quanta: u32,
    pub creation_time: u64,
    pub lifetime_ms: u32,
    pub last_dissipation: u64,
}

#[spacetimedb::table(name = quanta_storage, public)]
#[derive(Debug, Clone)]
pub struct QuantaStorage {
    #[primary_key]
    #[auto_inc]
    pub storage_id: u64,
    pub owner_type: String,
    pub owner_id: u64,
    pub frequency_band: FrequencyBand,
    pub total_quanta: u32,
    pub signature_samples: Vec<QuantaSample>,
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

// ============================================================================
// Mining System State (In-Memory)
// ============================================================================

lazy_static::lazy_static! {
    static ref MINING_STATE: Mutex<HashMap<u64, MiningSession>> = Mutex::new(HashMap::new());
    static ref QUANTUM_COUNTER: Mutex<u64> = Mutex::new(0);
}

struct MiningSession {
    player_id: u64,
    orb_id: u64,
    crystal_type: CrystalType,
    last_extraction: u64,
    pending_quanta: Vec<PendingQuantum>,
}

struct PendingQuantum {
    quantum_id: u64,
    signature: QuantaSignature,
    departure_time: u64,
    expected_arrival: u64,
}

fn get_next_quantum_id() -> u64 {
    let mut counter = QUANTUM_COUNTER.lock().unwrap();
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
    
    // Create center world
    let center = WorldCoords { x: 0, y: 0, z: 0 };
    ctx.db.world().insert(World {
        world_coords: center,
        world_name: "Genesis Core".to_string(),
        world_type: "center".to_string(),
        shell_level: 0,
    });
    
    // Create center world circuit
    ctx.db.world_circuit().insert(WorldCircuit {
        world_coords: center,
        circuit_id: 0,  // Let auto_inc handle this
        qubit_count: 12,
        emission_interval_ms: 5000,
        orbs_per_emission: 8,
        last_emission_time: 0,
    });
    
    // Create Shell 1 worlds
    let shell1_offsets = vec![
        (1, 0, 0), (-1, 0, 0),
        (0, 1, 0), (0, -1, 0),
        (0, 0, 1), (0, 0, -1),
    ];
    
    for (i, (dx, dy, dz)) in shell1_offsets.iter().enumerate() {
        let coords = WorldCoords { x: *dx, y: *dy, z: *dz };
        ctx.db.world().insert(World {
            world_coords: coords,
            world_name: format!("Shell 1 World {}", i + 1),
            world_type: "standard".to_string(),
            shell_level: 1,
        });
        
        ctx.db.world_circuit().insert(WorldCircuit {
            world_coords: coords,
            circuit_id: 0,  // Let auto_inc handle this
            qubit_count: 8,
            emission_interval_ms: 10000,
            orbs_per_emission: 5,
            last_emission_time: 0,
        });
    }
    
    init_game_settings(ctx)?;
    
    log::info!("Game world initialized with center and 6 Shell 1 worlds");
    Ok(())
}

fn init_game_settings(ctx: &ReducerContext) -> Result<(), String> {
    // Check if settings already exist
    if ctx.db.game_settings().iter().count() > 0 {
        log::info!("Game settings already initialized, skipping...");
        return Ok(());
    }
    
    let settings = vec![
        ("orb_lifetime_ms", "int", "30000", "How long quanta orbs exist before despawn"),
        ("orb_fall_gravity", "float", "-9.81", "Gravity acceleration for falling orbs"),
        ("collection_radius", "float", "2.5", "Radius for collecting orbs"),
        ("emission_variance", "float", "0.2", "Random variance in emission timing"),
        ("mining_range", "float", "30.0", "Maximum mining range in units"),
        ("extraction_interval_ms", "int", "2000", "Time between quantum extractions"),
        ("quantum_velocity", "float", "5.0", "Speed of quantum flight in units/second"),
        ("dissipation_tau_ms", "int", "10000", "Time constant for orb dissipation"),
    ];
    
    for (key, value_type, value, desc) in settings {
        ctx.db.game_settings().insert(GameSettings {
            setting_key: key.to_string(),
            value_type: value_type.to_string(),
            value: value.to_string(),
            description: desc.to_string(),
        });
    }
    
    log::info!("Game settings initialized");
    Ok(())
}

// ============================================================================
// Helper Functions
// ============================================================================

fn simple_random(seed: u64) -> f32 {
    let a = 1664525u64;
    let c = 1013904223u64;
    let m = 2u64.pow(32);
    let next = (a.wrapping_mul(seed).wrapping_add(c)) % m;
    (next as f32) / (m as f32)
}

fn generate_orb_composition(shell_level: u8, seed: u64) -> Vec<QuantaSample> {
    let mut composition = Vec::new();
    let total_quanta = 100u32;
    
    match shell_level {
        0 => {
            // Shell 0: 80% R/G/B, 10% RG/GB/BR, 10% future
            let primary_each = 27u32; // ~80/3
            let secondary_each = 3u32; // ~10/3
            
            // Primary colors (R, G, B)
            composition.push(QuantaSample {
                signature: QuantaSignature {
                    frequency: 0.2,  // Red
                    resonance: 0.5 + simple_random(seed) * 0.5,
                    flux_pattern: (simple_random(seed.wrapping_add(1)) * 65535.0) as u16,
                },
                amount: primary_each,
                source_shell: 0,
            });
            
            composition.push(QuantaSample {
                signature: QuantaSignature {
                    frequency: 0.575, // Green
                    resonance: 0.5 + simple_random(seed.wrapping_add(2)) * 0.5,
                    flux_pattern: (simple_random(seed.wrapping_add(3)) * 65535.0) as u16,
                },
                amount: primary_each,
                source_shell: 0,
            });
            
            composition.push(QuantaSample {
                signature: QuantaSignature {
                    frequency: 0.725, // Blue
                    resonance: 0.5 + simple_random(seed.wrapping_add(4)) * 0.5,
                    flux_pattern: (simple_random(seed.wrapping_add(5)) * 65535.0) as u16,
                },
                amount: primary_each,
                source_shell: 0,
            });
            
            // Secondary colors (RG, GB, BR)
            composition.push(QuantaSample {
                signature: QuantaSignature {
                    frequency: 0.45, // Yellow (RG)
                    resonance: 0.5 + simple_random(seed.wrapping_add(6)) * 0.5,
                    flux_pattern: (simple_random(seed.wrapping_add(7)) * 65535.0) as u16,
                },
                amount: secondary_each,
                source_shell: 1,
            });
            
            composition.push(QuantaSample {
                signature: QuantaSignature {
                    frequency: 0.65, // Cyan (GB)
                    resonance: 0.5 + simple_random(seed.wrapping_add(8)) * 0.5,
                    flux_pattern: (simple_random(seed.wrapping_add(9)) * 65535.0) as u16,
                },
                amount: secondary_each,
                source_shell: 1,
            });
            
            composition.push(QuantaSample {
                signature: QuantaSignature {
                    frequency: 0.85, // Magenta (BR)
                    resonance: 0.5 + simple_random(seed.wrapping_add(10)) * 0.5,
                    flux_pattern: (simple_random(seed.wrapping_add(11)) * 65535.0) as u16,
                },
                amount: secondary_each + 1, // +1 to reach 100
                source_shell: 1,
            });
        },
        1 => {
            // Shell 1: 80% RG/GB/BR, 10% R/G/B, 10% future
            let primary_each = 27u32;
            let secondary_each = 3u32;
            
            // Primary colors for Shell 1 (RG, GB, BR)
            composition.push(QuantaSample {
                signature: QuantaSignature {
                    frequency: 0.45, // Yellow (RG)
                    resonance: 0.5 + simple_random(seed) * 0.5,
                    flux_pattern: (simple_random(seed.wrapping_add(1)) * 65535.0) as u16,
                },
                amount: primary_each,
                source_shell: 1,
            });
            
            composition.push(QuantaSample {
                signature: QuantaSignature {
                    frequency: 0.65, // Cyan (GB)
                    resonance: 0.5 + simple_random(seed.wrapping_add(2)) * 0.5,
                    flux_pattern: (simple_random(seed.wrapping_add(3)) * 65535.0) as u16,
                },
                amount: primary_each,
                source_shell: 1,
            });
            
            composition.push(QuantaSample {
                signature: QuantaSignature {
                    frequency: 0.85, // Magenta (BR)
                    resonance: 0.5 + simple_random(seed.wrapping_add(4)) * 0.5,
                    flux_pattern: (simple_random(seed.wrapping_add(5)) * 65535.0) as u16,
                },
                amount: primary_each,
                source_shell: 1,
            });
            
            // Secondary colors for Shell 1 (R, G, B)
            composition.push(QuantaSample {
                signature: QuantaSignature {
                    frequency: 0.2, // Red
                    resonance: 0.5 + simple_random(seed.wrapping_add(6)) * 0.5,
                    flux_pattern: (simple_random(seed.wrapping_add(7)) * 65535.0) as u16,
                },
                amount: secondary_each,
                source_shell: 0,
            });
            
            composition.push(QuantaSample {
                signature: QuantaSignature {
                    frequency: 0.575, // Green
                    resonance: 0.5 + simple_random(seed.wrapping_add(8)) * 0.5,
                    flux_pattern: (simple_random(seed.wrapping_add(9)) * 65535.0) as u16,
                },
                amount: secondary_each,
                source_shell: 0,
            });
            
            composition.push(QuantaSample {
                signature: QuantaSignature {
                    frequency: 0.725, // Blue
                    resonance: 0.5 + simple_random(seed.wrapping_add(10)) * 0.5,
                    flux_pattern: (simple_random(seed.wrapping_add(11)) * 65535.0) as u16,
                },
                amount: secondary_each + 1,
                source_shell: 0,
            });
        },
        _ => {
            // Future shells - for now just emit mixed composition
            composition.push(QuantaSample {
                signature: QuantaSignature {
                    frequency: 0.5,
                    resonance: 0.5 + simple_random(seed) * 0.5,
                    flux_pattern: (simple_random(seed.wrapping_add(1)) * 65535.0) as u16,
                },
                amount: total_quanta,
                source_shell: shell_level,
            });
        }
    }
    
    composition
}

fn calculate_orb_color(composition: &[QuantaSample]) -> DbVector3 {
    if composition.is_empty() {
        return DbVector3::new(0.5, 0.5, 0.5); // Gray if empty
    }
    
    let total_quanta: u32 = composition.iter().map(|s| s.amount).sum();
    if total_quanta == 0 {
        return DbVector3::new(0.1, 0.1, 0.1); // Dark if depleted
    }
    
    // Calculate weighted average of all quanta radians
    let mut weighted_x = 0.0f32;
    let mut weighted_y = 0.0f32;
    
    for sample in composition {
        let weight = sample.amount as f32 / total_quanta as f32;
        let radians = sample.signature.to_radians();
        weighted_x += radians.cos() * weight;
        weighted_y += radians.sin() * weight;
    }
    
    // Convert back to frequency
    let avg_radians = weighted_y.atan2(weighted_x);
    let normalized_radians = if avg_radians < 0.0 {
        avg_radians + 2.0 * PI
    } else {
        avg_radians
    };
    
    let avg_frequency = normalized_radians / (2.0 * PI);
    
    // Map frequency to RGB
    frequency_to_rgb(avg_frequency)
}

fn frequency_to_rgb(frequency: f32) -> DbVector3 {
    // Simple HSV to RGB conversion where frequency maps to hue
    let hue = frequency * 360.0;
    let saturation = 1.0;
    let value = 1.0;
    
    let c = value * saturation;
    let x = c * (1.0 - ((hue / 60.0) % 2.0 - 1.0).abs());
    let m = value - c;
    
    let (r, g, b) = match hue as u32 {
        0..=59 => (c, x, 0.0),
        60..=119 => (x, c, 0.0),
        120..=179 => (0.0, c, x),
        180..=239 => (0.0, x, c),
        240..=299 => (x, 0.0, c),
        300..=359 => (c, 0.0, x),
        _ => (c, 0.0, 0.0),
    };
    
    DbVector3::new(r + m, g + m, b + m)
}

// ============================================================================
// Authentication Reducers
// ============================================================================

fn hash_password(password: &str) -> String {
    let mut hash = 0u64;
    for byte in password.bytes() {
        hash = hash.wrapping_mul(31).wrapping_add(byte as u64);
    }
    format!("{:x}", hash)
}

#[spacetimedb::reducer]
pub fn register_account(
    ctx: &ReducerContext,
    username: String,
    password: String,
) -> Result<(), String> {
    if username.len() < 3 {
        return Err("Username must be at least 3 characters".to_string());
    }
    
    if password.len() < 6 {
        return Err("Password must be at least 6 characters".to_string());
    }
    
    if ctx.db.user_account().username().find(&username).is_some() {
        return Err("Username already exists".to_string());
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
    log::info!("Created new player: {}", player_name);
    Ok(())
}

#[spacetimedb::reducer]
pub fn update_player_position(
    ctx: &ReducerContext,
    position: DbVector3,
    rotation: DbVector3,
) -> Result<(), String> {
    let sender_identity = ctx.sender;
    
    if let Some(mut player) = ctx.db.player().identity().find(&sender_identity) {
        player.position = position;
        player.rotation = rotation;
        player.last_update = ctx.timestamp;
        
        let old_player = ctx.db.player().identity().find(&sender_identity).unwrap();
        ctx.db.player().delete(old_player);
        ctx.db.player().insert(player);
    } else {
        log::error!("Attempted to update position for non-existent player with identity: {:?}", sender_identity);
    }
    Ok(())
}

// ============================================================================
// Mining System Reducers
// ============================================================================

#[spacetimedb::reducer]
pub fn choose_starting_crystal(
    ctx: &ReducerContext,
    crystal_type: CrystalType,
) -> Result<(), String> {
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    // Check if already has a crystal
    if ctx.db.player_crystal().player_id().find(&player.player_id).is_some() {
        return Err("You already have a crystal".to_string());
    }
    
    let crystal = PlayerCrystal {
        player_id: player.player_id,
        crystal_type,
        slot_count: 1, // Free players get 1 slot
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

#[spacetimedb::reducer]
pub fn start_mining(
    ctx: &ReducerContext,
    orb_id: u64,
) -> Result<(), String> {
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    let crystal = ctx.db.player_crystal()
        .player_id()
        .find(&player.player_id)
        .ok_or("You need a crystal to mine")?;
    
    let orb = ctx.db.quanta_orb()
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
    
    // Check if orb has matching quanta
    let has_matching = orb.quanta_composition.iter()
        .any(|sample| sample.signature.matches_crystal(&crystal.crystal_type));
    
    if !has_matching {
        return Err("This orb doesn't contain quanta matching your crystal".to_string());
    }
    
    // Stop any existing mining
    {
        let mut mining_state = MINING_STATE.lock().unwrap();
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
        last_extraction: current_time,
        pending_quanta: Vec::new(),
    };
    
    {
        let mut mining_state = MINING_STATE.lock().unwrap();
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
        let mut mining_state = MINING_STATE.lock().unwrap();
        mining_state.remove(&player.player_id).is_some()
    };
    
    if had_session {
        log::info!("Player {} stopped mining", player.name);
        Ok(())
    } else {
        Err("You are not currently mining".to_string())
    }
}

#[spacetimedb::reducer]
pub fn capture_quantum(
    ctx: &ReducerContext,
    quantum_id: u64,
) -> Result<(), String> {
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    let mut mining_state = MINING_STATE.lock().unwrap();
    let session = mining_state.get_mut(&player.player_id)
        .ok_or("You are not currently mining")?;
    
    // Find and validate the quantum
    let quantum_index = session.pending_quanta.iter()
        .position(|q| q.quantum_id == quantum_id)
        .ok_or("Invalid quantum ID")?;
    
    let quantum = &session.pending_quanta[quantum_index];
    
    // Check if it's ready for capture
    if current_time < quantum.expected_arrival {
        return Err("Quantum hasn't arrived yet".to_string());
    }
    
    // Add to player storage
    add_quanta_to_storage(
        ctx,
        "player".to_string(),
        player.player_id,
        quantum.signature,
        1, // Single quantum
        0, // Source shell (could track from orb)
    )?;
    
    // Remove from pending
    session.pending_quanta.remove(quantum_index);
    
    log::info!(
        "Player {} captured quantum {} ({})",
        player.name,
        quantum_id,
        quantum.signature.to_color_string()
    );
    
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
    
    // Process circuits and emit quanta
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
    
    // Process active mining sessions
    process_mining_sessions(ctx)?;
    
    // Clean up expired quanta orbs
    cleanup_expired_quanta_orbs(ctx)?;
    
    Ok(())
}

fn process_circuit_emission(ctx: &ReducerContext, circuit: &WorldCircuit) -> Result<(), String> {
    let circuit_position = DbVector3::new(0.0, 100.0, 0.0);
    
    for _ in 0..circuit.orbs_per_emission {
        emit_quanta_orb(ctx, circuit.world_coords, circuit_position)?;
    }
    
    log::info!(
        "Emitted {} quanta orbs from circuit at ({},{},{})",
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
    
    let tau_ms = 10000u64; // 10 seconds per quantum
    
    for orb in ctx.db.quanta_orb().iter() {
        if orb.total_quanta == 0 {
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
            
            // Remove one random quantum
            let total_quanta: u32 = updated_orb.quanta_composition.iter()
                .map(|s| s.amount)
                .sum();
            
            if total_quanta > 0 {
                let random_index = (simple_random(seed.wrapping_add(1)) * total_quanta as f32) as u32;
                let mut cumulative = 0u32;
                
                for sample in &mut updated_orb.quanta_composition {
                    cumulative += sample.amount;
                    if random_index < cumulative && sample.amount > 0 {
                        sample.amount -= 1;
                        break;
                    }
                }
                
                updated_orb.total_quanta = updated_orb.quanta_composition.iter()
                    .map(|s| s.amount)
                    .sum();
                updated_orb.last_dissipation = current_time;
                
                ctx.db.quanta_orb().delete(orb);
                ctx.db.quanta_orb().insert(updated_orb);
            }
        }
    }
    
    Ok(())
}

fn process_mining_sessions(ctx: &ReducerContext) -> Result<(), String> {
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    let mut sessions_to_process = Vec::new();
    {
        let mining_state = MINING_STATE.lock().unwrap();
        for (player_id, session) in mining_state.iter() {
            sessions_to_process.push((*player_id, session.orb_id, session.crystal_type));
        }
    }
    
    for (player_id, orb_id, crystal_type) in sessions_to_process {
        // Get fresh references
        let player = match ctx.db.player().player_id().find(&player_id) {
            Some(p) => p,
            None => continue,
        };
        
        let orb = match ctx.db.quanta_orb().orb_id().find(&orb_id) {
            Some(o) => o,
            None => {
                // Orb gone, stop mining
                let mut mining_state = MINING_STATE.lock().unwrap();
                mining_state.remove(&player_id);
                continue;
            }
        };
        
        // Check range
        let distance = player.position.distance_to(&orb.position);
        if distance > 30.0 {
            // Out of range, stop mining
            let mut mining_state = MINING_STATE.lock().unwrap();
            mining_state.remove(&player_id);
            continue;
        }
        
        let mut mining_state = MINING_STATE.lock().unwrap();
        if let Some(session) = mining_state.get_mut(&player_id) {
            // Check if time for next extraction
            if current_time >= session.last_extraction + 2000 {
                // Try to extract matching quantum
                let matching_sample = orb.quanta_composition.iter()
                    .find(|s| s.signature.matches_crystal(&crystal_type) && s.amount > 0);
                
                if let Some(sample) = matching_sample {
                    // Create pending quantum
                    let quantum_id = get_next_quantum_id();
                    let flight_time = (distance / 5.0 * 1000.0) as u64;
                    
                    let pending = PendingQuantum {
                        quantum_id,
                        signature: sample.signature,
                        departure_time: current_time,
                        expected_arrival: current_time + flight_time,
                    };
                    
                    session.pending_quanta.push(pending);
                    session.last_extraction = current_time;
                    
                    // Reduce orb quanta
                    let mut updated_orb = orb.clone();
                    for comp in &mut updated_orb.quanta_composition {
                        if comp.signature == sample.signature && comp.amount > 0 {
                            comp.amount -= 1;
                            break;
                        }
                    }
                    updated_orb.total_quanta = updated_orb.quanta_composition.iter()
                        .map(|s| s.amount)
                        .sum();
                    
                    ctx.db.quanta_orb().delete(orb);
                    ctx.db.quanta_orb().insert(updated_orb);
                    
                    log::info!(
                        "Extracted quantum {} for player {} (flight time: {}ms)",
                        quantum_id,
                        player.name,
                        flight_time
                    );
                }
            }
            
            // Clean up old pending quanta (evaporate after 1s grace period)
            session.pending_quanta.retain(|q| current_time < q.expected_arrival + 1000);
        }
    }
    
    Ok(())
}

fn cleanup_expired_quanta_orbs(ctx: &ReducerContext) -> Result<(), String> {
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    let expired_orbs: Vec<_> = ctx.db.quanta_orb()
        .iter()
        .filter(|orb| current_time > orb.creation_time + orb.lifetime_ms as u64 || orb.total_quanta == 0)
        .collect();
    
    let expired_count = expired_orbs.len();
    
    for orb in expired_orbs {
        ctx.db.quanta_orb().delete(orb);
    }
    
    if expired_count > 0 {
        log::info!("Cleaned up {} expired/empty quanta orbs", expired_count);
    }
    
    Ok(())
}

// ============================================================================
// Quanta System Reducers
// ============================================================================

#[spacetimedb::reducer]
pub fn emit_quanta_orb(
    ctx: &ReducerContext,
    world_coords: WorldCoords,
    circuit_position: DbVector3,
) -> Result<(), String> {
    let world = ctx.db.world()
        .iter()
        .find(|w| w.world_coords == world_coords)
        .ok_or("World not found")?;
        
    let circuit = ctx.db.world_circuit()
        .iter()
        .find(|c| c.world_coords == world_coords)
        .ok_or("Circuit not found")?;
    
    // Generate signature based on shell level and circuit
    let timestamp_ms = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
        
    let seed = timestamp_ms
        .wrapping_add(circuit.circuit_id)
        .wrapping_add((world_coords.x as u64).wrapping_mul(73))
        .wrapping_add((world_coords.y as u64).wrapping_mul(179))
        .wrapping_add((world_coords.z as u64).wrapping_mul(283));
    
    // Generate composition based on shell level
    let composition = generate_orb_composition(world.shell_level, seed);
    let total_quanta = composition.iter().map(|s| s.amount).sum();
    
    // Volcano-style emission
    let angle = simple_random(seed.wrapping_add(3)) * 2.0 * PI;
    let h_speed = 15.0 + simple_random(seed.wrapping_add(4)) * 10.0;
    let v_speed = 20.0 + simple_random(seed.wrapping_add(5)) * 15.0;
    
    let orb = QuantaOrb {
        orb_id: 0,
        world_coords,
        position: circuit_position,
        velocity: DbVector3::new(
            angle.cos() * h_speed,
            v_speed,
            angle.sin() * h_speed,
        ),
        quanta_composition: composition,
        total_quanta,
        creation_time: timestamp_ms,
        lifetime_ms: 30000,
        last_dissipation: timestamp_ms,
    };
    
    ctx.db.quanta_orb().insert(orb);
    Ok(())
}

#[spacetimedb::reducer]
pub fn collect_quanta_orb(
    ctx: &ReducerContext,
    orb_id: u64,
    player_id: u64,
) -> Result<(), String> {
    let orb = ctx.db.quanta_orb()
        .orb_id()
        .find(&orb_id)
        .ok_or("Orb not found")?;
        
    let player = ctx.db.player()
        .player_id()
        .find(&player_id)
        .ok_or("Player not found")?;
        
    if player.identity != ctx.sender {
        return Err("Not your player".to_string());
    }
    
    let world = ctx.db.world()
        .iter()
        .find(|w| w.world_coords == orb.world_coords)
        .ok_or("World not found")?;
    
    // Add all quanta to storage
    for sample in &orb.quanta_composition {
        if sample.amount > 0 {
            add_quanta_to_storage(
                ctx,
                "player".to_string(),
                player_id,
                sample.signature,
                sample.amount,
                world.shell_level,
            )?;
        }
    }
    
    ctx.db.quanta_orb().delete(orb);
    
    log::info!(
        "Player {} collected orb with {} total quanta",
        player.name,
        orb.total_quanta
    );
    
    Ok(())
}

// ============================================================================
// Quanta Storage Management
// ============================================================================

fn add_quanta_to_storage(
    ctx: &ReducerContext,
    owner_type: String,
    owner_id: u64,
    signature: QuantaSignature,
    amount: u32,
    source_shell: u8,
) -> Result<(), String> {
    let frequency_band = signature.get_frequency_band();
    
    let existing_storage = ctx.db.quanta_storage()
        .iter()
        .find(|s| s.owner_type == owner_type && 
                  s.owner_id == owner_id && 
                  s.frequency_band == frequency_band);
    
    if let Some(mut storage) = existing_storage {
        storage.total_quanta += amount;
        
        let sample = QuantaSample {
            signature,
            amount,
            source_shell,
        };
        
        storage.signature_samples.push(sample);
        
        if storage.signature_samples.len() > 10 {
            storage.signature_samples.remove(0);
        }
        
        storage.last_update = ctx.timestamp
            .duration_since(Timestamp::UNIX_EPOCH)
            .expect("Valid timestamp")
            .as_millis() as u64;
        
        let old_storage = ctx.db.quanta_storage()
            .iter()
            .find(|s| s.storage_id == storage.storage_id)
            .unwrap();
        ctx.db.quanta_storage().delete(old_storage);
        ctx.db.quanta_storage().insert(storage);
    } else {
        let sample = QuantaSample {
            signature,
            amount,
            source_shell,
        };
        
        let new_storage = QuantaStorage {
            storage_id: 0,
            owner_type,
            owner_id,
            frequency_band,
            total_quanta: amount,
            signature_samples: vec![sample],
            last_update: ctx.timestamp
                .duration_since(Timestamp::UNIX_EPOCH)
                .expect("Valid timestamp")
                .as_millis() as u64,
        };
        
        ctx.db.quanta_storage().insert(new_storage);
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
    
    let mining_state = MINING_STATE.lock().unwrap();
    log::info!("Active mining sessions: {}", mining_state.len());
    
    for (player_id, session) in mining_state.iter() {
        if let Some(player) = ctx.db.player().player_id().find(player_id) {
            log::info!(
                "  Player {}: mining orb {}, {} pending quanta",
                player.name,
                session.orb_id,
                session.pending_quanta.len()
            );
        }
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
            let mut mining_state = MINING_STATE.lock().unwrap();
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