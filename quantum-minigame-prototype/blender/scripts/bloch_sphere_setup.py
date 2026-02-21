"""
Create Bloch sphere visualization rig in Blender.

Creates:
- Wireframe sphere
- RGB axis indicators with labels
- State vector arrow
- Coordinate grid lines (equator, meridians)

Run this script in Blender to set up the scene, then use
import_quantum_data.py to animate it.
"""

import bpy
import math
from mathutils import Vector


def create_emissive_material(name: str, color: tuple, emission_strength: float = 0.5):
    """Create an emissive material."""
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links

    # Get or create principled BSDF
    bsdf = nodes.get("Principled BSDF")
    if not bsdf:
        bsdf = nodes.new(type='ShaderNodeBsdfPrincipled')

    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Emission Color"].default_value = color
    bsdf.inputs["Emission Strength"].default_value = emission_strength

    return mat


def create_bloch_sphere_rig(name: str = "BlochSphere",
                            radius: float = 1.0,
                            position: Vector = Vector((0, 0, 0))):
    """
    Create complete Bloch sphere visualization rig.

    Returns:
        dict: References to created objects
    """
    objects = {}

    # Create parent empty for organization
    parent = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(parent)
    parent.location = position
    objects['parent'] = parent

    # === Wireframe Sphere ===
    bpy.ops.mesh.primitive_uv_sphere_add(
        radius=radius,
        segments=32,
        ring_count=16,
        location=position
    )
    sphere = bpy.context.active_object
    sphere.name = f"{name}_Sphere"
    sphere.parent = parent

    # Wireframe material (transparent with emission for glow effect)
    mat_wire = bpy.data.materials.new(name=f"{name}_WireMat")
    mat_wire.use_nodes = True
    nodes = mat_wire.node_tree.nodes
    bsdf = nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (0.3, 0.3, 0.4, 1)
    bsdf.inputs["Alpha"].default_value = 0.15
    bsdf.inputs["Emission Color"].default_value = (0.2, 0.2, 0.3, 1)
    bsdf.inputs["Emission Strength"].default_value = 0.2
    mat_wire.blend_method = 'BLEND'
    mat_wire.shadow_method = 'NONE'
    sphere.data.materials.append(mat_wire)

    # Add wireframe modifier
    wire_mod = sphere.modifiers.new(name="Wireframe", type='WIREFRAME')
    wire_mod.thickness = 0.015
    wire_mod.use_replace = True
    objects['sphere'] = sphere

    # === Axis Arrows (RGB = XYZ) ===
    axis_config = {
        'X': {'color': (1, 0, 0, 1), 'rotation': (0, math.pi/2, 0)},
        'Y': {'color': (0, 1, 0, 1), 'rotation': (math.pi/2, 0, 0)},
        'Z': {'color': (0, 0, 1, 1), 'rotation': (0, 0, 0)},
    }

    for axis, config in axis_config.items():
        # Create cylinder for axis
        bpy.ops.mesh.primitive_cylinder_add(
            radius=0.02,
            depth=radius * 2.4,
            location=position
        )
        axis_obj = bpy.context.active_object
        axis_obj.name = f"{name}_Axis_{axis}"
        axis_obj.parent = parent
        axis_obj.rotation_euler = config['rotation']

        # Colored emissive material
        mat_axis = create_emissive_material(
            f"{name}_AxisMat_{axis}",
            config['color'],
            emission_strength=0.8
        )
        axis_obj.data.materials.append(mat_axis)
        objects[f'axis_{axis}'] = axis_obj

    # === State Vector Arrow ===
    # Arrow head (cone)
    bpy.ops.mesh.primitive_cone_add(
        radius1=0.1,
        radius2=0,
        depth=0.25,
        location=position + Vector((0, 0, radius * 0.88))
    )
    arrow_head = bpy.context.active_object
    arrow_head.name = f"{name}_ArrowHead_temp"

    # Arrow shaft (cylinder)
    bpy.ops.mesh.primitive_cylinder_add(
        radius=0.04,
        depth=radius * 0.75,
        location=position + Vector((0, 0, radius * 0.375))
    )
    arrow_shaft = bpy.context.active_object
    arrow_shaft.name = f"{name}_ArrowShaft_temp"

    # Join arrow parts
    bpy.ops.object.select_all(action='DESELECT')
    arrow_head.select_set(True)
    arrow_shaft.select_set(True)
    bpy.context.view_layer.objects.active = arrow_shaft
    bpy.ops.object.join()

    arrow = bpy.context.active_object
    arrow.name = f"{name}_Arrow"
    arrow.parent = parent

    # Set origin to bottom of arrow (for rotation from center)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.context.scene.cursor.location = position
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

    # Arrow material (bright gold with strong emission)
    mat_arrow = bpy.data.materials.new(name=f"{name}_ArrowMat")
    mat_arrow.use_nodes = True
    nodes = mat_arrow.node_tree.nodes
    bsdf = nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (1, 0.8, 0, 1)
    bsdf.inputs["Metallic"].default_value = 0.8
    bsdf.inputs["Roughness"].default_value = 0.2
    bsdf.inputs["Emission Color"].default_value = (1, 0.8, 0, 1)
    bsdf.inputs["Emission Strength"].default_value = 3.0
    arrow.data.materials.append(mat_arrow)

    objects['arrow'] = arrow

    # === State Labels (|0>, |1>, |+>, |->, |+i>, |-i>) ===
    labels = [
        ("|0>", Vector((0, 0, radius * 1.35)), (0, 0, 1, 1)),     # +Z (blue)
        ("|1>", Vector((0, 0, -radius * 1.35)), (0, 0, 1, 1)),    # -Z
        ("|+>", Vector((radius * 1.35, 0, 0)), (1, 0, 0, 1)),     # +X (red)
        ("|->", Vector((-radius * 1.35, 0, 0)), (1, 0, 0, 1)),    # -X
        ("|+i>", Vector((0, radius * 1.35, 0)), (0, 1, 0, 1)),    # +Y (green)
        ("|-i>", Vector((0, -radius * 1.35, 0)), (0, 1, 0, 1)),   # -Y
    ]

    for label_text, offset, color in labels:
        bpy.ops.object.text_add(location=position + offset)
        text_obj = bpy.context.active_object
        text_obj.data.body = label_text
        text_obj.data.align_x = 'CENTER'
        text_obj.data.align_y = 'CENTER'
        text_obj.data.size = 0.18
        text_obj.name = f"{name}_Label_{label_text.replace('|', '').replace('>', '')}"
        text_obj.parent = parent

        # Text material
        mat_text = create_emissive_material(f"{name}_TextMat_{label_text}", color, 1.0)
        text_obj.data.materials.append(mat_text)

        # Track to camera constraint for always facing
        if bpy.data.objects.get('Camera'):
            constraint = text_obj.constraints.new(type='TRACK_TO')
            constraint.target = bpy.data.objects.get('Camera')
            constraint.track_axis = 'TRACK_Z'
            constraint.up_axis = 'UP_Y'

    return objects


def create_entanglement_bridge(start: Vector, end: Vector, name: str = "EntanglementBridge"):
    """Create glowing bridge between two Bloch spheres."""
    # Calculate midpoint and length
    midpoint = (start + end) / 2
    direction = end - start
    length = direction.length

    # Create cylinder
    bpy.ops.mesh.primitive_cylinder_add(
        radius=0.06,
        depth=length,
        location=midpoint
    )
    bridge = bpy.context.active_object
    bridge.name = name

    # Rotate to align with direction
    bridge.rotation_euler = direction.to_track_quat('Z', 'Y').to_euler()

    # Emission material (purple glow)
    mat_bridge = bpy.data.materials.new(name="BridgeMat")
    mat_bridge.use_nodes = True
    nodes = mat_bridge.node_tree.nodes
    links = mat_bridge.node_tree.links

    # Remove default BSDF and add emission
    nodes.remove(nodes["Principled BSDF"])

    emission = nodes.new(type='ShaderNodeEmission')
    emission.inputs['Color'].default_value = (0.6, 0.2, 1, 1)  # Purple
    emission.inputs['Strength'].default_value = 2.0

    output = nodes["Material Output"]
    links.new(emission.outputs['Emission'], output.inputs['Surface'])

    bridge.data.materials.append(mat_bridge)

    return bridge


def create_parameter_bars(position: Vector = Vector((0, -2.5, 0))):
    """Create parameter visualization bars."""
    bars = {}

    bar_config = [
        ("ParamBar_A", Vector((-1.5, 0, 0)), (1, 0.35, 0.35, 1), "a (E)"),    # Red-orange
        ("ParamBar_B", Vector((0, 0, 0)), (0.35, 1, 0.35, 1), "b (phase)"),   # Green
        ("ParamBar_C", Vector((1.5, 0, 0)), (0.45, 0.35, 1, 1), "c (I)"),     # Blue-purple
    ]

    for bar_name, offset, color, label in bar_config:
        bar_pos = position + offset

        # Create bar
        bpy.ops.mesh.primitive_cube_add(size=1, location=bar_pos)
        bar = bpy.context.active_object
        bar.name = bar_name
        bar.scale = (0.4, 0.5, 0.4)  # Initial height 0.5

        # Set origin to bottom
        bpy.context.scene.cursor.location = bar_pos - Vector((0, 0.25, 0))
        bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

        # Material
        mat = create_emissive_material(f"{bar_name}_Mat", color, 0.5)
        bar.data.materials.append(mat)

        # Label
        bpy.ops.object.text_add(location=bar_pos + Vector((0, -0.4, 0)))
        text = bpy.context.active_object
        text.data.body = label
        text.data.align_x = 'CENTER'
        text.data.size = 0.15
        text.name = f"{bar_name}_Label"
        text.rotation_euler = (math.pi/2, 0, 0)

        bars[bar_name] = bar

    return bars


def create_probability_bars(position: Vector = Vector((0, -4, 0))):
    """Create probability distribution bars."""
    bars = {}

    bar_config = [
        ("ProbBar_00", Vector((-2.25, 0, 0)), (0.18, 0.8, 0.44, 1), "|00>"),
        ("ProbBar_01", Vector((-0.75, 0, 0)), (0.2, 0.6, 0.86, 1), "|01>"),
        ("ProbBar_10", Vector((0.75, 0, 0)), (0.91, 0.3, 0.24, 1), "|10>"),
        ("ProbBar_11", Vector((2.25, 0, 0)), (0.61, 0.35, 0.71, 1), "|11>"),
    ]

    for bar_name, offset, color, label in bar_config:
        bar_pos = position + offset

        bpy.ops.mesh.primitive_cube_add(size=1, location=bar_pos)
        bar = bpy.context.active_object
        bar.name = bar_name
        bar.scale = (0.6, 0.25, 0.6)

        # Set origin to bottom
        bpy.context.scene.cursor.location = bar_pos - Vector((0, 0.125, 0))
        bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

        mat = create_emissive_material(f"{bar_name}_Mat", color, 0.4)
        bar.data.materials.append(mat)

        # Label below
        bpy.ops.object.text_add(location=bar_pos + Vector((0, -0.3, 0)))
        text = bpy.context.active_object
        text.data.body = label
        text.data.align_x = 'CENTER'
        text.data.size = 0.12
        text.name = f"{bar_name}_Label"
        text.rotation_euler = (math.pi/2, 0, 0)

        bars[bar_name] = bar

    return bars


def create_fractal_plane(position: Vector = Vector((0, 0, -2.5)),
                         size: float = 2.0):
    """Create a plane for fractal texture display."""
    bpy.ops.mesh.primitive_plane_add(size=size, location=position)
    plane = bpy.context.active_object
    plane.name = "FractalPlane"
    plane.rotation_euler = (math.pi/2, 0, 0)  # Face forward

    # Create material with image texture node (placeholder)
    mat = bpy.data.materials.new(name="FractalMat")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links

    # Add image texture node
    img_node = nodes.new(type='ShaderNodeTexImage')
    img_node.location = (-300, 0)

    # Connect to emission for full brightness
    bsdf = nodes["Principled BSDF"]
    links.new(img_node.outputs['Color'], bsdf.inputs['Base Color'])
    links.new(img_node.outputs['Color'], bsdf.inputs['Emission Color'])
    bsdf.inputs['Emission Strength'].default_value = 0.5

    plane.data.materials.append(mat)

    return plane


def setup_scene_lighting():
    """Set up scene lighting and world."""
    # World background (dark blue)
    world = bpy.data.worlds.get("World") or bpy.data.worlds.new("World")
    bpy.context.scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.02, 0.02, 0.05, 1)
    bg.inputs["Strength"].default_value = 0.5

    # Key light (sun)
    bpy.ops.object.light_add(type='SUN', location=(5, -5, 8))
    sun = bpy.context.active_object
    sun.name = "KeyLight"
    sun.data.energy = 2.0
    sun.data.color = (1, 0.95, 0.9)

    # Fill light (area)
    bpy.ops.object.light_add(type='AREA', location=(-4, -3, 4))
    fill = bpy.context.active_object
    fill.name = "FillLight"
    fill.data.energy = 50
    fill.data.color = (0.7, 0.8, 1)
    fill.data.size = 3


def setup_camera(location: Vector = Vector((0, -10, 2)),
                 look_at: Vector = Vector((0, 0, 0))):
    """Set up scene camera."""
    bpy.ops.object.camera_add(location=location)
    camera = bpy.context.active_object
    camera.name = "Camera"
    bpy.context.scene.camera = camera

    # Point at target
    direction = look_at - location
    camera.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()

    # Camera settings
    camera.data.lens = 35
    camera.data.clip_end = 100

    return camera


def create_dual_bloch_scene():
    """
    Set up complete scene with two Bloch spheres and all visualization elements.
    """
    print("=== Creating Dual Bloch Sphere Scene ===")

    # Clear existing objects
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()

    # Reset cursor
    bpy.context.scene.cursor.location = (0, 0, 0)

    # Create two Bloch spheres
    print("Creating Bloch spheres...")
    sphere_E = create_bloch_sphere_rig("Bloch_E", radius=1.0, position=Vector((-2.5, 0, 0)))
    sphere_I = create_bloch_sphere_rig("Bloch_I", radius=1.0, position=Vector((2.5, 0, 0)))

    # Rename arrows for animation import compatibility
    sphere_E['arrow'].name = "BlochArrow_E"
    sphere_I['arrow'].name = "BlochArrow_I"

    # Create entanglement bridge
    print("Creating entanglement bridge...")
    bridge = create_entanglement_bridge(
        Vector((-1.5, 0, 0)),
        Vector((1.5, 0, 0)),
        "EntanglementBridge"
    )

    # Create parameter bars
    print("Creating parameter bars...")
    param_bars = create_parameter_bars(Vector((0, -3, 0)))

    # Create probability bars
    print("Creating probability bars...")
    prob_bars = create_probability_bars(Vector((0, -5, 0)))

    # Create fractal display plane
    print("Creating fractal plane...")
    fractal_plane = create_fractal_plane(Vector((0, 0, -3)), size=2.5)

    # Setup lighting
    print("Setting up lighting...")
    setup_scene_lighting()

    # Setup camera
    print("Setting up camera...")
    camera = setup_camera(Vector((0, -12, 3)), Vector((0, -1, 0)))

    # Render settings
    bpy.context.scene.render.engine = 'CYCLES'
    bpy.context.scene.cycles.samples = 128
    bpy.context.scene.render.resolution_x = 1920
    bpy.context.scene.render.resolution_y = 1080
    bpy.context.scene.render.fps = 30

    print("\n=== Scene Creation Complete ===")
    print("Objects created:")
    print("  - BlochArrow_E, BlochArrow_I (animated arrows)")
    print("  - EntanglementBridge (emission animation)")
    print("  - ParamBar_A, ParamBar_B, ParamBar_C (parameter display)")
    print("  - ProbBar_00/01/10/11 (probability display)")
    print("  - FractalPlane (texture display)")
    print("\nNext steps:")
    print("  1. Run import_quantum_data.py to load animation")
    print("  2. Press Space to play")
    print("  3. Render with F12")


# ============================================================
# Run when executed in Blender
# ============================================================
if __name__ == '__main__':
    create_dual_bloch_scene()
