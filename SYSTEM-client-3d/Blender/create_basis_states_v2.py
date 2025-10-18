"""
Create |0⟩ and |1⟩ States - SYSTEM Game Color Scheme

Based on your game's actual conventions:
- Crystal colors: RED (+X axis), GREEN (+Y axis), BLUE (+Z axis)
- |0⟩ state: North pole (+Y), GREEN (ground state)
- |1⟩ state: South pole (-Y), YELLOW/ORANGE (excited, complementary to green)

Since you don't have different colors for +/- yet, we use:
- GREEN for |0⟩ (matches +Y axis ground state)
- YELLOW for |1⟩ (excited state, visually distinct from green)
"""

import bpy
import sys
from pathlib import Path
import numpy as np

# Setup path (same as before)
def setup_path():
    script_dir = None
    try:
        for text in bpy.data.texts:
            if "create_basis_states" in text.name and text.filepath:
                script_dir = Path(text.filepath).parent / "Scripts"
                break
    except:
        pass
    if script_dir is None:
        try:
            script_dir = Path(__file__).parent / "Scripts"
        except:
            pass
    if script_dir is None or not script_dir.exists():
        script_dir = Path(r"H:\SpaceTime\SYSTEM\SYSTEM-client-3d\Blender\Scripts")
    if script_dir.exists() and str(script_dir) not in sys.path:
        sys.path.insert(0, str(script_dir))
    return script_dir

def create_basis_states():
    """Create |0⟩ and |1⟩ with game-accurate colors."""

    print("\n" + "=" * 70)
    print("SYSTEM Quantum Basis States - Game Colors")
    print("=" * 70)

    from Generators.hydrogen_orbital_meshes import clear_orbital_objects, create_blender_mesh
    from Quantum.hydrogen_wavefunctions import calculate_density_grid
    from skimage import measure

    clear_orbital_objects()

    # ========================================================================
    # |0⟩ State - GREEN (matches +Y axis / ground state)
    # ========================================================================

    print("\n|0⟩ State - North Pole (+Y axis)")
    print("  Color: GREEN (like your +Y axis)")
    print("  Crystal: Computational basis (Y-axis)")

    x0, y0, z0, d0 = calculate_density_grid(n=1, l=0, m=0, extent=15, resolution=80)
    d0_norm = d0 / np.max(d0)

    # GREEN - bright and vibrant like ground state energy
    iso_0 = [0.05, 0.15, 0.30]
    colors_0 = [
        (0.4, 1.0, 0.4, 0.4),   # Bright green outer
        (0.2, 0.85, 0.2, 0.6),  # Green middle
        (0.1, 0.7, 0.1, 0.8),   # Deep green inner
    ]

    objs_0 = []
    for i, (iso, col) in enumerate(zip(iso_0, colors_0)):
        try:
            v, f, _, _ = measure.marching_cubes(
                d0_norm, level=iso,
                spacing=(x0[1,0,0]-x0[0,0,0], y0[0,1,0]-y0[0,0,0], z0[0,0,1]-z0[0,0,0])
            )
            v[:, 0] += x0[0,0,0]
            v[:, 1] += y0[0,0,0]
            v[:, 2] += z0[0,0,0]

            obj = create_blender_mesh(f"ket0_green_{i}", v, f, col)
            obj.location.y = +30  # North/Up
            objs_0.append(obj)
            print(f"  Created shell {i+1}: {len(v)} verts")
        except:
            pass

    # ========================================================================
    # |1⟩ State - YELLOW/ORANGE (excited state, complementary to green)
    # ========================================================================

    print("\n|1⟩ State - South Pole (-Y axis)")
    print("  Color: YELLOW (excited state, distinct from green)")
    print("  Note: You don't have -Y crystal yet, using yellow for contrast")

    x1, y1, z1, d1 = calculate_density_grid(n=2, l=0, m=0, extent=40, resolution=80)
    d1_norm = d1 / np.max(d1)

    # YELLOW/GOLD - warm excited state color
    iso_1 = [0.02, 0.05, 0.15]
    colors_1 = [
        (1.0, 0.9, 0.3, 0.3),   # Pale yellow outer
        (1.0, 0.8, 0.2, 0.5),   # Golden middle
        (0.9, 0.7, 0.1, 0.7),   # Deep gold inner
    ]

    objs_1 = []
    for i, (iso, col) in enumerate(zip(iso_1, colors_1)):
        try:
            v, f, _, _ = measure.marching_cubes(
                d1_norm, level=iso,
                spacing=(x1[1,0,0]-x1[0,0,0], y1[0,1,0]-y1[0,0,0], z1[0,0,1]-z1[0,0,0])
            )
            v[:, 0] += x1[0,0,0]
            v[:, 1] += y1[0,0,0]
            v[:, 2] += z1[0,0,0]

            obj = create_blender_mesh(f"ket1_yellow_{i}", v, f, col)
            obj.location.y = -30  # South/Down
            objs_1.append(obj)
            print(f"  Created shell {i+1}: {len(v)} verts")
        except:
            pass

    # ========================================================================
    # Labels
    # ========================================================================

    # |0⟩ label - GREEN
    bpy.ops.object.text_add(location=(0, 25, -15))
    txt0 = bpy.context.active_object
    txt0.name = "Label_ket0"
    txt0.data.body = "|0⟩\n+Y axis\nGREEN\n(ground)"
    txt0.data.align_x = 'CENTER'
    txt0.data.size = 3

    mat0 = bpy.data.materials.new("Mat_ket0")
    mat0.use_nodes = True
    bsdf0 = mat0.node_tree.nodes["Principled BSDF"]
    bsdf0.inputs['Base Color'].default_value = (0.3, 1.0, 0.3, 1.0)
    bsdf0.inputs['Emission'].default_value = (0.3, 1.0, 0.3, 1.0)
    bsdf0.inputs['Emission Strength'].default_value = 2.0
    txt0.data.materials.append(mat0)

    # |1⟩ label - YELLOW
    bpy.ops.object.text_add(location=(0, -25, -15))
    txt1 = bpy.context.active_object
    txt1.name = "Label_ket1"
    txt1.data.body = "|1⟩\n-Y axis\nYELLOW\n(excited)"
    txt1.data.align_x = 'CENTER'
    txt1.data.size = 3

    mat1 = bpy.data.materials.new("Mat_ket1")
    mat1.use_nodes = True
    bsdf1 = mat1.node_tree.nodes["Principled BSDF"]
    bsdf1.inputs['Base Color'].default_value = (1.0, 0.85, 0.2, 1.0)
    bsdf1.inputs['Emission'].default_value = (1.0, 0.85, 0.2, 1.0)
    bsdf1.inputs['Emission Strength'].default_value = 2.0
    txt1.data.materials.append(mat1)

    # Y-axis reference line
    curve = bpy.data.curves.new('Y_Axis', 'CURVE')
    curve.dimensions = '3D'
    spline = curve.splines.new('POLY')
    spline.points.add(1)
    spline.points[0].co = (0, -40, 0, 1)
    spline.points[1].co = (0, +40, 0, 1)
    curve.bevel_depth = 0.2

    axis_obj = bpy.data.objects.new('Y_Axis', curve)
    bpy.context.collection.objects.link(axis_obj)

    mat_axis = bpy.data.materials.new("Mat_axis")
    mat_axis.use_nodes = True
    bsdf_axis = mat_axis.node_tree.nodes["Principled BSDF"]
    bsdf_axis.inputs['Base Color'].default_value = (0.5, 0.5, 0.5, 1.0)
    curve.materials.append(mat_axis)

    # ========================================================================
    # Summary
    # ========================================================================

    print("\n" + "=" * 70)
    print("SUCCESS! Created Basis States")
    print("=" * 70)
    print("\nGame-Accurate Colors:")
    print("  |0⟩ (UP, +Y):    GREEN - Ground state (matches your +Y/Green crystal)")
    print("  |1⟩ (DOWN, -Y):  YELLOW - Excited state (complementary, high contrast)")
    print("\nCrystal Mapping (for reference):")
    print("  +X axis (|+⟩):   RED crystal")
    print("  +Y axis (|0⟩):   GREEN (ground state)")
    print("  +Z axis (|+i⟩):  BLUE crystal")
    print("\nVisual Differences:")
    print("  1. Position: Up vs Down (Y-axis)")
    print("  2. Color: Green vs Yellow (complementary)")
    print("  3. Size: Small vs Large (3x difference)")
    print("  4. Structure: Smooth vs Shells (node visible)")
    print("\nPress Z → Material Preview to see colors!")
    print("=" * 70)

    return objs_0, objs_1

if __name__ == "__main__":
    setup_path()
    try:
        create_basis_states()
    except Exception as e:
        print(f"ERROR: {e}")
        import traceback
        traceback.print_exc()
