"""
Blender Quantum Setup Script

This script sets up the Quantum Orbital Visualization Framework in Blender.
Load this script in Blender's Text Editor and run it to initialize the framework.

Usage:
1. Open Blender
2. Switch to Scripting workspace
3. Open Text Editor
4. Click 'Open' and select this file
5. Click 'Run Script' (or press Alt+P)
"""

import bpy
import sys
from pathlib import Path

def setup_quantum_framework():
    """Setup the quantum orbital framework in Blender."""

    print("\n" + "=" * 70)
    print("QUANTUM ORBITAL FRAMEWORK - BLENDER SETUP")
    print("=" * 70)

    # Get the directory where this script is located
    # In Blender, bpy.data.filepath gives the .blend file path
    # We need to use the text block's filepath instead

    # Method 1: Try to get path from current text block
    script_dir = None
    for text in bpy.data.texts:
        if text.name == Path(__file__).name or "blender_quantum_setup" in text.name:
            if text.filepath:
                script_dir = Path(text.filepath).parent
                break

    # Method 2: Fallback to hardcoded path (works for your installation)
    if script_dir is None:
        script_dir = Path(r"H:\SpaceTime\SYSTEM\SYSTEM-client-3d\Blender")

    scripts_path = script_dir / "Scripts"

    print(f"\nScript directory: {script_dir}")
    print(f"Scripts path: {scripts_path}")

    # Verify Scripts directory exists
    if not scripts_path.exists():
        print(f"\nERROR: Scripts directory not found!")
        print(f"Expected: {scripts_path}")
        print("\nPlease ensure the Quantum framework is installed correctly.")
        return False

    # Add to Python path if not already there
    scripts_str = str(scripts_path)
    if scripts_str not in sys.path:
        sys.path.insert(0, scripts_str)
        print(f"\nAdded to Python path: {scripts_str}")
    else:
        print(f"\nAlready in Python path: {scripts_str}")

    # Verify imports
    print("\nVerifying imports...")

    try:
        import numpy as np
        print(f"  ✓ numpy {np.__version__}")
    except ImportError as e:
        print(f"  ✗ numpy - NOT INSTALLED")
        print(f"    Run install_blender_deps.ps1 first!")
        return False

    try:
        import scipy
        print(f"  ✓ scipy {scipy.__version__}")
    except ImportError as e:
        print(f"  ✗ scipy - NOT INSTALLED")
        print(f"    Run install_blender_deps.ps1 first!")
        return False

    try:
        from skimage import __version__ as skimage_version
        print(f"  ✓ scikit-image {skimage_version}")
    except ImportError as e:
        print(f"  ✗ scikit-image - NOT INSTALLED")
        print(f"    Run install_blender_deps.ps1 first!")
        return False

    # Try to import quantum modules
    try:
        from Quantum import quantum_constants
        print(f"  ✓ quantum_constants")
    except ImportError as e:
        print(f"  ✗ quantum_constants - {e}")
        return False

    try:
        from Quantum import hydrogen_wavefunctions
        print(f"  ✓ hydrogen_wavefunctions")
    except ImportError as e:
        print(f"  ✗ hydrogen_wavefunctions - {e}")
        return False

    try:
        from Quantum import bloch_orbital_mapper
        print(f"  ✓ bloch_orbital_mapper")
    except ImportError as e:
        print(f"  ✗ bloch_orbital_mapper - {e}")
        return False

    try:
        from Generators import hydrogen_orbital_meshes
        print(f"  ✓ hydrogen_orbital_meshes")
    except ImportError as e:
        print(f"  ✗ hydrogen_orbital_meshes - {e}")
        return False

    print("\n" + "=" * 70)
    print("SUCCESS! Quantum Orbital Framework is ready!")
    print("=" * 70)
    print("\nYou can now:")
    print("  1. Load 'test_in_blender.py' to verify with a simple test")
    print("  2. Load 'example_blender_script.py' to create orbital meshes")
    print("  3. Use the framework in your own scripts")
    print("\nExample:")
    print("  from Generators.hydrogen_orbital_meshes import create_orbital_mesh")
    print("  create_orbital_mesh(n=1, l=0, m=0)  # Create 1s orbital")
    print()

    return True

# Run setup
if __name__ == "__main__":
    success = setup_quantum_framework()
    if not success:
        print("\nSetup failed. Please check the errors above.")
