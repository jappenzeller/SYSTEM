"""
Hydrogen Wave Function Calculations

Implements radial wave functions, spherical harmonics, and complete
hydrogen orbital wave functions with proper normalization.
"""

import numpy as np
from scipy import special
from scipy.integrate import tplquad
from .quantum_constants import (
    BOHR_RADIUS,
    validate_quantum_numbers,
    RADIAL_NORMALIZATION,
)

# ============================================================================
# Radial Wave Functions
# ============================================================================

def generalized_laguerre(n, alpha, x):
    """
    Calculate generalized Laguerre polynomial L_n^alpha(x).

    Uses scipy's genlaguerre for accurate computation.

    Args:
        n: Degree of polynomial
        alpha: Generalized parameter
        x: Evaluation point(s)

    Returns:
        Value(s) of L_n^alpha(x)
    """
    return special.eval_genlaguerre(n, alpha, x)

def radial_wavefunction(n, l, r):
    """
    Calculate radial part of hydrogen wave function R_nl(r).

    Uses the formula:
    R_nl(r) = sqrt((2/n*a0)³ * (n-l-1)!/(2n*(n+l)!)) * exp(-r/(n*a0)) * (2r/(n*a0))^l * L_{n-l-1}^{2l+1}(2r/(n*a0))

    Args:
        n: Principal quantum number (1, 2, 3, ...)
        l: Azimuthal quantum number (0 to n-1)
        r: Radial distance (in Bohr radii, can be array)

    Returns:
        R_nl(r): Radial wave function value(s)
    """
    validate_quantum_numbers(n, l, 0)

    # Convert to numpy array for vectorized operations
    r = np.asarray(r, dtype=float)

    # Avoid division by zero and numerical issues at r=0
    # For l>0, R_nl(0) = 0. For l=0, we use limit value
    r_safe = np.where(r > 0, r, 1e-10)

    # Dimensionless radial coordinate
    rho = 2.0 * r_safe / n

    # Normalization constant
    # N = sqrt((2/n)³ * (n-l-1)! / (2n * (n+l)!))
    norm_factor = np.sqrt(
        (2.0 / n) ** 3 *
        special.factorial(n - l - 1) /
        (2.0 * n * special.factorial(n + l))
    )

    # Exponential term
    exp_term = np.exp(-rho / 2.0)

    # Power term
    power_term = rho ** l

    # Generalized Laguerre polynomial L_{n-l-1}^{2l+1}(rho)
    laguerre_term = generalized_laguerre(n - l - 1, 2 * l + 1, rho)

    # Complete radial function
    R_nl = norm_factor * exp_term * power_term * laguerre_term

    # Handle r=0 case
    if np.isscalar(r):
        if r == 0:
            R_nl = 0.0 if l > 0 else norm_factor
    else:
        R_nl = np.where(r > 0, R_nl, 0.0 if l > 0 else norm_factor)

    return R_nl

# ============================================================================
# Spherical Harmonics
# ============================================================================

def spherical_harmonic(l, m, theta, phi, real_form=True):
    """
    Calculate spherical harmonic Y_l^m(θ, φ).

    Args:
        l: Azimuthal quantum number (0, 1, 2, ...)
        m: Magnetic quantum number (-l to +l)
        theta: Polar angle (0 to π), measured from +z axis
        phi: Azimuthal angle (0 to 2π), measured from +x axis
        real_form: If True, return real-valued spherical harmonics

    Returns:
        Y_l^m(θ, φ): Complex or real spherical harmonic value(s)
    """
    # scipy.special.sph_harm uses (m, l, phi, theta) convention
    # and includes Condon-Shortley phase (-1)^m
    Y_lm = special.sph_harm(m, l, phi, theta)

    if real_form and m != 0:
        # Convert to real form for visualization
        # Real form: Y_l^m_real = (Y_l^m + (-1)^m * Y_l^{-m}*) / sqrt(2)  for m > 0
        #            Y_l^m_real = (Y_l^m - (-1)^m * Y_l^{-m}*) / (i*sqrt(2))  for m < 0
        if m > 0:
            Y_lm_conj = special.sph_harm(-m, l, phi, theta)
            Y_real = (Y_lm + (-1) ** m * np.conj(Y_lm_conj)) / np.sqrt(2)
        else:  # m < 0
            Y_lm_pos = special.sph_harm(-m, l, phi, theta)
            Y_real = (Y_lm - (-1) ** m * np.conj(Y_lm_pos)) / (1j * np.sqrt(2))
        return np.real(Y_real)

    return Y_lm

# ============================================================================
# Complete Hydrogen Wave Functions
# ============================================================================

def cartesian_to_spherical(x, y, z):
    """
    Convert Cartesian coordinates to spherical coordinates.

    Args:
        x, y, z: Cartesian coordinates (can be arrays)

    Returns:
        (r, theta, phi): Spherical coordinates
            r: Radial distance
            theta: Polar angle (0 to π)
            phi: Azimuthal angle (0 to 2π)
    """
    x, y, z = np.asarray(x), np.asarray(y), np.asarray(z)

    r = np.sqrt(x ** 2 + y ** 2 + z ** 2)
    theta = np.arccos(np.clip(z / np.maximum(r, 1e-10), -1, 1))
    phi = np.arctan2(y, x)

    # Ensure phi is in [0, 2π]
    phi = np.where(phi < 0, phi + 2 * np.pi, phi)

    return r, theta, phi

def hydrogen_orbital(n, l, m, x, y, z, real_form=True):
    """
    Calculate hydrogen wave function value at position (x, y, z).

    Complete wave function:
    ψ_nlm(r, θ, φ) = R_nl(r) * Y_l^m(θ, φ)

    Args:
        n: Principal quantum number (1, 2, 3, ...)
        l: Azimuthal quantum number (0 to n-1)
        m: Magnetic quantum number (-l to +l)
        x, y, z: Cartesian coordinates (in Bohr radii, can be arrays)
        real_form: If True, use real spherical harmonics

    Returns:
        ψ_nlm(r, θ, φ): Wave function value(s) (complex or real)
    """
    validate_quantum_numbers(n, l, m)

    # Convert to spherical coordinates
    r, theta, phi = cartesian_to_spherical(x, y, z)

    # Calculate radial part
    R_nl = radial_wavefunction(n, l, r)

    # Calculate angular part
    Y_lm = spherical_harmonic(l, m, theta, phi, real_form=real_form)

    # Complete wave function
    psi = R_nl * Y_lm

    return psi

def probability_density(psi):
    """
    Calculate probability density |ψ|².

    Args:
        psi: Wave function value(s) (complex or real)

    Returns:
        |ψ|²: Probability density
    """
    if np.iscomplexobj(psi):
        return np.abs(psi) ** 2
    else:
        return psi ** 2

# ============================================================================
# Normalization and Integration
# ============================================================================

def verify_normalization(n, l, m, r_max=None, num_points=50):
    """
    Verify that wave function is normalized: ∫|ψ|²dV = 1

    Uses numerical integration over spherical volume.

    Args:
        n, l, m: Quantum numbers
        r_max: Maximum radius for integration (default: 5*n² Bohr radii)
        num_points: Number of integration points per dimension

    Returns:
        float: Integral value (should be close to 1.0)
    """
    validate_quantum_numbers(n, l, m)

    if r_max is None:
        r_max = 5 * n ** 2  # Extend to ~5n² Bohr radii

    # Define integrand in spherical coordinates
    def integrand(phi, theta, r):
        # Convert to Cartesian for wave function
        x = r * np.sin(theta) * np.cos(phi)
        y = r * np.sin(theta) * np.sin(phi)
        z = r * np.cos(theta)

        psi = hydrogen_orbital(n, l, m, x, y, z, real_form=True)
        # Jacobian for spherical coordinates: r² sin(θ)
        return probability_density(psi) * r ** 2 * np.sin(theta)

    # Integrate over full sphere
    result, error = tplquad(
        integrand,
        0, r_max,           # r: 0 to r_max
        0, np.pi,           # theta: 0 to π
        0, 2 * np.pi,       # phi: 0 to 2π
    )

    return result

def verify_orthogonality(n1, l1, m1, n2, l2, m2, r_max=None):
    """
    Verify orthogonality: ∫ψ₁*ψ₂dV = 0 for different states.

    Args:
        n1, l1, m1: Quantum numbers for first state
        n2, l2, m2: Quantum numbers for second state
        r_max: Maximum radius for integration

    Returns:
        float: Integral value (should be close to 0 for orthogonal states)
    """
    validate_quantum_numbers(n1, l1, m1)
    validate_quantum_numbers(n2, l2, m2)

    if r_max is None:
        r_max = 5 * max(n1, n2) ** 2

    def integrand(phi, theta, r):
        x = r * np.sin(theta) * np.cos(phi)
        y = r * np.sin(theta) * np.sin(phi)
        z = r * np.cos(theta)

        psi1 = hydrogen_orbital(n1, l1, m1, x, y, z, real_form=True)
        psi2 = hydrogen_orbital(n2, l2, m2, x, y, z, real_form=True)

        return psi1 * psi2 * r ** 2 * np.sin(theta)

    result, error = tplquad(
        integrand,
        0, r_max,
        0, np.pi,
        0, 2 * np.pi,
    )

    return result

# ============================================================================
# Grid-Based Calculations
# ============================================================================

def calculate_orbital_grid(n, l, m, extent=None, resolution=64, real_form=True):
    """
    Calculate wave function on a 3D grid.

    Args:
        n, l, m: Quantum numbers
        extent: Spatial extent in Bohr radii (default: based on n)
        resolution: Grid resolution (points per axis)
        real_form: Use real spherical harmonics

    Returns:
        tuple: (x_grid, y_grid, z_grid, psi_grid)
            x_grid, y_grid, z_grid: Coordinate grids
            psi_grid: Wave function values on grid
    """
    from .quantum_constants import get_orbital_extent

    validate_quantum_numbers(n, l, m)

    if extent is None:
        extent = get_orbital_extent(n)

    # Create 3D grid
    x = np.linspace(-extent, extent, resolution)
    y = np.linspace(-extent, extent, resolution)
    z = np.linspace(-extent, extent, resolution)
    x_grid, y_grid, z_grid = np.meshgrid(x, y, z, indexing='ij')

    # Calculate wave function on grid
    psi_grid = hydrogen_orbital(n, l, m, x_grid, y_grid, z_grid, real_form=real_form)

    return x_grid, y_grid, z_grid, psi_grid

def calculate_density_grid(n, l, m, extent=None, resolution=64):
    """
    Calculate probability density on a 3D grid.

    Args:
        n, l, m: Quantum numbers
        extent: Spatial extent in Bohr radii (default: based on n)
        resolution: Grid resolution (points per axis)

    Returns:
        tuple: (x_grid, y_grid, z_grid, density_grid)
            x_grid, y_grid, z_grid: Coordinate grids
            density_grid: Probability density values |ψ|²
    """
    x_grid, y_grid, z_grid, psi_grid = calculate_orbital_grid(
        n, l, m, extent, resolution, real_form=True
    )

    density_grid = probability_density(psi_grid)

    return x_grid, y_grid, z_grid, density_grid

# ============================================================================
# Special Orbital Functions
# ============================================================================

def orbital_1s(x, y, z):
    """1s orbital (ground state)."""
    return hydrogen_orbital(1, 0, 0, x, y, z, real_form=True)

def orbital_2s(x, y, z):
    """2s orbital."""
    return hydrogen_orbital(2, 0, 0, x, y, z, real_form=True)

def orbital_2px(x, y, z):
    """2px orbital (m=1 real form)."""
    return hydrogen_orbital(2, 1, 1, x, y, z, real_form=True)

def orbital_2py(x, y, z):
    """2py orbital (m=-1 real form)."""
    return hydrogen_orbital(2, 1, -1, x, y, z, real_form=True)

def orbital_2pz(x, y, z):
    """2pz orbital (m=0)."""
    return hydrogen_orbital(2, 1, 0, x, y, z, real_form=True)

def orbital_3dz2(x, y, z):
    """3d_z² orbital (m=0)."""
    return hydrogen_orbital(3, 2, 0, x, y, z, real_form=True)

def orbital_3dxz(x, y, z):
    """3d_xz orbital (m=1 real form)."""
    return hydrogen_orbital(3, 2, 1, x, y, z, real_form=True)

def orbital_3dyz(x, y, z):
    """3d_yz orbital (m=-1 real form)."""
    return hydrogen_orbital(3, 2, -1, x, y, z, real_form=True)

def orbital_3dxy(x, y, z):
    """3d_xy orbital (m=2 real form)."""
    return hydrogen_orbital(3, 2, 2, x, y, z, real_form=True)

def orbital_3dx2_y2(x, y, z):
    """3d_x²-y² orbital (m=-2 real form)."""
    return hydrogen_orbital(3, 2, -2, x, y, z, real_form=True)

# ============================================================================
# Utility Functions
# ============================================================================

def get_orbital_function(n, l, m):
    """
    Get callable function for specific orbital.

    Args:
        n, l, m: Quantum numbers

    Returns:
        Callable function that takes (x, y, z) and returns ψ
    """
    validate_quantum_numbers(n, l, m)

    return lambda x, y, z: hydrogen_orbital(n, l, m, x, y, z, real_form=True)

def find_orbital_maximum(n, l, m):
    """
    Find approximate location of maximum probability density.

    For s orbitals (l=0), maximum is at nucleus (r=0).
    For others, maximum is typically at r ≈ n²/Z for hydrogen (Z=1).

    Args:
        n, l, m: Quantum numbers

    Returns:
        float: Approximate radius of maximum density (in Bohr radii)
    """
    if l == 0:
        # For s orbitals, maximum at nucleus
        return 0.0
    else:
        # Approximate maximum at r ≈ n²
        return n ** 2
