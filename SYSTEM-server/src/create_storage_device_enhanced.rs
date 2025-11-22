/// Create storage device for player
/// Limited to 10 devices per player
#[spacetimedb::reducer]
pub fn create_storage_device(ctx: &ReducerContext, x: f32, y: f32, z: f32, device_name: String) -> Result<(), String> {
    log::info!("════════════════════════════════════════════════════════════");
    log::info!("CREATE_STORAGE_DEVICE REDUCER CALLED");
    log::info!("════════════════════════════════════════════════════════════");
    log::info!("Parameters:");
    log::info!("  Position: ({}, {}, {})", x, y, z);
    log::info!("  Device Name: '{}'", device_name);
    log::info!("  Sender Identity: {:?}", ctx.sender);

    // Find player by identity
    log::info!("Step 1: Finding player by identity...");
    let player = match ctx.db.player().identity().find(&ctx.sender) {
        Some(p) => {
            log::info!("  ✅ Player found: ID={}, Name='{}'", p.player_id, p.name);
            log::info!("  Player World: ({},{},{})", p.current_world.x, p.current_world.y, p.current_world.z);
            p
        },
        None => {
            log::error!("  ❌ Player not found for identity {:?}", ctx.sender);
            return Err("Player not found".to_string());
        }
    };

    // Check 10 device limit
    log::info!("Step 2: Checking device limit (max 10 per player)...");
    let mut device_count = 0;
    for device in ctx.db.storage_device().iter() {
        if device.owner_player_id == player.player_id {
            device_count += 1;
        }
    }
    log::info!("  Current device count: {}/10", device_count);

    if device_count >= 10 {
        log::error!("  ❌ Device limit reached! Player {} already has 10 devices", player.player_id);
        return Err("Cannot create more than 10 storage devices per player".to_string());
    }

    // Create device
    log::info!("Step 3: Creating StorageDevice struct...");
    let device = StorageDevice {
        device_id: 0, // auto_inc
        owner_player_id: player.player_id,
        world_coords: player.current_world,
        position: DbVector3 { x, y, z },
        device_name: device_name.clone(),
        capacity_per_frequency: 1000,  // 1000 per frequency, 6000 total
        stored_composition: Vec::new(),  // Empty on creation
        created_at: ctx.timestamp,
    };

    // Insert into database
    log::info!("Step 4: Inserting into storage_device table...");
    let inserted = ctx.db.storage_device().insert(device);

    log::info!("════════════════════════════════════════════════════════════");
    log::info!("✅ STORAGE DEVICE CREATED SUCCESSFULLY");
    log::info!("════════════════════════════════════════════════════════════");
    log::info!("Device ID: {}", inserted.device_id);
    log::info!("Device Name: '{}'", device_name);
    log::info!("Owner: {} (player_id={})", player.name, player.player_id);
    log::info!("Position: ({:.2}, {:.2}, {:.2})", x, y, z);
    log::info!("World: ({},{},{})", player.current_world.x, player.current_world.y, player.current_world.z);
    log::info!("Total devices for player: {}", device_count + 1);
    log::info!("════════════════════════════════════════════════════════════");

    Ok(())
}
