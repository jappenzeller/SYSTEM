"""
A Gate Quantum Circuit for PN Neuron Encoding.

Circuit structure (2 qubits, 14 gates, depth 7):
    q0 (E): -[H]-[P(b)]-[Rx(2a)]-[P(b)]-[H]-----o-----[Rz(pi/4)]-
                                                 |
    q1 (I): -[H]-[P(b)]-[Ry(2c)]-[P(b)]-[H]-[Ry(pi/4)]----------
"""

from qiskit import QuantumCircuit
from qiskit.quantum_info import Statevector, DensityMatrix, partial_trace, concurrence
import numpy as np


def create_agate_circuit(a: float, b: float, c: float) -> QuantumCircuit:
    """
    Create the A Gate quantum circuit.

    Parameters:
        a: Excitatory amplitude [0, 1]
        b: Shared phase [0, 2pi]
        c: Inhibitory amplitude [0, 1]

    Returns:
        QuantumCircuit: 2-qubit A Gate circuit
    """
    qc = QuantumCircuit(2, name='A-Gate')

    # === Layer 1: Excitatory path (qubit 0) ===
    qc.h(0)
    qc.p(b, 0)
    qc.rx(2 * a, 0)
    qc.p(b, 0)
    qc.h(0)

    # === Layer 1: Inhibitory path (qubit 1) ===
    qc.h(1)
    qc.p(b, 1)
    qc.ry(2 * c, 1)
    qc.p(b, 1)
    qc.h(1)

    # === Layer 2: E-I Coupling ===
    qc.cry(np.pi / 4, 0, 1)  # E controls I
    qc.crz(np.pi / 4, 1, 0)  # I controls E

    return qc


def get_statevector(circuit: QuantumCircuit) -> np.ndarray:
    """Get the 4 complex amplitudes [alpha_00, alpha_01, alpha_10, alpha_11]."""
    sv = Statevector.from_instruction(circuit)
    return sv.data


def get_probabilities(circuit: QuantumCircuit) -> np.ndarray:
    """Get measurement probabilities for each basis state."""
    sv = Statevector.from_instruction(circuit)
    return sv.probabilities()


def compute_fidelity(circuit1: QuantumCircuit, circuit2: QuantumCircuit) -> float:
    """Compute quantum fidelity F = |<psi1|psi2>|^2."""
    sv1 = Statevector.from_instruction(circuit1)
    sv2 = Statevector.from_instruction(circuit2)
    return float(abs(sv1.inner(sv2)) ** 2)


def get_density_matrix(circuit: QuantumCircuit) -> DensityMatrix:
    """Get the full 2-qubit density matrix."""
    sv = Statevector.from_instruction(circuit)
    return DensityMatrix(sv)


def get_reduced_density_matrix(circuit: QuantumCircuit, qubit: int) -> DensityMatrix:
    """
    Get reduced density matrix for a single qubit.

    Args:
        circuit: Quantum circuit
        qubit: Which qubit to keep (0 or 1)

    Returns:
        Single-qubit density matrix
    """
    sv = Statevector.from_instruction(circuit)
    rho = DensityMatrix(sv)
    # Trace out the other qubit
    trace_out = 1 if qubit == 0 else 0
    return partial_trace(rho, [trace_out])


def get_entanglement(circuit: QuantumCircuit) -> float:
    """
    Compute entanglement (concurrence) of the 2-qubit state.

    Returns:
        Concurrence value in [0, 1]. 0 = separable, 1 = maximally entangled
    """
    sv = Statevector.from_instruction(circuit)
    return float(concurrence(sv))


if __name__ == '__main__':
    # Quick test
    circuit = create_agate_circuit(a=0.5, b=1.0, c=0.3)
    print("A Gate Circuit:")
    print(circuit)
    print(f"\nStatevector: {get_statevector(circuit)}")
    print(f"Probabilities: {get_probabilities(circuit)}")
    print(f"Entanglement: {get_entanglement(circuit):.4f}")
