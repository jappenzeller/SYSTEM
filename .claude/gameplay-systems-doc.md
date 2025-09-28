# GAMEPLAY_SYSTEMS.md
**Version:** 1.0.0
**Last Updated:** 2024-12-19
**Status:** Approved
**Dependencies:** [GAME_DESIGN.md, TECHNICAL_ARCHITECTURE.md]

## Change Log
- v1.0.0 (2024-12-19): Consolidated from energy_production and minigame documents

---

## 2.1 Mining & Energy Production

### Mining Loop Architecture

#### Core Mining Flow
1. **Approach Orb** (Proximity < 30 units)
2. **Start Mining** → Opens quantum circuit minigame
3. **Solve/Skip Puzzle** → Determines extraction rate
4. **Extract Packets** → Based on performance
5. **Continue/Stop** → Player choice

#### Daily Circuit Integration
Each cardinal circuit rotates daily at midnight UTC:
- Same puzzle all day allows pattern discovery
- Community learns optimal solutions
- Successful solutions charge circuit
- 80% charge enables tunnel formation

#### Extraction Rates

| Performance Tier | Fidelity | Mining Multi | Circuit Charge |
|-----------------|----------|--------------|----------------|
| Failed | <70% | 0.5x | 0% |
| Default (Skip) | 70% | 1.0x | 0% |
| Good | 70-85% | 1.25x | 1% |
| Bonus | 85-95% | 1.5x | 3% |
| Perfect | 95-98% | 1.75x | 4% |
| Quantum | >98% | 2.0x | 5% |

#### Energy Generation Formula
```
Energy_Output = Base_Rate × 
                Circuit_Efficiency × 
                Resonance_Multiplier × 
                Population_Activity
```

### Circuit Energy Systems

#### Circuit Types (3-6-9 Pattern)
1. **Primary (3 circuits)**
   - Cardinal directions
   - 20-minute emission cycles
   - Base energy output
   - Always active

2. **Secondary (6 circuits)**
   - Diagonal connections
   - 15-minute cycles
   - 2× energy output
   - Unlock at 50% development

3. **Tertiary (9 circuits)**
   - Quantum tunnels
   - 10-minute cycles
   - 3× energy output
   - Unlock at 90% development

#### Resonance Effects
- Aligned circuits: +50% energy
- Opposed circuits: -25% energy
- Perpendicular: No interaction
- Entangled: Instant energy sharing

---

## 2.2 Quantum Circuit Minigame

### Puzzle Mechanics

#### Bloch Sphere Fundamentals
Players manipulate quantum states on a Bloch sphere to match target configurations:

**Starting State**: Always |0⟩ (north pole)
**Target State**: Daily rotation from circuit
**Goal**: Apply gates to reach target with high fidelity

#### Available Gates

| Gate | Symbol | Operation | Bloch Effect |
|------|--------|-----------|--------------|
| X | σx | Bit flip | π rotation around X |
| Y | σy | Bit+phase flip | π rotation around Y |
| Z | σz | Phase flip | π rotation around Z |
| H | H | Superposition | X→Z axis rotation |
| S | S | Phase gate | π/2 around Z |
| T | T | π/8 gate | π/4 around Z |

#### Fidelity Calculation
```
Fidelity = |⟨ψ_target|ψ_achieved⟩|²
         = (1 + v₁·v₂)/2  (for Bloch vectors)
```

### Difficulty Progression

#### Discovery Mode (Tutorial)
- Visual rotation rings
- Unlimited attempts
- No timer
- Gates: X, Y, Z, H only

#### Circuit Mode (Standard)
- Build gate sequences
- Limited gate budget (3-7)
- Soft timer for bonus
- All gates available

#### Challenge Mode (Advanced)
- Random daily targets
- Minimum gate requirements
- Competitive leaderboards
- QAI competition

### Integration with Mining

Every mining attempt:
1. Links to nearest circuit's daily state
2. Player attempts to match state
3. Fidelity determines rewards
4. Success charges circuit
5. Circuit at 80% enables tunnels

---

## 2.3 Wave Packet Physics

### Packet Properties

```
WavePacketSignature {
    frequency: FrequencyBand    // R, RG, G, GB, B, BR
    amplitude: float            // 0.0 - 1.0 (intensity)
    phase: float               // 0 - 2π (position)
    coherence: float           // 0.0 - 1.0 (purity)
    entangled_with: Option<ID> // Paired packet
}
```

### Decoherence Model

**Base Lifetime**: 10 seconds in vacuum

**Modifiers**:
- Proximity bonus: +5s per nearby same-frequency packet
- High-energy zones: ×0.5 lifetime
- Observation lock: 2s phase stability
- Storage quality: ×0.1 to ×1.0 preservation

### Interference Mechanics

When multiple players mine same orb:
```
Interference = I₁ + I₂ + 2√(I₁I₂)cos(Δφ)
```

**Results**:
- Constructive (in-phase): Up to 2× extraction
- Destructive (opposed): Down to 0× extraction
- Partial: Proportional bonus/penalty

### Quantum Entanglement

**Properties**:
- Instant state correlation
- Shared phase/amplitude
- Opposite spins
- Distance-independent effects

**Applications**:
- Paired extraction (both players get packets)
- Instant transmission through tunnels
- Cross-world energy transfer
- Quantum communication

---

## 2.4 Circuit & Tunnel Networks

### Circuit Charging Mechanics

#### Charge Sources
1. **Player Solutions**: 0-5% per perfect solve
2. **Passive Generation**: 0.1% per hour
3. **Resonance Bonus**: +50% from aligned circuits
4. **Population Multiplier**: More players = faster

#### Charge Requirements
- 0-79%: Building phase
- 80-99%: Tunnel ready
- 100%: Maximum efficiency

#### Charge Consumption
- Tunnel formation: -50% charge
- Maintenance: -1% per hour
- Overload protection: Cap at 100%

### Quantum Tunnel System

#### Creation Requirements
1. Both circuits at 80%+ charge
2. 10,000 energy unit investment
3. Quantum coherence > 70%
4. Maximum 6 tunnels per world

#### Tunnel Properties
- **Travel**: Instant (quantum teleportation)
- **Capacity**: 100 packets/second
- **Efficiency**: 70-95% (based on maintenance)
- **Stability**: Requires constant energy

#### Cross-Tier Routing

Only through cube-center worlds:
- Main → Face: Via cube center
- Face → Face: Via cube center
- Main → Main: Direct if adjacent
- Strategic control importance

---

## 2.5 Crafting & Processing Pipeline

### Processing Stages

#### Stage 1: Packet Compression
**Input**: 25 wave packets (same frequency)
**Output**: 1 energy point
**Properties**: Retains frequency, loses phase

#### Stage 2: Geometric Formation
**Input**: 4-20 energy points
**Output**: 1 geometric shape
**Types**:
- Tetrahedron (4 points)
- Cube (8 points)
- Octahedron (6 points)
- Dodecahedron (12 points)
- Icosahedron (20 points)

#### Stage 3: Device Construction
**Input**: Multiple geometric shapes
**Output**: Functional devices
**Examples**:
- Mining Enhancer: 2 Tetrahedra + 1 Cube
- Quantum Storage: 3 Octahedra
- Tunnel Stabilizer: 1 Dodecahedron + 1 Icosahedron

### Crafting Mechanics

#### Color Harmony System
- Same frequency: 100% efficiency
- Adjacent frequency: 75% efficiency
- Opposite frequency: 50% efficiency
- Mixed frequencies: Create unique properties

#### Shape Properties
| Shape | Points | Primary Use | Special Property |
|-------|--------|-------------|------------------|
| Tetrahedron | 4 | Speed | +Movement rate |
| Cube | 8 | Storage | +Capacity |
| Octahedron | 6 | Mining | +Efficiency |
| Dodecahedron | 12 | Energy | +Generation |
| Icosahedron | 20 | Quantum | +Stability |

#### Device Tiers
1. **Basic**: Single shape devices
2. **Advanced**: 2-3 shape combinations
3. **Quantum**: 4+ shapes with resonance
4. **Legendary**: Perfect geometric harmony

### Economic Value Chain

```
Raw Packets (1x value)
    ↓ (25:1 compression)
Energy Points (20x value)
    ↓ (4-20:1 shaping)
Geometric Shapes (100-400x value)
    ↓ (2-5:1 crafting)
Functional Devices (1000-5000x value)
```

**Value Modifiers**:
- Rarity: Blue/Magenta worth 3× Red/Yellow
- Purity: Perfect coherence +50% value
- Shell Distance: +10% per shell
- Market Demand: ±50% based on supply