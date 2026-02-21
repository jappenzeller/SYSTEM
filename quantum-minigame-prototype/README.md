# Quantum Minigame Visual Prototype

A visualization pipeline for the A Gate quantum circuit, featuring dual Bloch spheres, entanglement visualization, and Julia set fractal fingerprints.

## Overview

This prototype creates stunning visual representations of quantum states evolving through the A Gate circuit, used in the SYSTEM quantum mining game. The pipeline:

1. **Python Core** - Simulates quantum circuits and extracts visualization data
2. **Animation Export** - Generates keyframe data as JSON
3. **Blender Rendering** - Creates high-quality 3D animations

## Quick Start

### Python Setup

```bash
# Create virtual environment
python -m venv venv

# Activate (Windows)
venv\Scripts\activate

# Activate (Unix/macOS)
source venv/bin/activate

# Install dependencies
pip install -r requirements.txt

# Run tests
python -m python.test_pipeline

# Launch interactive explorer
python -m python.interactive_preview
```

### Blender Workflow

1. Run `blender/scripts/bloch_sphere_setup.py` to create the scene
2. Edit `import_quantum_data.py` to point to your animation JSON
3. Run `import_quantum_data.py` to animate the scene
4. Press Space to play, F12 to render

## Project Structure

```
quantum-minigame-prototype/
├── python/
│   ├── agate_circuit.py          # A Gate quantum circuit (Qiskit)
│   ├── pn_dynamics.py            # ODE parameter evolution
│   ├── quantum_state.py          # State extraction (Bloch, entanglement)
│   ├── fractal_generator.py      # Julia set from amplitudes
│   ├── animation_exporter.py     # Export keyframes to JSON
│   ├── interactive_preview.py    # Matplotlib real-time test
│   └── test_pipeline.py          # Unit tests
│
├── blender/
│   └── scripts/
│       ├── bloch_sphere_setup.py     # Create visualization scene
│       ├── import_quantum_data.py    # Load JSON animation
│       └── render_animation.py       # Batch render
│
├── data/
│   ├── animations/               # Exported JSON files
│   └── fractals/                 # Rendered fractal images
│
├── output/
│   └── renders/                  # Final video output
│
└── requirements.txt
```

## The A Gate Circuit

```
q0 (E): ─[H]─[P(b)]─[Rx(2a)]─[P(b)]─[H]─────●─────[Rz(π/4)]─
                                             │
q1 (I): ─[H]─[P(b)]─[Ry(2c)]─[P(b)]─[H]─[Ry(π/4)]───────────
```

**Parameters:**
- `a` ∈ [0, 1]: Excitatory amplitude (fast decay)
- `b` ∈ [0, 2π]: Shared phase (integration)
- `c` ∈ [0, 1]: Inhibitory amplitude (slow growth)

## Visualization Elements

### Dual Bloch Spheres
- **E (q0)**: Red-orange sphere for excitatory qubit
- **I (q1)**: Blue-purple sphere for inhibitory qubit
- State vector arrows show quantum state direction
- Arrow length indicates purity (shorter = more mixed)

### Entanglement Bridge
- Glowing purple connector between spheres
- Brightness proportional to concurrence (entanglement measure)
- 0 = separable states, 1 = maximally entangled

### Fractal Fingerprint
- Julia set generated from 4 complex amplitudes
- Unique visual signature for each quantum state
- Blue-purple-gold color scheme

### Parameter Display
- Three bars showing a, b, c values
- Height animation tracks parameter evolution
- Color-coded: red (a), green (b), blue (c)

## Color Palette

| Element | Color | Hex | Usage |
|---------|-------|-----|-------|
| Excitatory | Red-Orange | #FF6B35 | E qubit, a parameter |
| Inhibitory | Blue-Purple | #7B68EE | I qubit, c parameter |
| Phase | Green | #00D084 | b parameter |
| Entanglement | Magenta | #FF00FF | Bridge glow |
| Background | Deep Blue | #0A0A1A | Scene background |

## API Examples

### Generate Animation

```python
from python.pn_dynamics import PNDynamics, generate_sine_input
from python.animation_exporter import generate_animation_data, export_animation_json

# Create input signal
signal = generate_sine_input(duration=5.0, frequency=0.3)

# Generate animation frames
frames = generate_animation_data(signal, sample_rate=5)

# Export to JSON
export_animation_json(frames, 'my_animation.json')
```

### Extract Quantum State

```python
from python.agate_circuit import create_agate_circuit
from python.quantum_state import extract_visualization_data

circuit = create_agate_circuit(a=0.5, b=1.0, c=0.3)
viz = extract_visualization_data(circuit)

print(f"Bloch E: {viz['bloch_E']}")
print(f"Concurrence: {viz['concurrence']}")
```

### Generate Fractal

```python
from python.agate_circuit import create_agate_circuit, get_statevector
from python.fractal_generator import generate_fractal_from_state, save_fractal

circuit = create_agate_circuit(a=0.6, b=1.2, c=0.5)
sv = get_statevector(circuit)

fractal = generate_fractal_from_state(sv, width=512, height=512)
save_fractal(fractal, 'quantum_fractal.png', colormap='quantum')
```

## License

Part of the SYSTEM quantum mining game project.
