# Wave Packet Visualization Architecture
**Version:** 1.0.0
**Last Updated:** 2025-10-04
**Status:** Active
**Dependencies:** [TECHNICAL_ARCHITECTURE.md, GAMEPLAY_SYSTEMS.md]

## Change Log
- v1.0.0 (2025-10-04): Initial documentation of Wave Packet Visualization System

---

## Overview

The Wave Packet Visualization System provides component-based rendering of quantum wave packets for orbs and extraction animations. The system uses 6-frequency composition (Red, Yellow, Green, Cyan, Blue, Magenta) with gaussian standing wave visualization.

### Design Philosophy

- **ScriptableObject Configuration**: Unity-friendly settings management
- **Component-Based Architecture**: MonoBehaviour components for display control
- **Static Utility Classes**: Pure mesh generation without GameObject management
- **Double-Sided Rendering**: Mirrored mesh generation for complete wave visualization
- **Multiple Display Modes**: Static (orbs), Animated (ping-pong), Extraction (one-time)

---

## System Architecture

### File Structure

```
SYSTEM-client-3d/Assets/Scripts/
├── WavePacket/
│   ├── Core/
│   │   ├── WavePacketSettings.cs          # ScriptableObject configuration
│   │   ├── WavePacketMeshGenerator.cs     # Double-sided mesh generation utility
│   │   └── WavePacketDisplay.cs           # Display component
│   ├── WavePacketRenderer.cs              # Base renderer class (legacy)
│   ├── WavePacketTestController.cs        # Parameterized test controller
│   ├── WavePacketRendererTestScene.cs     # Standalone test component
│   └── Editor/
│       └── WavePacketMenuItems.cs         # Unity editor menu items
├── Game/
│   ├── WavePacketOrbVisual.cs             # Orb prefab component
│   └── OrbVisualizationManager.cs         # Orb spawning system
└── Editor/
    └── WavePacketSetupEditor.cs            # Editor setup tools
```

### Component Hierarchy

```
WavePacketOrbVisual (on Orb Prefab)
└── WavePacketDisplay (created at runtime)
    ├── WavePacketSettings (ScriptableObject reference)
    └── MeshFilter + MeshRenderer (for rendering)
```

---

## Core Components

### WavePacketSettings (ScriptableObject)

**Location**: `Assets/Scripts/WavePacket/Core/WavePacketSettings.cs`

**Purpose**: Central configuration for all wave packet rendering parameters

**Key Fields**:
```csharp
public float discRadius = 1f;                    // Maximum radius of wave packet disc
public float extractionDuration = 4f;            // Animation duration (seconds)
public float rotationSpeed = 90f;                // Rotation speed (degrees/second)
public float[] ringRadii = { 0.75f, 0.625f, 0.5f, 0.375f, 0.25f, 0.125f };  // 6 frequency rings
public float ringWidth = 0.03f;                  // Gaussian width of each ring
public float heightScale = 0.00625f;             // Vertical displacement scale
public MeshQuality meshQuality = MeshQuality.Medium;  // 32x32, 64x64, or 128x128 grid
```

**Color Mapping**:
- Red: 0.0 rad (0°)
- Yellow: 1.047 rad (60°)
- Green: 2.094 rad (120°)
- Cyan: 3.142 rad (180°)
- Blue: 4.189 rad (240°)
- Magenta: 5.236 rad (300°)

**Mesh Quality Options**:
- Low: 32x32 grid (2,048 triangles per face)
- Medium: 64x64 grid (8,192 triangles per face) - Recommended
- High: 128x128 grid (32,768 triangles per face)

**Editor Menu**: `SYSTEM → Wave Packet → Create Default Settings`

---

### WavePacketMeshGenerator (Static Utility)

**Location**: `Assets/Scripts/WavePacket/Core/WavePacketMeshGenerator.cs`

**Purpose**: Pure mesh generation utility for creating double-sided wave packet discs

**Key Method**:
```csharp
public static Mesh GenerateWavePacketMesh(
    WavePacketSample[] samples,   // Frequency composition
    WavePacketSettings settings,  // Configuration
    float progress = 1.0f         // Animation progress (0-1)
)
```

**Double-Sided Mesh Architecture**:

1. **Top Face** (lines 30-55):
   - Vertices at `+height` with normals pointing up (`Vector3.up`)
   - Counter-clockwise triangle winding
   - Represents wave packet crest

2. **Bottom Face** (lines 57-83):
   - Vertices at `-height` (mirrored) with normals pointing down (`Vector3.down`)
   - Reversed triangle winding for correct normals
   - Represents wave packet trough

3. **Height Calculation**:
   ```csharp
   float height = CalculateHeightAtRadius(radius, samples, settings);

   // Top face
   vertices.Add(new Vector3(u * maxRadius, height, v * maxRadius));

   // Bottom face (mirrored)
   vertices.Add(new Vector3(u * maxRadius, -height, v * maxRadius));
   ```

4. **Color Calculation**:
   - Finds closest ring to current radius
   - Uses gaussian falloff for smooth transitions
   - Applies frequency-specific color from settings

**Gaussian Standing Wave Formula**:
```csharp
float gaussian = Mathf.Exp(-(distanceFromRing * distanceFromRing) / (2f * ringWidth * ringWidth));
height += sample.Count * heightScale * gaussian;
```

---

### WavePacketDisplay (MonoBehaviour)

**Location**: `Assets/Scripts/WavePacket/Core/WavePacketDisplay.cs`

**Purpose**: Component-based display controller with multiple rendering modes

**Display Modes**:
- **Static**: Fixed visualization (used for orbs)
- **Animated**: Ping-pong animation between states
- **Extraction**: One-time extraction animation

**Render Modes**:
- **GenerateMesh**: Runtime mesh generation (default)
- **UsePrefab**: Use pre-generated mesh from prefab
- **UseExistingMesh**: Operate on existing MeshFilter

**Key Methods**:
```csharp
public void SetComposition(WavePacketSample[] composition)  // Update wave packet composition
public void RefreshVisualization()                          // Regenerate mesh
public void StartAnimation()                                // Begin animation sequence
public void StopAnimation()                                 // End animation
```

**Usage Pattern**:
```csharp
// Create display
GameObject displayObj = new GameObject("WavePacketDisplay");
WavePacketDisplay display = displayObj.AddComponent<WavePacketDisplay>();

// Configure via reflection (since fields are private/serialized)
var settingsField = typeof(WavePacketDisplay).GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance);
settingsField.SetValue(display, wavePacketSettings);

// Set composition
display.SetComposition(wavePacketSamples);
```

**Editor Menu**: `SYSTEM → Wave Packet → Create Display`

---

### WavePacketOrbVisual (Orb Component)

**Location**: `Assets/Scripts/Game/WavePacketOrbVisual.cs`

**Purpose**: Orb prefab component that integrates wave packet visualization

**Key Features**:
- Creates `WavePacketDisplay` child object at runtime
- Configures display mode to Static
- Updates visualization when composition changes
- Fallback to default composition if none provided

**Initialization**:
```csharp
public void Initialize(
    ulong orbId,
    Color color,
    uint packets,
    uint miners,
    List<WavePacketSample> composition = null
)
```

**Default Composition Creation**:
```csharp
private WavePacketSample[] CreateDefaultComposition(Color color)
{
    // Maps Unity color to closest frequency
    // Returns single-frequency composition
}
```

**Visual Update Flow**:
```
OrbVisualizationManager.CreateOrbVisualization()
  └─> WavePacketOrbVisual.Initialize(composition)
      └─> UpdateVisuals()
          └─> WavePacketDisplay.SetComposition()
              └─> WavePacketMeshGenerator.GenerateWavePacketMesh()
```

---

## Integration with Game Systems

### Orb Spawning Flow

```
SpacetimeDB WavePacketOrb Table
  └─> SpacetimeDBEventBridge (OnInsert)
      └─> GameEventBus.Publish<OrbInsertedEvent>()
          └─> OrbVisualizationManager.OnOrbInserted()
              └─> CreateOrbVisualization()
                  ├─> Instantiate orb prefab
                  └─> WavePacketOrbVisual.Initialize(composition)
```

### Wave Packet Composition Data

**Server Type** (`lib.rs`):
```rust
pub struct WavePacketSample {
    frequency: f32,    // Radians (0.0 to 2π)
    amplitude: f32,    // Usually 1.0
    phase: f32,        // Usually 0.0
    count: u32,        // Number of packets at this frequency
}
```

**Client Type** (auto-generated):
```csharp
public partial struct WavePacketSample {
    public float Frequency;
    public float Amplitude;
    public float Phase;
    public uint Count;
}
```

### Frequency to Ring Index Mapping

```csharp
// WavePacketSettings.cs
public int GetRingIndexForFrequency(float frequency)
{
    // Red: 0.0 → ring 0
    // Yellow: 1.047 → ring 1
    // Green: 2.094 → ring 2
    // Cyan: 3.142 → ring 3
    // Blue: 4.189 → ring 4
    // Magenta: 5.236 → ring 5
}
```

---

## Testing System

### WavePacketTestController

**Location**: `Assets/Scripts/WavePacket/WavePacketTestController.cs`

**Purpose**: Parameterized test controller for isolated testing

**Test Capabilities**:
- Individual color testing (6 frequencies)
- Full spectrum testing (all 6 frequencies)
- Mixed RGB testing (custom combinations)
- Flying packet testing (extraction animation)

**Manual Test UI**:
```csharp
void OnGUI()
{
    // Buttons for each test
    // Manual trigger controls
    // Stop extraction
}
```

**Editor Menu**: `SYSTEM → Wave Packet → Create Test Scene GameObject`

### WavePacketRendererTestScene

**Location**: `Assets/Scripts/WavePacket/WavePacketRendererTestScene.cs`

**Purpose**: Standalone test component for legacy renderer testing

**Features**:
- Auto-start with configurable delay
- Sequential test execution
- Pause-at-halfway for inspection
- Position configuration for multiple instances

---

## Editor Tools

### WavePacketMenuItems

**Location**: `Assets/Scripts/WavePacket/Editor/WavePacketMenuItems.cs`

**Menu Items**:
1. `SYSTEM → Wave Packet → Create Default Settings`
   - Creates WavePacketSettings asset at `Assets/Settings/WavePacketSettings_Default.asset`

2. `SYSTEM → Wave Packet → Create Test Scene GameObject`
   - Creates GameObject with WavePacketTestController component

3. `SYSTEM → Wave Packet → Create Display`
   - Creates GameObject with WavePacketDisplay component

### WavePacketSetupEditor

**Location**: `Assets/Editor/WavePacketSetupEditor.cs`

**Purpose**: Editor tools for wave packet prefab setup

**Features**:
- Automatic prefab creation with wave packet visualization
- Settings assignment and validation
- Material setup assistance

---

## Configuration Guide

### Creating Wave Packet Settings

1. **Via Menu**:
   - `SYSTEM → Wave Packet → Create Default Settings`
   - Adjust parameters in Inspector

2. **Manual Creation**:
   - Right-click in Project → Create → SYSTEM → Wave Packet Settings
   - Configure all fields in Inspector

### Setting Up Orb Prefab

1. Create orb prefab base GameObject
2. Add `WavePacketOrbVisual` component
3. Assign `WavePacketSettings` asset to `wavePacketSettings` field
4. Check `useWavePacketDisplay` (should be true by default)
5. Assign prefab to `OrbVisualizationManager.orbPrefab`

### Scaling Configuration

**Default Scale** (1/20 of original extraction system):
- `discRadius = 1f` (was 20f)
- `ringRadii` scaled by 1/20
- `heightScale = 0.00625f` (was 0.125f)
- `ringWidth = 0.03f` (was 0.6f)

**Tuning Guidelines**:
- Increase `discRadius` for larger orbs
- Increase `heightScale` for more pronounced waves
- Increase `ringWidth` for smoother transitions
- Adjust `meshQuality` based on performance needs

---

## Performance Considerations

### Mesh Generation Cost

- **Low Quality**: ~2,048 triangles × 2 faces = 4,096 triangles
- **Medium Quality**: ~8,192 triangles × 2 faces = 16,384 triangles ⭐ Recommended
- **High Quality**: ~32,768 triangles × 2 faces = 65,536 triangles

### Optimization Strategies

1. **Static Orbs**: Generate mesh once, no updates
2. **Object Pooling**: Reuse WavePacketDisplay components (not yet implemented)
3. **LOD System**: Switch mesh quality based on distance (future enhancement)
4. **Batch Rendering**: Use GPU instancing for multiple orbs (future enhancement)

### WebGL Compatibility

- ✅ All components WebGL-compatible
- ✅ No runtime procedural generation required (can use prefabs)
- ✅ Double-sided mesh works on all platforms
- ✅ Shader uses standard URP pipeline

---

## Common Issues and Solutions

### Issue: Orbs Only Show Top Half

**Symptom**: Wave packet disc visible from above but not below

**Cause**: Single-sided mesh (missing bottom face)

**Solution**: Use `WavePacketMeshGenerator` which generates double-sided mesh

**Verification**:
```csharp
// Check vertex count (should be 2x grid size)
int expectedVertices = (resolution + 1) * (resolution + 1) * 2;
Debug.Log($"Mesh vertices: {mesh.vertexCount}, expected: {expectedVertices}");
```

### Issue: Orbs Not Appearing

**Symptom**: Orb GameObject created but no visual

**Checklist**:
1. ✓ `WavePacketOrbVisual.useWavePacketDisplay = true`
2. ✓ `wavePacketSettings` is assigned
3. ✓ Composition array has valid samples
4. ✓ MeshRenderer has valid material
5. ✓ GameObject is active and enabled

**Debug Logging**:
```csharp
// Enable in WavePacketOrbVisual
Debug.Log($"[WavePacketOrbVisual] Creating display for orb {orbId}");
Debug.Log($"[WavePacketOrbVisual] Composition: {currentComposition?.Length ?? 0} samples");
```

### Issue: Wave Packet Too Small/Large

**Symptom**: Visualization doesn't fit orb size

**Solution**: Adjust `discRadius` in WavePacketSettings
```csharp
settings.discRadius = desiredRadius;  // Typically 0.5f to 2.0f for orbs
```

### Issue: Waves Not Smooth

**Symptom**: Blocky or jagged wave appearance

**Solutions**:
1. Increase `meshQuality` to Medium or High
2. Increase `ringWidth` for smoother gaussian falloff
3. Check normals are calculated correctly (should be `Vector3.up` for top, `Vector3.down` for bottom)

---

## Future Enhancements

### Planned Features

1. **Animation System**: Full extraction animation support
2. **Object Pooling**: Reuse WavePacketDisplay components
3. **LOD System**: Distance-based quality switching
4. **GPU Instancing**: Batch rendering for multiple orbs
5. **Shader-Based Rendering**: Move wave calculation to GPU

### Migration Path

**Current**: CPU-based mesh generation
**Future**: GPU-based shader visualization

**Benefits**:
- Lower CPU overhead
- Real-time animation without mesh updates
- Better performance for large numbers of orbs

---

## Related Documentation

- **TECHNICAL_ARCHITECTURE.md**: Overall system architecture
- **GAMEPLAY_SYSTEMS.md**: Mining mechanics and wave packet gameplay
- **SDK_PATTERNS_REFERENCE.md**: SpacetimeDB integration patterns
- **CLAUDE.md**: Quick reference and recent updates

---

## Appendix: Code Examples

### Creating a Wave Packet Display Programmatically

```csharp
using SYSTEM.WavePacket;
using SpacetimeDB.Types;

// Create settings
var settings = ScriptableObject.CreateInstance<WavePacketSettings>();
settings.discRadius = 1f;
settings.meshQuality = MeshQuality.Medium;

// Create display GameObject
GameObject displayObj = new GameObject("MyWavePacket");
WavePacketDisplay display = displayObj.AddComponent<WavePacketDisplay>();

// Configure via reflection
var settingsField = typeof(WavePacketDisplay).GetField("settings",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
settingsField.SetValue(display, settings);

// Create composition
var samples = new WavePacketSample[] {
    new WavePacketSample { Frequency = 0.0f, Amplitude = 1.0f, Phase = 0.0f, Count = 10 },      // Red
    new WavePacketSample { Frequency = 2.094f, Amplitude = 1.0f, Phase = 0.0f, Count = 20 },    // Green
    new WavePacketSample { Frequency = 4.189f, Amplitude = 1.0f, Phase = 0.0f, Count = 15 }     // Blue
};

// Set composition and visualize
display.SetComposition(samples);
```

### Testing Individual Frequencies

```csharp
// Test each frequency independently
float[] frequencies = { 0.0f, 1.047f, 2.094f, 3.142f, 4.189f, 5.236f };
string[] colors = { "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta" };

for (int i = 0; i < 6; i++)
{
    var sample = new WavePacketSample {
        Frequency = frequencies[i],
        Amplitude = 1.0f,
        Phase = 0.0f,
        Count = 20
    };

    display.SetComposition(new[] { sample });
    Debug.Log($"Displaying {colors[i]} wave packet at frequency {frequencies[i]}");

    // Wait or pause for inspection
}
```

### Spawning Mixed Orb via Server Reducer

```bash
# Server reducer: spawn_mixed_orb(x: f32, y: f32, z: f32, red_packets: u32, green_packets: u32, blue_packets: u32)
spacetime call system spawn_mixed_orb 4.90 301.91 5.71 10 20 15
```

This creates an orb with:
- 10 red packets (frequency 0.0)
- 20 green packets (frequency 2.094)
- 15 blue packets (frequency 4.189)

The client automatically visualizes this composition using the WavePacketOrbVisual system.

---

**End of Document**
