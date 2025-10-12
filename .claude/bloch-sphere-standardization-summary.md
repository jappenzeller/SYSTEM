# Bloch Sphere Coordinate Standardization Summary

**Date:** 2025-10-12
**Status:** ✅ Complete
**Purpose:** Summary of Bloch sphere coordinate system standardization across SYSTEM codebase

---

## What Was Done

### 1. Created Master Reference Document
**File:** `.claude/bloch-sphere-coordinates-reference.md`

Comprehensive reference guide defining:
- **Standard mapping**: +Y is north pole (|0⟩ state)
- **Theta (θ)**: [0, π] polar angle from +Y axis
- **Phi (φ)**: [0, 2π] azimuthal angle in XZ plane
- **Quantum state positions**: All 6 basis states
- **Conversion formulas**: Spherical ↔ Cartesian
- **Code examples**: HLSL, C#, Rust

### 2. Updated Documentation

#### CLAUDE.md
- Added reference to Bloch sphere coordinate system
- Clarified quantum state marker positions (+Y north pole)
- Added link to reference document

#### technical-architecture-doc.md
- Updated shader coordinate documentation
- Replaced generic "longitude/latitude" with proper Bloch sphere terminology
- Added quantum state markers table with theta/phi values
- Added reference link

#### debug-commands-reference.md
- Added coordinate system section to "World Surface Information"
- Documented +Y as north pole, XZ plane as equator
- Added quantum state mapping
- Added reference link

### 3. Added Code Comments

#### WorldSphereEnergy.shader
```hlsl
// Bloch sphere spherical coordinates (Unity convention: +Y is north pole)
// theta: [0, π] polar angle from +Y axis
// phi: [-π, π] azimuthal angle in XZ plane
// See: .claude/bloch-sphere-coordinates-reference.md
```

#### WorldCircuitEmissionController.cs
```csharp
// Standard Bloch sphere: theta from +Y (north pole), phi in XZ plane
// See: .claude/bloch-sphere-coordinates-reference.md
float theta = Mathf.PI * (lat + 0.5f) / latitudeBands; // [0, π] from +Y axis
float phi = 2f * Mathf.PI * lon / lonSegments;         // [0, 2π] in XZ plane
```

---

## Coordinate System Summary

### Unity Axes → Bloch Sphere Mapping

| Unity Axis | Quantum State | Theta | Phi | Game Meaning |
|------------|---------------|-------|-----|--------------|
| +Y | \|0⟩ | 0 | - | North pole, "up" |
| -Y | \|1⟩ | π | - | South pole, "down" |
| +X | \|+⟩ | π/2 | 0 | Equator, "right" |
| -X | \|-⟩ | π/2 | π | Equator, "left" |
| +Z | \|+i⟩ | π/2 | π/2 | Equator, "forward" |
| -Z | \|-i⟩ | π/2 | 3π/2 | Equator, "backward" |

### Key Formulas

**Spherical → Cartesian:**
```
x = sin(θ) * cos(φ)
y = cos(θ)              // +Y is north pole
z = sin(θ) * sin(φ)
```

**Cartesian → Spherical:**
```
θ = acos(y / |r|)
φ = atan2(z, x)
```

---

## Why This Matters

### 1. **Physics Standard Compliance**
- Matches textbook Bloch sphere representation
- North pole (+Y) = |0⟩ is universal convention
- Makes codebase understandable to quantum physicists

### 2. **Unity Convention Alignment**
- +Y is "up" in Unity
- Natural mapping: up = |0⟩, down = |1⟩
- XZ plane is horizontal = equator = superposition states

### 3. **Code Consistency**
- All spherical coordinate code uses same convention
- No confusion between different coordinate systems
- Easy to verify correctness against standard formulas

### 4. **Future-Proofing**
- Ready for quantum circuit minigame implementation
- Clear standard for implementing BlochState types
- Documentation for onboarding new developers

---

## Files Modified

### Documentation
- ✅ `.claude/bloch-sphere-coordinates-reference.md` (NEW)
- ✅ `CLAUDE.md` - Added reference and clarifications
- ✅ `.claude/technical-architecture-doc.md` - Updated shader docs
- ✅ `.claude/debug-commands-reference.md` - Added coordinate system info
- ✅ `.claude/documentation-plan.md` - Marked as complete

### Code Comments
- ✅ `SYSTEM-client-3d/Assets/Shaders/WorldSphereEnergy.shader`
- ✅ `SYSTEM-client-3d/Assets/Scripts/WorldCircuitEmissionController.cs`

### No Changes Needed (Already Correct!)
- ✅ Shader implementation - Already using correct formulas
- ✅ Quantum state marker positions - Already at correct positions
- ✅ Grid line calculations - Already using proper theta/phi

---

## Future Work

### When Implementing Quantum Circuit Minigame

**Rust Server (BlochState type):**
```rust
#[spacetimedb(table)]
pub struct BlochState {
    pub theta: f32,  // [0, π] from +Y axis
    pub phi: f32,    // [0, 2π] in XZ plane
}
```

**Unity Client (Visualization):**
- Use reference formulas for Cartesian ↔ Spherical conversion
- Visual rotation around X, Y, Z axes for quantum gates
- Bloch sphere UI showing current state position

**Reference Materials:**
- All formulas documented in `.claude/bloch-sphere-coordinates-reference.md`
- Code examples ready to copy
- Coordinate system already standardized

---

## Testing Verification

### Visual Confirmation
The quantum state markers in the shader are at the correct positions:
- |0⟩ at (0, 1, 0) - ✅ Visible at north pole (+Y)
- |1⟩ at (0, -1, 0) - ✅ Visible at south pole (-Y)
- |+⟩ at (1, 0, 0) - ✅ Visible at equator +X
- |-⟩ at (-1, 0, 0) - ✅ Visible at equator -X
- |+i⟩ at (0, 0, 1) - ✅ Visible at equator +Z (forward)
- |-i⟩ at (0, 0, -1) - ✅ Visible at equator -Z (backward)

### Formula Verification
All spherical coordinate calculations follow the standard:
- ✅ `theta = acos(y)` - Polar angle from +Y
- ✅ `phi = atan2(z, x)` - Azimuthal in XZ plane
- ✅ Cartesian conversion uses correct trigonometry

---

## Benefits Achieved

1. ✅ **Consistency** - All code uses same coordinate system
2. ✅ **Clarity** - Clear documentation for current and future developers
3. ✅ **Correctness** - Matches physics/quantum computing standards
4. ✅ **Maintainability** - Single source of truth for coordinate formulas
5. ✅ **Extensibility** - Ready for quantum circuit implementation

---

## References

### Primary Documentation
- `.claude/bloch-sphere-coordinates-reference.md` - Master reference

### Related Documentation
- `CLAUDE.md` - Quantum Grid Shader section
- `.claude/technical-architecture-doc.md` - Shader architecture
- `.claude/gameplay-systems-doc.md` - Quantum circuit minigame design

### External Resources
- Nielsen & Chuang, "Quantum Computation and Quantum Information"
- Wikipedia: Bloch sphere
- Qiskit documentation (IBM Quantum)
