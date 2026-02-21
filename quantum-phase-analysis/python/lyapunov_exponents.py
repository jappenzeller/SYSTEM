"""
Lyapunov exponent computation for chaos detection.
"""

import numpy as np
from typing import Tuple
from .dynamics_core import DynamicsConfig, compute_derivatives, compute_jacobian


def compute_lyapunov_spectrum(config: DynamicsConfig,
                              initial: np.ndarray,
                              n_steps: int = 50000,
                              n_transient: int = 5000) -> Tuple[np.ndarray, np.ndarray]:
    """
    Compute all 3 Lyapunov exponents using QR decomposition.

    Returns:
        (exponents, convergence_history)
    """
    dt = config.dt

    state = initial.copy()
    Q = np.eye(3)  # Orthonormal basis

    lyap_sum = np.zeros(3)
    n_record = n_steps - n_transient
    history = np.zeros((n_record, 3))

    for i in range(n_steps):
        # Evolve state (simple Euler for speed)
        deriv = compute_derivatives(state, config)
        state = state + dt * deriv
        state = np.clip(state, [0, 0, 0], [1, 2*np.pi, 1])

        # Evolve tangent vectors: dQ/dt = J @ Q
        J = compute_jacobian(state, config)
        Q = Q + dt * (J @ Q)

        # Reorthonormalize
        Q, R = np.linalg.qr(Q)

        # Accumulate stretching factors
        if i >= n_transient:
            lyap_sum += np.log(np.abs(np.diag(R)) + 1e-10)
            idx = i - n_transient
            if idx < n_record:
                history[idx] = lyap_sum / ((idx + 1) * dt)

    exponents = lyap_sum / (n_record * dt)
    return exponents, history


def classify_dynamics(exponents: np.ndarray, tol: float = 0.01) -> str:
    """Classify dynamics from Lyapunov spectrum."""
    max_exp = np.max(exponents)

    if max_exp > tol:
        return 'chaotic'
    elif max_exp > -tol:
        return 'limit_cycle'
    else:
        return 'fixed_point'


def kaplan_yorke_dimension(exponents: np.ndarray) -> float:
    """Compute Kaplan-Yorke (Lyapunov) dimension."""
    sorted_exp = np.sort(exponents)[::-1]
    cumsum = np.cumsum(sorted_exp)

    j_indices = np.where(cumsum >= 0)[0]
    if len(j_indices) == 0:
        return 0.0

    j = j_indices[-1]

    if j + 1 >= len(sorted_exp):
        return float(len(sorted_exp))

    return j + 1 + cumsum[j] / abs(sorted_exp[j + 1])


def plot_lyapunov_convergence(history: np.ndarray, config: DynamicsConfig, ax=None):
    """Plot convergence of Lyapunov exponents over time."""
    import matplotlib.pyplot as plt

    if ax is None:
        fig, ax = plt.subplots(figsize=(10, 6), facecolor='#0a0a1a')

    ax.set_facecolor('#0a0a1a')

    t = np.arange(len(history)) * config.dt
    colors = ['#ff6b35', '#7b68ee', '#00d084']
    labels = ['lambda_1', 'lambda_2', 'lambda_3']

    for i in range(3):
        ax.plot(t, history[:, i], color=colors[i], linewidth=1, label=labels[i])

    ax.axhline(0, color='white', linestyle='--', alpha=0.5)
    ax.set_xlabel('Time (s)', color='white')
    ax.set_ylabel('Lyapunov Exponent', color='white')
    ax.set_title('Lyapunov Exponent Convergence', color='white')
    ax.tick_params(colors='white')
    ax.legend(facecolor='#1a1a2e', labelcolor='white')

    return ax
