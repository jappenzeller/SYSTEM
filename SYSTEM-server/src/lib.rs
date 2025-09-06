use spacetimedb::{
    log, Identity, ReducerContext, SpacetimeType, Table, Timestamp,
};
use std::collections::HashMap;
use std::sync::{Mutex, OnceLock};
use std::f32::consts::PI;

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
    pub circuit_type: String,
    pub qubit_count: u8, 
    pub orbs_per_emission: u32,
    pub emission_interval_ms: u64,
    pub last_emission_time: u64,
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
        let band = get_frequency_band(self.frequency);
        format!("{:?}", band)
    }
}

#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq)]
pub struct WavePacketSample {
    pub frequency: f32,
    pub amplitude: f32,
    pub phase: f32,
    pub count: u32,
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
// Mining System State
// ============================================================================

#[derive(Debug, Clone)]
pub struct PendingWavePacket {
    pub wave_packet_id: u64,
    pub signature: WavePacketSignature,
    pub extracted_at: u64,
    pub flight_time: u64,
}

#[derive(Debug, Clone)]
pub struct MiningSession {
    pub player_id: u64,
    pub orb_id: u64,
    pub crystal_type: CrystalType,
    pub started_at: u64,
    pub last_packet_time: u64,
    pub pending_wave_packets: Vec<PendingWavePacket>,
}

static MINING_STATE: OnceLock<Mutex<HashMap<u64, MiningSession>>> = OnceLock::new();

fn get_mining_state() -> &'static Mutex<HashMap<u64, MiningSession>> {
    MINING_STATE.get_or_init(|| Mutex::new(HashMap::new()))
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

fn get_frequency_band(frequency: f32) -> FrequencyBand {
    let radian = frequency * 2.0 * PI;
    if radian < PI / 6.0 || radian > 11.0 * PI / 6.0 { FrequencyBand::Red }
    else if radian < PI / 2.0 { FrequencyBand::Yellow }
    else if radian < 5.0 * PI / 6.0 { FrequencyBand::Green }
    else if radian < 7.0 * PI / 6.0 { FrequencyBand::Cyan }
    else if radian < 3.0 * PI / 2.0 { FrequencyBand::Blue }
    else { FrequencyBand::Magenta }
}

// ============================================================================
// Authentication Reducers
// ============================================================================

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
    
    // Create new player at center world
    let player = Player {
        player_id: 0, // auto-generated
        identity: ctx.sender,
        name: name.clone(),
        account_id,
        current_world: WorldCoords { x: 0, y: 0, z: 0 },
        position: DbVector3::new(0.0, 100.0, 0.0),
        rotation: DbQuaternion::default(),
        last_update: ctx.timestamp
            .duration_since(Timestamp::UNIX_EPOCH)
            .expect("Valid timestamp")
            .as_millis() as u64,
    };
    
    ctx.db.player().insert(player.clone());
    log::info!("Created new player '{}' (ID will be auto-generated) in center world", name);
    
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
    
    // Update player world
    let mut updated_player = player.clone();
    updated_player.current_world = world_coords;
    updated_player.position = DbVector3::new(0.0, 100.0, 0.0); // Reset position in new world
    updated_player.last_update = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    ctx.db.player().delete(player);
    ctx.db.player().insert(updated_player);
    
    log::info!("Player '{}' successfully traveled to world ({},{},{})",
        player_name, world_coords.x, world_coords.y, world_coords.z);
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
    
    // Clean up any active mining sessions
    let mut mining_state = get_mining_state().lock().unwrap();
    
    // Find player IDs to clean up (should be just one, but being safe)
    let player_ids: Vec<u64> = ctx.db.player()
        .iter()
        .filter(|p| p.identity == ctx.sender)
        .map(|p| p.player_id)
        .collect();
    
    for player_id in player_ids {
        if let Some(_session) = mining_state.remove(&player_id) {
            log::info!("Cleaned up mining session for player_id: {}", player_id);
            
            // Clean up any active extractions for this player
            let extractions_to_remove: Vec<u64> = ctx.db.wave_packet_extraction()
                .iter()
                .filter(|e| e.player_id == player_id)
                .map(|e| e.extraction_id)
                .collect();
                
            for extraction_id in extractions_to_remove {
                ctx.db.wave_packet_extraction()
                    .extraction_id()
                    .delete(&extraction_id);
                log::info!("Cleaned up extraction_id: {}", extraction_id);
            }
        }
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
    
    // Clean up any active mining sessions
    let mut mining_state = get_mining_state().lock().unwrap();
    
    // Find and remove any sessions for players with this identity
    let player_ids_to_remove: Vec<u64> = ctx.db.player()
        .iter()
        .filter(|p| p.identity == ctx.sender)
        .map(|p| p.player_id)
        .collect();
    
    for player_id in player_ids_to_remove {
        if mining_state.remove(&player_id).is_some() {
            log::info!("Cleaned up mining session for player ID: {}", player_id);
        }
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

#[spacetimedb::reducer]
pub fn start_mining(
    ctx: &ReducerContext,
    orb_id: u64,
    crystal_type: CrystalType,
) -> Result<(), String> {
    log::info!("=== START_MINING ===");
    log::info!("Orb ID: {}, Crystal: {:?}, Identity: {:?}", orb_id, crystal_type, ctx.sender);
    
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    log::info!("Player '{}' (ID: {}) starting mining", player.name, player.player_id);
    
    let orb = ctx.db.wave_packet_orb()
        .orb_id()
        .find(&orb_id)
        .ok_or("Orb not found")?;
    
    // Verify orb is in same world
    if orb.world_coords != player.current_world {
        log::warn!("Mining failed: Orb in different world - Player: {:?}, Orb: {:?}", 
            player.current_world, orb.world_coords);
        return Err(format!("Orb {} is in world {:?}, player is in {:?}", 
            orb_id, orb.world_coords, player.current_world));
    }
    
    // Check distance
    let distance = player.position.distance_to(&orb.position);
    const MAX_MINING_RANGE: f32 = 30.0;
    
    if distance > MAX_MINING_RANGE {
        log::warn!("Mining failed: Out of range - Distance: {}, Max: {}", distance, MAX_MINING_RANGE);
        return Err(format!("Too far from orb (distance: {:.1})", distance));
    }
    
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    let mut mining_state = get_mining_state().lock().unwrap();
    
    // Check if already mining
    if mining_state.contains_key(&player.player_id) {
        log::warn!("Mining failed: Player already mining");
        return Err("You are already mining".to_string());
    }
    
    // Create mining session
    let session = MiningSession {
        player_id: player.player_id,
        orb_id,
        crystal_type,
        started_at: current_time,
        last_packet_time: current_time,
        pending_wave_packets: Vec::new(),
    };
    
    mining_state.insert(player.player_id, session);
    
    log::info!("Mining session started successfully for player '{}' on orb {}", 
        player.name, orb_id);
    log::info!("=== START_MINING END ===");
    Ok(())
}

#[spacetimedb::reducer]
pub fn stop_mining(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("=== STOP_MINING START ===");
    log::info!("Identity: {:?}", ctx.sender);
    
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    let mut mining_state = get_mining_state().lock().unwrap();
    
    if let Some(session) = mining_state.remove(&player.player_id) {
        log::info!("Stopped mining session for player '{}' (ID: {}) on orb {}", 
            player.name, player.player_id, session.orb_id);
        
        // Log any pending packets that will be lost
        if !session.pending_wave_packets.is_empty() {
            log::info!("Player had {} pending wave packets that will be lost", 
                session.pending_wave_packets.len());
        }
        
        Ok(())
    } else {
        log::warn!("Stop mining failed: Player '{}' was not mining", player.name);
        Err("You are not currently mining".to_string())
    }
}

// Extract wave packet logic (simplified for brevity)
fn extract_wave_packet_helper(
    ctx: &ReducerContext,
    session: &mut MiningSession,
    current_time: u64,
) -> Result<(), String> {
    const EXTRACTION_INTERVAL_MS: u64 = 2000; // 2 seconds per packet
    
    // Check if enough time has passed
    if current_time < session.last_packet_time + EXTRACTION_INTERVAL_MS {
        return Ok(()); // Not time yet
    }
    
    // Get the orb
    let orb = ctx.db.wave_packet_orb()
        .orb_id()
        .find(&session.orb_id)
        .ok_or("Orb no longer exists")?;
    
    // Check if orb has packets
    if orb.total_wave_packets == 0 {
        return Err("Orb is empty".to_string());
    }
    
    // Get player for position
    let player = ctx.db.player()
        .player_id()
        .find(&session.player_id)
        .ok_or("Player not found")?;
    
    // Extract a wave packet based on crystal resonance
    let _crystal_freq = match session.crystal_type {
        CrystalType::Red => 0.0,
        CrystalType::Green => 1.0 / 3.0,
        CrystalType::Blue => 2.0 / 3.0,
    };
    
    // Find matching packets in orb
    let matching_sample = orb.wave_packet_composition.iter()
        .find(|sample| {
            let band = get_frequency_band(sample.frequency);
            match session.crystal_type {
                CrystalType::Red => matches!(band, FrequencyBand::Red | FrequencyBand::Yellow | FrequencyBand::Magenta),
                CrystalType::Green => matches!(band, FrequencyBand::Green | FrequencyBand::Yellow | FrequencyBand::Cyan),
                CrystalType::Blue => matches!(band, FrequencyBand::Blue | FrequencyBand::Cyan | FrequencyBand::Magenta),
            }
        });
    
    if let Some(sample) = matching_sample {
        // Create extracted packet
        let signature = WavePacketSignature::new(sample.frequency, sample.amplitude, sample.phase);
        let wave_packet_id = current_time; // Simple ID generation
        
        // Calculate flight time based on distance
        let distance = player.position.distance_to(&orb.position);
        let flight_time = ((distance / 5.0) * 1000.0) as u64; // 5 units/second
        
        let pending_packet = PendingWavePacket {
            wave_packet_id,
            signature,
            extracted_at: current_time,
            flight_time,
        };
        
        session.pending_wave_packets.push(pending_packet.clone());
        session.last_packet_time = current_time;
        
        // Update orb
        let mut updated_orb = orb.clone();
        updated_orb.total_wave_packets = updated_orb.total_wave_packets.saturating_sub(1);
        
        // Update the specific sample count
        let mut updated_composition = updated_orb.wave_packet_composition.clone();
        for comp in &mut updated_composition {
            if comp.frequency == sample.frequency && comp.count > 0 {
                comp.count -= 1;
                break;
            }
        }
        updated_orb.wave_packet_composition = updated_composition;
        
        ctx.db.wave_packet_orb().delete(orb);
        ctx.db.wave_packet_orb().insert(updated_orb);
        
        log::info!(
            "Player {} extracted wave packet {} ({}) from orb {}",
            player.name,
            wave_packet_id,
            signature.to_color_string(),
            session.orb_id
        );
        
        // Create extraction notification for client
        let extraction = WavePacketExtraction {
            extraction_id: wave_packet_id,
            player_id: player.player_id,
            wave_packet_id,
            signature,
            departure_time: current_time,
            expected_arrival: current_time + flight_time,
        };
        
        ctx.db.wave_packet_extraction().insert(extraction);
    }
    
    Ok(())
}

#[spacetimedb::reducer]
pub fn extract_wave_packet(ctx: &ReducerContext) -> Result<(), String> {
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    let mut mining_state = get_mining_state().lock().unwrap();
    let session = mining_state.get_mut(&player.player_id)
        .ok_or("You are not currently mining")?;
    
    extract_wave_packet_helper(ctx, session, current_time)
}

// Helper function to extract packets for a specific player (called from tick)
fn extract_wave_packet_for_player(
    ctx: &ReducerContext, 
    player_id: u64
) -> Result<(), String> {
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    let mut mining_state = get_mining_state().lock().unwrap();
    
    if let Some(session) = mining_state.get_mut(&player_id) {
        extract_wave_packet_helper(ctx, session, current_time)
    } else {
        Err("Player not mining".to_string())
    }
}

#[spacetimedb::reducer]
pub fn capture_wave_packet(
    ctx: &ReducerContext,
    wave_packet_id: u64,
) -> Result<(), String> {
    log::info!("=== CAPTURE_WAVE_PACKET START ===");
    log::info!("Wave packet ID: {}, Identity: {:?}", wave_packet_id, ctx.sender);
    
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    let mut mining_state = get_mining_state().lock().unwrap();
    let session = mining_state.get_mut(&player.player_id)
        .ok_or("You are not currently mining")?;
    
    // Find the pending wave packet
    let packet_index = session.pending_wave_packets.iter()
        .position(|p| p.wave_packet_id == wave_packet_id)
        .ok_or("Wave packet not found in pending list")?;
    
    let wave_packet = session.pending_wave_packets.remove(packet_index);
    
    log::info!("Player '{}' capturing wave packet {} ({})", 
        player.name, wave_packet_id, wave_packet.signature.to_color_string());
    
    // Add to player's storage
    add_wave_packets_to_storage(
        ctx,
        "player".to_string(),
        player.player_id,
        wave_packet.signature,
        1,
        player.current_world.x.abs() as u8,
    )?;
    
    // Remove extraction notification
    if let Some(extraction) = ctx.db.wave_packet_extraction()
        .iter()
        .find(|e| e.wave_packet_id == wave_packet_id) {
        ctx.db.wave_packet_extraction().delete(extraction);
    }
    
    log::info!("Wave packet {} successfully captured by player '{}'", 
        wave_packet_id, player.name);
    log::info!("=== CAPTURE_WAVE_PACKET END ===");
    
    Ok(())
}

// ============================================================================
// Wave Packet System Functions
// ============================================================================

fn add_wave_packets_to_storage(
    ctx: &ReducerContext,
    owner_type: String,
    owner_id: u64,
    signature: WavePacketSignature,
    count: u32,
    _world_distance: u8,
) -> Result<(), String> {
    let frequency_band = get_frequency_band(signature.frequency);
    
    // Find existing storage for this owner and frequency band
    let existing_storage = ctx.db.wave_packet_storage()
        .iter()
        .find(|s| s.owner_type == owner_type && 
                   s.owner_id == owner_id && 
                   s.frequency_band == frequency_band);
    
    if let Some(storage) = existing_storage {
        // Update existing storage
        let mut updated_storage = storage.clone();
        updated_storage.total_wave_packets += count;
        
        // Update or add the sample
        let mut found = false;
        for sample in &mut updated_storage.signature_samples {
            if sample.frequency == signature.frequency {
                sample.count += count;
                found = true;
                break;
            }
        }
        
        if !found {
            updated_storage.signature_samples.push(WavePacketSample {
                frequency: signature.frequency,
                amplitude: signature.amplitude,
                phase: signature.phase,
                count,
            });
        }
        
        updated_storage.last_update = ctx.timestamp
            .duration_since(Timestamp::UNIX_EPOCH)
            .expect("Valid timestamp")
            .as_millis() as u64;
        
        ctx.db.wave_packet_storage().delete(storage);
        ctx.db.wave_packet_storage().insert(updated_storage);
    } else {
        // Create new storage
        let sample = WavePacketSample {
            frequency: signature.frequency,
            amplitude: signature.amplitude,
            phase: signature.phase,
            count,
        };
        
        let new_storage = WavePacketStorage {
            storage_id: 0, // auto-generated
            owner_type,
            owner_id,
            frequency_band,
            total_wave_packets: count,
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
    
    // Process all active mining sessions
    let player_ids: Vec<u64> = {
        let mining_state = get_mining_state().lock().unwrap();
        mining_state.keys().cloned().collect()
    };
    
    for player_id in player_ids {
        if let Err(e) = extract_wave_packet_for_player(ctx, player_id) {
            log::warn!("Failed to extract wave packet for player {}: {}", player_id, e);
        }
    }
    
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
    
    let orb = WavePacketOrb {
        orb_id: 0, // auto-generated
        world_coords,
        position: source_position,
        velocity,
        wave_packet_composition: composition,
        total_wave_packets: total_packets,
        creation_time: ctx.timestamp
            .duration_since(Timestamp::UNIX_EPOCH)
            .expect("Valid timestamp")
            .as_millis() as u64,
        lifetime_ms: 300000, // 5 minutes
        last_dissipation: ctx.timestamp
            .duration_since(Timestamp::UNIX_EPOCH)
            .expect("Valid timestamp")
            .as_millis() as u64,
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
    
    // Clean up extractions older than 1 minute (should have been captured or lost)
    let old_extractions: Vec<_> = ctx.db.wave_packet_extraction()
        .iter()
        .filter(|e| current_time > e.expected_arrival + 60000)
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
    
    let mining_state = get_mining_state().lock().unwrap();
    log::info!("Active mining sessions: {}", mining_state.len());
    
    for (player_id, session) in mining_state.iter() {
        if let Some(player) = ctx.db.player().player_id().find(player_id) {
            log::info!(
                "Player '{}' (ID: {}) mining orb {} with {:?} crystal. Pending packets: {}",
                player.name,
                player.player_id,
                session.orb_id,
                session.crystal_type,
                session.pending_wave_packets.len()
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

#[spacetimedb::reducer]
pub fn debug_wave_packet_status(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("=== DEBUG_WAVE_PACKET_STATUS START ===");
    
    // Count packets by frequency band in storage
    let mut band_counts: HashMap<FrequencyBand, u32> = HashMap::new();
    
    for storage in ctx.db.wave_packet_storage().iter() {
        *band_counts.entry(storage.frequency_band).or_insert(0) += storage.total_wave_packets;
    }
    
    log::info!("Wave packets in storage by frequency band:");
    for (band, count) in band_counts {
        log::info!("  {:?}: {} packets", band, count);
    }
    
    // Show player inventories
    for player in ctx.db.player().iter() {
        let player_storage: Vec<_> = ctx.db.wave_packet_storage()
            .iter()
            .filter(|s| s.owner_type == "player" && s.owner_id == player.player_id)
            .collect();
        
        if !player_storage.is_empty() {
            log::info!("Player '{}' inventory:", player.name);
            for storage in player_storage {
                log::info!("  {:?} band: {} packets", 
                    storage.frequency_band, 
                    storage.total_wave_packets
                );
            }
        }
    }
    
    log::info!("=== DEBUG_WAVE_PACKET_STATUS END ===");
    Ok(())
}