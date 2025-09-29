# Energy Spire Circuit Visualization System

## Overview
The Circuit Visualization System implements quantum circuit structures on spherical worlds in an FCC (Face-Centered Cubic) lattice arrangement. Each circuit consists of a base platform on the world surface, an energy conduit, a distribution sphere, and a rotating ring assembly for quantum tunnel connections.

## World Radius Consistency
**CRITICAL**: The system uses a consistent world radius R = 300 units throughout. All calculations are based on this fundamental unit:
- World radius: R = 300 units
- Lattice spacing: 10R = 3000 units
- Face-center offset: 5R = 1500 units
- Cube-center offset: 5R = 1500 units

## Architecture

### Component Hierarchy
```
CircuitSpire (GameObject)
├── CircuitBase              // Ground-level platform on world surface
│   ├── BaseMesh             // Hexagonal/octagonal platform
│   ├── CircuitLight         // Point light for charge visualization
│   └── BaseParticles        // Particle system for energy effects
├── EnergyConduit            // Vertical connector (30-45 units)
│   ├── ConduitMesh          // Cylindrical/prismatic mesh
│   └── FlowParticles        // Energy flow particles
├── DistributionSphere       // Coverage visualization (35-45 units radius)
│   ├── SphereMesh           // Semi-transparent sphere
│   └── OrbitingParticles    // Field effect particles
└── RingAssembly             // Tunnel connection ring (60-75 units above surface)
    ├── RingMesh             // Torus mesh for ring
    └── ConnectionEffects    // Tunnel alignment effects
```

### Core Components

#### CircuitConstants.cs
Central configuration for all circuit parameters:
- World radius definitions (R = 300)
- FCC lattice spacing (10R = 3000)
- Circuit sizing ratios
- Performance settings
- Color definitions for frequency bands

#### CircuitBase.cs
Manages the ground-level circuit visualization:
- Positioned on world surface at radius R from center
- Handles charge level (0-100%)
- Cardinal direction placement (26 possible positions)
- Pulse effects and particle systems
- Integration with SpacetimeDB circuit data

#### DistributionSphere.cs
Visualizes the energy coverage area:
- Expands/contracts based on charge level
- Semi-transparent with rim lighting
- Orbiting particle field
- Overlap detection with nearby circuits
- LOD system for performance

#### RingAssemblyController.cs
Controls the single ring per circuit for tunnel connections:
- Rotates to align with target worlds
- Supports directional tunnels (different colors per direction)
- Gimbal-style rotation for any connection angle
- Pulse effects when connected
- Visual feedback for tunnel strength

#### QuantumTunnelRenderer.cs
Renders curved energy beams between circuits:
- Quadratic Bezier curves for smooth paths
- Energy packet flow animation
- Bi-directional flow support
- Adaptive quality based on distance
- Particle effects along beam

#### CircuitVisualizationManager.cs
Main integration component:
- Spawns circuits based on WorldCircuit database
- Maps WorldCoords to Unity positions (10R spacing)
- Manages object pooling for performance
- Handles EventBus integration
- Creates/destroys tunnel connections
- LOD and culling management

## Circuit Placement

### Cardinal Directions
Circuits are placed at specific positions on each world sphere:

**Primary (6 positions)**: Face centers
- NorthPole (0, R, 0)
- SouthPole (0, -R, 0)
- East (R, 0, 0)
- West (-R, 0, 0)
- Front (0, 0, R)
- Back (0, 0, -R)

**Secondary (8 positions)**: Vertices
- NorthEastFront, NorthEastBack
- NorthWestFront, NorthWestBack
- SouthEastFront, SouthEastBack
- SouthWestFront, SouthWestBack

**Tertiary (12 positions)**: Edges
- NorthEast, NorthWest, NorthFront, NorthBack
- SouthEast, SouthWest, SouthFront, SouthBack
- EastFront, EastBack, WestFront, WestBack

## World Positioning

### FCC Lattice System
Worlds are positioned in a Face-Centered Cubic lattice with spacing based on R:

```csharp
// Main grid vertices (spacing = 10R = 3000 units)
WorldCoords(1, 0, 0) → Unity position (3000, 0, 0)
WorldCoords(0, 1, 0) → Unity position (0, 3000, 0)
WorldCoords(0, 0, 1) → Unity position (0, 0, 3000)

// Face-center worlds (offset = 5R = 1500 units along one axis)
// Example: Face center between origin and (1,0,0)
Position = (1500, 0, 0)

// Cube-center worlds (offset = 5R = 1500 units along all axes)
// Example: Cube center at logical (0,0,0)
Position = (1500, 1500, 1500)
```

## Tunnel System

### Tunnel Types
Six frequency-based tunnel types with unique colors:
- Blue (0.2, 0.2, 1.0)
- Red (1.0, 0.2, 0.2)
- Green (0.2, 1.0, 0.2)
- Yellow (1.0, 1.0, 0.2)
- Cyan (0.2, 1.0, 1.0)
- Magenta (1.0, 0.2, 1.0)

### Directional Tunnels
A single circuit can connect to different worlds in different directions:
- Ring rotates to point toward connected world
- Ring color indicates tunnel type
- Example: Blue tunnel to +Z world, Red tunnel to +X world

### Connection Process
1. Circuits charge to 80% threshold
2. Ring assemblies align toward target worlds
3. Tunnel beam forms between aligned rings
4. Energy packets begin flowing
5. Visual effects indicate active connection

## Event Integration

The system integrates with GameEventBus for state management:

### Circuit Events
- `CircuitInsertedEvent`: New circuit added to database
- `CircuitUpdatedEvent`: Circuit properties changed
- `CircuitDeletedEvent`: Circuit removed
- `TunnelFormedEvent`: Tunnel connection established
- `TunnelBrokenEvent`: Tunnel connection severed

These events are allowed in PlayerReady, LoadingWorld, and InGame states.

## Performance Optimization

### Object Pooling
- Pre-spawned pools for each circuit type
- Configurable pool size (default: 50 per type)
- Reuse of tunnel renderer objects

### LOD System
- Full detail < 100 units
- Simplified 100-500 units
- Hidden particles > 500 units
- Culling distance: 1000 units

### Adaptive Quality
- Beam segment reduction at distance
- Particle count scaling
- Shader complexity reduction

## Usage Example

### Creating a Circuit Programmatically
```csharp
// The CircuitVisualizationManager handles this automatically, but for manual creation:

// 1. Get or create world at coordinates
GameObject world = GetOrCreateWorld(new WorldCoords(1, 0, 0));

// 2. Spawn circuit prefab
GameObject circuit = Instantiate(circuitSpirePrefab, world.transform);

// 3. Initialize circuit base
CircuitBase circuitBase = circuit.GetComponentInChildren<CircuitBase>();
circuitBase.Initialize(
    circuitId: 12345,
    coords: new WorldCoords(1, 0, 0),
    direction: CardinalDirection.NorthPole,
    world: world.transform
);

// 4. Start charging
circuitBase.SetCharging(true);

// 5. When ready, form tunnel
RingAssemblyController ring = circuit.GetComponentInChildren<RingAssemblyController>();
ring.ConnectToWorld(targetWorldPosition, TunnelType.Blue, 1.0f);
```

### Responding to Circuit Events
```csharp
// Subscribe to circuit events
GameEventBus.Instance.Subscribe<CircuitInsertedEvent>(OnCircuitInserted);

private void OnCircuitInserted(CircuitInsertedEvent evt)
{
    Debug.Log($"New circuit {evt.Circuit.CircuitId} at world {evt.Circuit.WorldCoords}");
    // Circuit visualization is automatically created by CircuitVisualizationManager
}
```

## Prefab Setup

### Creating Circuit Prefab in Unity
1. Create empty GameObject "CircuitSpire"
2. Add child "CircuitBase" with CircuitBase.cs component
3. Add child "DistributionSphere" with DistributionSphere.cs
4. Add child "RingAssembly" with RingAssemblyController.cs
5. Configure materials and particle systems
6. Save as prefab

### Material Requirements
- Base platform: Emissive material with _ChargeLevel parameter
- Distribution sphere: Transparent material with rim lighting
- Ring assembly: Metallic material with emission
- Tunnel beam: Additive particle material

## Testing

### Validation Checklist
1. ✅ World radius consistency (R = 300)
2. ✅ Lattice spacing (10R = 3000)
3. ✅ Circuit placement at R distance from world center
4. ✅ Ring rotation toward target worlds
5. ✅ Tunnel beam curvature
6. ✅ Energy packet flow
7. ✅ Charge visualization
8. ✅ Distribution sphere scaling
9. ✅ LOD transitions
10. ✅ Event integration

### Debug Settings
Enable debug visualization in CircuitVisualizationManager:
- `showDebugInfo`: General debug output
- `showWorldGrid`: Visualize world spheres
- `highlightActiveCircuits`: Highlight active circuits

Individual components also have debug gizmos:
- CircuitBase: `showDebugGizmos`
- DistributionSphere: `showDebugGizmos`, `showOverlapConnections`
- RingAssemblyController: `showDebugGizmos`, `showTargetDirection`
- QuantumTunnelRenderer: `showDebugPath`, `showPacketTrajectories`

## Future Enhancements

### Planned Features
- Multiple rings per circuit for complex connections
- Dynamic circuit types based on world properties
- Energy efficiency bonuses for overlapping distribution spheres
- Circuit upgrade paths
- Visual effects for overload conditions
- Network visualization mode
- Circuit blueprint system

### Optimization Opportunities
- GPU instancing for similar circuits
- Mesh combining for static elements
- Texture atlasing for materials
- Compute shader for particle updates
- Hierarchical LOD system

## Troubleshooting

### Common Issues

**Circuits not appearing:**
- Check CircuitVisualizationManager is in scene
- Verify GameEventBus state allows circuit events
- Ensure SpacetimeDB connection is established
- Check console for error messages

**Wrong world positions:**
- Verify world radius is 300 units
- Check CircuitConstants.LATTICE_SPACING = 3000
- Ensure WorldCoords mapping uses correct formula

**Tunnels not forming:**
- Verify both circuits are charged > 80%
- Check ring assemblies are properly initialized
- Ensure target world position is correct
- Verify tunnel renderer prefab is assigned

**Performance issues:**
- Enable object pooling
- Adjust LOD distances
- Reduce particle counts
- Enable adaptive quality
- Increase culling distance