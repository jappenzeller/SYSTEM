"""
Orbital Coefficients and Bloch Sphere Mapping

Maps Bloch sphere quantum states to superpositions of hydrogen orbitals,
providing coefficients for orbital mixing and state transformations.
"""

import numpy as np
from .quantum_constants import (
    BLOCH_PURE_STATES,
    BLOCH_EQUATORIAL_STATES,
    QUANTUM_GATES,
    rotation_x,
    rotation_y,
    rotation_z,
)

# ============================================================================
# Bloch Sphere to State Vector Conversion
# ============================================================================

def bloch_to_state_vector(theta, phi):
    """
    Convert Bloch sphere coordinates to quantum state vector.

    Bloch sphere parameterization:
    |ψ⟩ = cos(θ/2)|0⟩ + e^(iφ) sin(θ/2)|1⟩

    Args:
        theta: Polar angle (0 to π)
        phi: Azimuthal angle (0 to 2π)

    Returns:
        np.array: Complex state vector [α, β] where |ψ⟩ = α|0⟩ + β|1⟩
    """
    alpha = np.cos(theta / 2)
    beta = np.exp(1j * phi) * np.sin(theta / 2)

    return np.array([alpha, beta], dtype=complex)

def state_vector_to_bloch(state_vector):
    """
    Convert quantum state vector to Bloch sphere coordinates.

    Args:
        state_vector: Complex array [α, β] where |ψ⟩ = α|0⟩ + β|1⟩

    Returns:
        tuple: (theta, phi) Bloch sphere angles
    """
    alpha, beta = state_vector

    # Normalize
    norm = np.sqrt(np.abs(alpha) ** 2 + np.abs(beta) ** 2)
    alpha /= norm
    beta /= norm

    # Extract angles
    theta = 2 * np.arccos(np.abs(alpha))
    phi = np.angle(beta) - np.angle(alpha)

    # Ensure phi in [0, 2π]
    phi = phi % (2 * np.pi)

    return theta, phi

# ============================================================================
# Orbital Coefficient Mapping
# ============================================================================

def bloch_to_orbital_coeffs(theta, phi, basis='sp'):
    """
    Convert Bloch sphere angles to orbital mixture coefficients.

    Maps quantum states to orbital superpositions:
    - |0⟩ → 1s orbital
    - |1⟩ → 2s orbital (for 's' basis) or 2p orbital (for 'p' basis)
    - Superpositions → Linear combinations

    Args:
        theta: Polar angle on Bloch sphere (0 to π)
        phi: Azimuthal angle on Bloch sphere (0 to 2π)
        basis: Orbital basis to use ('sp', 'pp', 'sd')
            'sp': 1s and 2s orbitals
            'pp': 2px, 2py, 2pz orbitals
            'sd': 1s and 3d orbitals

    Returns:
        dict: Orbital coefficients with quantum numbers as keys
            e.g., {(1,0,0): α, (2,0,0): β} for 'sp' basis
    """
    # Get state vector
    state_vec = bloch_to_state_vector(theta, phi)
    alpha, beta = state_vec

    if basis == 'sp':
        # |0⟩ → 1s, |1⟩ → 2s
        return {
            (1, 0, 0): alpha,  # 1s
            (2, 0, 0): beta,   # 2s
        }

    elif basis == 'pp':
        # Map to p orbitals using equatorial plane
        # |0⟩ → 2pz (pointing up)
        # Equator → 2px, 2py combinations
        # |1⟩ → -2pz (pointing down)

        # For p orbitals, we use real combinations
        cos_half = np.cos(theta / 2)
        sin_half = np.sin(theta / 2)

        # Components
        c_px = sin_half * np.cos(phi)  # x component
        c_py = sin_half * np.sin(phi)  # y component
        c_pz = cos_half                # z component

        return {
            (2, 1, 1): c_px,   # 2px (real form, m=1)
            (2, 1, -1): c_py,  # 2py (real form, m=-1)
            (2, 1, 0): c_pz,   # 2pz (m=0)
        }

    elif basis == 'sd':
        # |0⟩ → 1s, |1⟩ → 3dz²
        return {
            (1, 0, 0): alpha,  # 1s
            (3, 2, 0): beta,   # 3dz²
        }

    else:
        raise ValueError(f"Unknown basis: {basis}. Use 'sp', 'pp', or 'sd'")

def orbital_coeffs_to_density(coeffs, x, y, z):
    """
    Calculate probability density from orbital coefficient mixture.

    Computes |ψ_total|² = |Σ c_i ψ_i|²

    Args:
        coeffs: Dict mapping (n,l,m) tuples to complex coefficients
        x, y, z: Spatial coordinates (in Bohr radii, can be arrays)

    Returns:
        np.array: Probability density at given points
    """
    from .hydrogen_wavefunctions import hydrogen_orbital

    # Sum all orbital contributions
    psi_total = 0.0

    for (n, l, m), coeff in coeffs.items():
        psi_i = hydrogen_orbital(n, l, m, x, y, z, real_form=True)
        psi_total += coeff * psi_i

    # Probability density
    density = np.abs(psi_total) ** 2

    return density

# ============================================================================
# Pure State Mappings
# ============================================================================

def get_pure_state_orbital(state_name):
    """
    Get orbital quantum numbers for a named pure state.

    Args:
        state_name: Name of pure state ('|0⟩', '|1⟩', '|+⟩', '|-⟩', '|+i⟩', '|-i⟩')

    Returns:
        tuple: (n, l, m) quantum numbers for corresponding orbital
    """
    if state_name not in BLOCH_PURE_STATES:
        raise ValueError(f"Unknown state: {state_name}")

    return BLOCH_PURE_STATES[state_name]['orbital']

def get_pure_state_bloch(state_name):
    """
    Get Bloch sphere coordinates for a named pure state.

    Args:
        state_name: Name of pure state

    Returns:
        tuple: (theta, phi) Bloch sphere angles
    """
    if state_name not in BLOCH_PURE_STATES:
        raise ValueError(f"Unknown state: {state_name}")

    return (
        BLOCH_PURE_STATES[state_name]['theta'],
        BLOCH_PURE_STATES[state_name]['phi']
    )

# ============================================================================
# Quantum Gate Operations
# ============================================================================

def apply_gate_to_state(state_vector, gate):
    """
    Apply quantum gate to state vector.

    Args:
        state_vector: Complex array [α, β]
        gate: 2x2 complex gate matrix or gate name string

    Returns:
        np.array: Transformed state vector
    """
    if isinstance(gate, str):
        if gate.startswith('RX('):
            # Rotation gate with parameter
            angle = float(gate[3:-1])
            gate_matrix = rotation_x(angle)
        elif gate.startswith('RY('):
            angle = float(gate[3:-1])
            gate_matrix = rotation_y(angle)
        elif gate.startswith('RZ('):
            angle = float(gate[3:-1])
            gate_matrix = rotation_z(angle)
        else:
            gate_matrix = QUANTUM_GATES.get(gate)
            if gate_matrix is None:
                raise ValueError(f"Unknown gate: {gate}")
    else:
        gate_matrix = gate

    # Apply gate: |ψ'⟩ = U|ψ⟩
    new_state = gate_matrix @ state_vector

    return new_state

def apply_gate_to_bloch(theta, phi, gate):
    """
    Apply quantum gate to state specified by Bloch angles.

    Args:
        theta, phi: Bloch sphere angles
        gate: Gate matrix or gate name

    Returns:
        tuple: (theta', phi') new Bloch angles after gate
    """
    # Convert to state vector
    state = bloch_to_state_vector(theta, phi)

    # Apply gate
    new_state = apply_gate_to_state(state, gate)

    # Convert back to Bloch
    new_theta, new_phi = state_vector_to_bloch(new_state)

    return new_theta, new_phi

def apply_gate_to_orbitals(coeffs, gate, basis='sp'):
    """
    Apply quantum gate to orbital coefficients.

    Args:
        coeffs: Current orbital coefficients dict
        gate: Gate to apply
        basis: Orbital basis being used

    Returns:
        dict: New orbital coefficients after gate
    """
    # Extract state vector from coefficients
    if basis == 'sp':
        alpha = coeffs.get((1, 0, 0), 0)
        beta = coeffs.get((2, 0, 0), 0)
    elif basis == 'pp':
        # For p orbitals, reconstruct theta, phi
        c_px = np.real(coeffs.get((2, 1, 1), 0))
        c_py = np.real(coeffs.get((2, 1, -1), 0))
        c_pz = np.real(coeffs.get((2, 1, 0), 0))

        theta = 2 * np.arccos(c_pz)
        phi = np.arctan2(c_py, c_px)

        alpha = np.cos(theta / 2)
        beta = np.exp(1j * phi) * np.sin(theta / 2)
    else:
        alpha = coeffs.get((1, 0, 0), 0)
        beta = coeffs.get((3, 2, 0), 0)

    state = np.array([alpha, beta])

    # Apply gate
    new_state = apply_gate_to_state(state, gate)

    # Convert back to orbital coefficients
    if basis == 'pp':
        new_theta, new_phi = state_vector_to_bloch(new_state)
        return bloch_to_orbital_coeffs(new_theta, new_phi, basis='pp')
    else:
        alpha_new, beta_new = new_state
        if basis == 'sp':
            return {
                (1, 0, 0): alpha_new,
                (2, 0, 0): beta_new,
            }
        else:  # sd
            return {
                (1, 0, 0): alpha_new,
                (3, 2, 0): beta_new,
            }

# ============================================================================
# Superposition Analysis
# ============================================================================

def analyze_superposition(theta, phi, basis='sp'):
    """
    Analyze the orbital superposition for given Bloch state.

    Args:
        theta, phi: Bloch sphere angles
        basis: Orbital basis

    Returns:
        dict: Analysis results including coefficients, probabilities, phases
    """
    coeffs = bloch_to_orbital_coeffs(theta, phi, basis)

    analysis = {
        'theta': theta,
        'phi': phi,
        'basis': basis,
        'coefficients': coeffs,
        'probabilities': {},
        'phases': {},
        'coherence': None,
    }

    # Calculate probabilities and phases
    total_prob = 0
    for nlm, coeff in coeffs.items():
        prob = np.abs(coeff) ** 2
        phase = np.angle(coeff)

        analysis['probabilities'][nlm] = prob
        analysis['phases'][nlm] = phase
        total_prob += prob

    # Coherence (off-diagonal density matrix element)
    if len(coeffs) == 2:
        keys = list(coeffs.keys())
        c1, c2 = coeffs[keys[0]], coeffs[keys[1]]
        analysis['coherence'] = c1 * np.conj(c2)

    # Normalization check
    analysis['normalized'] = np.isclose(total_prob, 1.0)

    return analysis

def get_interference_pattern(theta, phi, axis='z', extent=20, resolution=100, basis='sp'):
    """
    Calculate interference pattern for orbital superposition along an axis.

    Args:
        theta, phi: Bloch sphere angles
        axis: Axis to plot along ('x', 'y', or 'z')
        extent: Spatial extent in Bohr radii
        resolution: Number of points
        basis: Orbital basis

    Returns:
        tuple: (positions, densities) for plotting
    """
    coeffs = bloch_to_orbital_coeffs(theta, phi, basis)

    # Create line along axis
    positions = np.linspace(-extent, extent, resolution)

    if axis == 'x':
        x, y, z = positions, 0, 0
    elif axis == 'y':
        x, y, z = 0, positions, 0
    elif axis == 'z':
        x, y, z = 0, 0, positions
    else:
        raise ValueError(f"Unknown axis: {axis}")

    # Calculate density
    density = orbital_coeffs_to_density(coeffs, x, y, z)

    return positions, density

# ============================================================================
# Special Superposition States
# ============================================================================

def create_bell_state(which='phi_plus'):
    """
    Create coefficients for Bell-like states (single qubit analogs).

    Args:
        which: Which Bell-like state ('phi_plus', 'phi_minus', 'psi_plus', 'psi_minus')

    Returns:
        dict: Orbital coefficients
    """
    inv_sqrt2 = 1.0 / np.sqrt(2)

    if which == 'phi_plus':
        # |Φ+⟩ = (|0⟩ + |1⟩)/√2  (|+⟩ state)
        return {(1, 0, 0): inv_sqrt2, (2, 0, 0): inv_sqrt2}
    elif which == 'phi_minus':
        # |Φ-⟩ = (|0⟩ - |1⟩)/√2  (|-⟩ state)
        return {(1, 0, 0): inv_sqrt2, (2, 0, 0): -inv_sqrt2}
    elif which == 'psi_plus':
        # |Ψ+⟩ = (|0⟩ + i|1⟩)/√2  (|+i⟩ state)
        return {(1, 0, 0): inv_sqrt2, (2, 0, 0): 1j * inv_sqrt2}
    elif which == 'psi_minus':
        # |Ψ-⟩ = (|0⟩ - i|1⟩)/√2  (|-i⟩ state)
        return {(1, 0, 0): inv_sqrt2, (2, 0, 0): -1j * inv_sqrt2}
    else:
        raise ValueError(f"Unknown Bell state: {which}")

def create_arbitrary_superposition(amplitudes, basis='sp'):
    """
    Create orbital coefficients from arbitrary amplitudes.

    Args:
        amplitudes: Complex array [α, β]
        basis: Orbital basis to use

    Returns:
        dict: Orbital coefficients
    """
    # Normalize
    alpha, beta = amplitudes
    norm = np.sqrt(np.abs(alpha) ** 2 + np.abs(beta) ** 2)
    alpha /= norm
    beta /= norm

    # Convert to Bloch then to orbitals
    theta, phi = state_vector_to_bloch([alpha, beta])

    return bloch_to_orbital_coeffs(theta, phi, basis)
