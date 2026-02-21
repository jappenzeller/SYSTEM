"""
E-I Balance Explorer - Tracks when |a - c| approaches zero.

Key Discovery: Fractal complexity peaks when a ≈ c (E-I balance),
NOT when concurrence is highest. This suggests maximum quantum
interference occurs at the E-I balance point.

Features:
- |a - c| balance metric with color-coded display
- Balance indicator bar (centered meter)
- a = c diagonal on parameter space plot
- Balance event detection and logging
- Time series of |a - c|

Controls:
  Space      - Play/Pause
  Left/Right - Step frame
  1-5        - Speed (0.25x to 4x)
  B          - Jump to next balance crossing
"""

import numpy as np
import matplotlib
matplotlib.use('TkAgg')
import matplotlib.pyplot as plt
from matplotlib.animation import FuncAnimation
from matplotlib.widgets import Slider, Button
from matplotlib.patches import Circle
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent))

from python.quantum_state import bloch_to_unity_coords
from python.fractal_generator import julia_set
from python.pn_dynamics import PNDynamics, PNConfig, generate_sine_input
from python.agate_circuit import create_agate_circuit
from python.quantum_state import extract_visualization_data


# === Balance Tracking Constants ===
# Empirically discovered: inflection point at |a-c| = 0.01

BALANCE_THRESHOLD_PERFECT = 0.01     # Inflection point - quantum interference maximum
BALANCE_THRESHOLD_TRANSITION = 0.02  # Transition zone - interference collapsing
BALANCE_THRESHOLD_NEAR = 0.03        # Near balance - still some structure
BALANCE_THRESHOLD_LOOSE = 0.05       # Approaching - mostly dominated

BALANCE_COLORS = {
    'perfect': '#00ff00',     # Bright green - maximum interference
    'transition': '#88ff88',  # Light green - collapsing
    'near': '#ffff00',        # Yellow - weak interference
    'approaching': '#ff8800', # Orange - nearly dominated
    'normal': '#666666'       # Gray - one side dominates
}


# === Balance Metric Functions ===

def compute_balance_metric(a, c):
    """
    Compute E-I balance metrics.

    Empirically discovered: inflection point at |a-c| = 0.01
    Below this threshold: maximum quantum interference, complex fractals
    Above this threshold: interference collapses, simple fractals
    """
    diff = a - c
    dist = abs(diff)

    # Interference strength: exponential decay from balance point
    # At dist=0: strength=1.0, at dist=0.01: strength~0.37, at dist=0.03: strength~0.05
    interference_strength = np.exp(-dist / 0.01)

    if dist < BALANCE_THRESHOLD_PERFECT:
        status = 'perfect'
        dominant = 'BALANCED'
    elif dist < BALANCE_THRESHOLD_TRANSITION:
        status = 'transition'
        dominant = 'E' if diff > 0 else 'I'
    elif dist < BALANCE_THRESHOLD_NEAR:
        status = 'near'
        dominant = 'E' if diff > 0 else 'I'
    elif dist < BALANCE_THRESHOLD_LOOSE:
        status = 'approaching'
        dominant = 'E' if diff > 0 else 'I'
    else:
        status = 'normal'
        dominant = 'E' if diff > 0 else 'I'

    return {
        'difference': diff,
        'distance': dist,
        'status': status,
        'color': BALANCE_COLORS[status],
        'dominant': dominant,
        'interference_strength': interference_strength,
        'at_inflection': dist < BALANCE_THRESHOLD_PERFECT
    }


def detect_balance_crossings(a_history, c_history):
    """Detect frames where trajectory crosses a = c line."""
    diff = a_history - c_history
    crossings = []

    for i in range(1, len(diff)):
        if diff[i-1] * diff[i] < 0:  # Sign change
            direction = 'E→I' if diff[i-1] > 0 else 'I→E'
            crossings.append({
                'frame': i,
                'direction': direction,
                'a': a_history[i],
                'c': c_history[i]
            })

    return crossings


def find_balance_events(a_history, c_history, threshold=None):
    """
    Find contiguous regions where |a - c| < threshold.

    Default threshold is BALANCE_THRESHOLD_PERFECT (0.01) - the empirically
    discovered inflection point where quantum interference is maximum.
    """
    if threshold is None:
        threshold = BALANCE_THRESHOLD_PERFECT

    dist = np.abs(a_history - c_history)
    in_balance = dist < threshold

    events = []
    start = None

    for i, is_balanced in enumerate(in_balance):
        if is_balanced and start is None:
            start = i
        elif not is_balanced and start is not None:
            min_dist = np.min(dist[start:i])
            min_idx = start + np.argmin(dist[start:i])
            events.append({
                'start_frame': start,
                'end_frame': i - 1,
                'duration': i - start,
                'min_distance': min_dist,
                'min_frame': min_idx,
                'interference_at_min': np.exp(-min_dist / 0.01)
            })
            start = None

    if start is not None:
        min_dist = np.min(dist[start:])
        min_idx = start + np.argmin(dist[start:])
        events.append({
            'start_frame': start,
            'end_frame': len(dist) - 1,
            'duration': len(dist) - start,
            'min_distance': min_dist,
            'min_frame': min_idx,
            'interference_at_min': np.exp(-min_dist / 0.01)
        })

    return events


def run_balance_explorer(duration=10.0, frequency=0.2):
    print('=' * 60)
    print('E-I BALANCE EXPLORER')
    print('=' * 60)
    print()
    print('EMPIRICAL FINDING:')
    print('  Inflection point at |a - c| = 0.01')
    print('  Below: Maximum quantum interference (complex fractals)')
    print('  Above: Interference collapses (simple fractals)')
    print()
    print('Watch for:')
    print('  - GREEN GLOW when |a - c| < 0.01 (max interference)')
    print('  - Interference strength bar (100% at perfect balance)')
    print('  - Complex fractals in the green zone')
    print('=' * 60)
    print()
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

    # Detect balance events
    crossings = detect_balance_crossings(history['a'], history['c'])
    events = find_balance_events(history['a'], history['c'])  # Uses 0.01 threshold

    print(f'\nReady: {n_frames} frames')
    print(f'Detected {len(crossings)} balance crossings')
    print(f'Detected {len(events)} perfect balance events (|a-c| < 0.01)')

    if events:
        print('\nMax interference regions:')
        for i, ev in enumerate(events[:5]):
            duration_ms = ev['duration'] * 10  # 10ms per frame at dt=0.01
            print(f'  {i+1}. Frames {ev["start_frame"]}-{ev["end_frame"]} '
                  f'({duration_ms}ms, min |a-c| = {ev["min_distance"]:.5f}, '
                  f'interference = {ev["interference_at_min"]:.0%})')
        if events:
            print(f'\nTIP: Jump to frame {events[0]["start_frame"]} to see max interference!')

    print('\nControls: Space=Play, Arrows=Step, 1-5=Speed, B=Next Balance')

    # Setup figure with balance-focused layout
    plt.style.use('dark_background')
    fig = plt.figure(figsize=(18, 11), facecolor='#0a0a1a')

    # Grid layout
    gs = fig.add_gridspec(4, 4,
                          width_ratios=[1, 1, 1, 1.5],
                          height_ratios=[2, 1.2, 1, 0.6],
                          hspace=0.35, wspace=0.3)

    # Row 0: Bloch spheres and fractal
    ax_E = fig.add_subplot(gs[0, 0], projection='3d', facecolor='#0a0a1a')
    ax_I = fig.add_subplot(gs[0, 1], projection='3d', facecolor='#0a0a1a')
    ax_fractal = fig.add_subplot(gs[0:2, 2:4], facecolor='#0a0a1a')  # Large fractal

    # Row 1: Balance meter and parameter space
    ax_balance = fig.add_subplot(gs[1, 0:2], facecolor='#0a0a1a')  # Balance meter

    # Row 2: Input signal and balance time series
    ax_input = fig.add_subplot(gs[2, 0:2], facecolor='#0a0a1a')
    ax_param_space = fig.add_subplot(gs[2, 2:4], facecolor='#0a0a1a')  # a-c space with diagonal

    # Row 3: Timeline
    ax_timeline = fig.add_subplot(gs[3, :], facecolor='#1a1a2e')

    # Playback state
    state = {'playing': False, 'frame': 0, 'speed': 1.0}
    trail_length = 100
    animation_handle = [None]

    def draw_bloch_with_trail(ax, frame_idx, qubit):
        ax.clear()
        ax.set_facecolor('#0a0a1a')

        key = 'bloch_E' if qubit == 'E' else 'bloch_I'
        color = '#ff6b35' if qubit == 'E' else '#7b68ee'

        # Circle outlines
        theta = np.linspace(0, 2 * np.pi, 80)
        ax.plot(np.cos(theta), np.zeros_like(theta), np.sin(theta), color='#444466', alpha=0.5, linewidth=1)
        ax.plot(np.cos(theta), np.sin(theta), np.zeros_like(theta), color='#444466', alpha=0.5, linewidth=1)
        ax.plot(np.zeros_like(theta), np.cos(theta), np.sin(theta), color='#444466', alpha=0.5, linewidth=1)

        # Axes
        ax.quiver(0, 0, 0, 1.15, 0, 0, color='red', alpha=0.6, arrow_length_ratio=0.07, linewidth=1.2)
        ax.quiver(0, 0, 0, 0, 1.15, 0, color='green', alpha=0.6, arrow_length_ratio=0.07, linewidth=1.2)
        ax.quiver(0, 0, 0, 0, 0, 1.15, color='blue', alpha=0.6, arrow_length_ratio=0.07, linewidth=1.2)

        # Trail
        start = max(0, frame_idx - trail_length)
        trail_raw = history[key][start:frame_idx + 1]
        if len(trail_raw) > 1:
            trail = np.array([bloch_to_unity_coords(tuple(v)) for v in trail_raw])
            for j in range(len(trail) - 1):
                alpha = 0.15 + 0.75 * (j / len(trail))
                width = 0.8 + 2.0 * (j / len(trail))
                ax.plot3D(trail[j:j + 2, 0], trail[j:j + 2, 1], trail[j:j + 2, 2],
                          color=color, alpha=alpha, linewidth=width)

        # Current state
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

    def draw_balance_meter(ax, a, c):
        """Draw the E-I balance indicator with interference strength."""
        ax.clear()
        ax.set_facecolor('#0a0a1a')
        ax.set_xlim(-0.7, 0.7)
        ax.set_ylim(-0.6, 0.85)
        ax.axis('off')

        balance = compute_balance_metric(a, c)
        diff = balance['difference']
        dist = balance['distance']
        strength = balance['interference_strength']

        # Background track
        ax.plot([-0.5, 0.5], [0, 0], color='#333333', linewidth=15, solid_capstyle='round')

        # Inflection zone indicator (the critical |a-c| < 0.01 region)
        inflection_half_width = 0.01 * 2  # Scaled for display
        ax.fill_between([-inflection_half_width, inflection_half_width],
                        -0.06, 0.06, color='#00ff00', alpha=0.25)

        # Tick marks
        for x in [-0.5, -0.25, 0, 0.25, 0.5]:
            ax.plot([x, x], [-0.08, 0.08], color='#555555', linewidth=1)

        # Center marker (balance point)
        ax.plot([0], [0], 'o', color='white', markersize=8, zorder=5)

        # Current position indicator
        pos_x = np.clip(diff * 2, -0.5, 0.5)  # Scale for display

        # Glow effect when in inflection zone
        if balance['status'] == 'perfect':
            for r, alpha in [(0.15, 0.15), (0.10, 0.25), (0.06, 0.35)]:
                circle = plt.Circle((pos_x, 0), r, color='#00ff00', alpha=alpha, zorder=4)
                ax.add_patch(circle)
        elif balance['status'] == 'transition':
            circle = plt.Circle((pos_x, 0), 0.08, color='#88ff88', alpha=0.2, zorder=4)
            ax.add_patch(circle)

        ax.plot([pos_x], [0], 'o', color=balance['color'], markersize=25, zorder=10)
        ax.plot([pos_x], [0], 'o', color='white', markersize=12, zorder=11)

        # Labels
        ax.text(-0.5, 0.35, 'E dominant', color='#ff6b35', fontsize=10, ha='center', fontweight='bold')
        if balance['status'] == 'perfect':
            ax.text(0, 0.35, 'MAX INTERFERENCE', color='#00ff00', fontsize=10, ha='center', fontweight='bold')
        else:
            ax.text(0, 0.35, 'BALANCED', color='white', fontsize=10, ha='center')
        ax.text(0.5, 0.35, 'I dominant', color='#7b68ee', fontsize=10, ha='center', fontweight='bold')

        # Value display
        ax.text(0, -0.20, f'|a - c| = {dist:.4f}', color=balance['color'],
                fontsize=14, ha='center', family='monospace', fontweight='bold')

        # Interference strength bar
        bar_width = 0.5
        bar_height = 0.045
        bar_y = -0.35

        # Background
        ax.add_patch(plt.Rectangle((-bar_width/2, bar_y), bar_width, bar_height,
                                   facecolor='#333333', edgecolor='none'))
        # Filled portion (green gradient based on strength)
        fill_color = plt.cm.Greens(0.3 + 0.7 * strength)
        ax.add_patch(plt.Rectangle((-bar_width/2, bar_y), bar_width * strength, bar_height,
                                   facecolor=fill_color, edgecolor='none'))
        ax.text(0, bar_y - 0.12, f'Interference: {strength:.0%}',
                color='white', fontsize=10, ha='center')

        # Status text
        status_text = {
            'perfect': 'MAX QUANTUM INTERFERENCE',
            'transition': '~ Collapsing ~',
            'near': 'Weak Interference',
            'approaching': 'Approaching...',
            'normal': f'{balance["dominant"]} Dominant'
        }
        status_color = '#00ff00' if balance['status'] == 'perfect' else balance['color']
        ax.text(0, 0.70, status_text[balance['status']], color=status_color,
                fontsize=13, ha='center', fontweight='bold')

    def draw_parameter_space(ax, frame_idx):
        """Draw a-c parameter space with a=c diagonal."""
        ax.clear()
        ax.set_facecolor('#0a0a1a')

        # a = c diagonal (THE KEY LINE)
        ax.plot([0, 1], [0, 1], '--', color='#00ff00', linewidth=2.5,
                alpha=0.8, label='a = c (balance)', zorder=1)

        # Fill regions
        ax.fill_between([0, 1], [0, 1], [0, 0], color='#ff6b35', alpha=0.08)
        ax.text(0.75, 0.15, 'E > I', color='#ff6b35', fontsize=12, alpha=0.6)
        ax.fill_between([0, 1], [0, 1], [1, 1], color='#7b68ee', alpha=0.08)
        ax.text(0.15, 0.85, 'I > E', color='#7b68ee', fontsize=12, alpha=0.6)

        # Trajectory
        lookback = 300
        start = max(0, frame_idx - lookback)
        a_traj = history['a'][start:frame_idx + 1]
        c_traj = history['c'][start:frame_idx + 1]

        if len(a_traj) > 1:
            # Color by distance to diagonal
            distances = np.abs(a_traj - c_traj)
            norm_dist = 1 - np.clip(distances / 0.3, 0, 1)

            for j in range(len(a_traj) - 1):
                alpha = 0.2 + 0.7 * (j / len(a_traj))
                color = plt.cm.plasma(norm_dist[j])
                ax.plot(a_traj[j:j + 2], c_traj[j:j + 2], color=color, alpha=alpha, linewidth=1.5)

        # Current position
        a_now = history['a'][frame_idx]
        c_now = history['c'][frame_idx]
        balance = compute_balance_metric(a_now, c_now)

        # Glow when at/near inflection point
        if balance['status'] == 'perfect':
            ax.scatter([a_now], [c_now], color=balance['color'], s=500, alpha=0.4, zorder=8)
        elif balance['status'] == 'transition':
            ax.scatter([a_now], [c_now], color=balance['color'], s=350, alpha=0.3, zorder=8)

        ax.scatter([a_now], [c_now], color=balance['color'], s=150,
                   edgecolors='white', linewidths=2, zorder=10)

        # Mark visible crossings
        for cr in crossings:
            if start <= cr['frame'] <= frame_idx:
                ax.scatter([cr['a']], [cr['c']], color='#00ff00', s=80, marker='*', zorder=11)

        ax.set_xlim([0, 1])
        ax.set_ylim([0, 1])
        ax.set_xlabel('a (Excitatory)', color='white', fontsize=10)
        ax.set_ylabel('c (Inhibitory)', color='white', fontsize=10)
        ax.set_title('Parameter Space (color = distance to balance)', color='white', fontsize=11)
        ax.tick_params(colors='white', labelsize=8)
        ax.set_aspect('equal')
        ax.legend(loc='upper left', bbox_to_anchor=(0.0, -0.12),
                  facecolor='#1a1a2e', labelcolor='white', fontsize=9, framealpha=0.9)

    def update_display(frame_idx):
        # Bloch spheres
        draw_bloch_with_trail(ax_E, frame_idx, 'E')
        draw_bloch_with_trail(ax_I, frame_idx, 'I')

        # Fractal
        ax_fractal.clear()
        ax_fractal.set_facecolor('#0a0a1a')
        sv = history['statevector'][frame_idx]
        weights = np.abs(sv)
        if np.sum(weights) > 1e-10:
            c_julia = np.sum(sv * weights) / np.sum(weights) * 0.8
        else:
            c_julia = 0
        fractal = julia_set(c_julia, width=500, height=500, max_iter=100)
        ax_fractal.imshow(fractal, cmap='magma', origin='lower', aspect='equal')

        # Balance info in fractal title
        a_now = history['a'][frame_idx]
        c_now = history['c'][frame_idx]
        balance = compute_balance_metric(a_now, c_now)
        conc = history['concurrence'][frame_idx]

        title_color = balance['color'] if balance['status'] in ['perfect', 'near'] else 'white'
        ax_fractal.set_title(
            f'Quantum Fractal | |a-c| = {balance["distance"]:.4f} | Concurrence = {conc:.4f}',
            color=title_color, fontsize=12)
        ax_fractal.axis('off')

        # Balance meter
        draw_balance_meter(ax_balance, a_now, c_now)

        # Input signal with balance zones
        ax_input.clear()
        ax_input.set_facecolor('#0a0a1a')

        # Highlight perfect balance zones (|a-c| < 0.01) on input
        balance_dist = np.abs(history['a'] - history['c'])
        in_perfect = balance_dist < BALANCE_THRESHOLD_PERFECT
        ax_input.fill_between(history['t'], -0.2, 1.2, where=in_perfect,
                              alpha=0.35, color='#00ff00', label='Max interference')

        ax_input.fill_between(history['t'], 0, history['f'], alpha=0.3, color='cyan')
        ax_input.plot(history['t'], history['f'], color='cyan', linewidth=1.5)
        ax_input.axvline(history['t'][frame_idx], color='yellow', linewidth=2, alpha=0.9)
        ax_input.scatter([history['t'][frame_idx]], [history['f'][frame_idx]],
                         color='yellow', s=80, zorder=10, edgecolors='white')

        # Mark crossings
        for cr in crossings:
            ax_input.axvline(history['t'][cr['frame']], color='#00ff00', alpha=0.5, linewidth=1)

        ax_input.set_xlim([0, duration])
        ax_input.set_ylim([-0.2, 1.2])
        ax_input.set_xlabel('Time (s)', color='white', fontsize=9)
        ax_input.set_ylabel('f(t)', color='white', fontsize=9)
        ax_input.set_title('Input Signal (green = |a-c|<0.01 max interference)', color='white', fontsize=10)
        ax_input.tick_params(colors='white', labelsize=7)

        # Parameter space with diagonal
        draw_parameter_space(ax_param_space, frame_idx)

        # Frame text
        frame_text.set_text(
            f'Frame {frame_idx}/{n_frames} | t = {history["t"][frame_idx]:.2f}s | '
            f'a = {a_now:.3f} | c = {c_now:.3f} | Speed: {state["speed"]}x')

        fig.canvas.draw_idle()

    # Timeline
    timeline = Slider(ax_timeline, '', 0, n_frames - 1, valinit=0, valstep=1, color='#7b68ee')

    def on_timeline(val):
        state['frame'] = int(val)
        update_display(state['frame'])

    timeline.on_changed(on_timeline)

    # Transport buttons
    btn_color = '#2a2a4a'
    btn_hover = '#3a3a5a'
    btn_y = 0.02
    btn_h = 0.035
    btn_w = 0.055

    ax_start = plt.axes([0.12, btn_y, btn_w, btn_h])
    ax_back = plt.axes([0.18, btn_y, btn_w, btn_h])
    ax_play = plt.axes([0.24, btn_y, btn_w, btn_h])
    ax_forward = plt.axes([0.30, btn_y, btn_w, btn_h])
    ax_end = plt.axes([0.36, btn_y, btn_w, btn_h])
    ax_balance_btn = plt.axes([0.44, btn_y, 0.08, btn_h])

    btn_start = Button(ax_start, '|<', color=btn_color, hovercolor=btn_hover)
    btn_back = Button(ax_back, '<<', color=btn_color, hovercolor=btn_hover)
    btn_play = Button(ax_play, '>', color=btn_color, hovercolor=btn_hover)
    btn_forward = Button(ax_forward, '>>', color=btn_color, hovercolor=btn_hover)
    btn_end = Button(ax_end, '>|', color=btn_color, hovercolor=btn_hover)
    btn_balance = Button(ax_balance_btn, 'Next [B]', color='#2a4a2a', hovercolor='#3a6a3a')

    # Speed buttons
    ax_speed1 = plt.axes([0.58, btn_y, 0.04, btn_h])
    ax_speed2 = plt.axes([0.63, btn_y, 0.04, btn_h])
    ax_speed3 = plt.axes([0.68, btn_y, 0.04, btn_h])
    ax_speed4 = plt.axes([0.73, btn_y, 0.04, btn_h])
    ax_speed5 = plt.axes([0.78, btn_y, 0.04, btn_h])

    btn_speed1 = Button(ax_speed1, '.25x', color=btn_color, hovercolor=btn_hover)
    btn_speed2 = Button(ax_speed2, '.5x', color=btn_color, hovercolor=btn_hover)
    btn_speed3 = Button(ax_speed3, '1x', color='#4a4a6a', hovercolor=btn_hover)
    btn_speed4 = Button(ax_speed4, '2x', color=btn_color, hovercolor=btn_hover)
    btn_speed5 = Button(ax_speed5, '4x', color=btn_color, hovercolor=btn_hover)

    speed_buttons = [btn_speed1, btn_speed2, btn_speed3, btn_speed4, btn_speed5]
    speed_values = [0.25, 0.5, 1.0, 2.0, 4.0]

    def update_speed_buttons():
        for i, btn in enumerate(speed_buttons):
            btn.ax.set_facecolor('#4a4a6a' if speed_values[i] == state['speed'] else btn_color)
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

    def on_next_balance(event):
        """Jump to next balance crossing."""
        current = state['frame']
        for cr in crossings:
            if cr['frame'] > current:
                state['frame'] = cr['frame']
                timeline.set_val(state['frame'])
                print(f'Jumped to balance crossing at frame {cr["frame"]} ({cr["direction"]})')
                return
        # Wrap around
        if crossings:
            state['frame'] = crossings[0]['frame']
            timeline.set_val(state['frame'])
            print(f'Wrapped to first crossing at frame {crossings[0]["frame"]}')

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
    btn_balance.on_clicked(on_next_balance)

    for i in range(5):
        speed_buttons[i].on_clicked(make_speed_handler(i))

    # Frame text
    frame_text = fig.text(0.5, 0.065, '', ha='center', color='white', fontsize=10, family='monospace')

    # Keyboard controls
    def on_key(event):
        if event.key == ' ':
            on_play(None)
        elif event.key == 'right':
            state['frame'] = min(state['frame'] + 1, n_frames - 1)
            timeline.set_val(state['frame'])
        elif event.key == 'left':
            state['frame'] = max(state['frame'] - 1, 0)
            timeline.set_val(state['frame'])
        elif event.key == 'b':
            on_next_balance(None)
        elif event.key in '12345':
            make_speed_handler(int(event.key) - 1)(None)

    fig.canvas.mpl_connect('key_press_event', on_key)

    update_display(0)
    plt.tight_layout()
    plt.subplots_adjust(bottom=0.11)
    plt.show()


if __name__ == '__main__':
    run_balance_explorer(duration=10.0, frequency=0.2)
