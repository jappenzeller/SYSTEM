"""
Export animation keyframes for Blender import.

Generates JSON files containing:
- Parameter evolution (a, b, c over time)
- Bloch sphere coordinates per frame
- Entanglement values per frame
- Fractal parameters per frame
"""

import json
import numpy as np
from pathlib import Path
from typing import List, Dict, Optional
from dataclasses import dataclass, asdict

from .agate_circuit import create_agate_circuit
from .pn_dynamics import PNDynamics, PNConfig
from .quantum_state import extract_visualization_data, bloch_to_spherical
from .fractal_generator import statevector_to_julia_param


@dataclass
class AnimationFrame:
    """Single frame of animation data."""
    frame: int
    time: float

    # Parameters
    a: float
    b: float
    c: float
    f: float  # Input signal

    # Bloch spheres (Cartesian)
    bloch_E: tuple  # (x, y, z)
    bloch_I: tuple  # (x, y, z)

    # Bloch spheres (Spherical for easier Blender rotation)
    spherical_E: tuple  # (r, theta, phi)
    spherical_I: tuple  # (r, theta, phi)

    # Quantum properties
    purity_E: float
    purity_I: float
    concurrence: float

    # Fractal
    julia_c_real: float
    julia_c_imag: float

    # Probabilities
    prob_00: float
    prob_01: float
    prob_10: float
    prob_11: float


def generate_animation_data(input_signal: np.ndarray,
                            config: Optional[PNConfig] = None,
                            sample_rate: int = 1) -> List[Dict]:
    """
    Generate full animation data from input signal.

    Args:
        input_signal: Input signal array
        config: PN dynamics configuration
        sample_rate: Sample every N steps (for large signals)

    Returns:
        List of frame dictionaries
    """
    if config is None:
        config = PNConfig()

    pn = PNDynamics(config)
    frames = []

    for i, f_t in enumerate(input_signal):
        if i % sample_rate != 0:
            pn.step(f_t)
            continue

        # Evolve dynamics
        a, b, c = pn.step(f_t)

        # Create circuit and extract state
        circuit = create_agate_circuit(a, b, c)
        viz_data = extract_visualization_data(circuit)

        # Convert Bloch to spherical
        sph_E = bloch_to_spherical(viz_data['bloch_E'])
        sph_I = bloch_to_spherical(viz_data['bloch_I'])

        # Get Julia parameter
        julia_c = statevector_to_julia_param(viz_data['statevector'], method='weighted')

        # Probabilities
        probs = viz_data['probabilities']

        frame = AnimationFrame(
            frame=i // sample_rate,
            time=i * config.dt,
            a=a, b=b, c=c, f=float(f_t),
            bloch_E=viz_data['bloch_E'],
            bloch_I=viz_data['bloch_I'],
            spherical_E=sph_E,
            spherical_I=sph_I,
            purity_E=viz_data['purity_E'],
            purity_I=viz_data['purity_I'],
            concurrence=viz_data['concurrence'],
            julia_c_real=julia_c.real,
            julia_c_imag=julia_c.imag,
            prob_00=probs[0],
            prob_01=probs[1],
            prob_10=probs[2],
            prob_11=probs[3]
        )

        frames.append(asdict(frame))

    return frames


def generate_parameter_sweep(param_name: str, values: np.ndarray,
                             fixed_params: Dict[str, float]) -> List[Dict]:
    """
    Generate animation data for sweeping a single parameter.

    Args:
        param_name: 'a', 'b', or 'c'
        values: Array of parameter values to sweep
        fixed_params: Fixed values for other parameters

    Returns:
        List of frame dictionaries
    """
    frames = []

    for i, val in enumerate(values):
        params = fixed_params.copy()
        params[param_name] = float(val)

        circuit = create_agate_circuit(**params)
        viz_data = extract_visualization_data(circuit)

        sph_E = bloch_to_spherical(viz_data['bloch_E'])
        sph_I = bloch_to_spherical(viz_data['bloch_I'])
        julia_c = statevector_to_julia_param(viz_data['statevector'], method='weighted')
        probs = viz_data['probabilities']

        frame = AnimationFrame(
            frame=i,
            time=float(val),  # Use param value as "time"
            a=params['a'], b=params['b'], c=params['c'], f=0.0,
            bloch_E=viz_data['bloch_E'],
            bloch_I=viz_data['bloch_I'],
            spherical_E=sph_E,
            spherical_I=sph_I,
            purity_E=viz_data['purity_E'],
            purity_I=viz_data['purity_I'],
            concurrence=viz_data['concurrence'],
            julia_c_real=julia_c.real,
            julia_c_imag=julia_c.imag,
            prob_00=probs[0],
            prob_01=probs[1],
            prob_10=probs[2],
            prob_11=probs[3]
        )

        frames.append(asdict(frame))

    return frames


def export_animation_json(frames: List[Dict], filepath: str,
                          metadata: Optional[Dict] = None):
    """
    Export animation data to JSON file.

    Args:
        frames: List of frame dictionaries
        filepath: Output JSON path
        metadata: Optional metadata (config, description, etc.)
    """
    output = {
        'metadata': metadata or {},
        'frame_count': len(frames),
        'frames': frames
    }

    Path(filepath).parent.mkdir(parents=True, exist_ok=True)

    with open(filepath, 'w') as f:
        json.dump(output, f, indent=2)

    print(f"Exported {len(frames)} frames to {filepath}")


def export_fractal_sequence(frames: List[Dict], output_dir: str,
                            width: int = 512, height: int = 512,
                            colormap: str = 'quantum'):
    """
    Export fractal images for each frame.

    Useful for high-quality fractal rendering (vs real-time in Blender).
    """
    from .fractal_generator import julia_set, save_fractal

    output_path = Path(output_dir)
    output_path.mkdir(parents=True, exist_ok=True)

    total = len(frames)
    for idx, frame_data in enumerate(frames):
        c = complex(frame_data['julia_c_real'], frame_data['julia_c_imag'])
        fractal = julia_set(c, width, height)

        filename = f"fractal_{frame_data['frame']:04d}.png"
        save_fractal(fractal, str(output_path / filename), colormap)

        if (idx + 1) % 10 == 0 or idx == total - 1:
            print(f"  Exported fractal {idx + 1}/{total}")

    print(f"Exported {total} fractal images to {output_dir}")


def load_animation_json(filepath: str) -> Dict:
    """Load animation data from JSON file."""
    with open(filepath, 'r') as f:
        return json.load(f)


if __name__ == '__main__':
    from .pn_dynamics import generate_sine_input
    import os

    # Generate test animation
    print("=== Generating Test Animation ===")

    signal = generate_sine_input(duration=3.0, frequency=0.3, amplitude=0.4, offset=0.5, dt=0.01)
    config = PNConfig(lambda_a=0.1, lambda_c=0.05, dt=0.01)

    # Sample every 10 steps = 100ms per frame
    frames = generate_animation_data(signal, config, sample_rate=10)

    print(f"Generated {len(frames)} frames")
    print(f"First frame: frame={frames[0]['frame']}, t={frames[0]['time']:.3f}")
    print(f"  a={frames[0]['a']:.4f}, b={frames[0]['b']:.4f}, c={frames[0]['c']:.4f}")
    print(f"  concurrence={frames[0]['concurrence']:.4f}")

    print(f"Last frame: frame={frames[-1]['frame']}, t={frames[-1]['time']:.3f}")
    print(f"  a={frames[-1]['a']:.4f}, b={frames[-1]['b']:.4f}, c={frames[-1]['c']:.4f}")
    print(f"  concurrence={frames[-1]['concurrence']:.4f}")

    # Export
    output_dir = os.path.join(os.path.dirname(__file__), '..', 'data', 'animations')
    os.makedirs(output_dir, exist_ok=True)

    metadata = {
        'description': 'Test animation with sine wave input',
        'lambda_a': config.lambda_a,
        'lambda_c': config.lambda_c,
        'dt': config.dt,
        'sample_rate': 10,
        'input_type': 'sine',
        'duration': 3.0,
        'frequency': 0.3
    }

    export_animation_json(frames, os.path.join(output_dir, 'test_animation.json'), metadata)

    # Export first 10 fractals
    fractal_dir = os.path.join(os.path.dirname(__file__), '..', 'data', 'fractals', 'test_sequence')
    export_fractal_sequence(frames[:10], fractal_dir, width=256, height=256)
