"""
Link classical dynamics to quantum properties.
Maps (a,b,c) trajectory to quantum state observables.
"""

import numpy as np
import matplotlib
matplotlib.use('TkAgg')
import matplotlib.pyplot as plt
from typing import Dict, Tuple, Optional
from dataclasses import dataclass

from .dynamics_core import DynamicsConfig, integrate_rk4


@dataclass
class QuantumMapping:
    """Maps classical parameters to quantum gate angles."""
    # A Gate parameter mapping
    # a -> RX rotation angle
    # b -> phase gate angle
    # c -> RY rotation angle


def classical_to_quantum_angles(a: float, b: float, c: float) -> Tuple[float, float, float]:
    """
    Map classical (a,b,c) to quantum gate parameters.

    Returns:
        (rx_angle, phase_angle, ry_angle)
    """
    # Scale to gate angle ranges
    rx_angle = 2 * np.pi * a  # Full rotation range
    phase_angle = b  # Already in [0, 2pi]
    ry_angle = 2 * np.pi * c  # Full rotation range

    return rx_angle, phase_angle, ry_angle


def compute_bloch_coords_from_abc(a: float, b: float, c: float) -> Tuple[np.ndarray, np.ndarray]:
    """
    Compute Bloch sphere coordinates for both qubits from (a,b,c).

    Returns:
        (bloch_q0, bloch_q1) each as [x, y, z]
    """
    # Simplified mapping - assumes single qubit states
    # In reality, need to run the full A Gate circuit

    # Qubit 0 (Excitatory): primarily affected by a and b
    theta0 = np.pi * a  # Polar angle
    phi0 = b  # Azimuthal angle
    bloch_q0 = np.array([
        np.sin(theta0) * np.cos(phi0),
        np.sin(theta0) * np.sin(phi0),
        np.cos(theta0)
    ])

    # Qubit 1 (Inhibitory): primarily affected by c and b
    theta1 = np.pi * c
    phi1 = b + np.pi/2  # Phase shifted
    bloch_q1 = np.array([
        np.sin(theta1) * np.cos(phi1),
        np.sin(theta1) * np.sin(phi1),
        np.cos(theta1)
    ])

    return bloch_q0, bloch_q1


def estimate_concurrence(a: float, b: float, c: float) -> float:
    """
    Estimate entanglement (concurrence) from classical parameters.

    The A Gate creates entanglement through the CRY and CRZ gates.
    Maximum entanglement occurs at intermediate values of a and c.
    """
    # Entanglement is maximized when both qubits are in superposition
    # and the controlled gates create correlations

    # Simplified model: concurrence peaks when a,c ~ 0.5
    # and varies with the coupling angle
    coupling_strength = np.sin(np.pi/4)  # From CRY(pi/4) gate

    # Superposition factor
    superposition = 4 * a * (1 - a) * c * (1 - c)

    # Phase coherence factor
    coherence = np.abs(np.cos(b - np.pi/4))

    concurrence = coupling_strength * np.sqrt(superposition) * coherence
    return np.clip(concurrence, 0, 1)


def trajectory_to_quantum_observables(trajectory: np.ndarray,
                                       config: DynamicsConfig) -> Dict:
    """
    Convert classical trajectory to quantum observables.
    """
    n_frames = len(trajectory)

    bloch_q0 = np.zeros((n_frames, 3))
    bloch_q1 = np.zeros((n_frames, 3))
    concurrence = np.zeros(n_frames)
    purity_q0 = np.zeros(n_frames)
    purity_q1 = np.zeros(n_frames)

    for i, (a, b, c) in enumerate(trajectory):
        b0, b1 = compute_bloch_coords_from_abc(a, b, c)
        bloch_q0[i] = b0
        bloch_q1[i] = b1
        concurrence[i] = estimate_concurrence(a, b, c)

        # Purity from Bloch vector length (pure state = 1, mixed = < 1)
        purity_q0[i] = (1 + np.linalg.norm(b0)**2) / 2
        purity_q1[i] = (1 + np.linalg.norm(b1)**2) / 2

    return {
        'time': np.arange(n_frames) * config.dt,
        'bloch_q0': bloch_q0,
        'bloch_q1': bloch_q1,
        'concurrence': concurrence,
        'purity_q0': purity_q0,
        'purity_q1': purity_q1,
        'classical': trajectory
    }


def plot_quantum_observables(results: Dict) -> plt.Figure:
    """
    Plot quantum observables derived from classical trajectory.
    """
    fig = plt.figure(figsize=(16, 12), facecolor='#0a0a1a')

    t = results['time']
    traj = results['classical']

    # 1. Classical parameters
    ax1 = fig.add_subplot(2, 3, 1, facecolor='#0a0a1a')
    ax1.plot(t, traj[:, 0], color='#ff6b35', label='a (E)')
    ax1.plot(t, traj[:, 2], color='#7b68ee', label='c (I)')
    ax1.set_xlabel('Time (s)', color='white')
    ax1.set_ylabel('Value', color='white')
    ax1.set_title('Classical Parameters', color='white')
    ax1.tick_params(colors='white')
    ax1.legend(facecolor='#1a1a2e', labelcolor='white')

    # 2. Concurrence (entanglement)
    ax2 = fig.add_subplot(2, 3, 2, facecolor='#0a0a1a')
    ax2.fill_between(t, 0, results['concurrence'], color='#ff00ff', alpha=0.5)
    ax2.plot(t, results['concurrence'], color='#ff00ff', linewidth=1)
    ax2.set_xlabel('Time (s)', color='white')
    ax2.set_ylabel('Concurrence', color='white')
    ax2.set_title('Entanglement', color='white')
    ax2.tick_params(colors='white')
    ax2.set_ylim([0, 1])

    # 3. Purity
    ax3 = fig.add_subplot(2, 3, 3, facecolor='#0a0a1a')
    ax3.plot(t, results['purity_q0'], color='#ff6b35', label='Q0 (E)')
    ax3.plot(t, results['purity_q1'], color='#7b68ee', label='Q1 (I)')
    ax3.set_xlabel('Time (s)', color='white')
    ax3.set_ylabel('Purity', color='white')
    ax3.set_title('State Purity', color='white')
    ax3.tick_params(colors='white')
    ax3.legend(facecolor='#1a1a2e', labelcolor='white')

    # 4. Bloch sphere Q0
    ax4 = fig.add_subplot(2, 3, 4, projection='3d', facecolor='#0a0a1a')
    bloch0 = results['bloch_q0']
    colors = np.linspace(0, 1, len(bloch0))
    ax4.scatter(bloch0[:, 0], bloch0[:, 1], bloch0[:, 2],
               c=colors, cmap='plasma', s=1, alpha=0.5)

    # Draw sphere outline
    u = np.linspace(0, 2 * np.pi, 50)
    v = np.linspace(0, np.pi, 25)
    x = np.outer(np.cos(u), np.sin(v))
    y = np.outer(np.sin(u), np.sin(v))
    z = np.outer(np.ones(np.size(u)), np.cos(v))
    ax4.plot_wireframe(x, y, z, color='white', alpha=0.1)

    ax4.set_xlabel('X', color='white')
    ax4.set_ylabel('Y', color='white')
    ax4.set_zlabel('Z', color='white')
    ax4.set_title('Bloch Sphere Q0 (E)', color='white')

    # 5. Bloch sphere Q1
    ax5 = fig.add_subplot(2, 3, 5, projection='3d', facecolor='#0a0a1a')
    bloch1 = results['bloch_q1']
    ax5.scatter(bloch1[:, 0], bloch1[:, 1], bloch1[:, 2],
               c=colors, cmap='viridis', s=1, alpha=0.5)
    ax5.plot_wireframe(x, y, z, color='white', alpha=0.1)

    ax5.set_xlabel('X', color='white')
    ax5.set_ylabel('Y', color='white')
    ax5.set_zlabel('Z', color='white')
    ax5.set_title('Bloch Sphere Q1 (I)', color='white')

    # 6. Phase space with concurrence coloring
    ax6 = fig.add_subplot(2, 3, 6, facecolor='#0a0a1a')
    scatter = ax6.scatter(traj[:, 0], traj[:, 2],
                         c=results['concurrence'], cmap='magma',
                         s=1, alpha=0.5)
    plt.colorbar(scatter, ax=ax6, label='Concurrence')
    ax6.set_xlabel('a (E)', color='white')
    ax6.set_ylabel('c (I)', color='white')
    ax6.set_title('Phase Space (colored by entanglement)', color='white')
    ax6.tick_params(colors='white')

    plt.tight_layout()
    return fig


def compute_quantum_foam_signature(trajectory: np.ndarray,
                                    config: DynamicsConfig,
                                    window_size: int = 100) -> Dict:
    """
    Compute quantum signatures of foam dynamics.
    """
    observables = trajectory_to_quantum_observables(trajectory, config)

    # Sliding window statistics
    n_windows = len(trajectory) - window_size

    concurrence_mean = np.zeros(n_windows)
    concurrence_std = np.zeros(n_windows)
    bloch_speed_q0 = np.zeros(n_windows)
    bloch_speed_q1 = np.zeros(n_windows)

    for i in range(n_windows):
        window = slice(i, i + window_size)

        concurrence_mean[i] = np.mean(observables['concurrence'][window])
        concurrence_std[i] = np.std(observables['concurrence'][window])

        # Bloch vector velocity
        db0 = np.diff(observables['bloch_q0'][window], axis=0)
        db1 = np.diff(observables['bloch_q1'][window], axis=0)
        bloch_speed_q0[i] = np.mean(np.linalg.norm(db0, axis=1)) / config.dt
        bloch_speed_q1[i] = np.mean(np.linalg.norm(db1, axis=1)) / config.dt

    return {
        't_windows': np.arange(n_windows) * config.dt,
        'concurrence_mean': concurrence_mean,
        'concurrence_std': concurrence_std,
        'bloch_speed_q0': bloch_speed_q0,
        'bloch_speed_q1': bloch_speed_q1,
        'window_size': window_size
    }
