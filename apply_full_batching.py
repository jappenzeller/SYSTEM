#!/usr/bin/env python3
"""
Complete the batching implementation by wrapping transfer creation in loop
"""

def main():
    input_file = "SYSTEM-server/src/lib.rs"
    
    with open(input_file, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    # Find the start of the section to replace (after batches creation)
    # Look for "// Get player" comment
    start_idx = None
    for i, line in enumerate(lines):
        if i > 3180 and "// Get player" in line and "let player = ctx.db.player()" in lines[i+1]:
            start_idx = i
            break
    
    if start_idx is None:
        print("[FAIL] Could not find '// Get player' section")
        return
    
    # Find the end (the log::info and Ok(()) at the end of initiate_transfer)
    end_idx = None
    for i in range(start_idx, min(start_idx + 120, len(lines))):
        if 'log::info!("Transfer initiated:' in lines[i]:
            # Find the Ok(()) after this
            for j in range(i, min(i + 5, len(lines))):
                if lines[j].strip().startswith("Ok(())"):
                    end_idx = j
                    break
            if end_idx:
                break
    
    if end_idx is None:
        print("[FAIL] Could not find end of initiate_transfer function")
        return
    
    print(f"[INFO] Found section from line {start_idx+1} to {end_idx+1}")
    
    # Read the complete replacement from initiate_transfer_new.rs
    try:
        with open("initiate_transfer_new.rs", 'r', encoding='utf-8') as f:
            new_content = f.read()
        
        # Extract just the function body (skip the fn declaration)
        body_start = new_content.find("log::info!(\"=== INITIATE_TRANSFER START ===\");")
        body_end = new_content.rfind("Ok(())")
        
        if body_start == -1 or body_end == -1:
            print("[FAIL] Could not parse initiate_transfer_new.rs")
            return
        
        # Get the section from "// Get player" to "Ok(())"
        new_body_start = new_content.find("// Get player", body_start)
        new_body = new_content[new_body_start:body_end + len("Ok(())")]
        
        # Convert to lines
        new_lines = new_body.split('\n')
        
        # Add proper indentation (4 spaces for function body)
        new_lines_indented = []
        for line in new_lines:
            if line.strip():  # Non-empty lines
                new_lines_indented.append("    " + line + "\n")
            else:
                new_lines_indented.append("\n")
        
        # Replace the section
        lines[start_idx:end_idx+1] = new_lines_indented
        
        # Write back
        with open(input_file, 'w', encoding='utf-8') as f:
            f.writelines(lines)
        
        print(f"[SUCCESS] Applied complete batching implementation")
        print(f"- Replaced lines {start_idx+1} to {end_idx+1}")
        print(f"- Added batch processing loop")
        print(f"- Each batch creates separate PacketTransfer record")
        
    except FileNotFoundError:
        print("[FAIL] initiate_transfer_new.rs not found")
        return

if __name__ == "__main__":
    main()
