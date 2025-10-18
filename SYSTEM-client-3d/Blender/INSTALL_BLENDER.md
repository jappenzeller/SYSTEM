# Installing Quantum Orbital Framework in Blender 3.4

## Quick Start (3 Simple Steps)

### Step 1: Install Python Dependencies

Open PowerShell in this directory and run:

```powershell
.\install_blender_deps.ps1
```

This will install `scipy` and `scikit-image` into Blender's Python environment.

**Expected output:**
```
======================================================================
Quantum Orbital Framework - Blender 3.4 Dependency Installer
======================================================================

Found Blender Python:
  H:\Program Files\Blender Foundation\Blender 3.4\3.4\python\bin\python.exe

Installing scipy...
  SUCCESS: scipy installed

Installing scikit-image...
  SUCCESS: scikit-image installed

SUCCESS! All dependencies installed successfully!
```

### Step 2: Open Blender and Load Test Script

1. Open Blender 3.4
2. Switch to **Scripting** workspace (top menu)
3. In the Text Editor panel, click **Open**
4. Navigate to this directory and select `test_in_blender.py`
5. Click **Run Script** button (or press `Alt+P`)

**Expected output in Console:**
```
======================================================================
QUANTUM ORBITAL FRAMEWORK - BLENDER 3.4 VERIFICATION
======================================================================

TEST 1: Import Verification
  [PASS] numpy 1.22.0
  [PASS] scipy 1.11.x
  [PASS] scikit-image 0.21.x
  [PASS] quantum_constants
  [PASS] hydrogen_wavefunctions
  [PASS] bloch_orbital_mapper
  [PASS] hydrogen_orbital_meshes

TEST 2: Orbital Calculations
  [PASS] Calculate psi_1s(0,0,0) = 0.564190
  [PASS] 1s orbital is spherically symmetric

TEST 3: Bloch Sphere States
  Created |+> state:
    P(1s) = 0.500000
    P(2s) = 0.500000
  [PASS] Superposition state correct (50/50 mix)

TEST 4: Blender Mesh Generation
  Cleared existing orbital objects
  Creating 1s orbital mesh (this may take a moment)...
  [PASS] Created 1 mesh object(s)
         Mesh name: test_1s_iso0
         Vertices: 1234
         Faces: 2468

  SUCCESS! Check the 3D viewport for the orbital mesh!

======================================================================
SUCCESS! All tests passed!
======================================================================
```

You should see a **1s orbital mesh** appear in the 3D viewport!

### Step 3: Create More Orbitals

Load and run `example_blender_script.py` to create various orbital types:

1. In Text Editor, click **Open**
2. Select `example_blender_script.py`
3. Uncomment one of the example functions at the bottom:
   ```python
   example_1s_orbital()              # Simple 1s orbital
   # example_2p_orbitals()           # 2px, 2py, 2pz orbitals
   # example_bloch_state()           # |+⟩ superposition state
   # example_all_cardinal_states()   # All 6 Bloch states
   # example_all_orbitals()          # s, p, d orbitals
   ```
4. Click **Run Script**

---

## Troubleshooting

### Problem: "scipy - NOT INSTALLED"

**Solution:**
```powershell
# Manually install scipy
& "H:\Program Files\Blender Foundation\Blender 3.4\3.4\python\bin\python.exe" -m pip install scipy
```

### Problem: "scikit-image - NOT INSTALLED"

**Solution:**
```powershell
# Manually install scikit-image
& "H:\Program Files\Blender Foundation\Blender 3.4\3.4\python\bin\python.exe" -m pip install scikit-image
```

### Problem: "Scripts directory not found"

**Solution:**
The framework expects to find its files at:
```
H:\SpaceTime\SYSTEM\SYSTEM-client-3d\Blender\Scripts\
```

If your files are in a different location, edit the hardcoded path in:
- `test_in_blender.py` (line 47)
- `example_blender_script.py` (line 46)
- `blender_quantum_setup.py` (line 30)

Change this line:
```python
script_dir = Path(r"H:\SpaceTime\SYSTEM\SYSTEM-client-3d\Blender\Scripts")
```

To your actual path.

### Problem: PowerShell script won't run

**Solution:**
You may need to allow script execution:
```powershell
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
```

Then run the install script again.

### Problem: Mesh generation is slow

**Solution:**
In the script, reduce the resolution:
```python
create_orbital_mesh(n=1, l=0, m=0, resolution=32)  # Fast (default: 64)
```

Lower resolution = faster generation, less detail.

---

## Manual Installation (Alternative Method)

If the PowerShell script doesn't work, install packages manually:

```powershell
# Open PowerShell and navigate to Blender's Python
cd "H:\Program Files\Blender Foundation\Blender 3.4\3.4\python\bin"

# Install scipy
.\python.exe -m pip install scipy

# Install scikit-image
.\python.exe -m pip install scikit-image

# Verify installation
.\python.exe -c "import numpy; import scipy; from skimage import measure; print('Success!')"
```

---

## What Gets Installed

### Python Packages (into Blender's Python)
- **scipy** (~50 MB) - Special functions (Laguerre polynomials, spherical harmonics)
- **scikit-image** (~40 MB) - Marching cubes algorithm for mesh generation
- **numpy** (already included with Blender)

### Framework Files (already present)
```
Blender/
├── Scripts/
│   ├── Quantum/           # Core physics and quantum computing modules
│   └── Generators/        # Blender mesh generation
├── install_blender_deps.ps1        # This installation script
├── blender_quantum_setup.py        # Setup verification (optional)
├── test_in_blender.py             # Installation test (run this first!)
├── example_blender_script.py       # Examples and demos
└── README.md                       # Full documentation
```

---

## Next Steps After Installation

### 1. Learn the API

Read the [README.md](README.md) for:
- Module documentation
- Usage examples
- API reference
- Mathematical background

### 2. Create Custom Orbitals

```python
from Generators.hydrogen_orbital_meshes import create_orbital_mesh

# Create 2pz orbital
create_orbital_mesh(n=2, l=1, m=0, resolution=64)

# Create 3dz² orbital
create_orbital_mesh(n=3, l=2, m=0, resolution=64)
```

### 3. Visualize Quantum States

```python
from Generators.hydrogen_orbital_meshes import create_bloch_state_mesh
import numpy as np

# Create |+⟩ superposition state
create_bloch_state_mesh(theta=np.pi/2, phi=0.0, basis='sp')

# Create custom state at arbitrary Bloch angles
create_bloch_state_mesh(theta=np.pi/4, phi=np.pi/3, basis='sp')
```

### 4. Export to Unity

```python
from Generators.hydrogen_orbital_meshes import export_orbital_mesh

# Select an orbital object in Blender
obj = bpy.context.active_object

# Export as FBX for Unity
export_orbital_mesh(obj, filepath="1s_orbital.fbx", format='FBX')
```

---

## System Requirements

- **Blender**: 3.0 or later (tested on 3.4)
- **OS**: Windows, macOS, or Linux
- **Disk Space**: ~100 MB for dependencies
- **RAM**: 4 GB minimum (8 GB recommended for high-resolution orbitals)

---

## Support

If you encounter issues:

1. Check the [Troubleshooting](#troubleshooting) section above
2. Verify all files are present in the `Scripts/` directory
3. Check Blender's system console for detailed error messages
   - Windows: Window → Toggle System Console
4. Try the manual installation method
5. See [README.md](README.md) for detailed documentation

---

## Uninstallation

To remove the framework:

```powershell
# Remove Python packages from Blender
& "H:\Program Files\Blender Foundation\Blender 3.4\3.4\python\bin\python.exe" -m pip uninstall scipy scikit-image -y

# Delete framework files (optional)
# Just delete the Blender/ directory or keep it for later
```

---

**Installation complete! You're ready to visualize quantum orbitals in Blender!** 🎉
