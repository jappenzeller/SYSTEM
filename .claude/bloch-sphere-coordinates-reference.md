# Bloch Sphere Coordinate System

**Version:** 1.0.0
**Last Updated:** 2025-10-12
**Status:** Standard Reference
**Purpose:** Define the canonical Bloch sphere coordinate system for SYSTEM

---

## Unity Standard Mapping

**IMPORTANT:** +Y is North Pole (|0⟩ state)

This follows Unity's convention where +Y is "up" and aligns with the standard Bloch sphere representation where the north pole represents the |0⟩ quantum state.

---

## Coordinate Axes

### Y-Axis: Computational Basis (|0⟩ to |1⟩)
- **+Y** = |0⟩ state (theta = 0, north pole)
- **-Y** = |1⟩ state (theta = π, south pole)
- **Meaning**: The fundamental quantum computational states

### X-Axis: Superposition Basis (|+⟩ to |-⟩)
- **+X** = |+⟩ state (phi = 0, theta = π/2)
- **-X** = |-⟩ state (phi = π, theta = π/2)
- **Meaning**: Equal superposition states in the X basis
- **States**: |+⟩ = (|0⟩ + |1⟩)/√2, |-⟩ = (|0⟩ - |1⟩)/√2

### Z-Axis: Phase Basis (|+i⟩ to |-i⟩)
- **+Z** = |+i⟩ state (phi = π/2, theta = π/2) - **"Forward" in game**
- **-Z** = |-i⟩ state (phi = 3π/2, theta = π/2) - **"Backward" in game**
- **Meaning**: Equal superposition states with phase difference
- **States**: |+i⟩ = (|0⟩ + i|1⟩)/√2, |-i⟩ = (|0⟩ - i|1⟩)/√2

---

## Spherical Coordinates

### Theta (θ) - Polar Angle
- **Definition**: Angle from the +Y axis (north pole)
- **Range**: [0, π]
- **Values**:
  - θ = 0 → North pole (+Y) → |0⟩
  - θ = π/2 → Equator (XZ plane) → Superposition states
  - θ = π → South pole (-Y) → |1⟩

### Phi (φ) - Azimuthal Angle
- **Definition**: Angle in the XZ plane (horizontal plane)
- **Range**: [0, 2π] or [-π, π]
- **Values**:
  - φ = 0 → +X axis → |+⟩
  - φ = π/2 → +Z axis → |+i⟩ (forward)
  - φ = π → -X axis → |-⟩
  - φ = 3π/2 → -Z axis → |-i⟩ (backward)

---

## Conversion Formulas

### Spherical to Cartesian
```
x = sin(θ) * cos(φ)
y = cos(θ)              // +Y is north pole
z = sin(θ) * sin(φ)
```

**Example:**
```
|0⟩ state: θ=0, φ=0
→ x = sin(0)*cos(0) = 0
→ y = cos(0) = 1
→ z = sin(0)*sin(0) = 0
→ Position: (0, 1, 0) ✓
```

### Cartesian to Spherical
```
θ = acos(y / |r|)       // where |r| = sqrt(x² + y² + z²)
φ = atan2(z, x)
```

**Example:**
```
Position: (1, 0, 0)
→ θ = acos(0/1) = π/2
→ φ = atan2(0, 1) = 0
→ State: |+⟩ at equator on +X axis ✓
```

**Normalizing Phi to [0, 2π]:**
```
if (phi < 0) phi += 2*PI;
```

---

## Game World Alignment

### World Sphere
- **Radius**: 300 units
- **Center**: (0, 0, 0)
- **Surface**: All positions at distance 300 from origin

### Player Orientation
- **"Up"**: Radial direction from world center (surface normal)
- **Default spawn**: (0, 301, 0) - North pole at |0⟩ state
- **Equator**: XZ plane at y=0 (relative to world center)

### Quantum State Markers (Visual)
These are rendered on the world sphere shader:

| State | Position | Theta | Phi | Game Meaning |
|-------|----------|-------|-----|--------------|
| \|0⟩ | (0, 1, 0) | 0 | - | North pole, "up" |
| \|1⟩ | (0, -1, 0) | π | - | South pole, "down" |
| \|+⟩ | (1, 0, 0) | π/2 | 0 | Right |
| \|-⟩ | (-1, 0, 0) | π/2 | π | Left |
| \|+i⟩ | (0, 0, 1) | π/2 | π/2 | Forward |
| \|-i⟩ | (0, 0, -1) | π/2 | 3π/2 | Backward |

---

## Code Examples

### HLSL/Shader (Unity)
```hlsl
// Standard Bloch sphere with +Y as north pole
float3 normalized = normalize(position);
float theta = acos(normalized.y);  // [0, π] from +Y axis
float phi = atan2(normalized.z, normalized.x);  // [-π, π] in XZ plane

// Normalize phi to [0, 2π] if needed
if (phi < 0.0) phi += 2.0 * PI;
```

### C# (Unity)
```csharp
// Convert Bloch sphere coordinates to Unity position
public static Vector3 BlochToCartesian(float theta, float phi)
{
    float x = Mathf.Sin(theta) * Mathf.Cos(phi);
    float y = Mathf.Cos(theta);  // +Y is north pole
    float z = Mathf.Sin(theta) * Mathf.Sin(phi);
    return new Vector3(x, y, z);
}

// Convert Unity position to Bloch coordinates
public static (float theta, float phi) CartesianToBloch(Vector3 pos)
{
    Vector3 normalized = pos.normalized;
    float theta = Mathf.Acos(normalized.y);  // [0, π]
    float phi = Mathf.Atan2(normalized.z, normalized.x);  // [-π, π]

    // Normalize phi to [0, 2π]
    if (phi < 0) phi += 2f * Mathf.PI;

    return (theta, phi);
}
```

### Rust (SpacetimeDB)
```rust
/// Bloch sphere state using Unity convention (+Y is north pole)
#[spacetimedb(table)]
pub struct BlochState {
    pub theta: f32,  // [0, π] - polar angle from +Y axis
    pub phi: f32,    // [0, 2π] - azimuthal angle in XZ plane
}

impl BlochState {
    /// Convert to Cartesian coordinates (Unity space)
    pub fn to_cartesian(&self) -> (f32, f32, f32) {
        let x = self.theta.sin() * self.phi.cos();
        let y = self.theta.cos();  // +Y is north pole
        let z = self.theta.sin() * self.phi.sin();
        (x, y, z)
    }

    /// Create from Cartesian coordinates
    pub fn from_cartesian(x: f32, y: f32, z: f32) -> Self {
        let r = (x*x + y*y + z*z).sqrt();
        let theta = (y / r).acos();  // [0, π]
        let phi = z.atan2(x);        // [-π, π]

        // Normalize phi to [0, 2π]
        let phi = if phi < 0.0 { phi + 2.0 * PI } else { phi };

        BlochState { theta, phi }
    }
}
```

---

## Physical Meaning

### Quantum State Representation
Any pure quantum state can be represented on the Bloch sphere:
```
|ψ⟩ = cos(θ/2)|0⟩ + e^(iφ) sin(θ/2)|1⟩
```

Where:
- **θ** determines the amplitude (probability) of |0⟩ vs |1⟩
- **φ** determines the relative phase between |0⟩ and |1⟩

### Gate Operations
Quantum gates rotate states on the Bloch sphere:
- **X gate**: π rotation around X-axis (|0⟩ ↔ |1⟩)
- **Y gate**: π rotation around Y-axis
- **Z gate**: π rotation around Z-axis (phase flip)
- **H gate**: Hadamard - rotates between Z and X basis

---

## References

### Implementation Files
- **Shader**: `SYSTEM-client-3d/Assets/Shaders/WorldSphereEnergy.shader`
- **World Controller**: `SYSTEM-client-3d/Assets/Scripts/Game/WorldController.cs`
- **Circuit Emission**: `SYSTEM-client-3d/Assets/Scripts/WorldCircuitEmissionController.cs`

### Documentation
- **CLAUDE.md**: Quantum Grid Shader section
- **technical-architecture-doc.md**: Shader coordinate system
- **gameplay-systems-doc.md**: Quantum circuit minigame

### External Resources
- Nielsen & Chuang, "Quantum Computation and Quantum Information"
- Wikipedia: Bloch sphere (https://en.wikipedia.org/wiki/Bloch_sphere)

---

## Version History

- **v1.0.0 (2025-10-12)**: Initial standardization document
  - Established +Y as north pole (|0⟩ state)
  - Documented theta [0, π] and phi [0, 2π] conventions
  - Added conversion formulas and code examples
