use spacetimedb::{
    log, Identity, ReducerContext, ScheduleAt, SpacetimeType, Table, Timestamp,
};
use std::time::Duration;
use std::collections::HashMap;
use std::sync::{Mutex, OnceLock};
use std::f32::consts::PI;

// ============================================================================
// World Constants
// ============================================================================

/// Radius of the game world sphere in units
const WORLD_RADIUS: f32 = 300.0;

/// Height offset above the sphere surface for player spawning
const SURFACE_OFFSET: f32 = 1.0;
/// Packet travel speed for transfer timing (units per second)
const PACKET_SPEED: f32 = 5.0;

// Packet height constants (must match Unity CircuitConstants.cs)
/// Height for mining packets (orb to player)
const MINING_PACKET_HEIGHT: f32 = 1.0;
/// Height for packets at objects (players, storage devices)
const OBJECT_PACKET_HEIGHT: f32 = 1.0;
/// Height for packets traveling between spheres
const SPHERE_PACKET_HEIGHT: f32 = 10.0;

// ============================================================================
// Wave Packet Source Movement Constants
// ============================================================================

/// Source movement speed (same as player walk speed)
const SOURCE_MOVE_SPEED: f32 = 6.0;
/// Minimum travel distance for sources
const SOURCE_TRAVEL_MIN: f32 = 20.0;
/// Maximum travel distance for sources
const SOURCE_TRAVEL_MAX: f32 = 30.0;
/// Surface level height
const SOURCE_HEIGHT_0: f32 = 0.0;
/// Final mineable height
const SOURCE_HEIGHT_1: f32 = 1.0;
/// Vertical rise speed (units/second)
const SOURCE_RISE_SPEED: f32 = 2.0;
/// Radius to check for existing sources near circuit
const CIRCUIT_CHECK_RADIUS: f32 = 30.0;
/// Direction variance ±π/16 radians (~11.25°)
const DIRECTION_VARIANCE: f32 = 0.196;

// Source state constants
const SOURCE_STATE_MOVING_H: u8 = 0;    // Traveling horizontally on surface
const SOURCE_STATE_ARRIVED_H0: u8 = 1;  // Arrived at destination, height 0
const SOURCE_STATE_RISING: u8 = 2;      // Rising from height 0 to height 1
const SOURCE_STATE_STATIONARY: u8 = 3;  // At final position, height 1, mineable

// ============================================================================
// Wave Packet Frequency Constants (6-color system)
// ============================================================================

/// Red frequency (0° on unit circle)
const FREQ_RED: f32 = 0.0;

/// Yellow frequency (60° = π/3 rad)
const FREQ_YELLOW: f32 = 1.047;

/// Green frequency (120° = 2π/3 rad)
const FREQ_GREEN: f32 = 2.094;

/// Cyan frequency (180° = π rad)
const FREQ_CYAN: f32 = 3.142;

/// Blue frequency (240° = 4π/3 rad)
const FREQ_BLUE: f32 = 4.189;

/// Magenta frequency (300° = 5π/3 rad)
const FREQ_MAGENTA: f32 = 5.236;

// ============================================================================
// Core Game Types
// ============================================================================

#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct WorldCoords {
    pub x: i32,
    pub y: i32,
    pub z: i32,
}

#[derive(SpacetimeType, Debug, Clone, Copy)]
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

    pub fn distance_to(&self, other: &DbVector3) -> f32 {
        let dx = self.x - other.x;
        let dy = self.y - other.y;
        let dz = self.z - other.z;
        (dx * dx + dy * dy + dz * dz).sqrt()
    }

    pub fn magnitude(&self) -> f32 {
        (self.x * self.x + self.y * self.y + self.z * self.z).sqrt()
    }

    pub fn normalize(&self) -> Self {
        let mag = self.magnitude();
        if mag > 0.0001 {
            DbVector3 {
                x: self.x / mag,
                y: self.y / mag,
                z: self.z / mag,
            }
        } else {
            DbVector3::zero()
        }
    }

    pub fn scale(&self, s: f32) -> Self {
        DbVector3 {
            x: self.x * s,
            y: self.y * s,
            z: self.z * s,
        }
    }

    pub fn add(&self, other: &DbVector3) -> Self {
        DbVector3 {
            x: self.x + other.x,
            y: self.y + other.y,
            z: self.z + other.z,
        }
    }

    pub fn sub(&self, other: &DbVector3) -> Self {
        DbVector3 {
            x: self.x - other.x,
            y: self.y - other.y,
            z: self.z - other.z,
        }
    }

    pub fn dot(&self, other: &DbVector3) -> f32 {
        self.x * other.x + self.y * other.y + self.z * other.z
    }

    pub fn cross(&self, other: &DbVector3) -> Self {
        DbVector3 {
            x: self.y * other.z - self.z * other.y,
            y: self.z * other.x - self.x * other.z,
            z: self.x * other.y - self.y * other.x,
        }
    }
}

#[derive(SpacetimeType, Debug, Clone, Copy)]
pub struct DbQuaternion {
    pub x: f32,
    pub y: f32,
    pub z: f32,
    pub w: f32,
}

impl Default for DbQuaternion {
    fn default() -> Self {
        DbQuaternion { x: 0.0, y: 0.0, z: 0.0, w: 1.0 }
    }
}

// ============================================================================
// Authentication Tables
// ============================================================================

#[spacetimedb::table(name = account)]
#[derive(Debug, Clone)]
pub struct Account {
    #[primary_key]
    #[auto_inc]
    pub account_id: u64,
    #[unique]
    pub username: String,        // For login only
    pub display_name: String,    // Shown in-game, permanent
    pub pin_hash: String,        // 4-digit PIN (hashed)
    pub created_at: u64,
    pub last_login: u64,
}

#[spacetimedb::table(name = player_session)]
#[derive(Debug, Clone)]
pub struct PlayerSession {
    #[primary_key]
    #[auto_inc]
    pub session_id: u64,
    pub account_id: u64,
    pub identity: Identity,
    pub session_token: String,
    pub device_info: String,
    pub created_at: u64,
    pub expires_at: u64,
    pub last_activity: u64,
    pub is_active: bool,
}

// Store session tokens for client retrieval
#[spacetimedb::table(name = session_result, public)]
#[derive(Debug, Clone)]
pub struct SessionResult {
    #[primary_key]
    pub identity: Identity,
    pub session_token: String,
    pub created_at: u64,
}

// ============================================================================
// Player Tables
// ============================================================================

#[spacetimedb::table(name = player, public)]
#[derive(Debug, Clone)]
pub struct Player {
    #[primary_key]
    #[auto_inc]
    pub player_id: u64,
    
    #[unique]
    pub identity: Identity,
    
    pub name: String,
    pub account_id: Option<u64>,  // Link to account
    pub current_world: WorldCoords,
    pub position: DbVector3,
    pub rotation: DbQuaternion,
    pub last_update: u64,
}

#[spacetimedb::table(name = logged_out_player)]
#[derive(Debug, Clone)]
pub struct LoggedOutPlayer {
    #[primary_key]
    pub identity: Identity,
    pub player_id: u64,
    pub name: String,
    pub account_id: Option<u64>,  // Keep account link
    pub logout_time: Timestamp,
    // Position persistence fields
    pub last_world: WorldCoords,
    pub last_position: DbVector3,
    pub last_rotation: DbQuaternion,
}

/// Player's energy packet inventory
/// Stores wave packet frequencies collected from mining
/// Player inventory using unified wave packet composition
/// Max capacity: 300 total packets
#[spacetimedb::table(name = player_inventory, public)]
#[derive(Debug, Clone)]
pub struct PlayerInventory {
    #[primary_key]
    pub player_id: u64,

    /// Unified composition - automatically consolidated when packets are added
    pub inventory_composition: Vec<WavePacketSample>,

    pub total_count: u32,    // Sum of all packet counts, max 300
    pub last_updated: Timestamp,
}

// ============================================================================
// World Tables
// ============================================================================

#[spacetimedb::table(name = world, public)]
#[derive(Debug, Clone)]
pub struct World {
    #[primary_key]
    #[auto_inc]
    pub world_id: u64,
    pub world_coords: WorldCoords,
    pub world_name: String,
    pub world_type: String,
    pub shell_level: u8,
}

#[spacetimedb::table(name = world_circuit, public)]
#[derive(Debug, Clone)]
pub struct WorldCircuit {
    #[primary_key]
    #[auto_inc]
    pub circuit_id: u64,
    pub world_coords: WorldCoords,
    pub cardinal_direction: String,  // Same as DistributionSphere (e.g., "North", "NorthEast", etc.)
    pub circuit_type: String,
    pub qubit_count: u8,
    pub sources_per_emission: u32,
    pub emission_interval_ms: u64,
    pub last_emission_time: u64,
}

/// Energy distribution spheres (26 per world, cardinal directions)
/// Route energy packets between players and storage devices
/// DEPRECATED: Use DistributionSphere + QuantumTunnel instead
#[spacetimedb::table(name = energy_spire, public)]
#[derive(Debug, Clone)]
pub struct EnergySpire {
    #[primary_key]
    #[auto_inc]
    pub spire_id: u64,
    pub world_coords: WorldCoords,
    pub sphere_position: DbVector3,  // Pre-calculated position on world surface
    pub direction: String,           // Cardinal direction (e.g., "North", "NorthEast", etc.)
    pub ring_charge: f32,            // Charge level, max 100.0
    pub last_charge_time: Timestamp,
}

/// Distribution spheres - mid-level routing nodes (26 per world, cardinal directions)
/// Route energy packets between players and storage devices
/// Required component - all 26 positions have spheres
#[spacetimedb::table(name = distribution_sphere, public)]
#[derive(Debug, Clone)]
pub struct DistributionSphere {
    #[primary_key]
    #[auto_inc]
    pub sphere_id: u64,
    pub world_coords: WorldCoords,
    pub cardinal_direction: String,   // Cardinal direction (e.g., "North", "NorthEast", etc.)
    pub sphere_position: DbVector3,   // Pre-calculated position on world surface
    pub sphere_radius: u8,            // Default 40 units
    pub packets_routed: u64,          // Lifetime stat
    pub last_packet_time: Timestamp,
    pub transit_buffer: Vec<WavePacketSample>,  // Aggregated packets waiting for next pulse (unlimited capacity)
}

/// Quantum tunnels - top-level ring assemblies (26 per world, cardinal directions)
/// Accumulate charge from packet routing, form inter-world connections
/// Required component - all 26 positions have rings
#[spacetimedb::table(name = quantum_tunnel, public)]
#[derive(Debug, Clone)]
pub struct QuantumTunnel {
    #[primary_key]
    #[auto_inc]
    pub tunnel_id: u64,
    pub world_coords: WorldCoords,
    pub cardinal_direction: String,      // Same as DistributionSphere
    pub ring_charge: f32,                // 0-100
    pub tunnel_status: String,           // "Inactive", "Charging", "Active"
    pub connected_to_world: Option<WorldCoords>,
    pub connected_to_sphere_id: Option<u64>,
    pub tunnel_color: String,            // Tier-based color (Red, Green, Blue, Yellow, Cyan, Magenta, Grey)
    pub formed_at: Option<Timestamp>,
}

/// Player-placed energy storage devices
/// Store wave packets for later use or trade
#[spacetimedb::table(name = storage_device, public)]
#[derive(Debug, Clone)]
pub struct StorageDevice {
    #[primary_key]
    #[auto_inc]
    pub device_id: u64,
    pub owner_player_id: u64,
    pub world_coords: WorldCoords,
    pub position: DbVector3,
    pub device_name: String,                    // Display name for UI
    pub capacity_per_frequency: u32,            // Max per frequency (default 1000, total 6000)
    pub stored_composition: Vec<WavePacketSample>,  // Current stored packets by frequency
    pub created_at: Timestamp,
}

/// Active energy packet transfers
/// Tracks packets moving from player inventory to storage via spires
#[spacetimedb::table(name = packet_transfer, public)]
#[derive(Debug, Clone)]
pub struct PacketTransfer {
    #[primary_key]
    #[auto_inc]
    pub transfer_id: u64,
    pub player_id: u64,  // Deprecated: use source_object_id when source_object_type="Player"
    pub composition: Vec<WavePacketSample>,  // Packet frequency mix
    pub packet_count: u32,                   // How many packets in transfer
    pub route_waypoints: Vec<DbVector3>,     // Path positions
    pub route_spire_ids: Vec<u64>,           // Which spires receive charge
    pub destination_device_id: u64,          // Deprecated: use destination_object_id
    pub initiated_at: Timestamp,
    pub completed: bool,
    pub current_leg: u32,                    // Which hop in route (0 = initial, 1+ = subsequent legs)
    pub leg_start_time: Timestamp,           // When current leg started
    pub state: String,                       // Deprecated: use current_leg_type
    // NEW: Object-oriented transfer fields
    pub source_object_type: String,          // "Player", "StorageDevice", "Miner", etc.
    pub source_object_id: u64,               // ID of source object
    pub destination_object_type: String,     // "Player", "StorageDevice", "Miner", etc.
    pub destination_object_id: u64,          // ID of destination object
    pub current_leg_type: String,            // "PendingAtObject", "ObjectToSphere", "SphereToSphere", "SphereToObject", "ArrivedAtSphere"
    pub predicted_arrival_time: Timestamp,   // When packet should arrive at current destination
}

// ============================================================================
// Wave System Types
// ============================================================================

#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq)]
pub struct WavePacketSignature {
    pub frequency: f32,
    pub amplitude: f32,
    pub phase: f32,
}

impl WavePacketSignature {
    pub fn new(frequency: f32, amplitude: f32, phase: f32) -> Self {
        WavePacketSignature { frequency, amplitude, phase }
    }
    
    pub fn to_color_string(&self) -> String {
        // Map frequency to color name
        let color = if self.frequency < 0.5 {
            "Red"
        } else if self.frequency < 1.5 {
            "Yellow"
        } else if self.frequency < 2.5 {
            "Green"
        } else if self.frequency < 3.5 {
            "Cyan"
        } else if self.frequency < 4.5 {
            "Blue"
        } else {
            "Magenta"
        };
        format!("{}", color)
    }
}

#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq)]
pub struct WavePacketSample {
    pub frequency: f32,
    pub amplitude: f32,
    pub phase: f32,
    pub count: u32,
}


#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq, Eq)]
pub enum CrystalType {
    Red,
    Green,
    Blue,
}

// ============================================================================
// Wave Packet Tables
// ============================================================================

#[spacetimedb::table(name = wave_packet_source, public)]
#[derive(Debug, Clone)]
pub struct WavePacketSource {
    #[primary_key]
    #[auto_inc]
    pub source_id: u64,
    pub world_coords: WorldCoords,
    pub position: DbVector3,
    pub velocity: DbVector3,
    pub destination: DbVector3,  // Target position (for client interpolation)
    pub state: u8,  // 0=moving_h, 1=arrived_h0, 2=rising, 3=stationary
    pub wave_packet_composition: Vec<WavePacketSample>, // Multiple frequencies in one orb
    pub total_wave_packets: u32,
    pub creation_time: u64,
    pub lifetime_ms: u32,
    pub last_dissipation: u64,
    // Concurrent mining support
    pub active_miner_count: u32,  // Track how many miners
    pub last_depletion: u64,      // When packets were last removed
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
    #[auto_inc]
    pub extraction_id: u64,
    pub player_id: u64,
    pub source_type: String,     // "orb", "circuit", "device"
    pub source_id: u64,           // ID of the source (source_id, etc)
    pub packet_id: u64,           // Unique packet identifier
    pub composition: Vec<WavePacketSample>, // Multi-frequency composition
    pub total_count: u32,         // Total packets in this bundle
    pub departure_time: u64,
    pub expected_arrival: u64,
}

// ============================================================================
// Mining System State
// ============================================================================

// Structure for extraction requests (request-driven system)
#[derive(SpacetimeType, Debug, Clone)]
pub struct ExtractionRequest {
    pub frequency: f32,
    pub count: u32,
}


// NEW: Mining session table for concurrent mining support
#[spacetimedb::table(name = mining_session, public)]
#[derive(Debug, Clone)]
pub struct MiningSession {
    #[primary_key]
    #[auto_inc]
    pub session_id: u64,
    pub player_identity: Identity,
    pub source_id: u64,
    pub crystal_composition: Vec<WavePacketSample>,  // Unified wave system - crystals as frequency filters
    pub circuit_id: u64,
    pub started_at: u64,
    pub last_extraction: u64,
    pub extraction_multiplier: f32,  // Default 1.0, for future use
    pub total_extracted: u32,
    pub is_active: bool,
}

// ============================================================================
// Helper Functions
// ============================================================================

fn hash_password(password: &str) -> String {
    format!("hashed_{}", password)
}

fn hash_pin(pin: &str) -> String {
    // In production, use proper hashing like bcrypt
    format!("hashed_{}", pin)
}

fn generate_session_token(account_id: u64, identity: &Identity, timestamp: u64) -> String {
    // Simple token generation - in production use proper crypto
    let timestamp_secs = timestamp / 1000;
    
    format!("session_{}_{}_{}_{}", 
        account_id, 
        identity, 
        timestamp_secs,
        timestamp_secs % 10000
    )
}

#[spacetimedb::reducer]
pub fn register_account(
    ctx: &ReducerContext,
    username: String,
    display_name: String,
    pin: String,
) -> Result<(), String> {
    log::info!("=== REGISTER_ACCOUNT START ===");
    log::info!("Username: {}, Display Name: {}, Identity: {:?}", 
        username, display_name, ctx.sender);
    
    // Validate username
    if username.is_empty() || username.len() < 3 || username.len() > 20 {
        log::warn!("Registration failed: Invalid username length for '{}'", username);
        return Err("Username must be 3-20 characters".to_string());
    }
    
    // Validate display name
    if display_name.is_empty() || display_name.len() < 3 || display_name.len() > 20 {
        log::warn!("Registration failed: Invalid display name length for '{}'", display_name);
        return Err("Display name must be 3-20 characters".to_string());
    }
    
    // Validate PIN (4 digits)
    if pin.len() != 4 || !pin.chars().all(|c| c.is_numeric()) {
        log::warn!("Registration failed: Invalid PIN format for user '{}'", username);
        return Err("PIN must be exactly 4 digits".to_string());
    }
    
    // Check if username already exists
    if ctx.db.account().username().find(&username).is_some() {
        log::warn!("Registration failed: Username '{}' already taken", username);
        return Err("Username already taken".to_string());
    }
    
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    let account = Account {
        account_id: 0, // auto-generated
        username: username.clone(),
        display_name: display_name.clone(),
        pin_hash: hash_pin(&pin),
        created_at: current_time,
        last_login: current_time,
    };
    
    ctx.db.account().insert(account.clone());
    
    log::info!("Account created successfully - Username: {}, Account ID will be auto-generated", username);
    log::info!("=== REGISTER_ACCOUNT END ===");
    Ok(())
}

#[spacetimedb::reducer]
pub fn login(
    ctx: &ReducerContext,
    username: String,
    pin: String,
    device_info: String,
) -> Result<(), String> {
    log::info!("=== LOGIN START ===");
    log::info!("Username: {}, Device: {}, Identity: {:?}", 
        username, device_info, ctx.sender);
    
    // Validate inputs
    if username.is_empty() || pin.is_empty() {
        log::warn!("Login failed: Empty username or PIN");
        return Err("Username and PIN required".to_string());
    }
    
    // Find account
    let account = match ctx.db.account().username().find(&username) {
        Some(acc) => {
            log::info!("Found account - ID: {}, Username: {}", acc.account_id, acc.username);
            acc
        },
        None => {
            log::warn!("Login failed: Account not found for username '{}'", username);
            return Err("Account not found".to_string());
        }
    };
    
    // Verify PIN
    let provided_hash = hash_pin(&pin);
    if account.pin_hash != provided_hash {
        log::warn!("Login failed: Invalid PIN for user '{}' (expected: {}, got: {})", 
            username, account.pin_hash, provided_hash);
        return Err("Invalid PIN".to_string());
    }
    
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    // Check for existing active session
    let existing_sessions: Vec<PlayerSession> = ctx.db.player_session()
        .iter()
        .filter(|s| s.identity == ctx.sender && s.is_active)
        .collect();
    
    log::info!("Found {} existing active sessions for this identity", existing_sessions.len());
    
    // Mark old sessions as inactive
    for session in existing_sessions {
        log::info!("Deactivating old session ID: {}", session.session_id);
        let mut updated_session = session.clone();
        updated_session.is_active = false;
        ctx.db.player_session().delete(session);
        ctx.db.player_session().insert(updated_session);
    }
    
    // Create new session
    let session_token = generate_session_token(account.account_id, &ctx.sender, current_time);
    log::info!("Generated new session token: {}", session_token);
    
    let session = PlayerSession {
        session_id: 0, // auto-generated
        account_id: account.account_id,
        identity: ctx.sender,
        session_token: session_token.clone(),
        device_info,
        created_at: current_time,
        expires_at: current_time + (24 * 60 * 60 * 1000), // 24 hours
        last_activity: current_time,
        is_active: true,
    };
    
    ctx.db.player_session().insert(session.clone());
    log::info!("Created new session for account ID: {}", account.account_id);
    
    // Store session result for client retrieval
    // Clean up any existing session result first
    if let Some(existing) = ctx.db.session_result().identity().find(&ctx.sender) {
        log::info!("Removing existing session result for identity");
        ctx.db.session_result().delete(existing);
    }
    
    ctx.db.session_result().insert(SessionResult {
        identity: ctx.sender,
        session_token: session_token.clone(),
        created_at: current_time,
    });
    log::info!("Created SessionResult for identity: {:?}", ctx.sender);
    
    // Update account last login
    let account_id = account.account_id;
    let mut updated_account = account.clone();
    updated_account.last_login = current_time;
    ctx.db.account().delete(account);
    ctx.db.account().insert(updated_account);
    
    log::info!("Login successful for user '{}' (Account ID: {})", username, account_id);
    log::info!("=== LOGIN END ===");
    Ok(())
}

#[spacetimedb::reducer]
pub fn login_with_session(
    ctx: &ReducerContext,
    username: String,
    pin: String,
    device_info: String,
) -> Result<(), String> {
    log::info!("=== LOGIN_WITH_SESSION START ===");
    log::info!("Delegating to login() reducer");
    
    // Same as login but with session support
    let result = login(ctx, username, pin, device_info);
    
    log::info!("=== LOGIN_WITH_SESSION END ===");
    result
}

#[spacetimedb::reducer]
pub fn restore_session(
    ctx: &ReducerContext,
    session_token: String,
) -> Result<(), String> {
    log::info!("=== RESTORE_SESSION START ===");
    log::info!("Session token: {}, Identity: {:?}", session_token, ctx.sender);
    
    // Find the session
    let session = ctx.db.player_session()
        .iter()
        .find(|s| s.session_token == session_token && s.is_active)
        .ok_or("Invalid or expired session")?;
    
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    // Check if session is expired
    if current_time > session.expires_at {
        log::warn!("Session expired - Current time: {}, Expires at: {}", 
            current_time, session.expires_at);
        
        // Mark session as inactive
        let mut expired_session = session.clone();
        expired_session.is_active = false;
        ctx.db.player_session().delete(session.clone());
        ctx.db.player_session().insert(expired_session);
        
        return Err("Session expired".to_string());
    }
    
    // Save account_id before moving session
    let account_id = session.account_id;
    let old_identity = session.identity;
    
    // Check if identity matches (important for security)
    if old_identity != ctx.sender {
        log::warn!("Session identity mismatch - Session: {:?}, Sender: {:?}", 
            old_identity, ctx.sender);
        return Err("Session identity mismatch".to_string());
    }
    
    // Update session activity and extend expiration
    let mut updated_session = session.clone();
    updated_session.last_activity = current_time;
    // Extend expiration by another 24 hours on successful restore
    updated_session.expires_at = current_time + (24 * 60 * 60 * 1000);
    ctx.db.player_session().delete(session);
    ctx.db.player_session().insert(updated_session);
    
    // Get account info using saved account_id
    let account = ctx.db.account()
        .account_id()
        .find(&account_id)
        .ok_or("Account not found")?;
    
    log::info!("Found account - ID: {}, Username: {}", account.account_id, account.username);
    
    // Clean up any existing session results for this identity
    if let Some(existing) = ctx.db.session_result().identity().find(&ctx.sender) {
        ctx.db.session_result().delete(existing);
    }
    
    // Create a new session result for the client
    ctx.db.session_result().insert(SessionResult {
        identity: ctx.sender,
        session_token: session_token.clone(),
        created_at: current_time,
    });
    
    log::info!("Session restored for {} (account: {})", account.username, account_id);
    log::info!("=== RESTORE_SESSION END ===");
    Ok(())
}

#[spacetimedb::reducer]
pub fn logout(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("=== LOGOUT START ===");
    log::info!("Identity: {:?}", ctx.sender);
    
    let sessions: Vec<PlayerSession> = ctx.db.player_session()
        .iter()
        .filter(|s| s.identity == ctx.sender && s.is_active)
        .collect();
    
    log::info!("Found {} active sessions to logout", sessions.len());
    
    for session in sessions {
        let mut updated_session = session.clone();
        updated_session.is_active = false;
        ctx.db.player_session().delete(session.clone());
        ctx.db.player_session().insert(updated_session);
    }
    
    log::info!("Logout completed for identity {:?}", ctx.sender);
    log::info!("=== LOGOUT END ===");
    Ok(())
}

// Add a cleanup reducer to periodically clean expired sessions
#[spacetimedb::reducer]
pub fn cleanup_expired_sessions(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("=== CLEANUP_EXPIRED_SESSIONS START ===");
    
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    let expired_sessions: Vec<PlayerSession> = ctx.db.player_session()
        .iter()
        .filter(|s| s.is_active && s.expires_at < current_time)
        .collect();
    
    let count = expired_sessions.len();
    log::info!("Found {} expired sessions to clean up", count);
    
    for session in expired_sessions {
        let mut updated_session = session.clone();
        updated_session.is_active = false;
        ctx.db.player_session().delete(session);
        ctx.db.player_session().insert(updated_session);
    }
    
    if count > 0 {
        log::info!("Cleaned up {} expired sessions", count);
    }
    
    log::info!("=== CLEANUP_EXPIRED_SESSIONS END ===");
    Ok(())
}

// ============================================================================
// Spawn Position Helpers
// ============================================================================

/// Calculate a proper spawn position on the sphere surface for a given world
/// Returns a position at the north pole of the sphere with proper offset
fn calculate_spawn_position(world_coords: &WorldCoords) -> DbVector3 {
    // For now, all worlds spawn at their north pole
    // The world center is at the world coordinates
    let world_center_x = world_coords.x as f32 * 10000.0; // Worlds are spaced 10000 units apart
    let world_center_y = world_coords.y as f32 * 10000.0;
    let world_center_z = world_coords.z as f32 * 10000.0;
    
    // For center world (0,0,0), spawn at north pole
    if world_coords.x == 0 && world_coords.y == 0 && world_coords.z == 0 {
        // North pole is at positive Y direction
        // Position = center + (up vector * (radius + offset))
        let spawn_x = 0.0;
        let spawn_y = WORLD_RADIUS + SURFACE_OFFSET;
        let spawn_z = 0.0;
        
        log::info!("Calculated spawn position for center world: ({:.2}, {:.2}, {:.2})", 
            spawn_x, spawn_y, spawn_z);
        
        return DbVector3::new(spawn_x, spawn_y, spawn_z);
    }
    
    // For other worlds, calculate relative north pole
    let spawn_x = world_center_x;
    let spawn_y = world_center_y + WORLD_RADIUS + SURFACE_OFFSET;
    let spawn_z = world_center_z;
    
    log::info!("Calculated spawn position for world ({},{},{}): ({:.2}, {:.2}, {:.2})", 
        world_coords.x, world_coords.y, world_coords.z,
        spawn_x, spawn_y, spawn_z);
    
    DbVector3::new(spawn_x, spawn_y, spawn_z)
}

// ============================================================================
// Player Reducers
// ============================================================================

#[spacetimedb::reducer]
pub fn create_player(ctx: &ReducerContext, name: String) -> Result<(), String> {
    log::info!("=== CREATE_PLAYER START ===");
    log::info!("Name: '{}', Identity: {:?}", name, ctx.sender);
    
    if name.is_empty() || name.len() > 20 {
        log::warn!("Create player failed: Invalid name length '{}'", name);
        return Err("Player name must be 1-20 characters".to_string());
    }
    
    // Initialize worlds first if needed
    if ctx.db.world().iter().find(|w| w.world_coords == WorldCoords { x: 0, y: 0, z: 0 }).is_none() {
        log::info!("No center world found, initializing worlds...");
        init_worlds(ctx)?;
    }
    
    // Check if player already exists for this identity
    if let Some(existing) = ctx.db.player().identity().find(&ctx.sender) {
        log::warn!("Create player failed: Player already exists - Name: {}, ID: {}", 
            existing.name, existing.player_id);
        return Err(format!("You already have a player named {}", existing.name));
    }
    
    // Check if logged out player exists for this identity
    if let Some(logged_out) = ctx.db.logged_out_player().identity().find(&ctx.sender) {
        log::info!("Found logged out player - Name: {}, ID: {}, restoring...", 
            logged_out.name, logged_out.player_id);
        
        // Restore the player with saved position
        log::info!("Restoring saved position: World({},{},{}), Pos({:.2},{:.2},{:.2})", 
            logged_out.last_world.x, logged_out.last_world.y, logged_out.last_world.z,
            logged_out.last_position.x, logged_out.last_position.y, logged_out.last_position.z);
        
        let player = Player {
            player_id: logged_out.player_id,
            identity: logged_out.identity,
            name: logged_out.name.clone(),
            account_id: logged_out.account_id,
            current_world: logged_out.last_world.clone(),  // Restore saved world
            position: logged_out.last_position.clone(),     // Restore saved position
            rotation: logged_out.last_rotation.clone(),     // Restore saved rotation
            last_update: ctx.timestamp
                .duration_since(Timestamp::UNIX_EPOCH)
                .expect("Valid timestamp")
                .as_millis() as u64,
        };
        
        ctx.db.player().insert(player.clone());
        ctx.db.logged_out_player().delete(logged_out);
        
        log::info!("Restored player '{}' (ID: {}) with saved position", player.name, player.player_id);
        return Ok(());
    }
    
    // Get account info if we have a session
    let account_id = ctx.db.player_session()
        .iter()
        .find(|s| s.identity == ctx.sender && s.is_active)
        .and_then(|session| {
            log::info!("Found active session for account ID: {}", session.account_id);
            Some(session.account_id)
        });
    
    // NEW: Check if account already has a player
    if let Some(acc_id) = account_id {
        // Check active players
        if let Some(existing_player) = ctx.db.player()
            .iter()
            .find(|p| p.account_id == Some(acc_id)) {
            
            log::info!("Account {} has active player '{}', updating identity", acc_id, existing_player.name);
            
            // Update the player's identity to the new one
            let mut updated_player = existing_player.clone();
            updated_player.identity = ctx.sender;
            updated_player.last_update = ctx.timestamp
                .duration_since(Timestamp::UNIX_EPOCH)
                .expect("Valid timestamp")
                .as_millis() as u64;
            
            ctx.db.player().delete(existing_player);
            ctx.db.player().insert(updated_player.clone());
            
            log::info!("Updated player '{}' to new identity {:?}", updated_player.name, ctx.sender);
            return Ok(());
        }
        
        // Check logged out players
        if let Some(logged_out) = ctx.db.logged_out_player()
            .iter()
            .find(|p| p.account_id == Some(acc_id)) {
            
            log::info!("Account {} has logged out player '{}', restoring with new identity", 
                acc_id, logged_out.name);
            
            // Restore with new identity and saved position
            log::info!("Restoring saved position: World({},{},{}), Pos({:.2},{:.2},{:.2})", 
                logged_out.last_world.x, logged_out.last_world.y, logged_out.last_world.z,
                logged_out.last_position.x, logged_out.last_position.y, logged_out.last_position.z);
            
            let player = Player {
                player_id: logged_out.player_id,
                identity: ctx.sender,  // Use new identity
                name: logged_out.name.clone(),
                account_id: logged_out.account_id,
                current_world: logged_out.last_world.clone(),  // Restore saved world
                position: logged_out.last_position.clone(),     // Restore saved position
                rotation: logged_out.last_rotation.clone(),     // Restore saved rotation
                last_update: ctx.timestamp
                    .duration_since(Timestamp::UNIX_EPOCH)
                    .expect("Valid timestamp")
                    .as_millis() as u64,
            };
            
            ctx.db.player().insert(player.clone());
            ctx.db.logged_out_player().delete(logged_out.clone());
            
            log::info!("Restored player '{}' (ID: {}) with new identity", player.name, player.player_id);
            return Ok(());
        }
    }
    
    // If no active session, warn but allow creation
    if account_id.is_none() {
        log::warn!("No active session found for identity {:?}", ctx.sender);
    }
    
    // Create new player at center world with proper spawn position
    let center_world = WorldCoords { x: 0, y: 0, z: 0 };
    let spawn_position = calculate_spawn_position(&center_world);
    
    log::info!("Creating new player '{}' at spawn position ({:.2}, {:.2}, {:.2})", 
        name, spawn_position.x, spawn_position.y, spawn_position.z);
    
    let player = Player {
        player_id: 0, // auto-generated
        identity: ctx.sender,
        name: name.clone(),
        account_id,
        current_world: center_world,
        position: spawn_position,
        rotation: DbQuaternion::default(),
        last_update: ctx.timestamp
            .duration_since(Timestamp::UNIX_EPOCH)
            .expect("Valid timestamp")
            .as_millis() as u64,
    };
    
    ctx.db.player().insert(player.clone());
    log::info!("Created new player '{}' (ID will be auto-generated) at north pole of center world", name);
    
    log::info!("Player creation successful for '{}'", name);
    log::info!("=== CREATE_PLAYER END ===");
    Ok(())
}
#[spacetimedb::reducer]
pub fn update_player_position(
    ctx: &ReducerContext,
    position: DbVector3,
    rotation: DbQuaternion,
) -> Result<(), String> {
    // Find player
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    // Log position update every 100th time to avoid spam (based on update count)
    // In production, this should be disabled or use trace level logging
    let update_count = player.last_update % 100;
    if update_count == 0 {
        log::info!("Position update for '{}': Pos({:.2},{:.2},{:.2})", 
            player.name, position.x, position.y, position.z);
    }
    
    // Update player position
    let mut updated_player = player.clone();
    updated_player.position = position;
    updated_player.rotation = rotation;
    updated_player.last_update = current_time;
    
    ctx.db.player().delete(player);
    ctx.db.player().insert(updated_player);
    
    Ok(())
}

#[spacetimedb::reducer]
pub fn travel_to_world(
    ctx: &ReducerContext,
    world_coords: WorldCoords,
) -> Result<(), String> {
    log::info!("=== TRAVEL_TO_WORLD START ===");
    log::info!("Target world: ({},{},{}), Identity: {:?}", 
        world_coords.x, world_coords.y, world_coords.z, ctx.sender);
    
    // Find player
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    let player_name = player.name.clone();
    
    log::info!("Player '{}' (ID: {}) requesting travel from ({},{},{}) to ({},{},{})",
        player_name, player.player_id,
        player.current_world.x, player.current_world.y, player.current_world.z,
        world_coords.x, world_coords.y, world_coords.z);
    
    // Check if target world exists
    let world_exists = ctx.db.world()
        .iter()
        .any(|w| w.world_coords == world_coords);
    
    if !world_exists {
        log::warn!("Travel failed: Target world ({},{},{}) does not exist", 
            world_coords.x, world_coords.y, world_coords.z);
        return Err("Target world does not exist".to_string());
    }
    
    // Calculate spawn position for the target world
    let spawn_position = calculate_spawn_position(&world_coords);
    
    log::info!("Setting spawn position for world ({},{},{}): ({:.2}, {:.2}, {:.2})",
        world_coords.x, world_coords.y, world_coords.z,
        spawn_position.x, spawn_position.y, spawn_position.z);
    
    // Update player world and position
    let mut updated_player = player.clone();
    updated_player.current_world = world_coords;
    updated_player.position = spawn_position; // Use calculated spawn position
    updated_player.last_update = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    ctx.db.player().delete(player);
    ctx.db.player().insert(updated_player);
    
    log::info!("Player '{}' successfully traveled to world ({},{},{}) at position ({:.2}, {:.2}, {:.2})",
        player_name, world_coords.x, world_coords.y, world_coords.z,
        spawn_position.x, spawn_position.y, spawn_position.z);
    log::info!("=== TRAVEL_TO_WORLD END ===");
    
    Ok(())
}

// ============================================================================
// Crystal Selection
// ============================================================================

#[spacetimedb::reducer]
pub fn choose_crystal(
    ctx: &ReducerContext,
    crystal_type: CrystalType,
) -> Result<(), String> {
    log::info!("=== CHOOSE_CRYSTAL START ===");
    log::info!("Crystal type: {:?}, Identity: {:?}", crystal_type, ctx.sender);
    
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    log::info!("Player '{}' (ID: {}) choosing crystal", player.name, player.player_id);
    
    // Check if player already has a crystal
    if ctx.db.player_crystal().player_id().find(&player.player_id).is_some() {
        log::warn!("Choose crystal failed: Player already has a crystal");
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
    
    log::info!("Player '{}' successfully chose {:?} crystal", player.name, crystal_type);
    log::info!("=== CHOOSE_CRYSTAL END ===");
    
    Ok(())
}

// ============================================================================
// World Initialization
// ============================================================================

fn init_worlds(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("=== INIT_WORLDS START ===");
    
    // Create center world
    let center_world = World {
        world_id: 0,
        world_coords: WorldCoords { x: 0, y: 0, z: 0 },
        world_name: "Genesis".to_string(),
        world_type: "Core".to_string(),
        shell_level: 0,
    };
    ctx.db.world().insert(center_world);
    log::info!("Created center world: Genesis at (0,0,0)");
    
    // Create shell 1 worlds (6 face centers of cube)
    let shell1_coords = vec![
        (WorldCoords { x: 1, y: 0, z: 0 }, "East Prime"),
        (WorldCoords { x: -1, y: 0, z: 0 }, "West Prime"),
        (WorldCoords { x: 0, y: 1, z: 0 }, "North Prime"),
        (WorldCoords { x: 0, y: -1, z: 0 }, "South Prime"),
        (WorldCoords { x: 0, y: 0, z: 1 }, "Upper Prime"),
        (WorldCoords { x: 0, y: 0, z: -1 }, "Lower Prime"),
    ];
    
    for (coords, name) in shell1_coords {
        let world = World {
            world_id: 0,
            world_coords: coords,
            world_name: name.to_string(),
            world_type: "Prime".to_string(),
            shell_level: 1,
        };
        ctx.db.world().insert(world);
        log::info!("Created shell 1 world: {} at ({},{},{})", 
            name, coords.x, coords.y, coords.z);
    }
    
    log::info!("=== INIT_WORLDS END ===");
    Ok(())
}

// ============================================================================
// Disconnect Handler
// ============================================================================

/// Called automatically when a client disconnects from SpacetimeDB
#[spacetimedb::reducer(client_disconnected)]
pub fn __identity_disconnected__(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("=== CLIENT DISCONNECTED (AUTO) ===");
    log::info!("Identity: {:?}", ctx.sender);
    
    // Move player to logged out table
    if let Some(player) = ctx.db.player().identity().find(&ctx.sender) {
        log::info!("Moving player '{}' (ID: {}) to logged_out_player table", 
            player.name, player.player_id);
        
        // Get account_id through PlayerSession
        let account_id = ctx.db.player_session()
            .iter()
            .find(|s| s.identity == player.identity)
            .map(|s| s.account_id);
        
        // Save position data for restoration
        log::info!("Saving position for player '{}': World({},{},{}), Pos({:.2},{:.2},{:.2})", 
            player.name,
            player.current_world.x, player.current_world.y, player.current_world.z,
            player.position.x, player.position.y, player.position.z);
        
        let logged_out = LoggedOutPlayer {
            identity: player.identity,
            player_id: player.player_id,
            name: player.name.clone(),
            account_id,
            logout_time: ctx.timestamp,
            last_world: player.current_world.clone(),
            last_position: player.position.clone(),
            last_rotation: player.rotation.clone(),
        };
        
        ctx.db.logged_out_player().insert(logged_out);
        ctx.db.player().delete(player);
        
        log::info!("Player moved to logged out state with saved position");
    } else {
        log::info!("No active player found for disconnecting identity");
    }
    

    // Clean up NEW mining sessions (MiningSession table)
    let mining_sessions: Vec<_> = ctx.db.mining_session()
        .iter()
        .filter(|s| s.player_identity == ctx.sender && s.is_active)
        .collect();

    log::info!("Found {} active mining sessions to clean up", mining_sessions.len());

    for mining_session in mining_sessions {
        // Decrement orb's active miner count
        if let Some(source) = ctx.db.wave_packet_source().source_id().find(&mining_session.source_id) {
            let mut updated_source = source.clone();
            updated_source.active_miner_count = updated_source.active_miner_count.saturating_sub(1);
            ctx.db.wave_packet_source().delete(source);
            ctx.db.wave_packet_source().insert(updated_source);
            log::info!("Decremented active miner count for source {}", mining_session.source_id);
        }

        // Mark mining session as inactive
        let mut updated_session = mining_session.clone();
        updated_session.is_active = false;
        ctx.db.mining_session().delete(mining_session);
        ctx.db.mining_session().insert(updated_session);
    }
    
    log::info!("Disconnect handling completed");
    log::info!("=== CLIENT DISCONNECTED END ===");
    Ok(())
}

#[spacetimedb::reducer]
pub fn disconnect(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("=== DISCONNECT START ===");
    log::info!("Identity: {:?}", ctx.sender);
    
    // Move player to logged out table
    if let Some(player) = ctx.db.player().identity().find(&ctx.sender) {
        log::info!("Moving player '{}' (ID: {}) to logged_out_player table", 
            player.name, player.player_id);
        
        // Save position data for restoration
        log::info!("Saving position in disconnect: World({},{},{}), Pos({:.2},{:.2},{:.2})", 
            player.current_world.x, player.current_world.y, player.current_world.z,
            player.position.x, player.position.y, player.position.z);
        
        let logged_out = LoggedOutPlayer {
            identity: player.identity,
            player_id: player.player_id,
            name: player.name.clone(),
            account_id: player.account_id,
            logout_time: ctx.timestamp,
            last_world: player.current_world.clone(),
            last_position: player.position.clone(),
            last_rotation: player.rotation.clone(),
        };
        
        ctx.db.logged_out_player().insert(logged_out);
        ctx.db.player().delete(player);
        
        log::info!("Player moved to logged out state");
    } else {
        log::info!("No active player found for disconnecting identity");
    }
    
    // Mark sessions as inactive
    let sessions: Vec<PlayerSession> = ctx.db.player_session()
        .iter()
        .filter(|s| s.identity == ctx.sender && s.is_active)
        .collect();
    
    log::info!("Found {} active sessions to mark as inactive", sessions.len());
    
    for session in sessions {
        let mut updated_session = session.clone();
        updated_session.is_active = false;
        ctx.db.player_session().delete(session);
        ctx.db.player_session().insert(updated_session);
    }
    
    log::info!("Disconnect handling completed");
    log::info!("=== DISCONNECT END ===");
    Ok(())
}

// ============================================================================
// Mining Reducers
// ============================================================================

// ============================================================================
// Game Loop & Tick Processing
// ============================================================================

#[spacetimedb::reducer]
pub fn tick(ctx: &ReducerContext) -> Result<(), String> {
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    // Get last cleanup time from state or default to 0
    static LAST_CLEANUP: OnceLock<Mutex<u64>> = OnceLock::new();
    let last_cleanup_mutex = LAST_CLEANUP.get_or_init(|| Mutex::new(0));
    let mut last_cleanup = last_cleanup_mutex.lock().unwrap();
    
    // Clean up sessions every hour
    if current_time > *last_cleanup + (60 * 60 * 1000) {
        cleanup_expired_sessions(ctx)?;
        *last_cleanup = current_time;
    }
    
    // Process circuits and emit wave packet orbs
    for circuit in ctx.db.world_circuit().iter() {
        if current_time >= circuit.last_emission_time + circuit.emission_interval_ms {
            process_circuit_emission(ctx, &circuit)?;
            
            // Update circuit emission time
            let mut updated_circuit = circuit.clone();
            updated_circuit.last_emission_time = current_time;
            ctx.db.world_circuit().delete(circuit);
            ctx.db.world_circuit().insert(updated_circuit);
        }
    }
    
    // Process orb dissipation
    process_orb_dissipation(ctx)?;
    
    // Clean up expired wave packet orbs
    cleanup_expired_wave_packet_sources(ctx)?;
    
    // Clean up old extraction notifications
    cleanup_old_extractions(ctx)?;
    
    
    Ok(())
}

fn process_circuit_emission(ctx: &ReducerContext, circuit: &WorldCircuit) -> Result<(), String> {
    use rand::{Rng, SeedableRng};
    use rand::rngs::StdRng;

    // Get circuit position on sphere surface based on cardinal direction
    let circuit_position = get_cardinal_position(&circuit.cardinal_direction);

    // Count existing sources within CIRCUIT_CHECK_RADIUS of this circuit
    let existing_count = ctx.db.wave_packet_source().iter()
        .filter(|s| {
            s.world_coords == circuit.world_coords &&
            s.position.distance_to(&circuit_position) < CIRCUIT_CHECK_RADIUS
        })
        .count() as u32;

    // Calculate how many sources we need to spawn
    let needed = circuit.sources_per_emission.saturating_sub(existing_count);

    if needed == 0 {
        return Ok(());  // Already have enough sources nearby
    }

    // Create deterministic RNG for this emission
    let seed = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_nanos() as u64 + circuit.circuit_id;
    let mut rng = StdRng::seed_from_u64(seed);

    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;

    // Get circuit's surface normal
    let surface_normal = circuit_position.normalize();

    // Get primary color from circuit direction
    let primary_freq = get_direction_frequency(&circuit.cardinal_direction);

    for i in 0..needed {
        // Pick one of 8 tangent directions (45° apart)
        let direction_index = rng.gen_range(0..8);
        let base_direction = get_tangent_direction(&surface_normal, direction_index);

        // Apply ±π/16 variance (±11.25°)
        let variance = rng.gen_range(-DIRECTION_VARIANCE..DIRECTION_VARIANCE);
        let travel_direction = rotate_around_normal(&base_direction, &surface_normal, variance);

        // Calculate travel distance (20-30 units)
        let travel_distance = rng.gen_range(SOURCE_TRAVEL_MIN..SOURCE_TRAVEL_MAX);

        // Calculate destination position on sphere surface at height 0
        let spawn_position = surface_normal.scale(WORLD_RADIUS + SOURCE_HEIGHT_0);
        let destination = travel_on_sphere_surface(&spawn_position, &travel_direction, travel_distance);

        // Get secondary color from destination direction
        let dest_direction = closest_cardinal_direction(&destination);
        let secondary_freq = get_direction_frequency(&dest_direction);

        // Create 80/20 composition
        let total_packets = rng.gen_range(80..120);  // 80-120 packets per source
        let composition = create_mixed_composition(primary_freq, secondary_freq, total_packets);

        // Calculate velocity (tangent direction * speed)
        let velocity = travel_direction.scale(SOURCE_MOVE_SPEED);

        // Create the source with movement state
        let source = WavePacketSource {
            source_id: 0,  // auto-inc
            world_coords: circuit.world_coords,
            position: spawn_position,
            velocity,
            destination,
            state: SOURCE_STATE_MOVING_H,  // Start moving horizontally
            wave_packet_composition: composition.clone(),
            total_wave_packets: composition.iter().map(|s| s.count).sum(),
            creation_time: current_time,
            lifetime_ms: 600_000,  // 10 minutes
            last_dissipation: current_time,
            active_miner_count: 0,
            last_depletion: current_time,
        };

        ctx.db.wave_packet_source().insert(source);

        log::info!("[Emission] Circuit {} ({}) spawned moving source {} toward {} (dist={:.1})",
            circuit.circuit_id, circuit.cardinal_direction, i + 1, dest_direction, travel_distance);
    }

    log::info!("[Emission] Circuit {} ({}) emitted {} sources (had {} existing within {}u)",
        circuit.circuit_id, circuit.cardinal_direction, needed, existing_count, CIRCUIT_CHECK_RADIUS);

    Ok(())
}

#[spacetimedb::reducer]
pub fn emit_wave_packet_source(
    ctx: &ReducerContext,
    world_coords: WorldCoords,
    source_position: DbVector3,
) -> Result<(), String> {
    use rand::{Rng, SeedableRng};
    use rand::rngs::StdRng;
    
    // Create a deterministic RNG based on timestamp
    let seed = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_nanos() as u64;
    let mut rng = StdRng::seed_from_u64(seed);
    
    // Random velocity
    let velocity = DbVector3::new(
        rng.gen_range(-5.0..5.0),
        rng.gen_range(0.0..10.0),
        rng.gen_range(-5.0..5.0),
    );
    
    // Create 1-3 different wave packet samples in the orb
    let num_samples = rng.gen_range(1..=3);
    let mut composition = Vec::new();
    let mut total_packets = 0u32;
    
    for _ in 0..num_samples {
        let frequency = rng.gen::<f32>();
        let amplitude = rng.gen_range(0.5..1.0);
        let phase = rng.gen::<f32>() * 2.0 * PI;
        let count = rng.gen_range(5..20);
        
        composition.push(WavePacketSample {
            frequency,
            amplitude,
            phase,
            count,
        });
        
        total_packets += count;
    }
    
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;

    let source = WavePacketSource {
        source_id: 0, // auto-generated
        world_coords,
        position: source_position,
        velocity,
        destination: source_position,  // For stationary sources, destination = position
        state: SOURCE_STATE_STATIONARY, // Legacy sources start stationary
        wave_packet_composition: composition,
        total_wave_packets: total_packets,
        creation_time: current_time,
        lifetime_ms: 300000, // 5 minutes
        last_dissipation: current_time,
        active_miner_count: 0,
        last_depletion: current_time,
    };

    ctx.db.wave_packet_source().insert(source);

    Ok(())
}

fn process_orb_dissipation(ctx: &ReducerContext) -> Result<(), String> {
    use rand::{Rng, SeedableRng};
    use rand::rngs::StdRng;

    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;

    const DISSIPATION_INTERVAL_MS: u64 = 10000; // Every 10 seconds
    const DISSIPATION_RATE: u32 = 1; // Lose 1 packet per interval
    const DISSIPATION_PROBABILITY: f32 = 0.5; // 50% chance to dissipate

    // Create RNG for probability checks
    let seed = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_nanos() as u64;
    let mut rng = StdRng::seed_from_u64(seed);

    let sources_to_check: Vec<_> = ctx.db.wave_packet_source()
        .iter()
        .filter(|source| {
            source.total_wave_packets > 0 &&
            current_time >= source.last_dissipation + DISSIPATION_INTERVAL_MS
        })
        .collect();

    for source in sources_to_check {
        let source_id = source.source_id;

        // 50% probability check FIRST - skip update entirely if roll fails
        let should_dissipate = rng.gen::<f32>() < DISSIPATION_PROBABILITY;
        if !should_dissipate {
            // Don't update anything - no database write, no event fired
            continue;
        }

        // Roll passed - now we'll actually dissipate and update
        let mut updated_source = source.clone();
        updated_source.total_wave_packets = updated_source.total_wave_packets.saturating_sub(DISSIPATION_RATE);
        updated_source.last_dissipation = current_time;

        // Also reduce composition counts
        if updated_source.total_wave_packets == 0 {
            // Clear all samples if orb is empty
            for sample in &mut updated_source.wave_packet_composition {
                sample.count = 0;
            }
        } else {
            // Reduce one random sample
            let non_empty_indices: Vec<usize> = updated_source.wave_packet_composition.iter()
                .enumerate()
                .filter(|(_, s)| s.count > 0)
                .map(|(i, _)| i)
                .collect();

            if !non_empty_indices.is_empty() {
                let random_idx = non_empty_indices[rng.gen_range(0..non_empty_indices.len())];
                updated_source.wave_packet_composition[random_idx].count =
                    updated_source.wave_packet_composition[random_idx].count.saturating_sub(1);
            }
        }

        let is_now_empty = updated_source.total_wave_packets == 0;

        ctx.db.wave_packet_source().delete(source);
        ctx.db.wave_packet_source().insert(updated_source);

        if is_now_empty {
            log::info!("Source {} fully dissipated", source_id);
        }
    }

    Ok(())
}

fn cleanup_expired_wave_packet_sources(ctx: &ReducerContext) -> Result<(), String> {
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    let expired_sources: Vec<_> = ctx.db.wave_packet_source()
        .iter()
        .filter(|source| current_time >= source.creation_time + source.lifetime_ms as u64)
        .collect();
    
    for source in expired_sources {
        log::info!("Removing expired orb {}", source.source_id);
        ctx.db.wave_packet_source().delete(source);
    }
    
    Ok(())
}

fn cleanup_old_extractions(ctx: &ReducerContext) -> Result<(), String> {
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    // Clean up extractions older than 10 seconds (should have been captured or lost)
    let old_extractions: Vec<_> = ctx.db.wave_packet_extraction()
        .iter()
        .filter(|e| current_time > e.expected_arrival + 10000)
        .collect();
    
    for extraction in old_extractions {
        log::info!("Cleaning up old extraction {}", extraction.extraction_id);
        ctx.db.wave_packet_extraction().delete(extraction);
    }
    
    Ok(())
}

// ============================================================================
// Debug Reducers
// ============================================================================

#[spacetimedb::reducer]
pub fn debug_give_crystal(
    ctx: &ReducerContext,
    crystal_type: CrystalType,
) -> Result<(), String> {
    log::info!("=== DEBUG_GIVE_CRYSTAL START ===");
    log::info!("Crystal type: {:?}, Identity: {:?}", crystal_type, ctx.sender);
    
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    // Remove existing crystal if any
    if let Some(existing) = ctx.db.player_crystal().player_id().find(&player.player_id) {
        ctx.db.player_crystal().delete(existing);
        log::info!("Removed existing crystal");
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
    
    log::info!("DEBUG: Gave {} crystal to player {}", 
        match crystal_type {
            CrystalType::Red => "Red",
            CrystalType::Green => "Green", 
            CrystalType::Blue => "Blue",
        },
        player.name
    );
    
    log::info!("=== DEBUG_GIVE_CRYSTAL END ===");
    Ok(())
}

#[spacetimedb::reducer]
pub fn debug_mining_status(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("=== DEBUG_MINING_STATUS START ===");
    
    let sessions: Vec<_> = ctx.db.mining_session().iter().collect();
    log::info!("Active mining sessions: {}", sessions.len());

    for session in sessions {
        if let Some(player) = ctx.db.player().identity().find(&session.player_identity) {
            log::info!(
                "Player '{}' (ID: {}) mining orb {} with crystal composition. Total extracted: {}, Active: {}",
                player.name,
                player.player_id,
                session.source_id,
                session.total_extracted,
                session.is_active
            );
        }
    }
    // Also log orb status
    let orb_count = ctx.db.wave_packet_source().iter().count();
    let total_packets: u32 = ctx.db.wave_packet_source()
        .iter()
        .map(|source| source.total_wave_packets)
        .sum();
    
    log::info!("Total orbs: {}, Total wave packets: {}", orb_count, total_packets);
    
    log::info!("=== DEBUG_MINING_STATUS END ===");
    Ok(())
}


/// Debug command to list all active extraction records
#[spacetimedb::reducer]
pub fn debug_list_extractions(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("=== DEBUG_LIST_EXTRACTIONS START ===");

    let extractions: Vec<_> = ctx.db.wave_packet_extraction().iter().collect();
    log::info!("Active extractions: {}", extractions.len());

    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;

    for ext in extractions {
        let time_in_flight = current_time.saturating_sub(ext.departure_time);
        let time_to_arrival = ext.expected_arrival.saturating_sub(current_time);

        log::info!("  Extraction {}: Player {}, Source: {} {}, Packet {}, Total: {}",
            ext.extraction_id, ext.player_id, ext.source_type, ext.source_id,
            ext.packet_id, ext.total_count);
        log::info!("    Departure: {}, Arrival: {} (in flight: {} ms, ETA: {} ms)",
            ext.departure_time, ext.expected_arrival, time_in_flight, time_to_arrival);
        log::info!("    Composition ({} frequencies):", ext.composition.len());
        for sample in &ext.composition {
            log::info!("      Frequency {:.2}: {} packets (amp: {:.2}, phase: {:.2})",
                sample.frequency, sample.count, sample.amplitude, sample.phase);
        }
    }

    log::info!("=== DEBUG_LIST_EXTRACTIONS END ===");
    Ok(())
}

// ============================================================================
// Debug Spawn Position Testing
// ============================================================================

#[spacetimedb::reducer]
pub fn debug_reset_spawn_position(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("=== DEBUG_RESET_SPAWN_POSITION START ===");
    log::info!("Identity: {:?}", ctx.sender);
    
    // Find the player
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    let player_name = player.name.clone();
    let old_position = player.position.clone();
    
    // Calculate new spawn position using the helper function
    let new_position = calculate_spawn_position(&player.current_world);
    
    log::info!("Resetting spawn position for player '{}'", player_name);
    log::info!("  Old position: ({:.2}, {:.2}, {:.2})", 
        old_position.x, old_position.y, old_position.z);
    log::info!("  New position: ({:.2}, {:.2}, {:.2})", 
        new_position.x, new_position.y, new_position.z);
    
    // Update player position
    let mut updated_player = player.clone();
    updated_player.position = new_position;
    updated_player.rotation = DbQuaternion::default(); // Reset rotation too
    updated_player.last_update = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    ctx.db.player().delete(player);
    ctx.db.player().insert(updated_player);
    
    log::info!("Spawn position reset complete for '{}'", player_name);
    log::info!("=== DEBUG_RESET_SPAWN_POSITION END ===");
    Ok(())
}

#[spacetimedb::reducer]
pub fn debug_test_spawn_positions(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("=== DEBUG_TEST_SPAWN_POSITIONS START ===");
    
    // Test various world coordinates
    let test_worlds = vec![
        WorldCoords { x: 0, y: 0, z: 0 },    // Center world
        WorldCoords { x: 1, y: 0, z: 0 },    // Adjacent world
        WorldCoords { x: 0, y: 1, z: 0 },
        WorldCoords { x: 0, y: 0, z: 1 },
        WorldCoords { x: -1, y: -1, z: -1 }, // Diagonal world
    ];
    
    log::info!("Testing spawn position calculations:");
    for world in test_worlds {
        let spawn_pos = calculate_spawn_position(&world);
        log::info!("  World ({},{},{}) -> Spawn ({:.2}, {:.2}, {:.2})",
            world.x, world.y, world.z,
            spawn_pos.x, spawn_pos.y, spawn_pos.z);
        
        // Validate the spawn position
        let magnitude = (spawn_pos.x * spawn_pos.x + 
                        spawn_pos.y * spawn_pos.y + 
                        spawn_pos.z * spawn_pos.z).sqrt();
        
        let expected_magnitude = if world.x == 0 && world.y == 0 && world.z == 0 {
            WORLD_RADIUS + SURFACE_OFFSET
        } else {
            // For other worlds, calculate expected based on world spacing
            let world_center_mag = ((world.x as f32 * 10000.0).powi(2) +
                                   (world.y as f32 * 10000.0).powi(2) +
                                   (world.z as f32 * 10000.0).powi(2)).sqrt();
            world_center_mag + WORLD_RADIUS + SURFACE_OFFSET
        };
        
        let error = (magnitude - expected_magnitude).abs();
        if error > 1.0 {
            log::warn!("    WARNING: Spawn position error: {:.2} units", error);
        } else {
            log::info!("    OK: Within tolerance (error: {:.3})", error);
        }
    }
    
    log::info!("=== DEBUG_TEST_SPAWN_POSITIONS END ===");
    Ok(())
}

#[spacetimedb::reducer]
pub fn debug_validate_all_players(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("=== DEBUG_VALIDATE_ALL_PLAYERS START ===");
    
    let mut invalid_count = 0;
    let mut corrected_count = 0;
    
    for player in ctx.db.player().iter() {
        let position = &player.position;
        let magnitude = (position.x * position.x + 
                        position.y * position.y + 
                        position.z * position.z).sqrt();
        
        // Check for invalid positions
        let mut is_invalid = false;
        let mut reason = String::new();
        
        if position.x.is_nan() || position.y.is_nan() || position.z.is_nan() {
            is_invalid = true;
            reason = "Contains NaN".to_string();
        } else if position.x.is_infinite() || position.y.is_infinite() || position.z.is_infinite() {
            is_invalid = true;
            reason = "Contains Infinity".to_string();
        } else if magnitude < 10.0 {
            is_invalid = true;
            reason = format!("Too close to origin (mag: {:.2})", magnitude);
        } else if (position.y - 100.0).abs() < 1.0 && magnitude < 200.0 {
            is_invalid = true;
            reason = "Old default position (y=100)".to_string();
        }
        
        if is_invalid {
            invalid_count += 1;
            log::warn!("Player '{}' has invalid position: {} - {}",
                player.name, reason,
                format!("({:.2}, {:.2}, {:.2})", position.x, position.y, position.z));
            
            // Correct the position
            let corrected_position = calculate_spawn_position(&player.current_world);
            
            let mut updated_player = player.clone();
            updated_player.position = corrected_position;
            updated_player.last_update = ctx.timestamp
                .duration_since(Timestamp::UNIX_EPOCH)
                .expect("Valid timestamp")
                .as_millis() as u64;
            
            ctx.db.player().delete(player.clone());
            ctx.db.player().insert(updated_player);
            
            corrected_count += 1;
            log::info!("  Corrected to: ({:.2}, {:.2}, {:.2})",
                corrected_position.x, corrected_position.y, corrected_position.z);
        } else {
            // Check if on correct surface
            let expected_distance = WORLD_RADIUS + SURFACE_OFFSET;
            let distance_error = (magnitude - expected_distance).abs();
            
            if distance_error > 5.0 {
                log::info!("Player '{}' not on surface (error: {:.2} units) at ({:.2}, {:.2}, {:.2})",
                    player.name, distance_error,
                    position.x, position.y, position.z);
            }
        }
    }
    
    let total_players = ctx.db.player().iter().count();
    log::info!("Validation complete:");
    log::info!("  Total players: {}", total_players);
    log::info!("  Invalid positions: {}", invalid_count);
    log::info!("  Positions corrected: {}", corrected_count);
    
    log::info!("=== DEBUG_VALIDATE_ALL_PLAYERS END ===");
    Ok(())
}

// ============================================================================
// NEW: Concurrent Mining System Reducers
// ============================================================================

/// TESTING REDUCER: Spawn a test orb at a specified position
///
/// # Arguments
/// * `x`, `y`, `z` - Position coordinates
/// * `frequency` - Frequency band (0=Red, 1=Yellow, 2=Green, 3=Cyan, 4=Blue, 5=Magenta)
/// * `packet_count` - Number of wave packets in the orb
///
/// # Example
/// ```bash
/// spacetime call system spawn_test_orb 10.0 20.0 30.0 4 100
/// ```
#[spacetimedb::reducer]
pub fn spawn_test_orb(
    ctx: &ReducerContext,
    x: f32,
    y: f32,
    z: f32,
    frequency: u8,
    packet_count: u32,
) -> Result<(), String> {
    log::info!("=== SPAWN_TEST_ORB START ===");
    log::info!("Position: ({}, {}, {}), Frequency: {}, Packets: {}", x, y, z, frequency, packet_count);

    // Map frequency number to actual frequency value (radians)
    let freq_value = match frequency {
        0 => FREQ_RED,      // 0.0
        1 => FREQ_YELLOW,   // 1.047
        2 => FREQ_GREEN,    // 2.094
        3 => FREQ_CYAN,     // 3.142
        4 => FREQ_BLUE,     // 4.189
        5 => FREQ_MAGENTA,  // 5.236
        _ => return Err("Invalid frequency (must be 0-5)".to_string()),
    };

    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;

    // Create wave packet composition with single frequency
    let composition = vec![WavePacketSample {
        frequency: freq_value,
        amplitude: 1.0,
        phase: 0.0,
        count: packet_count,
    }];

    // Create orb at specified position (stationary for debug spawns)
    let position = DbVector3::new(x, y, z);
    let source = WavePacketSource {
        source_id: 0, // auto_inc will assign
        world_coords: WorldCoords { x: 0, y: 0, z: 0 },
        position,
        velocity: DbVector3::zero(),
        destination: position,
        state: SOURCE_STATE_STATIONARY,
        wave_packet_composition: composition,
        total_wave_packets: packet_count,
        creation_time: current_time,
        lifetime_ms: 3600000, // 1 hour lifetime
        last_dissipation: current_time,
        active_miner_count: 0,
        last_depletion: current_time,
    };

    // Insert into database
    ctx.db.wave_packet_source().insert(source.clone());

    log::info!("Test orb spawned successfully at ({}, {}, {}) with {} {:?} packets",
        x, y, z, packet_count, freq_value);
    log::info!("=== SPAWN_TEST_ORB END ===");

    Ok(())
}

/// Debug reducer to spawn orbs with mixed RGB composition
/// Useful for testing crystal-based frequency filtering
///
/// # Arguments
/// * `x, y, z` - Position in world space
/// * `red_packets` - Number of red frequency packets (0.0 rad)
/// * `green_packets` - Number of green frequency packets (2π/3 rad)
/// * `blue_packets` - Number of blue frequency packets (4π/3 rad)
#[spacetimedb::reducer]
pub fn spawn_mixed_orb(
    ctx: &ReducerContext,
    x: f32,
    y: f32,
    z: f32,
    red_packets: u32,
    green_packets: u32,
    blue_packets: u32,
) -> Result<(), String> {
    log::info!("=== SPAWN_MIXED_ORB DEBUG ===");
    log::info!("Position: ({}, {}, {})", x, y, z);
    log::info!("Composition: R:{}, G:{}, B:{}", red_packets, green_packets, blue_packets);

    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;

    // Build composition from packet counts
    let mut composition = Vec::new();

    if red_packets > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_RED,
            amplitude: 1.0,
            phase: 0.0,
            count: red_packets,
        });
    }

    if green_packets > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_GREEN,
            amplitude: 1.0,
            phase: 0.0,
            count: green_packets,
        });
    }

    if blue_packets > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_BLUE,
            amplitude: 1.0,
            phase: 0.0,
            count: blue_packets,
        });
    }

    let total_packets = red_packets + green_packets + blue_packets;

    if total_packets == 0 {
        return Err("Must specify at least one packet".to_string());
    }

    // Create orb at specified position (stationary for debug spawns)
    let position = DbVector3::new(x, y, z);
    let source = WavePacketSource {
        source_id: 0,  // auto_inc will assign
        world_coords: WorldCoords { x: 0, y: 0, z: 0 },
        position,
        velocity: DbVector3::zero(),
        destination: position,
        state: SOURCE_STATE_STATIONARY,
        wave_packet_composition: composition,
        total_wave_packets: total_packets,
        creation_time: current_time,
        lifetime_ms: 3600000,  // 1 hour lifetime
        last_dissipation: current_time,
        active_miner_count: 0,
        last_depletion: current_time,
    };

    ctx.db.wave_packet_source().insert(source);

    log::info!("Mixed orb spawned with {} total packets (R:{} G:{} B:{})",
        total_packets, red_packets, green_packets, blue_packets);
    log::info!("=== SPAWN_MIXED_ORB END ===");

    Ok(())
}

/// Debug reducer to spawn orbs with all 6 frequency types
/// Full spectrum: Red, Yellow, Green, Cyan, Blue, Magenta
///
/// # Arguments
/// * `x, y, z` - Position in world space
/// * `red_packets` - Red (0° = 0.0 rad)
/// * `yellow_packets` - Yellow (60° = 1.047 rad)
/// * `green_packets` - Green (120° = 2.094 rad)
/// * `cyan_packets` - Cyan (180° = 3.142 rad)
/// * `blue_packets` - Blue (240° = 4.189 rad)
/// * `magenta_packets` - Magenta (300° = 5.236 rad)
#[spacetimedb::reducer]
pub fn spawn_full_spectrum_orb(
    ctx: &ReducerContext,
    x: f32,
    y: f32,
    z: f32,
    red_packets: u32,
    yellow_packets: u32,
    green_packets: u32,
    cyan_packets: u32,
    blue_packets: u32,
    magenta_packets: u32,
) -> Result<(), String> {
    log::info!("=== SPAWN_FULL_SPECTRUM_ORB ===");
    log::info!("Position: ({}, {}, {})", x, y, z);
    log::info!("Composition: R:{} Y:{} G:{} C:{} B:{} M:{}",
        red_packets, yellow_packets, green_packets, cyan_packets, blue_packets, magenta_packets);

    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;

    // Build composition from packet counts (all 6 frequencies)
    let mut composition = Vec::new();

    if red_packets > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_RED,
            amplitude: 1.0,
            phase: 0.0,
            count: red_packets,
        });
    }

    if yellow_packets > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_YELLOW,
            amplitude: 1.0,
            phase: 0.0,
            count: yellow_packets,
        });
    }

    if green_packets > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_GREEN,
            amplitude: 1.0,
            phase: 0.0,
            count: green_packets,
        });
    }

    if cyan_packets > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_CYAN,
            amplitude: 1.0,
            phase: 0.0,
            count: cyan_packets,
        });
    }

    if blue_packets > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_BLUE,
            amplitude: 1.0,
            phase: 0.0,
            count: blue_packets,
        });
    }

    if magenta_packets > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_MAGENTA,
            amplitude: 1.0,
            phase: 0.0,
            count: magenta_packets,
        });
    }

    let total_packets = red_packets + yellow_packets + green_packets
        + cyan_packets + blue_packets + magenta_packets;

    if total_packets == 0 {
        return Err("Must specify at least one packet".to_string());
    }

    // Create orb at specified position (stationary for debug spawns)
    let position = DbVector3::new(x, y, z);
    let source = WavePacketSource {
        source_id: 0,  // auto_inc will assign
        world_coords: WorldCoords { x: 0, y: 0, z: 0 },
        position,
        velocity: DbVector3::zero(),
        destination: position,
        state: SOURCE_STATE_STATIONARY,
        wave_packet_composition: composition,
        total_wave_packets: total_packets,
        creation_time: current_time,
        lifetime_ms: 3600000,  // 1 hour lifetime
        last_dissipation: current_time,
        active_miner_count: 0,
        last_depletion: current_time,
    };

    ctx.db.wave_packet_source().insert(source);

    log::info!("Full spectrum orb spawned with {} total packets", total_packets);
    log::info!("=== SPAWN_FULL_SPECTRUM_ORB END ===");

    Ok(())
}

/// Advanced debug reducer to spawn orbs with flexible options
///
/// # Arguments
/// * `player_name` - Optional player name to spawn near (empty string = random surface positions)
/// * `orb_count` - Number of orbs to spawn
/// * `height_from_surface` - Height above the sphere surface (in units)
/// * `red`, `yellow`, `green`, `cyan`, `blue`, `magenta` - Packet counts per frequency (0 to skip)
///
/// # Examples
/// ```
/// // Spawn 10 mixed orbs near player "Alice" at 5 units above surface
/// spawn_debug_orbs("Alice", 10, 5.0, 50, 30, 40, 0, 60, 0)
///
/// // Spawn 20 random orbs across the surface at 10 units high
/// spawn_debug_orbs("", 20, 10.0, 100, 50, 75, 25, 80, 40)
/// ```
#[spacetimedb::reducer]
pub fn spawn_debug_orbs(
    ctx: &ReducerContext,
    player_name: String,
    orb_count: u32,
    height_from_surface: f32,
    red: u32,
    yellow: u32,
    green: u32,
    cyan: u32,
    blue: u32,
    magenta: u32,
) -> Result<(), String> {
    log::info!("=== SPAWN_DEBUG_ORBS START ===");
    log::info!("Player: '{}', Count: {}, Height: {}", player_name, orb_count, height_from_surface);
    log::info!("Composition: R:{} Y:{} G:{} C:{} B:{} M:{}", red, yellow, green, cyan, blue, magenta);

    // Validate at least one frequency has packets
    let total_packets = red + yellow + green + cyan + blue + magenta;
    if total_packets == 0 {
        return Err("Must specify at least one packet type".to_string());
    }

    // Build composition from packet counts
    let mut composition = Vec::new();
    if red > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_RED,
            amplitude: 1.0,
            phase: 0.0,
            count: red,
        });
    }
    if yellow > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_YELLOW,
            amplitude: 1.0,
            phase: 0.0,
            count: yellow,
        });
    }
    if green > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_GREEN,
            amplitude: 1.0,
            phase: 0.0,
            count: green,
        });
    }
    if cyan > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_CYAN,
            amplitude: 1.0,
            phase: 0.0,
            count: cyan,
        });
    }
    if blue > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_BLUE,
            amplitude: 1.0,
            phase: 0.0,
            count: blue,
        });
    }
    if magenta > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_MAGENTA,
            amplitude: 1.0,
            phase: 0.0,
            count: magenta,
        });
    }

    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;

    // Determine spawn origin
    let spawn_origin = if player_name.is_empty() {
        // Random spawn mode
        None
    } else {
        // Find player
        let player = ctx.db.player()
            .iter()
            .find(|p| p.name == player_name)
            .ok_or(format!("Player '{}' not found", player_name))?;

        log::info!("Found player at ({}, {}, {})",
            player.position.x, player.position.y, player.position.z);
        Some(player.position)
    };

    // Spawn orbs
    for i in 0..orb_count {
        let position = if let Some(origin) = spawn_origin {
            // Spawn in circle around player
            let angle = (i as f32 / orb_count as f32) * 2.0 * PI;
            let radius = 20.0; // 20 units from player

            // Calculate offset in tangent plane
            let origin_norm = (origin.x * origin.x + origin.y * origin.y + origin.z * origin.z).sqrt();
            let up = DbVector3::new(origin.x / origin_norm, origin.y / origin_norm, origin.z / origin_norm);

            // Find perpendicular vectors for tangent plane
            let arbitrary = if up.y.abs() < 0.9 {
                DbVector3::new(0.0, 1.0, 0.0)
            } else {
                DbVector3::new(1.0, 0.0, 0.0)
            };

            // Cross product for first tangent
            let tangent1 = DbVector3::new(
                arbitrary.y * up.z - arbitrary.z * up.y,
                arbitrary.z * up.x - arbitrary.x * up.z,
                arbitrary.x * up.y - arbitrary.y * up.x,
            );
            let t1_len = (tangent1.x * tangent1.x + tangent1.y * tangent1.y + tangent1.z * tangent1.z).sqrt();
            let tangent1 = DbVector3::new(tangent1.x / t1_len, tangent1.y / t1_len, tangent1.z / t1_len);

            // Cross product for second tangent
            let tangent2 = DbVector3::new(
                up.y * tangent1.z - up.z * tangent1.y,
                up.z * tangent1.x - up.x * tangent1.z,
                up.x * tangent1.y - up.y * tangent1.x,
            );

            // Calculate position in circle on tangent plane
            let offset_x = angle.cos() * radius;
            let offset_y = angle.sin() * radius;

            // Position at fixed radius from world center (WORLD_RADIUS + height)
            let target_radius = WORLD_RADIUS + height_from_surface;

            // Calculate point on tangent plane
            let tangent_point = DbVector3::new(
                origin.x + tangent1.x * offset_x + tangent2.x * offset_y,
                origin.y + tangent1.y * offset_x + tangent2.y * offset_y,
                origin.z + tangent1.z * offset_x + tangent2.z * offset_y,
            );

            // Normalize and scale to target radius
            let tp_len = (tangent_point.x * tangent_point.x + tangent_point.y * tangent_point.y + tangent_point.z * tangent_point.z).sqrt();
            DbVector3::new(
                tangent_point.x * target_radius / tp_len,
                tangent_point.y * target_radius / tp_len,
                tangent_point.z * target_radius / tp_len,
            )
        } else {
            // Random position on sphere surface
            // Use spherical coordinates
            let theta = (i as f32 / orb_count as f32) * 2.0 * PI; // Azimuth
            let phi = ((i as f32 * 0.618033988749895) % 1.0) * PI; // Polar angle (golden ratio for distribution)

            let radius_at_height = WORLD_RADIUS + height_from_surface;

            DbVector3::new(
                radius_at_height * phi.sin() * theta.cos(),
                radius_at_height * phi.cos(),
                radius_at_height * phi.sin() * theta.sin(),
            )
        };

        // Create orb (stationary for debug spawns)
        let source = WavePacketSource {
            source_id: 0, // auto_inc
            world_coords: WorldCoords { x: 0, y: 0, z: 0 },
            position,
            velocity: DbVector3::zero(),
            destination: position,
            state: SOURCE_STATE_STATIONARY,
            wave_packet_composition: composition.clone(),
            total_wave_packets: total_packets,
            creation_time: current_time,
            lifetime_ms: 3600000, // 1 hour
            last_dissipation: current_time,
            active_miner_count: 0,
            last_depletion: current_time,
        };

        ctx.db.wave_packet_source().insert(source);
        log::info!("Spawned orb {} at ({:.2}, {:.2}, {:.2})",
            i + 1, position.x, position.y, position.z);
    }

    log::info!("=== SPAWN_DEBUG_ORBS END - Created {} orbs ===", orb_count);
    Ok(())
}

/// NEW CONCURRENT MINING: Start mining an orb
/// Multiple players can mine the same orb simultaneously
///
/// # Arguments
/// * `source_id` - The orb to mine
///
/// # Returns
/// * Ok(()) if session started successfully
/// * Err if already mining this orb or orb not found
#[spacetimedb::reducer]
pub fn start_mining_v2(
    ctx: &ReducerContext,
    source_id: u64,
    crystal_composition: Vec<WavePacketSample>,
) -> Result<(), String> {
    log::info!("=== START_MINING_V2 START ===");
    log::info!("Orb ID: {}, Crystal composition: {} frequencies, Identity: {:?}",
        source_id, crystal_composition.len(), ctx.sender);
    for sample in &crystal_composition {
        log::info!("  Crystal: freq={:.3}, count={}", sample.frequency, sample.count);
    }

    // Check if player already mining THIS specific orb
    let existing_session = ctx.db.mining_session()
        .iter()
        .find(|s| s.player_identity == ctx.sender && s.source_id == source_id && s.is_active);

    if existing_session.is_some() {
        log::warn!("Player already mining this orb");
        return Err("You are already mining this orb".to_string());
    }

    // Verify orb exists and has packets remaining
    let source = ctx.db.wave_packet_source()
        .source_id()
        .find(&source_id)
        .ok_or("Orb not found")?;

    if source.total_wave_packets == 0 {
        log::warn!("Orb is depleted");
        return Err("Orb has no packets remaining".to_string());
    }

    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;

    // Validate crystal composition
    if crystal_composition.is_empty() {
        return Err("Must provide at least one crystal".to_string());
    }

    // Create new mining session
    let session = MiningSession {
        session_id: 0, // auto_inc
        player_identity: ctx.sender,
        source_id,
        crystal_composition,
        circuit_id: 0, // For future use
        started_at: current_time,
        last_extraction: current_time,
        extraction_multiplier: 1.0, // Default, for future puzzle bonuses
        total_extracted: 0,
        is_active: true,
    };

    ctx.db.mining_session().insert(session);

    // Increment orb's active miner count
    let mut updated_source = source.clone();
    updated_source.active_miner_count += 1;
    let active_count = updated_source.active_miner_count;

    ctx.db.wave_packet_source().delete(source);
    ctx.db.wave_packet_source().insert(updated_source);

    log::info!("Mining session started successfully for source {} (active miners: {})",
        source_id, active_count);
    log::info!("=== START_MINING_V2 END ===");

    Ok(())
}

/// NEW CONCURRENT MINING: Extract specific packet composition from orb (request-driven)
/// Player requests exact frequencies and counts
///
/// # Arguments
/// * `session_id` - The mining session ID
/// * `requested_frequencies` - What player wants to extract
///
/// # Returns
/// * Ok(()) if extraction successful
/// * Err if session invalid, cooldown active, orb depleted, or request cannot be fulfilled
#[spacetimedb::reducer]
pub fn extract_packets_v2(
    ctx: &ReducerContext,
    session_id: u64,
    requested_frequencies: Vec<ExtractionRequest>,
) -> Result<(), String> {
    log::info!("=== EXTRACT_PACKETS_V2 START ===");
    log::info!("Session ID: {}, Request: {} frequencies", session_id, requested_frequencies.len());
    for req in &requested_frequencies {
        log::info!("  Requesting {} packets of frequency {:.2}", req.count, req.frequency);
    }

    // Verify session exists and belongs to caller
    let session = ctx.db.mining_session()
        .session_id()
        .find(&session_id)
        .ok_or("Session not found")?;

    if session.player_identity != ctx.sender {
        log::warn!("Session does not belong to caller");
        return Err("Session does not belong to you".to_string());
    }

    if !session.is_active {
        log::warn!("Session is not active");
        return Err("Session is not active".to_string());
    }

    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;

    // Check 2-second cooldown
    const EXTRACTION_COOLDOWN_MS: u64 = 2000;
    let time_since_last = current_time.saturating_sub(session.last_extraction);

    if time_since_last < EXTRACTION_COOLDOWN_MS {
        let remaining_ms = EXTRACTION_COOLDOWN_MS - time_since_last;
        return Err(format!("Extraction on cooldown ({} ms remaining)", remaining_ms));
    }

    // Get the orb
    let source = ctx.db.wave_packet_source()
        .source_id()
        .find(&session.source_id)
        .ok_or("Orb no longer exists")?;

    // Validate request against orb composition AND crystal composition filtering
    let mut actual_extraction: Vec<WavePacketSample> = Vec::new();
    let mut total_to_extract = 0u32;

    for request in &requested_frequencies {
        // Check if crystal composition can extract this frequency
        // Exact match (within 0.01 rad) required
        let crystal_match = session.crystal_composition.iter()
            .find(|crystal| (crystal.frequency - request.frequency).abs() < 0.01);

        if crystal_match.is_none() {
            log::info!("  No crystal matches frequency {:.3} - skipping", request.frequency);
            continue;
        }

        let crystal = crystal_match.unwrap();

        // Crystal count determines extraction efficiency (10% per crystal)
        let extraction_rate = (crystal.count as f32 * 0.1).min(1.0);
        log::info!("  Crystal freq {:.3} (count={}) can extract at {:.0}% efficiency",
            crystal.frequency, crystal.count, extraction_rate * 100.0);

        // Find matching frequency in orb
        let available_sample = source.wave_packet_composition.iter()
            .find(|s| (s.frequency - request.frequency).abs() < 0.001);

        if let Some(sample) = available_sample {
            if sample.count >= request.count {
                // Can fulfill this request
                actual_extraction.push(WavePacketSample {
                    frequency: request.frequency,
                    amplitude: sample.amplitude,
                    phase: sample.phase,
                    count: request.count,
                });
                total_to_extract += request.count;

                log::info!("  Can extract {} packets of frequency {:.2}",
                    request.count, request.frequency);
            } else if sample.count > 0 {
                // Partial fulfillment
                actual_extraction.push(WavePacketSample {
                    frequency: request.frequency,
                    amplitude: sample.amplitude,
                    phase: sample.phase,
                    count: sample.count, // Give what we have
                });
                total_to_extract += sample.count;

                log::info!("  Partial: requested {} but only {} available for frequency {:.2}",
                    request.count, sample.count, request.frequency);
            } else {
                log::info!("  Cannot extract frequency {:.2} - none available", request.frequency);
            }
        } else {
            log::info!("  Frequency {:.2} not found in orb", request.frequency);
        }
    }

    if actual_extraction.is_empty() {
        return Err("Cannot fulfill extraction request - no matching frequencies available".to_string());
    }

    // Deduct from orb composition
    let mut updated_composition = source.wave_packet_composition.clone();

    for extracted in &actual_extraction {
        for sample in &mut updated_composition {
            if (sample.frequency - extracted.frequency).abs() < 0.001 {
                sample.count = sample.count.saturating_sub(extracted.count);
                break;
            }
        }
    }

    // Update orb
    let mut updated_source = source.clone();
    updated_source.wave_packet_composition = updated_composition;
    updated_source.total_wave_packets = updated_source.total_wave_packets.saturating_sub(total_to_extract);
    updated_source.last_depletion = current_time;

    // Save values we need before moving session
    let session_source_id = session.source_id;
    let session_player_identity = session.player_identity;

    // Update mining session (do this before modifying orb/session state)
    let mut updated_session = session.clone();
    updated_session.last_extraction = current_time;
    updated_session.total_extracted += total_to_extract;

    // Check if orb is now empty
    if updated_source.total_wave_packets == 0 {
        log::info!("Orb depleted, removing from world");
        updated_session.is_active = false;
        // Delete the depleted orb instead of updating it
        ctx.db.wave_packet_source().delete(source);
    } else {
        // Update orb if still has packets
        ctx.db.wave_packet_source().delete(source);
        ctx.db.wave_packet_source().insert(updated_source.clone());
    }

    ctx.db.mining_session().delete(session);
    ctx.db.mining_session().insert(updated_session);

    // Get player for visual packet creation
    let player = ctx.db.player()
        .identity()
        .find(&session_player_identity)
        .ok_or("Player not found")?;

    // Create visual extraction record with EXACT requested composition
    if !actual_extraction.is_empty() {
        let packet_id = (session_id << 32) | (current_time & 0xFFFFFFFF);
        let flight_time = 3000u64; // 3 seconds

        let extraction = WavePacketExtraction {
            extraction_id: 0, // auto_inc
            player_id: player.player_id,
            source_type: "orb".to_string(),
            source_id: session_source_id,
            packet_id,
            composition: actual_extraction.clone(), // Exact composition extracted
            total_count: total_to_extract,
            departure_time: current_time,
            expected_arrival: current_time + flight_time,
        };

        ctx.db.wave_packet_extraction().insert(extraction);

        log::info!("Created extraction record with {} total packets:", total_to_extract);
        for sample in &actual_extraction {
            log::info!("  Frequency {:.2}: {} packets", sample.frequency, sample.count);
        }

    }

    log::info!("Extracted {} total packets (orb remaining: {})",
        total_to_extract, updated_source.total_wave_packets);
    log::info!("=== EXTRACT_PACKETS_V2 END ===");

    Ok(())
}

/// Capture extracted wave packet when it arrives at player (visual complete)
/// This is called by the client when the visual packet reaches the player
///
/// # Arguments
/// * `packet_id` - The packet ID to capture
///
/// # Returns
/// * Ok(()) if packet captured successfully
/// * Err if extraction record not found or doesn't belong to caller
#[spacetimedb::reducer]
pub fn capture_extracted_packet_v2(
    ctx: &ReducerContext,
    packet_id: u64,
) -> Result<(), String> {
    log::info!("=== CAPTURE_EXTRACTED_PACKET_V2 START ===");
    log::info!("Packet ID: {}, Identity: {:?}", packet_id, ctx.sender);

    // Find and remove the extraction record (visual cleanup)
    let extraction = ctx.db.wave_packet_extraction()
        .iter()
        .find(|e| e.packet_id == packet_id)
        .ok_or("Extraction record not found")?;

    // Verify it belongs to the caller
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;

    if extraction.player_id != player.player_id {
        return Err("This packet doesn't belong to you".to_string());
    }

    // Add packet composition to player inventory
    let inventory = ctx.db.player_inventory()
        .player_id()
        .find(&player.player_id);

    if let Some(mut inv) = inventory.clone() {
        // Merge extracted composition into inventory
        for extracted_sample in &extraction.composition {
            let freq_int = (extracted_sample.frequency * 100.0).round() as i32;
            let mut found = false;

            for inv_sample in inv.inventory_composition.iter_mut() {
                let inv_freq_int = (inv_sample.frequency * 100.0).round() as i32;
                if inv_freq_int == freq_int {
                    inv_sample.count += extracted_sample.count;
                    found = true;
                    break;
                }
            }

            // If frequency not in inventory, add new sample
            if !found {
                inv.inventory_composition.push(extracted_sample.clone());
            }
        }

        inv.total_count += extraction.total_count;
        inv.last_updated = ctx.timestamp;

        // Check max capacity
        if inv.total_count > 300 {
            return Err("Inventory full (max 300 packets)".to_string());
        }

        let new_total = inv.total_count;
        // Update inventory
        ctx.db.player_inventory().delete(inventory.unwrap());
        ctx.db.player_inventory().insert(inv);

        log::info!("Added {} packets to player {} inventory (new total: {})",
            extraction.total_count, player.player_id, new_total);
    } else {
        // Create new inventory if doesn't exist
        let new_inv = PlayerInventory {
            player_id: player.player_id,
            inventory_composition: extraction.composition.clone(),
            total_count: extraction.total_count,
            last_updated: ctx.timestamp,
        };
        ctx.db.player_inventory().insert(new_inv);
        log::info!("Created inventory for player {} with {} packets",
            player.player_id, extraction.total_count);
    }

    ctx.db.wave_packet_extraction().delete(extraction);

    log::info!("Captured packet {} for player {}", packet_id, player.player_id);
    log::info!("=== CAPTURE_EXTRACTED_PACKET_V2 END ===");

    Ok(())
}

/// NEW CONCURRENT MINING: Stop mining
///
/// # Arguments
/// * `session_id` - The mining session ID to stop
///
/// # Returns
/// * Ok(()) if session stopped successfully
/// * Err if session not found or doesn't belong to caller
#[spacetimedb::reducer]
pub fn stop_mining_v2(
    ctx: &ReducerContext,
    session_id: u64,
) -> Result<(), String> {
    log::info!("=== STOP_MINING_V2 START ===");
    log::info!("Session ID: {}, Identity: {:?}", session_id, ctx.sender);

    // Verify session exists and belongs to caller
    let session = ctx.db.mining_session()
        .session_id()
        .find(&session_id)
        .ok_or("Session not found")?;

    if session.player_identity != ctx.sender {
        log::warn!("Session does not belong to caller");
        return Err("Session does not belong to you".to_string());
    }

    // Mark session as inactive
    let mut updated_session = session.clone();
    updated_session.is_active = false;
    let source_id = session.source_id;

    ctx.db.mining_session().delete(session);
    ctx.db.mining_session().insert(updated_session);

    // NOTE: Dont clean up pending extractions - let them complete and add to inventory
    // The client will call capture_extracted_packet_v2 when packets arrive
    log::info!("Mining session stopped - pending extractions will complete normally");


    // Decrement orb's active miner count
    if let Some(source) = ctx.db.wave_packet_source().source_id().find(&source_id) {
        let mut updated_source = source.clone();
        updated_source.active_miner_count = updated_source.active_miner_count.saturating_sub(1);
        let active_count = updated_source.active_miner_count;

        ctx.db.wave_packet_source().delete(source);
        ctx.db.wave_packet_source().insert(updated_source);

        log::info!("Mining session stopped (orb active miners: {})", active_count);
    } else {
        log::info!("Mining session stopped (orb no longer exists)");
    }

    log::info!("=== STOP_MINING_V2 END ===");

    Ok(())
}

// ============================================================================
// NEW: Test Utility Reducers
// ============================================================================

/// TESTING: Clear all wave packet sources from the database
/// WARNING: Test only - removes all wave packet sources
#[spacetimedb::reducer]
pub fn clear_all_sources(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("=== CLEAR_ALL_SOURCES START ===");

    let sources: Vec<_> = ctx.db.wave_packet_source().iter().collect();
    let count = sources.len();

    for source in sources {
        ctx.db.wave_packet_source().delete(source);
    }

    log::info!("Cleared {} sources", count);
    log::info!("=== CLEAR_ALL_SOURCES END ===");

    Ok(())
}

/// TESTING: Clear all storage devices
/// Useful for testing and cleanup
#[spacetimedb::reducer]
pub fn clear_all_storage_devices(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("=== CLEAR_ALL_STORAGE_DEVICES START ===");

    let devices: Vec<_> = ctx.db.storage_device().iter().collect();
    let count = devices.len();

    for device in devices {
        ctx.db.storage_device().delete(device);
    }

    log::info!("Cleared {} storage devices for testing", count);
    log::info!("=== CLEAR_ALL_STORAGE_DEVICES END ===");

    Ok(())
}

/// TESTING: Set an orb's packet count instantly
/// Useful for testing depletion scenarios
#[spacetimedb::reducer]
pub fn set_orb_packets(
    ctx: &ReducerContext,
    source_id: u64,
    new_count: u32,
) -> Result<(), String> {
    log::info!("=== SET_ORB_PACKETS START ===");
    log::info!("Orb ID: {}, New count: {}", source_id, new_count);

    let source = ctx.db.wave_packet_source()
        .source_id()
        .find(&source_id)
        .ok_or("Orb not found")?;

    let mut updated = source.clone();
    updated.total_wave_packets = new_count;

    // Also update first composition sample if it exists
    if let Some(first_sample) = updated.wave_packet_composition.get_mut(0) {
        first_sample.count = new_count;
    }

    ctx.db.wave_packet_source().delete(source);
    ctx.db.wave_packet_source().insert(updated);

    log::info!("Set orb {} to {} packets", source_id, new_count);
    log::info!("=== SET_ORB_PACKETS END ===");

    Ok(())
}

/// TESTING: List all active mining sessions
/// Debug reducer to see who is mining what
#[spacetimedb::reducer]
pub fn list_active_mining(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("=== LIST_ACTIVE_MINING START ===");

    let sessions: Vec<_> = ctx.db.mining_session()
        .iter()
        .filter(|s| s.is_active)
        .collect();

    log::info!("Active mining sessions: {}", sessions.len());

    for session in &sessions {
        log::info!("  Session {}: Player {:?} mining orb {} (extracted: {}, multiplier: {})",
            session.session_id,
            session.player_identity,
            session.source_id,
            session.total_extracted,
            session.extraction_multiplier
        );
    }

    // Also show orb stats
    let orbs: Vec<_> = ctx.db.wave_packet_source().iter().collect();
    log::info!("Orbs in database: {}", orbs.len());

    for source in &orbs {
        log::info!("  Orb {}: {} packets remaining, {} active miners",
            source.source_id,
            source.total_wave_packets,
            source.active_miner_count
        );
    }

    log::info!("=== LIST_ACTIVE_MINING END ===");

    Ok(())
}

// ============================================================================
// Energy Transfer System - Helper Functions
// ============================================================================

/// Get player inventory as WavePacketSample composition
/// Returns samples for all non-zero frequency counts
fn get_inventory_composition(ctx: &ReducerContext, player_id: u64) -> Result<Vec<WavePacketSample>, String> {
    let inventory = ctx.db.player_inventory()
        .player_id()
        .find(&player_id)
        .ok_or("Player inventory not found")?;

    Ok(inventory.inventory_composition.clone())
}

/// Deduct specific composition from player inventory
fn deduct_composition_from_inventory(ctx: &ReducerContext, player_id: u64, composition: &Vec<WavePacketSample>) -> Result<(), String> {
    let inventory = ctx.db.player_inventory()
        .player_id()
        .find(&player_id)
        .ok_or("Player inventory not found")?;

    let mut new_composition = inventory.inventory_composition.clone();
    let mut new_total = inventory.total_count;
    
    // Deduct each requested frequency/count from inventory composition
    for requested in composition {
        let freq_int = (requested.frequency * 100.0).round() as i32;
        let mut found = false;
        
        for inv_sample in new_composition.iter_mut() {
            let inv_freq_int = (inv_sample.frequency * 100.0).round() as i32;
            if inv_freq_int == freq_int {
                if inv_sample.count < requested.count {
                    return Err(format!("Insufficient inventory for frequency {}: have {}, need {}", 
                        requested.frequency, inv_sample.count, requested.count));
                }
                inv_sample.count -= requested.count;
                new_total -= requested.count;
                found = true;
                break;
            }
        }
        
        if !found {
            return Err(format!("Frequency {} not found in inventory", requested.frequency));
        }
    }
    
    // Remove samples with zero count
    new_composition.retain(|s| s.count > 0);
    
    let updated = PlayerInventory {
        player_id,
        inventory_composition: new_composition,
        total_count: new_total,
        last_updated: ctx.timestamp,
    };

    // SpacetimeDB update pattern: delete + insert
    ctx.db.player_inventory().delete(inventory);
    ctx.db.player_inventory().insert(updated);

    Ok(())
}

/// Find nearest energy spire to a position on a world
fn find_nearest_spire(ctx: &ReducerContext, world_coords: WorldCoords, position: DbVector3) -> Result<DistributionSphere, String> {
    let spires: Vec<_> = ctx.db.distribution_sphere()
        .iter()
        .filter(|s| s.world_coords == world_coords)
        .collect();

    if spires.is_empty() {
        return Err(format!("No distribution spheres found on world ({}, {}, {})", world_coords.x, world_coords.y, world_coords.z));
    }

    let mut nearest: Option<DistributionSphere> = None;
    let mut nearest_dist = f32::MAX;

    for spire in spires {
        let dx = spire.sphere_position.x - position.x;
        let dy = spire.sphere_position.y - position.y;
        let dz = spire.sphere_position.z - position.z;
        let dist = (dx*dx + dy*dy + dz*dz).sqrt();

        if dist < nearest_dist {
            nearest_dist = dist;
            nearest = Some(spire.clone());
        }
    }

    nearest.ok_or("Failed to find nearest sphere".to_string())
}

// ============================================================================
// Energy Transfer System - Reducers
// ============================================================================

/// Initialize player's energy inventory
/// Creates empty inventory with 0 packets
#[spacetimedb::reducer]
pub fn initialize_player_inventory(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("=== INITIALIZE_PLAYER_INVENTORY START ===");

    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;

    // Check if inventory already exists
    if ctx.db.player_inventory().player_id().find(&player.player_id).is_some() {
        return Err("Inventory already initialized".to_string());
    }

    let inventory = PlayerInventory {
        player_id: player.player_id,
        inventory_composition: Vec::new(),
        total_count: 0,
        last_updated: ctx.timestamp,
    };

    ctx.db.player_inventory().insert(inventory);

    log::info!("Initialized inventory for player {}", player.player_id);
    log::info!("=== INITIALIZE_PLAYER_INVENTORY END ===");

    Ok(())
}

/// Ensure player has an inventory, creating one if it doesn't exist
/// This is a safe version that doesn't error if inventory already exists
/// EXCEPTION USE ONLY: Should be called by UI as a fallback, not during normal gameplay
#[spacetimedb::reducer]
pub fn ensure_player_inventory(ctx: &ReducerContext) -> Result<(), String> {
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;

    // Check if inventory already exists
    if ctx.db.player_inventory().player_id().find(&player.player_id).is_some() {
        // Inventory exists, nothing to do
        return Ok(());
    }

    // Create empty inventory
    let inventory = PlayerInventory {
        player_id: player.player_id,
        inventory_composition: Vec::new(),
        total_count: 0,
        last_updated: ctx.timestamp,
    };

    ctx.db.player_inventory().insert(inventory);
    log::info!("Auto-created empty inventory for player {}", player.player_id);

    Ok(())
}

/// DEBUG: Add test packets to player's inventory
#[spacetimedb::reducer]
pub fn debug_add_test_packets(
    ctx: &ReducerContext,
    red: u32,
    yellow: u32,
    green: u32,
    cyan: u32,
    blue: u32,
    magenta: u32
) -> Result<(), String> {
    log::info!("=== DEBUG_ADD_TEST_PACKETS START ===");

    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;

    // Build composition from parameters
    let mut composition = Vec::new();

    if red > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_RED,
            amplitude: 1.0,
            phase: 0.0,
            count: red,
        });
    }
    if yellow > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_YELLOW,
            amplitude: 1.0,
            phase: 0.0,
            count: yellow,
        });
    }
    if green > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_GREEN,
            amplitude: 1.0,
            phase: 0.0,
            count: green,
        });
    }
    if cyan > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_CYAN,
            amplitude: 1.0,
            phase: 0.0,
            count: cyan,
        });
    }
    if blue > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_BLUE,
            amplitude: 1.0,
            phase: 0.0,
            count: blue,
        });
    }
    if magenta > 0 {
        composition.push(WavePacketSample {
            frequency: FREQ_MAGENTA,
            amplitude: 1.0,
            phase: 0.0,
            count: magenta,
        });
    }

    let total_count = red + yellow + green + cyan + blue + magenta;

    // Get or create inventory
    let inventory_opt = ctx.db.player_inventory().player_id().find(&player.player_id);

    if let Some(mut inv) = inventory_opt.clone() {
        // Merge composition into inventory
        for new_sample in &composition {
            let freq_int = (new_sample.frequency * 100.0).round() as i32;
            let mut found = false;

            for inv_sample in inv.inventory_composition.iter_mut() {
                let inv_freq_int = (inv_sample.frequency * 100.0).round() as i32;
                if inv_freq_int == freq_int {
                    inv_sample.count += new_sample.count;
                    found = true;
                    break;
                }
            }

            // If frequency not in inventory, add new sample
            if !found {
                inv.inventory_composition.push(new_sample.clone());
            }
        }

        inv.total_count += total_count;
        inv.last_updated = ctx.timestamp;

        // Check max capacity
        if inv.total_count > 300 {
            return Err("Inventory full (max 300 packets)".to_string());
        }

        let new_total = inv.total_count;

        ctx.db.player_inventory().delete(inventory_opt.unwrap());
        ctx.db.player_inventory().insert(inv);

        log::info!("Added {} packets to player {} inventory (new total: {})",
            total_count, player.player_id, new_total);
    } else {
        // Create new inventory
        let new_inv = PlayerInventory {
            player_id: player.player_id,
            inventory_composition: composition,
            total_count,
            last_updated: ctx.timestamp,
        };

        ctx.db.player_inventory().insert(new_inv);
        log::info!("Created inventory for player {} with {} packets",
            player.player_id, total_count);
    }

    log::info!("=== DEBUG_ADD_TEST_PACKETS END ===");
    Ok(())
}

/// Helper function to split large transfer compositions into batches
/// Each batch has max 5 packets per frequency and max 30 packets total
fn create_transfer_batches(composition: &[WavePacketSample]) -> Vec<Vec<WavePacketSample>> {
    const MAX_PER_FREQUENCY: u32 = 5;
    const MAX_TOTAL_PER_BATCH: u32 = 30;

    let mut batches: Vec<Vec<WavePacketSample>> = Vec::new();
    let mut current_batch: Vec<WavePacketSample> = Vec::new();
    let mut current_batch_total: u32 = 0;
    let mut freq_count_in_batch: std::collections::HashMap<i32, u32> = std::collections::HashMap::new();

    for sample in composition {
        let mut remaining = sample.count;

        while remaining > 0 {
            // Check how much of this frequency is already in the current batch
            let freq_int = (sample.frequency * 100.0).round() as i32;
            let freq_in_batch = freq_count_in_batch.get(&freq_int).copied().unwrap_or(0);
            
            // Calculate how much we can add (respecting per-frequency limit)
            let can_add_by_frequency = MAX_PER_FREQUENCY.saturating_sub(freq_in_batch).min(remaining);
            let can_add_by_total = MAX_TOTAL_PER_BATCH - current_batch_total;
            let to_add = can_add_by_frequency.min(can_add_by_total);

            if to_add == 0 {
                batches.push(current_batch);
                current_batch = Vec::new();
                current_batch_total = 0;
                freq_count_in_batch.clear();
                continue;
            }

            current_batch.push(WavePacketSample {
                frequency: sample.frequency,
                amplitude: sample.amplitude,
                phase: sample.phase,
                count: to_add,
            });

            current_batch_total += to_add;
            *freq_count_in_batch.entry(freq_int).or_insert(0) += to_add;
            remaining -= to_add;
        }
    }

    if !current_batch.is_empty() {
        batches.push(current_batch);
    }

    batches
}

/// Initiate energy packet transfer from player to storage device
/// Routes through nearest energy spires
/// AUTO-BATCHES large requests: max 5 per frequency, 30 total per batch
#[spacetimedb::reducer]
pub fn initiate_transfer(ctx: &ReducerContext, composition: Vec<WavePacketSample>, destination_device_id: u64) -> Result<(), String> {
    log::info!("=== INITIATE_TRANSFER START ===");
    log::info!("Composition: {:?}, Destination: {}", composition, destination_device_id);

    // Calculate total for logging
    let total_requested: u32 = composition.iter().map(|s| s.count).sum();

    // AUTO-BATCH: Split large requests into multiple transfers
    let batches = create_transfer_batches(&composition);
    log::info!("Total packets: {}, split into {} batches", total_requested, batches.len());

    // Get player
        let player = ctx.db.player()
            .identity()
            .find(&ctx.sender)
            .ok_or("Player not found")?;

        // Get storage device
        let storage = ctx.db.storage_device()
            .device_id()
            .find(&destination_device_id)
            .ok_or("Storage device not found")?;

        // Verify ownership
        if storage.owner_player_id != player.player_id {
            return Err("Not your storage device".to_string());
        }

        // Process each batch as a separate transfer
        let mut transfers_created = 0u32;
        for (batch_index, batch_composition) in batches.iter().enumerate() {
            let batch_total: u32 = batch_composition.iter().map(|s| s.count).sum();
            log::info!("Processing batch {}/{}: {} packets", batch_index + 1, batches.len(), batch_total);

            // Check inventory has enough of each frequency in this batch
            let inventory = ctx.db.player_inventory()
                .player_id()
                .find(&player.player_id)
                .ok_or("Player inventory not found")?;

            for sample in batch_composition {
                let freq_int = (sample.frequency * 100.0).round() as i32;
                let mut found = false;
                for inv_sample in &inventory.inventory_composition {
                    let inv_freq_int = (inv_sample.frequency * 100.0).round() as i32;
                    if inv_freq_int == freq_int {
                        if inv_sample.count < sample.count {
                            return Err(format!("Insufficient inventory for frequency {}: have {}, need {}", 
                                sample.frequency, inv_sample.count, sample.count));
                        }
                        found = true;
                        break;
                    }
                }
                if !found {
                    return Err(format!("Frequency {} not found in inventory", sample.frequency));
                }
            }

            // Check storage capacity
            let mut storage_totals: std::collections::HashMap<i32, u32> = std::collections::HashMap::new();
            for sample in &storage.stored_composition {
                let freq_int = (sample.frequency * 100.0).round() as i32;
                *storage_totals.entry(freq_int).or_insert(0) += sample.count;
            }
            for sample in batch_composition {
                let freq_int = (sample.frequency * 100.0).round() as i32;
                let current = storage_totals.get(&freq_int).copied().unwrap_or(0);
                if current + sample.count > storage.capacity_per_frequency {
                    return Err(format!("Storage full for frequency {}: capacity {}, current {}, transfer {}", 
                        sample.frequency, storage.capacity_per_frequency, current, sample.count));
                }
            }

            // Find nearest spires
            let player_spire = find_nearest_spire(ctx, player.current_world, player.position)?;
            let storage_spire = find_nearest_spire(ctx, storage.world_coords, storage.position)?;

            // Build route
            let mut waypoints = vec![player.position.clone()];
            let mut spire_ids = vec![player_spire.sphere_id];

            waypoints.push(player_spire.sphere_position.clone());

            // If different spires, add second hop
            if player_spire.sphere_id != storage_spire.sphere_id {
                waypoints.push(storage_spire.sphere_position.clone());
                spire_ids.push(storage_spire.sphere_id);
            }

            waypoints.push(storage.position.clone());

            // Deduct from inventory
            deduct_composition_from_inventory(ctx, player.player_id, &batch_composition)?;

            // Create transfer record in pending state (will be departed by two_second_pulse)
            let transfer = PacketTransfer {
                transfer_id: 0,
                player_id: player.player_id,
                composition: batch_composition.clone(),
                packet_count: batch_total,
                route_waypoints: waypoints.clone(),
                route_spire_ids: spire_ids.clone(),
                destination_device_id,
                initiated_at: ctx.timestamp,
                completed: false,
                current_leg: 0,
                leg_start_time: ctx.timestamp,
                state: "PlayerPulse".to_string(),
                source_object_type: "Player".to_string(),
                source_object_id: player.player_id,
                destination_object_type: "StorageDevice".to_string(),
                destination_object_id: destination_device_id,
                current_leg_type: "PendingAtObject".to_string(),
                predicted_arrival_time: Timestamp::UNIX_EPOCH,
            };

            ctx.db.packet_transfer().insert(transfer);
            transfers_created += 1;

            log::info!("Batch {} transfer created: {} packets routed through {} spires",
                batch_index + 1, batch_total, spire_ids.len());
        }

        log::info!("Transfer complete: {} total packets in {} transfer records", total_requested, transfers_created);
        log::info!("=== INITIATE_TRANSFER END ===");

        Ok(())
}

/// Complete energy packet transfer
/// Charges spires and adds packets to storage
#[spacetimedb::reducer]
pub fn complete_transfer(ctx: &ReducerContext, transfer_id: u64) -> Result<(), String> {
    log::info!("=== COMPLETE_TRANSFER START ===");
    log::info!("Transfer ID: {}", transfer_id);

    // Get transfer
    let transfer = ctx.db.packet_transfer()
        .transfer_id()
        .find(&transfer_id)
        .ok_or("Transfer not found")?;

    if transfer.completed {
        return Err("Transfer already completed".to_string());
    }

    // Update each sphere in the route
    for sphere_id in &transfer.route_spire_ids {
        let sphere = ctx.db.distribution_sphere()
            .sphere_id()
            .find(sphere_id)
            .ok_or(format!("Distribution sphere {} not found", sphere_id))?;

        // Update sphere statistics
        let mut updated_sphere = sphere.clone();
        updated_sphere.packets_routed += transfer.packet_count as u64;
        updated_sphere.last_packet_time = ctx.timestamp;

        ctx.db.distribution_sphere().delete(sphere);
        ctx.db.distribution_sphere().insert(updated_sphere.clone());

        // Find and update corresponding quantum tunnel
        let tunnel = ctx.db.quantum_tunnel()
            .iter()
            .find(|t| t.world_coords == updated_sphere.world_coords &&
                     t.cardinal_direction == updated_sphere.cardinal_direction)
            .ok_or(format!("Quantum tunnel not found for sphere {}", sphere_id))?;

        let mut updated_tunnel = tunnel.clone();
        updated_tunnel.ring_charge = (updated_tunnel.ring_charge + 1.0).min(100.0);
        let new_charge = updated_tunnel.ring_charge;

        // Update tunnel status based on charge
        if updated_tunnel.ring_charge >= 100.0 && updated_tunnel.tunnel_status == "Inactive" {
            updated_tunnel.tunnel_status = "Charging".to_string();
        }

        ctx.db.quantum_tunnel().delete(tunnel);
        ctx.db.quantum_tunnel().insert(updated_tunnel);

        log::info!("Routed {} packets through sphere {}: tunnel charge now {}",
            transfer.packet_count, sphere_id, new_charge);
    }

    // Add to storage
    let storage = ctx.db.storage_device()
        .device_id()
        .find(&transfer.destination_device_id)
        .ok_or("Storage device not found")?;

    let mut updated_storage = storage.clone();
    
    // Add packets to storage composition
    for sample in &transfer.composition {
        let mut found = false;
        for existing in &mut updated_storage.stored_composition {
            if (existing.frequency - sample.frequency).abs() < 0.01 {
                existing.count += sample.count;
                found = true;
                break;
            }
        }
        if !found {
            updated_storage.stored_composition.push(sample.clone());
        }
    }

    ctx.db.storage_device().delete(storage);
    ctx.db.storage_device().insert(updated_storage);

    // Mark transfer complete
    let mut updated_transfer = transfer.clone();
    updated_transfer.completed = true;
    let packet_count = updated_transfer.packet_count;
    let device_id = updated_transfer.destination_device_id;

    ctx.db.packet_transfer().delete(transfer.clone());
    ctx.db.packet_transfer().insert(updated_transfer);

    log::info!("Transfer complete: {} packets added to storage {}", packet_count, device_id);
    log::info!("=== COMPLETE_TRANSFER END ===");

    Ok(())
}


/// Tick player transfer pulses (2-second intervals)
/// Moves packets from player to first sphere
#[spacetimedb::reducer]
pub fn tick_player_transfers(ctx: &ReducerContext) -> Result<(), String> {
    let now = ctx.timestamp;
    let two_seconds = std::time::Duration::from_secs(2);
    
    for transfer in ctx.db.packet_transfer().iter() {
        if transfer.completed {
            continue;
        }
        
        // Only handle PlayerPulse state
        if transfer.state != "PlayerPulse" {
            continue;
        }
        
        // Check if 2 seconds have elapsed
        let elapsed = now.duration_since(transfer.leg_start_time);
        if elapsed < Some(two_seconds) {
            continue;
        }
        
        // Move packets from player to first sphere
        let first_sphere_id = transfer.route_spire_ids[0];
        let sphere = ctx.db.distribution_sphere()
            .sphere_id()
            .find(&first_sphere_id)
            .ok_or("First sphere not found")?;
        
        // Add packets to sphere's transit buffer
        let mut updated_sphere = sphere.clone();
        for sample in &transfer.composition {
            // Check if frequency already exists in buffer
            let mut found = false;
            for existing in &mut updated_sphere.transit_buffer {
                if (existing.frequency - sample.frequency).abs() < 0.01 {
                    existing.count += sample.count;
                    found = true;
                    break;
                }
            }
            if !found {
                updated_sphere.transit_buffer.push(sample.clone());
            }
        }
        updated_sphere.packets_routed += transfer.packet_count as u64;
        updated_sphere.last_packet_time = now;
        
        ctx.db.distribution_sphere().delete(sphere);
        ctx.db.distribution_sphere().insert(updated_sphere);
        
        // Update transfer state
        let mut updated_transfer = transfer.clone();
        updated_transfer.current_leg = 1;
        updated_transfer.state = "InTransit".to_string();
        updated_transfer.leg_start_time = now;
        
        let transfer_id_for_log = transfer.transfer_id;
        ctx.db.packet_transfer().delete(transfer.clone());
        ctx.db.packet_transfer().insert(updated_transfer);
        
        log::info!("Player transfer {} moved to sphere {} (InTransit)", transfer_id_for_log, first_sphere_id);
    }
    
    Ok(())
}
/// TESTING: Add packets to player inventory

/// World sphere pulse (1-second synchronized pulse for all spheres in a world)
/// Moves packets between spheres and to final storage
#[spacetimedb::reducer]
pub fn world_sphere_pulse(ctx: &ReducerContext, world_x: i32, world_y: i32, world_z: i32) -> Result<(), String> {
    let world_coords = WorldCoords { x: world_x, y: world_y, z: world_z };
    let now = ctx.timestamp;
    
    // Collect all transfers in this world that are in InTransit state
    let mut active_transfers: Vec<PacketTransfer> = Vec::new();
    for transfer in ctx.db.packet_transfer().iter() {
        if transfer.state == "InTransit" && !transfer.completed {
            // Check if transfer involves this world
            let player = ctx.db.player()
                .player_id()
                .find(&transfer.player_id);
            if let Some(p) = player {
                if p.current_world == world_coords {
                    active_transfers.push(transfer.clone());
                }
            }
        }
    }
    
    // Process each active transfer
    for transfer in &active_transfers {
        let current_leg = transfer.current_leg as usize;
        
        // Check if we're at the final leg (sphere to storage)
        if current_leg >= transfer.route_spire_ids.len() {
            // Move to storage device
            let storage = ctx.db.storage_device()
                .device_id()
                .find(&transfer.destination_device_id);
            
            if let Some(storage) = storage {
                let mut updated_storage = storage.clone();
                
                // Add packets to storage composition
                for sample in &transfer.composition {
                    let mut found = false;
                    for existing in &mut updated_storage.stored_composition {
                        if (existing.frequency - sample.frequency).abs() < 0.01 {
                            existing.count += sample.count;
                            found = true;
                            break;
                        }
                    }
                    if !found {
                        updated_storage.stored_composition.push(sample.clone());
                    }
                }
                
                ctx.db.storage_device().delete(storage);
                ctx.db.storage_device().insert(updated_storage);
                
                // Mark transfer complete
                let mut updated_transfer = transfer.clone();
                updated_transfer.completed = true;
                updated_transfer.state = "Completed".to_string();
                
                let transfer_id_log = transfer.transfer_id;
                let device_id_log = transfer.destination_device_id;
                ctx.db.packet_transfer().delete(transfer.clone());
                ctx.db.packet_transfer().insert(updated_transfer);
                
                log::info!("Transfer {} completed - packets delivered to storage {}", 
                    transfer_id_log, device_id_log);
            }
        } else {
            // Move to next sphere
            let next_sphere_id = transfer.route_spire_ids[current_leg];
            let sphere = ctx.db.distribution_sphere()
                .sphere_id()
                .find(&next_sphere_id);
            
            if let Some(sphere) = sphere {
                let mut updated_sphere = sphere.clone();
                
                // Add packets to sphere's transit buffer
                for sample in &transfer.composition {
                    let mut found = false;
                    for existing in &mut updated_sphere.transit_buffer {
                        if (existing.frequency - sample.frequency).abs() < 0.01 {
                            existing.count += sample.count;
                            found = true;
                            break;
                        }
                    }
                    if !found {
                        updated_sphere.transit_buffer.push(sample.clone());
                    }
                }
                updated_sphere.packets_routed += transfer.packet_count as u64;
                updated_sphere.last_packet_time = now;
                
                ctx.db.distribution_sphere().delete(sphere);
                ctx.db.distribution_sphere().insert(updated_sphere);
                
                // Update transfer to next leg
                let mut updated_transfer = transfer.clone();
                updated_transfer.current_leg += 1;
                updated_transfer.leg_start_time = now;
                
                let transfer_id_advance = transfer.transfer_id;
                ctx.db.packet_transfer().delete(transfer.clone());
                ctx.db.packet_transfer().insert(updated_transfer);
                
                log::info!("Transfer {} advanced to leg {} (sphere {})", 
                    transfer_id_advance, current_leg + 1, next_sphere_id);
            }
        }
    }
    
    // Pulse visualization event (all spheres flash together)
    log::info!("World sphere pulse for world ({}, {}, {}) - processed {} transfers", 
        world_x, world_y, world_z, active_transfers.len());
    
    Ok(())
}
/// Debug reducer for testing transfer system
#[spacetimedb::reducer]
pub fn add_test_inventory(
    ctx: &ReducerContext,
    player_id: u64,
    red: u32,
    yellow: u32,
    green: u32,
    cyan: u32,
    blue: u32,
    magenta: u32
) -> Result<(), String> {
    log::info!("=== ADD_TEST_INVENTORY START ===");

    let total = red + yellow + green + cyan + blue + magenta;

    if total > 300 {
        return Err(format!("Total exceeds max inventory (300): {}", total));
    }

    // Build composition from counts
    let mut composition = Vec::new();
    
    if red > 0 {
        composition.push(WavePacketSample {
            frequency: 0.0,
            amplitude: 1.0,
            phase: 0.0,
            count: red,
        });
    }
    if yellow > 0 {
        composition.push(WavePacketSample {
            frequency: 1.047,
            amplitude: 1.0,
            phase: 0.0,
            count: yellow,
        });
    }
    if green > 0 {
        composition.push(WavePacketSample {
            frequency: 2.094,
            amplitude: 1.0,
            phase: 0.0,
            count: green,
        });
    }
    if cyan > 0 {
        composition.push(WavePacketSample {
            frequency: 3.142,
            amplitude: 1.0,
            phase: 0.0,
            count: cyan,
        });
    }
    if blue > 0 {
        composition.push(WavePacketSample {
            frequency: 4.189,
            amplitude: 1.0,
            phase: 0.0,
            count: blue,
        });
    }
    if magenta > 0 {
        composition.push(WavePacketSample {
            frequency: 5.236,
            amplitude: 1.0,
            phase: 0.0,
            count: magenta,
        });
    }

    // Check if inventory exists
    let existing = ctx.db.player_inventory().player_id().find(&player_id);

    if let Some(inv) = existing {
        // Update existing
        let updated = PlayerInventory {
            player_id,
            inventory_composition: composition,
            total_count: total,
            last_updated: ctx.timestamp,
        };

        ctx.db.player_inventory().delete(inv);
        ctx.db.player_inventory().insert(updated);
    } else {
        // Create new
        let inventory = PlayerInventory {
            player_id,
            inventory_composition: composition,
            total_count: total,
            last_updated: ctx.timestamp,
        };

        ctx.db.player_inventory().insert(inventory);
    }

    log::info!("Set inventory for player {}: R={} Y={} G={} C={} B={} M={} (Total: {})",
        player_id, red, yellow, green, cyan, blue, magenta, total);
    log::info!("=== ADD_TEST_INVENTORY END ===");

    Ok(())
}

/// Create storage device for player
/// Limited to 10 devices per player
#[spacetimedb::reducer]
pub fn create_storage_device(ctx: &ReducerContext, x: f32, y: f32, z: f32, device_name: String) -> Result<(), String> {
    log::info!("=== CREATE_STORAGE_DEVICE START ===");

    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;

    // Check 10 device limit
    let mut device_count = 0;
    for device in ctx.db.storage_device().iter() {
        if device.owner_player_id == player.player_id {
            device_count += 1;
        }
    }
    if device_count >= 10 {
        return Err("Cannot create more than 10 storage devices per player".to_string());
    }

    let device = StorageDevice {
        device_id: 0, // auto_inc
        owner_player_id: player.player_id,
        world_coords: player.current_world,
        position: DbVector3 { x, y, z },
        device_name,
        capacity_per_frequency: 1000,  // 1000 per frequency, 6000 total
        stored_composition: Vec::new(),  // Empty on creation
        created_at: ctx.timestamp,
    };

    ctx.db.storage_device().insert(device);

    log::info!("Created storage device at ({}, {}, {}) for player {} (total devices: {})", 
        x, y, z, player.player_id, device_count + 1);
    log::info!("=== CREATE_STORAGE_DEVICE END ===");

    Ok(())
}

/// TESTING: Create energy spire for testing
#[spacetimedb::reducer]
pub fn create_energy_spire(
    ctx: &ReducerContext,
    world_x: i32,
    world_y: i32,
    world_z: i32,
    pos_x: f32,
    pos_y: f32,
    pos_z: f32,
    direction: String
) -> Result<(), String> {
    log::info!("=== CREATE_ENERGY_SPIRE START ===");

    let sphere = DistributionSphere {
        sphere_id: 0, // auto_inc
        world_coords: WorldCoords { x: world_x, y: world_y, z: world_z },
        cardinal_direction: direction,
        sphere_position: DbVector3 { x: pos_x, y: pos_y, z: pos_z },
        sphere_radius: 40,
        packets_routed: 0,
        last_packet_time: ctx.timestamp,
        transit_buffer: Vec::new(),
    };

    ctx.db.distribution_sphere().insert(sphere);

    log::info!("Created energy spire at ({}, {}, {}) on world ({}, {}, {})",
        pos_x, pos_y, pos_z, world_x, world_y, world_z);
    log::info!("=== CREATE_ENERGY_SPIRE END ===");

    Ok(())
}

// ============================================================================
// Energy Spire System - Helper Functions
// ============================================================================

/// Calculate cardinal direction position on world sphere
/// World radius R = 300 units (from CLAUDE.md spec)
fn get_cardinal_position(direction: &str) -> DbVector3 {
    const R: f32 = 300.0;
    match direction {
        "North" => DbVector3 { x: 0.0, y: R, z: 0.0 },          // +Y (north pole)
        "South" => DbVector3 { x: 0.0, y: -R, z: 0.0 },         // -Y (south pole)
        "East" => DbVector3 { x: R, y: 0.0, z: 0.0 },           // +X (east)
        "West" => DbVector3 { x: -R, y: 0.0, z: 0.0 },          // -X (west)
        "Forward" => DbVector3 { x: 0.0, y: 0.0, z: R },        // +Z (forward)
        "Back" => DbVector3 { x: 0.0, y: 0.0, z: -R },          // -Z (back)
        _ => DbVector3 { x: 0.0, y: R, z: 0.0 }, // Default to North
    }
}

/// Get tunnel color based on cardinal direction (tier-based)
fn get_tunnel_color(direction: &str) -> String {
    match direction {
        // Cardinal (6) - Primary colors
        "North" | "South" => "Green".to_string(),    // ±Y axis (Green tunnels)
        "East" | "West" => "Red".to_string(),        // ±X axis (Red tunnels)
        "Forward" | "Back" => "Blue".to_string(),    // ±Z axis (Blue tunnels)

        // Edge centers (12) - Secondary colors
        "NorthEast" | "NorthWest" | "SouthEast" | "SouthWest" => "Yellow".to_string(),  // XY plane
        "NorthForward" | "NorthBack" | "SouthForward" | "SouthBack" => "Cyan".to_string(),  // YZ plane
        "EastForward" | "EastBack" | "WestForward" | "WestBack" => "Magenta".to_string(),  // XZ plane

        // Vertex corners (8) - White
        "NorthEastForward" | "NorthEastBack" | "NorthWestForward" | "NorthWestBack" |
        "SouthEastForward" | "SouthEastBack" | "SouthWestForward" | "SouthWestBack" => "White".to_string(),

        _ => "Grey".to_string(),
    }
}

// ============================================================================
// Energy Spire System - Reducers
// ============================================================================

/// Spawn main 6 energy spires (N/S/E/W/Forward/Back) for a world
/// Creates DistributionSphere + QuantumTunnel for each cardinal direction
#[spacetimedb::reducer]
pub fn spawn_main_spires(
    ctx: &ReducerContext,
    world_x: i32,
    world_y: i32,
    world_z: i32
) -> Result<(), String> {
    log::info!("=== SPAWN_MAIN_SPIRES START ===");
    log::info!("World: ({}, {}, {})", world_x, world_y, world_z);

    let world_coords = WorldCoords { x: world_x, y: world_y, z: world_z };
    let directions = vec!["North", "South", "East", "West", "Forward", "Back"];

    for direction in directions {
        let position = get_cardinal_position(direction);
        let color = get_tunnel_color(direction);

        // Create DistributionSphere
        let sphere = DistributionSphere {
            sphere_id: 0, // auto_inc
            world_coords: world_coords.clone(),
            cardinal_direction: direction.to_string(),
            sphere_position: position.clone(),
            sphere_radius: 40,
            packets_routed: 0,
            last_packet_time: ctx.timestamp,
            transit_buffer: Vec::new(),
        };
        ctx.db.distribution_sphere().insert(sphere);

        // Create QuantumTunnel
        let tunnel = QuantumTunnel {
            tunnel_id: 0, // auto_inc
            world_coords: world_coords.clone(),
            cardinal_direction: direction.to_string(),
            ring_charge: 0.0,
            tunnel_status: "Inactive".to_string(),
            connected_to_world: None,
            connected_to_sphere_id: None,
            tunnel_color: color.clone(),
            formed_at: None,
        };
        ctx.db.quantum_tunnel().insert(tunnel);

        log::info!("Created spire: {} at ({}, {}, {}) - Color: {}",
            direction, position.x, position.y, position.z, color);
    }

    log::info!("=== SPAWN_MAIN_SPIRES END ===");
    Ok(())
}

/// Spawn a single circuit at a spire location (optional component)
/// Circuits emit orbs for mining
#[spacetimedb::reducer]
pub fn spawn_circuit_at_spire(
    ctx: &ReducerContext,
    world_x: i32,
    world_y: i32,
    world_z: i32,
    cardinal_direction: String,
    circuit_type: String,
    qubit_count: u8,
    sources_per_emission: u32,
    emission_interval_ms: u64
) -> Result<(), String> {
    log::info!("=== SPAWN_CIRCUIT_AT_SPIRE START ===");

    let world_coords = WorldCoords { x: world_x, y: world_y, z: world_z };

    // Verify that a DistributionSphere exists at this location
    let sphere_exists = ctx.db.distribution_sphere()
        .iter()
        .any(|s| s.world_coords == world_coords && s.cardinal_direction == cardinal_direction);

    if !sphere_exists {
        return Err(format!("No distribution sphere at {} on world ({},{},{}). Spawn spires first.",
            cardinal_direction, world_x, world_y, world_z));
    }

    // Create the circuit
    let circuit = WorldCircuit {
        circuit_id: 0, // auto_inc
        world_coords: world_coords.clone(),
        cardinal_direction: cardinal_direction.clone(),
        circuit_type,
        qubit_count,
        sources_per_emission,
        emission_interval_ms,
        last_emission_time: 0, // Not yet emitted
    };

    ctx.db.world_circuit().insert(circuit);

    log::info!("Created circuit at {} on world ({},{},{})", cardinal_direction, world_x, world_y, world_z);
    log::info!("=== SPAWN_CIRCUIT_AT_SPIRE END ===");

    Ok(())
}

/// Spawn circuits at the 6 main cardinal directions (North, South, East, West, Forward, Back)
/// Creates WorldCircuit components at each cardinal spire location
#[spacetimedb::reducer]
pub fn spawn_6_cardinal_circuits(
    ctx: &ReducerContext,
    world_x: i32,
    world_y: i32,
    world_z: i32
) -> Result<(), String> {
    log::info!("=== SPAWN_6_CARDINAL_CIRCUITS START ===");
    log::info!("World: ({}, {}, {})", world_x, world_y, world_z);

    let world_coords = WorldCoords { x: world_x, y: world_y, z: world_z };

    // 6 cardinal directions (face centers)
    let cardinal_directions = vec![
        "North",
        "South",
        "East",
        "West",
        "Forward",
        "Back",
    ];

    for direction in cardinal_directions {
        // Verify that a DistributionSphere exists at this location
        let sphere_exists = ctx.db.distribution_sphere()
            .iter()
            .any(|s| s.world_coords == world_coords && s.cardinal_direction == direction);

        if !sphere_exists {
            log::warn!("No distribution sphere at {} on world ({},{},{}). Skipping circuit creation.",
                direction, world_x, world_y, world_z);
            continue;
        }

        // Only North (|0⟩) emits sources for now - others are 0
        let sources = if direction == "North" { 1 } else { 0 };

        // Create the circuit with default values
        let circuit = WorldCircuit {
            circuit_id: 0, // auto_inc
            world_coords: world_coords.clone(),
            cardinal_direction: direction.to_string(),
            circuit_type: "Basic".to_string(),
            qubit_count: 1,
            sources_per_emission: sources,
            emission_interval_ms: 10000, // Every 10 seconds
            last_emission_time: 0, // Not yet emitted
        };

        ctx.db.world_circuit().insert(circuit);

        log::info!("Created circuit at {} on world ({},{},{}) - {} sources per emission",
            direction, world_x, world_y, world_z, sources);
    }

    log::info!("=== SPAWN_6_CARDINAL_CIRCUITS END - Created 6 circuits ===");
    Ok(())
}



/// Spawn all 26 energy spires (FCC lattice) for a world
/// Creates DistributionSphere + QuantumTunnel for each position
/// 6 cardinal + 12 edge + 8 vertex = 26 total
#[spacetimedb::reducer]
pub fn spawn_all_26_spires(
    ctx: &ReducerContext,
    world_x: i32,
    world_y: i32,
    world_z: i32
) -> Result<(), String> {
    log::info!("=== SPAWN_ALL_26_SPIRES START ===");
    log::info!("World: ({}, {}, {})", world_x, world_y, world_z);

    let world_coords = WorldCoords { x: world_x, y: world_y, z: world_z };
    const R: f32 = 300.0;
    const SQRT2: f32 = 1.414213562373095;
    const SQRT3: f32 = 1.732050807568877;

    // All 26 spire positions with their names
    let all_spires = vec![
        // 6 Cardinal (face centers)
        ("North", 0.0, R, 0.0, "Green"),
        ("South", 0.0, -R, 0.0, "Green"),
        ("East", R, 0.0, 0.0, "Red"),
        ("West", -R, 0.0, 0.0, "Red"),
        ("Forward", 0.0, 0.0, R, "Blue"),
        ("Back", 0.0, 0.0, -R, "Blue"),
        
        // 12 Edge centers (between two cardinals)
        ("NorthEast", R/SQRT2, R/SQRT2, 0.0, "Yellow"),
        ("NorthWest", -R/SQRT2, R/SQRT2, 0.0, "Yellow"),
        ("SouthEast", R/SQRT2, -R/SQRT2, 0.0, "Yellow"),
        ("SouthWest", -R/SQRT2, -R/SQRT2, 0.0, "Yellow"),
        ("NorthForward", 0.0, R/SQRT2, R/SQRT2, "Cyan"),
        ("NorthBack", 0.0, R/SQRT2, -R/SQRT2, "Cyan"),
        ("SouthForward", 0.0, -R/SQRT2, R/SQRT2, "Cyan"),
        ("SouthBack", 0.0, -R/SQRT2, -R/SQRT2, "Cyan"),
        ("EastForward", R/SQRT2, 0.0, R/SQRT2, "Magenta"),
        ("EastBack", R/SQRT2, 0.0, -R/SQRT2, "Magenta"),
        ("WestForward", -R/SQRT2, 0.0, R/SQRT2, "Magenta"),
        ("WestBack", -R/SQRT2, 0.0, -R/SQRT2, "Magenta"),
        
        // 8 Vertex (corners - between three cardinals)
        ("NorthEastForward", R/SQRT3, R/SQRT3, R/SQRT3, "White"),
        ("NorthEastBack", R/SQRT3, R/SQRT3, -R/SQRT3, "White"),
        ("NorthWestForward", -R/SQRT3, R/SQRT3, R/SQRT3, "White"),
        ("NorthWestBack", -R/SQRT3, R/SQRT3, -R/SQRT3, "White"),
        ("SouthEastForward", R/SQRT3, -R/SQRT3, R/SQRT3, "White"),
        ("SouthEastBack", R/SQRT3, -R/SQRT3, -R/SQRT3, "White"),
        ("SouthWestForward", -R/SQRT3, -R/SQRT3, R/SQRT3, "White"),
        ("SouthWestBack", -R/SQRT3, -R/SQRT3, -R/SQRT3, "White"),
    ];

    // Height constants for energy infrastructure
    const DISTRIBUTION_SPHERE_HEIGHT: f32 = 10.0;  // Distribution spheres at height 10
    const QUANTUM_TUNNEL_HEIGHT: f32 = 20.0;        // Quantum tunnels at height 20 (client-side positioning)

    for (direction, x, y, z, color) in all_spires {
        // Calculate surface normal (normalized direction from world center)
        let length = (x * x + y * y + z * z).sqrt();
        let normal_x = x / length;
        let normal_y = y / length;
        let normal_z = z / length;

        // Distribution sphere position: surface + 10 units along normal
        let sphere_position = DbVector3 {
            x: x + normal_x * DISTRIBUTION_SPHERE_HEIGHT,
            y: y + normal_y * DISTRIBUTION_SPHERE_HEIGHT,
            z: z + normal_z * DISTRIBUTION_SPHERE_HEIGHT,
        };

        // Create DistributionSphere at height 10
        let sphere = DistributionSphere {
            sphere_id: 0, // auto_inc
            world_coords: world_coords.clone(),
            cardinal_direction: direction.to_string(),
            sphere_position,
            sphere_radius: 40,
            packets_routed: 0,
            last_packet_time: ctx.timestamp,
            transit_buffer: Vec::new(),  // Start with empty buffer
        };
        ctx.db.distribution_sphere().insert(sphere);

        // Create QuantumTunnel
        let tunnel = QuantumTunnel {
            tunnel_id: 0, // auto_inc
            world_coords: world_coords.clone(),
            cardinal_direction: direction.to_string(),
            ring_charge: 0.0,
            tunnel_status: "Inactive".to_string(),
            connected_to_world: None,
            connected_to_sphere_id: None,
            tunnel_color: color.to_string(),
            formed_at: None,
        };
        ctx.db.quantum_tunnel().insert(tunnel);

        log::info!("Created spire: {} - DistributionSphere at height 10: ({:.2}, {:.2}, {:.2}) - Color: {}",
            direction, sphere_position.x, sphere_position.y, sphere_position.z, color);
    }

    log::info!("=== SPAWN_ALL_26_SPIRES END - Created 26 spires (spheres at R+10, tunnels at R+20) ===");
    Ok(())
}

#[spacetimedb::reducer]
pub fn debug_create_storage_device(
    ctx: &ReducerContext,
    player_id: u64,
    x: f32,
    y: f32,
    z: f32,
    device_name: String
) -> Result<(), String> {
    log::info!("=== DEBUG_CREATE_STORAGE_DEVICE START ===");
    log::info!("Creating storage device for player {}: {} at ({}, {}, {})", 
        player_id, device_name, x, y, z);

    // Verify player exists
    let player = ctx.db.player()
        .player_id()
        .find(&player_id)
        .ok_or("Player not found")?;

    let device = StorageDevice {
        device_id: 0, // auto_inc
        owner_player_id: player_id,
        world_coords: player.current_world,
        position: DbVector3 { x, y, z },
        device_name,
        capacity_per_frequency: 1000,  // Default 1000 per frequency, 6000 total
        stored_composition: Vec::new(),
        created_at: ctx.timestamp,
    };

    ctx.db.storage_device().insert(device);

    log::info!("Storage device created successfully");
    Ok(())
}
// ============================================================================
// Game Loop System
// ============================================================================

/// Game loop schedule table for automatic tick execution
#[spacetimedb::table(name = game_loop_schedule, public, scheduled(game_loop))]
#[derive(Debug, Clone)]
pub struct GameLoopSchedule {
    #[primary_key]
    #[auto_inc]
    pub scheduled_id: u64,
    pub scheduled_at: ScheduleAt,
}

/// Game tick counter for multi-clock timing
#[spacetimedb::table(name = game_tick_counter, public)]
#[derive(Debug, Clone)]
pub struct GameTickCounter {
    #[primary_key]
    pub id: u32,  // Always 0 for singleton counter
    pub tick_count: u64,
}

/// Main game loop reducer - runs at 10Hz (100ms intervals)
/// Implements multi-clock system:
/// - Every 100ms: Check arrivals via process_packet_transfers()
/// - Every 20 ticks (2 seconds): Object↔Sphere departures
/// - Every 100 ticks (10 seconds): Sphere↔Sphere departures
#[spacetimedb::reducer]
pub fn game_loop(ctx: &ReducerContext, _arg: GameLoopSchedule) -> Result<(), String> {
    // Get or initialize tick counter
    let tick_count = match ctx.db.game_tick_counter().id().find(&0) {
        Some(counter) => {
            let new_count = counter.tick_count + 1;
            let mut updated = counter.clone();
            updated.tick_count = new_count;
            ctx.db.game_tick_counter().delete(counter);
            ctx.db.game_tick_counter().insert(updated);
            new_count
        }
        None => {
            // Initialize counter
            ctx.db.game_tick_counter().insert(GameTickCounter {
                id: 0,
                tick_count: 1,
            });
            1
        }
    };

    // Process packet arrivals EVERY tick (100ms granularity for accurate arrival timing)
    process_packet_transfers(ctx)?;

    // Process source movement EVERY tick (smooth movement at 10Hz)
    process_source_movement(ctx);

    // Two-second pulse: Object↔Sphere departures (every 20 ticks)
    if tick_count % 20 == 0 {
        two_second_pulse(ctx)?;
    }

    // Ten-second pulse: Sphere↔Sphere departures (every 100 ticks)
    if tick_count % 100 == 0 {
        ten_second_pulse(ctx)?;
    }

    Ok(())
}

/// Start the game loop
#[spacetimedb::reducer]
pub fn start_game_loop(ctx: &ReducerContext) -> Result<(), String> {
    // Check if game loop is already running
    if ctx.db.game_loop_schedule().iter().next().is_some() {
        return Err("Game loop is already running".to_string());
    }

    // Initialize schedule
    ctx.db.game_loop_schedule().insert(GameLoopSchedule {
        scheduled_id: 0, // auto_inc will assign
        scheduled_at: ScheduleAt::Interval(Duration::from_millis(100).into()),
    });

    log::info!("Game loop started at 10Hz (100ms intervals)");
    Ok(())
}

/// Stop the game loop
#[spacetimedb::reducer]
pub fn stop_game_loop(ctx: &ReducerContext) -> Result<(), String> {
    let schedules: Vec<GameLoopSchedule> = ctx.db.game_loop_schedule().iter().collect();
    for schedule in schedules {
        ctx.db.game_loop_schedule().delete(schedule);
    }
    log::info!("Game loop stopped");
    Ok(())
}

// ============================================================================
// Packet Transfer Processing
// ============================================================================

/// Process all packet transfers - check for arrivals based on predicted_arrival_time
/// Runs every 100ms to catch arrivals with high precision
fn process_packet_transfers(ctx: &ReducerContext) -> Result<(), String> {
    let now = ctx.timestamp;

    for transfer in ctx.db.packet_transfer().iter() {
        if transfer.completed {
            continue;
        }

        // Check if packet has arrived at destination
        if now < transfer.predicted_arrival_time {
            continue;
        }

        // Route to appropriate arrival handler based on current leg type
        match transfer.current_leg_type.as_str() {
            "PendingAtObject" => {
                // Not yet departed, waiting for two_second_pulse
                continue;
            }
            "ObjectToSphere" => process_object_to_sphere_arrival(ctx, &transfer)?,
            "SphereToSphere" => process_sphere_to_sphere_arrival(ctx, &transfer)?,
            "SphereToObject" => process_sphere_to_object_arrival(ctx, &transfer)?,
            "ArrivedAtSphere" => {
                // Already arrived, waiting for pulse
                continue;
            }
            _ => {
                log::warn!("[Transfer] Unknown leg type '{}' for transfer {}",
                    transfer.current_leg_type, transfer.transfer_id);
            }
        }
    }

    Ok(())
}

/// Handle Object→Sphere ARRIVALS (adds to sphere buffer, doesn't advance leg)
fn process_object_to_sphere_arrival(ctx: &ReducerContext, transfer: &PacketTransfer) -> Result<(), String> {
    let now = ctx.timestamp;

    // Get first sphere in route
    let sphere_id = transfer.route_spire_ids[0];
    let sphere = ctx.db.distribution_sphere()
        .sphere_id()
        .find(&sphere_id)
        .ok_or(format!("First sphere {} not found", sphere_id))?;

    // Add packets to sphere's transit buffer
    let mut updated_sphere = sphere.clone();
    add_to_buffer(&mut updated_sphere.transit_buffer, &transfer.composition);
    updated_sphere.packets_routed += transfer.packet_count as u64;
    updated_sphere.last_packet_time = now;

    ctx.db.distribution_sphere().delete(sphere);
    ctx.db.distribution_sphere().insert(updated_sphere);

    // Mark transfer as arrived, waiting for departure pulse
    let mut updated_transfer = transfer.clone();
    updated_transfer.current_leg_type = "ArrivedAtSphere".to_string();
    updated_transfer.predicted_arrival_time = Timestamp::UNIX_EPOCH; // Clear arrival time

    ctx.db.packet_transfer().delete(transfer.clone());
    ctx.db.packet_transfer().insert(updated_transfer);

    log::info!("[Arrival] {} {} arrived at sphere {} - waiting for departure pulse",
        transfer.source_object_type, transfer.source_object_id, sphere_id);

    Ok(())
}

/// Handle Sphere→Sphere ARRIVALS (adds to sphere buffer, doesn't advance leg)
fn process_sphere_to_sphere_arrival(ctx: &ReducerContext, transfer: &PacketTransfer) -> Result<(), String> {
    let now = ctx.timestamp;
    let current_sphere_idx = transfer.current_leg as usize;

    if current_sphere_idx >= transfer.route_spire_ids.len() {
        return Err(format!("Invalid sphere index {} for transfer {}", current_sphere_idx, transfer.transfer_id));
    }

    // Get destination sphere for this leg
    let sphere_id = transfer.route_spire_ids[current_sphere_idx];
    let sphere = ctx.db.distribution_sphere()
        .sphere_id()
        .find(&sphere_id)
        .ok_or(format!("Sphere {} not found", sphere_id))?;

    // Add packets to sphere's transit buffer
    let mut updated_sphere = sphere.clone();
    add_to_buffer(&mut updated_sphere.transit_buffer, &transfer.composition);
    updated_sphere.packets_routed += transfer.packet_count as u64;
    updated_sphere.last_packet_time = now;

    ctx.db.distribution_sphere().delete(sphere);
    ctx.db.distribution_sphere().insert(updated_sphere);

    // Mark transfer as arrived, waiting for departure pulse
    let mut updated_transfer = transfer.clone();
    updated_transfer.current_leg_type = "ArrivedAtSphere".to_string();
    updated_transfer.predicted_arrival_time = Timestamp::UNIX_EPOCH; // Clear arrival time

    ctx.db.packet_transfer().delete(transfer.clone());
    ctx.db.packet_transfer().insert(updated_transfer);

    log::info!("[Arrival] Transfer {} arrived at sphere {} - waiting for departure pulse",
        transfer.transfer_id, sphere_id);

    Ok(())
}

/// Handle Sphere→Object ARRIVALS (final delivery to player, storage, miner, etc.)
fn process_sphere_to_object_arrival(ctx: &ReducerContext, transfer: &PacketTransfer) -> Result<(), String> {
    let now = ctx.timestamp;

    // Deliver to destination object based on type
    match transfer.destination_object_type.as_str() {
        "StorageDevice" => {
            let storage = ctx.db.storage_device()
                .device_id()
                .find(&transfer.destination_object_id)
                .ok_or(format!("StorageDevice {} not found", transfer.destination_object_id))?;

            let mut updated_storage = storage.clone();
            add_to_buffer(&mut updated_storage.stored_composition, &transfer.composition);

            ctx.db.storage_device().delete(storage);
            ctx.db.storage_device().insert(updated_storage);

            log::info!("[Arrival] Delivered {} packets to StorageDevice {}",
                transfer.packet_count, transfer.destination_object_id);
        }
        "Player" => {
            // TODO: Implement player inventory delivery when ready
            log::warn!("[Arrival] Player delivery not yet implemented for transfer {}", transfer.transfer_id);
        }
        "Miner" => {
            // TODO: Implement miner delivery when ready
            log::warn!("[Arrival] Miner delivery not yet implemented for transfer {}", transfer.transfer_id);
        }
        _ => {
            return Err(format!("Unknown destination object type: {}", transfer.destination_object_type));
        }
    }

    // Mark transfer as completed
    let mut completed_transfer = transfer.clone();
    completed_transfer.completed = true;
    completed_transfer.state = "Completed".to_string();
    completed_transfer.current_leg_type = "Completed".to_string();

    ctx.db.packet_transfer().delete(transfer.clone());
    ctx.db.packet_transfer().insert(completed_transfer);

    log::info!("[Arrival] Transfer {} completed - delivered to {} {}",
        transfer.transfer_id, transfer.destination_object_type, transfer.destination_object_id);

    Ok(())
}

// ============================================================================
// Pulse Functions (Departures)
// ============================================================================

/// Two-second pulse: Process Object→Sphere and Sphere→Object DEPARTURES
/// Called every 20 ticks (2 seconds)
fn two_second_pulse(ctx: &ReducerContext) -> Result<(), String> {
    let now = ctx.timestamp;

    log::info!("[2s Pulse] Processing Object→Sphere and Sphere→Object departures");

    // Process all transfers pending at source objects for Object→Sphere departure
    // LIMIT: Only one transfer per source object per pulse
    let mut departed_sources: std::collections::HashSet<(String, u64)> = std::collections::HashSet::new();
    
    for transfer in ctx.db.packet_transfer().iter() {
        if transfer.completed {
            continue;
        }

        if transfer.current_leg_type == "PendingAtObject" {
            // Check if this source has already departed a transfer this pulse
            let source_key = (transfer.source_object_type.clone(), transfer.source_object_id);
            if departed_sources.contains(&source_key) {
                // Skip - this source already departed one transfer this pulse
                continue;
            }
            
            // Don't use ? operator - log errors and continue processing other transfers
            if let Err(e) = depart_object_to_sphere(ctx, &transfer) {
                log::error!("[2s Pulse] Failed to depart transfer {} from object to sphere: {}", transfer.transfer_id, e);
                // Continue processing remaining transfers
            } else {
                // Mark this source as having departed a transfer
                departed_sources.insert(source_key);
                log::info!("[2s Pulse] Departed transfer {} from {} {}", 
                    transfer.transfer_id, transfer.source_object_type, transfer.source_object_id);
            }
        }
    }

    // Process all transfers waiting at spheres for Sphere→Object departure (final leg)
    for transfer in ctx.db.packet_transfer().iter() {
        if transfer.completed || transfer.current_leg_type != "ArrivedAtSphere" {
            continue;
        }

        // Check if this is ready for Sphere→Object departure (at last sphere)
        let current_sphere_idx = transfer.current_leg as usize;
        if current_sphere_idx + 1 >= transfer.route_spire_ids.len() {
            // At last sphere, depart to final object
            // Don't use ? operator - log errors and continue processing other transfers
            if let Err(e) = depart_sphere_to_object(ctx, &transfer) {
                log::error!("[2s Pulse] Failed to depart transfer {} from sphere to object: {}", transfer.transfer_id, e);
                // Continue processing remaining transfers
            }
        }
        // If there are more spheres, this will be handled by ten_second_pulse
    }

    Ok(())
}

/// Ten-second pulse: Process Sphere→Sphere DEPARTURES + Circuit Emission
/// Called every 100 ticks (10 seconds)
fn ten_second_pulse(ctx: &ReducerContext) -> Result<(), String> {
    let now = ctx.timestamp;
    let current_time = now
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;

    log::info!("[10s Pulse] Processing Sphere→Sphere departures and circuit emission");

    // Process all transfers waiting at spheres for Sphere→Sphere departure
    for transfer in ctx.db.packet_transfer().iter() {
        if transfer.completed || transfer.current_leg_type != "ArrivedAtSphere" {
            continue;
        }

        // Check if there are more sphere hops
        let current_sphere_idx = transfer.current_leg as usize;
        if current_sphere_idx + 1 < transfer.route_spire_ids.len() {
            // More spheres in route, depart to next sphere
            depart_sphere_to_sphere(ctx, &transfer)?;
        }
    }

    // Process circuit emissions - now with proper radius checking and movement
    let circuits: Vec<WorldCircuit> = ctx.db.world_circuit().iter().collect();
    for circuit in circuits {
        if current_time >= circuit.last_emission_time + circuit.emission_interval_ms {
            process_circuit_emission(ctx, &circuit)?;

            // Update circuit emission time
            let mut updated_circuit = circuit.clone();
            updated_circuit.last_emission_time = current_time;
            ctx.db.world_circuit().circuit_id().delete(&circuit.circuit_id);
            ctx.db.world_circuit().insert(updated_circuit);
        }
    }

    // Process orb dissipation (50% chance to lose 1 packet every 10 seconds)
    process_orb_dissipation(ctx)?;

    // Clean up expired wave packet sources
    cleanup_expired_wave_packet_sources(ctx)?;

    Ok(())
}

/// Initiate Object→Sphere departure (first leg)
fn depart_object_to_sphere(ctx: &ReducerContext, transfer: &PacketTransfer) -> Result<(), String> {
    let now = ctx.timestamp;

    // Get source object position
    let source_pos = get_object_position(ctx,
        &transfer.source_object_type,
        transfer.source_object_id)?;

    // Get first sphere in route
    let first_sphere_id = transfer.route_spire_ids[0];
    let first_sphere = ctx.db.distribution_sphere()
        .sphere_id()
        .find(&first_sphere_id)
        .ok_or(format!("First sphere {} not found", first_sphere_id))?;

    let sphere_pos = &first_sphere.sphere_position;

    // Calculate distance and travel time (with height transition from 1 to 10)
    let distance = calculate_distance(&source_pos, &sphere_pos);
    let travel_time = calculate_travel_time(distance, "ObjectToSphere");

    // Start transfer to first sphere
    let mut updated_transfer = transfer.clone();
    updated_transfer.leg_start_time = now;
    updated_transfer.current_leg_type = "ObjectToSphere".to_string();
    updated_transfer.predicted_arrival_time = now + travel_time;

    ctx.db.packet_transfer().delete(transfer.clone());
    ctx.db.packet_transfer().insert(updated_transfer.clone());

    log::info!("[Departure] Transfer {} departing {} {} → sphere {} (distance: {:.1}, ETA: {}s)",
        transfer.transfer_id,
        transfer.source_object_type, transfer.source_object_id,
        first_sphere_id,
        distance, travel_time.as_secs());

    Ok(())
}

/// Initiate Sphere→Object departure (final leg)
fn depart_sphere_to_object(ctx: &ReducerContext, transfer: &PacketTransfer) -> Result<(), String> {
    let now = ctx.timestamp;

    // Get current sphere position
    let current_sphere_idx = (transfer.current_leg as usize).saturating_sub(1);
    let sphere_id = transfer.route_spire_ids.get(current_sphere_idx)
        .ok_or("Invalid sphere index")?;
    let sphere = ctx.db.distribution_sphere()
        .sphere_id()
        .find(sphere_id)
        .ok_or(format!("Sphere {} not found", sphere_id))?;

    let sphere_pos = &sphere.sphere_position;

    // Get destination object position
    let dest_pos = get_object_position(ctx,
        &transfer.destination_object_type,
        transfer.destination_object_id)?;

    // Calculate distance and travel time (with height transition from 10 to 1)
    let distance = calculate_distance(&sphere_pos, &dest_pos);
    let travel_time = calculate_travel_time(distance, "SphereToObject");

    // Advance transfer to final leg
    let mut updated_transfer = transfer.clone();
    updated_transfer.current_leg += 1;
    updated_transfer.leg_start_time = now;
    updated_transfer.current_leg_type = "SphereToObject".to_string();
    updated_transfer.predicted_arrival_time = now + travel_time;

    ctx.db.packet_transfer().delete(transfer.clone());
    ctx.db.packet_transfer().insert(updated_transfer.clone());

    log::info!("[Departure] Transfer {} departing sphere {} → {} {} (distance: {:.1}, ETA: {}s)",
        transfer.transfer_id, sphere_id,
        transfer.destination_object_type, transfer.destination_object_id,
        distance, travel_time.as_secs());

    Ok(())
}

/// Initiate Sphere→Sphere departure
fn depart_sphere_to_sphere(ctx: &ReducerContext, transfer: &PacketTransfer) -> Result<(), String> {
    let now = ctx.timestamp;

    // Get current sphere position
    let current_sphere_idx = transfer.current_leg as usize;
    let sphere_id = transfer.route_spire_ids.get(current_sphere_idx)
        .ok_or("Invalid current sphere index")?;
    let sphere = ctx.db.distribution_sphere()
        .sphere_id()
        .find(sphere_id)
        .ok_or(format!("Current sphere {} not found", sphere_id))?;

    let sphere_pos = &sphere.sphere_position;

    // Get next sphere position
    let next_sphere_idx = current_sphere_idx + 1;
    let next_sphere_id = transfer.route_spire_ids.get(next_sphere_idx)
        .ok_or("Invalid next sphere index")?;
    let next_sphere = ctx.db.distribution_sphere()
        .sphere_id()
        .find(next_sphere_id)
        .ok_or(format!("Next sphere {} not found", next_sphere_id))?;

    let next_pos = &next_sphere.sphere_position;

    // Calculate distance and travel time (constant height at 10)
    let distance = calculate_distance(&sphere_pos, &next_pos);
    let travel_time = calculate_travel_time(distance, "SphereToSphere");

    // Advance transfer to next sphere leg
    let mut updated_transfer = transfer.clone();
    updated_transfer.current_leg += 1;
    updated_transfer.leg_start_time = now;
    updated_transfer.current_leg_type = "SphereToSphere".to_string();
    updated_transfer.predicted_arrival_time = now + travel_time;

    ctx.db.packet_transfer().delete(transfer.clone());
    ctx.db.packet_transfer().insert(updated_transfer.clone());

    log::info!("[Departure] Transfer {} departing sphere {} → sphere {} (distance: {:.1}, ETA: {}s)",
        transfer.transfer_id, sphere_id, next_sphere_id,
        distance, travel_time.as_secs());

    Ok(())
}

// ============================================================================
// Helper Functions
// ============================================================================

/// Calculate 3D distance between two DbVector3 positions
fn calculate_distance(pos1: &DbVector3, pos2: &DbVector3) -> f32 {
    let dx = pos2.x - pos1.x;
    let dy = pos2.y - pos1.y;
    let dz = pos2.z - pos1.z;
    (dx * dx + dy * dy + dz * dz).sqrt()
}

/// Calculate travel time based on distance, leg type, and height transitions
/// Accounts for both horizontal travel and vertical height changes
fn calculate_travel_time(horizontal_distance: f32, leg_type: &str) -> Duration {
    // Determine vertical distance based on leg type
    let vertical_distance = match leg_type {
        "ObjectToSphere" => {
            // Rising from object height (1) to sphere height (10)
            SPHERE_PACKET_HEIGHT - OBJECT_PACKET_HEIGHT  // 9 units
        }
        "SphereToSphere" => {
            // Constant height at sphere level
            0.0
        }
        "SphereToObject" => {
            // Descending from sphere height (10) to object height (1)
            SPHERE_PACKET_HEIGHT - OBJECT_PACKET_HEIGHT  // 9 units
        }
        _ => {
            // Unknown leg type, assume no vertical movement
            log::warn!("[Travel Time] Unknown leg type '{}', assuming no height change", leg_type);
            0.0
        }
    };

    // Calculate total path distance (sequential: vertical + horizontal)
    // Two-phase movement: vertical THEN horizontal (or horizontal THEN vertical)
    let total_distance = vertical_distance + horizontal_distance;

    let seconds = (total_distance / PACKET_SPEED).ceil() as u64;
    Duration::from_secs(seconds)
}

/// Get position of an object by type and ID
fn get_object_position(ctx: &ReducerContext, object_type: &str, object_id: u64) -> Result<DbVector3, String> {
    match object_type {
        "Player" => {
            let player = ctx.db.player()
                .player_id()
                .find(&object_id)
                .ok_or(format!("Player {} not found", object_id))?;
            Ok(player.position.clone())
        }
        "StorageDevice" => {
            let device = ctx.db.storage_device()
                .device_id()
                .find(&object_id)
                .ok_or(format!("StorageDevice {} not found", object_id))?;
            Ok(device.position.clone())
        }
        "Miner" => {
            // TODO: Add miner position lookup when implemented
            Err("Miner position lookup not yet implemented".to_string())
        }
        _ => Err(format!("Unknown object type: {}", object_type))
    }
}

/// Add wave packet samples to a buffer, merging frequencies
fn add_to_buffer(buffer: &mut Vec<WavePacketSample>, samples: &[WavePacketSample]) {
    for sample in samples {
        let mut found = false;
        for existing in buffer.iter_mut() {
            if (existing.frequency - sample.frequency).abs() < 0.01 {
                existing.count += sample.count;
                found = true;
                break;
            }
        }
        if !found {
            buffer.push(sample.clone());
        }
    }
}

// ============================================================================
// Wave Packet Source Movement Helper Functions
// ============================================================================

/// Get one of 8 tangent directions on sphere surface at given normal
/// index 0-7 maps to 0°, 45°, 90°, 135°, 180°, 225°, 270°, 315° around tangent plane
fn get_tangent_direction(surface_normal: &DbVector3, index: u32) -> DbVector3 {
    let angle = (index as f32) * PI / 4.0;  // 8 directions, 45° apart

    // Create orthonormal basis on tangent plane
    // Choose an arbitrary vector not parallel to normal
    let arbitrary = if surface_normal.y.abs() < 0.9 {
        DbVector3::new(0.0, 1.0, 0.0)  // Y-up
    } else {
        DbVector3::new(1.0, 0.0, 0.0)  // X-right
    };

    // tangent1 = normal × arbitrary (perpendicular to both)
    let tangent1 = surface_normal.cross(&arbitrary).normalize();
    // tangent2 = normal × tangent1 (perpendicular to both)
    let tangent2 = surface_normal.cross(&tangent1);

    // Combine tangents with angle to get direction
    DbVector3::new(
        tangent1.x * angle.cos() + tangent2.x * angle.sin(),
        tangent1.y * angle.cos() + tangent2.y * angle.sin(),
        tangent1.z * angle.cos() + tangent2.z * angle.sin(),
    ).normalize()
}

/// Rotate a direction vector around a normal axis by given angle (radians)
fn rotate_around_normal(direction: &DbVector3, normal: &DbVector3, angle: f32) -> DbVector3 {
    // Rodrigues' rotation formula
    let cos_a = angle.cos();
    let sin_a = angle.sin();
    let dot = direction.dot(normal);

    let cross = normal.cross(direction);

    DbVector3::new(
        direction.x * cos_a + cross.x * sin_a + normal.x * dot * (1.0 - cos_a),
        direction.y * cos_a + cross.y * sin_a + normal.y * dot * (1.0 - cos_a),
        direction.z * cos_a + cross.z * sin_a + normal.z * dot * (1.0 - cos_a),
    ).normalize()
}

/// Travel along sphere surface from start position in given direction for given distance
/// Returns the destination position on the sphere surface
fn travel_on_sphere_surface(start: &DbVector3, direction: &DbVector3, distance: f32) -> DbVector3 {
    // Arc length on sphere: distance = radius * angle
    // angle = distance / radius
    let angle = distance / WORLD_RADIUS;

    // Get current radius (might have height offset)
    let start_radius = start.magnitude();
    let start_normal = start.normalize();

    // Use Rodrigues' rotation to move along great circle
    let cos_a = angle.cos();
    let sin_a = angle.sin();

    // Rotate start_normal around the axis perpendicular to both start and direction
    // The axis is: start_normal × direction (but direction is tangent, so this is tricky)
    // Simpler: move along tangent then project back to sphere

    // Project movement onto sphere surface
    let moved = DbVector3::new(
        start.x + direction.x * distance,
        start.y + direction.y * distance,
        start.z + direction.z * distance,
    );

    // Project back onto sphere at original radius
    moved.normalize().scale(start_radius)
}

/// Map cardinal direction name to frequency constant
fn get_direction_frequency(direction: &str) -> f32 {
    match direction {
        // Cardinal directions (6 faces)
        "North" | "South" => FREQ_GREEN,
        "East" | "West" => FREQ_RED,
        "Forward" | "Back" => FREQ_BLUE,

        // Edge directions (12 edges) - Yellow for XY, Cyan for YZ, Magenta for XZ
        d if d.contains("North") && d.contains("East") && !d.contains("Forward") && !d.contains("Back") => FREQ_YELLOW,
        d if d.contains("North") && d.contains("West") && !d.contains("Forward") && !d.contains("Back") => FREQ_YELLOW,
        d if d.contains("South") && d.contains("East") && !d.contains("Forward") && !d.contains("Back") => FREQ_YELLOW,
        d if d.contains("South") && d.contains("West") && !d.contains("Forward") && !d.contains("Back") => FREQ_YELLOW,
        "NorthForward" | "NorthBack" | "SouthForward" | "SouthBack" => FREQ_CYAN,
        "EastForward" | "EastBack" | "WestForward" | "WestBack" => FREQ_MAGENTA,

        // Vertex directions (8 corners) - White (use all frequencies equally, just pick one)
        _ => FREQ_RED,  // For white vertices, default to red
    }
}

/// Find which of the 26 cardinal directions is closest to a position
fn closest_cardinal_direction(position: &DbVector3) -> String {
    let normalized = position.normalize();

    const SQRT2: f32 = 1.414213562373095;
    const SQRT3: f32 = 1.732050807568877;

    // All 26 directions with their normalized vectors
    let directions: Vec<(&str, DbVector3)> = vec![
        // 6 Cardinal (face centers)
        ("North", DbVector3::new(0.0, 1.0, 0.0)),
        ("South", DbVector3::new(0.0, -1.0, 0.0)),
        ("East", DbVector3::new(1.0, 0.0, 0.0)),
        ("West", DbVector3::new(-1.0, 0.0, 0.0)),
        ("Forward", DbVector3::new(0.0, 0.0, 1.0)),
        ("Back", DbVector3::new(0.0, 0.0, -1.0)),

        // 12 Edge centers
        ("NorthEast", DbVector3::new(1.0/SQRT2, 1.0/SQRT2, 0.0)),
        ("NorthWest", DbVector3::new(-1.0/SQRT2, 1.0/SQRT2, 0.0)),
        ("SouthEast", DbVector3::new(1.0/SQRT2, -1.0/SQRT2, 0.0)),
        ("SouthWest", DbVector3::new(-1.0/SQRT2, -1.0/SQRT2, 0.0)),
        ("NorthForward", DbVector3::new(0.0, 1.0/SQRT2, 1.0/SQRT2)),
        ("NorthBack", DbVector3::new(0.0, 1.0/SQRT2, -1.0/SQRT2)),
        ("SouthForward", DbVector3::new(0.0, -1.0/SQRT2, 1.0/SQRT2)),
        ("SouthBack", DbVector3::new(0.0, -1.0/SQRT2, -1.0/SQRT2)),
        ("EastForward", DbVector3::new(1.0/SQRT2, 0.0, 1.0/SQRT2)),
        ("EastBack", DbVector3::new(1.0/SQRT2, 0.0, -1.0/SQRT2)),
        ("WestForward", DbVector3::new(-1.0/SQRT2, 0.0, 1.0/SQRT2)),
        ("WestBack", DbVector3::new(-1.0/SQRT2, 0.0, -1.0/SQRT2)),

        // 8 Vertex (corners)
        ("NorthEastForward", DbVector3::new(1.0/SQRT3, 1.0/SQRT3, 1.0/SQRT3)),
        ("NorthEastBack", DbVector3::new(1.0/SQRT3, 1.0/SQRT3, -1.0/SQRT3)),
        ("NorthWestForward", DbVector3::new(-1.0/SQRT3, 1.0/SQRT3, 1.0/SQRT3)),
        ("NorthWestBack", DbVector3::new(-1.0/SQRT3, 1.0/SQRT3, -1.0/SQRT3)),
        ("SouthEastForward", DbVector3::new(1.0/SQRT3, -1.0/SQRT3, 1.0/SQRT3)),
        ("SouthEastBack", DbVector3::new(1.0/SQRT3, -1.0/SQRT3, -1.0/SQRT3)),
        ("SouthWestForward", DbVector3::new(-1.0/SQRT3, -1.0/SQRT3, 1.0/SQRT3)),
        ("SouthWestBack", DbVector3::new(-1.0/SQRT3, -1.0/SQRT3, -1.0/SQRT3)),
    ];

    let mut best_direction = "North";
    let mut best_dot = -2.0f32;

    for (name, dir) in directions {
        let dot = normalized.dot(&dir);
        if dot > best_dot {
            best_dot = dot;
            best_direction = name;
        }
    }

    best_direction.to_string()
}

/// Create a mixed composition with primary (80%) and secondary (20%) colors
fn create_mixed_composition(
    primary_freq: f32,
    secondary_freq: f32,
    total_packets: u32,
) -> Vec<WavePacketSample> {
    let primary_count = (total_packets as f32 * 0.8) as u32;
    let secondary_count = total_packets - primary_count;

    let mut composition = Vec::new();

    if primary_count > 0 {
        composition.push(WavePacketSample {
            frequency: primary_freq,
            amplitude: 1.0,
            phase: 0.0,
            count: primary_count,
        });
    }

    if secondary_count > 0 {
        composition.push(WavePacketSample {
            frequency: secondary_freq,
            amplitude: 1.0,
            phase: 0.0,
            count: secondary_count,
        });
    }

    composition
}

/// Get circuit position on sphere surface based on cardinal direction
fn get_circuit_surface_position(circuit: &WorldCircuit) -> DbVector3 {
    get_cardinal_position(&circuit.cardinal_direction)
}

// ============================================================================
// Source Movement Processing Functions
// ============================================================================

/// Process all source movement each game tick (called at 10Hz)
fn process_source_movement(ctx: &ReducerContext) {
    let sources: Vec<WavePacketSource> = ctx.db.wave_packet_source().iter().collect();

    for source in sources {
        match source.state {
            SOURCE_STATE_MOVING_H => process_horizontal_movement(ctx, source),
            SOURCE_STATE_ARRIVED_H0 => start_rising(ctx, source),
            SOURCE_STATE_RISING => process_vertical_movement(ctx, source),
            SOURCE_STATE_STATIONARY => {}, // No movement
            _ => {},
        }
    }
}

/// Process horizontal movement along sphere surface
fn process_horizontal_movement(ctx: &ReducerContext, source: WavePacketSource) {
    let dt = 0.1;  // 10Hz = 100ms per tick

    // Check if arrived at destination
    let distance_to_dest = source.position.distance_to(&source.destination);
    if distance_to_dest < SOURCE_MOVE_SPEED * dt * 1.5 {
        // Arrived - snap to destination and transition to next state
        let mut updated = source.clone();
        updated.position = source.destination;
        updated.velocity = DbVector3::zero();
        updated.state = SOURCE_STATE_ARRIVED_H0;

        ctx.db.wave_packet_source().source_id().delete(&source.source_id);
        ctx.db.wave_packet_source().insert(updated);
        return;
    }

    // Continue moving
    let new_pos = DbVector3::new(
        source.position.x + source.velocity.x * dt,
        source.position.y + source.velocity.y * dt,
        source.position.z + source.velocity.z * dt,
    );

    // Project back onto sphere surface at height 0
    let surface_normal = new_pos.normalize();
    let projected_pos = surface_normal.scale(WORLD_RADIUS + SOURCE_HEIGHT_0);

    let mut updated = source.clone();
    updated.position = projected_pos;

    ctx.db.wave_packet_source().source_id().delete(&source.source_id);
    ctx.db.wave_packet_source().insert(updated);
}

/// Start rising from height 0 to height 1
fn start_rising(ctx: &ReducerContext, source: WavePacketSource) {
    let surface_normal = source.position.normalize();

    let mut updated = source.clone();
    updated.state = SOURCE_STATE_RISING;
    // Set radial velocity (pointing outward from sphere center)
    updated.velocity = surface_normal.scale(SOURCE_RISE_SPEED);

    ctx.db.wave_packet_source().source_id().delete(&source.source_id);
    ctx.db.wave_packet_source().insert(updated);
}

/// Process vertical (radial) movement from height 0 to height 1
fn process_vertical_movement(ctx: &ReducerContext, source: WavePacketSource) {
    let dt = 0.1;  // 10Hz = 100ms per tick

    let surface_normal = source.position.normalize();
    let current_height = source.position.magnitude() - WORLD_RADIUS;

    if current_height >= SOURCE_HEIGHT_1 {
        // Reached final height - become stationary
        let final_pos = surface_normal.scale(WORLD_RADIUS + SOURCE_HEIGHT_1);

        let mut updated = source.clone();
        updated.position = final_pos;
        updated.velocity = DbVector3::zero();
        updated.state = SOURCE_STATE_STATIONARY;

        ctx.db.wave_packet_source().source_id().delete(&source.source_id);
        ctx.db.wave_packet_source().insert(updated);
    } else {
        // Continue rising
        let new_height = current_height + SOURCE_RISE_SPEED * dt;
        let new_pos = surface_normal.scale(WORLD_RADIUS + new_height);

        let mut updated = source.clone();
        updated.position = new_pos;

        ctx.db.wave_packet_source().source_id().delete(&source.source_id);
        ctx.db.wave_packet_source().insert(updated);
    }
}

// ============================================================================
// Database Initialization
// ============================================================================

/// Database initialization reducer - runs automatically on first publish
#[spacetimedb::reducer(init)]
pub fn __init__(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("=== DATABASE INITIALIZATION START ===");
    
    // Spawn initial world objects
    spawn_all_26_spires(ctx, 0, 0, 0)?;
    log::info!("[Init] Created 26 energy spires");
    
    spawn_6_cardinal_circuits(ctx, 0, 0, 0)?;
    log::info!("[Init] Created 6 cardinal circuits");
    
    // Start game loop
    start_game_loop(ctx)?;
    log::info!("[Init] Started game loop at 10Hz");
    
    log::info!("=== DATABASE INITIALIZATION COMPLETE ===");
    Ok(())
}
