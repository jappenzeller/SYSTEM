"""
Create Visually Distinct |0⟩ and |1⟩ States for Blender

This script creates two quantum basis states with clear visual differences:
- |0⟩ state: Small, compact (1s orbital) - BLUE
- |1⟩ state: Large, diffuse (2s orbital) - RED with node structure

Run this in Blender to see the quantum basis states side by side.
"""

import bpy
import sys
from pathlib import Path
import numpy as np

# ============================================================================
# Setup Path
# ============================================================================

def setup_path():
    """Add Scripts directory to Python path."""
    script_dir = None

    # Method 1: From text block
    try:
        for text in bpy.data.texts:
            if "create_zero_one_states" in text.name and text.filepath:
                script_dir = Path(text.filepath).parent / "Scripts"
                break
    except:
        pass

    # Method 2: Relative to __file__
    if script_dir is None:
        try:
            script_dir = Path(__file__).parent / "Scripts"
        except:
            pass

    # Method 3: Hardcoded
    if script_dir is None or not script_dir.exists():
        script_dir = Path(r"H:\SpaceTime\SYSTEM\SYSTEM-client-3d\Blender\Scripts")

    if script_dir.exists() and str(script_dir) not in sys.path:
        sys.path.insert(0, str(script_dir))
        print(f"Added to path: {script_dir}")

    return script_dir

# ============================================================================
# Main Creation Function
# ============================================================================

def create_zero_one_states():
    """Create visually distinct |0⟩ and |1⟩ states."""

    print("\n" + "=" * 70)
    print("Creating |0⟩ and |1⟩ Quantum Basis States")
    print("=" * 70)

    # Import after path setup
    from Generators.hydrogen_orbital_meshes import clear_orbital_objects, create_blender_mesh
    from Quantum.hydrogen_wavefunctions import calculate_density_grid
    from skimage import measure

    # Clear existing orbitals
    clear_orbital_objects()
    print("\nCleared existing orbital objects")

    # ========================================================================
    # Create |0⟩ State - 1s Orbital (BLUE, SMALL, COMPACT)
    # ========================================================================

    print("\n" + "-" * 70)
    print("Creating |0⟩ State (1s orbital)")
    print("-" * 70)

    # Calculate 1s orbital density
    print("  Calculating 1s wave function...")
    x_grid_0, y_grid_0, z_grid_0, density_0 = calculate_density_grid(
        n=1, l=0, m=0,
        extent=15,      # Smaller extent (compact)
        resolution=80   # Good detail
    )

    # Normalize density
    max_density_0 = np.max(density_0)
    density_0_norm = density_0 / max_density_0

    print(f"  Max density: {max_density_0:.6f}")

    # Create isosurfaces for |0⟩
    iso_levels_0 = [0.05, 0.15, 0.30]  # Multiple shells for depth
    colors_0 = [
        (0.2, 0.4, 1.0, 0.4),   # Light blue (outer)
        (0.1, 0.3, 0.9, 0.6),   # Medium blue (middle)
        (0.0, 0.2, 0.8, 0.8),   # Deep blue (inner, more opaque)
    ]

    objects_0 = []
    for i, (iso_val, color) in enumerate(zip(iso_levels_0, colors_0)):
        print(f"  Creating isosurface {i+1}/{len(iso_levels_0)} at {iso_val*100:.0f}%...")

        try:
            verts, faces, normals, values = measure.marching_cubes(
                density_0_norm,
                level=iso_val,
                spacing=(
                    x_grid_0[1, 0, 0] - x_grid_0[0, 0, 0],
                    y_grid_0[0, 1, 0] - y_grid_0[0, 0, 0],
                    z_grid_0[0, 0, 1] - z_grid_0[0, 0, 0],
                )
            )

            # Offset vertices
            verts[:, 0] += x_grid_0[0, 0, 0]
            verts[:, 1] += y_grid_0[0, 0, 0]
            verts[:, 2] += z_grid_0[0, 0, 0]

            # Create mesh in Blender
            obj = create_blender_mesh(
                name=f"ket0_shell{i}",
                vertices=verts,
                faces=faces,
                color=color
            )

            # Position: Left side
            obj.location.x = -30  # 30 units to the left

            objects_0.append(obj)
            print(f"    Created with {len(verts)} vertices")

        except ValueError as e:
            print(f"    Skipped (no surface at this level)")

    print(f"\n  ✓ Created |0⟩ state with {len(objects_0)} shells")
    print(f"    Color: BLUE")
    print(f"    Size: SMALL (compact 1s orbital)")
    print(f"    Position: LEFT (-30, 0, 0)")

    # ========================================================================
    # Create |1⟩ State - 2s Orbital (RED, LARGE, WITH NODE)
    # ========================================================================

    print("\n" + "-" * 70)
    print("Creating |1⟩ State (2s orbital)")
    print("-" * 70)

    # Calculate 2s orbital density
    print("  Calculating 2s wave function...")
    x_grid_1, y_grid_1, z_grid_1, density_1 = calculate_density_grid(
        n=2, l=0, m=0,
        extent=40,      # Larger extent (diffuse)
        resolution=80   # Good detail
    )

    # Normalize density
    max_density_1 = np.max(density_1)
    density_1_norm = density_1 / max_density_1

    print(f"  Max density: {max_density_1:.6f}")

    # Create isosurfaces for |1⟩ - emphasize the node structure
    iso_levels_1 = [0.02, 0.05, 0.15]  # Lower levels to show outer shell
    colors_1 = [
        (1.0, 0.3, 0.3, 0.3),   # Light red (outer shell)
        (0.9, 0.2, 0.2, 0.5),   # Medium red (middle)
        (0.8, 0.1, 0.1, 0.7),   # Deep red (inner, near node)
    ]

    objects_1 = []
    for i, (iso_val, color) in enumerate(zip(iso_levels_1, colors_1)):
        print(f"  Creating isosurface {i+1}/{len(iso_levels_1)} at {iso_val*100:.1f}%...")

        try:
            verts, faces, normals, values = measure.marching_cubes(
                density_1_norm,
                level=iso_val,
                spacing=(
                    x_grid_1[1, 0, 0] - x_grid_1[0, 0, 0],
                    y_grid_1[0, 1, 0] - y_grid_1[0, 0, 0],
                    z_grid_1[0, 0, 1] - z_grid_1[0, 0, 0],
                )
            )

            # Offset vertices
            verts[:, 0] += x_grid_1[0, 0, 0]
            verts[:, 1] += y_grid_1[0, 0, 0]
            verts[:, 2] += z_grid_1[0, 0, 0]

            # Create mesh in Blender
            obj = create_blender_mesh(
                name=f"ket1_shell{i}",
                vertices=verts,
                faces=faces,
                color=color
            )

            # Position: Right side
            obj.location.x = +30  # 30 units to the right

            objects_1.append(obj)
            print(f"    Created with {len(verts)} vertices")

        except ValueError as e:
            print(f"    Skipped (no surface at this level)")

    print(f"\n  ✓ Created |1⟩ state with {len(objects_1)} shells")
    print(f"    Color: RED")
    print(f"    Size: LARGE (diffuse 2s orbital with node)")
    print(f"    Position: RIGHT (+30, 0, 0)")

    # ========================================================================
    # Add Text Labels
    # ========================================================================

    print("\n" + "-" * 70)
    print("Adding text labels")
    print("-" * 70)

    # Add |0⟩ label
    bpy.ops.object.text_add(location=(-30, -25, 0))
    text_0 = bpy.context.active_object
    text_0.name = "Label_ket0"
    text_0.data.body = "|0⟩\n1s orbital\n(ground state)"
    text_0.data.align_x = 'CENTER'
    text_0.data.size = 3

    # Blue material for text
    mat_0 = bpy.data.materials.new(name="Label0_material")
    mat_0.use_nodes = True
    bsdf_0 = mat_0.node_tree.nodes.get("Principled BSDF")
    if bsdf_0:
        bsdf_0.inputs['Base Color'].default_value = (0.2, 0.4, 1.0, 1.0)
        bsdf_0.inputs['Emission'].default_value = (0.2, 0.4, 1.0, 1.0)
        bsdf_0.inputs['Emission Strength'].default_value = 2.0
    text_0.data.materials.append(mat_0)

    print("  ✓ Added |0⟩ label (blue)")

    # Add |1⟩ label
    bpy.ops.object.text_add(location=(+30, -25, 0))
    text_1 = bpy.context.active_object
    text_1.name = "Label_ket1"
    text_1.data.body = "|1⟩\n2s orbital\n(excited state)"
    text_1.data.align_x = 'CENTER'
    text_1.data.size = 3

    # Red material for text
    mat_1 = bpy.data.materials.new(name="Label1_material")
    mat_1.use_nodes = True
    bsdf_1 = mat_1.node_tree.nodes.get("Principled BSDF")
    if bsdf_1:
        bsdf_1.inputs['Base Color'].default_value = (1.0, 0.3, 0.3, 1.0)
        bsdf_1.inputs['Emission'].default_value = (1.0, 0.3, 0.3, 1.0)
        bsdf_1.inputs['Emission Strength'].default_value = 2.0
    text_1.data.materials.append(mat_1)

    print("  ✓ Added |1⟩ label (red)")

    # ========================================================================
    # Setup Camera View
    # ========================================================================

    print("\n" + "-" * 70)
    print("Setting up camera")
    print("-" * 70)

    # Select all objects for framing
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects_0 + objects_1:
        obj.select_set(True)

    if objects_0:
        bpy.context.view_layer.objects.active = objects_0[0]

    print("  ✓ Selected all orbital objects")

    # ========================================================================
    # Summary
    # ========================================================================

    print("\n" + "=" * 70)
    print("SUCCESS! Quantum Basis States Created")
    print("=" * 70)

    print("\nVisual Differences:")
    print("  |0⟩ State (LEFT):")
    print("    • Color: BLUE")
    print("    • Size: SMALL (~15 Bohr radii)")
    print("    • Structure: Single compact shell (no nodes)")
    print("    • Orbital: 1s (ground state)")
    print("")
    print("  |1⟩ State (RIGHT):")
    print("    • Color: RED")
    print("    • Size: LARGE (~40 Bohr radii)")
    print("    • Structure: Outer shell + inner core (radial node at ~2a₀)")
    print("    • Orbital: 2s (first excited state)")

    print("\nViewing Tips:")
    print("  • Press Z → Material Preview or Rendered to see colors")
    print("  • Press Numpad . to frame all objects")
    print("  • Rotate view to see the size difference")
    print("  • Note: |1⟩ is about 3x larger than |0⟩!")

    print("\nKey Differences at a Glance:")
    print("  1. SIZE: |1⟩ is much larger (excited state has more energy)")
    print("  2. COLOR: Blue (|0⟩) vs Red (|1⟩)")
    print("  3. STRUCTURE: |1⟩ shows radial node (shell structure)")
    print("  4. POSITION: Left (|0⟩) vs Right (|1⟩)")

    print("\n" + "=" * 70)
    print()

    return objects_0, objects_1

# ============================================================================
# Entry Point
# ============================================================================

if __name__ == "__main__":
    # Setup path
    setup_path()

    # Create the states
    try:
        objects_0, objects_1 = create_zero_one_states()
        print("Done! Check the 3D viewport.")
    except Exception as e:
        print(f"\nERROR: {e}")
        import traceback
        traceback.print_exc()
