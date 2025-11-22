#!/usr/bin/env python3
"""
Limit Object->Sphere departures to 1 transfer per source object per pulse
"""

def main():
    input_file = "SYSTEM-server/src/lib.rs"
    
    with open(input_file, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Replace the Object->Sphere processing loop
    old_code = """    log::info!("[2s Pulse] Processing Object→Sphere and Sphere→Object departures");

    // Process all transfers pending at source objects for Object→Sphere departure
    for transfer in ctx.db.packet_transfer().iter() {
        if transfer.completed {
            continue;
        }

        if transfer.current_leg_type == "PendingAtObject" {
            // Don't use ? operator - log errors and continue processing other transfers
            if let Err(e) = depart_object_to_sphere(ctx, &transfer) {
                log::error!("[2s Pulse] Failed to depart transfer {} from object to sphere: {}", transfer.transfer_id, e);
                // Continue processing remaining transfers
            }
        }
    }"""
    
    new_code = """    log::info!("[2s Pulse] Processing Object→Sphere and Sphere→Object departures");

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
    }"""
    
    if old_code in content:
        content = content.replace(old_code, new_code, 1)
        print("[OK] Limited Object->Sphere to 1 transfer per source per pulse")
    else:
        print("[FAIL] Could not find Object->Sphere processing loop")
        return
    
    # Write back
    with open(input_file, 'w', encoding='utf-8') as f:
        f.write(content)
    
    print(f"\n[SUCCESS] Fixed pulse transfer rate limiting")
    print("- Added HashSet to track departed sources")
    print("- Skip additional transfers from same source in same pulse")
    print("- Each player can now send 1 transfer per 2-second pulse")
    print("\nResult: 10 red packets (2 batches) will take 4 seconds to depart (2 pulses)")

if __name__ == "__main__":
    main()
