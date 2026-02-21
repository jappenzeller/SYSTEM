"""
Interactive Matplotlib preview for quick testing.

Provides real-time visualization without Blender.
"""

import numpy as np
import matplotlib.pyplot as plt
from matplotlib.widgets import Slider, Button
from mpl_toolkits.mplot3d import Axes3D

from .agate_circuit import create_agate_circuit
from .quantum_state import extract_visualization_data, bloch_to_unity_coords
from .fractal_generator import generate_fractal_from_state


def draw_bloch_sphere(ax, bloch_coords, color='blue', label='', show_axes=True, use_unity_coords=True):
    """Draw a Bloch sphere with state vector.

    Args:
        use_unity_coords: If True, converts to SYSTEM game coordinates (Y=up, Z=forward)
    """
    ax.clear()

    # Convert to Unity/game coordinates if requested
    # Standard Bloch: Z=up (|0>/|1>), X/Y horizontal
    # Unity/Game: Y=up (|0>/|1>), X/Z horizontal
    if use_unity_coords:
        bx, by, bz = bloch_to_unity_coords(bloch_coords)
    else:
        bx, by, bz = bloch_coords

    # Sphere wireframe (in Unity coords: Y is up)
    u = np.linspace(0, 2 * np.pi, 20)
    v = np.linspace(0, np.pi, 20)
    x = np.outer(np.cos(u), np.sin(v))
    z = np.outer(np.sin(u), np.sin(v))  # Z is forward in Unity
    y = np.outer(np.ones(np.size(u)), np.cos(v))  # Y is up in Unity
    ax.plot_wireframe(x, y, z, color='gray', alpha=0.2, linewidth=0.5)

    if show_axes:
        # Axes with SYSTEM game colors: Red=X, Green=Y(up), Blue=Z(forward)
        ax.quiver(0, 0, 0, 1.2, 0, 0, color='red', alpha=0.7, arrow_length_ratio=0.08, linewidth=1.5)
        ax.quiver(0, 0, 0, 0, 1.2, 0, color='green', alpha=0.7, arrow_length_ratio=0.08, linewidth=1.5)  # Y up
        ax.quiver(0, 0, 0, 0, 0, 1.2, color='blue', alpha=0.7, arrow_length_ratio=0.08, linewidth=1.5)   # Z forward

        # Axis labels matching game convention
        ax.text(1.4, 0, 0, 'X |+>', color='red', fontsize=9)
        ax.text(-1.4, 0, 0, '|->', color='red', fontsize=9)
        ax.text(0, 1.4, 0, 'Y |0>', color='green', fontsize=10)   # Green Y is up = |0>
        ax.text(0, -1.4, 0, '|1>', color='green', fontsize=10)    # -Y is |1>
        ax.text(0, 0, 1.4, 'Z |+i>', color='blue', fontsize=9)    # Blue Z forward = |+i>
        ax.text(0, 0, -1.4, '|-i>', color='blue', fontsize=9)

    # State vector (already converted above)
    r = np.sqrt(bx**2 + by**2 + bz**2)

    if r > 0.01:
        ax.quiver(0, 0, 0, bx, by, bz, color=color, arrow_length_ratio=0.12, linewidth=2.5)
        ax.scatter([bx], [by], [bz], color=color, s=80, edgecolors='white', linewidths=1)

    ax.set_xlim([-1.5, 1.5])
    ax.set_ylim([-1.5, 1.5])
    ax.set_zlim([-1.5, 1.5])
    ax.set_xlabel('X')
    ax.set_ylabel('Y')
    ax.set_zlabel('Z')
    ax.set_title(label)

    # Equal aspect ratio
    ax.set_box_aspect([1, 1, 1])


def draw_entanglement_indicator(ax, concurrence, purity_E, purity_I):
    """Draw entanglement and purity indicators."""
    ax.clear()

    # Bar chart
    categories = ['Concurrence', 'Purity E', 'Purity I']
    values = [concurrence, purity_E, purity_I]
    colors = ['magenta', '#FF6B35', '#7B68EE']

    bars = ax.barh(categories, values, color=colors, edgecolor='white', linewidth=1)

    ax.set_xlim([0, 1])
    ax.set_xlabel('Value')
    ax.set_title('Quantum Properties')

    # Add value labels
    for bar, val in zip(bars, values):
        ax.text(val + 0.02, bar.get_y() + bar.get_height()/2,
                f'{val:.3f}', va='center', fontsize=9)


def draw_probabilities(ax, probs):
    """Draw probability distribution bar chart."""
    ax.clear()

    states = ['|00>', '|01>', '|10>', '|11>']
    colors = ['#2ecc71', '#3498db', '#e74c3c', '#9b59b6']

    bars = ax.bar(states, probs, color=colors, edgecolor='white', linewidth=1)

    ax.set_ylim([0, 1])
    ax.set_ylabel('Probability')
    ax.set_title('Measurement Probabilities')

    # Add value labels
    for bar, prob in zip(bars, probs):
        if prob > 0.01:
            ax.text(bar.get_x() + bar.get_width()/2, prob + 0.02,
                    f'{prob:.3f}', ha='center', fontsize=9)


def interactive_agate_explorer():
    """
    Launch interactive explorer with sliders for a, b, c.
    """
    # Set up figure with dark background
    plt.style.use('dark_background')
    fig = plt.figure(figsize=(16, 8))
    fig.patch.set_facecolor('#0A0A1A')

    # Subplots layout: 2 Bloch spheres + fractal on top, properties below
    ax_E = fig.add_subplot(231, projection='3d')
    ax_I = fig.add_subplot(232, projection='3d')
    ax_F = fig.add_subplot(233)
    ax_ent = fig.add_subplot(234)
    ax_prob = fig.add_subplot(235)
    ax_params = fig.add_subplot(236)

    # Set backgrounds
    for ax in [ax_E, ax_I]:
        ax.set_facecolor('#0A0A1A')
    for ax in [ax_F, ax_ent, ax_prob, ax_params]:
        ax.set_facecolor('#1a1a2e')

    # Initial parameters
    init_a, init_b, init_c = 0.5, 1.0, 0.3

    def update(val=None):
        a = slider_a.val
        b = slider_b.val
        c = slider_c.val

        # Create circuit and extract data
        circuit = create_agate_circuit(a, b, c)
        viz = extract_visualization_data(circuit)

        # Draw Bloch spheres
        draw_bloch_sphere(ax_E, viz['bloch_E'], color='#FF6B35',
                          label=f'E (q0) r={np.linalg.norm(viz["bloch_E"]):.2f}')
        draw_bloch_sphere(ax_I, viz['bloch_I'], color='#7B68EE',
                          label=f'I (q1) r={np.linalg.norm(viz["bloch_I"]):.2f}')

        # Draw fractal
        ax_F.clear()
        fractal = generate_fractal_from_state(viz['statevector'], width=200, height=200, max_iter=80)
        ax_F.imshow(fractal, cmap='magma', origin='lower', aspect='equal')
        ax_F.set_title(f'Julia Set (c = {viz["concurrence"]:.3f})')
        ax_F.axis('off')

        # Draw entanglement/purity
        draw_entanglement_indicator(ax_ent, viz['concurrence'], viz['purity_E'], viz['purity_I'])

        # Draw probabilities
        draw_probabilities(ax_prob, viz['probabilities'])

        # Draw parameter values
        ax_params.clear()
        ax_params.set_facecolor('#1a1a2e')
        param_text = f"""
Parameters:
  a (Excitatory) = {a:.4f}
  b (Phase)      = {b:.4f} ({np.degrees(b):.1f} deg)
  c (Inhibitory) = {c:.4f}

Bloch E: ({viz['bloch_E'][0]:.3f}, {viz['bloch_E'][1]:.3f}, {viz['bloch_E'][2]:.3f})
Bloch I: ({viz['bloch_I'][0]:.3f}, {viz['bloch_I'][1]:.3f}, {viz['bloch_I'][2]:.3f})

Concurrence: {viz['concurrence']:.4f}
Global Phase: {viz['global_phase']:.4f}
"""
        ax_params.text(0.05, 0.95, param_text, transform=ax_params.transAxes,
                       fontsize=10, verticalalignment='top', fontfamily='monospace',
                       color='white')
        ax_params.axis('off')
        ax_params.set_title('State Info')

        fig.canvas.draw_idle()

    # Sliders
    slider_ax_a = plt.axes([0.15, 0.06, 0.2, 0.02])
    slider_ax_b = plt.axes([0.45, 0.06, 0.2, 0.02])
    slider_ax_c = plt.axes([0.75, 0.06, 0.2, 0.02])

    slider_a = Slider(slider_ax_a, 'a (E)', 0.0, 1.0, valinit=init_a, color='#FF6B35')
    slider_b = Slider(slider_ax_b, 'b (phi)', 0.0, 2*np.pi, valinit=init_b, color='#00D084')
    slider_c = Slider(slider_ax_c, 'c (I)', 0.0, 1.0, valinit=init_c, color='#7B68EE')

    slider_a.on_changed(update)
    slider_b.on_changed(update)
    slider_c.on_changed(update)

    # Reset button
    reset_ax = plt.axes([0.02, 0.02, 0.08, 0.03])
    reset_btn = Button(reset_ax, 'Reset', color='#2a2a4a', hovercolor='#3a3a5a')

    def reset(event):
        slider_a.reset()
        slider_b.reset()
        slider_c.reset()

    reset_btn.on_clicked(reset)

    # Initial draw
    update()

    plt.tight_layout()
    plt.subplots_adjust(bottom=0.12)
    plt.show()


def animate_parameter_sweep(param: str = 'a', frames: int = 100):
    """
    Create animation of parameter sweep.

    Args:
        param: 'a', 'b', or 'c'
        frames: Number of frames
    """
    from matplotlib.animation import FuncAnimation

    plt.style.use('dark_background')
    fig = plt.figure(figsize=(12, 5))

    ax_E = fig.add_subplot(131, projection='3d')
    ax_I = fig.add_subplot(132, projection='3d')
    ax_F = fig.add_subplot(133)

    # Parameter sweep values
    if param == 'a':
        values = np.linspace(0, 1, frames)
        fixed = {'b': np.pi/2, 'c': 0.3}
    elif param == 'b':
        values = np.linspace(0, 2*np.pi, frames)
        fixed = {'a': 0.5, 'c': 0.3}
    else:  # c
        values = np.linspace(0, 1, frames)
        fixed = {'a': 0.5, 'b': np.pi/2}

    def animate(i):
        params = fixed.copy()
        params[param] = values[i]

        circuit = create_agate_circuit(**params)
        viz = extract_visualization_data(circuit)

        draw_bloch_sphere(ax_E, viz['bloch_E'], color='#FF6B35', label=f'E (q0)')
        draw_bloch_sphere(ax_I, viz['bloch_I'], color='#7B68EE', label=f'I (q1)')

        ax_F.clear()
        fractal = generate_fractal_from_state(viz['statevector'], width=150, height=150)
        ax_F.imshow(fractal, cmap='magma', origin='lower')
        ax_F.set_title(f'{param} = {values[i]:.3f}')
        ax_F.axis('off')

        return []

    anim = FuncAnimation(fig, animate, frames=frames, interval=50, blit=False)
    plt.tight_layout()
    plt.show()

    return anim


if __name__ == '__main__':
    import sys

    if len(sys.argv) > 1 and sys.argv[1] == 'animate':
        param = sys.argv[2] if len(sys.argv) > 2 else 'a'
        animate_parameter_sweep(param)
    else:
        interactive_agate_explorer()
