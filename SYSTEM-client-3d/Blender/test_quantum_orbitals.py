"""
Test Script for Quantum Orbital Visualization Framework

Verifies:
1. 1s orbital is spherically symmetric
2. 2px orbital has correct shape along x-axis
3. Bloch state (θ=π/2, φ=0) produces equal mix of |0⟩ and |1⟩
4. Probability density integrates to 1
"""

import numpy as np
import sys
from pathlib import Path

# Add Scripts directory to path
script_dir = Path(__file__).parent / "Scripts"
sys.path.insert(0, str(script_dir))

from Quantum.hydrogen_wavefunctions import (
    hydrogen_orbital,
    probability_density,
    verify_normalization,
    verify_orthogonality,
    radial_wavefunction,
)
from Quantum.bloch_orbital_mapper import (
    BlochOrbitalState,
    create_state,
    create_superposition,
)
from Quantum.orbital_coefficients import bloch_to_orbital_coeffs
from Quantum.quantum_constants import validate_quantum_numbers

# ============================================================================
# Test Functions
# ============================================================================

def test_1s_spherical_symmetry():
    """Test that 1s orbital is spherically symmetric."""
    print("\n" + "=" * 70)
    print("TEST 1: 1s Orbital Spherical Symmetry")
    print("=" * 70)

    # Calculate 1s at same radius but different angles
    r = 5.0  # 5 Bohr radii

    # Points at same radius
    points = [
        (r, 0, 0),    # +x axis
        (0, r, 0),    # +y axis
        (0, 0, r),    # +z axis
        (-r, 0, 0),   # -x axis
        (0, -r, 0),   # -y axis
        (0, 0, -r),   # -z axis
    ]

    values = []
    for x, y, z in points:
        psi = hydrogen_orbital(1, 0, 0, x, y, z)
        values.append(psi)
        print(f"  ψ₁ₛ({x:+6.1f}, {y:+6.1f}, {z:+6.1f}) = {psi:+.6f}")

    # Check all values are equal (within numerical precision)
    values = np.array(values)
    max_diff = np.max(np.abs(values - values[0]))

    print(f"\n  Maximum difference: {max_diff:.2e}")
    print(f"  All equal (tolerance 1e-10): {max_diff < 1e-10}")

    if max_diff < 1e-10:
        print("  ✓ PASSED: 1s orbital is spherically symmetric")
        return True
    else:
        print("  ✗ FAILED: 1s orbital not symmetric")
        return False

def test_2px_orbital_shape():
    """Test that 2px orbital has correct shape along axes."""
    print("\n" + "=" * 70)
    print("TEST 2: 2px Orbital Shape")
    print("=" * 70)

    r = 10.0  # 10 Bohr radii

    # 2px should have lobes along x-axis
    points = {
        '+x': (r, 0, 0),
        '-x': (-r, 0, 0),
        '+y': (0, r, 0),
        '-y': (0, -r, 0),
        '+z': (0, 0, r),
        '-z': (0, 0, -r),
    }

    print("  2px orbital (real form, m=1):")
    values = {}
    for label, (x, y, z) in points.items():
        psi = hydrogen_orbital(2, 1, 1, x, y, z, real_form=True)
        values[label] = psi
        print(f"    {label:3s}: ψ₂ₚₓ({x:+6.1f}, {y:+6.1f}, {z:+6.1f}) = {psi:+.6f}")

    # Check expectations:
    # - Values along x should be significant
    # - Values along y, z should be smaller (nodes on y-z plane)
    x_amplitude = abs(values['+x'])
    y_amplitude = abs(values['+y'])
    z_amplitude = abs(values['+z'])

    print(f"\n  |ψ| along x-axis: {x_amplitude:.6f}")
    print(f"  |ψ| along y-axis: {y_amplitude:.6f}")
    print(f"  |ψ| along z-axis: {z_amplitude:.6f}")

    # x should be larger than y and z
    x_dominant = (x_amplitude > y_amplitude) and (x_amplitude > z_amplitude)

    print(f"\n  x-axis dominant: {x_dominant}")

    if x_dominant:
        print("  ✓ PASSED: 2px orbital has correct directional properties")
        return True
    else:
        print("  ✗ FAILED: 2px orbital shape incorrect")
        return False

def test_bloch_superposition():
    """Test that Bloch state (θ=π/2, φ=0) produces |+⟩ = (|0⟩ + |1⟩)/√2."""
    print("\n" + "=" * 70)
    print("TEST 3: Bloch Sphere Superposition")
    print("=" * 70)

    # Create |+⟩ state: θ=π/2, φ=0
    theta = np.pi / 2
    phi = 0.0

    state = BlochOrbitalState(theta, phi, basis='sp')

    print(f"  Bloch angles: θ={theta:.4f}, φ={phi:.4f}")
    print(f"  Expected: |+⟩ = (|0⟩ + |1⟩)/√2")
    print(f"\n  State vector: {state.state_vector}")

    alpha, beta = state.state_vector
    expected_alpha = 1.0 / np.sqrt(2)
    expected_beta = 1.0 / np.sqrt(2)

    print(f"\n  α = {alpha:.6f} (expected: {expected_alpha:.6f})")
    print(f"  β = {beta:.6f} (expected: {expected_beta:.6f})")

    # Check coefficients
    alpha_close = np.isclose(np.abs(alpha), expected_alpha, atol=1e-10)
    beta_close = np.isclose(np.abs(beta), expected_beta, atol=1e-10)

    print(f"\n  Orbital coefficients:")
    for nlm, coeff in state.orbital_coeffs.items():
        n, l, m = nlm
        prob = np.abs(coeff) ** 2
        print(f"    ({n},{l},{m}): {coeff:.6f}, P = {prob:.6f}")

    # Check equal probabilities
    probs = state.get_orbital_probabilities()
    prob_1s = probs.get((1, 0, 0), 0)
    prob_2s = probs.get((2, 0, 0), 0)

    probs_equal = np.isclose(prob_1s, prob_2s, atol=1e-10)
    probs_half = np.isclose(prob_1s, 0.5, atol=1e-10)

    print(f"\n  P(1s) = {prob_1s:.6f}")
    print(f"  P(2s) = {prob_2s:.6f}")
    print(f"  Probabilities equal: {probs_equal}")
    print(f"  Each probability ≈ 0.5: {probs_half}")

    if alpha_close and beta_close and probs_equal and probs_half:
        print("  ✓ PASSED: Superposition state correct")
        return True
    else:
        print("  ✗ FAILED: Superposition state incorrect")
        return False

def test_normalization():
    """Test that probability density integrates to 1."""
    print("\n" + "=" * 70)
    print("TEST 4: Wave Function Normalization")
    print("=" * 70)

    orbitals_to_test = [
        (1, 0, 0),  # 1s
        (2, 0, 0),  # 2s
        (2, 1, 0),  # 2pz
    ]

    all_passed = True

    for n, l, m in orbitals_to_test:
        print(f"\n  Testing normalization of {n}{['s','p','d','f'][l]}")

        # Verify normalization (this is slow - uses numerical integration)
        print(f"    Computing ∫|ψ|²dV (this may take a moment)...")

        try:
            integral = verify_normalization(n, l, m, r_max=20*n, num_points=30)
            print(f"    ∫|ψ_{n}{l}{m}|²dV = {integral:.6f}")

            is_normalized = np.isclose(integral, 1.0, atol=0.01)  # 1% tolerance
            print(f"    Normalized (within 1%): {is_normalized}")

            if not is_normalized:
                all_passed = False
                print(f"    ✗ FAILED")
            else:
                print(f"    ✓ PASSED")

        except Exception as e:
            print(f"    ✗ ERROR: {e}")
            all_passed = False

    if all_passed:
        print("\n  ✓ PASSED: All tested orbitals are normalized")
        return True
    else:
        print("\n  ✗ FAILED: Some orbitals not normalized")
        return False

def test_orthogonality():
    """Test orthogonality between different states."""
    print("\n" + "=" * 70)
    print("TEST 5: Orbital Orthogonality")
    print("=" * 70)

    pairs = [
        ((1, 0, 0), (2, 0, 0)),  # 1s and 2s
        ((2, 1, 0), (2, 1, 1)),  # 2pz and 2px
    ]

    all_passed = True

    for state1, state2 in pairs:
        n1, l1, m1 = state1
        n2, l2, m2 = state2

        print(f"\n  Testing <ψ_{n1}{l1}{m1}|ψ_{n2}{l2}{m2}>")
        print(f"    Computing overlap integral (this may take a moment)...")

        try:
            overlap = verify_orthogonality(n1, l1, m1, n2, l2, m2, r_max=20)
            print(f"    Overlap = {overlap:.6f}")

            is_orthogonal = np.abs(overlap) < 0.01  # 1% tolerance
            print(f"    Orthogonal (within 1%): {is_orthogonal}")

            if not is_orthogonal:
                all_passed = False
                print(f"    ✗ FAILED")
            else:
                print(f"    ✓ PASSED")

        except Exception as e:
            print(f"    ✗ ERROR: {e}")
            all_passed = False

    if all_passed:
        print("\n  ✓ PASSED: States are orthogonal")
        return True
    else:
        print("\n  ✗ FAILED: Some states not orthogonal")
        return False

def test_quantum_gates():
    """Test quantum gate operations."""
    print("\n" + "=" * 70)
    print("TEST 6: Quantum Gate Operations")
    print("=" * 70)

    # Start with |0⟩
    state = create_state('|0⟩')
    print(f"  Initial state: |0⟩")
    print(f"  θ={state.theta:.4f}, φ={state.phi:.4f}")

    # Apply Hadamard gate: H|0⟩ = |+⟩
    state.apply_gate('H')
    print(f"\n  After H gate:")
    print(f"  θ={state.theta:.4f}, φ={state.phi:.4f}")
    print(f"  State vector: {state.state_vector}")

    # Should be at θ=π/2, φ=0
    h_correct = np.isclose(state.theta, np.pi/2, atol=1e-6) and np.isclose(state.phi, 0.0, atol=1e-6)
    print(f"  Correct |+⟩ state: {h_correct}")

    # Apply Pauli X: X|+⟩ = |+⟩ (eigenstate)
    initial_theta = state.theta
    state.apply_gate('X')
    print(f"\n  After X gate:")
    print(f"  θ={state.theta:.4f}, φ={state.phi:.4f}")

    # Apply another H: should return to |1⟩
    state.apply_gate('H')
    print(f"\n  After second H gate:")
    print(f"  θ={state.theta:.4f}, φ={state.phi:.4f}")

    # Should be close to |1⟩ (θ=π)
    final_correct = np.isclose(state.theta, np.pi, atol=1e-6)
    print(f"  Correct |1⟩ state: {final_correct}")

    if h_correct and final_correct:
        print("\n  ✓ PASSED: Gate operations work correctly")
        return True
    else:
        print("\n  ✗ FAILED: Gate operations incorrect")
        return False

# ============================================================================
# Main Test Runner
# ============================================================================

def run_all_tests():
    """Run all tests and report results."""
    print("\n" + "=" * 70)
    print("QUANTUM ORBITAL VISUALIZATION FRAMEWORK - TEST SUITE")
    print("=" * 70)

    tests = [
        ("Spherical Symmetry", test_1s_spherical_symmetry),
        ("2px Orbital Shape", test_2px_orbital_shape),
        ("Bloch Superposition", test_bloch_superposition),
        ("Quantum Gates", test_quantum_gates),
    ]

    # Optional slow tests
    import os
    run_slow_tests = os.getenv('RUN_SLOW_TESTS', 'false').lower() == 'true'

    if not run_slow_tests:
        print("\nSkipping slow integration tests (set RUN_SLOW_TESTS=true to enable)")

    if run_slow_tests:
        tests.extend([
            ("Normalization", test_normalization),
            ("Orthogonality", test_orthogonality),
        ])

    results = []

    for name, test_func in tests:
        try:
            passed = test_func()
            results.append((name, passed))
        except Exception as e:
            print(f"\n  ✗ ERROR in {name}: {e}")
            import traceback
            traceback.print_exc()
            results.append((name, False))

    # Summary
    print("\n" + "=" * 70)
    print("TEST SUMMARY")
    print("=" * 70)

    passed_count = sum(1 for _, passed in results if passed)
    total_count = len(results)

    for name, passed in results:
        status = "✓ PASSED" if passed else "✗ FAILED"
        print(f"  {name:30s} {status}")

    print(f"\n  Total: {passed_count}/{total_count} tests passed")

    if passed_count == total_count:
        print("\n  🎉 ALL TESTS PASSED!")
    else:
        print(f"\n  ⚠ {total_count - passed_count} test(s) failed")

    return passed_count == total_count

if __name__ == "__main__":
    success = run_all_tests()
    sys.exit(0 if success else 1)
