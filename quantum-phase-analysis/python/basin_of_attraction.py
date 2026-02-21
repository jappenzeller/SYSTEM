"""
Basin of attraction analysis - initial condition sensitivity.
"""

import numpy as np
import matplotlib
matplotlib.use('TkAgg')
import matplotlib.pyplot as plt
from typing import Dict, Tuple, Optional
from tqdm import tqdm

from .dynamics_core import DynamicsConfig, integrate_rk4, find_fixed_points


def compute_basin_2d(config: DynamicsConfig,
                     plane: str = 'ac',
                     fixed_coord: float = 0.5,
                     resolution: int = 100,
                     t_max: float = 50.0,
                     tolerance: float = 0.05) -> Dict:
    """
    Compute basin of attraction in a 2D slice.

    Returns:
        Dict with grid coordinates and basin labels
    """
    # Get fixed points for classification
    fps = find_fixed_points(config)

    # Coordinate setup
    idx_map = {'ac': (0, 2, 1), 'ab': (0, 1, 2), 'bc': (1, 2, 0)}
    x_idx, y_idx, fixed_idx = idx_map[plane]

    ranges = {
        'ac': ((0.01, 0.99), (0.01, 0.99)),
        'ab': ((0.01, 0.99), (0.01, 2*np.pi - 0.01)),
        'bc': ((0.01, 2*np.pi - 0.01), (0.01, 0.99)),
    }
    x_range, y_range = ranges[plane]

    x = np.linspace(*x_range, resolution)
    y = np.linspace(*y_range, resolution)
    X, Y = np.meshgrid(x, y)

    basin = np.zeros((resolution, resolution))
    final_states = np.zeros((resolution, resolution, 3))

    n_steps = int(t_max / config.dt)

    for i in tqdm(range(resolution), desc=f'Basin ({plane})'):
        for j in range(resolution):
            initial = np.zeros(3)
            initial[x_idx] = X[i, j]
            initial[y_idx] = Y[i, j]
            initial[fixed_idx] = fixed_coord

            traj = integrate_rk4(initial, config, n_steps, record_every=100)
            final = traj[-1]
            final_states[i, j] = final

            # Classify by which attractor it reaches
            if fps:
                distances = [np.linalg.norm(final - fp['point']) for fp in fps]
                nearest = np.argmin(distances)
                if distances[nearest] < tolerance:
                    basin[i, j] = nearest + 1  # 1-indexed
                else:
                    basin[i, j] = 0  # Unknown/limit cycle
            else:
                basin[i, j] = 0

    return {
        'plane': plane,
        'X': X, 'Y': Y,
        'basin': basin,
        'final_states': final_states,
        'fixed_points': fps,
        'x_label': ['a', 'b', 'c'][x_idx],
        'y_label': ['a', 'b', 'c'][y_idx]
    }


def plot_basin(results: Dict, ax: Optional[plt.Axes] = None) -> plt.Axes:
    """
    Plot basin of attraction map.
    """
    if ax is None:
        fig, ax = plt.subplots(figsize=(10, 10), facecolor='#0a0a1a')

    ax.set_facecolor('#0a0a1a')

    # Custom colormap
    n_attractors = int(np.max(results['basin']))
    colors = ['#333333'] + list(plt.cm.Set2(np.linspace(0, 1, max(n_attractors, 1))))
    from matplotlib.colors import ListedColormap
    cmap = ListedColormap(colors[:n_attractors + 1])

    im = ax.pcolormesh(results['X'], results['Y'], results['basin'],
                       cmap=cmap, shading='auto')

    # Mark fixed points
    for i, fp in enumerate(results['fixed_points']):
        x_idx = {'a': 0, 'b': 1, 'c': 2}[results['x_label']]
        y_idx = {'a': 0, 'b': 1, 'c': 2}[results['y_label']]

        marker = 'o' if fp['stability'] == 'stable' else 'x'
        color = '#00ff00' if fp['stability'] == 'stable' else '#ff0000'
        ax.scatter(fp['point'][x_idx], fp['point'][y_idx],
                  c=color, s=200, marker=marker, edgecolors='white',
                  linewidths=2, zorder=10, label=f'FP{i+1}')

    ax.set_xlabel(results['x_label'], color='white', fontsize=12)
    ax.set_ylabel(results['y_label'], color='white', fontsize=12)
    ax.set_title(f'Basin of Attraction ({results["plane"]} plane)', color='white', fontsize=14)
    ax.tick_params(colors='white')
    ax.legend(facecolor='#1a1a2e', labelcolor='white')

    return ax


def compute_basin_boundary_fractal_dimension(basin: np.ndarray,
                                               box_sizes: Optional[np.ndarray] = None) -> Dict:
    """
    Estimate fractal dimension of basin boundary using box counting.
    """
    if box_sizes is None:
        box_sizes = np.array([2, 4, 8, 16, 32, 64])

    # Find boundary pixels (where neighboring pixels have different labels)
    boundary = np.zeros_like(basin, dtype=bool)
    for di in [-1, 0, 1]:
        for dj in [-1, 0, 1]:
            if di == 0 and dj == 0:
                continue
            shifted = np.roll(np.roll(basin, di, axis=0), dj, axis=1)
            boundary |= (basin != shifted)

    counts = []
    for size in box_sizes:
        n_boxes = 0
        for i in range(0, basin.shape[0], size):
            for j in range(0, basin.shape[1], size):
                box = boundary[i:i+size, j:j+size]
                if np.any(box):
                    n_boxes += 1
        counts.append(n_boxes)

    counts = np.array(counts)
    log_sizes = np.log(box_sizes)
    log_counts = np.log(counts + 1)

    # Linear fit
    coeffs = np.polyfit(log_sizes, log_counts, 1)
    fractal_dim = -coeffs[0]

    return {
        'box_sizes': box_sizes,
        'counts': counts,
        'fractal_dimension': fractal_dim,
        'boundary_mask': boundary
    }


def sensitivity_analysis(config: DynamicsConfig,
                         base_point: np.ndarray,
                         n_perturbations: int = 100,
                         epsilon: float = 0.01,
                         t_max: float = 20.0) -> Dict:
    """
    Analyze sensitivity to initial conditions.
    """
    np.random.seed(42)

    n_steps = int(t_max / config.dt)
    base_traj = integrate_rk4(base_point, config, n_steps)

    divergences = []
    final_distances = []

    for _ in range(n_perturbations):
        # Random perturbation
        perturbation = np.random.randn(3) * epsilon
        perturbed = base_point + perturbation
        perturbed = np.clip(perturbed, [0, 0, 0], [1, 2*np.pi, 1])

        perturbed_traj = integrate_rk4(perturbed, config, n_steps)

        # Track divergence over time
        min_len = min(len(base_traj), len(perturbed_traj))
        distances = np.linalg.norm(base_traj[:min_len] - perturbed_traj[:min_len], axis=1)
        divergences.append(distances)
        final_distances.append(distances[-1])

    divergences = np.array(divergences)
    mean_divergence = np.mean(divergences, axis=0)
    std_divergence = np.std(divergences, axis=0)

    return {
        'base_point': base_point,
        'epsilon': epsilon,
        'mean_divergence': mean_divergence,
        'std_divergence': std_divergence,
        'final_distances': np.array(final_distances),
        'time': np.arange(len(mean_divergence)) * config.dt
    }


def plot_sensitivity(results: Dict, ax: Optional[plt.Axes] = None) -> plt.Axes:
    """
    Plot sensitivity analysis results.
    """
    if ax is None:
        fig, ax = plt.subplots(figsize=(10, 6), facecolor='#0a0a1a')

    ax.set_facecolor('#0a0a1a')

    t = results['time']
    mean = results['mean_divergence']
    std = results['std_divergence']

    ax.semilogy(t, mean, color='#ff6b35', linewidth=2, label='Mean divergence')
    ax.fill_between(t, mean - std, mean + std, color='#ff6b35', alpha=0.3)
    ax.axhline(results['epsilon'], color='white', linestyle='--', alpha=0.5,
              label=f'Initial perturbation (epsilon={results["epsilon"]})')

    ax.set_xlabel('Time (s)', color='white')
    ax.set_ylabel('Distance from base trajectory', color='white')
    ax.set_title('Sensitivity to Initial Conditions', color='white')
    ax.tick_params(colors='white')
    ax.legend(facecolor='#1a1a2e', labelcolor='white')

    return ax
