use spacetimedb::{
    log, Identity, ReducerContext, SpacetimeType, Table, Timestamp,
};
use std::collections::HashMap;
use std::sync::{Mutex, OnceLock};
use std::f32::consts::PI;

#[cfg(test)]
pub mod test_api;

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
#[spacetimedb::table(name = session_result)]
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
    
    #[unique]
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
    
    pub fn to_color_string(&self) -> String {
        let band = get_frequency_band(self.frequency);
        format!("{:?}", band)
    }
}

#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq)]
pub struct WavePacketSample {
    pub signature: WavePacketSignature,
    pub amount: u32,
}

#[derive(SpacetimeType, Clone, Copy, Debug, PartialEq)]
pub enum CrystalType {
    Red,
    Green,
    Blue,
}

impl CrystalType {
    pub fn get_frequency(&self) -> f32 {
        match self {
            CrystalType::Red => 0.2,    // Middle of red band
            CrystalType::Green => 0.575, // Middle of green band
            CrystalType::Blue => 0.725,  // Middle of blue band
        }
    }
}

#[derive(SpacetimeType, Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum FrequencyBand {
    Red,
    Yellow,
    Green,
    Cyan,
    Blue,
    Magenta,
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

fn generate_session_token(account_id: u64, identity: &Identity) -> String {
    // Simple token generation - in production use proper crypto
    use std::time::{SystemTime, UNIX_EPOCH};
    let timestamp = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap()
        .as_secs();
    
    format!("session_{}_{}_{}_{}", 
        account_id, 
        identity, 
        timestamp,
        timestamp % 10000
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
    // Validate username
    if username.is_empty() || username.len() < 3 || username.len() > 20 {
        return Err("Username must be 3-20 characters".to_string());
    }
    
    // Validate display name
    if display_name.is_empty() || display_name.len() < 3 || display_name.len() > 20 {
        return Err("Display name must be 3-20 characters".to_string());
    }
    
    // Validate PIN (4 digits)
    if pin.len() != 4 || !pin.chars().all(|c| c.is_numeric()) {
        return Err("PIN must be exactly 4 digits".to_string());
    }
    
    // Check if username already exists
    if ctx.db.account().username().find(&username).is_some() {
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
    
    ctx.db.account().insert(account);
    
    log::info!("Account registered - Username: {}, Display Name: {}", username, display_name);
    Ok(())
}

#[spacetimedb::reducer]
pub fn login_with_session(
    ctx: &ReducerContext,
    username: String,
    pin: String,
    device_info: String,
) -> Result<(), String> {
    // Find account by username
    let account = ctx.db.account()
        .username()
        .find(&username)
        .ok_or("Invalid username or PIN".to_string())?;
    
    // Verify PIN
    if account.pin_hash != hash_pin(&pin) {
        return Err("Invalid username or PIN".to_string());
    }
    
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    // Update last login
    let mut updated_account = account.clone();
    updated_account.last_login = current_time;
    ctx.db.account().delete(account);
    ctx.db.account().insert(updated_account.clone());
    
    // Generate session token
    let session_token = generate_session_token(updated_account.account_id, &ctx.sender);
    
    // Expire any existing sessions for this identity
    let sessions_to_expire: Vec<_> = ctx.db.player_session()
        .iter()
        .filter(|s| s.identity == ctx.sender)
        .collect();
        
    for session in sessions_to_expire {
        let mut expired_session = session.clone();
        expired_session.is_active = false;
        ctx.db.player_session().delete(session);
        ctx.db.player_session().insert(expired_session);
    }
    
    // Create new session
    let session = PlayerSession {
        session_id: 0,
        account_id: updated_account.account_id,
        identity: ctx.sender,
        session_token: session_token.clone(),
        device_info: device_info.clone(),
        created_at: current_time,
        expires_at: current_time + (7 * 24 * 60 * 60 * 1000), // 7 days
        last_activity: current_time,
        is_active: true,
    };
    
    ctx.db.player_session().insert(session);
    
    // Store session token for client to retrieve
    // Delete any existing result first
    if let Some(existing) = ctx.db.session_result().identity().find(&ctx.sender) {
        ctx.db.session_result().delete(existing);
    }
    
    ctx.db.session_result().insert(SessionResult {
        identity: ctx.sender,
        session_token: session_token.clone(),
        created_at: current_time,
    });
    
    // Handle player creation/restoration
    if let Some(mut player) = ctx.db.player().identity().find(&ctx.sender) {
        // Update existing player
        player.account_id = Some(updated_account.account_id);
        player.name = updated_account.display_name.clone();
        let player_copy = player.clone();
        ctx.db.player().delete(player);
        ctx.db.player().insert(player_copy);
        
        log::info!("Player {} logged in successfully", updated_account.display_name);
    } else {
        // Check for logged out player with this account
        let logged_out_players: Vec<_> = ctx.db.logged_out_player()
            .iter()
            .filter(|p| p.account_id == Some(updated_account.account_id))
            .collect();
            
        if let Some(logged_out) = logged_out_players.first() {
            // Restore player with current identity
            let restored_player = Player {
                player_id: 0, // Get new ID
                identity: ctx.sender, // Use current connection identity
                name: updated_account.display_name.clone(),
                account_id: Some(updated_account.account_id),
                current_world: WorldCoords { x: 0, y: 0, z: 0 },
                position: DbVector3::new(0.0, 0.0, 0.0),
                rotation: DbQuaternion::default(),
                last_update: current_time,
            };
            
            ctx.db.player().insert(restored_player);
            ctx.db.logged_out_player().delete(logged_out.clone());
            
            log::info!("Player {} restored from logged out state", updated_account.display_name);
        } else {
            // Create new player
            let new_player = Player {
                player_id: 0,
                identity: ctx.sender,
                name: updated_account.display_name.clone(),
                account_id: Some(updated_account.account_id),
                current_world: WorldCoords { x: 0, y: 0, z: 0 },
                position: DbVector3::new(0.0, 0.0, 0.0),
                rotation: DbQuaternion::default(),
                last_update: current_time,
            };
            
            ctx.db.player().insert(new_player);
            
            log::info!("New player created for {}", updated_account.display_name);
        }
    }
    
    log::info!("Session created for {} on {}", updated_account.display_name, device_info);
    Ok(())
}

#[spacetimedb::reducer]
pub fn restore_session(
    ctx: &ReducerContext,
    session_token: String,
) -> Result<(), String> {
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    // Find active session
    let sessions: Vec<_> = ctx.db.player_session()
        .iter()
        .filter(|s| s.session_token == session_token && s.is_active)
        .collect();
        
    let session = sessions.first()
        .ok_or("Invalid or expired session".to_string())?;
    
    // Check expiration
    if session.expires_at < current_time {
        // Expire the session
        let mut expired_session = session.clone();
        expired_session.is_active = false;
        ctx.db.player_session().delete(session.clone());
        ctx.db.player_session().insert(expired_session);
        return Err("Session expired".to_string());
    }
    
    // Get account
    let account = ctx.db.account()
        .account_id()
        .find(&session.account_id)
        .ok_or("Account not found".to_string())?;
    
    // Update session with new identity (for reconnection)
    let mut updated_session = session.clone();
    updated_session.identity = ctx.sender;
    updated_session.last_activity = current_time;
    ctx.db.player_session().delete(session.clone());
    ctx.db.player_session().insert(updated_session);
    
    // Restore or create player
    if let Some(mut player) = ctx.db.player().identity().find(&ctx.sender) {
        // Update existing player
        player.account_id = Some(account.account_id);
        player.name = account.display_name.clone();
        let player_copy = player.clone();
        ctx.db.player().delete(player);
        ctx.db.player().insert(player_copy);
    } else {
        // Check for logged out player with this account
        let logged_out_players: Vec<_> = ctx.db.logged_out_player()
            .iter()
            .filter(|p| p.account_id == Some(account.account_id))
            .collect();
            
        if let Some(logged_out) = logged_out_players.first() {
            // Restore from logged out
            let restored_player = Player {
                player_id: 0,
                identity: ctx.sender,
                name: account.display_name.clone(),
                account_id: Some(account.account_id),
                current_world: WorldCoords { x: 0, y: 0, z: 0 },
                position: DbVector3::new(0.0, 0.0, 0.0),
                rotation: DbQuaternion::default(),
                last_update: current_time,
            };
            
            ctx.db.player().insert(restored_player);
            ctx.db.logged_out_player().delete(logged_out.clone());
        } else {
            // Create new player
            let new_player = Player {
                player_id: 0,
                identity: ctx.sender,
                name: account.display_name.clone(),
                account_id: Some(account.account_id),
                current_world: WorldCoords { x: 0, y: 0, z: 0 },
                position: DbVector3::new(0.0, 0.0, 0.0),
                rotation: DbQuaternion::default(),
                last_update: current_time,
            };
            
            ctx.db.player().insert(new_player);
        }
    }
    
    log::info!("Session restored for {}", account.display_name);
    Ok(())
}

#[spacetimedb::reducer]
pub fn logout(ctx: &ReducerContext) -> Result<(), String> {
    // Invalidate all sessions for this identity
    let sessions_to_expire: Vec<_> = ctx.db.player_session()
        .iter()
        .filter(|s| s.identity == ctx.sender)
        .collect();
        
    for session in sessions_to_expire {
        let mut expired_session = session.clone();
        expired_session.is_active = false;
        ctx.db.player_session().delete(session);
        ctx.db.player_session().insert(expired_session);
    }
    
    // Save player state to logged_out_player
    if let Some(player) = ctx.db.player().identity().find(&ctx.sender) {
        let player_name = player.name.clone(); // Clone before moving
        
        let logged_out = LoggedOutPlayer {
            player_id: player.player_id,
            identity: player.identity,
            name: player.name.clone(),
            account_id: player.account_id,
            logout_time: ctx.timestamp,
        };
        
        ctx.db.logged_out_player().insert(logged_out);
        ctx.db.player().delete(player);
        
        log::info!("Player {} logged out and saved", player_name);
    }
    
    Ok(())
}

// ============================================================================
// Player Management Reducers (Updated)
// ============================================================================

#[spacetimedb::reducer]
pub fn login(
    ctx: &ReducerContext,
    username: String,
    password: String,
) -> Result<(), String> {
    // For backwards compatibility, use the session version
    login_with_session(ctx, username, password, "Unknown".to_string())
}

#[spacetimedb::reducer]
pub fn create_player(ctx: &ReducerContext, name: String) -> Result<(), String> {
    // Check if player already exists
    if ctx.db.player().identity().find(&ctx.sender).is_some() {
        return Err("You already have a player".to_string());
    }
    
    // Check if we can restore from logged out players
    if let Some(logged_out) = ctx.db.logged_out_player().identity().find(&ctx.sender) {
        let restored_player = Player {
            player_id: logged_out.player_id,
            identity: ctx.sender,
            name: logged_out.name.clone(),
            account_id: None,
            current_world: WorldCoords { x: 0, y: 0, z: 0 },
            position: DbVector3::new(0.0, 0.0, 0.0),
            rotation: DbQuaternion::default(),
            last_update: ctx.timestamp
                .duration_since(Timestamp::UNIX_EPOCH)
                .expect("Valid timestamp")
                .as_millis() as u64,
        };
        
        ctx.db.player().insert(restored_player);
        ctx.db.logged_out_player().delete(logged_out);
        
        log::info!("Player {} restored from logout", name);
        return Ok(());
    }
    
    // Create new player
    let player = Player {
        player_id: 0,
        identity: ctx.sender,
        name: name.clone(),
        account_id: None,
        current_world: WorldCoords { x: 0, y: 0, z: 0 },
        position: DbVector3::new(0.0, 0.0, 0.0),
        rotation: DbQuaternion::default(),
        last_update: ctx.timestamp
            .duration_since(Timestamp::UNIX_EPOCH)
            .expect("Valid timestamp")
            .as_millis() as u64,
    };
    
    ctx.db.player().insert(player);
    
    log::info!("New player created: {}", name);
    Ok(())
}

#[spacetimedb::reducer]
pub fn update_player_position(
    ctx: &ReducerContext,
    world_coords: WorldCoords,
    position: DbVector3,
    rotation: DbQuaternion,
) -> Result<(), String> {
    let mut player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    player.current_world = world_coords;
    player.position = position;
    player.rotation = rotation;
    player.last_update = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    let player_copy = player.clone();
    ctx.db.player().delete(player);
    ctx.db.player().insert(player_copy);
    
    Ok(())
}

// ============================================================================
// Crystal System Reducers
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
    
    // Check if player already has a crystal
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

// ============================================================================
// Wave Packet Emission
// ============================================================================

#[spacetimedb::reducer]
pub fn emit_wave_packet_orb(
    ctx: &ReducerContext,
    world_coords: WorldCoords,
    source_position: DbVector3,
) -> Result<(), String> {
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    let _world = ctx.db.world()
        .iter()
        .find(|w| w.world_coords == world_coords)
        .ok_or("World not found")?;
    
    // Import rand::Rng trait for the gen methods
    use rand::Rng;
    let mut rng = ctx.rng();
    
    // Generate random orb position around the circuit
    let angle = rng.gen::<f32>() * 2.0 * PI;
    let distance = 50.0 + rng.gen::<f32>() * 100.0; // 50-150 units from center
    let height = 280.0 + rng.gen::<f32>() * 40.0;  // 280-320 units high
    
    let position = DbVector3::new(
        source_position.x + angle.cos() * distance,
        height,
        source_position.z + angle.sin() * distance,
    );
    
    // Random velocity
    let velocity_angle = rng.gen::<f32>() * 2.0 * PI;
    let speed = 5.0 + rng.gen::<f32>() * 10.0; // 5-15 units/second
    
    let velocity = DbVector3::new(
        velocity_angle.cos() * speed,
        rng.gen::<f32>() * 2.0 - 1.0, // -1 to 1 vertical
        velocity_angle.sin() * speed,
    );
    
    // Generate wave packet composition
    let mut wave_packet_composition = Vec::new();
    let packet_count = 3 + rng.gen::<u8>() % 5; // 3-7 different frequencies
    
    for _ in 0..packet_count {
        let frequency = rng.gen::<f32>();
        let resonance = 0.8 + rng.gen::<f32>() * 0.2;
        let flux_pattern = rng.gen::<u16>();
        let amount = 1 + rng.gen::<u32>() % 5; // 1-5 packets of each frequency
        
        let signature = WavePacketSignature::new(frequency, resonance, flux_pattern);
        wave_packet_composition.push(WavePacketSample { signature, amount });
    }
    
    // Calculate total packets
    let total_wave_packets = wave_packet_composition.iter()
        .map(|s| s.amount)
        .sum();
    
    let orb = WavePacketOrb {
        orb_id: 0,
        world_coords,
        position,
        velocity,
        wave_packet_composition,
        total_wave_packets,
        creation_time: current_time,
        lifetime_ms: 300000, // 5 minutes
        last_dissipation: current_time,
    };
    
    ctx.db.wave_packet_orb().insert(orb);
    
    log::info!(
        "Emitted wave packet orb at ({:.1}, {:.1}, {:.1}) with {} total packets",
        position.x, position.y, position.z, total_wave_packets
    );
    
    Ok(())
}

// ============================================================================
// Mining System Reducers
// ============================================================================

#[spacetimedb::reducer]
pub fn start_mining(
    ctx: &ReducerContext,
    orb_id: u64,
) -> Result<(), String> {
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    let mut mining_state = get_mining_state().lock().unwrap();
    
    // Check if already mining
    if mining_state.contains_key(&player.player_id) {
        return Err("You are already mining".to_string());
    }
    
    // Find the orb
    let orb = ctx.db.wave_packet_orb()
        .orb_id()
        .find(&orb_id)
        .ok_or("Orb not found")?;
    
    // Check if player is in same world
    if orb.world_coords != player.current_world {
        return Err("Orb is in a different world".to_string());
    }
    
    // Check distance (30 units max)
    let distance = player.position.distance_to(&orb.position);
    if distance > 30.0 {
        return Err("Too far from orb".to_string());
    }
    
    // Get player's crystal
    let crystal = ctx.db.player_crystal()
        .player_id()
        .find(&player.player_id)
        .ok_or("You need a crystal to mine wave packets")?;
    
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    // Create mining session
    let session = MiningSession {
        player_id: player.player_id,
        orb_id,
        crystal_type: crystal.crystal_type,
        started_at: current_time,
        last_packet_time: current_time,
        pending_wave_packets: Vec::new(),
    };
    
    mining_state.insert(player.player_id, session);
    
    log::info!(
        "Player {} started mining orb {} with {:?} crystal",
        player.name, orb_id, crystal.crystal_type
    );
    
    Ok(())
}

#[spacetimedb::reducer]
pub fn stop_mining(ctx: &ReducerContext) -> Result<(), String> {
    let player = ctx.db.player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    
    let mut mining_state = get_mining_state().lock().unwrap();
    
    if mining_state.remove(&player.player_id).is_some() {
        log::info!("Player {} stopped mining", player.name);
        Ok(())
    } else {
        Err("You are not currently mining".to_string())
    }
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
    
    // Check if enough time has passed (2 seconds between extractions)
    if current_time < session.last_packet_time + 2000 {
        return Err("Still extracting previous wave packet".to_string());
    }
    
    // Get the orb
    let orb = ctx.db.wave_packet_orb()
        .orb_id()
        .find(&session.orb_id)
        .ok_or("Orb no longer exists")?;
    
    // Check if orb has packets left
    if orb.total_wave_packets == 0 {
        return Err("Orb is empty".to_string());
    }
    
    // Find compatible wave packets
    let crystal_frequency = session.crystal_type.get_frequency();
    let crystal_band = get_frequency_band(crystal_frequency);
    
    let compatible_samples: Vec<&WavePacketSample> = orb.wave_packet_composition.iter()
        .filter(|sample| {
            let packet_band = get_frequency_band(sample.signature.frequency);
            packet_band == crystal_band && sample.amount > 0
        })
        .collect();
    
    if compatible_samples.is_empty() {
        return Err("No compatible wave packets in this orb".to_string());
    }
    
    // Extract a random compatible packet
    use rand::Rng;
    let sample_index = ctx.rng().gen::<usize>() % compatible_samples.len();
    let sample = compatible_samples[sample_index];
    let signature = sample.signature;
    
    // Generate unique wave packet ID
    let wave_packet_id = current_time * 1000 + ctx.rng().gen::<u64>() % 1000;
    
    // Calculate flight time based on distance
    let distance = player.position.distance_to(&orb.position);
    let flight_time = ((distance / 5.0) * 1000.0) as u64; // 5 units/second
    
    // Add to pending packets
    let pending = PendingWavePacket {
        wave_packet_id,
        signature,
        extracted_at: current_time,
        flight_time,
    };
    
    session.pending_wave_packets.push(pending.clone());
    session.last_packet_time = current_time;
    
    // Update orb - decrease packet count
    let mut updated_orb = orb.clone();
    for sample in &mut updated_orb.wave_packet_composition {
        if sample.signature == signature && sample.amount > 0 {
            sample.amount -= 1;
            break;
        }
    }
    updated_orb.total_wave_packets -= 1;
    
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
    
    // Find the pending wave packet
    let packet_index = session.pending_wave_packets.iter()
        .position(|p| p.wave_packet_id == wave_packet_id)
        .ok_or("Wave packet not found in pending list")?;
    
    let wave_packet = session.pending_wave_packets.remove(packet_index);
    
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
    
    // Check if player is in same world
    if orb.world_coords != player.current_world {
        return Err("Orb is in a different world".to_string());
    }
    
    // Check distance (10 units for direct collection)
    let distance = player.position.distance_to(&orb.position);
    if distance > 10.0 {
        return Err("Too far from orb".to_string());
    }
    
    // Get player's crystal to check compatibility
    let crystal = ctx.db.player_crystal()
        .player_id()
        .find(&player.player_id)
        .ok_or("You need a crystal to collect wave packets")?;
    
    let crystal_band = get_frequency_band(crystal.crystal_type.get_frequency());
    
    // Collect all compatible wave packets
    let mut total_packets = 0u32;
    for sample in &orb.wave_packet_composition {
        let packet_band = get_frequency_band(sample.signature.frequency);
        if packet_band == crystal_band && sample.amount > 0 {
            add_wave_packets_to_storage(
                ctx,
                "player".to_string(),
                player.player_id,
                sample.signature,
                sample.amount,
                orb.world_coords.x.abs() as u8,
            )?;
            total_packets += sample.amount;
        }
    }
    
    // Delete the orb
    ctx.db.wave_packet_orb().delete(orb);
    
    log::info!(
        "Player {} collected orb {} with {} wave packets",
        player.name,
        orb_id,
        total_packets
    );
    
    Ok(())
}

fn add_wave_packets_to_storage(
    ctx: &ReducerContext,
    owner_type: String,
    owner_id: u64,
    signature: WavePacketSignature,
    amount: u32,
    _source_shell: u8,
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
        "Circuit at ({},{},{}) emitted {} orbs",
        circuit.world_coords.x,
        circuit.world_coords.y,
        circuit.world_coords.z,
        circuit.orbs_per_emission
    );
    
    Ok(())
}

fn process_orb_dissipation(ctx: &ReducerContext) -> Result<(), String> {
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    // Dissipate packets every 10 seconds
    let dissipation_interval = 10000u64;
    let dissipation_amount = 1u32;
    
    let orbs_to_update: Vec<_> = ctx.db.wave_packet_orb()
        .iter()
        .filter(|orb| current_time >= orb.last_dissipation + dissipation_interval && orb.total_wave_packets > 0)
        .collect();
    
    for orb in orbs_to_update {
        let orb_id = orb.orb_id; // Save ID before moving
        let mut updated_orb = orb.clone();
        
        // Dissipate packets from random frequencies
        let mut _packets_dissipated = 0u32; // Prefix with underscore
        for _ in 0..dissipation_amount {
            if updated_orb.total_wave_packets == 0 {
                break;
            }
            
            // Find a sample with packets
            let available_samples: Vec<usize> = updated_orb.wave_packet_composition
                .iter()
                .enumerate()
                .filter(|(_, s)| s.amount > 0)
                .map(|(i, _)| i)
                .collect();
            
            if !available_samples.is_empty() {
                use rand::Rng;
                let index = available_samples[ctx.rng().gen::<usize>() % available_samples.len()];
                updated_orb.wave_packet_composition[index].amount -= 1;
                updated_orb.total_wave_packets -= 1;
                _packets_dissipated += 1;
            }
        }
        
        updated_orb.last_dissipation = current_time;
        
        ctx.db.wave_packet_orb().delete(orb);
        
        if updated_orb.total_wave_packets > 0 {
            ctx.db.wave_packet_orb().insert(updated_orb);
        } else {
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
        .filter(|orb| current_time > orb.creation_time + orb.lifetime_ms as u64)
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
        
        let player_name = player.name.clone();
        
        ctx.db.logged_out_player().insert(LoggedOutPlayer {
            identity: player.identity,
            player_id: player.player_id,
            name: player.name.clone(),
            account_id: player.account_id,  // Keep account link
            logout_time: ctx.timestamp,
        });
        
        ctx.db.player().delete(player);
        log::info!("Player {} logged out", player_name);
    }
    
    Ok(())
}

#[spacetimedb::reducer]
pub fn clear_session_result(ctx: &ReducerContext) -> Result<(), String> {
    // Clean up the session result after client has retrieved it
    if let Some(result) = ctx.db.session_result().identity().find(&ctx.sender) {
        ctx.db.session_result().delete(result);
        log::info!("Cleared session result for identity: {}", ctx.sender);
    }
    Ok(())
}

// ============================================================================
// Initialization Reducer (Runs on first publish and when database is cleared)
// ============================================================================

#[spacetimedb::reducer(init)]
pub fn init(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("Initializing game world...");
    
    // Create default game settings
    let settings = vec![
        ("emission_interval_ms", "5000", "Time between wave packet emissions (milliseconds)"),
        ("orbs_per_emission", "3", "Number of orbs emitted per interval"),
        ("orb_lifetime_ms", "300000", "How long orbs last before expiring (5 minutes)"),
        ("dissipation_interval_ms", "10000", "Time between packet dissipation checks"),
        ("dissipation_amount", "1", "Number of packets that dissipate per interval"),
        ("mining_packet_interval_ms", "2000", "Time between packet extractions"),
        ("packet_flight_speed", "5", "Speed of packets traveling to player (units/second)"),
    ];
    
    for (key, value, description) in settings {
        ctx.db.game_settings().insert(GameSettings {
            setting_key: key.to_string(),
            value_type: "integer".to_string(),
            value: value.to_string(),
            description: description.to_string(),
        });
    }
    
    // Create the center world (0, 0, 0)
    let center_world = World {
        world_id: 0,
        world_coords: WorldCoords { x: 0, y: 0, z: 0 },
        world_name: "Center World".to_string(),
        world_type: "core".to_string(),
        shell_level: 0,
    };
    
    ctx.db.world().insert(center_world);
    
    // Create world circuit for center world
    let center_circuit = WorldCircuit {
        world_coords: WorldCoords { x: 0, y: 0, z: 0 },
        circuit_id: 0,
        qubit_count: 2,
        emission_interval_ms: 5000,
        orbs_per_emission: 3,
        last_emission_time: 0,
    };
    
    ctx.db.world_circuit().insert(center_circuit);
    
    log::info!("Game world initialized with center world and default settings");
    Ok(())
}