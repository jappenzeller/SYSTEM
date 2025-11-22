#!/usr/bin/env python3
"""
Fix pulse error handling to continue on errors instead of stopping all processing
"""

def main():
    input_file = "SYSTEM-server/src/lib.rs"
    
    with open(input_file, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Fix 1: Replace depart_object_to_sphere error propagation
    old_code_1 = """        if transfer.current_leg_type == "PendingAtObject" {
            depart_object_to_sphere(ctx, &transfer)?;
        }"""
    
    new_code_1 = """        if transfer.current_leg_type == "PendingAtObject" {
            // Don't use ? operator - log errors and continue processing other transfers
            if let Err(e) = depart_object_to_sphere(ctx, &transfer) {
                log::error!("[2s Pulse] Failed to depart transfer {} from object to sphere: {}", transfer.transfer_id, e);
                // Continue processing remaining transfers
            }
        }"""
    
    if old_code_1 in content:
        content = content.replace(old_code_1, new_code_1, 1)
        print("[OK] Fixed depart_object_to_sphere error handling")
    else:
        print("[FAIL] Could not find depart_object_to_sphere pattern")
    
    # Fix 2: Replace depart_sphere_to_object error propagation
    old_code_2 = """            // At last sphere, depart to final object
            depart_sphere_to_object(ctx, &transfer)?;"""
    
    new_code_2 = """            // At last sphere, depart to final object
            // Don't use ? operator - log errors and continue processing other transfers
            if let Err(e) = depart_sphere_to_object(ctx, &transfer) {
                log::error!("[2s Pulse] Failed to depart transfer {} from sphere to object: {}", transfer.transfer_id, e);
                // Continue processing remaining transfers
            }"""
    
    if old_code_2 in content:
        content = content.replace(old_code_2, new_code_2, 1)
        print("[OK] Fixed depart_sphere_to_object error handling")
    else:
        print("[FAIL] Could not find depart_sphere_to_object pattern")
    
    # Write back
    with open(input_file, 'w', encoding='utf-8') as f:
        f.write(content)
    
    print(f"\n[SUCCESS] Updated {input_file}")
    print("- Object->Sphere errors now logged and skipped")
    print("- Sphere->Object errors now logged and skipped")
    print("- Other transfers will continue processing")

if __name__ == "__main__":
    main()
