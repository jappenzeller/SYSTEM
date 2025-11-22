#!/usr/bin/env python3
"""
Apply transfer batching modifications to lib.rs
"""

def main():
    input_file = "SYSTEM-server/src/lib.rs"
    output_file = "SYSTEM-server/src/lib.rs"

    with open(input_file, 'r', encoding='utf-8') as f:
        lines = f.readlines()

    # Find the line with "/// Initiate energy packet transfer"
    insert_pos = None
    for i, line in enumerate(lines):
        if "/// Initiate energy packet transfer from player to storage device" in line:
            insert_pos = i
            break

    if insert_pos is None:
        print("ERROR: Could not find insertion point")
        return

    # Helper function to insert
    helper_function = '''/// Helper function to split large transfer compositions into batches
/// Each batch has max 5 packets per frequency and max 30 packets total
fn create_transfer_batches(composition: &[WavePacketSample]) -> Vec<Vec<WavePacketSample>> {
    const MAX_PER_FREQUENCY: u32 = 5;
    const MAX_TOTAL_PER_BATCH: u32 = 30;

    let mut batches: Vec<Vec<WavePacketSample>> = Vec::new();
    let mut current_batch: Vec<WavePacketSample> = Vec::new();
    let mut current_batch_total: u32 = 0;

    for sample in composition {
        let mut remaining = sample.count;

        while remaining > 0 {
            let can_add_by_frequency = MAX_PER_FREQUENCY.min(remaining);
            let can_add_by_total = MAX_TOTAL_PER_BATCH - current_batch_total;
            let to_add = can_add_by_frequency.min(can_add_by_total);

            if to_add == 0 {
                batches.push(current_batch);
                current_batch = Vec::new();
                current_batch_total = 0;
                continue;
            }

            current_batch.push(WavePacketSample {
                frequency: sample.frequency,
                amplitude: sample.amplitude,
                phase: sample.phase,
                count: to_add,
            });

            current_batch_total += to_add;
            remaining -= to_add;
        }
    }

    if !current_batch.is_empty() {
        batches.push(current_batch);
    }

    batches
}

'''

    # Insert helper function
    lines.insert(insert_pos, helper_function)

    # Update the doc comment for initiate_transfer
    for i in range(insert_pos, min(insert_pos + 60, len(lines))):
        if "/// Routes through nearest energy spires" in lines[i]:
            lines[i] = "/// Routes through nearest energy spires\n/// AUTO-BATCHES large requests: max 5 per frequency, 30 total per batch\n"
            break

    # Find and replace the validation section
    # Look for "// Validate composition (max 5 per frequency, 30 total)"
    validation_start = None
    for i in range(insert_pos, min(insert_pos + 100, len(lines))):
        if "// Validate composition (max 5 per frequency, 30 total)" in lines[i]:
            validation_start = i
            break

    if validation_start:
        # Find the end of validation (line before "// Get player")
        validation_end = None
        for i in range(validation_start, min(validation_start + 20, len(lines))):
            if "// Get player" in lines[i]:
                validation_end = i
                break

        if validation_end:
            # Replace validation with batching logic
            new_logic = '''    // Calculate total for logging
    let total_requested: u32 = composition.iter().map(|s| s.count).sum();

    // AUTO-BATCH: Split large requests into multiple transfers
    let batches = create_transfer_batches(&composition);
    log::info!("Total packets: {}, split into {} batches", total_requested, batches.len());

'''
            # Remove old lines and insert new
            del lines[validation_start:validation_end]
            lines.insert(validation_start, new_logic)

    # Write back
    with open(output_file, 'w', encoding='utf-8') as f:
        f.writelines(lines)

    print(f"Successfully modified {output_file}")
    print("- Added create_transfer_batches() helper function")
    print("- Replaced validation logic with auto-batching")
    print("\nNOTE: You still need to manually wrap the transfer creation in a loop!")
    print("Wrap everything from 'Check inventory' to 'ctx.db.packet_transfer().insert(transfer)' in:")
    print("  for (batch_index, batch_composition) in batches.iter().enumerate() { ... }")

if __name__ == "__main__":
    main()
