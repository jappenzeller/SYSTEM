"""
Bloch Orbital Mapper - High-Level Interface

Provides a unified interface for working with quantum states represented
as both Bloch sphere coordinates and hydrogen orbital superpositions.
"""

import numpy as np
from .quantum_constants import (
    DEFAULT_GRID_RESOLUTION,
    get_orbital_extent,
    get_orbital_name,
)
from .hydrogen_wavefunctions import (
    hydrogen_orbital,
    probability_density,
    calculate_density_grid,
)
from .orbital_coefficients import (
    bloch_to_state_vector,
    state_vector_to_bloch,
    bloch_to_orbital_coeffs,
    orbital_coeffs_to_density,
    apply_gate_to_bloch,
    apply_gate_to_orbitals,
    analyze_superposition,
)

# ============================================================================
# Main Bloch Orbital State Class
# ============================================================================

class BlochOrbitalState:
    """
    Represents a quantum state with both Bloch sphere and orbital representations.

    This class provides a unified interface for:
    - Setting state via Bloch angles or state vector
    - Calculating orbital superposition coefficients
    - Generating 3D probability density grids
    - Applying quantum gates
    - Analyzing interference patterns

    Attributes:
        theta (float): Bloch sphere polar angle (0 to π)
        phi (float): Bloch sphere azimuthal angle (0 to 2π)
        basis (str): Orbital basis ('sp', 'pp', or 'sd')
        state_vector (np.array): Complex state vector [α, β]
        orbital_coeffs (dict): Orbital mixture coefficients
    """

    def __init__(self, theta=0.0, phi=0.0, basis='sp'):
        """
        Initialize Bloch orbital state.

        Args:
            theta: Initial polar angle (default: 0 = |0⟩ state)
            phi: Initial azimuthal angle (default: 0)
            basis: Orbital basis to use (default: 'sp' for s-orbital basis)
        """
        self.basis = basis
        self._theta = 0.0
        self._phi = 0.0
        self._state_vector = np.array([1.0, 0.0], dtype=complex)
        self._orbital_coeffs = {}
        self._density_cache = None

        # Set initial state
        self.set_bloch_state(theta, phi)

    @property
    def theta(self):
        """Bloch sphere polar angle."""
        return self._theta

    @property
    def phi(self):
        """Bloch sphere azimuthal angle."""
        return self._phi

    @property
    def state_vector(self):
        """Complex state vector [α, β]."""
        return self._state_vector.copy()

    @property
    def orbital_coeffs(self):
        """Orbital mixture coefficients."""
        return self._orbital_coeffs.copy()

    # ========================================================================
    # State Setting Methods
    # ========================================================================

    def set_bloch_state(self, theta, phi):
        """
        Set state from Bloch sphere coordinates.

        Args:
            theta: Polar angle (0 to π)
            phi: Azimuthal angle (0 to 2π)
        """
        self._theta = theta
        self._phi = phi
        self._state_vector = bloch_to_state_vector(theta, phi)
        self._orbital_coeffs = bloch_to_orbital_coeffs(theta, phi, self.basis)
        self._density_cache = None  # Invalidate cache

    def set_state_vector(self, state_vector):
        """
        Set state from complex state vector.

        Args:
            state_vector: Complex array [α, β]
        """
        # Normalize
        state_vector = np.asarray(state_vector, dtype=complex)
        norm = np.sqrt(np.sum(np.abs(state_vector) ** 2))
        state_vector /= norm

        self._state_vector = state_vector
        self._theta, self._phi = state_vector_to_bloch(state_vector)
        self._orbital_coeffs = bloch_to_orbital_coeffs(self._theta, self._phi, self.basis)
        self._density_cache = None

    def set_pure_state(self, state_name):
        """
        Set to a named pure state.

        Args:
            state_name: Name of state ('|0⟩', '|1⟩', '|+⟩', '|-⟩', '|+i⟩', '|-i⟩')
        """
        from .quantum_constants import BLOCH_PURE_STATES

        if state_name not in BLOCH_PURE_STATES:
            raise ValueError(f"Unknown state: {state_name}")

        state_info = BLOCH_PURE_STATES[state_name]
        self.set_bloch_state(state_info['theta'], state_info['phi'])

    # ========================================================================
    # Orbital Access Methods
    # ========================================================================

    def get_orbital_mixture(self):
        """
        Get current orbital coefficients.

        Returns:
            dict: Mapping from (n, l, m) tuples to complex coefficients
        """
        return self._orbital_coeffs.copy()

    def get_dominant_orbital(self):
        """
        Get the orbital with largest coefficient magnitude.

        Returns:
            tuple: ((n, l, m), coefficient) for dominant orbital
        """
        max_coeff = 0.0
        dominant = None

        for nlm, coeff in self._orbital_coeffs.items():
            mag = np.abs(coeff)
            if mag > max_coeff:
                max_coeff = mag
                dominant = (nlm, coeff)

        return dominant

    def get_orbital_probabilities(self):
        """
        Get probability (|coefficient|²) for each orbital.

        Returns:
            dict: Mapping from (n, l, m) to probability
        """
        probs = {}
        for nlm, coeff in self._orbital_coeffs.items():
            probs[nlm] = np.abs(coeff) ** 2

        return probs

    # ========================================================================
    # Density Calculation Methods
    # ========================================================================

    def calculate_density_grid(self, resolution=None, extent=None, use_cache=True):
        """
        Generate 3D probability density grid for current state.

        Args:
            resolution: Grid resolution (default: from constants)
            extent: Spatial extent in Bohr radii (default: auto from n)
            use_cache: Use cached grid if available

        Returns:
            tuple: (x_grid, y_grid, z_grid, density_grid)
        """
        if use_cache and self._density_cache is not None:
            return self._density_cache

        if resolution is None:
            resolution = DEFAULT_GRID_RESOLUTION

        if extent is None:
            # Use extent based on highest n value
            max_n = max(nlm[0] for nlm in self._orbital_coeffs.keys())
            extent = get_orbital_extent(max_n)

        # Create grid
        x = np.linspace(-extent, extent, resolution)
        y = np.linspace(-extent, extent, resolution)
        z = np.linspace(-extent, extent, resolution)
        x_grid, y_grid, z_grid = np.meshgrid(x, y, z, indexing='ij')

        # Calculate density from orbital mixture
        density_grid = orbital_coeffs_to_density(
            self._orbital_coeffs,
            x_grid, y_grid, z_grid
        )

        # Cache result
        self._density_cache = (x_grid, y_grid, z_grid, density_grid)

        return self._density_cache

    def calculate_slice(self, plane='xy', position=0.0, resolution=None, extent=None):
        """
        Calculate 2D density slice through orbital.

        Args:
            plane: Plane to slice ('xy', 'xz', or 'yz')
            position: Position along perpendicular axis
            resolution: Grid resolution
            extent: Spatial extent

        Returns:
            tuple: (coord1_grid, coord2_grid, density_slice)
        """
        if resolution is None:
            resolution = DEFAULT_GRID_RESOLUTION

        if extent is None:
            max_n = max(nlm[0] for nlm in self._orbital_coeffs.keys())
            extent = get_orbital_extent(max_n)

        # Create 2D grid
        coord = np.linspace(-extent, extent, resolution)
        c1, c2 = np.meshgrid(coord, coord, indexing='ij')

        # Map to 3D coordinates based on plane
        if plane == 'xy':
            x, y, z = c1, c2, position
        elif plane == 'xz':
            x, y, z = c1, position, c2
        elif plane == 'yz':
            x, y, z = position, c1, c2
        else:
            raise ValueError(f"Unknown plane: {plane}")

        # Calculate density
        density = orbital_coeffs_to_density(self._orbital_coeffs, x, y, z)

        return c1, c2, density

    # ========================================================================
    # Gate Operations
    # ========================================================================

    def apply_gate(self, gate_type):
        """
        Apply quantum gate to current state.

        Args:
            gate_type: Gate name ('X', 'Y', 'Z', 'H', 'S', 'T') or matrix

        Returns:
            BlochOrbitalState: self (for method chaining)
        """
        # Apply gate to Bloch coordinates
        new_theta, new_phi = apply_gate_to_bloch(self._theta, self._phi, gate_type)

        # Update state
        self.set_bloch_state(new_theta, new_phi)

        return self

    def apply_rotation(self, axis, angle):
        """
        Apply rotation gate around specified axis.

        Args:
            axis: Rotation axis ('X', 'Y', or 'Z')
            angle: Rotation angle in radians

        Returns:
            BlochOrbitalState: self
        """
        gate_name = f"R{axis.upper()}({angle})"
        return self.apply_gate(gate_name)

    # ========================================================================
    # Analysis Methods
    # ========================================================================

    def analyze(self):
        """
        Perform detailed analysis of current state.

        Returns:
            dict: Analysis results including probabilities, phases, coherence
        """
        return analyze_superposition(self._theta, self._phi, self.basis)

    def get_purity(self):
        """
        Calculate state purity (always 1.0 for pure states).

        Returns:
            float: Purity Tr(ρ²)
        """
        # For pure states, purity is always 1
        return 1.0

    def get_bloch_vector(self):
        """
        Calculate Bloch vector components (x, y, z).

        Returns:
            np.array: Bloch vector [x, y, z]
        """
        x = np.sin(self._theta) * np.cos(self._phi)
        y = np.sin(self._theta) * np.sin(self._phi)
        z = np.cos(self._theta)

        return np.array([x, y, z])

    def measure_expectation(self, observable):
        """
        Calculate expectation value of observable.

        Args:
            observable: 2x2 matrix representing observable

        Returns:
            float: Expectation value ⟨ψ|O|ψ⟩
        """
        expectation = np.conj(self._state_vector) @ observable @ self._state_vector
        return np.real(expectation)

    # ========================================================================
    # Visualization Helper Methods
    # ========================================================================

    def get_isosurface_values(self, fractions=[0.01, 0.05, 0.1]):
        """
        Get isosurface values as fractions of maximum density.

        Args:
            fractions: List of fractions of maximum density

        Returns:
            list: Absolute density values for isosurfaces
        """
        if self._density_cache is None:
            self.calculate_density_grid()

        _, _, _, density = self._density_cache
        max_density = np.max(density)

        return [frac * max_density for frac in fractions]

    def get_visualization_info(self):
        """
        Get information for visualization (colors, labels, etc.).

        Returns:
            dict: Visualization parameters
        """
        analysis = self.analyze()
        dominant_nlm, dominant_coeff = self.get_dominant_orbital()

        n, l, m = dominant_nlm
        orbital_name = get_orbital_name(n, l, m)

        info = {
            'orbital_name': orbital_name,
            'dominant_orbital': dominant_nlm,
            'dominant_probability': np.abs(dominant_coeff) ** 2,
            'theta': self._theta,
            'phi': self._phi,
            'bloch_vector': self.get_bloch_vector(),
            'state_label': self._get_state_label(),
            'probabilities': self.get_orbital_probabilities(),
        }

        return info

    def _get_state_label(self):
        """Generate human-readable state label."""
        alpha, beta = self._state_vector

        # Check if close to pure state
        if np.abs(alpha) > 0.99:
            return "|0⟩"
        elif np.abs(beta) > 0.99:
            return "|1⟩"
        elif np.abs(np.abs(alpha) - np.abs(beta)) < 0.01:
            # Superposition state
            phase = np.angle(beta) - np.angle(alpha)
            if np.abs(phase) < 0.1:
                return "|+⟩"
            elif np.abs(phase - np.pi) < 0.1:
                return "|-⟩"
            elif np.abs(phase - np.pi / 2) < 0.1:
                return "|+i⟩"
            elif np.abs(phase + np.pi / 2) < 0.1:
                return "|-i⟩"

        # General superposition
        return f"α|0⟩ + β|1⟩"

    # ========================================================================
    # String Representation
    # ========================================================================

    def __str__(self):
        """String representation of state."""
        alpha, beta = self._state_vector

        parts = []
        if np.abs(alpha) > 1e-10:
            parts.append(f"({alpha:.3f})|0⟩")
        if np.abs(beta) > 1e-10:
            sign = "+" if len(parts) > 0 else ""
            parts.append(f"{sign}({beta:.3f})|1⟩")

        state_str = " ".join(parts) if parts else "0"

        return (
            f"BlochOrbitalState(\n"
            f"  State: {state_str}\n"
            f"  Bloch: θ={self._theta:.3f}, φ={self._phi:.3f}\n"
            f"  Basis: {self.basis}\n"
            f"  Orbitals: {len(self._orbital_coeffs)}\n"
            f")"
        )

    def __repr__(self):
        """Repr representation."""
        return f"BlochOrbitalState(theta={self._theta:.3f}, phi={self._phi:.3f}, basis='{self.basis}')"

# ============================================================================
# Convenience Functions
# ============================================================================

def create_state(state_name, basis='sp'):
    """
    Create BlochOrbitalState from named pure state.

    Args:
        state_name: Name of state ('|0⟩', '|1⟩', '|+⟩', '|-⟩', '|+i⟩', '|-i⟩')
        basis: Orbital basis

    Returns:
        BlochOrbitalState: Initialized state
    """
    state = BlochOrbitalState(basis=basis)
    state.set_pure_state(state_name)
    return state

def create_superposition(alpha, beta, basis='sp'):
    """
    Create BlochOrbitalState from state vector coefficients.

    Args:
        alpha: Coefficient of |0⟩
        beta: Coefficient of |1⟩
        basis: Orbital basis

    Returns:
        BlochOrbitalState: Initialized state
    """
    state = BlochOrbitalState(basis=basis)
    state.set_state_vector([alpha, beta])
    return state

def create_bloch_state(theta, phi, basis='sp'):
    """
    Create BlochOrbitalState from Bloch angles.

    Args:
        theta: Polar angle
        phi: Azimuthal angle
        basis: Orbital basis

    Returns:
        BlochOrbitalState: Initialized state
    """
    return BlochOrbitalState(theta, phi, basis)
