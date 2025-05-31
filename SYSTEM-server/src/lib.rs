pub mod math;

use math::DbVector2;

use spacetimedb::{Identity, SpacetimeType, ReducerContext, Timestamp,Table,ScheduleAt };
use spacetimedb::rand::Rng;
use std::time::Duration;

/// A single row in `tick_timer` tells SpacetimeDB to call `tick` at a fixed interval.
/// The `scheduled(tick)` attribute means "whenever a new row is inserted here,
/// schedule the `tick` reducer according to its `scheduled_at` field."
#[spacetimedb::table(name = tick_timer, scheduled(tick))]
pub struct TickTimer {
    /// auto‐incremented primary key (unused aside from uniqueness)
    #[primary_key]
    #[auto_inc]
    pub scheduled_id: u64,

    /// The `ScheduleAt` value (e.g. `Interval(50 ms)`) defines when/if to run `tick`
    pub scheduled_at: spacetimedb::ScheduleAt,
}

#[spacetimedb::table(name = game_settings, public)]
pub struct GameSettings {
    #[primary_key]
    id: u32,
    tick_ms: u64,
    max_players: u32,
    // … other settings …
}

#[spacetimedb::table(name = player, public)]
#[spacetimedb::table(name = logged_out_player)]
#[derive(Debug, Clone)]
pub struct Player {
    #[primary_key]
    identity: Identity,
    #[unique]
    #[auto_inc]
    player_id: u32,
    name: String,
}

// Note the `init` parameter passed to the reducer macro.
// That indicates to SpacetimeDB that it should be called
// once upon database creation.
#[spacetimedb::reducer(init)]
pub fn init(ctx: &ReducerContext) -> Result<(), String> {
    log::info!("Initializing...");
    if ctx.db.game_settings().iter().next().is_none() {
        ctx.db.game_settings().insert( GameSettings {
            id: 0,
            tick_ms: 100,
            max_players: 32,
        });
    }
    
    // Insert a single TickTimer row with an Interval of 100 ms
    ctx.db.tick_timer().try_insert(TickTimer {
        scheduled_id: 0, // auto_inc will overwrite
        scheduled_at: ScheduleAt::Interval(Duration::from_millis(100).into()),
    })?;
    Ok(())
}

#[spacetimedb::reducer]
pub fn tick(ctx: &ReducerContext, _timer: TickTimer) -> Result<(), String> {
    Ok(())
}

#[spacetimedb::reducer(client_connected)]
pub fn connect(ctx: &ReducerContext) -> Result<(), String> {
    // Only check if the user was previously logged out and restore them
    // DO NOT create new players here - wait for enter_game
    if let Some(player) = ctx
        .db
        .logged_out_player()
        .identity()
        .find(&ctx.sender)
        .clone()
    {
        let player_name = player.name.clone(); // Clone the name before moving
        // Delete from logged_out_player and re-insert into player
        ctx.db
            .logged_out_player()
            .identity()
            .delete(&ctx.sender);
        ctx.db.player().insert(player);
        log::info!("Restored logged out player: {}", player_name);
        return Ok(());
    }

    // If no logged out player found, just log the connection but don't create anything
    log::info!("Client connected: {}. Waiting for enter_game.", ctx.sender);
    Ok(())
}

#[spacetimedb::reducer(client_disconnected)]
pub fn disconnect(ctx: &ReducerContext) -> Result<(), String> {
    // Only move to logged_out_player if they actually have a player row
    if let Some(player) = ctx.db.player().identity().find(&ctx.sender) {
        let player_name = player.name.clone();
        ctx.db.logged_out_player().insert(player);
        ctx.db.player().identity().delete(&ctx.sender);
        log::info!("Player disconnected and moved to logged_out: {}", player_name);
    } else {
        log::info!("Client disconnected without a player row: {}", ctx.sender);
    }
    Ok(())
}

#[spacetimedb::reducer]
pub fn enter_game(ctx: &ReducerContext, name: String) -> Result<(), String> {
    log::info!("enter_game called with name: {}", name);
    
    // Validate the name
    if name.trim().is_empty() {
        return Err("Name cannot be empty".to_string());
    }
    
    if name.len() > 32 {
        return Err("Name must be 32 characters or less".to_string());
    }

    // Check if player already exists (they might have reconnected)
    if let Some(mut player) = ctx.db.player().identity().find(ctx.sender) {
        // Update existing player's name
        player.name = name.clone(); // Clone the name for logging
        let player_name = player.name.clone(); // Clone before moving
        ctx.db.player().identity().update(player);
        log::info!("Updated existing player name to: {}", player_name);
        return Ok(());
    }

    // Create a new player
    let new_player = Player {
        identity: ctx.sender,
        player_id: 0, // auto_inc will generate a new ID
        name,
    };
    
    ctx.db.player().try_insert(new_player.clone())?;
    log::info!("Created new player: {} with ID: {}", new_player.name, new_player.player_id);
    
    Ok(())
}