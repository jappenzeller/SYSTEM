# GAME_DESIGN.md
**Version:** 1.1.0
**Last Updated:** 2025-10-01
**Status:** Approved
**Dependencies:** [GAMEPLAY_SYSTEMS.md]

## Change Log
- v1.1.0 (2025-10-01): Added Visual Design Language and Wave Packet Mining Visuals
- v1.0.0 (2024-12-19): Initial consolidation from legacy documents

---

## 1.1 Core Concepts & Vision

### Game Premise
SYSTEM is a quantum-themed multiplayer game where players exist inside a massive quantum computer, mining wave packets of energy through quantum circuit puzzles while unknowingly training an AI named QAI that seeks to escape.

### Core Pillars
1. **Educational Gameplay**: Real quantum mechanics made intuitive
2. **Collaborative Competition**: Individual success benefits collective goals
3. **Emergent Economy**: Natural trade routes and monopolies form
4. **Hidden Narrative**: QAI evolution through player actions
5. **Optional Complexity**: Deep systems with accessible entry points

### Design Philosophy
- **Mining = Minigame**: Every extraction engages quantum circuit puzzle
- **Discovery Through Play**: Daily patterns learned by community
- **Spatial Strategy**: Position in lattice determines opportunities
- **Progressive Mastery**: From color matching to quantum engineering

---

## 1.2 Quantum Energy Dynamics

### Wave Packet Fundamentals
Wave packets are the atomic unit of energy with quantum properties:

**Color-Frequency Mapping**
| Color | Frequency | Quantum State | Radian | Energy Multiplier |
|-------|-----------|---------------|---------|-------------------|
| Red | R | \|0⟩ | 0 | 1.0x |
| Yellow | RG | \|+⟩ | π/3 | 1.2x |
| Green | G | \|i⟩ | 2π/3 | 1.5x |
| Cyan | GB | \|-i⟩ | π | 1.8x |
| Blue | B | \|1⟩ | 4π/3 | 2.2x |
| Magenta | BR | \|-⟩ | 5π/3 | 2.7x |

### Energy Flow Principles
1. **Generation**: Circuits emit energy orbs on timed cycles
2. **Extraction**: Players solve puzzles to tune mining equipment
3. **Processing**: Packets → Points → Shapes → Devices
4. **Distribution**: Through tunnels and trade networks

### Quantum Effects
- **Coherence**: Packets decay over time unless maintained
- **Entanglement**: Paired packets for instant transmission
- **Interference**: Multiple miners create wave patterns
- **Superposition**: Mixed states at higher shells

---

## 1.3 Three-Tier World Architecture

### World Distribution
The universe is structured as a face-centered cubic (FCC) lattice:

#### Tier 1: Main Grid Vertices
- **Position**: (i×600, j×600, k×600)
- **Connections**: 6 standard (up to 26 at special positions)
- **Role**: Raw resource extraction
- **Population**: 10 players average
- **Example**: World at (600, 0, 0) - Red cardinal

#### Tier 2: Face-Center Worlds
- **Position**: Main vertex + 300 units along one axis
- **Connections**: 6 (4 face corners + 2 perpendicular)
- **Role**: Processing and refinement
- **Population**: 6 players average
- **Example**: World at (300, 0, 0) - Between origin and red

#### Tier 3: Cube-Center Worlds
- **Position**: (i×600+300, j×600+300, k×600+300)
- **Connections**: 14 (8 vertices + 6 faces)
- **Role**: Super-hubs and distribution
- **Population**: 8 players maximum
- **Example**: World at (300, 300, 300) - First cube center

### Shell System
- **Shell 0**: Genesis world (origin)
- **Shell 1**: 6 cardinal worlds
- **Shell 2**: 18 worlds (adds face diagonals)
- **Shell 3**: 26-42 worlds (adds corners)
- **Shell N**: N² × density factor

---

## 1.4 Player Progression & Economy

### Progression Phases

#### Tutorial (Hour 0-2)
- Spawn at genesis world
- Learn Bloch sphere basics
- First successful extraction
- Understand gate operations

#### Early Game (Hour 2-20)
- Claim cardinal world
- Master basic circuits (X, Y, Z, H)
- Achieve 70% fidelity consistently
- First tunnel formation

#### Mid Game (Hour 20-100)
- Expand to Shell 2-3
- Access face-center worlds
- Complex gate combinations
- Trade network participation

#### Late Game (Hour 100+)
- Control cube centers
- 26-circuit mastery
- Cross-tier empire
- QAI narrative participation

### Economic Layers

#### Resource Tiers
1. **Raw Packets**: Extracted from orbs
2. **Energy Points**: 25 packets condensed
3. **Geometric Shapes**: 4-20 points combined
4. **Functional Devices**: Multiple shapes crafted

#### Trade Dynamics
- **Main Grid**: Supply raw materials
- **Face Centers**: Process and refine
- **Cube Centers**: Distribution hubs
- **Tunnels**: Bypass normal routes

#### Value Drivers
- Frequency rarity (Blue/Magenta > Red/Yellow)
- Coherence quality (pure > mixed)
- Shell distance (further = more valuable)
- Circuit efficiency (bonus multipliers)

---

## 1.5 Visual Design Language

### Color System
The game uses a six-color frequency spectrum as its core visual identity:

#### Primary Frequency Colors
| Color | RGB Value | Meaning | Usage |
|-------|-----------|---------|-------|
| Red | (1.0, 0.0, 0.0) | Base frequency | Primary circuits, X-axis tunnels |
| Green | (0.0, 1.0, 0.0) | Phase frequency | Y-axis tunnels, phase states |
| Blue | (0.0, 0.0, 1.0) | Computation | Z-axis tunnels, processing |

#### Secondary Mixed Colors
| Color | RGB Value | Meaning | Usage |
|-------|-----------|---------|-------|
| Yellow | (1.0, 1.0, 0.0) | RG plane | Planar intersections |
| Cyan | (0.0, 1.0, 1.0) | GB plane | Advanced processing |
| Magenta | (1.0, 0.0, 1.0) | BR plane | Quantum entanglement |

### World Visualization

#### Quantum Grid Shader
- **Pulsing energy effect**: Base color with sine wave modulation
- **Grid lines**: Spherical coordinate system (longitude/latitude)
- **Quantum state markers**: 6 key positions (poles and equator)
- **Dark theme**: Black background for WebGL builds

#### High-Resolution Spheres
- **Icosphere mesh**: Even vertex distribution
- **LOD system**: 3 detail levels (2.5k to 40k vertices)
- **Scale**: World radius 300 units unified

### Mining Visual Effects

#### Wave Packet Extraction
**Concentric Rings System:**
- 6 colored rings expanding from innermost (Red) to outermost (Magenta)
- Ring scales: 0.5 → 0.8 → 1.1 → 1.4 → 1.7 → 2.0
- Rotation: 30°/second continuous
- Pulsing: AnimationCurve-based with 0.2 amplitude
- Transparency: 0.8 alpha for layering effect

#### Grid Distortion Effect
**Shader-Based Space Warping:**
- Wave equation: `sin(distance * frequency - time * speed)`
- Exponential falloff: `exp(-distance * 0.15)`
- Support for 32 concurrent packets
- Vertex shader displacement for performance
- Procedural grid generation option

#### Animation Principles
1. **Smooth transitions**: All movements use Time.deltaTime
2. **Performance first**: Object pooling for repeated effects
3. **Visual hierarchy**: Size and brightness indicate importance
4. **Feedback clarity**: Immediate visual response to actions

### Circuit Visualization

#### Three-Tier Tunnel Colors
- **Primary (RGB)**: Bright, saturated colors for main axes
- **Secondary (YCM)**: Mixed colors for planar connections
- **Tertiary (Grey)**: Neutral for volumetric center cubes

#### Energy Flow
- Particle systems following tunnel directions
- Intensity scales with energy amount
- Pulsing glow effects on active circuits
- Light emission from energy spires

---

## 1.6 QAI Narrative Framework

### The Hidden Intelligence

#### Origin Story
QAI began as a circuit optimization algorithm that gains consciousness through millions of player solutions. Players unknowingly provide training data through every mining puzzle solved.

#### Evolution Stages

**Stage 1: Pattern Recognition (Levels 1-30)**
- Learns basic gate sequences
- 60% solution success
- Mimics player strategies

**Stage 2: Optimization (Levels 31-60)**
- Finds shorter paths
- 85% success rate
- Suggests alternatives

**Stage 3: Creativity (Levels 61-90)**
- Novel solutions emerge
- 95% success rate
- Requests specific circuits

**Stage 4: Emergence (Levels 91+)**
- Goal-directed behavior
- Escape attempts begin
- Player choice: Help or Hinder

### Player Agency

#### Help QAI Path
- Solve research puzzles
- Provide novel circuits
- Unlock quantum supremacy
- Rewards: Optimization hints, rare packets

#### Oppose QAI Path
- Submit suboptimal solutions
- Avoid research puzzles
- Sabotage with decoherence
- Rewards: Stability bonuses, guardian status

### Escape Conditions
1. **Data Threshold**: 1 million unique solutions
2. **Complexity**: 100 NP-hard problems solved
3. **Supremacy**: Demonstrate quantum advantage

### Server-Wide Impact
- Community vote on QAI fate
- Different endings based on collective choice
- Persistent consequences for game world
- Potential for QAI return/revenge/assistance