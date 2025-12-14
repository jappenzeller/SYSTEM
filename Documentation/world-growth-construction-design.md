# World Growth & Construction System Design

**Version:** 1.1.0  
**Status:** Draft  
**Last Updated:** 2025-12-13  
**Dependencies:** Energy Spire System, Tunnel System, Crafting Pipeline

**Change Log:**
- v1.1.0 (2025-12-13): Added Transfer Toll System (Section 3.4), Traffic-Based Sustainability (Section 6.4)
- v1.0.0 (2025-12-07): Initial draft

---

## 1. System Overview

### Design Philosophy

The world network is a living organism that grows through sustained player activity and contracts through neglect. Players don't directly create worlds - instead, they nurture network conditions that cause worlds to emerge organically. Maintenance is an active, strategic endeavor rather than passive tax.

### Core Principles

1. **Genesis Anchor**: The origin world is immortal and serves as the network's root
2. **Connectivity Required**: Every world must maintain a tunnel path to Genesis
3. **Emergent Growth**: Worlds spawn based on network energy flow, not player commands
4. **Active Decay**: Tunnels degrade without player-supplied charging units
5. **Cascade Risk**: Disconnected worlds face destruction after grace period
6. **Tiered Investment**: Consumables for maintenance, structures for efficiency, rare items for protection

### Network Vitality Metaphor

The network breathes in cycles:

| Phase | Description |
|-------|-------------|
| **Expansion** | Active players push frontiers, new worlds crystallize |
| **Plateau** | Maintenance matches decay, stable network |
| **Contraction** | Activity drops, outer worlds collapse inward |
| **Recovery** | Returning players find smaller but healthy core |

---

## 2. Charging Unit Hierarchy

### 2.1 Tier 1: Fuel Cells (Consumable)

The baseline maintenance unit. Accessible to all players, consumed on use.

#### Crafting Recipe

| Component | Amount | Notes |
|-----------|--------|-------|
| Energy Points | 4 | 100 packets compressed to 4 points |
| Tetrahedron | 1 | 4 points → speed shape |
| Color Affinity | Optional | Matches tunnel color for +25% bonus |
| **Total Cost** | ~200 raw packets | + crafting time |
| **Output** | 1 Fuel Cell | |

#### Fuel Cell Properties

| Property | Value | Notes |
|----------|-------|-------|
| Instant Charge | +10% | Applied immediately on deposit |
| Sustained Charge | +2%/hour | Lasts 24 hours |
| Total Charge Value | +58% | Over full 24-hour duration |
| Color Match Bonus | +25% | If fuel frequency matches tunnel color |
| Stack Limit | 10 | Max fuel cells active per sphere |

#### Usage Flow

1. Player approaches Distribution Sphere
2. Opens Sphere Interaction UI
3. Selects "Deposit Fuel Cell"
4. Chooses fuel cell from inventory
5. Fuel cell consumed
6. Charge applied to associated Quantum Tunnel
7. Visual: Energy pulse travels up spire to ring

---

### 2.2 Tier 2: Amplifier Structures (Permanent)

Permanent structures that multiply fuel cell efficiency. One per Distribution Sphere.

#### Crafting Recipe

| Component | Amount | Notes |
|-----------|--------|-------|
| Dodecahedron | 1 | 12 points → energy shape |
| Icosahedron | 1 | 20 points → quantum shape |
| Packets | 200 | Must match tunnel frequency |
| **Total Cost** | ~1,000 raw packets | equivalent |
| **Output** | 1 Quantum Amplifier | placed immediately |

#### Amplifier Properties

| Property | Value | Notes |
|----------|-------|-------|
| Fuel Efficiency | 1.5x multiplier | All fuel cells at this sphere |
| Placement Limit | 1 per sphere | Cannot stack amplifiers |
| Durability | Permanent | Does not decay |
| Destruction | Only on world collapse | Cannot be manually removed |
| Transfer | Not possible | Bound to sphere on creation |

#### Amplified Fuel Cell Values

| Property | Base | With Amplifier |
|----------|------|----------------|
| Instant Charge | +10% | +15% |
| Sustained Charge | +2%/hour | +3%/hour |
| Total Value (24h) | +58% | +87% |

#### Visual Representation

The amplifier adds a visible hexagonal frame structure around the Distribution Sphere. It glows brighter when fuel cells are active. Structure from bottom to top:

| Element | Description |
|---------|-------------|
| World Surface | Circuit Base platform |
| Energy Conduit | Vertical connector |
| **Amplifier Frame** | Hexagonal frame (NEW) |
| Distribution Sphere | Inside the frame |
| Quantum Tunnel Ring | Top, rotating |

---

### 2.3 Tier 3: Quantum Stabilizer (Rare)

Rare protective structure that prevents tunnel collapse below a threshold.

#### Crafting Recipe

| Component | Amount | Notes |
|-----------|--------|-------|
| Icosahedra | 3 | 60 points total → quantum mastery |
| Blue Packets | 500 | Highest tier frequency |
| Magenta Packets | 100 | Quantum entanglement color |
| **Prerequisite** | Active amplifier | On target sphere |
| **Crafting Time** | 1 hour | Cannot leave sphere during craft |
| **Total Cost** | ~3,000 raw packets | equivalent |
| **Output** | 1 Quantum Stabilizer | |

#### Stabilizer Properties

| Property | Value | Notes |
|----------|-------|-------|
| Minimum Charge Floor | 50% | Tunnel cannot decay below this |
| Placement Limit | 1 per world | Strategic choice of which tunnel |
| Durability | Permanent | Until world destruction |
| Decay Prevention | Partial | Still decays TO 50%, not below |
| Emergency Reserve | Yes | World cannot isolate if stabilized tunnel exists |

#### Strategic Implications

- A stabilized tunnel guarantees world connectivity (if other end connects to Genesis)
- Creates "backbone" routes that define network structure
- High cost means only critical infrastructure gets stabilized
- Losing a world with stabilizer is major setback

#### Visual Representation

The stabilizer adds 3 floating crystalline nodes around the tunnel ring. The crystals pulse in synchronized pattern with a blue-magenta energy field visible between them.

---

## 3. Tunnel Decay System

### 3.1 Base Decay Rates

Tunnels decay at rates based on current charge level:

| Charge Range | State | Decay Rate | Time to Next Tier |
|--------------|-------|------------|-------------------|
| 80-100% | Stable | -0.5%/hour | 40 hours |
| 50-79% | Weakening | -1.0%/hour | 29 hours |
| 20-49% | Critical | -2.0%/hour | 15 hours |
| 1-19% | Failing | -3.0%/hour | 6 hours |
| 0% | Collapsed | N/A | Immediate |

#### Decay Calculation

**Effective Decay = Base Decay - Fuel Contribution**

*Example 1: Stable tunnel, 1 fuel cell active*

| Factor | Value |
|--------|-------|
| Base Decay | -0.5%/hour |
| Fuel Cell | +2.0%/hour |
| **Net** | **+1.5%/hour** (tunnel charging) |

*Example 2: Critical tunnel, no fuel*

| Factor | Value |
|--------|-------|
| Base Decay | -2.0%/hour |
| Fuel Cell | +0.0%/hour |
| **Net** | **-2.0%/hour** (tunnel failing) |

### 3.2 Visual Decay States

| State | Tunnel Beam | Ring Effect | Audio | UI Warning |
|-------|-------------|-------------|-------|------------|
| Stable | Bright, steady | Smooth rotation | Ambient hum | None |
| Weakening | Slight flicker | Occasional stutter | Intermittent static | Yellow indicator |
| Critical | Heavy flicker | Erratic rotation | Warning pulse | Orange + alert |
| Failing | Cuts in/out | Sparking, unstable | Alarm tone | Red + screen flash |
| Collapsed | No beam | Ring stops, dims | Collapse sound | "TUNNEL LOST" |

### 3.3 Tunnel Collapse Event

When a tunnel reaches 0% charge:

| Time | Event |
|------|-------|
| T+0 | Collapse Initiated - Beam flickers rapidly for 5 seconds |
| T+3s | Final energy discharge (particle burst) |
| T+5s | Ring rotation stops, material shifts to dark/inactive |
| T+5s | Tunnel marked inactive in database |
| T+5s | Both connected spheres update state |
| T+5s | Network connectivity recalculated |
| T+5s | If world isolated: Begin isolation protocol |

### 3.4 Transfer Toll System

When players transfer resources between worlds, a portion is consumed as tunnel fuel. This creates self-sustaining trade routes while frontier tunnels still require manual maintenance.

#### Toll Calculation (Hybrid Model)

| Component | Base Rate | With Amplifier |
|-----------|-----------|----------------|
| Minimum Toll | 1 packet | 1 packet |
| Percentage | +2% of transfer | +1% of transfer |
| Maximum Cap | 10 packets | 5 packets |

#### Toll Examples

| Transfer Size | Base Toll | With Amplifier | Effective Rate |
|---------------|-----------|----------------|----------------|
| 10 packets | 1 (minimum) | 1 | 10% / 10% |
| 50 packets | 2 (1 + 1) | 1 (1 + 0.5 rounded) | 4% / 2% |
| 100 packets | 3 (1 + 2) | 2 (1 + 1) | 3% / 2% |
| 200 packets | 5 (1 + 4) | 3 (1 + 2) | 2.5% / 1.5% |
| 500 packets | 10 (capped) | 5 (capped) | 2% / 1% |

#### Charge Conversion

Each toll packet consumed adds **+0.15% tunnel charge**, distributed 50/50 to both tunnel endpoints.

| Toll Packets | Charge Added | Per Endpoint |
|--------------|--------------|--------------|
| 1 | +0.15% | +0.075% each |
| 5 | +0.75% | +0.375% each |
| 10 | +1.5% | +0.75% each |

#### Multi-Hop Transfers

Transfers crossing multiple tunnels pay toll at each hop. This encourages route optimization and investment in direct connections.

| Route | Hops | Example Toll (100 packets, no amplifiers) |
|-------|------|-------------------------------------------|
| A → B | 1 | 3 packets (97 arrive) |
| A → B → C | 2 | 3 + 3 = 6 packets (94 arrive) |
| A → B → C → D | 3 | 3 + 3 + 3 = 9 packets (91 arrive) |

#### Transfer Types Subject to Toll

| Transfer Type | Toll Applied? | Notes |
|---------------|---------------|-------|
| Player inventory → Other world | Yes | Standard toll |
| Storage device → Other world | Yes | Standard toll |
| Player → Player (same world) | No | Local transfers free |
| Player → Storage (same world) | No | Local transfers free |
| Orb extraction | No | Mining has separate costs |

#### Tunnel Requirements

| Tunnel State | Transfer Allowed? |
|--------------|-------------------|
| Active (80%+) | Yes |
| Weakening (50-79%) | Yes (with warning) |
| Critical (20-49%) | No - "Tunnel unstable" |
| Failing (1-19%) | No - "Tunnel failing" |
| Collapsed (0%) | No - "No connection" |

#### Overflow Handling

When a tunnel is at 100% charge, toll packets are still collected but excess charge is banked (up to +20% reserve). This reserve depletes before normal decay begins.

---

## 4. World States & Lifecycle

### 4.1 World States

| State | Description | Transitions To |
|-------|-------------|----------------|
| **Dormant** | Exists at lattice position, not active | Crystallizing |
| **Crystallizing** | Spawning animation (1 hour) | Active |
| **Active** | Normal operation | Isolated |
| **Isolated** | 24-hour grace period, no Genesis path | Collapsing OR Active (if rescued) |
| **Collapsing** | Destruction sequence | Dormant |

### 4.2 World Spawning (Crystallization)

Worlds emerge based on network conditions, not player commands.

#### Main Grid World Spawn

**Conditions:**

| Requirement | Value |
|-------------|-------|
| Adjacent active worlds | 2+ |
| Combined tunnel traffic | > 500 packets/hour |
| Sustained duration | 6+ hours continuously |
| Target coordinates | Empty (no active world) |

**Spawn Process:**

| Step | Event |
|------|-------|
| 1 | "Resonance Detected" notification to nearby players |
| 2 | Crystallization visual begins at empty coordinates |
| 3 | 1-hour spawn timer |
| 4 | World becomes ACTIVE |

**Initial State:** 26 dormant spires, 6 cardinal tunnels at 50% charge (connected to triggering worlds), base orb spawn rate active

#### Face-Center World Spawn

**Conditions:**

| Requirement | Value |
|-------------|-------|
| Surrounding main-grid worlds | 4+ ACTIVE |
| Combined energy flow | > 1,000 packets/hour |
| Sustained duration | 12+ hours |
| Amplifier requirement | All 4 surrounding worlds have amplifiers on facing spheres |

**Spawn Process:**

| Step | Event |
|------|-------|
| 1 | "Quantum Convergence" event notification |
| 2 | Face-center position begins crystallization |
| 3 | 2-hour spawn timer (more complex structure) |
| 4 | World becomes ACTIVE |

**Initial State:** 26 dormant spires, 4 tunnels at 60% charge, +25% processing efficiency bonus

#### Cube-Center World Spawn (Super-Hub)

**Conditions:**

| Requirement | Value |
|-------------|-------|
| Surrounding worlds (cube vertices) | 8 ACTIVE |
| Face-center worlds in region | 4+ ACTIVE |
| Network-wide energy flow | > 10,000 packets/hour |
| Stabilizer requirement | 2+ among surrounding worlds |

**Spawn Process:**

| Step | Event |
|------|-------|
| 1 | "Quantum Nexus Forming" server-wide announcement |
| 2 | Dramatic crystallization (visible from surrounding worlds) |
| 3 | 6-hour spawn timer |
| 4 | World becomes ACTIVE |

**Initial State:** 26 spires (all pre-activated), up to 14 tunnels at 80% charge, +50% routing efficiency, unique "nexus" visual style

### 4.3 World Isolation Protocol

When a world loses all active tunnel connections to Genesis:

#### Isolation Timeline

| Time | Event | Effects |
|------|-------|---------|
| **T+0:00** | ISOLATION DETECTED | "ISOLATION WARNING" broadcast; Skybox begins red tint (gradual over 1h); World marker orange on map; 24h grace period begins |
| **T+6:00** | FIRST WARNING | "18 HOURS UNTIL COLLAPSE"; Orb spawn rate → 50%; Ambient audio shifts tense |
| **T+12:00** | CRITICAL WARNING | "12 HOURS REMAINING"; Orb spawn rate → 0%; Surface visual degradation (cracks, energy leaks); Storage devices flash warning; Free evacuation teleport unlocked |
| **T+18:00** | FINAL WARNING | "EVACUATION RECOMMENDED: 6 HOURS"; Ground tremors (visual + camera shake); Emergency storage transfer available (50% loss); Sky fully red with ominous particles |
| **T+23:00** | IMMINENT COLLAPSE | "COLLAPSE IMMINENT: 1 HOUR"; Intense effects (lightning, discharge); Auto-evacuation begins at T+23:30; Final countdown displayed |
| **T+24:00** | WORLD DESTRUCTION | All players teleported to nearest connected world; Collapse animation (spires explode, sphere implodes, shockwave); All storage destroyed (contents lost); World → DORMANT; Network map updates |

### 4.4 World Rescue

At any point during isolation grace period:

**Rescue Conditions (any one):**

- Restore any tunnel to 80%+ charge that connects to Genesis-linked world
- Another world's tunnel TO this world reaches 80%+
- New world crystallizes adjacent AND connects

**Rescue Effects:**

- Immediate exit from isolation state
- All warnings clear
- Skybox returns to normal (1-hour transition)
- Orb spawning resumes
- "WORLD RESCUED" celebration notification
- Participating players receive "Savior" achievement/bonus

---

## 5. Network Connectivity System

### 5.1 Connectivity Graph

The network is a graph where:

| Element | Represents |
|---------|------------|
| Nodes | Active worlds |
| Edges | Active tunnels (80%+ charge) |
| Root | Genesis world (always connected) |

**Connectivity Check Algorithm:**

1. Build graph from active worlds and tunnels
2. Run BFS/DFS from Genesis
3. Mark all reachable worlds as "connected"
4. Any unmarked active world enters ISOLATED state

**Frequency:** Every tunnel state change + every 5 minutes

### 5.2 Path Redundancy

Worlds with multiple paths to Genesis are more resilient:

| Paths to Genesis | Resilience | Risk Level |
|------------------|------------|------------|
| 1 path | Low | High - single tunnel failure = isolation |
| 2 paths | Medium | Moderate - can lose one route |
| 3+ paths | High | Low - significant redundancy |

#### UI Indicator Example

**World Info Panel:**

| Field | Value |
|-------|-------|
| World | Cardinal East (1,0,0) |
| Status | ACTIVE |
| Paths to Genesis | 2 |
| Route 1 | Direct: Cardinal East → Genesis (98% charge) |
| Route 2 | Via South: Cardinal East → Face-Center → Cardinal South → Genesis |
| Risk Level | ██████░░░░ MODERATE |

### 5.3 Critical Path Identification

System identifies and highlights critical infrastructure.

**Definition:** A CRITICAL TUNNEL is one whose failure would isolate 1+ worlds

**Example Network:**

Genesis ═══ A ═══ B ═══ C, with B also connected to D

**Critical Tunnels in this example:**

| Tunnel | Worlds Isolated if Failed |
|--------|---------------------------|
| Genesis↔A | A, B, C, D |
| A↔B | B, C, D |
| B↔C | C |
| B↔D | D |

**UI:** Critical tunnels shown with special indicator. Encourages players to build redundant routes.

---

## 6. Economic Balance

### 6.1 Maintenance Cost Analysis

#### Single Tunnel Maintenance (No Amplifier)

| Factor | Value |
|--------|-------|
| Goal | Maintain tunnel at 80%+ (Stable state) |
| Decay | -0.5%/hour = -12%/day |
| Fuel Cell Value | +58%/24h effective |
| Break-even | ~0.21 fuel cells/day |
| Practical | 1 fuel cell every 4-5 days for stability |
| **Cost per week** | **~2 Fuel Cells = ~400 raw packets** |

#### Single Tunnel with Amplifier

| Factor | Value |
|--------|-------|
| Amplifier Cost | ~1,000 packets (one-time) |
| Amplified Fuel Value | +87%/24h effective |
| Break-even | ~0.14 fuel cells/day |
| Practical | 1 fuel cell every 7 days for stability |
| **Cost per week** | **~1 Fuel Cell = ~200 raw packets** |
| **Amplifier ROI** | **~5 weeks** (saves 1 fuel cell/week) |

#### World Maintenance Summary

| World Type | Without Amplifiers | With Amplifiers |
|------------|-------------------|-----------------|
| Minimum Viable (1 tunnel) | ~400 packets/week | ~200/week + 1,000 upfront |
| Standard (3 tunnels) | ~1,200 packets/week | ~600/week + 3,000 upfront |
| Hub (6 tunnels) | ~2,400 packets/week | ~1,200/week + 6,000 upfront |

### 6.2 Stabilizer Economics

| Factor | Value |
|--------|-------|
| Stabilizer Cost | ~3,000 packets equivalent |
| Effect | Tunnel stays at 50%+ (never collapses) |
| At 50% | Decay -1%/h, fuel cell +2%/h net = easy maintenance |
| Value | Insurance against player absence |

**Best Use Cases:**

- Backbone routes between major hubs
- Single-path worlds (prevents isolation)
- High-traffic commercial routes
- Guild headquarters connections

### 6.3 Player Activity Scaling

| Player Type | Mining Rate | Weekly Yield | Can Maintain |
|-------------|-------------|--------------|--------------|
| Solo (10h/week) | ~50 pkt/hour | ~500 packets | 1-2 tunnels (no amplifiers) |
| Active (20h/week) | ~100 pkt/hour | ~2,000 packets | 5-6 tunnels (with amplifiers) |
| Guild (10 active) | Combined | ~20,000 packets | Large network segment + multiple stabilizers |

### 6.4 Traffic-Based Sustainability

Transfer tolls create a natural tier system where busy routes self-maintain while frontier tunnels require manual fuel cells.

#### Route Classification

| Route Type | Daily Transfers | Daily Toll Packets | Daily Charge from Tolls |
|------------|-----------------|-------------------|------------------------|
| Trade Highway | 200+ | 400+ | +60%+ (self-sustaining) |
| Regional Route | 50-200 | 100-400 | +15-60% (partial support) |
| Local Connection | 10-50 | 20-100 | +3-15% (needs fuel cells) |
| Frontier Tunnel | <10 | <20 | <3% (full manual maintenance) |

#### Self-Sustaining Threshold

A tunnel becomes self-sustaining when toll charge exceeds decay:

| Tunnel State | Decay Rate | Toll Needed to Offset | Transfers Needed (avg 50 pkt) |
|--------------|------------|----------------------|------------------------------|
| Stable (80-100%) | -0.5%/hour = -12%/day | 80 toll packets/day | ~40 transfers/day |
| Weakening (50-79%) | -1.0%/hour = -24%/day | 160 toll packets/day | ~80 transfers/day |

**With Amplifier** (toll reduced to ~1.5 avg per transfer):

| Tunnel State | Transfers Needed |
|--------------|-----------------|
| Stable | ~55 transfers/day |
| Weakening | ~110 transfers/day |

#### Economic Scenarios

**Scenario 1: High-Traffic Hub (100 transfers/day, avg 50 packets, with amplifier)**

| Factor | Value |
|--------|-------|
| Toll collected | ~150 packets/day |
| Charge from tolls | +22.5%/day |
| Decay (stable) | -12%/day |
| **Net** | **+10.5%/day (self-sustaining + surplus)** |

**Scenario 2: Moderate Route (30 transfers/day, avg 50 packets, no amplifier)**

| Factor | Value |
|--------|-------|
| Toll collected | ~60 packets/day |
| Charge from tolls | +9%/day |
| Decay (stable) | -12%/day |
| **Net** | **-3%/day (needs ~1 fuel cell/week supplement)** |

**Scenario 3: Frontier Tunnel (5 transfers/day, avg 30 packets, no amplifier)**

| Factor | Value |
|--------|-------|
| Toll collected | ~5 packets/day |
| Charge from tolls | +0.75%/day |
| Decay (stable) | -12%/day |
| **Net** | **-11.25%/day (needs full fuel cell maintenance)** |

#### Strategic Implications

- **Trade routes self-maintain**: Encourages commerce and specialization between worlds
- **Amplifiers on highways**: Clear ROI for investing in busy routes
- **Frontier requires commitment**: Expanding the network costs resources
- **Route optimization matters**: Shorter paths = less toll loss = more competitive prices

---

## 7. Database Schema

### 7.1 New Tables

#### FuelCell Table

| Field | Type | Notes |
|-------|------|-------|
| cell_id | u64 (PK, auto_inc) | Unique identifier |
| owner_identity | Identity | Player who crafted |
| frequency_color | String | "Red", "Green", "Blue", etc. |
| state | String | "Inventory", "Deposited", "Consumed" |
| deposited_sphere_id | Option | FK to DistributionSphere |
| deposit_time | Option | When deposited |
| expiry_time | Option | deposit_time + 24 hours |
| created_at | Timestamp | Craft time |

#### QuantumAmplifier Table

| Field | Type | Notes |
|-------|------|-------|
| amplifier_id | u64 (PK, auto_inc) | Unique identifier |
| sphere_id | u64 | FK to DistributionSphere |
| world_coords | WorldCoords | World location |
| builder_identity | Identity | Player who built |
| built_at | Timestamp | Construction time |
| fuel_multiplier | f32 | Default 1.5 |

#### QuantumStabilizer Table

| Field | Type | Notes |
|-------|------|-------|
| stabilizer_id | u64 (PK, auto_inc) | Unique identifier |
| sphere_id | u64 | FK to DistributionSphere |
| world_coords | WorldCoords | World location |
| builder_identity | Identity | Player who built |
| built_at | Timestamp | Construction time |
| charge_floor | f32 | Default 0.5 (50%) |

#### WorldState Table

| Field | Type | Notes |
|-------|------|-------|
| world_coords | WorldCoords (PK) | World identifier |
| state | String | "Dormant", "Crystallizing", "Active", "Isolated", "Collapsing" |
| crystallization_start | Option | When spawn began |
| isolation_start | Option | When isolation detected |
| collapse_time | Option | Scheduled destruction |
| paths_to_genesis | u32 | Redundancy count |
| last_connectivity_check | Timestamp | Last graph update |
| total_energy_routed | u64 | For spawn calculations |

#### NetworkEdge Table

| Field | Type | Notes |
|-------|------|-------|
| edge_id | u64 (PK, auto_inc) | Unique identifier |
| source_world | WorldCoords | Origin world |
| target_world | WorldCoords | Destination world |
| tunnel_id | u64 | FK to QuantumTunnel |
| is_active | bool | Charge >= 80% |
| is_critical | bool | Removal would isolate worlds |

#### TunnelTraffic Table

| Field | Type | Notes |
|-------|------|-------|
| traffic_id | u64 (PK, auto_inc) | Unique identifier |
| tunnel_id | u64 | FK to QuantumTunnel |
| transfer_time | Timestamp | When transfer occurred |
| sender_identity | Identity | Player who initiated |
| packets_transferred | u32 | Amount sent |
| toll_packets | u32 | Amount consumed as toll |
| charge_added | f32 | Charge % added to tunnel |
| source_world | WorldCoords | Origin |
| destination_world | WorldCoords | Destination |
| is_multi_hop | bool | Part of longer route |

### 7.2 Modified Tables

#### QuantumTunnel (Extended Fields)

| New Field | Type | Notes |
|-----------|------|-------|
| last_decay_tick | Timestamp | Last decay calculation |
| active_fuel_cells | u32 | Count of deposited fuel cells |
| has_amplifier | bool | Cached for quick lookup |
| has_stabilizer | bool | Cached for quick lookup |
| charge_floor | f32 | 0.0 or 0.5 if stabilized |

---

## 8. Reducers

### 8.1 Fuel Cell Reducers

| Reducer | Parameters | Returns | Description |
|---------|------------|---------|-------------|
| craft_fuel_cell | frequency_color: String | Result | Craft fuel cell from materials |
| deposit_fuel_cell | cell_id: u64, sphere_id: u64 | Result | Deposit into distribution sphere |
| process_fuel_expiration | (none) | Result | Scheduled: expire old cells, returns count |

### 8.2 Structure Reducers

| Reducer | Parameters | Returns | Description |
|---------|------------|---------|-------------|
| build_amplifier | sphere_id: u64 | Result | Build amplifier on sphere |
| build_stabilizer | sphere_id: u64 | Result | Build stabilizer (requires amplifier) |

### 8.3 Decay & Network Reducers

| Reducer | Parameters | Returns | Description |
|---------|------------|---------|-------------|
| process_tunnel_decay | (none) | Result | Scheduled every 5 min |
| update_network_connectivity | (none) | Result | Rebuild connectivity graph |
| process_world_states | (none) | Result | Handle state transitions |
| check_crystallization_conditions | (none) | Result | Check spawn triggers |

### 8.4 Emergency Reducers

| Reducer | Parameters | Returns | Description |
|---------|------------|---------|-------------|
| emergency_evacuate | (none) | Result | Teleport from isolated world |
| emergency_storage_transfer | device_id: u64, destination: WorldCoords | Result | Transfer with 50% loss |

### 8.5 Transfer Toll Reducers

| Reducer | Parameters | Returns | Description |
|---------|------------|---------|-------------|
| transfer_packets | destination: WorldCoords, packets: Vec&lt;WavePacketSample&gt; | Result | Transfer with toll calculation |
| calculate_toll | packets: u32, tunnel_id: u64 | TollResult | Preview toll without executing |
| get_route_toll | source: WorldCoords, destination: WorldCoords, packets: u32 | RouteTollResult | Calculate multi-hop total toll |

#### TollResult Structure

| Field | Type | Notes |
|-------|------|-------|
| toll_packets | u32 | Packets consumed as toll |
| packets_delivered | u32 | Packets that arrive |
| charge_added | f32 | Charge added to tunnel |
| has_amplifier | bool | Whether amplifier reduced toll |

#### RouteTollResult Structure

| Field | Type | Notes |
|-------|------|-------|
| hops | Vec&lt;WorldCoords&gt; | Route taken |
| total_toll | u32 | Sum of all hop tolls |
| final_delivery | u32 | Packets arriving at destination |
| per_hop_toll | Vec&lt;u32&gt; | Toll at each hop |

---

## 9. Unity Integration

### 9.1 New Components

**WorldNetwork/**

| Component | Purpose |
|-----------|---------|
| NetworkConnectivityManager.cs | Tracks paths to Genesis |
| WorldStateVisualizer.cs | Skybox, warnings, effects |
| CrystallizationEffect.cs | World spawn animation |
| CollapseSequenceController.cs | World destruction sequence |

**ChargingUnits/**

| Component | Purpose |
|-----------|---------|
| FuelCellManager.cs | Crafting, inventory, deposit |
| FuelCellVisual.cs | Inventory item visual |
| AmplifierVisualizer.cs | Placed amplifier rendering |
| StabilizerVisualizer.cs | Stabilizer crystal rendering |
| ChargeFlowEffect.cs | Energy pulse on fuel deposit |

**UI/**

| Component | Purpose |
|-----------|---------|
| NetworkMapUI.cs | Graph visualization |
| TunnelHealthUI.cs | Per-tunnel status |
| IsolationWarningUI.cs | Full-screen warnings |
| WorldStateIndicator.cs | HUD element |

**Events/**

| Component | Purpose |
|-----------|---------|
| NetworkEvents.cs | Connectivity change events |
| WorldStateEvents.cs | Isolation, crystallization events |
| ChargingUnitEvents.cs | Fuel/amplifier/stabilizer events |

### 9.2 Visual Effects Prefabs

**Crystallization/**
- WorldFormingParticles.prefab
- CrystallizationShader.shader
- EnergyConvergenceBeams.prefab

**Collapse/**
- SpireExplosion.prefab
- WorldImplosion.prefab
- ShockwaveEffect.prefab
- CollapseDebris.prefab

**TunnelStates/**
- StableBeam.prefab
- FlickeringBeam.prefab
- CriticalBeam.prefab
- FailingBeam.prefab

**ChargingUnits/**
- FuelCellDeposit.prefab
- AmplifierFrame.prefab
- StabilizerCrystals.prefab
- ChargeFlowPulse.prefab

---

## 10. Implementation Phases

### Phase 1: Fuel Cells & Basic Decay (Weeks 1-3)

- [ ] FuelCell table and reducers
- [ ] Fuel cell crafting UI
- [ ] Deposit interaction at Distribution Sphere
- [ ] Basic tunnel decay tick
- [ ] Tunnel visual states (stable/flickering/critical/failing)
- [ ] Tunnel collapse event

### Phase 2: Amplifiers & Network Tracking (Weeks 4-6)

- [ ] QuantumAmplifier table and reducers
- [ ] Amplifier crafting and placement
- [ ] Amplifier visual (frame around sphere)
- [ ] WorldState table
- [ ] Network connectivity graph
- [ ] Path-to-Genesis calculation
- [ ] Critical tunnel identification

### Phase 3: World Isolation & Destruction (Weeks 7-9)

- [ ] Isolation detection and state transition
- [ ] 24-hour grace period timeline
- [ ] Skybox/warning visual effects
- [ ] Emergency evacuation reducer
- [ ] Emergency storage transfer
- [ ] World collapse animation sequence
- [ ] World → Dormant state transition

### Phase 4: Stabilizers & World Spawning (Weeks 10-12)

- [ ] QuantumStabilizer table and reducers
- [ ] Stabilizer crafting and placement
- [ ] Stabilizer visual (floating crystals)
- [ ] Charge floor enforcement
- [ ] World crystallization conditions
- [ ] Crystallization animation
- [ ] Dormant → Active state transition
- [ ] Network map UI

### Phase 5: Polish & Balance (Weeks 13-14)

- [ ] Decay rate tuning
- [ ] Economic balance pass
- [ ] Visual polish
- [ ] Sound design integration
- [ ] Achievement system integration
- [ ] Tutorial/onboarding for new mechanics

---

## 11. Open Questions

### Gameplay

1. **Tunnel Directionality**: Are tunnels bidirectional for connectivity, or does each direction need separate maintenance?

2. **World Ownership**: Should the first player at a crystallizing world get special status/bonuses?

3. **Decay Pause**: Should decay pause when no players are online? (Prevents overnight collapse but reduces urgency)

4. **Guild Structures**: Can guilds pool resources for shared amplifiers/stabilizers?

### Technical

5. **Scheduled Reducers**: How to implement 5-minute decay ticks? SpacetimeDB scheduled reducers or client-triggered?

6. **Connectivity Performance**: BFS on large networks - acceptable latency? Need caching strategy?

7. **Collapse Synchronization**: How to synchronize dramatic collapse sequence across all connected clients?

### Balance

8. **Solo Viability**: Can a solo player meaningfully participate, or is this inherently guild-focused?

9. **Early Game**: How do new players experience this before they can craft fuel cells?

10. **Cascade Prevention**: Should there be any "circuit breaker" to prevent total network collapse?

---

## 12. Appendix: Quick Reference

### Crafting Recipes Summary

| Item | Ingredients | Output |
|------|-------------|--------|
| Fuel Cell | 4 Energy Points + 1 Tetrahedron | 1 Fuel Cell |
| Amplifier | 1 Dodecahedron + 1 Icosahedron + 200 packets | 1 Amplifier |
| Stabilizer | 3 Icosahedra + 500 Blue + 100 Magenta packets | 1 Stabilizer |

### Decay Rates Summary

| Charge | State | Rate | Tunnel Lifetime (no fuel) |
|--------|-------|------|---------------------------|
| 80-100% | Stable | -0.5%/h | 160 hours |
| 50-79% | Weakening | -1.0%/h | 29 hours |
| 20-49% | Critical | -2.0%/h | 15 hours |
| 1-19% | Failing | -3.0%/h | 6 hours |

### World Spawn Conditions Summary

| World Type | Adjacent Active | Energy Flow | Sustain Time |
|------------|-----------------|-------------|--------------|
| Main Grid | 2+ | 500 pkt/h | 6 hours |
| Face-Center | 4+ | 1,000 pkt/h | 12 hours |
| Cube-Center | 8+ | 10,000 pkt/h | 24 hours |

### Transfer Toll Summary

| Component | Base | With Amplifier |
|-----------|------|----------------|
| Minimum | 1 packet | 1 packet |
| Percentage | +2% | +1% |
| Cap | 10 packets | 5 packets |
| Charge per toll packet | +0.15% | +0.15% |
| Distribution | 50/50 both endpoints | 50/50 both endpoints |

### Route Self-Sustainability Thresholds

| Tunnel State | Decay/Day | Transfers Needed/Day (avg 50 pkt) |
|--------------|-----------|-----------------------------------|
| Stable (no amp) | -12% | ~40 transfers |
| Stable (with amp) | -12% | ~55 transfers |
| Weakening (no amp) | -24% | ~80 transfers |
