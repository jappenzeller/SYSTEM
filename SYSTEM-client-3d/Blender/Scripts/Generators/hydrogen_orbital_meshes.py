"""
Hydrogen Orbital Mesh Generation for Blender

Generates 3D meshes for hydrogen orbitals and quantum states using
Blender's Python API. Works both inside Blender and as standalone
tool for mesh export.
"""

import numpy as np

# Try to import Blender, but allow standalone usage
try:
    import bpy
    BLENDER_AVAILABLE = True
except ImportError:
    BLENDER_AVAILABLE = False
    print("Warning: Blender (bpy) not available. Mesh creation will be disabled.")

# Import quantum modules
import sys
from pathlib import Path

# Add parent directory to path for imports
script_dir = Path(__file__).parent.parent
if str(script_dir) not in sys.path:
    sys.path.insert(0, str(script_dir))

from Quantum.bloch_orbital_mapper import BlochOrbitalState, create_state, create_bloch_state
from Quantum.quantum_constants import (
    DEFAULT_GRID_RESOLUTION,
    DEFAULT_ISO_VALUES,
    get_orbital_color,
    get_orbital_name,
)
from Quantum.hydrogen_wavefunctions import calculate_density_grid

# ============================================================================
# Mesh Generation (using scikit-image marching cubes)
# ============================================================================

def generate_isosurface_mesh(density_grid, x_grid, y_grid, z_grid, iso_value):
    """
    Generate isosurface mesh from density grid using marching cubes.

    Args:
        density_grid: 3D array of density values
        x_grid, y_grid, z_grid: Coordinate grids
        iso_value: Isosurface threshold value

    Returns:
        tuple: (vertices, faces) where vertices is Nx3 array, faces is Mx3 array
    """
    try:
        from skimage import measure
    except ImportError:
        raise ImportError(
            "scikit-image required for mesh generation. "
            "Install with: pip install scikit-image"
        )

    # Run marching cubes
    verts, faces, normals, values = measure.marching_cubes(
        density_grid,
        level=iso_value,
        spacing=(
            x_grid[1, 0, 0] - x_grid[0, 0, 0],
            y_grid[0, 1, 0] - y_grid[0, 0, 0],
            z_grid[0, 0, 1] - z_grid[0, 0, 0],
        )
    )

    # Offset vertices to match grid origin
    verts[:, 0] += x_grid[0, 0, 0]
    verts[:, 1] += y_grid[0, 0, 0]
    verts[:, 2] += z_grid[0, 0, 0]

    return verts, faces

# ============================================================================
# Blender Mesh Creation
# ============================================================================

def create_blender_mesh(name, vertices, faces, color=None):
    """
    Create a Blender mesh object from vertices and faces.

    Args:
        name: Name for the mesh object
        vertices: Nx3 numpy array of vertex positions
        faces: Mx3 numpy array of face indices
        color: Optional RGBA color tuple

    Returns:
        bpy.types.Object: Created mesh object (if Blender available)
    """
    if not BLENDER_AVAILABLE:
        raise RuntimeError("Blender not available. Cannot create mesh.")

    # Create mesh data
    mesh = bpy.data.meshes.new(name=f"{name}_mesh")

    # Convert to lists for Blender
    verts_list = vertices.tolist()
    faces_list = faces.tolist()

    # Create mesh from data
    mesh.from_pydata(verts_list, [], faces_list)
    mesh.update()

    # Create object
    obj = bpy.data.objects.new(name, mesh)

    # Add to scene
    bpy.context.collection.objects.link(obj)

    # Create material if color provided
    if color is not None:
        mat = bpy.data.materials.new(name=f"{name}_material")
        mat.use_nodes = True

        # Get principled BSDF
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs['Base Color'].default_value = color
            bsdf.inputs['Alpha'].default_value = color[3] if len(color) > 3 else 1.0

            # Enable transparency
            mat.blend_method = 'BLEND'
            mat.show_transparent_back = True

        # Assign material
        if obj.data.materials:
            obj.data.materials[0] = mat
        else:
            obj.data.materials.append(mat)

    # Smooth shading
    for poly in mesh.polygons:
        poly.use_smooth = True

    return obj

# ============================================================================
# Orbital Mesh Creation
# ============================================================================

def create_orbital_mesh(n, l, m, iso_values=None, resolution=None, name=None):
    """
    Create Blender mesh for a single hydrogen orbital.

    Args:
        n, l, m: Quantum numbers
        iso_values: List of isosurface values (default: from constants)
        resolution: Grid resolution (default: from constants)
        name: Custom name for mesh (default: auto-generated)

    Returns:
        list: List of created Blender objects (one per isosurface)
    """
    if not BLENDER_AVAILABLE:
        raise RuntimeError("Blender not available. Cannot create mesh.")

    if iso_values is None:
        iso_values = DEFAULT_ISO_VALUES

    if resolution is None:
        resolution = DEFAULT_GRID_RESOLUTION

    if name is None:
        name = get_orbital_name(n, l, m)

    # Calculate density grid
    x_grid, y_grid, z_grid, density_grid = calculate_density_grid(
        n, l, m, resolution=resolution
    )

    # Normalize density for consistent iso_values
    max_density = np.max(density_grid)
    if max_density > 0:
        density_grid = density_grid / max_density

    # Create mesh for each isosurface
    objects = []
    color = get_orbital_color(l)

    for i, iso_frac in enumerate(iso_values):
        # Generate mesh
        try:
            verts, faces = generate_isosurface_mesh(
                density_grid, x_grid, y_grid, z_grid, iso_frac
            )
        except ValueError as e:
            print(f"Warning: Could not generate isosurface at {iso_frac}: {e}")
            continue

        if len(verts) == 0 or len(faces) == 0:
            continue

        # Adjust alpha based on isosurface level
        iso_color = list(color[:3]) + [color[3] * (1.0 - i / len(iso_values))]

        # Create Blender object
        obj_name = f"{name}_iso{i}"
        obj = create_blender_mesh(obj_name, verts, faces, color=iso_color)

        objects.append(obj)

    return objects

# ============================================================================
# Bloch State Mesh Creation
# ============================================================================

def create_bloch_state_mesh(theta, phi, basis='sp', iso_values=None, resolution=None, name=None):
    """
    Create Blender mesh for a quantum state defined by Bloch angles.

    Args:
        theta, phi: Bloch sphere angles
        basis: Orbital basis ('sp', 'pp', or 'sd')
        iso_values: List of isosurface values
        resolution: Grid resolution
        name: Custom name for mesh

    Returns:
        list: List of created Blender objects
    """
    if not BLENDER_AVAILABLE:
        raise RuntimeError("Blender not available. Cannot create mesh.")

    # Create state
    state = create_bloch_state(theta, phi, basis)

    if name is None:
        info = state.get_visualization_info()
        name = f"{info['state_label']}_{basis}"

    # Calculate density grid
    x_grid, y_grid, z_grid, density_grid = state.calculate_density_grid(resolution=resolution)

    # Normalize
    max_density = np.max(density_grid)
    if max_density > 0:
        density_grid = density_grid / max_density

    if iso_values is None:
        iso_values = DEFAULT_ISO_VALUES

    # Create mesh for each isosurface
    objects = []

    # Get dominant orbital for coloring
    dominant_nlm, _ = state.get_dominant_orbital()
    _, l, _ = dominant_nlm
    color = get_orbital_color(l)

    for i, iso_frac in enumerate(iso_values):
        try:
            verts, faces = generate_isosurface_mesh(
                density_grid, x_grid, y_grid, z_grid, iso_frac
            )
        except ValueError as e:
            print(f"Warning: Could not generate isosurface at {iso_frac}: {e}")
            continue

        if len(verts) == 0 or len(faces) == 0:
            continue

        # Adjust alpha
        iso_color = list(color[:3]) + [color[3] * (1.0 - i / len(iso_values))]

        # Create object
        obj_name = f"{name}_iso{i}"
        obj = create_blender_mesh(obj_name, verts, faces, color=iso_color)

        objects.append(obj)

    return objects

# ============================================================================
# Gate Animation
# ============================================================================

def animate_gate_operation(initial_state, gate, duration=1.0, frames=60, basis='sp'):
    """
    Create animation of quantum gate operation.

    Args:
        initial_state: BlochOrbitalState or (theta, phi) tuple
        gate: Gate name or matrix
        duration: Animation duration in seconds
        frames: Number of frames
        basis: Orbital basis

    Returns:
        list: List of created keyframed objects
    """
    if not BLENDER_AVAILABLE:
        raise RuntimeError("Blender not available. Cannot create animation.")

    # Create initial state if needed
    if isinstance(initial_state, tuple):
        theta, phi = initial_state
        state = create_bloch_state(theta, phi, basis)
    else:
        state = initial_state

    # Calculate final state
    final_state = BlochOrbitalState(state.theta, state.phi, basis)
    final_state.apply_gate(gate)

    # Create initial mesh
    objects = create_bloch_state_mesh(
        state.theta, state.phi, basis,
        name=f"gate_anim_initial"
    )

    # Set up animation
    fps = bpy.context.scene.render.fps
    frame_duration = int(duration * fps)

    for frame in range(frames + 1):
        t = frame / frames

        # Interpolate between states (linear on Bloch sphere)
        theta_t = state.theta + t * (final_state.theta - state.theta)
        phi_t = state.phi + t * (final_state.phi - state.phi)

        # Calculate density for this frame
        interp_state = create_bloch_state(theta_t, phi_t, basis)
        x_grid, y_grid, z_grid, density = interp_state.calculate_density_grid()

        # Update mesh (simplified - just update first object for demo)
        if objects and len(objects) > 0:
            obj = objects[0]

            # Set keyframe for visibility/alpha
            frame_num = int(frame * frame_duration / frames)
            bpy.context.scene.frame_set(frame_num)

            # Could update mesh vertices here for smooth animation
            # For now, just keyframe material opacity

            if obj.data.materials:
                mat = obj.data.materials[0]
                if mat.use_nodes:
                    bsdf = mat.node_tree.nodes.get("Principled BSDF")
                    if bsdf:
                        alpha_value = 0.7 * (1.0 - abs(2 * t - 1))  # Fade in/out
                        bsdf.inputs['Alpha'].default_value = alpha_value
                        bsdf.inputs['Alpha'].keyframe_insert(data_path="default_value", frame=frame_num)

    return objects

# ============================================================================
# Utility Functions
# ============================================================================

def clear_orbital_objects():
    """Remove all orbital-related objects from scene."""
    if not BLENDER_AVAILABLE:
        return

    # Delete objects with orbital-related names
    for obj in bpy.data.objects:
        if any(keyword in obj.name.lower() for keyword in ['orbital', 'iso', 'bloch', 'gate']):
            bpy.data.objects.remove(obj, do_unlink=True)

def export_orbital_mesh(obj, filepath, format='OBJ'):
    """
    Export orbital mesh to file.

    Args:
        obj: Blender object to export
        filepath: Output file path
        format: Export format ('OBJ', 'FBX', 'GLTF')
    """
    if not BLENDER_AVAILABLE:
        raise RuntimeError("Blender not available. Cannot export mesh.")

    # Select only this object
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    # Export based on format
    if format.upper() == 'OBJ':
        bpy.ops.export_scene.obj(filepath=filepath, use_selection=True)
    elif format.upper() == 'FBX':
        bpy.ops.export_scene.fbx(filepath=filepath, use_selection=True)
    elif format.upper() == 'GLTF':
        bpy.ops.export_scene.gltf(filepath=filepath, use_selection=True)
    else:
        raise ValueError(f"Unsupported format: {format}")

    print(f"Exported {obj.name} to {filepath}")

# ============================================================================
# Example Usage Functions
# ============================================================================

def create_example_orbitals():
    """Create example orbitals (1s, 2s, 2p, 3d) for demonstration."""
    if not BLENDER_AVAILABLE:
        print("Blender not available.")
        return

    orbitals = [
        (1, 0, 0),  # 1s
        (2, 0, 0),  # 2s
        (2, 1, 0),  # 2pz
        (2, 1, 1),  # 2px
        (3, 2, 0),  # 3dz²
    ]

    all_objects = []
    x_offset = 0

    for n, l, m in orbitals:
        print(f"Creating {get_orbital_name(n, l, m)} orbital...")

        objects = create_orbital_mesh(n, l, m, resolution=64)

        # Offset in X for layout
        for obj in objects:
            obj.location.x += x_offset

        all_objects.extend(objects)
        x_offset += 50  # Spacing in Bohr radii

    print(f"Created {len(all_objects)} orbital meshes.")
    return all_objects

def create_bloch_sphere_states():
    """Create meshes for the 6 cardinal Bloch sphere states."""
    if not BLENDER_AVAILABLE:
        print("Blender not available.")
        return

    states = ['|0⟩', '|1⟩', '|+⟩', '|-⟩', '|+i⟩', '|-i⟩']

    all_objects = []
    x_offset = 0

    for state_name in states:
        print(f"Creating {state_name} state...")

        state = create_state(state_name, basis='sp')
        objects = create_bloch_state_mesh(
            state.theta, state.phi,
            basis='sp',
            name=state_name.strip('⟩').strip('|')
        )

        # Offset in X
        for obj in objects:
            obj.location.x += x_offset

        all_objects.extend(objects)
        x_offset += 40

    print(f"Created {len(all_objects)} state meshes.")
    return all_objects

# ============================================================================
# Main Entry Point for Blender
# ============================================================================

if __name__ == "__main__" and BLENDER_AVAILABLE:
    print("Running hydrogen orbital mesh generator in Blender...")

    # Clear existing orbital objects
    clear_orbital_objects()

    # Create example orbitals
    # Uncomment one of these:

    # create_example_orbitals()
    create_bloch_sphere_states()

    print("Done!")
