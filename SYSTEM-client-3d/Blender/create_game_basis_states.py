"""
Create |0⟩ and |1⟩ States Matching SYSTEM Game Convention

This script creates quantum basis states that match the SYSTEM game's visual style:
- |0⟩ state: North pole (+Y), GREEN (1s orbital) - SMALL, COMPACT
- |1⟩ state: South pole (-Y), RED (2s orbital) - LARGE, DIFFUSE

These match the Bloch sphere coordinate system:
- +Y axis = |0⟩ = North pole = GREEN
- -Y axis = |1⟩ = South pole = RED

Run this in Blender to create game-accurate visualizations.
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
            if "create_game_basis_states" in text.name and text.filepath:
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

def create_game_basis_states():
    """Create |0⟩ and |1⟩ states matching SYSTEM game conventions."""

    print("\n" + "=" * 70)
    print("Creating SYSTEM Game Basis States")
    print("|0⟩ (North/+Y/GREEN) and |1⟩ (South/-Y/RED)")
    print("=" * 70)

    # Import after path setup
    from Generators.hydrogen_orbital_meshes import clear_orbital_objects, create_blender_mesh
    from Quantum.hydrogen_wavefunctions import calculate_density_grid
    from skimage import measure

    # Clear existing orbitals
    clear_orbital_objects()
    print("\nCleared existing orbital objects")

    # ========================================================================
    # Create |0⟩ State - NORTH POLE - GREEN - 1s Orbital
    # ========================================================================

    print("\n" + "-" * 70)
    print("Creating |0⟩ State (North Pole, +Y axis)")
    print("-" * 70)
    print("  Bloch sphere: θ=0, North pole")
    print("  Orbital: 1s (ground state)")
    print("  Color: GREEN (matches game)")
    print("  Size: SMALL (compact electron cloud)")

    # Calculate 1s orbital density
    print("\n  Calculating 1s wave function...")
    x_grid_0, y_grid_0, z_grid_0, density_0 = calculate_density_grid(
        n=1, l=0, m=0,
        extent=15,      # Compact extent
        resolution=80   # Good detail
    )

    # Normalize density
    max_density_0 = np.max(density_0)
    density_0_norm = density_0 / max_density_0

    print(f"  Max density: {max_density_0:.6f}")

    # GREEN color scheme (like your game's north pole marker)
    # Multiple shells for depth and better visualization
    iso_levels_0 = [0.05, 0.15, 0.30]
    colors_0 = [
        (0.2, 1.0, 0.3, 0.4),   # Light green (outer)
        (0.1, 0.9, 0.2, 0.6),   # Medium green (middle)
        (0.0, 0.7, 0.1, 0.8),   # Deep green (inner, more opaque)
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
                name=f"ket0_green_shell{i}",
                vertices=verts,
                faces=faces,
                color=color
            )

            # Position: ABOVE (North pole, +Y direction)
            obj.location.y = +30  # 30 units UP (+Y)

            objects_0.append(obj)
            print(f"    Created with {len(verts)} vertices")

        except ValueError as e:
            print(f"    Skipped (no surface at this level)")

    print(f"\n  ✓ Created |0⟩ state with {len(objects_0)} shells")
    print(f"    Color: GREEN")
    print(f"    Position: NORTH/UP (0, +30, 0)")
    print(f"    Bloch: θ=0 (north pole)")
    print(f"    Size: SMALL (compact 1s orbital)")

    # ========================================================================
    # Create |1⟩ State - SOUTH POLE - RED - 2s Orbital
    # ========================================================================

    print("\n" + "-" * 70)
    print("Creating |1⟩ State (South Pole, -Y axis)")
    print("-" * 70)
    print("  Bloch sphere: θ=π, South pole")
    print("  Orbital: 2s (first excited state)")
    print("  Color: RED (matches game)")
    print("  Size: LARGE (diffuse with radial node)")

    # Calculate 2s orbital density
    print("\n  Calculating 2s wave function...")
    x_grid_1, y_grid_1, z_grid_1, density_1 = calculate_density_grid(
        n=2, l=0, m=0,
        extent=40,      # Larger extent (excited state)
        resolution=80   # Good detail
    )

    # Normalize density
    max_density_1 = np.max(density_1)
    density_1_norm = density_1 / max_density_1

    print(f"  Max density: {max_density_1:.6f}")

    # RED color scheme (like your game's south pole / red crystal)
    # Lower iso values to show the outer shell and node structure
    iso_levels_1 = [0.02, 0.05, 0.15]
    colors_1 = [
        (1.0, 0.3, 0.2, 0.3),   # Light red (outer shell)
        (0.9, 0.2, 0.1, 0.5),   # Medium red (middle)
        (0.8, 0.1, 0.0, 0.7),   # Deep red (inner, near node)
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
                name=f"ket1_red_shell{i}",
                vertices=verts,
                faces=faces,
                color=color
            )

            # Position: BELOW (South pole, -Y direction)
            obj.location.y = -30  # 30 units DOWN (-Y)

            objects_1.append(obj)
            print(f"    Created with {len(verts)} vertices")

        except ValueError as e:
            print(f"    Skipped (no surface at this level)")

    print(f"\n  ✓ Created |1⟩ state with {len(objects_1)} shells")
    print(f"    Color: RED")
    print(f"    Position: SOUTH/DOWN (0, -30, 0)")
    print(f"    Bloch: θ=π (south pole)")
    print(f"    Size: LARGE (diffuse 2s orbital with radial node)")

    # ========================================================================
    # Add Text Labels
    # ========================================================================

    print("\n" + "-" * 70)
    print("Adding text labels")
    print("-" * 70)

    # Add |0⟩ label (GREEN, NORTH)
    bpy.ops.object.text_add(location=(0, 25, -15))
    text_0 = bpy.context.active_object
    text_0.name = "Label_ket0_north"
    text_0.data.body = "|0⟩\nNorth Pole (+Y)\n1s orbital\nGREEN"
    text_0.data.align_x = 'CENTER'
    text_0.data.size = 3

    # Green material for text
    mat_0 = bpy.data.materials.new(name="Label0_material")
    mat_0.use_nodes = True
    bsdf_0 = mat_0.node_tree.nodes.get("Principled BSDF")
    if bsdf_0:
        bsdf_0.inputs['Base Color'].default_value = (0.2, 1.0, 0.3, 1.0)
        bsdf_0.inputs['Emission'].default_value = (0.2, 1.0, 0.3, 1.0)
        bsdf_0.inputs['Emission Strength'].default_value = 2.0
    text_0.data.materials.append(mat_0)

    print("  ✓ Added |0⟩ label (green, north)")

    # Add |1⟩ label (RED, SOUTH)
    bpy.ops.object.text_add(location=(0, -25, -15))
    text_1 = bpy.context.active_object
    text_1.name = "Label_ket1_south"
    text_1.data.body = "|1⟩\nSouth Pole (-Y)\n2s orbital\nRED"
    text_1.data.align_x = 'CENTER'
    text_1.data.size = 3

    # Red material for text
    mat_1 = bpy.data.materials.new(name="Label1_material")
    mat_1.use_nodes = True
    bsdf_1 = mat_1.node_tree.nodes.get("Principled BSDF")
    if bsdf_1:
        bsdf_1.inputs['Base Color'].default_value = (1.0, 0.3, 0.2, 1.0)
        bsdf_1.inputs['Emission'].default_value = (1.0, 0.3, 0.2, 1.0)
        bsdf_1.inputs['Emission Strength'].default_value = 2.0
    text_1.data.materials.append(mat_1)

    print("  ✓ Added |1⟩ label (red, south)")

    # ========================================================================
    # Add Y-Axis Arrow for Reference
    # ========================================================================

    print("\n" + "-" * 70)
    print("Adding Y-axis reference")
    print("-" * 70)

    # Create a simple line along Y axis
    curve_data = bpy.data.curves.new('Y_Axis', type='CURVE')
    curve_data.dimensions = '3D'
    curve_data.resolution_u = 2

    # Create a polyline
    polyline = curve_data.splines.new('POLY')
    polyline.points.add(1)  # We need 2 points total (one already exists)
    polyline.points[0].co = (0, -40, 0, 1)  # Bottom
    polyline.points[1].co = (0, +40, 0, 1)  # Top

    # Create object
    curve_obj = bpy.data.objects.new('Y_Axis_Line', curve_data)
    bpy.context.collection.objects.link(curve_obj)

    # Material for axis line
    mat_axis = bpy.data.materials.new(name="Axis_material")
    mat_axis.use_nodes = True
    bsdf_axis = mat_axis.node_tree.nodes.get("Principled BSDF")
    if bsdf_axis:
        bsdf_axis.inputs['Base Color'].default_value = (0.5, 0.5, 0.5, 1.0)
        bsdf_axis.inputs['Emission'].default_value = (0.5, 0.5, 0.5, 1.0)
        bsdf_axis.inputs['Emission Strength'].default_value = 1.0
    curve_data.materials.append(mat_axis)

    # Set bevel for visible line
    curve_data.bevel_depth = 0.2

    print("  ✓ Added Y-axis reference line")

    # ========================================================================
    # Setup Camera View
    # ========================================================================

    print("\n" + "-" * 70)
    print("Setting up camera view")
    print("-" * 70)

    # Select all orbital objects for framing
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
    print("SUCCESS! SYSTEM Game Basis States Created")
    print("=" * 70)

    print("\nBloch Sphere Mapping:")
    print("  |0⟩ State (NORTH, +Y):")
    print("    • Position: (0, +30, 0) - UP")
    print("    • Color: GREEN")
    print("    • Bloch: θ=0 (north pole)")
    print("    • Size: SMALL (~15 Bohr radii)")
    print("    • Orbital: 1s (ground state, n=1)")
    print("    • Structure: Single compact sphere (no nodes)")
    print("")
    print("  |1⟩ State (SOUTH, -Y):")
    print("    • Position: (0, -30, 0) - DOWN")
    print("    • Color: RED")
    print("    • Bloch: θ=π (south pole)")
    print("    • Size: LARGE (~40 Bohr radii)")
    print("    • Orbital: 2s (first excited state, n=2)")
    print("    • Structure: Outer shell + inner core (radial node)")

    print("\nGame Integration:")
    print("  • Matches SYSTEM Bloch sphere convention")
    print("  • +Y axis = |0⟩ = North pole = GREEN")
    print("  • -Y axis = |1⟩ = South pole = RED")
    print("  • Vertical arrangement shows energy levels")
    print("  • Size difference shows excitation")

    print("\nVisual Differences at a Glance:")
    print("  1. POSITION: Up (|0⟩) vs Down (|1⟩) - matches Bloch sphere!")
    print("  2. COLOR: Green (|0⟩) vs Red (|1⟩) - matches game colors!")
    print("  3. SIZE: Small (|0⟩) vs Large (|1⟩) - ~3x difference!")
    print("  4. STRUCTURE: Smooth (|0⟩) vs Shells (|1⟩) - node visible!")

    print("\nViewing Tips:")
    print("  • Press Z → Material Preview or Rendered to see colors")
    print("  • Press Numpad 7 for top view (look down Y axis)")
    print("  • Press Numpad 1 for side view (see vertical arrangement)")
    print("  • Scroll to zoom - note the dramatic size difference!")
    print("  • Y-axis line shows Bloch sphere vertical axis")

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
        objects_0, objects_1 = create_game_basis_states()
        print("Done! Check the 3D viewport.")
        print("\nThese states now match your SYSTEM game's Bloch sphere:")
        print("  - GREEN |0⟩ at north pole (+Y)")
        print("  - RED |1⟩ at south pole (-Y)")
    except Exception as e:
        print(f"\nERROR: {e}")
        import traceback
        traceback.print_exc()
