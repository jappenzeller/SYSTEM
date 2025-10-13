# Claude Code: Quantum Minigame Implementation Plan

**Project**: SYSTEM - Quantum Mining Game  
**Feature**: Bloch Sphere Puzzle Minigame  
**Timeline**: 18-24 weeks (3 phases)  
**Dependencies**: SpaceTimeDB, Unity 2022.3+, Existing Circuit System

## Overview

Transform the mining mechanic from simple frequency matching to engaging quantum puzzle solving. Players reverse-engineer quantum gate sequences from Bloch sphere visualizations to charge world circuits and eventually form collaborative quantum tunnels.

## Phase 1: Circuit-Powered Mining (Weeks 1-6)

### Goal
Replace frequency matching with Bloch sphere puzzles tied to world circuits.

### MVP Scope

#### Core Features
- **Gate Set**: H, X, Y, Z, S gates only (T gate in future)
- **Target Circuits**: Primary positions only (6 per world)
- **Puzzle Complexity**: Fixed 3-4 gate sequences
- **Scoring**: Binary pass/fail (>80% fidelity = success)
- **Reward**: 2% circuit charge per successful solve

#### Database Schema Updates
```rust
// Extend existing WorldCircuit table
WorldCircuit {
    // ... existing fields ...
    quantum_state: BlochState,      // Daily puzzle state
    puzzle_difficulty: u8,          // 1-3 for Phase 1
    daily_seed: u32,                // For deterministic generation
}

// New table for tracking attempts
MiningAttempt {
    attempt_id: u64,
    player: Identity,
    circuit_id: u64,
    submitted_gates: Vec<u8>,
    fidelity_score: f32,
    success: bool,
    timestamp: Timestamp,
}
```

#### Unity Components Structure
```
QuantumMinigame/
├── BlochSphereVisualizer.cs      // Core visualization
├── GateSequenceInput.cs           // Player input handling
├── FidelityCalculator.cs          // Score calculation
├── CircuitPuzzleManager.cs        // Integration layer
└── UI/
    ├── GateButtonUI.cs
    ├── BlochSphereUI.cs
    └── MinigameHUD.cs
```

#### Success Metrics
- [ ] >50% of mining attempts use puzzle (not skip)
- [ ] Average completion time <30 seconds
- [ ] Circuit charging rate maintains balance
- [ ] Player retention stable

### Implementation Checklist

#### Week 1-2: Core Systems
- [ ] Bloch sphere visualization component
- [ ] Gate application mathematics
- [ ] Basic UI with gate buttons
- [ ] Fidelity scoring algorithm

#### Week 3-4: Integration
- [ ] Connect to WorldCircuit table
- [ ] Daily rotation system
- [ ] Mining session flow
- [ ] Skip option with reduced rewards

#### Week 5-6: Polish & Testing
- [ ] Visual feedback and animations
- [ ] Tutorial system
- [ ] Performance optimization
- [ ] Balance testing

---

## Phase 2: Quantum State Extraction (Weeks 7-14)

### Goal
Make every wave packet orb contain a unique quantum puzzle, scaling rewards with skill.

### MVP Scope

#### Enhanced Features
- **Dynamic Difficulty**: 2-6 gate sequences based on orb properties
- **Efficiency Scaling**: 50-150% extraction based on accuracy
- **Pattern Library**: 50+ reusable patterns with variations
- **Coherence Mechanic**: Fresher orbs = easier puzzles
- **Ghost Paths**: See other players' recent solutions

#### Database Schema Evolution
```rust
// Extend WavePacketOrb
WavePacketOrb {
    // ... existing fields ...
    puzzle_pattern_id: u64,         // Links to pattern library
    rotation_offset: Vec3,          // Randomizes pattern
    difficulty_modifier: f32,       // Based on amplitude/coherence
}

// Pattern library for reuse
PuzzlePattern {
    pattern_id: u64,
    gate_sequence: Vec<u8>,
    base_difficulty: u8,
    path_complexity: f32,
    frequency_affinity: u8,         // Best for certain colors
}

// Player progression tracking
PlayerPatternStats {
    player: Identity,
    pattern_id: u64,
    best_fidelity: f32,
    attempt_count: u32,
    average_time: f32,
}
```

#### New Systems
```
QuantumStateExtraction/
├── PatternGenerator.cs            // Creates puzzle variations
├── DifficultyScaler.cs           // Adjusts based on orb stats
├── GhostPathRenderer.cs          // Shows other solutions
├── PartialCreditScorer.cs        // Path similarity algorithm
└── OrbPuzzleIntegration.cs      // Connects to mining system
```

#### Phase 2 Additions
- **Partial Credit**: Path similarity scoring for near-misses
- **Visual Hints**: Circuit puzzles provide clues for nearby orbs
- **Pattern Recognition**: UI shows if pattern previously encountered
- **Skill Progression**: Track player improvement over time

### Success Metrics
- [ ] Mining engagement +30% over Phase 1
- [ ] Players develop pattern preferences
- [ ] Skill curve visible in data
- [ ] Reduced skip usage (<30%)

---

## Phase 3: Collaborative Tunnel Formation (Weeks 15-24)

### Goal
Transform quantum tunnel creation into cooperative multiplayer puzzles.

### MVP Scope

#### Collaborative Features
- **Tunnel Challenges**: Triggered at 80% circuit charge
- **Minimum Players**: 2-3 required for success
- **Phase Alignment**: Synchronized solutions score higher
- **Real-time Visualization**: See others' attempts live
- **Tunnel Persistence**: 24-hour duration, needs maintenance

#### Database Schema Final Form
```rust
// Tunnel challenge tracking
TunnelChallenge {
    challenge_id: u64,
    source_circuit: u64,
    target_circuit: u64,
    tunnel_type: TunnelColor,
    required_fidelity: f32,         // Threshold for success
    current_progress: f32,          // Combined player efforts
    quantum_target: BlochState,     // Shared goal state
    active_players: Vec<Identity>,
    expires_at: Timestamp,
}

// Individual contributions
TunnelContribution {
    challenge_id: u64,
    player: Identity,
    gate_sequence: Vec<u8>,
    fidelity_contribution: f32,
    phase_alignment: f32,           // Sync with others
    timestamp: Timestamp,
}

// Established connections
QuantumTunnel {
    tunnel_id: u64,
    source_world: WorldCoords,
    target_world: WorldCoords,
    tunnel_color: TunnelColor,
    stability: f32,                 // Degrades over time
    contributors: Vec<Identity>,    // Credit list
    established_at: Timestamp,
}
```

#### Multiplayer Systems
```
CollaborativeTunneling/
├── TunnelChallengeManager.cs     // Orchestrates challenges
├── PhaseAlignmentScorer.cs       // Quantum interference math
├── RealtimeSyncSystem.cs         // Multiplayer updates
├── TunnelStabilityTracker.cs     // Decay and maintenance
├── ContributionVisualizer.cs     // Shows all players' work
└── Networking/
    ├── ChallengeSync.cs
    └── SolutionBroadcast.cs
```

#### Cooperation Mechanics
- **Quantum Interference**: Overlapping solutions strengthen
- **Time Windows**: 4-hour challenges encourage coordination
- **Role Specialization**: Scouts vs. refiners emerge naturally
- **Progress Persistence**: Partial attempts build toward goal

### Success Metrics
- [ ] 3+ average players per tunnel
- [ ] 60%+ successful tunnel formation
- [ ] Community coordination emerges
- [ ] Tunnel network grows organically

---

## Technical Requirements Across All Phases

### Performance Targets
- Puzzle Generation: <50ms
- Solution Validation: <100ms server-side
- Visual Updates: Maintain 60 FPS
- Multiplayer Sync: <200ms latency

### Shared Infrastructure

#### Core Systems
```
SharedQuantumSystems/
├── BlochMathematics.cs           // Quantum state calculations
├── GateLibrary.cs                // Gate definitions & matrices
├── StateValidator.cs             // Server-side verification
├── QuantumSerializer.cs          // Network optimization
└── TutorialFramework.cs          // Expandable tutorials
```

#### Visual Components
- Bloch sphere with customizable LOD
- Gate sequence timeline UI
- Fidelity feedback systems
- Particle effects for quantum states

### Risk Mitigation

#### Player Experience
- Always provide skip option (reduced rewards)
- Difficulty accessible to casual players
- Clear visual feedback for actions
- Comprehensive tutorials per phase

#### Technical Safeguards
- Rollback capability between phases
- Feature flags for gradual rollout
- Extensive logging for analysis
- Performance profiling throughout

## Implementation Timeline

### Pre-Production (Week 0)
- [ ] Technical spike: Bloch sphere in Unity
- [ ] UX mockups and flow diagrams
- [ ] Database schema review
- [ ] Team alignment meeting

### Phase 1: Weeks 1-6
- Sprint 1-2: Core visualization
- Sprint 3: Circuit integration
- Sprint 4: Polish and testing

### Phase 2: Weeks 7-14
- Sprint 5-6: Orb puzzle system
- Sprint 7: Pattern library
- Sprint 8-9: Ghost paths & multiplayer prep

### Phase 3: Weeks 15-24
- Sprint 10-11: Challenge system
- Sprint 12-13: Multiplayer sync
- Sprint 14-15: Balance and polish

### Post-Launch
- Week 25+: Community feedback integration
- Ongoing: New patterns and mechanics
- Future: Advanced features (multi-qubit, etc.)

## Success Criteria

### Phase 1 Complete When:
- Players engage with puzzles >50%
- Circuit charging balanced
- Core loop proven fun

### Phase 2 Complete When:
- Mining transformed into skill game
- Player progression visible
- Foundation for multiplayer ready

### Phase 3 Complete When:
- Collaborative gameplay emerges
- Tunnel networks self-organize
- Community coordination active

---

**Document Status**: Ready for Implementation  
**Review Required**: Technical Lead, Game Designer  
**Dependencies**: Existing circuit system must be stable