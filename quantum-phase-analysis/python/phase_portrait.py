"""
Phase portrait visualization in 2D and 3D.
"""

import numpy as np
import matplotlib
matplotlib.use('TkAgg')
import matplotlib.pyplot as plt
from matplotlib.colors import Normalize
from mpl_toolkits.mplot3d import Axes3D
from typing import Tuple, List, Optional

from .dynamics_core import DynamicsConfig, compute_derivatives, integrate_rk4, find_fixed_points


def compute_vector_field_2d(config: DynamicsConfig,
                            plane: str = 'ac',
                            fixed_coord: float = 0.5,
                            resolution: int = 20) -> Tuple[np.ndarray, ...]:
    """
    Compute 2D vector field for phase portrait.

    Args:
        config: Dynamics configuration
        plane: 'ac', 'ab', or 'bc'
        fixed_coord: Value of third coordinate
        resolution: Grid resolution

    Returns:
        X, Y, U, V arrays for quiver plot
    """
    # Define coordinate ranges
    ranges = {
        'ac': ((0.01, 0.99), (0.01, 0.99)),  # a, c
        'ab': ((0.01, 0.99), (0.01, 2*np.pi - 0.01)),  # a, b
        'bc': ((0.01, 2*np.pi - 0.01), (0.01, 0.99)),  # b, c
    }

    x_range, y_range = ranges[plane]
    x = np.linspace(*x_range, resolution)
    y = np.linspace(*y_range, resolution)
    X, Y = np.meshgrid(x, y)

    U = np.zeros_like(X)
    V = np.zeros_like(Y)

    # Map plane to state indices
    idx_map = {
        'ac': (0, 2, 1),  # x=a, y=c, fixed=b
        'ab': (0, 1, 2),  # x=a, y=b, fixed=c
        'bc': (1, 2, 0),  # x=b, y=c, fixed=a
    }
    x_idx, y_idx, fixed_idx = idx_map[plane]

    for i in range(resolution):
        for j in range(resolution):
            state = np.zeros(3)
            state[x_idx] = X[i, j]
            state[y_idx] = Y[i, j]
            state[fixed_idx] = fixed_coord

            deriv = compute_derivatives(state, config)
            U[i, j] = deriv[x_idx]
            V[i, j] = deriv[y_idx]

    return X, Y, U, V


def compute_nullclines(config: DynamicsConfig, plane: str = 'ac') -> dict:
    """
    Compute nullclines (where da/dt=0, dc/dt=0, etc).
    """
    f = config.f_constant
    lambda_a = config.lambda_a
    lambda_c = config.lambda_c

    nullclines = {}

    if plane == 'ac':
        # da/dt = 0: -lambda_a*a + f*(1-a) = 0 -> a = f/(lambda_a + f)
        a_null = f / (lambda_a + f) if (lambda_a + f) > 0 else 0
        nullclines['da_dt_zero'] = {
            'type': 'vertical',
            'value': a_null,
            'label': 'da/dt = 0'
        }

        # dc/dt = 0: lambda_c*c + f*(1-c) = 0 -> c = f/(f - lambda_c)
        if f > lambda_c:
            c_null = f / (f - lambda_c)
            if c_null <= 1:
                nullclines['dc_dt_zero'] = {
                    'type': 'horizontal',
                    'value': c_null,
                    'label': 'dc/dt = 0'
                }

    return nullclines


def plot_phase_portrait_2d(config: DynamicsConfig,
                           plane: str = 'ac',
                           fixed_coord: float = 0.5,
                           n_trajectories: int = 16,
                           t_max: float = 20.0,
                           ax: Optional[plt.Axes] = None,
                           show_nullclines: bool = True,
                           show_fixed_points: bool = True,
                           colorby: str = 'time') -> plt.Axes:
    """
    Generate 2D phase portrait.
    """
    if ax is None:
        fig, ax = plt.subplots(figsize=(10, 10), facecolor='#0a0a1a')

    ax.set_facecolor('#0a0a1a')

    # Labels and ranges
    labels = {
        'ac': ('a (Excitatory)', 'c (Inhibitory)', (0, 1), (0, 1)),
        'ab': ('a (Excitatory)', 'b (Phase)', (0, 1), (0, 2*np.pi)),
        'bc': ('b (Phase)', 'c (Inhibitory)', (0, 2*np.pi), (0, 1)),
    }
    x_label, y_label, x_lim, y_lim = labels[plane]
    idx_map = {'ac': (0, 2), 'ab': (0, 1), 'bc': (1, 2)}
    x_idx, y_idx = idx_map[plane]

    # Vector field
    X, Y, U, V = compute_vector_field_2d(config, plane, fixed_coord, resolution=20)
    magnitude = np.sqrt(U**2 + V**2)
    magnitude[magnitude == 0] = 1
    ax.quiver(X, Y, U/magnitude, V/magnitude, magnitude,
              cmap='plasma', alpha=0.6, scale=25)

    # Nullclines
    if show_nullclines:
        nullclines = compute_nullclines(config, plane)
        colors = {'da_dt_zero': '#ff6b35', 'dc_dt_zero': '#7b68ee'}
        for name, nc in nullclines.items():
            if nc['type'] == 'vertical':
                ax.axvline(nc['value'], color=colors.get(name, 'white'),
                          linestyle='--', linewidth=2, label=nc['label'])
            else:
                ax.axhline(nc['value'], color=colors.get(name, 'white'),
                          linestyle='--', linewidth=2, label=nc['label'])

    # Sample trajectories
    np.random.seed(42)
    n_steps = int(t_max / config.dt)
    fixed_idx = {'ac': 1, 'ab': 2, 'bc': 0}[plane]

    for _ in range(n_trajectories):
        initial = np.zeros(3)
        initial[x_idx] = np.random.uniform(*x_lim)
        initial[y_idx] = np.random.uniform(*y_lim)
        initial[fixed_idx] = fixed_coord

        traj = integrate_rk4(initial, config, n_steps)

        # Plot with time coloring
        if colorby == 'time':
            colors_arr = np.linspace(0, 1, len(traj))
            for i in range(len(traj) - 1):
                ax.plot(traj[i:i+2, x_idx], traj[i:i+2, y_idx],
                       color=plt.cm.viridis(colors_arr[i]), alpha=0.6, linewidth=0.5)
        else:
            ax.plot(traj[:, x_idx], traj[:, y_idx], 'w-', alpha=0.4, linewidth=0.5)

        # Start/end markers
        ax.scatter(traj[0, x_idx], traj[0, y_idx], c='green', s=20, zorder=5)
        ax.scatter(traj[-1, x_idx], traj[-1, y_idx], c='red', s=20, zorder=5)

    # Fixed points
    if show_fixed_points:
        fps = find_fixed_points(config)
        for fp in fps:
            color = '#00ff00' if fp['stability'] == 'stable' else '#ff0000'
            marker = 'o' if fp['stability'] == 'stable' else 'x'
            ax.scatter(fp['point'][x_idx], fp['point'][y_idx],
                      c=color, s=200, marker=marker, edgecolors='white',
                      linewidths=2, zorder=10)

    ax.set_xlim(x_lim)
    ax.set_ylim(y_lim)
    ax.set_xlabel(x_label, color='white', fontsize=12)
    ax.set_ylabel(y_label, color='white', fontsize=12)
    ax.set_title(f'Phase Portrait ({plane} plane)', color='white', fontsize=14)
    ax.tick_params(colors='white')
    ax.legend(facecolor='#1a1a2e', edgecolor='white', labelcolor='white', loc='upper right')

    return ax


def plot_phase_portrait_3d(config: DynamicsConfig,
                           n_trajectories: int = 10,
                           t_max: float = 20.0,
                           ax: Optional[Axes3D] = None) -> Axes3D:
    """
    Generate 3D phase portrait in full (a, b, c) space.
    """
    if ax is None:
        fig = plt.figure(figsize=(12, 10), facecolor='#0a0a1a')
        ax = fig.add_subplot(111, projection='3d', facecolor='#0a0a1a')

    np.random.seed(42)
    n_steps = int(t_max / config.dt)

    for _ in range(n_trajectories):
        initial = np.array([
            np.random.uniform(0.1, 0.9),
            np.random.uniform(0.1, 2*np.pi - 0.1),
            np.random.uniform(0.1, 0.9)
        ])

        traj = integrate_rk4(initial, config, n_steps)

        # Color by time
        colors_arr = np.linspace(0, 1, len(traj))
        for i in range(len(traj) - 1):
            ax.plot3D(traj[i:i+2, 0], traj[i:i+2, 1], traj[i:i+2, 2],
                     color=plt.cm.viridis(colors_arr[i]), alpha=0.6, linewidth=0.5)

        ax.scatter(*initial, c='green', s=50)
        ax.scatter(*traj[-1], c='red', s=50)

    # Fixed points
    fps = find_fixed_points(config)
    for fp in fps:
        color = '#00ff00' if fp['stability'] == 'stable' else '#ff0000'
        ax.scatter(*fp['point'], c=color, s=200, marker='*', edgecolors='white')

    ax.set_xlim([0, 1])
    ax.set_ylim([0, 2*np.pi])
    ax.set_zlim([0, 1])
    ax.set_xlabel('a (E)', color='white')
    ax.set_ylabel('b (phi)', color='white')
    ax.set_zlabel('c (I)', color='white')
    ax.set_title('3D Phase Space', color='white')
    ax.tick_params(colors='white')

    return ax
