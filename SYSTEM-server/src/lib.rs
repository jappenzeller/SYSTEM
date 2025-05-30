pub mod math;

use math::DbVector2;

use spacetimedb::{Identity, SpacetimeType, ReducerContext, Timestamp,Table,ScheduleAt };
use spacetimedb::rand::Rng;
use std::time::Duration;


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

    Ok(())
}

#[spacetimedb::reducer(client_connected)]
pub fn connect(ctx: &ReducerContext) -> Result<(), String> {
    if let Some(player) = ctx.db.logged_out_player().identity().find(&ctx.sender) {
        ctx.db.player().insert(player.clone());
        ctx.db
            .logged_out_player()
            .identity()
            .delete(&player.identity);
    } else {
        ctx.db.player().try_insert(Player {
            identity: ctx.sender,
            player_id: 0,
            name: String::new(),
        })?;
    }
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

