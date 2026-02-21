"""
Enhanced Quantum Neuron Animation Explorer with full playback controls.

Features:
- Play/Pause/Step controls
- Variable speed playback (0.1x to 4x)
- Timeline scrubbing
- Frame bookmarking with notes
- Screenshot export
- State export for Blender
- Keyboard shortcuts

Controls:
  Space    - Play/Pause
  Left/Right or ,/. - Step frame
  Home/End - Jump to start/end
  1-6      - Speed presets (0.1x to 4x)
  M        - Bookmark frame
  S        - Screenshot
"""

import numpy as np
import matplotlib.pyplot as plt
from matplotlib.widgets import Slider, Button, RadioButtons
from matplotlib.animation import FuncAnimation
from mpl_toolkits.mplot3d import Axes3D
from dataclasses import dataclass, field
from typing import List, Dict, Optional
from datetime import datetime
import json
from pathlib import Path

# Import existing modules
from .agate_circuit import create_agate_circuit
from .quantum_state import extract_visualization_data, bloch_to_unity_coords
from .fractal_generator import generate_fractal_from_state
from .pn_dynamics import PNDynamics, PNConfig, generate_sine_input


@dataclass
class Bookmark:
    """A saved frame state for later analysis."""
    frame: int
    time: float
    a: float
    b: float
    c: float
    concurrence: float
    note: str = ""
    timestamp: str = field(default_factory=lambda: datetime.now().isoformat())


@dataclass
class PlaybackState:
    """Current state of the animation playback."""
    playing: bool = False
    current_frame: int = 0
    total_frames: int = 0
    speed: float = 1.0

    # Animation data
    history: Dict = field(default_factory=dict)

    # Bookmarks
    bookmarks: List[Bookmark] = field(default_factory=list)

    # Display options
    show_trajectory: bool = True
    trajectory_length: int = 50  # Frames of history to show


class QuantumNeuronExplorer:
    """
    Interactive explorer for quantum PN neuron dynamics.

    Provides full playback controls for analyzing patterns in:
    - Bloch sphere trajectories
    - Entanglement dynamics
    - Fractal evolution
    - Probability distributions
    """

    # Speed presets
    SPEEDS = [0.1, 0.25, 0.5, 1.0, 2.0, 4.0]

    def __init__(self, input_signal: np.ndarray, config: Optional[PNConfig] = None,
                 use_unity_coords: bool = True):
        """
        Initialize explorer with input signal.

        Args:
            input_signal: Array of f(t) values driving the dynamics
            config: PN dynamics configuration
            use_unity_coords: Use SYSTEM game coordinate system (Y=up, Z=forward)
        """
        self.config = config or PNConfig()
        self.input_signal = input_signal
        self.use_unity_coords = use_unity_coords

        # Pre-compute all frames
        print("Pre-computing quantum states...")
        self._precompute_animation()
        print(f"Ready: {self.state.total_frames} frames computed")

        # Set up the figure
        self._setup_figure()
        self._setup_controls()
        self._setup_keyboard()

        # Animation handle
        self.animation = None

    def _precompute_animation(self):
        """Pre-compute all quantum states for smooth playback."""
        pn = PNDynamics(self.config)

        n_frames = len(self.input_signal)

        # Storage for all computed data
        history = {
            'a': np.zeros(n_frames),
            'b': np.zeros(n_frames),
            'c': np.zeros(n_frames),
            'f': np.array(self.input_signal),
            't': np.arange(n_frames) * self.config.dt,
            'bloch_E': np.zeros((n_frames, 3)),
            'bloch_I': np.zeros((n_frames, 3)),
            'purity_E': np.zeros(n_frames),
            'purity_I': np.zeros(n_frames),
            'concurrence': np.zeros(n_frames),
            'probabilities': np.zeros((n_frames, 4)),
            'statevector': np.zeros((n_frames, 4), dtype=complex),
        }

        for i, f_t in enumerate(self.input_signal):
            # Evolve dynamics
            a, b, c = pn.step(f_t)

            # Store parameters
            history['a'][i] = a
            history['b'][i] = b
            history['c'][i] = c

            # Create circuit and extract visualization data
            circuit = create_agate_circuit(a, b, c)
            viz = extract_visualization_data(circuit)

            # Store quantum data
            history['bloch_E'][i] = viz['bloch_E']
            history['bloch_I'][i] = viz['bloch_I']
            history['purity_E'][i] = viz['purity_E']
            history['purity_I'][i] = viz['purity_I']
            history['concurrence'][i] = viz['concurrence']
            history['probabilities'][i] = viz['probabilities']
            history['statevector'][i] = viz['statevector']

            # Progress indicator
            if (i + 1) % 200 == 0:
                print(f"  {i+1}/{n_frames} frames...")

        self.state = PlaybackState(
            total_frames=n_frames,
            history=history
        )

    def _setup_figure(self):
        """Create the figure layout."""
        plt.style.use('dark_background')
        self.fig = plt.figure(figsize=(16, 10), facecolor='#0a0a1a')
        self.fig.canvas.manager.set_window_title('Quantum Neuron Explorer')

        # Grid layout
        gs = self.fig.add_gridspec(
            4, 4,
            height_ratios=[3, 3, 1, 0.5],
            width_ratios=[1, 1, 1, 0.8],
            hspace=0.3, wspace=0.3
        )

        # === Row 1: 3D Bloch Spheres + Fractal + Info ===
        self.ax_bloch_E = self.fig.add_subplot(gs[0, 0], projection='3d', facecolor='#0a0a1a')
        self.ax_bloch_I = self.fig.add_subplot(gs[0, 1], projection='3d', facecolor='#0a0a1a')
        self.ax_fractal = self.fig.add_subplot(gs[0, 2], facecolor='#0a0a1a')
        self.ax_info = self.fig.add_subplot(gs[0, 3], facecolor='#0a0a1a')

        # === Row 2: Metrics and Trajectories ===
        self.ax_params = self.fig.add_subplot(gs[1, 0], facecolor='#0a0a1a')
        self.ax_quantum = self.fig.add_subplot(gs[1, 1], facecolor='#0a0a1a')
        self.ax_probs = self.fig.add_subplot(gs[1, 2], facecolor='#0a0a1a')
        self.ax_trajectory = self.fig.add_subplot(gs[1, 3], facecolor='#0a0a1a')

        # === Row 3: Timeline ===
        self.ax_timeline = self.fig.add_subplot(gs[2, :], facecolor='#1a1a2e')

        # === Row 4: Controls (hidden axis) ===
        self.ax_controls = self.fig.add_subplot(gs[3, :], facecolor='#0a0a1a')
        self.ax_controls.axis('off')

        # Style 2D axes
        for ax in [self.ax_info, self.ax_params, self.ax_quantum,
                   self.ax_probs, self.ax_trajectory, self.ax_fractal]:
            ax.tick_params(colors='white')
            for spine in ax.spines.values():
                spine.set_color('white')

    def _setup_controls(self):
        """Create playback control buttons and timeline."""

        # === Timeline Slider ===
        self.timeline_slider = Slider(
            self.ax_timeline,
            'Frame',
            0, self.state.total_frames - 1,
            valinit=0,
            valstep=1,
            color='#7b68ee'
        )
        self.timeline_slider.on_changed(self._on_timeline_change)

        # === Control Buttons ===
        button_width = 0.045
        button_height = 0.03
        button_y = 0.02
        start_x = 0.08
        spacing = 0.055

        # Transport controls
        self.btn_start = Button(
            plt.axes([start_x, button_y, button_width, button_height]),
            '|<', color='#2a2a4a', hovercolor='#3a3a6a'
        )
        self.btn_start.on_clicked(self._jump_to_start)

        self.btn_back = Button(
            plt.axes([start_x + spacing, button_y, button_width, button_height]),
            '<<', color='#2a2a4a', hovercolor='#3a3a6a'
        )
        self.btn_back.on_clicked(self._step_back)

        self.btn_play = Button(
            plt.axes([start_x + spacing*2, button_y, button_width, button_height]),
            '>', color='#4a4a8a', hovercolor='#5a5aaa'
        )
        self.btn_play.on_clicked(self._toggle_play)

        self.btn_forward = Button(
            plt.axes([start_x + spacing*3, button_y, button_width, button_height]),
            '>>', color='#2a2a4a', hovercolor='#3a3a6a'
        )
        self.btn_forward.on_clicked(self._step_forward)

        self.btn_end = Button(
            plt.axes([start_x + spacing*4, button_y, button_width, button_height]),
            '>|', color='#2a2a4a', hovercolor='#3a3a6a'
        )
        self.btn_end.on_clicked(self._jump_to_end)

        # Speed selector
        speed_ax = plt.axes([0.42, 0.008, 0.14, 0.055], facecolor='#1a1a2e')
        self.speed_radio = RadioButtons(
            speed_ax,
            ['0.1x', '0.25x', '0.5x', '1x', '2x', '4x'],
            active=3,  # 1x default
            activecolor='#7b68ee'
        )
        self.speed_radio.on_clicked(self._on_speed_change)
        for label in self.speed_radio.labels:
            label.set_fontsize(8)

        # Bookmark button
        self.btn_bookmark = Button(
            plt.axes([0.60, button_y, 0.07, button_height]),
            'Mark [M]', color='#2a4a2a', hovercolor='#3a6a3a'
        )
        self.btn_bookmark.on_clicked(self._add_bookmark)

        # Screenshot button
        self.btn_screenshot = Button(
            plt.axes([0.68, button_y, 0.07, button_height]),
            'Save [S]', color='#4a2a4a', hovercolor='#6a3a6a'
        )
        self.btn_screenshot.on_clicked(self._save_screenshot)

        # Export button
        self.btn_export = Button(
            plt.axes([0.76, button_y, 0.07, button_height]),
            'Export', color='#2a2a4a', hovercolor='#3a3a6a'
        )
        self.btn_export.on_clicked(self._export_state)

        # Frame counter text
        self.frame_text = self.fig.text(
            0.88, 0.025,
            f'Frame: 0/{self.state.total_frames}',
            color='white', fontsize=9, family='monospace'
        )

    def _setup_keyboard(self):
        """Set up keyboard shortcuts."""
        self.fig.canvas.mpl_connect('key_press_event', self._on_key_press)

    def _on_key_press(self, event):
        """Handle keyboard shortcuts."""
        if event.key == ' ':
            self._toggle_play(None)
        elif event.key in ['right', '.']:
            self._step_forward(None)
        elif event.key in ['left', ',']:
            self._step_back(None)
        elif event.key == 'home':
            self._jump_to_start(None)
        elif event.key == 'end':
            self._jump_to_end(None)
        elif event.key == 'm':
            self._add_bookmark(None)
        elif event.key == 's':
            self._save_screenshot(None)
        elif event.key in '123456':
            idx = int(event.key) - 1
            if idx < len(self.SPEEDS):
                self.state.speed = self.SPEEDS[idx]
                self.speed_radio.set_active(idx)

    # === Playback Control Methods ===

    def _toggle_play(self, event):
        """Toggle play/pause."""
        self.state.playing = not self.state.playing
        self.btn_play.label.set_text('||' if self.state.playing else '>')

        if self.state.playing:
            self._start_animation()
        else:
            self._stop_animation()

    def _start_animation(self):
        """Start the animation loop."""
        interval = int(33 / self.state.speed)  # Base ~30fps, adjusted by speed
        self.animation = FuncAnimation(
            self.fig,
            self._animate_frame,
            interval=interval,
            blit=False,
            cache_frame_data=False
        )
        plt.draw()

    def _stop_animation(self):
        """Stop the animation loop."""
        if self.animation:
            self.animation.event_source.stop()
            self.animation = None

    def _animate_frame(self, frame_num):
        """Animation callback - advance one frame."""
        if not self.state.playing:
            return

        self.state.current_frame += 1
        if self.state.current_frame >= self.state.total_frames:
            self.state.current_frame = 0

        self._update_display()
        self.timeline_slider.set_val(self.state.current_frame)

    def _step_forward(self, event):
        """Step forward one frame."""
        self.state.playing = False
        self.btn_play.label.set_text('>')
        self._stop_animation()

        self.state.current_frame = min(
            self.state.current_frame + 1,
            self.state.total_frames - 1
        )
        self._update_display()
        self.timeline_slider.set_val(self.state.current_frame)

    def _step_back(self, event):
        """Step back one frame."""
        self.state.playing = False
        self.btn_play.label.set_text('>')
        self._stop_animation()

        self.state.current_frame = max(self.state.current_frame - 1, 0)
        self._update_display()
        self.timeline_slider.set_val(self.state.current_frame)

    def _jump_to_start(self, event):
        """Jump to first frame."""
        self.state.current_frame = 0
        self._update_display()
        self.timeline_slider.set_val(0)

    def _jump_to_end(self, event):
        """Jump to last frame."""
        self.state.current_frame = self.state.total_frames - 1
        self._update_display()
        self.timeline_slider.set_val(self.state.current_frame)

    def _on_timeline_change(self, val):
        """Handle timeline scrubbing."""
        new_frame = int(val)
        if new_frame != self.state.current_frame:
            self.state.current_frame = new_frame
            self._update_display()

    def _on_speed_change(self, label):
        """Handle speed change."""
        speed_map = {'0.1x': 0.1, '0.25x': 0.25, '0.5x': 0.5,
                     '1x': 1.0, '2x': 2.0, '4x': 4.0}
        self.state.speed = speed_map.get(label, 1.0)

        # Restart animation with new speed if playing
        if self.state.playing:
            self._stop_animation()
            self._start_animation()

    # === Bookmark Methods ===

    def _add_bookmark(self, event):
        """Bookmark current frame."""
        h = self.state.history
        i = self.state.current_frame

        bookmark = Bookmark(
            frame=i,
            time=float(h['t'][i]),
            a=float(h['a'][i]),
            b=float(h['b'][i]),
            c=float(h['c'][i]),
            concurrence=float(h['concurrence'][i]),
            note=f"Bookmark {len(self.state.bookmarks) + 1}"
        )
        self.state.bookmarks.append(bookmark)
        print(f"Bookmarked frame {i}: a={bookmark.a:.3f}, b={bookmark.b:.3f}, "
              f"c={bookmark.c:.3f}, C={bookmark.concurrence:.3f}")

        # Update display to show bookmark
        self._update_display()

    def _save_screenshot(self, event):
        """Save current view as image."""
        output_dir = Path(__file__).parent.parent / 'output' / 'screenshots'
        output_dir.mkdir(parents=True, exist_ok=True)

        timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
        filename = output_dir / f'quantum_frame{self.state.current_frame}_{timestamp}.png'
        self.fig.savefig(filename, facecolor=self.fig.get_facecolor(),
                         dpi=150, bbox_inches='tight')
        print(f"Saved: {filename}")

    def _export_state(self, event):
        """Export current state and all bookmarks to JSON."""
        h = self.state.history
        i = self.state.current_frame

        export_data = {
            'current_frame': {
                'frame': i,
                'time': float(h['t'][i]),
                'parameters': {
                    'a': float(h['a'][i]),
                    'b': float(h['b'][i]),
                    'c': float(h['c'][i]),
                },
                'quantum': {
                    'bloch_E': h['bloch_E'][i].tolist(),
                    'bloch_I': h['bloch_I'][i].tolist(),
                    'purity_E': float(h['purity_E'][i]),
                    'purity_I': float(h['purity_I'][i]),
                    'concurrence': float(h['concurrence'][i]),
                    'probabilities': h['probabilities'][i].tolist(),
                }
            },
            'bookmarks': [
                {
                    'frame': b.frame,
                    'time': b.time,
                    'a': b.a, 'b': b.b, 'c': b.c,
                    'concurrence': b.concurrence,
                    'note': b.note,
                    'timestamp': b.timestamp
                }
                for b in self.state.bookmarks
            ],
            'config': {
                'lambda_a': self.config.lambda_a,
                'lambda_c': self.config.lambda_c,
                'dt': self.config.dt,
            }
        }

        output_dir = Path(__file__).parent.parent / 'output' / 'exports'
        output_dir.mkdir(parents=True, exist_ok=True)

        timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
        filename = output_dir / f'quantum_export_{timestamp}.json'
        with open(filename, 'w') as f:
            json.dump(export_data, f, indent=2)
        print(f"Exported: {filename}")

    # === Display Update Methods ===

    def _update_display(self):
        """Update all visualizations for current frame."""
        h = self.state.history
        i = self.state.current_frame

        # Update frame counter
        self.frame_text.set_text(
            f'Frame: {i}/{self.state.total_frames} | '
            f't={h["t"][i]:.3f}s | '
            f'{self.state.speed}x'
        )

        # Clear axes
        self.ax_bloch_E.clear()
        self.ax_bloch_I.clear()
        self.ax_fractal.clear()
        self.ax_info.clear()
        self.ax_params.clear()
        self.ax_quantum.clear()
        self.ax_probs.clear()
        self.ax_trajectory.clear()

        # Draw all components
        self._draw_bloch_sphere(self.ax_bloch_E, h['bloch_E'][i], 'E (q0)', '#ff6b35', h, i, 'E')
        self._draw_bloch_sphere(self.ax_bloch_I, h['bloch_I'][i], 'I (q1)', '#7b68ee', h, i, 'I')
        self._draw_fractal(h['statevector'][i], h['concurrence'][i])
        self._draw_info(h, i)
        self._draw_parameters(h, i)
        self._draw_quantum_properties(h, i)
        self._draw_probabilities(h['probabilities'][i])
        self._draw_trajectory(h, i)

        self.fig.canvas.draw_idle()

    def _draw_bloch_sphere(self, ax, bloch_vec, title, color, history, frame_idx, qubit):
        """Draw Bloch sphere with state vector and trajectory."""
        # Convert to Unity coordinates if requested
        if self.use_unity_coords:
            bx, by, bz = bloch_to_unity_coords(tuple(bloch_vec))
        else:
            bx, by, bz = bloch_vec

        # Sphere wireframe (Unity coords: Y up)
        u = np.linspace(0, 2 * np.pi, 15)
        v = np.linspace(0, np.pi, 15)
        if self.use_unity_coords:
            x = np.outer(np.cos(u), np.sin(v))
            z = np.outer(np.sin(u), np.sin(v))
            y = np.outer(np.ones(np.size(u)), np.cos(v))
        else:
            x = np.outer(np.cos(u), np.sin(v))
            y = np.outer(np.sin(u), np.sin(v))
            z = np.outer(np.ones(np.size(u)), np.cos(v))
        ax.plot_wireframe(x, y, z, color='gray', alpha=0.15, linewidth=0.3)

        # Axes (RGB for XYZ, Green=Y up in Unity)
        ax.quiver(0, 0, 0, 1.2, 0, 0, color='red', alpha=0.6, arrow_length_ratio=0.08)
        ax.quiver(0, 0, 0, 0, 1.2, 0, color='green', alpha=0.6, arrow_length_ratio=0.08)
        ax.quiver(0, 0, 0, 0, 0, 1.2, color='blue', alpha=0.6, arrow_length_ratio=0.08)

        # Trajectory (last N frames)
        if self.state.show_trajectory:
            traj_key = 'bloch_E' if qubit == 'E' else 'bloch_I'
            start = max(0, frame_idx - self.state.trajectory_length)
            traj_raw = history[traj_key][start:frame_idx+1]

            # Convert trajectory to display coords
            if self.use_unity_coords:
                traj = np.array([bloch_to_unity_coords(tuple(v)) for v in traj_raw])
            else:
                traj = traj_raw

            if len(traj) > 1:
                for j in range(len(traj) - 1):
                    alpha = 0.2 + 0.6 * (j / len(traj))
                    ax.plot3D(
                        traj[j:j+2, 0], traj[j:j+2, 1], traj[j:j+2, 2],
                        color=color, alpha=alpha, linewidth=1
                    )

        # Current state vector
        r = np.sqrt(bx**2 + by**2 + bz**2)
        if r > 0.01:
            ax.quiver(0, 0, 0, bx, by, bz, color=color, arrow_length_ratio=0.12, linewidth=2.5)
            ax.scatter([bx], [by], [bz], color=color, s=60, edgecolors='white', linewidths=1)

        # Labels
        ax.set_title(f'{title} r={r:.2f}', color='white', fontsize=10)
        ax.set_xlim([-1.3, 1.3])
        ax.set_ylim([-1.3, 1.3])
        ax.set_zlim([-1.3, 1.3])
        ax.set_facecolor('#0a0a1a')
        ax.tick_params(colors='white', labelsize=6)
        ax.set_xlabel('X', color='red', fontsize=8)
        ax.set_ylabel('Y' if self.use_unity_coords else 'Y', color='green', fontsize=8)
        ax.set_zlabel('Z', color='blue', fontsize=8)
        ax.set_box_aspect([1,1,1])

    def _draw_fractal(self, statevector, concurrence):
        """Draw Julia set fractal."""
        fractal = generate_fractal_from_state(statevector, width=180, height=180, max_iter=60)
        self.ax_fractal.imshow(fractal, cmap='magma', origin='lower', aspect='equal')
        self.ax_fractal.set_title(f'Julia Set (C={concurrence:.3f})', color='white', fontsize=10)
        self.ax_fractal.axis('off')

    def _draw_info(self, history, i):
        """Draw state information panel."""
        self.ax_info.axis('off')

        info_text = f"""Parameters:
  a = {history['a'][i]:.4f}
  b = {history['b'][i]:.4f} ({np.degrees(history['b'][i]):.1f} deg)
  c = {history['c'][i]:.4f}

Bloch E:
  ({history['bloch_E'][i][0]:.3f},
   {history['bloch_E'][i][1]:.3f},
   {history['bloch_E'][i][2]:.3f})

Bloch I:
  ({history['bloch_I'][i][0]:.3f},
   {history['bloch_I'][i][1]:.3f},
   {history['bloch_I'][i][2]:.3f})

Concurrence: {history['concurrence'][i]:.4f}
Input f(t): {history['f'][i]:.4f}

Bookmarks: {len(self.state.bookmarks)}"""

        self.ax_info.text(0.05, 0.98, info_text, transform=self.ax_info.transAxes,
                          fontsize=8, color='white', family='monospace',
                          verticalalignment='top')
        self.ax_info.set_title('State Info', color='white', fontsize=10)

    def _draw_parameters(self, history, i):
        """Draw parameter evolution plot."""
        lookback = min(300, i)
        start = max(0, i - lookback)
        t_slice = history['t'][start:i+1]

        if len(t_slice) > 0:
            self.ax_params.plot(t_slice, history['a'][start:i+1], color='#ff6b35',
                               label='a', linewidth=1.5)
            self.ax_params.plot(t_slice, history['b'][start:i+1] / (2*np.pi),
                               color='#00d084', label='b/2pi', linewidth=1.5)
            self.ax_params.plot(t_slice, history['c'][start:i+1], color='#7b68ee',
                               label='c', linewidth=1.5)

            # Current position marker
            self.ax_params.axvline(history['t'][i], color='white', linestyle='--', alpha=0.5)

            self.ax_params.set_xlim([t_slice[0], t_slice[-1]])

        self.ax_params.set_ylim([0, 1.1])
        self.ax_params.set_xlabel('Time (s)', color='white', fontsize=8)
        self.ax_params.legend(loc='upper right', fontsize=7, facecolor='#1a1a2e',
                             labelcolor='white')
        self.ax_params.set_title('Parameters', color='white', fontsize=10)
        self.ax_params.tick_params(colors='white', labelsize=7)
        self.ax_params.set_facecolor('#0a0a1a')

    def _draw_quantum_properties(self, history, i):
        """Draw quantum properties bar chart."""
        props = ['Concurrence', 'Purity E', 'Purity I']
        values = [history['concurrence'][i], history['purity_E'][i], history['purity_I'][i]]
        colors = ['#ff00ff', '#ff6b35', '#7b68ee']

        bars = self.ax_quantum.barh(props, values, color=colors, edgecolor='white', linewidth=0.5)

        for bar, val in zip(bars, values):
            self.ax_quantum.text(val + 0.02, bar.get_y() + bar.get_height()/2,
                                f'{val:.3f}', va='center', color='white', fontsize=8)

        self.ax_quantum.set_xlim([0, 1.15])
        self.ax_quantum.set_title('Quantum Properties', color='white', fontsize=10)
        self.ax_quantum.tick_params(colors='white', labelsize=8)
        self.ax_quantum.set_facecolor('#0a0a1a')

    def _draw_probabilities(self, probs):
        """Draw measurement probability bar chart."""
        basis = ['|00>', '|01>', '|10>', '|11>']
        colors = ['#00ff88', '#00aaff', '#ff6b35', '#7b68ee']

        bars = self.ax_probs.bar(basis, probs, color=colors, edgecolor='white', linewidth=0.5)

        for bar, p in zip(bars, probs):
            if p > 0.05:
                self.ax_probs.text(bar.get_x() + bar.get_width()/2, bar.get_height() + 0.02,
                                  f'{p:.2f}', ha='center', color='white', fontsize=7)

        self.ax_probs.set_ylim([0, 1.15])
        self.ax_probs.set_title('Probabilities', color='white', fontsize=10)
        self.ax_probs.tick_params(colors='white', labelsize=7)
        self.ax_probs.set_facecolor('#0a0a1a')

    def _draw_trajectory(self, history, i):
        """Draw 2D parameter space trajectory (a vs c) colored by concurrence."""
        lookback = min(300, i)
        start = max(0, i - lookback)

        a_traj = history['a'][start:i+1]
        c_traj = history['c'][start:i+1]
        conc_traj = history['concurrence'][start:i+1]

        if len(a_traj) > 0:
            self.ax_trajectory.scatter(
                a_traj, c_traj,
                c=conc_traj, cmap='plasma',
                s=3, alpha=0.7, vmin=0, vmax=0.5
            )

        # Current position
        self.ax_trajectory.scatter([history['a'][i]], [history['c'][i]],
                                   color='white', s=80, marker='*',
                                   edgecolors='black', linewidths=1)

        # Show bookmarks as green diamonds
        for bm in self.state.bookmarks:
            self.ax_trajectory.scatter([bm.a], [bm.c], color='#00ff00', s=40, marker='d')

        self.ax_trajectory.set_xlim([0, 1])
        self.ax_trajectory.set_ylim([0, 1])
        self.ax_trajectory.set_xlabel('a (E)', color='white', fontsize=8)
        self.ax_trajectory.set_ylabel('c (I)', color='white', fontsize=8)
        self.ax_trajectory.set_title('a-c Space', color='white', fontsize=10)
        self.ax_trajectory.tick_params(colors='white', labelsize=7)
        self.ax_trajectory.set_facecolor('#0a0a1a')

    def run(self):
        """Launch the explorer."""
        self._update_display()
        plt.show()


# === Convenience Functions ===

def explore_sine_dynamics(duration: float = 10.0, frequency: float = 0.2,
                          dt: float = 0.01):
    """Quick start with sine wave input."""
    print("=" * 50)
    print("Quantum Neuron Explorer")
    print("=" * 50)
    print("Controls:")
    print("  Space      - Play/Pause")
    print("  Left/Right - Step frame")
    print("  Home/End   - Jump to start/end")
    print("  1-6        - Speed (0.1x to 4x)")
    print("  M          - Bookmark frame")
    print("  S          - Screenshot")
    print("=" * 50)

    signal = generate_sine_input(
        duration=duration,
        frequency=frequency,
        amplitude=0.5,
        offset=0.4,
        dt=dt
    )
    config = PNConfig(lambda_a=0.08, lambda_c=0.03, dt=dt)
    explorer = QuantumNeuronExplorer(signal, config)
    explorer.run()


def explore_custom_signal(signal: np.ndarray, config: Optional[PNConfig] = None):
    """Explore with custom input signal."""
    explorer = QuantumNeuronExplorer(signal, config)
    explorer.run()


if __name__ == '__main__':
    explore_sine_dynamics(duration=10.0, frequency=0.2)
