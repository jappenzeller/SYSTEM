"""
Test Script for Blender - Verify Quantum Orbital Framework Installation

This script verifies that the Quantum Orbital Visualization Framework
is properly installed and working in Blender 3.4.

USAGE:
1. Run install_blender_deps.ps1 first (installs scipy and scikit-image)
2. Open Blender 3.4
3. Switch to Scripting workspace
4. Load this script in Text Editor
5. Click 'Run Script' (Alt+P)
6. Check the console output for test results
7. A simple 1s orbital should appear in the viewport

If all tests pass, you're ready to use the full framework!
"""

import bpy
import sys
from pathlib import Path

# ============================================================================
# Setup Python Path
# ============================================================================

def setup_path():
    """Add Scripts directory to Python path."""
    # Try multiple methods to find the Scripts directory
    script_dir = None

    # Method 1: From text block filepath
    try:
        for text in bpy.data.texts:
            if "test_in_blender" in text.name and text.filepath:
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

    # Method 3: Hardcoded for known installation
    if script_dir is None or not script_dir.exists():
        script_dir = Path(r"H:\SpaceTime\SYSTEM\SYSTEM-client-3d\Blender\Scripts")

    if script_dir.exists() and str(script_dir) not in sys.path:
        sys.path.insert(0, str(script_dir))
        return script_dir
    elif not script_dir.exists():
        print(f"ERROR: Scripts directory not found!")
        print(f"Expected: {script_dir}")
        return None
    else:
        return script_dir

# ============================================================================
# Test Functions
# ============================================================================

def test_imports():
    """Test that all required modules can be imported."""
    print("\n" + "=" * 70)
    print("TEST 1: Import Verification")
    print("=" * 70)

    all_passed = True

    # Test standard scientific packages
    try:
        import numpy as np
        print(f"  [PASS] numpy {np.__version__}")
    except ImportError as e:
        print(f"  [FAIL] numpy - {e}")
        all_passed = False

    try:
        import scipy
        print(f"  [PASS] scipy {scipy.__version__}")
    except ImportError as e:
        print(f"  [FAIL] scipy - {e}")
        print(f"         Run install_blender_deps.ps1 to install!")
        all_passed = False

    try:
        from skimage import __version__ as skimage_version
        print(f"  [PASS] scikit-image {skimage_version}")
    except ImportError as e:
        print(f"  [FAIL] scikit-image - {e}")
        print(f"         Run install_blender_deps.ps1 to install!")
        all_passed = False

    # Test quantum framework modules
    try:
        from Quantum import quantum_constants
        print(f"  [PASS] quantum_constants")
    except ImportError as e:
        print(f"  [FAIL] quantum_constants - {e}")
        all_passed = False

    try:
        from Quantum import hydrogen_wavefunctions
        print(f"  [PASS] hydrogen_wavefunctions")
    except ImportError as e:
        print(f"  [FAIL] hydrogen_wavefunctions - {e}")
        all_passed = False

    try:
        from Quantum import bloch_orbital_mapper
        print(f"  [PASS] bloch_orbital_mapper")
    except ImportError as e:
        print(f"  [FAIL] bloch_orbital_mapper - {e}")
        all_passed = False

    try:
        from Generators import hydrogen_orbital_meshes
        print(f"  [PASS] hydrogen_orbital_meshes")
    except ImportError as e:
        print(f"  [FAIL] hydrogen_orbital_meshes - {e}")
        all_passed = False

    return all_passed

def test_calculations():
    """Test basic orbital calculations."""
    print("\n" + "=" * 70)
    print("TEST 2: Orbital Calculations")
    print("=" * 70)

    try:
        import numpy as np
        from Quantum.hydrogen_wavefunctions import hydrogen_orbital

        # Calculate 1s orbital at origin
        psi = hydrogen_orbital(1, 0, 0, 0, 0, 0)
        print(f"  [PASS] Calculate psi_1s(0,0,0) = {psi:.6f}")

        # Verify spherical symmetry
        r = 5.0
        points = [(r, 0, 0), (0, r, 0), (0, 0, r)]
        values = [hydrogen_orbital(1, 0, 0, x, y, z) for x, y, z in points]

        if all(np.isclose(v, values[0]) for v in values):
            print(f"  [PASS] 1s orbital is spherically symmetric")
        else:
            print(f"  [FAIL] 1s orbital not symmetric")
            return False

        return True

    except Exception as e:
        print(f"  [FAIL] Calculation error: {e}")
        import traceback
        traceback.print_exc()
        return False

def test_bloch_state():
    """Test Bloch sphere state creation."""
    print("\n" + "=" * 70)
    print("TEST 3: Bloch Sphere States")
    print("=" * 70)

    try:
        import numpy as np
        from Quantum.bloch_orbital_mapper import create_state

        # Create |+> state
        state = create_state('|+⟩')
        probs = state.get_orbital_probabilities()

        prob_1s = probs.get((1, 0, 0), 0)
        prob_2s = probs.get((2, 0, 0), 0)

        print(f"  Created |+> state:")
        print(f"    P(1s) = {prob_1s:.6f}")
        print(f"    P(2s) = {prob_2s:.6f}")

        if np.isclose(prob_1s, 0.5, atol=1e-6) and np.isclose(prob_2s, 0.5, atol=1e-6):
            print(f"  [PASS] Superposition state correct (50/50 mix)")
            return True
        else:
            print(f"  [FAIL] Superposition state incorrect")
            return False

    except Exception as e:
        print(f"  [FAIL] State creation error: {e}")
        import traceback
        traceback.print_exc()
        return False

def test_mesh_generation():
    """Test creating a simple orbital mesh in Blender."""
    print("\n" + "=" * 70)
    print("TEST 4: Blender Mesh Generation")
    print("=" * 70)

    try:
        from Generators.hydrogen_orbital_meshes import create_orbital_mesh, clear_orbital_objects

        # Clear any existing orbital objects
        clear_orbital_objects()
        print(f"  Cleared existing orbital objects")

        # Create a simple 1s orbital with low resolution for speed
        print(f"  Creating 1s orbital mesh (this may take a moment)...")
        objects = create_orbital_mesh(
            n=1, l=0, m=0,
            iso_values=[0.1],  # Just one isosurface for quick test
            resolution=32,     # Low resolution for speed
            name="test_1s"
        )

        if objects and len(objects) > 0:
            print(f"  [PASS] Created {len(objects)} mesh object(s)")
            print(f"         Mesh name: {objects[0].name}")
            print(f"         Vertices: {len(objects[0].data.vertices)}")
            print(f"         Faces: {len(objects[0].data.polygons)}")

            # Center view on the object
            bpy.ops.object.select_all(action='DESELECT')
            objects[0].select_set(True)
            bpy.context.view_layer.objects.active = objects[0]

            print(f"\n  SUCCESS! Check the 3D viewport for the orbital mesh!")
            return True
        else:
            print(f"  [FAIL] No mesh objects created")
            return False

    except Exception as e:
        print(f"  [FAIL] Mesh generation error: {e}")
        import traceback
        traceback.print_exc()
        return False

# ============================================================================
# Main Test Runner
# ============================================================================

def run_all_tests():
    """Run all verification tests."""
    print("\n" + "=" * 70)
    print("QUANTUM ORBITAL FRAMEWORK - BLENDER 3.4 VERIFICATION")
    print("=" * 70)

    # Setup path
    script_dir = setup_path()
    if script_dir is None:
        print("\n[FAIL] Could not find Scripts directory!")
        print("Please ensure the framework is installed correctly.")
        return False

    print(f"\nScripts directory: {script_dir}")

    # Run tests
    results = []

    results.append(("Import Verification", test_imports()))
    if results[0][1]:  # Only continue if imports work
        results.append(("Orbital Calculations", test_calculations()))
        results.append(("Bloch Sphere States", test_bloch_state()))
        results.append(("Mesh Generation", test_mesh_generation()))

    # Summary
    print("\n" + "=" * 70)
    print("TEST SUMMARY")
    print("=" * 70)

    passed = sum(1 for _, result in results if result)
    total = len(results)

    for name, result in results:
        status = "[PASS]" if result else "[FAIL]"
        print(f"  {status} {name}")

    print(f"\n  Total: {passed}/{total} tests passed")

    if passed == total:
        print("\n" + "=" * 70)
        print("SUCCESS! All tests passed!")
        print("=" * 70)
        print("\nThe Quantum Orbital Framework is fully functional in Blender!")
        print("\nNext steps:")
        print("  - Load 'example_blender_script.py' to create more orbitals")
        print("  - Check out the README.md for full documentation")
        print("  - Try creating different quantum states!")
    else:
        print("\n" + "=" * 70)
        print(f"INCOMPLETE: {total - passed} test(s) failed")
        print("=" * 70)
        print("\nPlease check the errors above and:")
        print("  1. Make sure you ran install_blender_deps.ps1")
        print("  2. Verify scipy and scikit-image are installed")
        print("  3. Check that all framework files are present")

    return passed == total

# ============================================================================
# Entry Point
# ============================================================================

if __name__ == "__main__":
    success = run_all_tests()
