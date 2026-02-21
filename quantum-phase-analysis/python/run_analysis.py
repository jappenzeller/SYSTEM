"""
Main analysis pipeline.
"""

import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent.parent))

import numpy as np
import matplotlib
matplotlib.use('TkAgg')
import matplotlib.pyplot as plt

from python.dynamics_core import DynamicsConfig, integrate_rk4, find_fixed_points
from python.phase_portrait import plot_phase_portrait_2d, plot_phase_portrait_3d
from python.bifurcation_analysis import bifurcation_1d, bifurcation_2d, plot_bifurcation_1d, plot_bifurcation_2d
from python.lyapunov_exponents import compute_lyapunov_spectrum, classify_dynamics, kaplan_yorke_dimension, plot_lyapunov_convergence
from python.foam_boundary import plot_foam_analysis
from python.fixed_point_analysis import classify_fixed_point, plot_fixed_point_eigenspectrum, fixed_point_parameter_continuation, plot_continuation
from python.basin_of_attraction import compute_basin_2d, plot_basin, sensitivity_analysis, plot_sensitivity
from python.quantum_correlation import trajectory_to_quantum_observables, plot_quantum_observables


def ensure_dirs():
    for d in ['figures/phase', 'figures/bifurcation', 'figures/lyapunov',
              'figures/foam', 'figures/basin', 'figures/quantum']:
        Path(d).mkdir(parents=True, exist_ok=True)


def main():
    print("=" * 60)
    print("QUANTUM PN NEURON DYNAMICAL SYSTEMS ANALYSIS")
    print("=" * 60)

    ensure_dirs()
    config = DynamicsConfig(lambda_a=0.1, lambda_c=0.05, f_constant=0.5)

    # 1. Phase portraits
    print("\n[1/7] Phase portraits...")
    for plane in ['ac', 'ab', 'bc']:
        fig, ax = plt.subplots(figsize=(10, 10), facecolor='#0a0a1a')
        plot_phase_portrait_2d(config, plane=plane, ax=ax)
        fig.savefig(f'figures/phase/phase_{plane}.png', facecolor='#0a0a1a', dpi=150)
        plt.close()
        print(f"    Saved phase_{plane}.png")

    fig = plt.figure(figsize=(12, 10), facecolor='#0a0a1a')
    ax = fig.add_subplot(111, projection='3d', facecolor='#0a0a1a')
    plot_phase_portrait_3d(config, ax=ax)
    fig.savefig('figures/phase/phase_3d.png', facecolor='#0a0a1a', dpi=150)
    plt.close()
    print("    Saved phase_3d.png")

    # 2. Fixed point analysis
    print("\n[2/7] Fixed point analysis...")
    fps = find_fixed_points(config)
    classifications = [classify_fixed_point(fp['point'], config) for fp in fps]

    for i, cl in enumerate(classifications):
        print(f"    FP{i+1}: {cl['point']}")
        print(f"         Type: {cl['type']}, Stability: {cl['stability']}")
        print(f"         Eigenvalues: {cl['eigenvalues']}")

    fig, ax = plt.subplots(figsize=(8, 8), facecolor='#0a0a1a')
    plot_fixed_point_eigenspectrum(classifications, ax=ax)
    fig.savefig('figures/phase/eigenspectrum.png', facecolor='#0a0a1a', dpi=150)
    plt.close()

    # Continuation
    cont_results = fixed_point_parameter_continuation('f_constant', (0.1, 1.0), n_values=50, base_config=config)
    fig = plot_continuation(cont_results)
    fig.savefig('figures/phase/continuation_f.png', facecolor='#0a0a1a', dpi=150)
    plt.close()
    print("    Saved eigenspectrum.png, continuation_f.png")

    # 3. Bifurcation analysis
    print("\n[3/7] Bifurcation diagrams...")
    for param in ['lambda_a', 'f_constant']:
        prange = (0.01, 0.5) if 'lambda' in param else (0.1, 2.0)
        results = bifurcation_1d(param, prange, n_values=80, base_config=config)
        fig, ax = plt.subplots(figsize=(12, 8), facecolor='#0a0a1a')
        plot_bifurcation_1d(results, ax=ax)
        fig.savefig(f'figures/bifurcation/bif_{param}.png', facecolor='#0a0a1a', dpi=150)
        plt.close()
        print(f"    Saved bif_{param}.png")

    # 2D bifurcation
    results_2d = bifurcation_2d('lambda_a', (0.01, 0.3), 'f_constant', (0.1, 1.0),
                                 resolution=40, base_config=config)
    fig, ax = plot_bifurcation_2d(results_2d)
    fig.savefig('figures/bifurcation/bif_2d.png', facecolor='#0a0a1a', dpi=150)
    plt.close()
    print("    Saved bif_2d.png")

    # 4. Lyapunov exponents
    print("\n[4/7] Lyapunov analysis...")
    initial = np.array([0.5, 1.0, 0.5])
    exponents, history = compute_lyapunov_spectrum(config, initial, n_steps=30000)
    print(f"    Lyapunov exponents: {exponents}")
    print(f"    Classification: {classify_dynamics(exponents)}")
    print(f"    K-Y dimension: {kaplan_yorke_dimension(exponents):.3f}")

    fig, ax = plt.subplots(figsize=(10, 6), facecolor='#0a0a1a')
    plot_lyapunov_convergence(history, config, ax=ax)
    fig.savefig('figures/lyapunov/convergence.png', facecolor='#0a0a1a', dpi=150)
    plt.close()
    print("    Saved convergence.png")

    # 5. Basin of attraction
    print("\n[5/7] Basin of attraction...")
    basin_results = compute_basin_2d(config, plane='ac', resolution=50, t_max=30.0)
    fig, ax = plt.subplots(figsize=(10, 10), facecolor='#0a0a1a')
    plot_basin(basin_results, ax=ax)
    fig.savefig('figures/basin/basin_ac.png', facecolor='#0a0a1a', dpi=150)
    plt.close()
    print("    Saved basin_ac.png")

    # Sensitivity analysis
    sens_results = sensitivity_analysis(config, initial, n_perturbations=50, epsilon=0.01, t_max=20.0)
    fig, ax = plt.subplots(figsize=(10, 6), facecolor='#0a0a1a')
    plot_sensitivity(sens_results, ax=ax)
    fig.savefig('figures/basin/sensitivity.png', facecolor='#0a0a1a', dpi=150)
    plt.close()
    print("    Saved sensitivity.png")

    # 6. Foam analysis
    print("\n[6/7] Foam boundary analysis...")
    traj = integrate_rk4(initial, config, n_steps=50000)
    fig = plot_foam_analysis(traj, config)
    fig.savefig('figures/foam/foam_analysis.png', facecolor='#0a0a1a', dpi=150)
    plt.close()
    print("    Saved foam_analysis.png")

    # 7. Quantum correlations
    print("\n[7/7] Quantum observable mapping...")
    quantum_obs = trajectory_to_quantum_observables(traj[::10], config)  # Subsample for speed
    fig = plot_quantum_observables(quantum_obs)
    fig.savefig('figures/quantum/observables.png', facecolor='#0a0a1a', dpi=150)
    plt.close()
    print("    Saved observables.png")

    print("\n" + "=" * 60)
    print("COMPLETE - Results saved to figures/")
    print("=" * 60)

    # Summary
    print("\nSUMMARY:")
    print("-" * 40)
    print(f"Configuration:")
    print(f"  lambda_a = {config.lambda_a}")
    print(f"  lambda_c = {config.lambda_c}")
    print(f"  f = {config.f_constant}")
    print(f"\nFixed Points: {len(fps)}")
    for i, cl in enumerate(classifications):
        print(f"  FP{i+1}: {cl['type']} ({cl['stability']})")
    print(f"\nDynamics: {classify_dynamics(exponents)}")
    print(f"Kaplan-Yorke dimension: {kaplan_yorke_dimension(exponents):.3f}")


if __name__ == '__main__':
    main()
