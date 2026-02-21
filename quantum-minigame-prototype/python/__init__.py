"""
Quantum Minigame Prototype - Python Core

Visualization pipeline for the A Gate quantum circuit.
"""

from .agate_circuit import create_agate_circuit, get_statevector, get_probabilities, compute_fidelity
from .pn_dynamics import PNDynamics, PNConfig, generate_sine_input, generate_pulse_input, generate_noise_input
from .quantum_state import extract_visualization_data, get_bloch_coords, bloch_to_spherical
from .fractal_generator import generate_fractal_from_state, julia_set, fractal_to_image, save_fractal
from .animation_exporter import generate_animation_data, export_animation_json, export_fractal_sequence

__version__ = "0.1.0"
__all__ = [
    'create_agate_circuit',
    'get_statevector',
    'get_probabilities',
    'compute_fidelity',
    'PNDynamics',
    'PNConfig',
    'generate_sine_input',
    'generate_pulse_input',
    'generate_noise_input',
    'extract_visualization_data',
    'get_bloch_coords',
    'bloch_to_spherical',
    'generate_fractal_from_state',
    'julia_set',
    'fractal_to_image',
    'save_fractal',
    'generate_animation_data',
    'export_animation_json',
    'export_fractal_sequence',
]
