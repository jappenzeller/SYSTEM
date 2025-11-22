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
