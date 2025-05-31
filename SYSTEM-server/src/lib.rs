pub mod math;

use math::DbVector2;

use spacetimedb::{Identity, SpacetimeType, ReducerContext, Timestamp,Table,ScheduleAt };
use spacetimedb::rand::Rng;
use std::time::Duration;

/// A single row in `tick_timer` tells SpacetimeDB to call `tick` at a fixed interval.
/// The `scheduled(tick)` attribute means “whenever a new row is inserted here,
/// schedule the `tick` reducer according to its `scheduled_at` field.”
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
    
    // Insert a single TickTimer row with an Interval of 50 ms
    ctx.db.tick_timer().try_insert(TickTimer {
        scheduled_id: 0, // auto_inc will overwrite
        scheduled_at: ScheduleAt::Interval(Duration::from_millis(50).into()),
    })?;
    Ok(())
}

#[spacetimedb::reducer]
pub fn tick(ctx: &ReducerContext, _timer: TickTimer) -> Result<(), String> {


    Ok(())
}

#[spacetimedb::reducer(client_connected)]
pub fn connect(ctx: &ReducerContext) -> Result<(), String> {
    // 1) If the user was in `logged_out_player`, move them back to `player`.
    log::debug!("checking logged out");
    if let Some(player) = ctx
        .db
        .logged_out_player()
        .identity()
        .find(&ctx.sender)
        .clone()
    {
        // Delete from logged_out_player and re-insert into player
        ctx.db
            .logged_out_player()
            .identity()
            .delete(&ctx.sender);
        ctx.db.player().insert(player);
        return Ok(());
    }

    // 2) If a Player row for this identity already exists, do nothing.
    if ctx.db.player().identity().find(&ctx.sender).is_some() {
        // Log for debugging—but do not return Err, just quietly skip insertion:
        log::debug!(
            "connect reducer: Player row already exists for identity {}. Skipping insert.",
            ctx.sender
        );
        return Ok(());
    }

    // 3) Otherwise, insert a brand‐new Player row
    ctx.db.player().try_insert(Player {
        identity: ctx.sender,
        player_id: 0,               // auto_inc will generate a new ID
        name: String::new(),        // or some default placeholder
    })?;

    Ok(())
}


#[spacetimedb::reducer(client_disconnected)]
pub fn disconnect(ctx: &ReducerContext) -> Result<(), String> {
    let player = ctx
        .db
        .player()
        .identity()
        .find(&ctx.sender)
        .ok_or("Player not found")?;
    let player_id = player.player_id;
    ctx.db.logged_out_player().insert(player);
    ctx.db.player().identity().delete(&ctx.sender);

    Ok(())
}


#[spacetimedb::reducer]
pub fn enter_game(ctx: &ReducerContext, name: String) -> Result<(), String> {
    log::info!("Creating player with name {}", name);
    let mut player: Player = ctx.db.player().identity().find(ctx.sender).ok_or("")?;
    let player_id = player.player_id;
    player.name = name;
    ctx.db.player().identity().update(player);

    Ok(())
}

