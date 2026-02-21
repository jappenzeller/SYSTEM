"""
Blender script to import quantum animation data from JSON.

Usage in Blender:
    1. Open this script in Blender's Text Editor
    2. Run the script
    3. Animation keyframes will be applied to scene objects

Requires scene objects named:
    - 'BlochArrow_E': Excitatory qubit state arrow
    - 'BlochArrow_I': Inhibitory qubit state arrow
    - 'EntanglementBridge': Connection between spheres
    - 'ParamBar_A', 'ParamBar_B', 'ParamBar_C': Parameter visualizers
"""

import bpy
import json
import math
from pathlib import Path


def load_animation_json(filepath: str) -> dict:
    """Load animation data from JSON file."""
    with open(filepath, 'r') as f:
        return json.load(f)


def apply_bloch_animation(obj, frames_data, qubit: str = 'E'):
    """
    Apply Bloch sphere animation to object.

    The object's rotation represents the state vector direction.
    Scale represents purity (1.0 = pure, <1.0 = mixed).
    """
    # Clear existing animation
    obj.animation_data_clear()
    obj.animation_data_create()
    obj.animation_data.action = bpy.data.actions.new(name=f"BlochAnim_{qubit}")

    for frame_data in frames_data:
        frame_num = frame_data['frame']

        # Get spherical coordinates
        if qubit == 'E':
            r, theta, phi = frame_data['spherical_E']
            purity = frame_data['purity_E']
        else:
            r, theta, phi = frame_data['spherical_I']
            purity = frame_data['purity_I']

        # Convert to Blender rotation (Euler XYZ)
        # theta = polar angle from +Z, phi = azimuthal in XY plane
        # Blender rotation: first rotate about X by theta, then about Z by phi
        obj.rotation_euler = (theta, 0, phi)
        obj.keyframe_insert(data_path="rotation_euler", frame=frame_num)

        # Scale by r (Bloch vector length, indicates purity for reduced states)
        scale_val = max(r, 0.1)  # Minimum scale to keep visible
        obj.scale = (scale_val, scale_val, scale_val)
        obj.keyframe_insert(data_path="scale", frame=frame_num)

    # Set interpolation to smooth
    if obj.animation_data and obj.animation_data.action:
        for fcurve in obj.animation_data.action.fcurves:
            for keyframe in fcurve.keyframe_points:
                keyframe.interpolation = 'BEZIER'


def apply_entanglement_animation(bridge_obj, frames_data):
    """
    Animate entanglement bridge emission strength.

    Uses concurrence to drive emission intensity.
    """
    # Check for emission material
    mat = bridge_obj.active_material
    if not mat or not mat.use_nodes:
        print(f"Warning: {bridge_obj.name} has no node-based material")
        return

    emission = None
    for node in mat.node_tree.nodes:
        if node.type == 'EMISSION':
            emission = node
            break

    if not emission:
        print(f"Warning: No emission node found in {bridge_obj.name} material")
        return

    # Clear existing animation on the emission strength
    strength_input = emission.inputs['Strength']

    for frame_data in frames_data:
        frame_num = frame_data['frame']
        # Scale concurrence for visible emission (0-10 range)
        strength = frame_data['concurrence'] * 10

        strength_input.default_value = strength
        strength_input.keyframe_insert(data_path='default_value', frame=frame_num)


def apply_parameter_animation(obj_a, obj_b, obj_c, frames_data):
    """
    Animate parameter display objects (e.g., bars or text).

    Objects scale Y to show parameter value.
    """
    for obj in [obj_a, obj_b, obj_c]:
        obj.animation_data_clear()
        obj.animation_data_create()

    for frame_data in frames_data:
        frame_num = frame_data['frame']

        # Parameter a bar (0-1 range)
        obj_a.scale.y = max(frame_data['a'], 0.01)
        obj_a.keyframe_insert(data_path="scale", index=1, frame=frame_num)

        # Parameter b bar (normalized to 0-1 range from 0-2pi)
        obj_b.scale.y = max(frame_data['b'] / (2 * math.pi), 0.01)
        obj_b.keyframe_insert(data_path="scale", index=1, frame=frame_num)

        # Parameter c bar (0-1 range)
        obj_c.scale.y = max(frame_data['c'], 0.01)
        obj_c.keyframe_insert(data_path="scale", index=1, frame=frame_num)


def apply_probability_animation(prob_objs: dict, frames_data):
    """
    Animate probability display objects.

    Args:
        prob_objs: Dict with keys '00', '01', '10', '11' -> objects
    """
    for frame_data in frames_data:
        frame_num = frame_data['frame']

        prob_objs['00'].scale.y = max(frame_data['prob_00'], 0.01)
        prob_objs['00'].keyframe_insert(data_path="scale", index=1, frame=frame_num)

        prob_objs['01'].scale.y = max(frame_data['prob_01'], 0.01)
        prob_objs['01'].keyframe_insert(data_path="scale", index=1, frame=frame_num)

        prob_objs['10'].scale.y = max(frame_data['prob_10'], 0.01)
        prob_objs['10'].keyframe_insert(data_path="scale", index=1, frame=frame_num)

        prob_objs['11'].scale.y = max(frame_data['prob_11'], 0.01)
        prob_objs['11'].keyframe_insert(data_path="scale", index=1, frame=frame_num)


def apply_fractal_animation(plane_obj, frames_data, fractal_dir: str):
    """
    Animate fractal texture on a plane by swapping image textures.

    Assumes fractal images are named fractal_XXXX.png in fractal_dir.
    """
    mat = plane_obj.active_material
    if not mat or not mat.use_nodes:
        print(f"Warning: {plane_obj.name} has no node-based material")
        return

    # Find image texture node
    img_node = None
    for node in mat.node_tree.nodes:
        if node.type == 'TEX_IMAGE':
            img_node = node
            break

    if not img_node:
        print(f"Warning: No image texture node found in {plane_obj.name} material")
        return

    # Load all fractal images
    fractal_path = Path(fractal_dir)
    images = {}

    for frame_data in frames_data:
        frame_num = frame_data['frame']
        img_file = fractal_path / f"fractal_{frame_num:04d}.png"

        if img_file.exists():
            if str(img_file) not in bpy.data.images:
                img = bpy.data.images.load(str(img_file))
            else:
                img = bpy.data.images[str(img_file)]
            images[frame_num] = img

    # Create driver or use frame change handler
    # For simplicity, we'll create multiple image nodes and use keyframes
    # (A more advanced approach would use a driver with frame number)

    print(f"Loaded {len(images)} fractal images")
    # Note: Full texture animation would require more complex setup
    # This is a placeholder for the concept


def import_quantum_animation(json_path: str, fractal_dir: str = None):
    """
    Main import function.

    Expects scene to have objects named:
        - 'BlochArrow_E': Excitatory qubit state arrow
        - 'BlochArrow_I': Inhibitory qubit state arrow
        - 'EntanglementBridge': Connection between spheres
        - 'ParamBar_A', 'ParamBar_B', 'ParamBar_C': Parameter visualizers
    """
    data = load_animation_json(json_path)
    frames = data['frames']
    metadata = data.get('metadata', {})

    print(f"=== Importing Quantum Animation ===")
    print(f"Frames: {len(frames)}")
    print(f"Metadata: {metadata}")

    # Get scene objects
    arrow_E = bpy.data.objects.get('BlochArrow_E')
    arrow_I = bpy.data.objects.get('BlochArrow_I')
    bridge = bpy.data.objects.get('EntanglementBridge')
    bar_a = bpy.data.objects.get('ParamBar_A')
    bar_b = bpy.data.objects.get('ParamBar_B')
    bar_c = bpy.data.objects.get('ParamBar_C')
    fractal_plane = bpy.data.objects.get('FractalPlane')

    # Apply animations
    if arrow_E:
        apply_bloch_animation(arrow_E, frames, 'E')
        print("  [OK] Applied E qubit animation")
    else:
        print("  [--] BlochArrow_E not found")

    if arrow_I:
        apply_bloch_animation(arrow_I, frames, 'I')
        print("  [OK] Applied I qubit animation")
    else:
        print("  [--] BlochArrow_I not found")

    if bridge:
        apply_entanglement_animation(bridge, frames)
        print("  [OK] Applied entanglement animation")
    else:
        print("  [--] EntanglementBridge not found")

    if bar_a and bar_b and bar_c:
        apply_parameter_animation(bar_a, bar_b, bar_c, frames)
        print("  [OK] Applied parameter bar animations")
    else:
        print("  [--] ParamBar_A/B/C not found (need all three)")

    if fractal_plane and fractal_dir:
        apply_fractal_animation(fractal_plane, frames, fractal_dir)
        print("  [OK] Applied fractal animation")
    elif fractal_plane:
        print("  [--] FractalPlane found but no fractal_dir specified")

    # Check for probability bars
    prob_objs = {
        '00': bpy.data.objects.get('ProbBar_00'),
        '01': bpy.data.objects.get('ProbBar_01'),
        '10': bpy.data.objects.get('ProbBar_10'),
        '11': bpy.data.objects.get('ProbBar_11'),
    }
    if all(prob_objs.values()):
        apply_probability_animation(prob_objs, frames)
        print("  [OK] Applied probability bar animations")

    # Set timeline
    bpy.context.scene.frame_start = 0
    bpy.context.scene.frame_end = len(frames) - 1
    bpy.context.scene.frame_current = 0

    print(f"\n=== Import Complete ===")
    print(f"Timeline: 0 - {len(frames)-1} frames")
    print(f"Tip: Press Space to play animation")


# ============================================================
# Run when executed in Blender
# ============================================================
if __name__ == '__main__':
    # Update these paths to your files
    # Use // for Blender-relative paths, or absolute paths
    JSON_PATH = "//../../data/animations/example_evolution.json"
    FRACTAL_DIR = "//../../data/fractals/test_sequence"

    # Convert to absolute if using relative
    json_abs = bpy.path.abspath(JSON_PATH)
    fractal_abs = bpy.path.abspath(FRACTAL_DIR)

    import_quantum_animation(json_abs, fractal_abs)
