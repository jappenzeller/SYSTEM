"""
Fixed point analysis - find and classify equilibria.
"""

import numpy as np
import matplotlib
matplotlib.use('TkAgg')
import matplotlib.pyplot as plt
from typing import Dict, List, Tuple, Optional
from scipy.optimize import fsolve

from .dynamics_core import DynamicsConfig, compute_derivatives, compute_jacobian, find_fixed_points, analyze_eigenvalues


def numerical_fixed_point_search(config: DynamicsConfig,
                                  n_guesses: int = 50,
                                  tol: float = 1e-8) -> List[np.ndarray]:
    """
    Find fixed points numerically using multiple initial guesses.
    """
    def equations(state):
        return compute_derivatives(state, config)

    found_points = []
    np.random.seed(42)

    for _ in range(n_guesses):
        guess = np.array([
            np.random.uniform(0.01, 0.99),
            np.random.uniform(0.01, 2*np.pi - 0.01),
            np.random.uniform(0.01, 0.99)
        ])

        try:
            solution, info, ier, mesg = fsolve(equations, guess, full_output=True)

            # Check if solution is valid
            if ier == 1 and np.all(solution >= 0) and solution[0] <= 1 and solution[2] <= 1:
                # Check if it's actually a fixed point
                residual = np.linalg.norm(equations(solution))
                if residual < tol:
                    # Check if we already found this point
                    is_new = True
                    for fp in found_points:
                        if np.linalg.norm(solution - fp) < 0.01:
                            is_new = False
                            break
                    if is_new:
                        found_points.append(solution)
        except:
            pass

    return found_points


def classify_fixed_point(point: np.ndarray, config: DynamicsConfig) -> Dict:
    """
    Detailed classification of a fixed point.
    """
    J = compute_jacobian(point, config)
    eigenvalues = np.linalg.eigvals(J)
    eig_analysis = analyze_eigenvalues(eigenvalues)

    # Determine type
    real_parts = np.real(eigenvalues)
    imag_parts = np.imag(eigenvalues)

    n_positive = np.sum(real_parts > 0)
    n_negative = np.sum(real_parts < 0)
    n_zero = np.sum(np.abs(real_parts) < 1e-10)
    has_complex = np.any(np.abs(imag_parts) > 1e-10)

    if n_positive == 0 and n_zero == 0:
        if has_complex:
            fp_type = 'stable_spiral'
        else:
            fp_type = 'stable_node'
    elif n_negative == 0 and n_zero == 0:
        if has_complex:
            fp_type = 'unstable_spiral'
        else:
            fp_type = 'unstable_node'
    elif n_zero > 0:
        fp_type = 'center_or_nonhyperbolic'
    else:
        fp_type = f'saddle_{n_positive}_{n_negative}'

    return {
        'point': point,
        'jacobian': J,
        'eigenvalues': eigenvalues,
        'type': fp_type,
        'stability': 'stable' if n_positive == 0 and n_zero == 0 else 'unstable',
        'n_unstable_directions': n_positive,
        'has_oscillation': has_complex,
        **eig_analysis
    }


def plot_fixed_point_eigenspectrum(classifications: List[Dict],
                                    ax: Optional[plt.Axes] = None) -> plt.Axes:
    """
    Plot eigenvalues in the complex plane for all fixed points.
    """
    if ax is None:
        fig, ax = plt.subplots(figsize=(8, 8), facecolor='#0a0a1a')

    ax.set_facecolor('#0a0a1a')

    colors = plt.cm.tab10(np.linspace(0, 1, len(classifications)))

    for i, cl in enumerate(classifications):
        eigs = cl['eigenvalues']
        ax.scatter(np.real(eigs), np.imag(eigs),
                  c=[colors[i]]*len(eigs), s=100, label=f'FP{i+1}: {cl["type"]}',
                  edgecolors='white', linewidths=1, zorder=5)

    # Unit circle and axes
    theta = np.linspace(0, 2*np.pi, 100)
    ax.axhline(0, color='white', linestyle='--', alpha=0.3)
    ax.axvline(0, color='red', linestyle='--', alpha=0.5, label='Stability boundary')

    ax.set_xlabel('Real(lambda)', color='white')
    ax.set_ylabel('Imag(lambda)', color='white')
    ax.set_title('Eigenvalue Spectrum', color='white')
    ax.tick_params(colors='white')
    ax.legend(facecolor='#1a1a2e', labelcolor='white', loc='upper left')
    ax.set_aspect('equal')
    ax.grid(True, alpha=0.2, color='white')

    return ax


def fixed_point_parameter_continuation(param_name: str,
                                        param_range: Tuple[float, float],
                                        n_values: int = 100,
                                        base_config: DynamicsConfig = None) -> Dict:
    """
    Track how fixed points move as a parameter changes.
    """
    base_config = base_config or DynamicsConfig()
    param_vals = np.linspace(param_range[0], param_range[1], n_values)

    results = {
        'param_name': param_name,
        'param_values': param_vals,
        'fixed_points': [],
        'eigenvalues': [],
        'stability': [],
        'types': []
    }

    for pval in param_vals:
        # Create config with varied parameter
        cfg = DynamicsConfig(
            lambda_a=pval if param_name == 'lambda_a' else base_config.lambda_a,
            lambda_c=pval if param_name == 'lambda_c' else base_config.lambda_c,
            f_constant=pval if param_name == 'f_constant' else base_config.f_constant,
            dt=base_config.dt
        )

        fps = find_fixed_points(cfg)
        if fps:
            fp = fps[0]
            classification = classify_fixed_point(fp['point'], cfg)
            results['fixed_points'].append(fp['point'])
            results['eigenvalues'].append(fp['eigenvalues'])
            results['stability'].append(classification['stability'])
            results['types'].append(classification['type'])
        else:
            results['fixed_points'].append(np.array([np.nan, np.nan, np.nan]))
            results['eigenvalues'].append(np.array([np.nan, np.nan, np.nan]))
            results['stability'].append('none')
            results['types'].append('none')

    return results


def plot_continuation(results: Dict, observable: str = 'a') -> plt.Figure:
    """
    Plot fixed point continuation diagram.
    """
    fig, axes = plt.subplots(2, 1, figsize=(12, 10), facecolor='#0a0a1a')

    obs_idx = {'a': 0, 'b': 1, 'c': 2}[observable]
    fp_values = [fp[obs_idx] for fp in results['fixed_points']]
    eig_reals = [np.real(eig) for eig in results['eigenvalues']]

    # Top: Fixed point position
    ax1 = axes[0]
    ax1.set_facecolor('#0a0a1a')

    # Color by stability
    for i in range(len(results['param_values']) - 1):
        color = '#00ff00' if results['stability'][i] == 'stable' else '#ff0000'
        ax1.plot(results['param_values'][i:i+2], fp_values[i:i+2],
                color=color, linewidth=2)

    ax1.set_ylabel(f'{observable}* (fixed point)', color='white')
    ax1.set_title(f'Fixed Point Continuation: {results["param_name"]}', color='white')
    ax1.tick_params(colors='white')

    # Bottom: Eigenvalue real parts
    ax2 = axes[1]
    ax2.set_facecolor('#0a0a1a')

    colors = ['#ff6b35', '#7b68ee', '#00d084']
    for j in range(3):
        eig_j = [er[j] if len(er) > j else np.nan for er in eig_reals]
        ax2.plot(results['param_values'], eig_j, color=colors[j],
                linewidth=1.5, label=f'Re(lambda_{j+1})')

    ax2.axhline(0, color='white', linestyle='--', alpha=0.5)
    ax2.set_xlabel(results['param_name'], color='white')
    ax2.set_ylabel('Re(eigenvalue)', color='white')
    ax2.set_title('Eigenvalue Real Parts', color='white')
    ax2.tick_params(colors='white')
    ax2.legend(facecolor='#1a1a2e', labelcolor='white')

    plt.tight_layout()
    return fig
