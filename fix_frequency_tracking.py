#!/usr/bin/env python3
"""
Fix per-frequency tracking in create_transfer_batches
"""

def main():
    input_file = "SYSTEM-server/src/lib.rs"
    
    with open(input_file, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Fix 1: Add HashMap declaration after line with current_batch_total
    old_code_1 = """    let mut batches: Vec<Vec<WavePacketSample>> = Vec::new();
    let mut current_batch: Vec<WavePacketSample> = Vec::new();
    let mut current_batch_total: u32 = 0;

    for sample in composition {"""
    
    new_code_1 = """    let mut batches: Vec<Vec<WavePacketSample>> = Vec::new();
    let mut current_batch: Vec<WavePacketSample> = Vec::new();
    let mut current_batch_total: u32 = 0;
    let mut freq_count_in_batch: std::collections::HashMap<i32, u32> = std::collections::HashMap::new();

    for sample in composition {"""
    
    if old_code_1 in content:
        content = content.replace(old_code_1, new_code_1, 1)
        print("[OK] Added frequency tracking HashMap")
    else:
        print("[FAIL] Could not find HashMap insertion point")
        return
    
    # Fix 2: Replace constraint calculation
    old_code_2 = """        while remaining > 0 {
            let can_add_by_frequency = MAX_PER_FREQUENCY.min(remaining);
            let can_add_by_total = MAX_TOTAL_PER_BATCH - current_batch_total;
            let to_add = can_add_by_frequency.min(can_add_by_total);"""
    
    new_code_2 = """        while remaining > 0 {
            // Check how much of this frequency is already in the current batch
            let freq_int = (sample.frequency * 100.0).round() as i32;
            let freq_in_batch = freq_count_in_batch.get(&freq_int).copied().unwrap_or(0);
            
            // Calculate how much we can add (respecting per-frequency limit)
            let can_add_by_frequency = MAX_PER_FREQUENCY.saturating_sub(freq_in_batch).min(remaining);
            let can_add_by_total = MAX_TOTAL_PER_BATCH - current_batch_total;
            let to_add = can_add_by_frequency.min(can_add_by_total);"""
    
    if old_code_2 in content:
        content = content.replace(old_code_2, new_code_2, 1)
        print("[OK] Updated constraint calculation")
    else:
        print("[FAIL] Could not find constraint calculation")
        return
    
    # Fix 3: Update frequency counter after push
    old_code_3 = """            current_batch.push(WavePacketSample {
                frequency: sample.frequency,
                amplitude: sample.amplitude,
                phase: sample.phase,
                count: to_add,
            });

            current_batch_total += to_add;
            remaining -= to_add;"""
    
    new_code_3 = """            current_batch.push(WavePacketSample {
                frequency: sample.frequency,
                amplitude: sample.amplitude,
                phase: sample.phase,
                count: to_add,
            });

            current_batch_total += to_add;
            *freq_count_in_batch.entry(freq_int).or_insert(0) += to_add;
            remaining -= to_add;"""
    
    if old_code_3 in content:
        content = content.replace(old_code_3, new_code_3, 1)
        print("[OK] Added frequency counter update")
    else:
        print("[FAIL] Could not find frequency counter update point")
        return
    
    # Fix 4: Clear frequency counter when flushing batch
    old_code_4 = """            if to_add == 0 {
                batches.push(current_batch);
                current_batch = Vec::new();
                current_batch_total = 0;
                continue;
            }"""
    
    new_code_4 = """            if to_add == 0 {
                batches.push(current_batch);
                current_batch = Vec::new();
                current_batch_total = 0;
                freq_count_in_batch.clear();
                continue;
            }"""
    
    if old_code_4 in content:
        content = content.replace(old_code_4, new_code_4, 1)
        print("[OK] Added frequency counter clear on batch flush")
    else:
        print("[FAIL] Could not find batch flush point")
        return
    
    # Write back
    with open(input_file, 'w', encoding='utf-8') as f:
        f.write(content)
    
    print(f"\n[SUCCESS] Fixed per-frequency tracking in create_transfer_batches()")
    print("- Added HashMap to track frequencies in current batch")
    print("- Modified constraint calculation to respect per-frequency limits")
    print("- Counter updated when adding packets")
    print("- Counter cleared when flushing batch")
    print("\nNow 10 red packets will split into 2 batches of 5 each!")

if __name__ == "__main__":
    main()
