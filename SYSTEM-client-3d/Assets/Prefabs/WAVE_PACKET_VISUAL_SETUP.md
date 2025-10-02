# Wave Packet Visual Prefab Setup Guide

## Visual Reference
See `Documentation/wave-packet-sketch.png` for the target visual design showing concentric colored rings with grid distortion effect that warps as wave packets travel.

## Overview
The Wave Packet Visual System creates stunning visual effects when mining quantum orbs:
- **Concentric colored rings** representing the 6 frequency bands (Red→Yellow→Green→Cyan→Blue→Magenta)
- **Grid distortion effect** that warps space as packets move
- **Smooth animations** with rotation, pulsing, and expansion
- **Performance optimized** with object pooling and shader-based distortion

---

## Creating the Concentric Rings Prefab

### Step 1: Create Container GameObject
1. In Unity: **GameObject** → **Create Empty**
2. Name it: `ConcentricRingsPrefab`
3. Position: (0, 0, 0)
4. Rotation: (0, 0, 0)
5. Scale: (1, 1, 1)

### Step 2: Create 6 Ring Objects
Create rings for each frequency band from inner to outer:

```
ConcentricRingsPrefab
├── Ring_0_Red       (Scale: 0.5, 0.02, 0.5)    - Innermost
├── Ring_1_Yellow    (Scale: 0.8, 0.02, 0.8)
├── Ring_2_Green     (Scale: 1.1, 0.02, 1.1)
├── Ring_3_Cyan      (Scale: 1.4, 0.02, 1.4)
├── Ring_4_Blue      (Scale: 1.7, 0.02, 1.7)
└── Ring_5_Magenta   (Scale: 2.0, 0.02, 2.0)    - Outermost
```

For each ring:
1. **Add Child**: Right-click ConcentricRingsPrefab → 3D Object → Cylinder
2. **Name**: According to hierarchy above
3. **Remove Collider**: Select cylinder → Remove Component → Capsule Collider
4. **Set Scale**: As specified above (Y=0.02 for thin rings)
5. **Position**: Keep at (0, 0, 0) local position

### Step 3: Create Ring Materials
For each ring, create a transparent emissive material:

1. **Create Material**:
   - Right-click in Project → Create → Material
   - Name: `RingMaterial_[Color]` (e.g., `RingMaterial_Red`)

2. **Configure Material Settings**:
   ```
   Shader: Universal Render Pipeline/Lit
   Surface Type: Transparent
   Blending Mode: Alpha
   Render Face: Both
   Alpha Clipping: Off
   ```

3. **Set Colors** (matching the sketch):
   - **Red**: (1.0, 0.0, 0.0, 0.8)
   - **Yellow**: (1.0, 1.0, 0.0, 0.8)
   - **Green**: (0.0, 1.0, 0.0, 0.8)
   - **Cyan**: (0.0, 1.0, 1.0, 0.8)
   - **Blue**: (0.0, 0.0, 1.0, 0.8)
   - **Magenta**: (1.0, 0.0, 1.0, 0.8)

4. **Enable Emission**:
   - Check "Emission"
   - Set Emission Color to same as base color
   - Emission intensity: 0.5

5. **Apply to Rings**: Drag materials onto corresponding ring objects

### Step 4: Save as Prefab
1. Drag `ConcentricRingsPrefab` from Hierarchy to `Assets/Prefabs/` folder
2. This creates the prefab asset

---

## Creating the Grid Distortion Plane

### Step 1: Create Grid Plane
1. **GameObject** → **3D Object** → **Plane**
2. Name: `DistortionGridPlane`
3. Position: (0, -0.5, 0) - Slightly below ground level
4. Scale: (10, 1, 10) - Covers 100x100 units

### Step 2: Create Grid Texture (Optional)
Create or use a checkerboard/grid texture:
1. **Create Texture**: 512x512 pixels
2. **Pattern**: Black lines on transparent background
3. **Import Settings**:
   - Texture Type: Default
   - Alpha Source: From Input Texture
   - Alpha Is Transparency: ✓

### Step 3: Create Grid Distortion Material
1. **Create Material**:
   - Right-click in Project → Create → Material
   - Name: `GridDistortionMaterial`

2. **Assign Shader**:
   - Shader: `SYSTEM/WavePacketGridDistortion`

3. **Configure Properties**:
   ```
   Main Texture: [Your grid texture or leave blank for procedural]
   Grid Color: (0.2, 0.3, 0.4, 0.5)
   Distortion Strength: 0.5
   Fade Distance: 20.0
   Wave Speed: 2.0
   Wave Frequency: 10.0
   Grid Scale: 1.0
   Grid Line Width: 0.02
   Emission Intensity: 0.5
   ```

4. **Apply to Plane**: Drag material onto DistortionGridPlane

### Step 4: Save Grid Prefab
1. Drag `DistortionGridPlane` to `Assets/Prefabs/` folder

---

## Setting Up the Mining System

### Step 1: Add WavePacketVisualizer Component
1. Select the GameObject that has `WavePacketMiningSystem`
2. **Add Component** → **Scripts** → **SYSTEM.Game** → **WavePacketVisualizer**

### Step 2: Configure the Visualizer
In the Inspector for WavePacketVisualizer:

#### Wave Visual Components:
- **Concentric Rings Prefab**: Drag your `ConcentricRingsPrefab`
- **Grid Distortion Material**: Drag your `GridDistortionMaterial`
- **Grid Plane Prefab**: Drag your `DistortionGridPlane` (optional)

#### Ring Configuration:
Set frequency colors (or use defaults):
```
Element 0: Red     (1, 0, 0, 0.8)
Element 1: Yellow  (1, 1, 0, 0.8)
Element 2: Green   (0, 1, 0, 0.8)
Element 3: Cyan    (0, 1, 1, 0.8)
Element 4: Blue    (0, 0, 1, 0.8)
Element 5: Magenta (1, 0, 1, 0.8)
```

#### Animation Settings:
- **Ring Expansion Rate**: 2.0
- **Ring Rotation Speed**: 30.0
- **Pulse Curve**: Create custom or use default
- **Pulse Amplitude**: 0.2
- **Grid Distortion Strength**: 0.5

#### Performance:
- **Max Active Packets**: 32
- **Use Object Pooling**: ✓ (recommended)

---

## Testing Your Setup

### Quick Test in Editor
1. Enter Play Mode
2. Open Console → Type debug commands:
```bash
# Spawn test orbs with different frequencies
spacetime call system spawn_test_orb 0 299 0 0 100
spacetime call system spawn_test_orb 20 299 0 2 100
spacetime call system spawn_test_orb -20 299 0 4 100
```

3. Approach an orb and start mining
4. You should see:
   - Concentric rings appear at orb
   - Rings rotate and pulse
   - Grid distorts as packet moves
   - Packet travels to player
   - Effect cleans up on capture

### Debugging
Enable debug visualization:
1. Select GameObject with `WavePacketVisualizer`
2. Enable Gizmos in Scene view
3. Yellow spheres show active packet positions
4. Lines show packet trajectories

---

## Performance Optimization

### Object Pooling
- Pre-creates 10 ring objects
- Reuses them instead of instantiate/destroy
- Reduces GC allocations

### Shader Optimization
- Grid distortion calculated in vertex shader
- Maximum 32 concurrent packets
- Distance-based fading reduces overdraw

### LOD Suggestions
For distant packets:
- Reduce ring count (3 instead of 6)
- Simplify grid distortion
- Lower particle count

---

## Troubleshooting

### Rings Not Visible
- Check material transparency settings
- Verify render queue is set to Transparent (3000)
- Ensure Z-Write is disabled
- Check alpha values aren't too low

### Grid Not Distorting
- Verify shader is assigned correctly
- Check `_ActivePacketCount` is being set
- Ensure material has shader properties exposed
- Grid plane might be too small - increase scale

### Performance Issues
- Reduce `Max Active Packets` to 16 or 8
- Disable grid distortion on low-end devices
- Use simpler ring geometry (quads instead of cylinders)
- Enable object pooling

### Packets Not Moving
- Check `WavePacketMiningSystem` has player transform assigned
- Verify movement coroutines aren't being stopped early
- Check packet speed isn't 0

---

## Visual Customization

### Alternative Styles

#### Holographic Style
- Increase transparency (alpha 0.3-0.5)
- Add rim lighting to materials
- Use additive blending mode
- Increase emission intensity

#### Energy Field Style
- Add particle systems to each ring
- Use noise texture for grid
- Animate material properties
- Add light components to rings

#### Minimalist Style
- Use only 3 rings (RGB)
- Simple unlit shaders
- No grid distortion
- Flat colors

---

## Integration with Other Systems

### With Circuit System
- Align ring colors with circuit tunnel types
- Share distortion data between systems
- Synchronize visual effects

### With Orb Visualization
- Match orb emission colors to packet frequencies
- Coordinate particle effects
- Share material instances for consistency

---

## Next Steps

1. **Create Variations**: Make different prefabs for different world types
2. **Add Sound**: Attach audio sources to rings for mining sounds
3. **Enhance Particles**: Add trailing particles as packets move
4. **Dynamic Colors**: Modulate colors based on extraction success rate
5. **Advanced Shaders**: Add refraction, chromatic aberration effects