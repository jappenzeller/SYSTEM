"""
Test the complete pipeline from input signal to animation export.

Run with: python -m python.test_pipeline
"""

import numpy as np
import sys
import os
from pathlib import Path

# Add parent to path for imports when running directly
sys.path.insert(0, str(Path(__file__).parent.parent))

from python.agate_circuit import create_agate_circuit, get_statevector, get_probabilities, get_entanglement
from python.pn_dynamics import PNDynamics, PNConfig, generate_sine_input, generate_pulse_input
from python.quantum_state import extract_visualization_data, bloch_to_spherical, bloch_to_unity_coords
from python.fractal_generator import generate_fractal_from_state, save_fractal, statevector_to_julia_param
from python.animation_exporter import generate_animation_data, export_animation_json, export_fractal_sequence


def test_basic_circuit():
    """Test A Gate circuit creation."""
    print("=" * 50)
    print("Testing A Gate Circuit")
    print("=" * 50)

    circuit = create_agate_circuit(a=0.5, b=1.0, c=0.3)
    print(f"\nCircuit created: {circuit.name} with {circuit.num_qubits} qubits, {circuit.depth()} depth")

    sv = get_statevector(circuit)
    probs = get_probabilities(circuit)
    ent = get_entanglement(circuit)

    print(f"\nStatevector: {sv}")
    print(f"Probabilities: {probs}")
    print(f"Entanglement (concurrence): {ent:.4f}")

    # Test parameter extremes
    print("\nParameter extremes:")
    for a, b, c in [(0, 0, 0), (1, 0, 0), (0, np.pi, 0), (0, 0, 1), (1, np.pi, 1)]:
        circuit = create_agate_circuit(a=a, b=b, c=c)
        probs = get_probabilities(circuit)
        ent = get_entanglement(circuit)
        print(f"  a={a:.1f}, b={b:.2f}, c={c:.1f} -> P(00)={probs[0]:.3f}, Concurrence={ent:.3f}")

    print("\n[OK] Basic circuit tests passed")


def test_dynamics():
    """Test PN dynamics evolution."""
    print("\n" + "=" * 50)
    print("Testing PN Dynamics")
    print("=" * 50)

    config = PNConfig(lambda_a=0.1, lambda_c=0.05, dt=0.01)
    pn = PNDynamics(config)

    # Generate test input
    signal = generate_sine_input(duration=5.0, frequency=0.5, dt=0.01)

    # Evolve with history
    history = pn.evolve_with_history(signal)

    print(f"\nSignal duration: {history['t'][-1]:.2f}s, {len(history['t'])} samples")
    print(f"Final state: a={history['a'][-1]:.4f}, b={history['b'][-1]:.4f}, c={history['c'][-1]:.4f}")
    print(f"Max values: a_max={history['a'].max():.4f}, b_max={history['b'].max():.4f}, c_max={history['c'].max():.4f}")

    # Test different inputs
    print("\nDifferent input types:")

    # Pulse input
    pn.reset()
    pulse = generate_pulse_input(duration=2.0, pulse_start=0.5, pulse_width=0.5, dt=0.01)
    a, b, c = pn.evolve(pulse)
    print(f"  Pulse input -> a={a:.4f}, b={b:.4f}, c={c:.4f}")

    # Step response
    pn.reset()
    step = np.ones(1000) * 0.5
    a, b, c = pn.evolve(step)
    print(f"  Constant 0.5 -> a={a:.4f}, b={b:.4f}, c={c:.4f}")

    print("\n[OK] Dynamics tests passed")


def test_visualization_data():
    """Test quantum state extraction."""
    print("\n" + "=" * 50)
    print("Testing Visualization Data Extraction")
    print("=" * 50)

    circuit = create_agate_circuit(a=0.7, b=np.pi/2, c=0.4)
    viz = extract_visualization_data(circuit)

    print(f"\nBloch vectors:")
    print(f"  E (q0): {viz['bloch_E']}")
    print(f"  I (q1): {viz['bloch_I']}")

    print(f"\nBloch lengths (purity indicators):")
    print(f"  |E|: {np.linalg.norm(viz['bloch_E']):.4f}")
    print(f"  |I|: {np.linalg.norm(viz['bloch_I']):.4f}")

    print(f"\nPurity values:")
    print(f"  Purity E: {viz['purity_E']:.4f}")
    print(f"  Purity I: {viz['purity_I']:.4f}")

    print(f"\nEntanglement: {viz['concurrence']:.4f}")
    print(f"Global phase: {viz['global_phase']:.4f}")

    # Test coordinate conversions
    sph = bloch_to_spherical(viz['bloch_E'])
    unity = bloch_to_unity_coords(viz['bloch_E'])

    print(f"\nCoordinate conversions for E:")
    print(f"  Spherical (r, theta, phi): ({sph[0]:.4f}, {sph[1]:.4f}, {sph[2]:.4f})")
    print(f"  Unity (x, y, z): {unity}")

    print("\n[OK] Visualization data tests passed")


def test_fractal():
    """Test fractal generation."""
    print("\n" + "=" * 50)
    print("Testing Fractal Generation")
    print("=" * 50)

    circuit = create_agate_circuit(a=0.6, b=1.2, c=0.5)
    sv = get_statevector(circuit)

    print(f"\nStatevector: {sv}")

    # Test different Julia parameter methods
    print("\nJulia parameter methods:")
    for method in ['dominant', 'weighted', 'determinant', 'interference']:
        c = statevector_to_julia_param(sv, method=method)
        print(f"  {method}: c = {c:.4f}")

    # Generate fractal
    fractal = generate_fractal_from_state(sv, width=256, height=256, max_iter=100)
    print(f"\nFractal shape: {fractal.shape}")
    print(f"Fractal range: [{fractal.min():.2f}, {fractal.max():.2f}]")

    # Save test fractal
    output_dir = Path(__file__).parent.parent / "data" / "fractals"
    output_dir.mkdir(parents=True, exist_ok=True)

    output_path = output_dir / "test_fractal.png"
    save_fractal(fractal, str(output_path))
    print(f"\nSaved to: {output_path}")

    print("\n[OK] Fractal tests passed")


def test_full_pipeline():
    """Test complete animation export."""
    print("\n" + "=" * 50)
    print("Testing Full Animation Pipeline")
    print("=" * 50)

    # Generate input signal
    signal = generate_sine_input(duration=3.0, frequency=0.3, amplitude=0.4, offset=0.5, dt=0.01)

    # Generate animation data (sample every 10 steps = 100ms per frame at dt=0.01)
    config = PNConfig(lambda_a=0.1, lambda_c=0.05, dt=0.01)
    frames = generate_animation_data(signal, config, sample_rate=10)

    print(f"\nGenerated {len(frames)} frames")
    print(f"\nFirst frame (frame {frames[0]['frame']}, t={frames[0]['time']:.3f}s):")
    print(f"  Parameters: a={frames[0]['a']:.4f}, b={frames[0]['b']:.4f}, c={frames[0]['c']:.4f}")
    print(f"  Bloch E: {frames[0]['bloch_E']}")
    print(f"  Concurrence: {frames[0]['concurrence']:.4f}")

    print(f"\nLast frame (frame {frames[-1]['frame']}, t={frames[-1]['time']:.3f}s):")
    print(f"  Parameters: a={frames[-1]['a']:.4f}, b={frames[-1]['b']:.4f}, c={frames[-1]['c']:.4f}")
    print(f"  Bloch E: {frames[-1]['bloch_E']}")
    print(f"  Concurrence: {frames[-1]['concurrence']:.4f}")

    # Export JSON
    output_dir = Path(__file__).parent.parent / "data" / "animations"
    output_dir.mkdir(parents=True, exist_ok=True)

    json_path = output_dir / "test_animation.json"

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

    export_animation_json(frames, str(json_path), metadata)

    # Export fractal sequence (first 10 frames only for speed)
    fractal_dir = Path(__file__).parent.parent / "data" / "fractals" / "test_sequence"
    print(f"\nExporting first 10 fractals...")
    export_fractal_sequence(frames[:10], str(fractal_dir), width=256, height=256)

    print("\n[OK] Full pipeline tests passed")


def test_parameter_sweep():
    """Test parameter sweep animation."""
    print("\n" + "=" * 50)
    print("Testing Parameter Sweep")
    print("=" * 50)

    from python.animation_exporter import generate_parameter_sweep

    # Sweep 'a' parameter
    a_values = np.linspace(0, 1, 50)
    frames = generate_parameter_sweep('a', a_values, {'b': np.pi/2, 'c': 0.3})

    print(f"\nGenerated {len(frames)} frames for 'a' sweep")

    # Check entanglement variation
    concurrences = [f['concurrence'] for f in frames]
    print(f"Concurrence range: [{min(concurrences):.4f}, {max(concurrences):.4f}]")

    # Export
    output_path = Path(__file__).parent.parent / "data" / "animations" / "a_sweep.json"
    export_animation_json(frames, str(output_path), {'sweep': 'a', 'fixed': {'b': np.pi/2, 'c': 0.3}})

    print("\n[OK] Parameter sweep tests passed")


def run_all_tests():
    """Run all tests."""
    print("\n" + "=" * 60)
    print(" QUANTUM MINIGAME PROTOTYPE - TEST SUITE")
    print("=" * 60)

    try:
        test_basic_circuit()
        test_dynamics()
        test_visualization_data()
        test_fractal()
        test_full_pipeline()
        test_parameter_sweep()

        print("\n" + "=" * 60)
        print(" ALL TESTS PASSED")
        print("=" * 60)
        print("\nOutput files created in:")
        print(f"  - {Path(__file__).parent.parent / 'data' / 'animations'}")
        print(f"  - {Path(__file__).parent.parent / 'data' / 'fractals'}")
        print("\nNext steps:")
        print("  1. Run interactive preview: python -m python.interactive_preview")
        print("  2. Open Blender and run blender/scripts/bloch_sphere_setup.py")
        print("  3. Import animation with blender/scripts/import_quantum_data.py")

    except Exception as e:
        print(f"\n[FAIL] Test failed with error: {e}")
        import traceback
        traceback.print_exc()
        return 1

    return 0


if __name__ == '__main__':
    exit(run_all_tests())
