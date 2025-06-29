// SYSTEM Server - Quantum Metaverse with Quanta Processing
// Single file architecture for clean compilation

use spacetimedb::{Identity, ReducerContext, Table, Timestamp, SpacetimeType};
use std::f32::consts::PI;

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
    pub frequency: f32,      // 0.0-1.0 (maps to color spectrum)
    pub resonance: f32,      // 0.0-1.0 (stability/purity)
    pub flux_pattern: u16,   // Bit pattern for unique variations
}

impl QuantaSignature {
    pub fn calculate_hash(&self) -> u32 {
        let freq_bits = (self.frequency * 1000.0) as u32;
        let res_bits = (self.resonance * 100.0) as u32;
        (freq_bits << 16) | (res_bits << 8) | (self.flux_pattern as u32 & 0xFF)
    }
    
    pub fn get_frequency_band(&self) -> FrequencyBand {
        match self.frequency {
            f if f < 0.15 => FrequencyBand::Infrared,
            f if f < 0.3 => FrequencyBand::Red,
            f if f < 0.4 => FrequencyBand::Orange,
            f if f < 0.5 => FrequencyBand::Yellow,
            f if f < 0.65 => FrequencyBand::Green,
            f if f < 0.8 => FrequencyBand::Blue,
            f if f < 0.95 => FrequencyBand::Violet,
            _ => FrequencyBand::Ultraviolet,
        }
    }
    
    pub fn to_color_string(&self) -> String {
        match self.get_frequency_band() {
            FrequencyBand::Infrared => "Deep Red",
            FrequencyBand::Red => "Red",
            FrequencyBand::Orange => "Orange",
            FrequencyBand::Yellow => "Yellow",
            FrequencyBand::Green => "Green",
            FrequencyBand::Blue => "Blue",
            FrequencyBand::Violet => "Violet",
            FrequencyBand::Ultraviolet => "Ultra Violet",
        }.to_string()
    }
}

#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum FrequencyBand {
    Infrared,    // 0.0-0.15  (Deep Red)
    Red,         // 0.15-0.3  (Red)
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
    pub signature: QuantaSignature,
    pub quanta_amount: u32,
    pub creation_time: u64,
    pub lifetime_ms: u32,
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
pub fn register(ctx: &ReducerContext, username: String, password: String) -> Result<(), String> {
    if username.len() < 3 || username.len() > 20 {
        return Err("Username must be between 3 and 20 characters".to_string());
    }
    
    if password.len() < 6 {
        return Err("Password must be at least 6 characters".to_string());
    }
    
    if ctx.db.user_account().username().find(&username).is_some() {
        return Err("Username already taken".to_string());
    }
    
    let password_hash = hash_password(&password);
    
    ctx.db.user_account().insert(UserAccount {
        account_id: 0,
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
    
    if let Some(existing_link) = ctx.db.account_identity()
        .iter()
        .find(|link| link.account_id == account.account_id) {
        ctx.db.account_identity().delete(existing_link);
    }
    
    ctx.db.account_identity().insert(AccountIdentity {
        identity: ctx.sender,
        account_id: account.account_id,
    });
    
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
    
    if let Some(existing_player) = ctx.db.player().iter()
        .find(|p| p.identity == ctx.sender) {
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
    
    if let Some(logged_out) = ctx.db.logged_out_player().identity().find(&ctx.sender) {
        ctx.db.player().insert(Player {
            identity: ctx.sender,
            player_id: logged_out.player_id,
            name: player_name.clone(),
            current_world: WorldCoords { x: 0, y: 0, z: 0 },
            position: DbVector3::zero(),
            rotation: DbVector3::zero(),
            last_update: ctx.timestamp,
        });
        
        ctx.db.logged_out_player().delete(logged_out);
        log::info!("Player '{}' restored for account: {}", player_name, account.username);
        return Ok(());
    }
    
    ctx.db.player().insert(Player {
        identity: ctx.sender,
        player_id: 0,
        name: player_name.clone(),
        current_world: WorldCoords { x: 0, y: 0, z: 0 },
        position: DbVector3::zero(),
        rotation: DbVector3::zero(),
        last_update: ctx.timestamp,
    });
    
    log::info!("New player '{}' created for account: {}", player_name, account.username);
    Ok(())
}

#[spacetimedb::reducer]
pub fn update_player_position(
    ctx: &ReducerContext,
    position: DbVector3,
    rotation: DbVector3,
) -> Result<(), String> {
    let sender_identity = ctx.sender;
    
    if let Some(player) = ctx.db.player().identity().find(&sender_identity) {
        let mut updated_player = player.clone();
        updated_player.position = position;
        updated_player.rotation = rotation;
        updated_player.last_update = ctx.timestamp;
        
        ctx.db.player().delete(player);
        ctx.db.player().insert(updated_player);
    } else {
        log::warn!("Attempted to update position for non-existent player with identity: {:?}", sender_identity);
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

fn cleanup_expired_quanta_orbs(ctx: &ReducerContext) -> Result<(), String> {
    let current_time = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    let expired_orbs: Vec<_> = ctx.db.quanta_orb()
        .iter()
        .filter(|orb| current_time > orb.creation_time + orb.lifetime_ms as u64)
        .collect();
    
    let expired_count = expired_orbs.len();
    
    for orb in expired_orbs {
        ctx.db.quanta_orb().delete(orb);
    }
    
    if expired_count > 0 {
        log::info!("Cleaned up {} expired quanta orbs", expired_count);
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
    
    let signature = QuantaSignature {
        frequency: generate_frequency_for_shell(world.shell_level, seed),
        resonance: 0.5 + (simple_random(seed.wrapping_add(1)) * 0.5),
        flux_pattern: (simple_random(seed.wrapping_add(2)) * 65535.0) as u16,
    };
    
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
        signature,
        quanta_amount: 10 + (circuit.qubit_count as u32 * 5),
        creation_time: timestamp_ms,
        lifetime_ms: 30000,
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
    
    add_quanta_to_storage(
        ctx,
        "player".to_string(),
        player_id,
        orb.signature,
        orb.quanta_amount,
        world.shell_level,
    )?;
    
    // Store values we need for logging before deleting the orb
    let orb_amount = orb.quanta_amount;
    let orb_signature = orb.signature;
    
    ctx.db.quanta_orb().delete(orb);
    
    log::info!(
        "Player {} collected {} quanta of {} (freq: {:.2})",
        player.name,
        orb_amount,
        orb_signature.to_color_string(),
        orb_signature.frequency
    );
    
    Ok(())
}

#[spacetimedb::reducer]
pub fn transfer_quanta(
    ctx: &ReducerContext,
    from_player_id: u64,
    to_player_id: u64,
    frequency_band: FrequencyBand,
    amount: u32,
) -> Result<(), String> {
    let from_player = ctx.db.player()
        .player_id()
        .find(&from_player_id)
        .ok_or("From player not found")?;
        
    if from_player.identity != ctx.sender {
        return Err("Not your player".to_string());
    }
    
    let to_player = ctx.db.player()
        .player_id()
        .find(&to_player_id)
        .ok_or("To player not found")?;
    
    let from_storage = ctx.db.quanta_storage()
        .iter()
        .find(|s| s.owner_type == "player" && 
                  s.owner_id == from_player_id && 
                  s.frequency_band == frequency_band)
        .ok_or("You don't have any quanta in this frequency band")?;
    
    if from_storage.total_quanta < amount {
        return Err(format!("Insufficient quanta. You have {} but tried to transfer {}", 
                          from_storage.total_quanta, amount));
    }
    
    deduct_quanta_from_storage(ctx, &from_storage, amount)?;
    
    if let Some(sample) = from_storage.signature_samples.first() {
        add_quanta_to_storage(
            ctx,
            "player".to_string(),
            to_player_id,
            sample.signature,
            amount,
            sample.source_shell,
        )?;
    }
    
    log::info!(
        "Player {} transferred {} {} quanta to player {}",
        from_player.name,
        amount,
        format!("{:?}", frequency_band),
        to_player.name
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

fn deduct_quanta_from_storage(
    ctx: &ReducerContext,
    storage: &QuantaStorage,
    amount: u32,
) -> Result<(), String> {
    let mut updated_storage = storage.clone();
    updated_storage.total_quanta -= amount;
    
    let ratio = updated_storage.total_quanta as f32 / storage.total_quanta as f32;
    for sample in &mut updated_storage.signature_samples {
        sample.amount = (sample.amount as f32 * ratio) as u32;
    }
    
    updated_storage.signature_samples.retain(|s| s.amount > 0);
    
    updated_storage.last_update = ctx.timestamp
        .duration_since(Timestamp::UNIX_EPOCH)
        .expect("Valid timestamp")
        .as_millis() as u64;
    
    ctx.db.quanta_storage().delete(storage.clone());
    
    if updated_storage.total_quanta > 0 {
        ctx.db.quanta_storage().insert(updated_storage);
    }
    
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

fn generate_frequency_for_shell(shell_level: u8, seed: u64) -> f32 {
    let base_freq = match shell_level {
        0 => 0.5,   // Center world: middle spectrum (green)
        1 => 0.25,  // Shell 1: red spectrum
        2 => 0.75,  // Shell 2: blue spectrum
        3 => 0.4,   // Shell 3: orange/yellow
        4 => 0.6,   // Shell 4: green/blue
        5 => 0.9,   // Shell 5: violet/UV
        _ => 0.5,   // Default to middle
    };
    
    let variance = 0.15;
    let random_offset = (simple_random(seed) - 0.5) * variance;
    let freq = base_freq + random_offset;
    freq.clamp(0.0, 1.0)
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
pub fn debug_reset_world(ctx: &ReducerContext) -> Result<(), String> {
    log::warn!("RESETTING WORLD DATA!");
    
    // Delete all worlds
    for world in ctx.db.world().iter() {
        ctx.db.world().delete(world);
    }
    
    // Delete all circuits
    for circuit in ctx.db.world_circuit().iter() {
        ctx.db.world_circuit().delete(circuit);
    }
    
    // Delete all game settings
    for setting in ctx.db.game_settings().iter() {
        ctx.db.game_settings().delete(setting);
    }
    
    // Delete all quanta orbs
    for orb in ctx.db.quanta_orb().iter() {
        ctx.db.quanta_orb().delete(orb);
    }
    
    log::info!("World data cleared. Run __setup__ or debug_init_world to reinitialize.");
    Ok(())
}

#[spacetimedb::reducer]
pub fn debug_init_world(ctx: &ReducerContext) -> Result<(), String> {
    init_game_world(ctx)?;
    log::info!("World initialized via debug command");
    Ok(())
}

#[spacetimedb::reducer]
pub fn debug_quanta_status(ctx: &ReducerContext) -> Result<(), String> {
    let orb_count = ctx.db.quanta_orb().iter().count();
    let storage_count = ctx.db.quanta_storage().iter().count();
    
    log::info!("=== QUANTA SYSTEM STATUS ===");
    log::info!("Active quanta orbs: {}", orb_count);
    log::info!("Storage entries: {}", storage_count);
    
    let mut orbs_by_world = std::collections::HashMap::new();
    for orb in ctx.db.quanta_orb().iter() {
        *orbs_by_world.entry(orb.world_coords).or_insert(0) += 1;
    }
    
    for (coords, count) in orbs_by_world {
        log::info!("  World ({},{},{}): {} orbs", coords.x, coords.y, coords.z, count);
    }
    
    let mut quanta_by_band = std::collections::HashMap::new();
    for storage in ctx.db.quanta_storage().iter() {
        *quanta_by_band.entry(storage.frequency_band).or_insert(0) += storage.total_quanta;
    }
    
    log::info!("\nQuanta stored by frequency band:");
    for (band, total) in quanta_by_band {
        log::info!("  {:?}: {} quanta", band, total);
    }
    
    Ok(())
}

#[spacetimedb::reducer]  
pub fn debug_test_quanta_emission(ctx: &ReducerContext) -> Result<(), String> {
    let center_coords = WorldCoords { x: 0, y: 0, z: 0 };
    let circuit_position = DbVector3::new(0.0, 100.0, 0.0);
    
    for _i in 0..5 {
        emit_quanta_orb(ctx, center_coords, circuit_position)?;
    }
    
    log::info!("DEBUG: Emitted 5 test quanta orbs in center world");
    Ok(())
}

#[spacetimedb::reducer]
pub fn debug_give_quanta(
    ctx: &ReducerContext,
    player_id: u64,
    frequency: f32,
    amount: u32,
) -> Result<(), String> {
    let player = ctx.db.player()
        .player_id()
        .find(&player_id)
        .ok_or("Player not found")?;
    
    let signature = QuantaSignature {
        frequency: frequency.clamp(0.0, 1.0),
        resonance: 0.8,
        flux_pattern: 42,
    };
    
    add_quanta_to_storage(
        ctx,
        "player".to_string(),
        player_id,
        signature,
        amount,
        0,
    )?;
    
    log::info!(
        "DEBUG: Gave {} {} quanta (freq: {}) to player {}",
        amount,
        signature.to_color_string(),
        frequency,
        player.name
    );
    
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