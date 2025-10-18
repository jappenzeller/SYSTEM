"""Quick test without Unicode characters to verify basic functionality"""

import numpy as np
import sys
from pathlib import Path

# Add Scripts directory to path
script_dir = Path(__file__).parent / "Scripts"
sys.path.insert(0, str(script_dir))

print("Testing import...")
from Quantum.hydrogen_wavefunctions import hydrogen_orbital
from Quantum.bloch_orbital_mapper import BlochOrbitalState, create_state
print("OK - Imports successful\n")

print("=" * 70)
print("TEST 1: 1s Orbital Spherical Symmetry")
print("=" * 70)

r = 5.0
points = [(r, 0, 0), (0, r, 0), (0, 0, r), (-r, 0, 0), (0, -r, 0), (0, 0, -r)]
values = []

for x, y, z in points:
    psi = hydrogen_orbital(1, 0, 0, x, y, z)
    values.append(psi)
    print(f"  psi_1s({x:+6.1f}, {y:+6.1f}, {z:+6.1f}) = {psi:+.6f}")

values = np.array(values)
max_diff = np.max(np.abs(values - values[0]))
print(f"\n  Maximum difference: {max_diff:.2e}")
test1_pass = max_diff < 1e-10
print(f"  PASSED: {test1_pass}\n")

print("=" * 70)
print("TEST 2: 2px Orbital Shape")
print("=" * 70)

r = 10.0
points = {'+x': (r, 0, 0), '-x': (-r, 0, 0), '+y': (0, r, 0), '+z': (0, 0, r)}

print("  2px orbital (real form, m=1):")
values_2px = {}
for label, (x, y, z) in points.items():
    psi = hydrogen_orbital(2, 1, 1, x, y, z, real_form=True)
    values_2px[label] = psi
    print(f"    {label:3s}: psi_2px({x:+6.1f}, {y:+6.1f}, {z:+6.1f}) = {psi:+.6f}")

x_amp = abs(values_2px['+x'])
y_amp = abs(values_2px['+y'])
z_amp = abs(values_2px['+z'])
test2_pass = (x_amp > y_amp) and (x_amp > z_amp)
print(f"\n  x-axis dominant: {test2_pass}")
print(f"  PASSED: {test2_pass}\n")

print("=" * 70)
print("TEST 3: Bloch Superposition")
print("=" * 70)

theta = np.pi / 2
phi = 0.0
state = BlochOrbitalState(theta, phi, basis='sp')

print(f"  Bloch angles: theta={theta:.4f}, phi={phi:.4f}")
print(f"  State vector: {state.state_vector}")

alpha, beta = state.state_vector
expected = 1.0 / np.sqrt(2)

print(f"\n  |alpha| = {np.abs(alpha):.6f} (expected: {expected:.6f})")
print(f"  |beta|  = {np.abs(beta):.6f} (expected: {expected:.6f})")

probs = state.get_orbital_probabilities()
prob_1s = probs.get((1, 0, 0), 0)
prob_2s = probs.get((2, 0, 0), 0)

print(f"\n  P(1s) = {prob_1s:.6f}")
print(f"  P(2s) = {prob_2s:.6f}")

test3_pass = (np.isclose(prob_1s, prob_2s, atol=1e-10) and
              np.isclose(prob_1s, 0.5, atol=1e-10))
print(f"  PASSED: {test3_pass}\n")

print("=" * 70)
print("TEST 4: Quantum Gates")
print("=" * 70)

state = create_state('|0>')
print(f"  Initial: |0> at theta={state.theta:.4f}, phi={state.phi:.4f}")

state.apply_gate('H')
print(f"  After H: theta={state.theta:.4f}, phi={state.phi:.4f}")
h_correct = np.isclose(state.theta, np.pi/2, atol=1e-6)

state.apply_gate('X')
state.apply_gate('H')
print(f"  After X,H: theta={state.theta:.4f}, phi={state.phi:.4f}")
final_correct = np.isclose(state.theta, np.pi, atol=1e-6)

test4_pass = h_correct and final_correct
print(f"  PASSED: {test4_pass}\n")

print("=" * 70)
print("SUMMARY")
print("=" * 70)

all_pass = test1_pass and test2_pass and test3_pass and test4_pass

tests = [
    ("Spherical Symmetry", test1_pass),
    ("2px Orbital Shape", test2_pass),
    ("Bloch Superposition", test3_pass),
    ("Quantum Gates", test4_pass),
]

for name, passed in tests:
    status = "PASS" if passed else "FAIL"
    print(f"  {name:30s} {status}")

passed_count = sum(1 for _, p in tests if p)
print(f"\n  Total: {passed_count}/{len(tests)} tests passed")

if all_pass:
    print("\n  ALL TESTS PASSED!")
else:
    print(f"\n  {len(tests) - passed_count} test(s) failed")

sys.exit(0 if all_pass else 1)
