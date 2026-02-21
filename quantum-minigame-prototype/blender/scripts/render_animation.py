"""
Batch render animation to video or image sequence.

Run this script in Blender after importing animation data.
"""

import bpy
import os
from pathlib import Path


def setup_render_settings(output_path: str,
                           format: str = 'FFMPEG',
                           quality: str = 'HIGH',
                           fps: int = 30):
    """
    Configure render settings for animation output.

    Args:
        output_path: Directory for output files
        format: 'FFMPEG' (video), 'PNG', 'JPEG', 'OPEN_EXR'
        quality: 'LOW', 'MEDIUM', 'HIGH', 'ULTRA'
        fps: Frames per second
    """
    scene = bpy.context.scene
    render = scene.render

    # Output path
    Path(output_path).mkdir(parents=True, exist_ok=True)
    render.filepath = output_path

    # Format settings
    if format == 'FFMPEG':
        render.image_settings.file_format = 'FFMPEG'
        render.ffmpeg.format = 'MPEG4'
        render.ffmpeg.codec = 'H264'

        if quality == 'LOW':
            render.ffmpeg.constant_rate_factor = 'HIGH'
        elif quality == 'MEDIUM':
            render.ffmpeg.constant_rate_factor = 'MEDIUM'
        elif quality == 'HIGH':
            render.ffmpeg.constant_rate_factor = 'PERC_LOSSLESS'
        else:  # ULTRA
            render.ffmpeg.constant_rate_factor = 'LOSSLESS'

        render.ffmpeg.audio_codec = 'NONE'

    elif format == 'PNG':
        render.image_settings.file_format = 'PNG'
        render.image_settings.color_mode = 'RGBA'
        render.image_settings.color_depth = '16'
        render.image_settings.compression = 15

    elif format == 'JPEG':
        render.image_settings.file_format = 'JPEG'
        render.image_settings.quality = 95 if quality in ['HIGH', 'ULTRA'] else 85

    elif format == 'OPEN_EXR':
        render.image_settings.file_format = 'OPEN_EXR'
        render.image_settings.color_depth = '32'
        render.image_settings.exr_codec = 'ZIP'

    # FPS
    render.fps = fps

    # Quality presets
    quality_presets = {
        'LOW': {'resolution_x': 960, 'resolution_y': 540, 'samples': 32},
        'MEDIUM': {'resolution_x': 1280, 'resolution_y': 720, 'samples': 64},
        'HIGH': {'resolution_x': 1920, 'resolution_y': 1080, 'samples': 128},
        'ULTRA': {'resolution_x': 3840, 'resolution_y': 2160, 'samples': 256},
    }

    preset = quality_presets.get(quality, quality_presets['HIGH'])
    render.resolution_x = preset['resolution_x']
    render.resolution_y = preset['resolution_y']
    render.resolution_percentage = 100

    # Engine settings
    if scene.render.engine == 'CYCLES':
        scene.cycles.samples = preset['samples']
        scene.cycles.use_denoising = True
        scene.cycles.denoiser = 'OPENIMAGEDENOISE'


def render_animation(output_path: str = "//../../output/renders/",
                     format: str = 'FFMPEG',
                     quality: str = 'HIGH',
                     frame_start: int = None,
                     frame_end: int = None):
    """
    Render the animation.

    Args:
        output_path: Output directory (Blender relative paths OK)
        format: Output format
        quality: Quality preset
        frame_start: Start frame (None = use scene setting)
        frame_end: End frame (None = use scene setting)
    """
    scene = bpy.context.scene

    # Convert relative path
    abs_output = bpy.path.abspath(output_path)

    # Setup render settings
    setup_render_settings(abs_output, format, quality)

    # Frame range
    if frame_start is not None:
        scene.frame_start = frame_start
    if frame_end is not None:
        scene.frame_end = frame_end

    print(f"=== Rendering Animation ===")
    print(f"Output: {abs_output}")
    print(f"Format: {format}")
    print(f"Quality: {quality}")
    print(f"Frames: {scene.frame_start} - {scene.frame_end}")
    print(f"Resolution: {scene.render.resolution_x}x{scene.render.resolution_y}")

    # Render
    bpy.ops.render.render(animation=True)

    print(f"\n=== Render Complete ===")
    print(f"Output saved to: {abs_output}")


def render_single_frame(output_path: str,
                        frame: int,
                        format: str = 'PNG',
                        quality: str = 'HIGH'):
    """
    Render a single frame as an image.
    """
    scene = bpy.context.scene
    abs_output = bpy.path.abspath(output_path)

    setup_render_settings(abs_output, format, quality)

    scene.frame_set(frame)
    bpy.ops.render.render(write_still=True)

    print(f"Rendered frame {frame} to {abs_output}")


def render_turntable(output_path: str,
                     frames: int = 120,
                     format: str = 'FFMPEG',
                     quality: str = 'HIGH'):
    """
    Render a turntable animation (camera orbits around scene).

    Creates camera animation if it doesn't exist.
    """
    scene = bpy.context.scene
    camera = bpy.data.objects.get('Camera')

    if not camera:
        print("Error: No camera in scene")
        return

    # Create empty at center for camera to orbit around
    center = bpy.data.objects.get('TurntableCenter')
    if not center:
        center = bpy.data.objects.new('TurntableCenter', None)
        bpy.context.collection.objects.link(center)
        center.location = (0, 0, 0)

    # Parent camera to center (if not already)
    if camera.parent != center:
        # Store current camera transform
        camera_loc = camera.location.copy()

        # Parent
        camera.parent = center
        camera.location = camera_loc

    # Animate center rotation
    center.animation_data_clear()
    center.animation_data_create()

    center.rotation_euler = (0, 0, 0)
    center.keyframe_insert(data_path="rotation_euler", frame=1)

    center.rotation_euler = (0, 0, 2 * 3.14159)  # Full rotation
    center.keyframe_insert(data_path="rotation_euler", frame=frames)

    # Linear interpolation for smooth rotation
    for fcurve in center.animation_data.action.fcurves:
        for kf in fcurve.keyframe_points:
            kf.interpolation = 'LINEAR'

    # Set frame range
    scene.frame_start = 1
    scene.frame_end = frames

    # Render
    render_animation(output_path, format, quality, 1, frames)


# ============================================================
# Quick render functions for command line
# ============================================================

def quick_preview():
    """Quick low-res render for preview."""
    render_animation("//../../output/renders/preview_", 'FFMPEG', 'LOW')


def production_render():
    """Full quality production render."""
    render_animation("//../../output/renders/production_", 'FFMPEG', 'ULTRA')


def image_sequence():
    """Render as PNG image sequence."""
    render_animation("//../../output/renders/frames/frame_", 'PNG', 'HIGH')


# ============================================================
# Run when executed in Blender
# ============================================================
if __name__ == '__main__':
    # Default: Medium quality preview
    render_animation(
        output_path="//../../output/renders/quantum_animation_",
        format='FFMPEG',
        quality='MEDIUM'
    )
