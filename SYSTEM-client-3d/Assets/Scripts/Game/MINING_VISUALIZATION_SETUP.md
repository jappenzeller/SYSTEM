# Mining Visualization Setup Instructions

## Overview
This document explains how to set up the mining visualization system that shows glowing circles under players who are actively mining.

## Files Created
1. **Shader**: `Assets/Shaders/WorldSphereEnergyMining.shader` - Enhanced shader with mining overlay support
2. **Script**: `Assets/Scripts/Game/MiningVisualizationManager.cs` - Manager that tracks mining players and updates shader

## Setup Steps

### 1. Apply the New Shader to World Material

#### Option A: Update Existing Material (Recommended)
1. Open Unity Editor
2. Navigate to `Assets/Materials/` folder
3. Find the `WorldSphereMaterial`
4. In the Inspector, change the Shader dropdown from "SYSTEM/WorldSphereEnergy" to "SYSTEM/WorldSphereEnergyMining"
5. Adjust the new Mining Overlay properties:
   - **Mining Circle Radius**: 5 (adjust for desired circle size)
   - **Mining Glow Intensity**: 1.5 (adjust for brightness)
   - **Pulse Speed**: 2 (adjust for pulsing animation speed)
   - **Edge Softness**: 0.5 (adjust for edge fade)

#### Option B: Create New Material
1. Right-click in `Assets/Materials/` folder
2. Create → Material
3. Name it "WorldSphereMiningMaterial"
4. Set Shader to "SYSTEM/WorldSphereEnergyMining"
5. Copy settings from original WorldSphereMaterial
6. Apply to CenterWorld prefab

### 2. Ensure MiningVisualizationManager is Active

The `MiningVisualizationManager` is a singleton that auto-creates itself when needed. It will:
- Automatically find the world sphere
- Subscribe to mining events
- Update the shader with player positions

To verify it's working:
1. Enter Play mode
2. Check the Hierarchy for "MiningVisualizationManager" GameObject
3. Enable "Show Debug Info" in the Inspector to see active mining players

### 3. Integration with Existing Mining System

The system automatically integrates with the existing WavePacketMiningSystem:
- When a player calls `StartMining`, they appear with a glowing circle
- When a player calls `StopMining`, their circle disappears
- Player disconnections are handled automatically

### 4. Testing the System

1. Start the game with multiple Unity instances (or multiplayer test)
2. Have players equip different colored crystals
3. Start mining near orbs
4. You should see glowing circles appear under mining players:
   - Red crystal → Red circle
   - Green crystal → Green circle
   - Blue crystal → Blue circle

## Customization

### Adjusting Visual Properties

In the material inspector, you can adjust:
- **Mining Radius**: Size of the glowing circles (1-10 units)
- **Mining Intensity**: Brightness of the glow (0-3)
- **Mining Pulse Speed**: Speed of pulsing animation (0-5)
- **Edge Softness**: How soft the circle edges are (0.1-2)

### Modifying Colors

Edit `MiningVisualizationManager.cs` to change crystal colors:
```csharp
[SerializeField] private Color redCrystalColor = new Color(1f, 0f, 0f, 1f);
[SerializeField] private Color greenCrystalColor = new Color(0f, 1f, 0f, 1f);
// etc...
```

### Performance Tuning

The system supports up to 10 simultaneous mining players by default. To change this:
1. In the shader, modify: `#define MAX_MINING_PLAYERS 10`
2. In MiningVisualizationManager.cs, update: `private const int MAX_MINING_PLAYERS = 10;`

The update interval can be adjusted in the Inspector (default 0.1 seconds).

## Troubleshooting

### Circles Not Appearing
1. Check that the world material uses "SYSTEM/WorldSphereEnergyMining" shader
2. Verify MiningVisualizationManager exists in scene
3. Enable debug mode to see if players are being tracked
4. Check console for any error messages

### Performance Issues
1. Reduce update interval (increase from 0.1 to 0.2 seconds)
2. Lower mining intensity and disable pulsing
3. Reduce MAX_MINING_PLAYERS if not needed

### Visual Issues
1. Adjust Mining Radius if circles are too large/small
2. Tweak Edge Softness for better blending
3. Modify Mining Intensity if too bright/dim

## Technical Details

The system works by:
1. Tracking all players with active mining state
2. Converting world positions to sphere-local coordinates
3. Passing position/color arrays to the shader via MaterialPropertyBlock
4. The shader renders circles using great circle distance calculations
5. Circles curve correctly on the sphere surface

The implementation uses MaterialPropertyBlock to avoid creating material instances, ensuring good performance even with many players.