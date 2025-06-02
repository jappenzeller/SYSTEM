# Parameterized Energy System Design Document
**Project**: SYSTEM Quantum Metaverse  
**Version**: 1.0  
**Date**: 2025-06-01  

## Overview

The Parameterized Energy System replaces static, hardcoded energy types with a mathematically-driven system that generates infinite energy variations based on quantum-inspired parameters. This system scales seamlessly from Shell 0 (center world) to Shell 10+ while providing rich mixing mechanics and discovery incentives.

## Core Architecture

### Energy Signature Structure

```rust
#[derive(SpacetimeType, Debug, Clone, Copy, PartialEq)]
pub struct EnergySignature {
    pub frequency: f32,        // 0.0-1.0: Primary frequency (wavelength-like)
    pub amplitude: f32,        // 0.0-1.0: Intensity/power level
    pub phase: f32,           // 0.0-2π: Quantum phase
    pub coherence: f32,       // 0.0-1.0: Purity/stability
    pub entanglement: u8,     // 0-255: Entanglement complexity
    pub resonance_pattern: u32, // Bit pattern for harmonic resonances
}
```

### Parameter Meanings

- **Frequency**: Base energy "color" - lower frequencies are redder, higher are bluer
- **Amplitude**: Energy intensity - affects power output and mixing potential
- **Phase**: Quantum interference patterns - affects mixing compatibility
- **Coherence**: Stability and purity - degrades over time and distance
- **Entanglement**: Complexity level - enables exotic interactions
- **Resonance Pattern**: Harmonic signature - determines mixing resonances

## Energy Classification System

### Dynamic Energy Types

```rust
#[derive(SpacetimeType, Debug, Clone, PartialEq)]
pub struct EnergyType {
    pub signature: EnergySignature,
    pub name: String,           // Generated: "Crimson-Azure Flux"
    pub classification: EnergyClass,
    pub shell_origin: u8,       // Discovery shell
    pub rarity: f32,           // 0.0-1.0 spawn probability
}

#[derive(SpacetimeType, Debug, Clone, PartialEq)]
pub enum EnergyClass {
    Primary,      // 0.0-0.5 complexity: Basic frequencies
    Secondary,    // 0.5-1.0: Simple combinations
    Tertiary,     // 1.0-1.5: Complex combinations  
    Quantum,      // 1.5-2.0: High entanglement
    Exotic,       // 2.0-2.5: Very high parameters
    Legendary,    // 2.5-2.9: Near-perfect parameters
    Artifact,     // 2.9-3.0: Perfect parameters (extremely rare)
}
```

### Complexity Calculation

```
complexity_score = amplitude + coherence + (entanglement / 255.0)
```

Energy class determines:
- Visual effects intensity
- Mixing efficiency
- Market value
- Unlock requirements

## Shell-Based Progression

### Shell Configuration

Each shell level has unique circuit capabilities:

```rust
#[derive(SpacetimeType, Debug, Clone)]
pub struct CircuitConfiguration {
    pub max_qubits: u8,               // Circuit complexity limit
    pub frequency_range: (f32, f32),  // Available frequency spectrum
    pub amplitude_ceiling: f32,       // Maximum power output
    pub coherence_baseline: f32,      // Minimum stability
    pub exotic_unlock_threshold: f32, // When exotic types appear
}
```

### Progression Table

| Shell | Max Qubits | Frequency Range | Amplitude Cap | Coherence Base | Unlocks |
|-------|------------|-----------------|---------------|----------------|---------|
| 0     | 1          | 0.0-0.25        | 0.2           | 1.0            | Primary |
| 1     | 2          | 0.05-0.35       | 0.27          | 0.97           | Secondary |
| 2     | 3          | 0.1-0.45        | 0.33          | 0.94           | Tertiary |
| 3     | 4          | 0.15-0.55       | 0.4           | 0.91           | Quantum |
| 4     | 5          | 0.2-0.65        | 0.47          | 0.88           | Exotic |
| 5     | 6          | 0.25-0.75       | 0.53          | 0.85           | Legendary |
| 10    | 11         | 0.5-1.0         | 0.8           | 0.7            | Artifact |

### Scaling Formula

```rust
fn get_shell_circuit_config(shell_level: u8) -> CircuitConfiguration {
    CircuitConfiguration {
        max_qubits: (shell_level + 1).min(16),
        frequency_range: (
            shell_level as f32 / 20.0,
            (shell_level + 5) as f32 / 20.0
        ),
        amplitude_ceiling: (shell_level + 3) as f32 / 15.0,
        coherence_baseline: 1.0 - (shell_level as f32 * 0.03),
        exotic_unlock_threshold: shell_level as f32 / 10.0,
    }
}
```

## Energy Generation

### Circuit-Based Generation

Each world circuit generates energies within its shell's parameter ranges:

```rust
impl EnergySignature {
    pub fn generate(shell_level: u8, circuit_qubits: u8, seed: u64) -> Self {
        let mut rng = SeededRng::new(seed);
        let config = get_shell_circuit_config(shell_level);
        
        // Frequency within shell range
        let frequency = rng.gen_range(
            config.frequency_range.0..config.frequency_range.1
        );
        
        // Amplitude scales with qubits
        let max_amplitude = config.amplitude_ceiling * (circuit_qubits as f32 / 8.0);
        let amplitude = rng.gen_range(0.1..max_amplitude.min(1.0));
        
        // Phase complexity increases with shell
        let phase = rng.gen::<f32>() * 2.0 * PI * (shell_level + 1) as f32;
        
        // Coherence penalty for outer shells
        let coherence_penalty = shell_level as f32 * 0.05;
        let coherence = (rng.gen::<f32>() * 0.8 + 0.2) - coherence_penalty;
        
        // Entanglement from circuit qubits
        let entanglement = (circuit_qubits.pow(2) * 8).min(255);
        
        // Unique resonance pattern
        let resonance_pattern = generate_resonance_pattern(shell_level, circuit_qubits, seed);
        
        EnergySignature {
            frequency: frequency.clamp(0.0, 1.0),
            amplitude: amplitude.clamp(0.0, 1.0),
            phase: phase % (2.0 * PI),
            coherence: coherence.clamp(0.0, 1.0),
            entanglement,
            resonance_pattern,
        }
    }
}
```

### Emission Frequency

- **Shell 0**: Every 30 seconds, 4 orbs
- **Shell N**: Every (30 / (N+1)) seconds, 4 orbs
- Higher shells emit more frequently but with higher complexity

## Energy Mixing System

### Combination Algorithm

Players can mix energies using specialized devices:

```rust
#[spacetimedb::reducer]
pub fn mix_energies(
    ctx: &ReducerContext, 
    player_id: u32,
    energy_a: EnergySignature,
    energy_b: EnergySignature,
    mixing_device_efficiency: f32
) -> Result<EnergySignature, String> {
    
    // Calculate interference patterns
    let freq_interference = calculate_frequency_interference(&energy_a, &energy_b);
    let phase_coherence = calculate_phase_coherence(&energy_a, &energy_b);
    let efficiency_factor = mixing_device_efficiency * phase_coherence;
    
    let mixed_signature = EnergySignature {
        frequency: interpolate_with_interference(
            energy_a.frequency, 
            energy_b.frequency, 
            freq_interference
        ),
        amplitude: combine_amplitudes(
            energy_a.amplitude, 
            energy_b.amplitude, 
            efficiency_factor
        ),
        phase: combine_phases(energy_a.phase, energy_b.phase),
        coherence: (energy_a.coherence + energy_b.coherence) * efficiency_factor * 0.5,
        entanglement: combine_entanglement(energy_a.entanglement, energy_b.entanglement),
        resonance_pattern: energy_a.resonance_pattern ^ energy_b.resonance_pattern,
    };
    
    Ok(mixed_signature)
}
```

### Mixing Rules

1. **Frequency Interference**: Similar frequencies reinforce, different frequencies create harmonics
2. **Amplitude Combination**: Uses wave interference math - can be constructive or destructive
3. **Phase Alignment**: Better alignment = higher efficiency
4. **Coherence Loss**: Mixing always reduces coherence (entropy increase)
5. **Entanglement Combination**: XOR operation creates new patterns
6. **Resonance Patterns**: Bit-wise XOR creates unique combinations

## Discovery System

### Energy Type Registry

```rust
#[spacetimedb::table(name = discovered_energy_types, public)]
pub struct DiscoveredEnergyType {
    #[primary_key]
    pub type_hash: u64,         // Hash of signature for uniqueness
    pub signature: EnergySignature,
    pub name: String,           // First discoverer names it
    pub discovered_by: Identity,
    pub discovery_timestamp: u64,
    pub shell_origin: u8,
    pub discovery_count: u32,   // Affects rarity
}
```

### Discovery Mechanics

1. **First Discovery**: Player who first creates a signature gets naming rights
2. **Rarity Calculation**: `rarity = 1.0 / (1.0 + discovery_count * 0.1)`
3. **Unique Signatures**: Hash prevents duplicate discoveries
4. **Shell Attribution**: Energy types remember their origin shell

### Naming System

Auto-generated names based on parameters:

```rust
pub fn generate_name(signature: &EnergySignature) -> String {
    let hue_name = frequency_to_hue_name(signature.frequency);
    let intensity_name = amplitude_to_intensity_name(signature.amplitude);
    let phase_modifier = phase_to_modifier(signature.phase);
    
    format!("{}-{} {}", hue_name, intensity_name, phase_modifier)
}
```

**Examples**:
- "Crimson-Bright Resonance" (freq: 0.1, amp: 0.8, phase: 0.3)
- "Violet-Dim Flux" (freq: 0.9, amp: 0.2, phase: 1.7)
- "Azure-Intense Harmony" (freq: 0.6, amp: 0.9, phase: 0.1)

## Implementation Details

### Database Schema Updates

```rust
// Replace old EnergyType enum with parameterized system
pub struct EnergyStorage {
    pub storage_entry_id: u64,
    pub owner_type: String,
    pub owner_id: u64,
    pub energy_signature: EnergySignature,  // Instead of enum
    pub quantum_count: u32,
    pub capacity: u32,
}

pub struct EnergyOrb {
    pub orb_id: u64,
    pub world_coords: WorldCoords,
    pub position: DbVector3,
    pub velocity: DbVector3,
    pub energy_signature: EnergySignature,  // Instead of enum
    pub quantum_count: u32,                 // 100 quanta per orb
    pub creation_time: u64,
}
```

### Visual System Integration

```csharp
// Unity color calculation from signature
public Color CalculateVisualColor(EnergySignature signature) {
    // Convert frequency to hue (0-360 degrees)
    float hue = signature.frequency * 360f;
    
    // Amplitude affects brightness
    float brightness = 0.3f + (signature.amplitude * 0.7f);
    
    // Coherence affects saturation
    float saturation = 0.4f + (signature.coherence * 0.6f);
    
    // Phase affects additional color shifting
    float phaseShift = (signature.phase / (2f * Mathf.PI)) * 60f; // ±30 degree shift
    hue = (hue + phaseShift) % 360f;
    
    return Color.HSVToRGB(hue / 360f, saturation, brightness);
}
```

### Performance Considerations

1. **Signature Hashing**: Use fast hash for type lookup
2. **Parameter Caching**: Cache generated signatures per world/seed
3. **Discovery Indexing**: Index discovered types by complexity/shell
4. **Mixing Optimization**: Pre-calculate common mixing patterns
5. **Visual Batching**: Group similar signatures for rendering

## Gameplay Impact

### Player Progression

1. **Shell Advancement**: Unlock higher-complexity energies
2. **Circuit Mastery**: Learn to generate specific signatures
3. **Mixing Expertise**: Discover valuable combinations
4. **Collection Goals**: Hunt for rare/perfect signatures
5. **Market Trading**: Unique signatures have unique values

### Economic Implications

1. **Rarity-Based Pricing**: Perfect parameters = highest value
2. **Discovery Bonuses**: First discoverer gets premium prices
3. **Shell Premiums**: Outer shell energies worth more
4. **Mixing Services**: Players specialize in efficient combinations
5. **Signature Banking**: Store valuable parameter combinations

### Strategic Depth

1. **Location Choice**: Which shells to build in for desired energies
2. **Circuit Optimization**: Tune circuits for specific outputs
3. **Mixing Chains**: Plan multi-step combinations
4. **Market Timing**: When to reveal new discoveries
5. **Collection Building**: Assemble signature libraries

## Future Expansions

### Advanced Features

1. **Signature Evolution**: Energies that change over time
2. **Quantum Decay**: Coherence degradation mechanics
3. **Resonance Networks**: Signatures that enhance each other
4. **Temporal Variations**: Time-based parameter shifts
5. **Cross-Shell Interactions**: Long-distance signature effects

### Technical Enhancements

1. **Machine Learning**: Generate signatures from player preferences
2. **Blockchain Integration**: Permanent signature ownership
3. **Procedural Naming**: More sophisticated name generation
4. **Visual Effects**: Parameter-driven particle systems
5. **Audio Synthesis**: Signatures generate unique sounds

## Conclusion

The Parameterized Energy System provides:

- **Infinite Scalability**: Works for any number of shells
- **Rich Discovery**: Mathematical uniqueness guarantees new finds
- **Progressive Complexity**: Natural difficulty curve
- **Emergent Gameplay**: Player-driven economy and optimization
- **Quantum Authenticity**: Based on real quantum principles

This system transforms energy from a simple resource into a core game mechanic that drives exploration, experimentation, and economic activity throughout the quantum metaverse.