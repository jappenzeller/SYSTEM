# Quantum Orbital Visualization Framework

A Python-based system for visualizing hydrogen wave functions as they relate to quantum computing concepts. Maps Bloch sphere coordinates to atomic orbital shapes, allowing visualization of quantum states as 3D meshes in Blender.

## Features

- **Accurate Physics**: Implements hydrogen wave functions using Laguerre polynomials and spherical harmonics
- **Bloch Sphere Mapping**: Converts quantum computing states to orbital superpositions
- **3D Mesh Generation**: Creates isosurface meshes using marching cubes algorithm
- **Blender Integration**: Works seamlessly within Blender for visualization
- **Quantum Gates**: Apply standard quantum gates and visualize state transformations
- **Standalone Testing**: Can be used outside Blender for numerical analysis

## Installation

### 1. Install Python Dependencies

```bash
cd SYSTEM-client-3d/Blender
pip install -r requirements.txt
```

### 2. For Blender Integration

Install packages into Blender's Python environment:

**Windows:**
```bash
"C:\Program Files\Blender Foundation\Blender 3.x\3.x\python\bin\python.exe" -m pip install numpy scipy scikit-image
```

**Mac/Linux:**
```bash
/path/to/blender/python/bin/python -m pip install numpy scipy scikit-image
```

## Quick Start

### Standalone Testing

Run the test suite to verify installation:

```bash
cd SYSTEM-client-3d/Blender
python test_quantum_orbitals.py
```

### Blender Usage

1. Open Blender
2. Switch to Scripting workspace
3. Load `example_blender_script.py`
4. Run the script (Alt+P)
5. A 1s orbital mesh will appear in the viewport

## Project Structure

```
SYSTEM-client-3d/Blender/
├── Scripts/
│   ├── Quantum/
│   │   ├── quantum_constants.py       # Physical constants and mappings
│   │   ├── hydrogen_wavefunctions.py  # Wave function calculations
│   │   ├── orbital_coefficients.py    # Bloch sphere mapping
│   │   └── bloch_orbital_mapper.py    # High-level interface
│   └── Generators/
│       └── hydrogen_orbital_meshes.py # Blender mesh generation
├── test_quantum_orbitals.py           # Test suite
├── example_blender_script.py          # Usage examples
├── requirements.txt                   # Python dependencies
└── README.md                          # This file
```

## Core Modules

### quantum_constants.py

Defines physical constants, quantum number ranges, and visualization parameters:

- `BOHR_RADIUS`, `PLANCK_CONSTANT`, etc.
- `validate_quantum_numbers(n, l, m)` - Check quantum number validity
- `BLOCH_PURE_STATES` - Cardinal point mappings
- `QUANTUM_GATES` - Standard gate definitions

### hydrogen_wavefunctions.py

Implements hydrogen orbital wave functions:

```python
from Quantum.hydrogen_wavefunctions import hydrogen_orbital, probability_density

# Calculate wave function at position (x, y, z) in Bohr radii
psi = hydrogen_orbital(n=1, l=0, m=0, x=5.0, y=0.0, z=0.0)

# Get probability density
rho = probability_density(psi)

# Calculate on 3D grid
x_grid, y_grid, z_grid, density = calculate_density_grid(n=1, l=0, m=0, resolution=64)
```

**Key Functions:**
- `radial_wavefunction(n, l, r)` - Radial part R_nl(r)
- `spherical_harmonic(l, m, theta, phi)` - Angular part Y_lm
- `hydrogen_orbital(n, l, m, x, y, z)` - Complete wave function
- `verify_normalization(n, l, m)` - Check ∫|ψ|²dV = 1

### orbital_coefficients.py

Maps Bloch sphere states to orbital mixtures:

```python
from Quantum.orbital_coefficients import bloch_to_orbital_coeffs

# Convert Bloch angles to orbital coefficients
theta, phi = np.pi/2, 0.0  # |+⟩ state
coeffs = bloch_to_orbital_coeffs(theta, phi, basis='sp')
# Returns: {(1,0,0): 0.707, (2,0,0): 0.707}  (equal mix of 1s and 2s)
```

**Key Functions:**
- `bloch_to_state_vector(theta, phi)` - Get [α, β] coefficients
- `bloch_to_orbital_coeffs(theta, phi, basis)` - Get orbital mixture
- `apply_gate_to_bloch(theta, phi, gate)` - Transform state
- `orbital_coeffs_to_density(coeffs, x, y, z)` - Calculate |ψ_total|²

### bloch_orbital_mapper.py

High-level interface for quantum states:

```python
from Quantum.bloch_orbital_mapper import BlochOrbitalState, create_state

# Create state
state = BlochOrbitalState(theta=np.pi/2, phi=0.0, basis='sp')

# Or use convenience function
state = create_state('|+⟩')

# Get orbital mixture
coeffs = state.get_orbital_mixture()

# Apply quantum gate
state.apply_gate('H')  # Hadamard gate

# Calculate density grid
x, y, z, density = state.calculate_density_grid(resolution=64)

# Analyze state
analysis = state.analyze()
print(analysis['probabilities'])
```

**BlochOrbitalState Methods:**
- `set_bloch_state(theta, phi)` - Set from angles
- `set_state_vector([alpha, beta])` - Set from coefficients
- `set_pure_state(name)` - Set to named state
- `apply_gate(gate_type)` - Apply quantum gate
- `calculate_density_grid()` - Generate 3D density
- `analyze()` - Get detailed state information

### hydrogen_orbital_meshes.py

Blender mesh generation (requires Blender):

```python
from Generators.hydrogen_orbital_meshes import create_orbital_mesh

# Create 1s orbital mesh in Blender
objects = create_orbital_mesh(
    n=1, l=0, m=0,
    iso_values=[0.01, 0.05, 0.1],
    resolution=64
)

# Create Bloch state mesh
objects = create_bloch_state_mesh(
    theta=np.pi/2, phi=0.0,
    basis='sp',
    name='plus_state'
)
```

**Key Functions:**
- `create_orbital_mesh(n, l, m, ...)` - Single orbital
- `create_bloch_state_mesh(theta, phi, ...)` - Superposition state
- `create_example_orbitals()` - Demo orbitals (1s, 2s, 2p, 3d)
- `create_bloch_sphere_states()` - All 6 cardinal states
- `animate_gate_operation(...)` - Gate transformation animation

## Usage Examples

### Example 1: Calculate Wave Function

```python
import numpy as np
from Quantum.hydrogen_wavefunctions import hydrogen_orbital

# Calculate 1s orbital at 5 Bohr radii from nucleus
psi_1s = hydrogen_orbital(n=1, l=0, m=0, x=5.0, y=0.0, z=0.0)
print(f"ψ₁ₛ(5,0,0) = {psi_1s:.6f}")

# Calculate 2px orbital along x-axis
psi_2px = hydrogen_orbital(n=2, l=1, m=1, x=10.0, y=0.0, z=0.0, real_form=True)
print(f"ψ₂ₚₓ(10,0,0) = {psi_2px:.6f}")
```

### Example 2: Create Superposition State

```python
from Quantum.bloch_orbital_mapper import create_superposition

# Create (|0⟩ + i|1⟩)/√2 state
state = create_superposition(alpha=1.0, beta=1j, basis='sp')

print(f"State: {state}")
print(f"Bloch angles: θ={state.theta:.4f}, φ={state.phi:.4f}")

# Get orbital probabilities
probs = state.get_orbital_probabilities()
for nlm, prob in probs.items():
    print(f"  P{nlm} = {prob:.4f}")
```

### Example 3: Apply Quantum Gates

```python
from Quantum.bloch_orbital_mapper import create_state

# Start with |0⟩
state = create_state('|0⟩')

# Apply Hadamard: H|0⟩ = |+⟩
state.apply_gate('H')
print(f"After H gate: {state.get_visualization_info()['state_label']}")

# Apply Pauli X: X|+⟩ = |+⟩ (eigenstate)
state.apply_gate('X')

# Apply rotation
state.apply_rotation('Y', np.pi/4)
```

### Example 4: Create Blender Mesh (in Blender)

```python
import bpy
from Generators.hydrogen_orbital_meshes import create_orbital_mesh, clear_orbital_objects

# Clear existing orbitals
clear_orbital_objects()

# Create 2pz orbital
objects = create_orbital_mesh(
    n=2, l=1, m=0,
    iso_values=[0.05, 0.1, 0.2],  # Multiple isosurfaces
    resolution=128,                # High resolution
    name='2pz_orbital'
)

print(f"Created {len(objects)} meshes")
```

### Example 5: Visualize Gate Operation

```python
# In Blender
from Generators.hydrogen_orbital_meshes import animate_gate_operation
import numpy as np

# Animate H gate on |0⟩ state
objects = animate_gate_operation(
    initial_state=(0.0, 0.0),  # θ=0, φ=0 (|0⟩)
    gate='H',
    duration=2.0,
    frames=60,
    basis='sp'
)
```

## Coordinate Systems

### Bloch Sphere Convention

- **|0⟩**: North pole (θ=0, φ=0)
- **|1⟩**: South pole (θ=π, φ=0)
- **|+⟩**: Equator, +X axis (θ=π/2, φ=0)
- **|-⟩**: Equator, -X axis (θ=π/2, φ=π)
- **|+i⟩**: Equator, +Y axis (θ=π/2, φ=π/2)
- **|-i⟩**: Equator, -Y axis (θ=π/2, φ=3π/2)

### Spherical Coordinates

- **r**: Radial distance (in Bohr radii)
- **θ**: Polar angle (0 to π, from +Z axis)
- **φ**: Azimuthal angle (0 to 2π, from +X axis)

### Orbital Basis Options

- **'sp'**: 1s and 2s orbitals (default)
- **'pp'**: 2px, 2py, 2pz orbitals
- **'sd'**: 1s and 3d_z² orbitals

## Quantum Numbers

Hydrogen orbitals are specified by three quantum numbers:

- **n** (principal): 1, 2, 3, ... (shell)
- **l** (azimuthal): 0 to n-1 (subshell: s=0, p=1, d=2, f=3, ...)
- **m** (magnetic): -l to +l (orbital orientation)

**Examples:**
- (1, 0, 0): 1s orbital
- (2, 1, 0): 2p_z orbital
- (2, 1, ±1): 2p_x and 2p_y (real forms)
- (3, 2, 0): 3d_z² orbital

## Testing

The test suite verifies:

1. **Spherical symmetry** of s orbitals
2. **Directional properties** of p orbitals
3. **Correct superposition** from Bloch angles
4. **Normalization**: ∫|ψ|²dV = 1
5. **Orthogonality** between different states
6. **Quantum gate** operations

Run tests:

```bash
python test_quantum_orbitals.py
```

Fast tests (skip slow integrations):
- Answer 'n' when prompted
- Tests 1-4 complete in seconds

Full tests (include normalization/orthogonality):
- Answer 'y' when prompted
- Tests may take 1-2 minutes

## Performance Tips

### Grid Resolution

- **32**: Fast, low detail (testing)
- **64**: Default, good balance
- **128**: High detail, slower
- **256**: Very high detail, very slow

### Isosurface Values

Use fractions of maximum density:
- **0.01**: Outer surface (1%)
- **0.05**: Medium density (5%)
- **0.1**: High density (10%)
- **0.2**: Core region (20%)

More isosurfaces = more meshes = slower but prettier

### Spatial Extent

Default extents are based on principal quantum number:
- n=1: ±10 Bohr radii
- n=2: ±20 Bohr radii
- n=3: ±30 Bohr radii

Increase for diffuse orbitals, decrease for speed.

## Mathematical Details

### Wave Function

Complete hydrogen wave function:

```
ψ_nlm(r,θ,φ) = R_nl(r) × Y_lm(θ,φ)
```

Where:
- R_nl(r): Radial part (Laguerre polynomials)
- Y_lm(θ,φ): Angular part (spherical harmonics)

### Radial Function

```
R_nl(r) = N × exp(-ρ/2) × ρ^l × L_{n-l-1}^{2l+1}(ρ)
```

Where:
- ρ = 2r/(n×a₀)
- a₀ = Bohr radius
- L: Generalized Laguerre polynomial
- N: Normalization constant

### Normalization

All wave functions satisfy:

```
∫∫∫ |ψ_nlm|² r² sin(θ) dr dθ dφ = 1
```

## Known Limitations

1. **Single electron only**: Hydrogen atom only (no multi-electron)
2. **Non-relativistic**: No spin-orbit coupling
3. **Numerical integration**: Normalization checks are approximate
4. **Real spherical harmonics**: Complex phases not visualized directly
5. **Blender dependency**: Mesh generation requires Blender installed

## Future Enhancements

- [ ] Multi-electron configurations
- [ ] Time evolution animation
- [ ] Measurement probability visualization
- [ ] Export to Unity-compatible formats
- [ ] GPU-accelerated density calculations
- [ ] Interactive Blender add-on
- [ ] WebGL visualization
- [ ] Quantum circuit visualization

## References

1. Griffiths, D. J. (2018). *Introduction to Quantum Mechanics* (3rd ed.)
2. Sakurai, J. J., & Napolitano, J. (2017). *Modern Quantum Mechanics* (2nd ed.)
3. Nielsen, M. A., & Chuang, I. L. (2010). *Quantum Computation and Quantum Information*
4. SciPy documentation: `scipy.special` for special functions
5. Blender Python API: https://docs.blender.org/api/current/

## License

Part of the SYSTEM project. See main project license.

## Credits

Created for visualizing quantum states in the SYSTEM multiplayer wave packet mining game.

## Support

For issues or questions, see project documentation or raise an issue in the main repository.
