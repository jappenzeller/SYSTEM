"""
Bifurcation analysis - how behavior changes with parameters.
"""

import numpy as np
import matplotlib
matplotlib.use('TkAgg')
import matplotlib.pyplot as plt
from typing import Tuple, Dict, List
from scipy.signal import find_peaks
from tqdm import tqdm

from .dynamics_core import DynamicsConfig, integrate_rk4, find_fixed_points


def bifurcation_1d(param_name: str,
                   param_range: Tuple[float, float],
                   n_values: int = 100,
                   base_config: DynamicsConfig = None,
                   observable: str = 'a',
                   n_transient: int = 5000,
                   n_sample: int = 2000) -> Dict:
    """
    1D bifurcation diagram.

    Args:
        param_name: 'lambda_a', 'lambda_c', or 'f_constant'
        param_range: (min, max) for sweep
        n_values: Resolution of sweep
        base_config: Starting configuration
        observable: 'a', 'b', or 'c'
        n_transient: Steps to discard
        n_sample: Steps to sample for attractor

    Returns:
        Dict with param_values, attractor data, classifications
    """
    base_config = base_config or DynamicsConfig()
    param_vals = np.linspace(param_range[0], param_range[1], n_values)
    obs_idx = {'a': 0, 'b': 1, 'c': 2}[observable]

    results = {
        'param_name': param_name,
        'param_values': param_vals,
        'attractors': [],
        'attractor_type': [],
        'fixed_point_values': []
    }

    for pval in tqdm(param_vals, desc=f'Bifurcation ({param_name})'):
        # Create config with varied parameter
        cfg = DynamicsConfig(
            lambda_a=pval if param_name == 'lambda_a' else base_config.lambda_a,
            lambda_c=pval if param_name == 'lambda_c' else base_config.lambda_c,
            f_constant=pval if param_name == 'f_constant' else base_config.f_constant,
            dt=base_config.dt
        )

        # Integrate
        initial = np.array([0.5, 1.0, 0.5])
        total_steps = n_transient + n_sample
        traj = integrate_rk4(initial, cfg, total_steps)
        steady = traj[n_transient:]

        # Extract observable
        obs = steady[:, obs_idx]

        # Find attractor structure (local maxima for periodic orbits)
        peaks, _ = find_peaks(obs, distance=20)

        if len(peaks) > 3:
            # Periodic - record peak values
            attractor_vals = obs[peaks[-20:]] if len(peaks) >= 20 else obs[peaks]
            atype = 'periodic'
        else:
            # Fixed point
            attractor_vals = obs[-50:]
            atype = 'fixed_point' if np.std(attractor_vals) < 0.01 else 'complex'

        results['attractors'].append(attractor_vals)
        results['attractor_type'].append(atype)

        # Theoretical fixed point
        fps = find_fixed_points(cfg)
        if fps:
            results['fixed_point_values'].append(fps[0]['point'][obs_idx])
        else:
            results['fixed_point_values'].append(np.nan)

    return results


def bifurcation_2d(param1: str, range1: Tuple[float, float],
                   param2: str, range2: Tuple[float, float],
                   resolution: int = 50,
                   base_config: DynamicsConfig = None,
                   metric: str = 'amplitude') -> Dict:
    """
    2D parameter space map.

    Args:
        param1, param2: Parameter names
        range1, range2: Parameter ranges
        resolution: Grid resolution
        metric: 'amplitude', 'period', 'concurrence'

    Returns:
        Dict with parameter grids and metric map
    """
    base_config = base_config or DynamicsConfig()

    p1_vals = np.linspace(*range1, resolution)
    p2_vals = np.linspace(*range2, resolution)

    result_map = np.zeros((resolution, resolution))

    for i, p1 in enumerate(tqdm(p1_vals, desc='2D bifurcation')):
        for j, p2 in enumerate(p2_vals):
            # Build config
            cfg = DynamicsConfig(
                lambda_a=base_config.lambda_a,
                lambda_c=base_config.lambda_c,
                f_constant=base_config.f_constant,
                dt=base_config.dt
            )
            if param1 == 'lambda_a':
                cfg.lambda_a = p1
            elif param1 == 'lambda_c':
                cfg.lambda_c = p1
            elif param1 == 'f_constant':
                cfg.f_constant = p1

            if param2 == 'lambda_a':
                cfg.lambda_a = p2
            elif param2 == 'lambda_c':
                cfg.lambda_c = p2
            elif param2 == 'f_constant':
                cfg.f_constant = p2

            # Quick integration
            initial = np.array([0.5, 1.0, 0.5])
            traj = integrate_rk4(initial, cfg, n_steps=5000, record_every=10)
            steady = traj[len(traj)//2:]  # Last half

            if metric == 'amplitude':
                result_map[i, j] = np.max(steady[:, 0]) - np.min(steady[:, 0])
            elif metric == 'std':
                result_map[i, j] = np.std(steady[:, 0])

    return {
        'param1': param1, 'param2': param2,
        'p1_values': p1_vals, 'p2_values': p2_vals,
        'result': result_map, 'metric': metric
    }


def plot_bifurcation_1d(results: Dict, ax: plt.Axes = None) -> plt.Axes:
    """Plot 1D bifurcation diagram."""
    if ax is None:
        fig, ax = plt.subplots(figsize=(12, 8), facecolor='#0a0a1a')

    ax.set_facecolor('#0a0a1a')

    for pval, attractor, atype in zip(results['param_values'],
                                       results['attractors'],
                                       results['attractor_type']):
        color = {'fixed_point': '#00ff00', 'periodic': '#ff6b35', 'complex': '#ff00ff'}[atype]
        ax.scatter([pval] * len(attractor), attractor, c=color, s=0.5, alpha=0.5)

    # Theoretical fixed point curve
    ax.plot(results['param_values'], results['fixed_point_values'],
            'w--', linewidth=1, alpha=0.5, label='Theoretical FP')

    ax.set_xlabel(results['param_name'], color='white')
    ax.set_ylabel('Attractor Values', color='white')
    ax.set_title(f'Bifurcation Diagram: {results["param_name"]}', color='white')
    ax.tick_params(colors='white')
    ax.legend(facecolor='#1a1a2e', labelcolor='white')

    return ax


def plot_bifurcation_2d(results: Dict) -> Tuple[plt.Figure, plt.Axes]:
    """Plot 2D bifurcation map."""
    fig, ax = plt.subplots(figsize=(10, 8), facecolor='#0a0a1a')
    ax.set_facecolor('#0a0a1a')

    im = ax.imshow(results['result'].T, origin='lower', aspect='auto',
                   extent=[results['p1_values'][0], results['p1_values'][-1],
                          results['p2_values'][0], results['p2_values'][-1]],
                   cmap='magma')

    ax.set_xlabel(results['param1'], color='white')
    ax.set_ylabel(results['param2'], color='white')
    ax.set_title(f'Parameter Space: {results["metric"]}', color='white')
    ax.tick_params(colors='white')

    cbar = plt.colorbar(im, ax=ax)
    cbar.ax.tick_params(colors='white')
    cbar.set_label(results['metric'], color='white')

    return fig, ax
