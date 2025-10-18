"""
Example Blender Script - Create 1s Orbital Mesh

This script demonstrates how to use the Quantum Orbital Visualization
Framework within Blender to create a 3D mesh of a hydrogen 1s orbital.

USAGE:
1. Open Blender
2. Open Scripting workspace
3. Load this script
4. Run script (Alt+P or click Run Script button)
5. A 1s orbital mesh will appear in the 3D viewport

REQUIREMENTS:
- Blender 3.0 or later
- Python packages: numpy, scipy, scikit-image
  Install in Blender's Python:
    Windows: "C:\Program Files\Blender Foundation\Blender 3.x\3.x\python\bin\python.exe" -m pip install numpy scipy scikit-image
    Mac/Linux: /path/to/blender/python/bin/python -m pip install numpy scipy scikit-image
"""

import bpy
import sys
from pathlib import Path

# Auto-detect Scripts directory
# Method 1: Try to get path from current text block in Blender
script_dir = None
try:
    for text in bpy.data.texts:
        if "example_blender_script" in text.name and text.filepath:
            script_dir = Path(text.filepath).parent / "Scripts"
            break
except:
    pass

# Method 2: Fallback - try relative to this file
if script_dir is None:
    try:
        script_dir = Path(__file__).parent / "Scripts"
    except:
        pass

# Method 3: Hardcoded fallback for known installation
if script_dir is None or not script_dir.exists():
    script_dir = Path(r"H:\SpaceTime\SYSTEM\SYSTEM-client-3d\Blender\Scripts")

# Add to Python path
if script_dir.exists() and str(script_dir) not in sys.path:
    sys.path.insert(0, str(script_dir))
    print(f"Added to path: {script_dir}")
elif not script_dir.exists():
    print(f"WARNING: Scripts directory not found at {script_dir}")
    print("Please run blender_quantum_setup.py first!")

# Import quantum orbital modules
from Generators.hydrogen_orbital_meshes import (
    create_orbital_mesh,
    create_bloch_state_mesh,
    create_example_orbitals,
    create_bloch_sphere_states,
    clear_orbital_objects,
)
from Quantum.bloch_orbital_mapper import create_state
from Quantum.quantum_constants import get_orbital_name

# ============================================================================
# Example 1: Create a Single 1s Orbital
# ============================================================================

def example_1s_orbital():
    """Create a simple 1s orbital mesh."""
    print("\n" + "=" * 70)
    print("EXAMPLE 1: Creating 1s Orbital Mesh")
    print("=" * 70)

    # Clear any existing orbital objects
    clear_orbital_objects()

    # Create 1s orbital
    # Parameters: n=1, l=0, m=0
    print("Creating 1s orbital with default settings...")

    objects = create_orbital_mesh(
        n=1,           # Principal quantum number
        l=0,           # Azimuthal quantum number (0 = s orbital)
        m=0,           # Magnetic quantum number
        iso_values=[0.01, 0.05, 0.1],  # Isosurface levels (1%, 5%, 10% of max)
        resolution=64,  # Grid resolution (higher = more detail, slower)
        name="1s_orbital"
    )

    print(f"Created {len(objects)} isosurface meshes")

    # Center view on orbital
    if objects:
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        bpy.ops.view3d.view_selected()

    print("✓ 1s orbital created successfully!")
    print("  Tip: Switch to Rendered view (Z key) to see materials")

# ============================================================================
# Example 2: Create 2p Orbitals
# ============================================================================

def example_2p_orbitals():
    """Create all three 2p orbitals (px, py, pz)."""
    print("\n" + "=" * 70)
    print("EXAMPLE 2: Creating 2p Orbitals")
    print("=" * 70)

    clear_orbital_objects()

    # Create 2px, 2py, 2pz orbitals
    p_orbitals = [
        (2, 1, 1),   # 2px
        (2, 1, -1),  # 2py
        (2, 1, 0),   # 2pz
    ]

    all_objects = []
    x_spacing = 50  # Spacing between orbitals

    for i, (n, l, m) in enumerate(p_orbitals):
        name = get_orbital_name(n, l, m)
        print(f"Creating {name} orbital...")

        objects = create_orbital_mesh(
            n, l, m,
            iso_values=[0.05, 0.1],
            resolution=64,
        )

        # Position orbitals side by side
        for obj in objects:
            obj.location.x = i * x_spacing

        all_objects.extend(objects)

    print(f"✓ Created {len(all_objects)} total meshes")

# ============================================================================
# Example 3: Create Bloch Sphere State
# ============================================================================

def example_bloch_state():
    """Create a quantum superposition state from Bloch sphere coordinates."""
    print("\n" + "=" * 70)
    print("EXAMPLE 3: Creating Bloch State |+⟩")
    print("=" * 70)

    clear_orbital_objects()

    # Create |+⟩ = (|0⟩ + |1⟩)/√2 state
    # On Bloch sphere: θ=π/2 (equator), φ=0 (x-axis)

    import numpy as np

    theta = np.pi / 2  # Equator
    phi = 0.0          # Along +x

    print(f"Creating state with θ={theta:.4f}, φ={phi:.4f}")
    print("This represents: |+⟩ = (|0⟩ + |1⟩)/√2")
    print("In orbital basis: equal mix of 1s and 2s")

    objects = create_bloch_state_mesh(
        theta=theta,
        phi=phi,
        basis='sp',  # Use s-orbital basis (1s and 2s)
        iso_values=[0.05, 0.1],
        resolution=64,
        name="plus_state"
    )

    print(f"✓ Created {len(objects)} meshes for superposition state")

# ============================================================================
# Example 4: Create Multiple Bloch States
# ============================================================================

def example_all_cardinal_states():
    """Create all 6 cardinal Bloch sphere states."""
    print("\n" + "=" * 70)
    print("EXAMPLE 4: Creating All Cardinal Bloch States")
    print("=" * 70)

    clear_orbital_objects()

    # Use the convenience function
    objects = create_bloch_sphere_states()

    print(f"✓ Created {len(objects)} meshes total")
    print("  States created: |0⟩, |1⟩, |+⟩, |-⟩, |+i⟩, |-i⟩")

# ============================================================================
# Example 5: Create Multiple Orbital Types
# ============================================================================

def example_all_orbitals():
    """Create example s, p, and d orbitals."""
    print("\n" + "=" * 70)
    print("EXAMPLE 5: Creating Multiple Orbital Types")
    print("=" * 70)

    clear_orbital_objects()

    # Use the convenience function
    objects = create_example_orbitals()

    print(f"✓ Created {len(objects)} meshes total")
    print("  Orbitals: 1s, 2s, 2pz, 2px, 3dz²")

# ============================================================================
# Example 6: Custom State Analysis
# ============================================================================

def example_state_analysis():
    """Create a custom state and print analysis."""
    print("\n" + "=" * 70)
    print("EXAMPLE 6: State Analysis and Visualization")
    print("=" * 70)

    clear_orbital_objects()

    # Create a custom superposition: (|0⟩ + i|1⟩)/√2
    state = create_state('|+i⟩', basis='sp')

    # Print analysis
    print("\nState Analysis:")
    print(f"  {state}")

    analysis = state.analyze()
    print(f"\n  Bloch coordinates: θ={analysis['theta']:.4f}, φ={analysis['phi']:.4f}")

    print("\n  Orbital probabilities:")
    for nlm, prob in analysis['probabilities'].items():
        n, l, m = nlm
        name = get_orbital_name(n, l, m)
        print(f"    {name}: {prob:.4f} ({prob*100:.1f}%)")

    print("\n  Coherence:", analysis['coherence'])

    # Create mesh
    import numpy as np
    objects = create_bloch_state_mesh(
        state.theta,
        state.phi,
        basis='sp',
        iso_values=[0.05, 0.1],
        resolution=64,
    )

    print(f"\n✓ Created visualization with {len(objects)} meshes")

# ============================================================================
# Main Script Execution
# ============================================================================

if __name__ == "__main__":
    print("\n" + "=" * 70)
    print("QUANTUM ORBITAL VISUALIZATION - BLENDER EXAMPLES")
    print("=" * 70)

    # Choose which example to run
    # Uncomment ONE of the following:

    example_1s_orbital()              # Simple 1s orbital
    # example_2p_orbitals()           # 2px, 2py, 2pz orbitals
    # example_bloch_state()           # Single superposition state
    # example_all_cardinal_states()   # All 6 Bloch sphere cardinal states
    # example_all_orbitals()          # Multiple orbital types
    # example_state_analysis()        # Custom state with analysis

    print("\n" + "=" * 70)
    print("DONE!")
    print("=" * 70)
    print("\nTips:")
    print("  - Press Z to change viewport shading (Wireframe/Solid/Material/Rendered)")
    print("  - Press Numpad 7 for top view, Numpad 1 for front view")
    print("  - Press Numpad . to center view on selected object")
    print("  - Use mouse wheel to zoom, middle-click-drag to rotate view")
    print("  - Edit this script to create different orbitals or states")
