use spacetimedb::{
    log, Identity, ReducerContext, SpacetimeType, Table, Timestamp,
};
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
    
    pub fn distance_to(&self, other: &DbVector3) -> f32 {
        let dx = self.x - other.x;
        let dy = self.y - other.y;
        let dz = self.z - other.z;
        (dx * dx + dy * dy + dz * dz).sqrt()
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
    pub orbs_per_emission: u32,
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
    pub player_id: u64,
    pub composition: Vec<WavePacketSample>,  // Player's frequency mix
    pub packet_count: u32,                   // How many packets in transfer
    pub route_waypoints: Vec<DbVector3>,     // Path positions
    pub route_spire_ids: Vec<u64>,           // Which spires receive charge
    pub destination_device_id: u64,
    pub initiated_at: Timestamp,
    pub completed: bool,
    pub current_leg: u32,                    // Which hop in route (0 = player->sphere, 1+ = sphere->sphere)
    pub leg_start_time: Timestamp,           // When current leg started
    pub state: String,                       // "PlayerPulse", "InTransit", "Completed"
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
    // NEW: Concurrent mining support
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
    pub source_id: u64,           // ID of the source (orb_id, etc)
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
    pub orb_id: u64,
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
    
    // Create a circuit in center world
    let center_circuit = WorldCircuit {
        circuit_id: 0,
        world_coords: WorldCoords { x: 0, y: 0, z: 0 },
        cardinal_direction: "Center".to_string(),
        circuit_type: "Basic".to_string(),
        qubit_count: 1,
        orbs_per_emission: 3,
        emission_interval_ms: 10000, // Every 10 seconds
        last_emission_time: 0,
    };
    ctx.db.world_circuit().insert(center_circuit);
    log::info!("Created basic circuit in center world");
    
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
        if let Some(orb) = ctx.db.wave_packet_orb().orb_id().find(&mining_session.orb_id) {
            let mut updated_orb = orb.clone();
            updated_orb.active_miner_count = updated_orb.active_miner_count.saturating_sub(1);
            ctx.db.wave_packet_orb().delete(orb);
            ctx.db.wave_packet_orb().insert(updated_orb);
            log::info!("Decremented active miner count for orb {}", mining_session.orb_id);
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
    
    log::info!("Circuit {} emitted {} orbs in world {:?}", 
        circuit.circuit_id, circuit.orbs_per_emission, circuit.world_coords);
    
    Ok(())
}

#[spacetimedb::reducer]
pub fn emit_wave_packet_orb(
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

    let orb = WavePacketOrb {
        orb_id: 0, // auto-generated
        world_coords,
        position: source_position,
        velocity,
        wave_packet_composition: composition,
        total_wave_packets: total_packets,
        creation_time: current_time,
        lifetime_ms: 300000, // 5 minutes
        last_dissipation: current_time,
        active_miner_count: 0,
        last_depletion: current_time,
    };
    
    ctx.db.wave_packet_orb().insert(orb);
    
    Ok(())
}

fn process_orb_dissipation(ctx: &ReducerContext) -> Result<(), String> {
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    const DISSIPATION_INTERVAL_MS: u64 = 10000; // Every 10 seconds
    const DISSIPATION_RATE: u32 = 1; // Lose 1 packet per interval
    
    let orbs_to_update: Vec<_> = ctx.db.wave_packet_orb()
        .iter()
        .filter(|orb| {
            orb.total_wave_packets > 0 && 
            current_time >= orb.last_dissipation + DISSIPATION_INTERVAL_MS
        })
        .collect();
    
    for orb in orbs_to_update {
        let orb_id = orb.orb_id;
        let was_empty = orb.total_wave_packets == 0;
        
        let mut updated_orb = orb.clone();
        updated_orb.total_wave_packets = updated_orb.total_wave_packets.saturating_sub(DISSIPATION_RATE);
        updated_orb.last_dissipation = current_time;
        
        // Also reduce composition counts proportionally
        if updated_orb.total_wave_packets == 0 {
            // Clear all samples if orb is empty
            for sample in &mut updated_orb.wave_packet_composition {
                sample.count = 0;
            }
        } else {
            // Reduce one random sample
            if let Some(sample) = updated_orb.wave_packet_composition.iter_mut()
                .find(|s| s.count > 0) {
                sample.count = sample.count.saturating_sub(1);
            }
        }
        
        let is_now_empty = updated_orb.total_wave_packets == 0;
        
        ctx.db.wave_packet_orb().delete(orb);
        ctx.db.wave_packet_orb().insert(updated_orb);
        
        if !was_empty && is_now_empty {
            log::info!("Orb {} fully dissipated", orb_id);
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
        .filter(|orb| current_time >= orb.creation_time + orb.lifetime_ms as u64)
        .collect();
    
    for orb in expired_orbs {
        log::info!("Removing expired orb {}", orb.orb_id);
        ctx.db.wave_packet_orb().delete(orb);
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
                session.orb_id,
                session.total_extracted,
                session.is_active
            );
        }
    }
    // Also log orb status
    let orb_count = ctx.db.wave_packet_orb().iter().count();
    let total_packets: u32 = ctx.db.wave_packet_orb()
        .iter()
        .map(|orb| orb.total_wave_packets)
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

    // Create orb at specified position
    let orb = WavePacketOrb {
        orb_id: 0, // auto_inc will assign
        world_coords: WorldCoords { x: 0, y: 0, z: 0 },
        position: DbVector3::new(x, y, z),
        velocity: DbVector3::new(0.0, 0.0, 0.0),
        wave_packet_composition: composition,
        total_wave_packets: packet_count,
        creation_time: current_time,
        lifetime_ms: 3600000, // 1 hour lifetime
        last_dissipation: current_time,
        active_miner_count: 0,
        last_depletion: current_time,
    };

    // Insert into database
    ctx.db.wave_packet_orb().insert(orb.clone());

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

    // Create orb at specified position
    let orb = WavePacketOrb {
        orb_id: 0,  // auto_inc will assign
        world_coords: WorldCoords { x: 0, y: 0, z: 0 },
        position: DbVector3::new(x, y, z),
        velocity: DbVector3::new(0.0, 0.0, 0.0),
        wave_packet_composition: composition,
        total_wave_packets: total_packets,
        creation_time: current_time,
        lifetime_ms: 3600000,  // 1 hour lifetime
        last_dissipation: current_time,
        active_miner_count: 0,
        last_depletion: current_time,
    };

    ctx.db.wave_packet_orb().insert(orb);

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

    // Create orb at specified position
    let orb = WavePacketOrb {
        orb_id: 0,  // auto_inc will assign
        world_coords: WorldCoords { x: 0, y: 0, z: 0 },
        position: DbVector3::new(x, y, z),
        velocity: DbVector3::new(0.0, 0.0, 0.0),
        wave_packet_composition: composition,
        total_wave_packets: total_packets,
        creation_time: current_time,
        lifetime_ms: 3600000,  // 1 hour lifetime
        last_dissipation: current_time,
        active_miner_count: 0,
        last_depletion: current_time,
    };

    ctx.db.wave_packet_orb().insert(orb);

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

        // Create orb
        let orb = WavePacketOrb {
            orb_id: 0, // auto_inc
            world_coords: WorldCoords { x: 0, y: 0, z: 0 },
            position,
            velocity: DbVector3::new(0.0, 0.0, 0.0),
            wave_packet_composition: composition.clone(),
            total_wave_packets: total_packets,
            creation_time: current_time,
            lifetime_ms: 3600000, // 1 hour
            last_dissipation: current_time,
            active_miner_count: 0,
            last_depletion: current_time,
        };

        ctx.db.wave_packet_orb().insert(orb);
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
/// * `orb_id` - The orb to mine
///
/// # Returns
/// * Ok(()) if session started successfully
/// * Err if already mining this orb or orb not found
#[spacetimedb::reducer]
pub fn start_mining_v2(
    ctx: &ReducerContext,
    orb_id: u64,
    crystal_composition: Vec<WavePacketSample>,
) -> Result<(), String> {
    log::info!("=== START_MINING_V2 START ===");
    log::info!("Orb ID: {}, Crystal composition: {} frequencies, Identity: {:?}",
        orb_id, crystal_composition.len(), ctx.sender);
    for sample in &crystal_composition {
        log::info!("  Crystal: freq={:.3}, count={}", sample.frequency, sample.count);
    }

    // Check if player already mining THIS specific orb
    let existing_session = ctx.db.mining_session()
        .iter()
        .find(|s| s.player_identity == ctx.sender && s.orb_id == orb_id && s.is_active);

    if existing_session.is_some() {
        log::warn!("Player already mining this orb");
        return Err("You are already mining this orb".to_string());
    }

    // Verify orb exists and has packets remaining
    let orb = ctx.db.wave_packet_orb()
        .orb_id()
        .find(&orb_id)
        .ok_or("Orb not found")?;

    if orb.total_wave_packets == 0 {
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
        orb_id,
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
    let mut updated_orb = orb.clone();
    updated_orb.active_miner_count += 1;
    let active_count = updated_orb.active_miner_count;

    ctx.db.wave_packet_orb().delete(orb);
    ctx.db.wave_packet_orb().insert(updated_orb);

    log::info!("Mining session started successfully for orb {} (active miners: {})",
        orb_id, active_count);
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
    let orb = ctx.db.wave_packet_orb()
        .orb_id()
        .find(&session.orb_id)
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
        let available_sample = orb.wave_packet_composition.iter()
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
    let mut updated_composition = orb.wave_packet_composition.clone();

    for extracted in &actual_extraction {
        for sample in &mut updated_composition {
            if (sample.frequency - extracted.frequency).abs() < 0.001 {
                sample.count = sample.count.saturating_sub(extracted.count);
                break;
            }
        }
    }

    // Update orb
    let mut updated_orb = orb.clone();
    updated_orb.wave_packet_composition = updated_composition;
    updated_orb.total_wave_packets = updated_orb.total_wave_packets.saturating_sub(total_to_extract);
    updated_orb.last_depletion = current_time;

    // Save values we need before moving session
    let session_orb_id = session.orb_id;
    let session_player_identity = session.player_identity;

    // Update mining session (do this before modifying orb/session state)
    let mut updated_session = session.clone();
    updated_session.last_extraction = current_time;
    updated_session.total_extracted += total_to_extract;

    // Check if orb is now empty
    if updated_orb.total_wave_packets == 0 {
        log::info!("Orb depleted, removing from world");
        updated_session.is_active = false;
        // Delete the depleted orb instead of updating it
        ctx.db.wave_packet_orb().delete(orb);
    } else {
        // Update orb if still has packets
        ctx.db.wave_packet_orb().delete(orb);
        ctx.db.wave_packet_orb().insert(updated_orb.clone());
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
            source_id: session_orb_id,
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
        total_to_extract, updated_orb.total_wave_packets);
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
    let orb_id = session.orb_id;

    ctx.db.mining_session().delete(session);
    ctx.db.mining_session().insert(updated_session);

    // NOTE: Dont clean up pending extractions - let them complete and add to inventory
    // The client will call capture_extracted_packet_v2 when packets arrive
    log::info!("Mining session stopped - pending extractions will complete normally");


    // Decrement orb's active miner count
    if let Some(orb) = ctx.db.wave_packet_orb().orb_id().find(&orb_id) {
        let mut updated_orb = orb.clone();
        updated_orb.active_miner_count = updated_orb.active_miner_count.saturating_sub(1);
        let active_count = updated_orb.active_miner_count;

        ctx.db.wave_packet_orb().delete(orb);
        ctx.db.wave_packet_orb().insert(updated_orb);

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

/// TESTING: Clear all orbs from the database
/// WARNING: Test only - removes all orbs
#[spacetimedb::reducer]
pub fn clear_all_orbs(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("=== CLEAR_ALL_ORBS START ===");

    let orbs: Vec<_> = ctx.db.wave_packet_orb().iter().collect();
    let count = orbs.len();

    for orb in orbs {
        ctx.db.wave_packet_orb().delete(orb);
    }

    log::info!("Cleared {} orbs for testing", count);
    log::info!("=== CLEAR_ALL_ORBS END ===");

    Ok(())
}

/// TESTING: Set an orb's packet count instantly
/// Useful for testing depletion scenarios
#[spacetimedb::reducer]
pub fn set_orb_packets(
    ctx: &ReducerContext,
    orb_id: u64,
    new_count: u32,
) -> Result<(), String> {
    log::info!("=== SET_ORB_PACKETS START ===");
    log::info!("Orb ID: {}, New count: {}", orb_id, new_count);

    let orb = ctx.db.wave_packet_orb()
        .orb_id()
        .find(&orb_id)
        .ok_or("Orb not found")?;

    let mut updated = orb.clone();
    updated.total_wave_packets = new_count;

    // Also update first composition sample if it exists
    if let Some(first_sample) = updated.wave_packet_composition.get_mut(0) {
        first_sample.count = new_count;
    }

    ctx.db.wave_packet_orb().delete(orb);
    ctx.db.wave_packet_orb().insert(updated);

    log::info!("Set orb {} to {} packets", orb_id, new_count);
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
            session.orb_id,
            session.total_extracted,
            session.extraction_multiplier
        );
    }

    // Also show orb stats
    let orbs: Vec<_> = ctx.db.wave_packet_orb().iter().collect();
    log::info!("Orbs in database: {}", orbs.len());

    for orb in &orbs {
        log::info!("  Orb {}: {} packets remaining, {} active miners",
            orb.orb_id,
            orb.total_wave_packets,
            orb.active_miner_count
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

/// Initiate energy packet transfer from player to storage device
/// Routes through nearest energy spires
#[spacetimedb::reducer]
pub fn initiate_transfer(ctx: &ReducerContext, composition: Vec<WavePacketSample>, destination_device_id: u64) -> Result<(), String> {
    log::info!("=== INITIATE_TRANSFER START ===");
    log::info!("Composition: {:?}, Destination: {}", composition, destination_device_id);

    // Validate composition (max 5 per frequency, 30 total)
    let mut total_count = 0u32;
    for sample in &composition {
        if sample.count > 5 {
            return Err(format!("Cannot transfer more than 5 packets of frequency {}", sample.frequency));
        }
        total_count += sample.count;
    }
    if total_count > 30 {
        return Err(format!("Cannot transfer more than 30 total packets (got {})", total_count));
    }

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

    // Check inventory has enough of each frequency
    let inventory = ctx.db.player_inventory()
        .player_id()
        .find(&player.player_id)
        .ok_or("Player inventory not found - call initialize_player_inventory first")?;

    for sample in &composition {
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
    for sample in &composition {
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
    deduct_composition_from_inventory(ctx, player.player_id, &composition)?;

    // Create transfer record with new state fields
    let transfer = PacketTransfer {
        transfer_id: 0, // auto_inc will set this
        player_id: player.player_id,
        composition: composition.clone(),
        packet_count: total_count,
        route_waypoints: waypoints.clone(),
        route_spire_ids: spire_ids.clone(),
        destination_device_id,
        initiated_at: ctx.timestamp,
        completed: false,
        current_leg: 0,  // Starts at player->sphere leg
        leg_start_time: ctx.timestamp,
        state: "PlayerPulse".to_string(),  // Waiting for 2-second player pulse
    };

    ctx.db.packet_transfer().insert(transfer);

    log::info!("Transfer initiated: {} packets routed through {} spires", total_count, spire_ids.len());
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
    orbs_per_emission: u32,
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
        orbs_per_emission,
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

        // Create the circuit with default values
        let circuit = WorldCircuit {
            circuit_id: 0, // auto_inc
            world_coords: world_coords.clone(),
            cardinal_direction: direction.to_string(),
            circuit_type: "Basic".to_string(),
            qubit_count: 1,
            orbs_per_emission: 8,
            emission_interval_ms: 10000, // Every 10 seconds
            last_emission_time: 0, // Not yet emitted
        };

        ctx.db.world_circuit().insert(circuit);

        log::info!("Created circuit at {} on world ({},{},{}) - 8 orbs per emission",
            direction, world_x, world_y, world_z);
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
