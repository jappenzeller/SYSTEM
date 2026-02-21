"""
Quantum state extraction for visualization.

Extracts:
- Bloch sphere coordinates (reduced density matrices)
- Entanglement (concurrence)
- State purity
"""

import numpy as np
from qiskit import QuantumCircuit
from qiskit.quantum_info import Statevector, DensityMatrix, partial_trace, concurrence
from typing import Dict, Tuple


def get_bloch_coords(rho_single: DensityMatrix) -> Tuple[float, float, float]:
    """
    Extract Bloch vector (x, y, z) from single-qubit density matrix.

    The Bloch vector components are:
        x = 2 * Re(rho_01)
        y = 2 * Im(rho_10)
        z = rho_00 - rho_11

    Returns:
        (x, y, z): Bloch vector coordinates
    """
    data = rho_single.data
    x = 2 * np.real(data[0, 1])
    y = 2 * np.imag(data[1, 0])
    z = np.real(data[0, 0] - data[1, 1])
    return (float(x), float(y), float(z))


def get_purity(rho: DensityMatrix) -> float:
    """Compute purity Tr(rho^2). Pure state = 1, maximally mixed = 0.5."""
    return float(np.real(np.trace(rho.data @ rho.data)))


def extract_visualization_data(circuit: QuantumCircuit) -> Dict:
    """
    Extract all visualization data from a quantum circuit.

    Returns:
        dict: {
            'statevector': complex array [4],
            'probabilities': float array [4],
            'bloch_E': (x, y, z) for excitatory qubit,
            'bloch_I': (x, y, z) for inhibitory qubit,
            'purity_E': float,
            'purity_I': float,
            'concurrence': float (entanglement measure),
            'global_phase': float
        }
    """
    # Get statevector
    sv = Statevector.from_instruction(circuit)
    amplitudes = sv.data

    # Full density matrix
    rho = DensityMatrix(sv)

    # Reduced density matrices (partial trace)
    rho_E = partial_trace(rho, [1])  # Trace out qubit 1 -> E (q0)
    rho_I = partial_trace(rho, [0])  # Trace out qubit 0 -> I (q1)

    # Extract Bloch coordinates
    bloch_E = get_bloch_coords(rho_E)
    bloch_I = get_bloch_coords(rho_I)

    # Compute entanglement
    ent = float(concurrence(sv))

    # Global phase from first non-zero amplitude
    nonzero_idx = np.argmax(np.abs(amplitudes) > 1e-10)
    global_phase = float(np.angle(amplitudes[nonzero_idx]))

    return {
        'statevector': [complex(a) for a in amplitudes],  # Keep as complex for internal use
        'statevector_list': [[float(a.real), float(a.imag)] for a in amplitudes],  # For JSON
        'probabilities': sv.probabilities().tolist(),
        'bloch_E': bloch_E,
        'bloch_I': bloch_I,
        'purity_E': get_purity(rho_E),
        'purity_I': get_purity(rho_I),
        'concurrence': ent,
        'global_phase': global_phase
    }


def bloch_to_spherical(bloch: Tuple[float, float, float]) -> Tuple[float, float, float]:
    """
    Convert Bloch (x, y, z) to spherical (r, theta, phi).

    Convention (matching SYSTEM project):
        - theta: polar angle from +Z axis [0, pi]
        - phi: azimuthal angle in XY plane [0, 2pi]
        - r: length of Bloch vector (1 for pure, <1 for mixed)

    Returns:
        (r, theta, phi)
    """
    x, y, z = bloch
    r = np.sqrt(x**2 + y**2 + z**2)

    if r < 1e-10:
        return (0.0, 0.0, 0.0)

    # Standard spherical coordinates
    theta = np.arccos(np.clip(z / r, -1, 1))
    phi = np.arctan2(y, x)
    if phi < 0:
        phi += 2 * np.pi

    return (float(r), float(theta), float(phi))


def spherical_to_bloch(r: float, theta: float, phi: float) -> Tuple[float, float, float]:
    """
    Convert spherical (r, theta, phi) to Bloch (x, y, z).

    Returns:
        (x, y, z): Bloch vector coordinates
    """
    x = r * np.sin(theta) * np.cos(phi)
    y = r * np.sin(theta) * np.sin(phi)
    z = r * np.cos(theta)
    return (float(x), float(y), float(z))


def bloch_to_unity_coords(bloch: Tuple[float, float, float]) -> Tuple[float, float, float]:
    """
    Convert standard Bloch coordinates to SYSTEM Unity coordinate system.

    SYSTEM uses +Y as north pole (|0>), standard Bloch uses +Z.

    Standard Bloch:
        +Z = |0>
        -Z = |1>
        +X = |+>
        -X = |->

    SYSTEM Unity:
        +Y = |0>
        -Y = |1>
        +X = |+>
        -X = |->

    Returns:
        (x, y, z) in Unity coordinate system
    """
    x, y, z = bloch
    # Rotate: standard Z -> Unity Y
    return (float(x), float(z), float(y))


def compute_von_neumann_entropy(rho: DensityMatrix) -> float:
    """
    Compute von Neumann entropy S = -Tr(rho * log(rho)).

    Returns:
        Entropy value >= 0. Pure state = 0, maximally mixed = log(2) for qubit.
    """
    eigenvalues = np.linalg.eigvalsh(rho.data)
    # Filter out zero/negative eigenvalues (numerical noise)
    eigenvalues = eigenvalues[eigenvalues > 1e-15]
    return float(-np.sum(eigenvalues * np.log2(eigenvalues)))


def compute_mutual_information(circuit: QuantumCircuit) -> float:
    """
    Compute quantum mutual information I(E:I) = S(E) + S(I) - S(EI).

    Returns:
        Mutual information value
    """
    sv = Statevector.from_instruction(circuit)
    rho = DensityMatrix(sv)
    rho_E = partial_trace(rho, [1])
    rho_I = partial_trace(rho, [0])

    S_E = compute_von_neumann_entropy(rho_E)
    S_I = compute_von_neumann_entropy(rho_I)
    S_EI = compute_von_neumann_entropy(rho)

    return S_E + S_I - S_EI


if __name__ == '__main__':
    from agate_circuit import create_agate_circuit

    # Test extraction
    circuit = create_agate_circuit(a=0.7, b=np.pi/2, c=0.4)
    viz = extract_visualization_data(circuit)

    print("=== Visualization Data ===")
    print(f"Bloch E: {viz['bloch_E']}")
    print(f"Bloch I: {viz['bloch_I']}")
    print(f"Purity E: {viz['purity_E']:.4f}")
    print(f"Purity I: {viz['purity_I']:.4f}")
    print(f"Concurrence: {viz['concurrence']:.4f}")
    print(f"Global phase: {viz['global_phase']:.4f}")

    # Test coordinate conversions
    sph = bloch_to_spherical(viz['bloch_E'])
    print(f"\nBloch E spherical (r, theta, phi): {sph}")

    unity = bloch_to_unity_coords(viz['bloch_E'])
    print(f"Bloch E Unity coords: {unity}")

    # Test mutual information
    mi = compute_mutual_information(circuit)
    print(f"\nMutual information I(E:I): {mi:.4f}")
