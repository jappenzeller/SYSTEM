# Getting Started with Quantum Orbital Visualization

## 🚀 Quick Start (5 Minutes)

### For Blender 3.4 Users

**Step 1:** Install dependencies (run once)
```powershell
.\install_blender_deps.ps1
```

**Step 2:** Open Blender → Scripting workspace → Load `test_in_blender.py` → Run

**Step 3:** See a 1s orbital appear! ✨

---

### For Python Users (No Blender)

**Step 1:** Install dependencies
```bash
pip install -r requirements.txt
```

**Step 2:** Run tests
```bash
python quick_test.py
```

**Step 3:** All tests pass! ✅

---

## 📁 What's What?

| File | Purpose | When to Use |
|------|---------|-------------|
| `INSTALL_BLENDER.md` | **Full installation guide** | First time setup in Blender |
| `install_blender_deps.ps1` | **Installer script** | Run this first for Blender |
| `test_in_blender.py` | **Verification test** | Check if Blender install works |
| `example_blender_script.py` | **Examples** | Create different orbitals |
| `quick_test.py` | **Standalone test** | Test without Blender |
| `README.md` | **Full documentation** | Learn the API |
| `IMPLEMENTATION_SUMMARY.md` | **Technical details** | See what was built |

---

## 🎯 Common Tasks

### Create a 1s Orbital in Blender

```python
from Generators.hydrogen_orbital_meshes import create_orbital_mesh
create_orbital_mesh(n=1, l=0, m=0)
```

### Create a Superposition State

```python
from Generators.hydrogen_orbital_meshes import create_bloch_state_mesh
import numpy as np
create_bloch_state_mesh(theta=np.pi/2, phi=0.0)  # |+⟩ state
```

### Calculate Wave Function (Python)

```python
from Quantum.hydrogen_wavefunctions import hydrogen_orbital
psi = hydrogen_orbital(n=1, l=0, m=0, x=5, y=0, z=0)
print(f"Wave function value: {psi}")
```

### Apply Quantum Gates

```python
from Quantum.bloch_orbital_mapper import create_state
state = create_state('|0⟩')
state.apply_gate('H')  # Hadamard
print(state.get_visualization_info())
```

---

## ❓ Troubleshooting

| Problem | Solution |
|---------|----------|
| scipy not found | Run `install_blender_deps.ps1` |
| Scripts directory error | Check path in line 47 of scripts |
| PowerShell won't run | `Set-ExecutionPolicy RemoteSigned` |
| Mesh too slow | Lower resolution: `resolution=32` |
| Unicode errors in tests | Use `quick_test.py` instead |

---

## 📚 Learn More

- **[INSTALL_BLENDER.md](INSTALL_BLENDER.md)** - Detailed Blender installation
- **[README.md](README.md)** - Complete API documentation
- **[IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md)** - Technical deep dive

---

## 🎓 Examples Included

1. **1s orbital** - Spherical ground state
2. **2p orbitals** - Three directional lobes (px, py, pz)
3. **Bloch states** - Quantum superpositions (|0⟩, |1⟩, |+⟩, |-⟩, |+i⟩, |-i⟩)
4. **3d orbitals** - Complex shapes (dz², dxy, etc.)
5. **Gate operations** - Transform states with quantum gates

All examples are in `example_blender_script.py`!

---

**Ready to create quantum orbitals? Pick your platform above and get started!** ⚛️
