# CenterWorld Prefab Setup Instructions

## Quick Setup Steps

### 1. Update the CenterWorld Prefab Script Reference
1. Open Unity Editor
2. Navigate to `Assets/Prefabs/` folder
3. Select the **CenterWorld** prefab
4. In the Inspector, find the **CenterWorldController** component
5. If it shows "Missing Script":
   - Click the circle icon next to the missing script
   - Select `CenterWorldController` from `Assets/Scripts/Game/`
   - The script should now be properly linked

### 2. Configure the CenterWorld Prefab Components

The CenterWorld prefab should have these components configured:

#### Required Components:
1. **Transform**
   - Position: (0, 0, 0)
   - Rotation: (0, 0, 0)
   - Scale: (600, 600, 600) *[This gives us a 300-unit radius sphere]*

2. **MeshFilter**
   - Mesh: Select Unity's built-in **Sphere** mesh
   - (Click the circle icon → search "Sphere" → select the built-in sphere)

3. **MeshRenderer**
   - Materials: Can be empty (material is set at runtime)
   - Or assign a default material if you prefer

4. **MeshCollider**
   - Convex: **UNCHECKED** (important for accurate collision)
   - Mesh: Select Unity's built-in **Sphere** mesh (same as MeshFilter)

5. **CenterWorldController** Script
   - World Radius: 300
   - Base Material: (optional, can assign or leave empty)
   - Primary Color: Your choice (default: bluish)
   - Enable Rotation: Your choice
   - Show Gizmos: true (helpful for debugging)

### 3. Verify WorldManager Setup

1. In your game scene, find the **WorldManager** GameObject
2. Check the WorldManager component:
   - World Surface Prefab: Should point to the **CenterWorld** prefab
   - World Radius: 300 (should match CenterWorldController)

### 4. Test the Setup

1. Enter Play Mode
2. You should see:
   - A single world sphere at the origin
   - Radius of 300 units (check with gizmos)
   - No duplicate spheres
   - No console errors about missing components

## Architecture Overview

The new architecture works as follows:

```
WorldManager (in scene)
    ↓ Instantiates
CenterWorld Prefab
    ├── Transform (Scale 600,600,600)
    ├── MeshFilter (Unity Sphere)
    ├── MeshRenderer
    ├── MeshCollider (Unity Sphere)
    └── CenterWorldController Script
            ↓ At runtime
        - Gets existing components
        - Applies material
        - Handles rotation
        - Provides world API
```

## Key Changes from Old System

### Before (Complex):
- WorldManager creates CenterWorld prefab
- CenterWorldController creates another sphere prefab inside
- Double instantiation, confusing hierarchy
- Prefab validation and fallback systems

### After (Simple):
- WorldManager creates CenterWorld prefab
- CenterWorld prefab is complete and self-contained
- CenterWorldController configures existing components
- Clean, single-object hierarchy

## Troubleshooting

### "Missing Script" on CenterWorld prefab
- Re-assign the script from `Assets/Scripts/Game/CenterWorldController.cs`

### No sphere visible
- Check MeshFilter has Sphere mesh assigned
- Check Transform scale is (600, 600, 600)
- Check MeshRenderer is enabled

### Double spheres appearing
- Make sure you're using the new CenterWorldController.cs
- Check that worldSpherePrefab field is removed from the script

### Physics not working
- Ensure MeshCollider has Sphere mesh assigned
- Ensure Convex is UNCHECKED on MeshCollider

### World is wrong size
- Transform scale should be 600 (for 300 radius)
- CenterWorldController worldRadius should be 300
- WorldManager worldRadius should be 300

## Benefits of New Architecture

1. **Simplicity**: One prefab, one controller, no confusion
2. **Performance**: No runtime instantiation of child objects
3. **Reliability**: Prefab is pre-configured, less can go wrong
4. **Maintainability**: Clear separation of concerns
5. **WebGL Compatible**: No procedural generation issues

## Next Steps

Once this is working, you can:
1. Customize the material for different visual styles
2. Add world features via the IWorldFeature interface
3. Enable rotation for dynamic worlds
4. Add atmospheric effects as child objects