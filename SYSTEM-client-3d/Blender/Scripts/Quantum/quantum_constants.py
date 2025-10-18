"""
Quantum Constants and Definitions

Physical constants, quantum number ranges, and visualization parameters
for hydrogen orbital calculations.
"""

import numpy as np

# ============================================================================
# Physical Constants (SI units unless otherwise noted)
# ============================================================================

# Bohr radius (meters)
BOHR_RADIUS = 5.29177210903e-11  # m

# Fine structure constant (dimensionless)
FINE_STRUCTURE_CONSTANT = 7.2973525693e-3

# Planck constant (J·s)
PLANCK_CONSTANT = 6.62607015e-34
HBAR = PLANCK_CONSTANT / (2 * np.pi)

# Elementary charge (C)
ELEMENTARY_CHARGE = 1.602176634e-19

# Electron mass (kg)
ELECTRON_MASS = 9.1093837015e-31

# Rydberg energy (eV)
RYDBERG_ENERGY = 13.605693122994  # eV

# ============================================================================
# Quantum Number Definitions
# ============================================================================

# Valid quantum number ranges
MAX_PRINCIPAL_QUANTUM_NUMBER = 7  # n: 1, 2, 3, ..., 7
MAX_AZIMUTHAL_QUANTUM_NUMBER = 6  # l: 0 to n-1

# Orbital letter designations
ORBITAL_LETTERS = {
    0: 's',
    1: 'p',
    2: 'd',
    3: 'f',
    4: 'g',
    5: 'h',
    6: 'i',
}

# Quantum number validation
def validate_quantum_numbers(n, l, m):
    """
    Validate quantum numbers for hydrogen orbitals.

    Args:
        n: Principal quantum number
        l: Azimuthal quantum number
        m: Magnetic quantum number

    Returns:
        bool: True if valid, raises ValueError if invalid

    Raises:
        ValueError: If quantum numbers are invalid
    """
    if not isinstance(n, int) or n < 1:
        raise ValueError(f"Principal quantum number n must be positive integer, got {n}")

    if not isinstance(l, int) or l < 0 or l >= n:
        raise ValueError(f"Azimuthal quantum number l must be 0 ≤ l < n, got l={l}, n={n}")

    if not isinstance(m, int) or abs(m) > l:
        raise ValueError(f"Magnetic quantum number m must satisfy |m| ≤ l, got m={m}, l={l}")

    if n > MAX_PRINCIPAL_QUANTUM_NUMBER:
        raise ValueError(f"n={n} exceeds maximum supported value {MAX_PRINCIPAL_QUANTUM_NUMBER}")

    return True

# ============================================================================
# Normalization Constants
# ============================================================================

# Spherical harmonic normalization factors are calculated in hydrogen_wavefunctions.py
# using scipy.special.sph_harm which includes proper normalization

# Radial function normalization factors (pre-computed for common orbitals)
# These are 1/sqrt(n³ a₀³) where a₀ is Bohr radius
RADIAL_NORMALIZATION = {
    (1, 0): 2.0,                    # 1s
    (2, 0): 1.0 / (2.0 * np.sqrt(2)), # 2s
    (2, 1): 1.0 / (2.0 * np.sqrt(6)), # 2p
    (3, 0): 2.0 / (27.0 * np.sqrt(3)), # 3s
    (3, 1): 4.0 / (27.0 * np.sqrt(6)), # 3p
    (3, 2): 2.0 / (81.0 * np.sqrt(30)), # 3d
}

# ============================================================================
# Bloch Sphere to Orbital Mapping
# ============================================================================

# Pure state mappings (cardinal points on Bloch sphere)
BLOCH_PURE_STATES = {
    '|0⟩': {'theta': 0.0, 'phi': 0.0, 'orbital': (1, 0, 0), 'name': '1s'},
    '|1⟩': {'theta': np.pi, 'phi': 0.0, 'orbital': (2, 0, 0), 'name': '2s'},
    '|+⟩': {'theta': np.pi/2, 'phi': 0.0, 'orbital': (2, 1, 1), 'name': '2px'},
    '|-⟩': {'theta': np.pi/2, 'phi': np.pi, 'orbital': (2, 1, -1), 'name': '2px*'},
    '|+i⟩': {'theta': np.pi/2, 'phi': np.pi/2, 'orbital': (2, 1, 0), 'name': '2py'},
    '|-i⟩': {'theta': np.pi/2, 'phi': 3*np.pi/2, 'orbital': (2, 1, 0), 'name': '2py*'},
}

# Equatorial states (superpositions)
BLOCH_EQUATORIAL_STATES = {
    'X+': {'theta': np.pi/2, 'phi': 0.0},      # |+⟩ = (|0⟩ + |1⟩)/√2
    'X-': {'theta': np.pi/2, 'phi': np.pi},    # |-⟩ = (|0⟩ - |1⟩)/√2
    'Y+': {'theta': np.pi/2, 'phi': np.pi/2},  # |+i⟩ = (|0⟩ + i|1⟩)/√2
    'Y-': {'theta': np.pi/2, 'phi': 3*np.pi/2},# |-i⟩ = (|0⟩ - i|1⟩)/√2
}

# ============================================================================
# Visualization Parameters
# ============================================================================

# Color mappings for phase visualization
PHASE_COLORMAP = 'hsv'  # Use HSV colormap for phase (0 to 2π)

# Probability density isosurface values (fraction of maximum)
DEFAULT_ISO_VALUES = [0.01, 0.05, 0.1, 0.2]

# Grid resolution for orbital calculations (points per axis)
DEFAULT_GRID_RESOLUTION = 64
HIGH_RES_GRID = 128
LOW_RES_GRID = 32

# Spatial extent for orbital visualization (in Bohr radii)
ORBITAL_EXTENT = {
    1: 10.0,  # n=1: ±10 a₀
    2: 20.0,  # n=2: ±20 a₀
    3: 30.0,  # n=3: ±30 a₀
    4: 40.0,  # n=4: ±40 a₀
    5: 50.0,  # n=5: ±50 a₀
    6: 60.0,  # n=6: ±60 a₀
    7: 70.0,  # n=7: ±70 a₀
}

# Color schemes for different orbital types
ORBITAL_COLORS = {
    's': (0.2, 0.6, 1.0, 0.7),   # Blue for s orbitals
    'p': (1.0, 0.5, 0.2, 0.7),   # Orange for p orbitals
    'd': (0.2, 1.0, 0.4, 0.7),   # Green for d orbitals
    'f': (0.8, 0.2, 1.0, 0.7),   # Purple for f orbitals
    'g': (1.0, 1.0, 0.2, 0.7),   # Yellow for g orbitals
    'h': (0.2, 1.0, 1.0, 0.7),   # Cyan for h orbitals
    'i': (1.0, 0.2, 0.6, 0.7),   # Magenta for i orbitals
}

# Phase colors (for positive/negative lobes)
PHASE_POSITIVE_COLOR = (1.0, 0.3, 0.3, 0.7)  # Red for positive phase
PHASE_NEGATIVE_COLOR = (0.3, 0.3, 1.0, 0.7)  # Blue for negative phase

# ============================================================================
# Quantum Gates
# ============================================================================

# Pauli matrices (for gate operations)
PAULI_X = np.array([[0, 1], [1, 0]], dtype=complex)
PAULI_Y = np.array([[0, -1j], [1j, 0]], dtype=complex)
PAULI_Z = np.array([[1, 0], [0, -1]], dtype=complex)
IDENTITY = np.array([[1, 0], [0, 1]], dtype=complex)

# Hadamard gate
HADAMARD = np.array([[1, 1], [1, -1]], dtype=complex) / np.sqrt(2)

# Phase gates
S_GATE = np.array([[1, 0], [0, 1j]], dtype=complex)
T_GATE = np.array([[1, 0], [0, np.exp(1j * np.pi / 4)]], dtype=complex)

# Rotation gates (parameterized)
def rotation_x(theta):
    """Rotation around X-axis by angle theta."""
    return np.array([
        [np.cos(theta/2), -1j * np.sin(theta/2)],
        [-1j * np.sin(theta/2), np.cos(theta/2)]
    ], dtype=complex)

def rotation_y(theta):
    """Rotation around Y-axis by angle theta."""
    return np.array([
        [np.cos(theta/2), -np.sin(theta/2)],
        [np.sin(theta/2), np.cos(theta/2)]
    ], dtype=complex)

def rotation_z(theta):
    """Rotation around Z-axis by angle theta."""
    return np.array([
        [np.exp(-1j * theta/2), 0],
        [0, np.exp(1j * theta/2)]
    ], dtype=complex)

# Gate name mapping
QUANTUM_GATES = {
    'X': PAULI_X,
    'Y': PAULI_Y,
    'Z': PAULI_Z,
    'H': HADAMARD,
    'S': S_GATE,
    'T': T_GATE,
    'I': IDENTITY,
}

# ============================================================================
# Utility Functions
# ============================================================================

def get_orbital_name(n, l, m=None):
    """
    Get human-readable name for orbital.

    Args:
        n: Principal quantum number
        l: Azimuthal quantum number
        m: Magnetic quantum number (optional)

    Returns:
        str: Orbital name (e.g., "1s", "2px", "3dz²")
    """
    letter = ORBITAL_LETTERS.get(l, f'l{l}')

    if m is None:
        return f"{n}{letter}"

    # Special names for specific m values
    if l == 1:  # p orbitals
        if m == 1:
            suffix = 'x'
        elif m == -1:
            suffix = 'y'
        elif m == 0:
            suffix = 'z'
        else:
            suffix = f'm{m}'
    elif l == 2:  # d orbitals
        if m == 0:
            suffix = 'z²'
        elif m == 1:
            suffix = 'xz'
        elif m == -1:
            suffix = 'yz'
        elif m == 2:
            suffix = 'xy'
        elif m == -2:
            suffix = 'x²-y²'
        else:
            suffix = f'm{m}'
    else:
        suffix = f'm{m}' if m != 0 else ''

    return f"{n}{letter}{suffix}"

def get_orbital_extent(n):
    """
    Get spatial extent for orbital visualization.

    Args:
        n: Principal quantum number

    Returns:
        float: Extent in Bohr radii
    """
    return ORBITAL_EXTENT.get(n, n * 10.0)

def get_orbital_color(l, phase=None):
    """
    Get color for orbital visualization.

    Args:
        l: Azimuthal quantum number
        phase: Optional phase value (for phase-dependent coloring)

    Returns:
        tuple: RGBA color tuple
    """
    if phase is not None:
        # Color by phase
        return PHASE_POSITIVE_COLOR if phase >= 0 else PHASE_NEGATIVE_COLOR

    # Color by orbital type
    letter = ORBITAL_LETTERS.get(l, 's')
    return ORBITAL_COLORS.get(letter, (0.5, 0.5, 0.5, 0.7))
