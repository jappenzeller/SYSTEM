"""
Bloch Sphere Explorer with large fractal display and traced paths.

Layout:
- Left: Two Bloch spheres with circle outlines and traced paths
- Right: Large fractal (half screen)
- Bottom left: Sine wave input display
- Bottom: Timeline with playback controls

Controls:
  Space      - Play/Pause
  Left/Right - Step frame
  1-5        - Speed (0.25x to 4x)
"""

import numpy as np
import matplotlib
matplotlib.use('TkAgg')
import matplotlib.pyplot as plt
from matplotlib.animation import FuncAnimation
from matplotlib.widgets import Slider, Button
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent))

from python.quantum_state import bloch_to_unity_coords
from python.fractal_generator import julia_set
from python.pn_dynamics import PNDynamics, PNConfig, generate_sine_input
from python.agate_circuit import create_agate_circuit
from python.quantum_state import extract_visualization_data


def run_explorer(duration=10.0, frequency=0.2):
    print('Pre-computing quantum states...')

    # Generate sine wave input
    dt = 0.01
    signal = generate_sine_input(duration, frequency, amplitude=0.5, offset=0.4, dt=dt)

    # Pre-compute all states
    config = PNConfig(lambda_a=0.08, lambda_c=0.03, dt=dt)
    pn = PNDynamics(config)

    n_frames = len(signal)
    history = {
        'a': np.zeros(n_frames),
        'b': np.zeros(n_frames),
        'c': np.zeros(n_frames),
        'f': signal,
        't': np.arange(n_frames) * dt,
        'bloch_E': np.zeros((n_frames, 3)),
        'bloch_I': np.zeros((n_frames, 3)),
        'concurrence': np.zeros(n_frames),
        'statevector': np.zeros((n_frames, 4), dtype=complex),
    }

    for i, f_t in enumerate(signal):
        a, b, c = pn.step(f_t)
        history['a'][i] = a
        history['b'][i] = b
        history['c'][i] = c
        circuit = create_agate_circuit(a, b, c)
        viz = extract_visualization_data(circuit)
        history['bloch_E'][i] = viz['bloch_E']
        history['bloch_I'][i] = viz['bloch_I']
        history['concurrence'][i] = viz['concurrence']
        history['statevector'][i] = viz['statevector']
        if (i + 1) % 200 == 0:
            print(f'  {i+1}/{n_frames} frames...')

    print(f'Ready: {n_frames} frames')
    print('Controls: Space=Play/Pause, Arrows=Step, 1-5=Speed')

    # Setup figure
    plt.style.use('dark_background')
    fig = plt.figure(figsize=(18, 9), facecolor='#0a0a1a')

    # Grid: left side spheres + charts, right side large fractal
    gs = fig.add_gridspec(3, 3, width_ratios=[1, 1, 1.8], height_ratios=[2, 1.5, 0.8],
                          hspace=0.3, wspace=0.25)

    ax_E = fig.add_subplot(gs[0, 0], projection='3d', facecolor='#0a0a1a')
    ax_I = fig.add_subplot(gs[0, 1], projection='3d', facecolor='#0a0a1a')
    ax_fractal = fig.add_subplot(gs[0:2, 2], facecolor='#0a0a1a')  # Large, spans 2 rows
    ax_input = fig.add_subplot(gs[1, 0], facecolor='#0a0a1a')      # Sine wave
    ax_params = fig.add_subplot(gs[1, 1], facecolor='#0a0a1a')     # Compact params
    ax_timeline = fig.add_subplot(gs[2, :], facecolor='#1a1a2e')   # Full width

    # Playback state
    state = {'playing': False, 'frame': 0, 'speed': 1.0}
    trail_length = 100
    animation_handle = [None]

    def draw_bloch_with_trail(ax, frame_idx, qubit):
        ax.clear()
        ax.set_facecolor('#0a0a1a')

        key = 'bloch_E' if qubit == 'E' else 'bloch_I'
        color = '#ff6b35' if qubit == 'E' else '#7b68ee'

        # Draw sphere as three circle outlines (not wireframe)
        theta = np.linspace(0, 2 * np.pi, 80)

        # Equator (XZ plane in Unity coords - Y=0)
        ax.plot(np.cos(theta), np.zeros_like(theta), np.sin(theta),
                color='#444466', alpha=0.5, linewidth=1)
        # Prime meridian (XY plane - Z=0)
        ax.plot(np.cos(theta), np.sin(theta), np.zeros_like(theta),
                color='#444466', alpha=0.5, linewidth=1)
        # Side meridian (YZ plane - X=0)
        ax.plot(np.zeros_like(theta), np.cos(theta), np.sin(theta),
                color='#444466', alpha=0.5, linewidth=1)

        # Axes with labels
        ax.quiver(0, 0, 0, 1.15, 0, 0, color='red', alpha=0.6, arrow_length_ratio=0.07, linewidth=1.2)
        ax.quiver(0, 0, 0, 0, 1.15, 0, color='green', alpha=0.6, arrow_length_ratio=0.07, linewidth=1.2)
        ax.quiver(0, 0, 0, 0, 0, 1.15, color='blue', alpha=0.6, arrow_length_ratio=0.07, linewidth=1.2)

        # Axis labels
        ax.text(1.25, 0, 0, 'X', color='red', fontsize=8)
        ax.text(0, 1.25, 0, 'Y', color='green', fontsize=8)
        ax.text(0, 0, 1.25, 'Z', color='blue', fontsize=8)

        # Trail (traced path)
        start = max(0, frame_idx - trail_length)
        trail_raw = history[key][start:frame_idx + 1]

        if len(trail_raw) > 1:
            # Convert to Unity coords
            trail = np.array([bloch_to_unity_coords(tuple(v)) for v in trail_raw])

            # Draw trail with gradient alpha and width
            for j in range(len(trail) - 1):
                alpha = 0.15 + 0.75 * (j / len(trail))
                width = 0.8 + 2.0 * (j / len(trail))
                ax.plot3D(trail[j:j + 2, 0], trail[j:j + 2, 1], trail[j:j + 2, 2],
                          color=color, alpha=alpha, linewidth=width)

        # Current state vector
        bx, by, bz = bloch_to_unity_coords(tuple(history[key][frame_idx]))
        r = np.sqrt(bx ** 2 + by ** 2 + bz ** 2)

        ax.quiver(0, 0, 0, bx, by, bz, color=color, arrow_length_ratio=0.1, linewidth=3)
        ax.scatter([bx], [by], [bz], color=color, s=100, edgecolors='white', linewidths=2, zorder=10)

        ax.set_xlim([-1.2, 1.2])
        ax.set_ylim([-1.2, 1.2])
        ax.set_zlim([-1.2, 1.2])
        ax.set_title(f'{qubit} qubit (r={r:.2f})', color='white', fontsize=11)
        ax.tick_params(colors='white', labelsize=6)
        ax.set_box_aspect([1, 1, 1])

    def update_display(frame_idx):
        # Bloch spheres with trails
        draw_bloch_with_trail(ax_E, frame_idx, 'E')
        draw_bloch_with_trail(ax_I, frame_idx, 'I')

        # Large fractal
        ax_fractal.clear()
        ax_fractal.set_facecolor('#0a0a1a')

        # Use weighted combination for more interesting fractals
        sv = history['statevector'][frame_idx]
        weights = np.abs(sv)
        if np.sum(weights) > 1e-10:
            c_julia = np.sum(sv * weights) / np.sum(weights) * 0.8
        else:
            c_julia = 0

        fractal = julia_set(c_julia, width=500, height=500, max_iter=100)
        ax_fractal.imshow(fractal, cmap='magma', origin='lower', aspect='equal')

        conc = history['concurrence'][frame_idx]
        ax_fractal.set_title(f'Quantum Fractal | Concurrence = {conc:.4f}',
                             color='white', fontsize=13)
        ax_fractal.axis('off')

        # Sine wave input with playhead
        ax_input.clear()
        ax_input.set_facecolor('#0a0a1a')
        ax_input.fill_between(history['t'], 0, history['f'], alpha=0.3, color='cyan')
        ax_input.plot(history['t'], history['f'], color='cyan', linewidth=1.5, label='Input f(t)')
        ax_input.axvline(history['t'][frame_idx], color='yellow', linestyle='-', alpha=0.9, linewidth=2)
        ax_input.scatter([history['t'][frame_idx]], [history['f'][frame_idx]],
                         color='yellow', s=80, zorder=10, edgecolors='white', linewidths=1)
        ax_input.set_xlim([0, duration])
        ax_input.set_ylim([-0.2, 1.1])
        ax_input.set_xlabel('Time (s)', color='white', fontsize=9)
        ax_input.set_ylabel('f(t)', color='white', fontsize=9)
        ax_input.set_title('Sine Wave Input', color='white', fontsize=11)
        ax_input.tick_params(colors='white', labelsize=7)

        # Compact parameter bars
        ax_params.clear()
        ax_params.set_facecolor('#0a0a1a')
        params = ['a (E)', 'b/2pi', 'c (I)', 'Conc']
        vals = [history['a'][frame_idx],
                history['b'][frame_idx] / (2 * np.pi),
                history['c'][frame_idx],
                history['concurrence'][frame_idx]]
        colors = ['#ff6b35', '#00d084', '#7b68ee', '#ff00ff']
        bars = ax_params.barh(params, vals, color=colors, height=0.6, edgecolor='white', linewidth=0.5)
        ax_params.set_xlim([0, 1.15])
        ax_params.set_title('Parameters', color='white', fontsize=11)
        ax_params.tick_params(colors='white', labelsize=8)
        for bar, val in zip(bars, vals):
            ax_params.text(val + 0.03, bar.get_y() + bar.get_height() / 2,
                           f'{val:.3f}', va='center', color='white', fontsize=9)

        # Frame counter
        frame_text.set_text(
            f'Frame {frame_idx}/{n_frames} | t = {history["t"][frame_idx]:.2f}s | Speed: {state["speed"]}x')

        fig.canvas.draw_idle()

    # Timeline slider
    timeline = Slider(ax_timeline, '', 0, n_frames - 1, valinit=0, valstep=1, color='#7b68ee')

    def on_timeline(val):
        state['frame'] = int(val)
        update_display(state['frame'])

    timeline.on_changed(on_timeline)

    # Transport control buttons
    btn_color = '#2a2a4a'
    btn_hover = '#3a3a5a'
    btn_y = 0.02
    btn_h = 0.04
    btn_w = 0.06

    ax_start = plt.axes([0.15, btn_y, btn_w, btn_h])
    ax_back = plt.axes([0.22, btn_y, btn_w, btn_h])
    ax_play = plt.axes([0.29, btn_y, btn_w, btn_h])
    ax_forward = plt.axes([0.36, btn_y, btn_w, btn_h])
    ax_end = plt.axes([0.43, btn_y, btn_w, btn_h])

    btn_start = Button(ax_start, '|<', color=btn_color, hovercolor=btn_hover)
    btn_back = Button(ax_back, '<<', color=btn_color, hovercolor=btn_hover)
    btn_play = Button(ax_play, '>', color=btn_color, hovercolor=btn_hover)
    btn_forward = Button(ax_forward, '>>', color=btn_color, hovercolor=btn_hover)
    btn_end = Button(ax_end, '>|', color=btn_color, hovercolor=btn_hover)

    # Speed buttons
    ax_speed1 = plt.axes([0.55, btn_y, 0.04, btn_h])
    ax_speed2 = plt.axes([0.60, btn_y, 0.04, btn_h])
    ax_speed3 = plt.axes([0.65, btn_y, 0.04, btn_h])
    ax_speed4 = plt.axes([0.70, btn_y, 0.04, btn_h])
    ax_speed5 = plt.axes([0.75, btn_y, 0.04, btn_h])

    btn_speed1 = Button(ax_speed1, '.25x', color=btn_color, hovercolor=btn_hover)
    btn_speed2 = Button(ax_speed2, '.5x', color=btn_color, hovercolor=btn_hover)
    btn_speed3 = Button(ax_speed3, '1x', color='#4a4a6a', hovercolor=btn_hover)  # Default selected
    btn_speed4 = Button(ax_speed4, '2x', color=btn_color, hovercolor=btn_hover)
    btn_speed5 = Button(ax_speed5, '4x', color=btn_color, hovercolor=btn_hover)

    speed_buttons = [btn_speed1, btn_speed2, btn_speed3, btn_speed4, btn_speed5]
    speed_values = [0.25, 0.5, 1.0, 2.0, 4.0]

    def update_speed_buttons():
        for i, btn in enumerate(speed_buttons):
            if speed_values[i] == state['speed']:
                btn.ax.set_facecolor('#4a4a6a')
            else:
                btn.ax.set_facecolor(btn_color)
        fig.canvas.draw_idle()

    def on_start(event):
        state['frame'] = 0
        timeline.set_val(state['frame'])

    def on_back(event):
        state['frame'] = max(0, state['frame'] - 10)
        timeline.set_val(state['frame'])

    def on_play(event):
        state['playing'] = not state['playing']
        btn_play.label.set_text('||' if state['playing'] else '>')
        if state['playing']:
            def animate(f):
                if state['playing']:
                    state['frame'] = (state['frame'] + 1) % n_frames
                    timeline.set_val(state['frame'])
            animation_handle[0] = FuncAnimation(fig, animate, interval=int(33 / state['speed']),
                                                cache_frame_data=False)
            plt.draw()
        else:
            if animation_handle[0]:
                animation_handle[0].event_source.stop()

    def on_forward(event):
        state['frame'] = min(state['frame'] + 10, n_frames - 1)
        timeline.set_val(state['frame'])

    def on_end(event):
        state['frame'] = n_frames - 1
        timeline.set_val(state['frame'])

    def make_speed_handler(speed_idx):
        def handler(event):
            state['speed'] = speed_values[speed_idx]
            update_speed_buttons()
            if state['playing']:
                if animation_handle[0]:
                    animation_handle[0].event_source.stop()
                def animate(f):
                    if state['playing']:
                        state['frame'] = (state['frame'] + 1) % n_frames
                        timeline.set_val(state['frame'])
                animation_handle[0] = FuncAnimation(fig, animate, interval=int(33 / state['speed']),
                                                    cache_frame_data=False)
                plt.draw()
            update_display(state['frame'])
        return handler

    btn_start.on_clicked(on_start)
    btn_back.on_clicked(on_back)
    btn_play.on_clicked(on_play)
    btn_forward.on_clicked(on_forward)
    btn_end.on_clicked(on_end)

    for i in range(5):
        speed_buttons[i].on_clicked(make_speed_handler(i))

    # Frame text
    frame_text = fig.text(0.5, 0.08, '', ha='center', color='white', fontsize=11, family='monospace')

    # Keyboard controls
    def on_key(event):
        if event.key == ' ':
            on_play(None)  # Use the button handler
        elif event.key == 'right':
            state['frame'] = min(state['frame'] + 1, n_frames - 1)
            timeline.set_val(state['frame'])
        elif event.key == 'left':
            state['frame'] = max(state['frame'] - 1, 0)
            timeline.set_val(state['frame'])
        elif event.key in '12345':
            idx = int(event.key) - 1
            make_speed_handler(idx)(None)  # Use the button handler

    fig.canvas.mpl_connect('key_press_event', on_key)

    update_display(0)
    plt.tight_layout()
    plt.subplots_adjust(bottom=0.12)  # Room for buttons
    plt.show()


if __name__ == '__main__':
    run_explorer(duration=10.0, frequency=0.2)
