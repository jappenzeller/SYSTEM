"""
Core dynamical system for quantum PN neuron.
ODE definitions, integration, and basic analysis.
"""

import numpy as np
from dataclasses import dataclass
from typing import Tuple, Optional, Callable


@dataclass
class DynamicsConfig:
    """Configuration for PN dynamics."""
    lambda_a: float = 0.1      # Excitatory decay rate
    lambda_c: float = 0.05     # Inhibitory growth rate
    f_constant: float = 0.5    # Constant input (for autonomous analysis)
    dt: float = 0.001          # Integration timestep

    # Bounds
    a_bounds: Tuple[float, float] = (0.0, 1.0)
    b_bounds: Tuple[float, float] = (0.0, 2*np.pi)
    c_bounds: Tuple[float, float] = (0.0, 1.0)


def compute_derivatives(state: np.ndarray,
                        config: DynamicsConfig,
                        f_t: Optional[float] = None) -> np.ndarray:
    """
    Compute (da/dt, db/dt, dc/dt).

    Args:
        state: [a, b, c] array
        config: Dynamics configuration
        f_t: Input at current time (uses config.f_constant if None)

    Returns:
        [da/dt, db/dt, dc/dt] array
    """
    a, b, c = state
    f = f_t if f_t is not None else config.f_constant

    da = -config.lambda_a * a + f * (1 - a)
    db = f * (1 - b)
    dc = config.lambda_c * c + f * (1 - c)

    return np.array([da, db, dc])


def compute_jacobian(state: np.ndarray,
                     config: DynamicsConfig,
                     f_t: Optional[float] = None) -> np.ndarray:
    """
    Compute Jacobian matrix at given state.

    J[i,j] = d(dx_i/dt)/dx_j
    """
    f = f_t if f_t is not None else config.f_constant

    J = np.array([
        [-(config.lambda_a + f), 0, 0],
        [0, -f, 0],
        [0, 0, config.lambda_c - f]
    ])
    return J


def integrate_rk4(initial: np.ndarray,
                  config: DynamicsConfig,
                  n_steps: int,
                  f_func: Optional[Callable[[float], float]] = None,
                  record_every: int = 1) -> np.ndarray:
    """
    Integrate trajectory using RK4.

    Args:
        initial: [a0, b0, c0] initial state
        config: Dynamics configuration
        n_steps: Number of integration steps
        f_func: Optional f(t) function, uses config.f_constant if None
        record_every: Record state every N steps (for memory efficiency)

    Returns:
        (n_recorded, 3) trajectory array
    """
    n_recorded = (n_steps + record_every - 1) // record_every
    trajectory = np.zeros((n_recorded, 3))

    state = initial.copy()
    dt = config.dt

    record_idx = 0
    for i in range(n_steps):
        t = i * dt
        f_t = f_func(t) if f_func else config.f_constant

        # RK4 step
        k1 = compute_derivatives(state, config, f_t)
        k2 = compute_derivatives(state + 0.5*dt*k1, config, f_t)
        k3 = compute_derivatives(state + 0.5*dt*k2, config, f_t)
        k4 = compute_derivatives(state + dt*k3, config, f_t)

        state = state + (dt/6) * (k1 + 2*k2 + 2*k3 + k4)

        # Apply bounds
        state[0] = np.clip(state[0], *config.a_bounds)
        state[1] = np.clip(state[1], *config.b_bounds)
        state[2] = np.clip(state[2], *config.c_bounds)

        # Record
        if i % record_every == 0:
            trajectory[record_idx] = state
            record_idx += 1

    return trajectory[:record_idx]


def find_fixed_points(config: DynamicsConfig) -> list:
    """
    Find fixed points analytically.

    Returns list of dicts: {'point', 'eigenvalues', 'stability', 'type'}
    """
    f = config.f_constant
    lambda_a = config.lambda_a
    lambda_c = config.lambda_c

    fixed_points = []

    if f == 0:
        # Origin is marginally stable
        fixed_points.append({
            'point': np.array([0.0, 0.0, 0.0]),
            'eigenvalues': np.array([-lambda_a, 0, lambda_c]),
            'stability': 'saddle',
            'type': 'origin (f=0)'
        })
    else:
        # Interior fixed point
        a_star = f / (lambda_a + f)
        b_star = 1.0  # Phase saturates

        if f > lambda_c:
            c_star = f / (f - lambda_c)
            c_star = min(c_star, 1.0)  # Clamp to bounds
        else:
            c_star = 1.0  # Saturates at boundary

        point = np.array([a_star, b_star, c_star])
        J = compute_jacobian(point, config)
        eigenvalues = np.linalg.eigvals(J)

        # Classify stability
        real_parts = np.real(eigenvalues)
        if all(real_parts < 0):
            stability = 'stable'
        elif all(real_parts > 0):
            stability = 'unstable'
        else:
            stability = 'saddle'

        fixed_points.append({
            'point': point,
            'eigenvalues': eigenvalues,
            'stability': stability,
            'type': 'interior' if c_star < 1.0 else 'boundary (c=1)'
        })

    return fixed_points


def analyze_eigenvalues(eigenvalues: np.ndarray) -> dict:
    """
    Detailed eigenvalue analysis.
    """
    real = np.real(eigenvalues)
    imag = np.imag(eigenvalues)

    # Sort by real part (most unstable first)
    order = np.argsort(real)[::-1]
    sorted_eig = eigenvalues[order]

    return {
        'eigenvalues': sorted_eig,
        'real_parts': np.real(sorted_eig),
        'imag_parts': np.imag(sorted_eig),
        'max_real': np.max(real),
        'is_stable': np.max(real) < 0,
        'has_oscillation': np.any(np.abs(imag) > 1e-10),
        'oscillation_frequency': np.max(np.abs(imag)) / (2 * np.pi) if np.any(np.abs(imag) > 1e-10) else 0
    }
