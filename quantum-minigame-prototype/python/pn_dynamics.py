"""
PN (Positive-Negative) Neural Dynamics for A Gate Parameters.

ODEs:
    da/dt = -lambda_a * a + f(t) * (1 - a)    [Excitatory: decay + input]
    db/dt = f(t) * (1 - b)                     [Phase: pure integration]
    dc/dt = +lambda_c * c + f(t) * (1 - c)    [Inhibitory: growth + input]
"""

import numpy as np
from dataclasses import dataclass
from typing import Tuple, Optional


@dataclass
class PNConfig:
    """Configuration for PN dynamics."""
    lambda_a: float = 0.1      # Excitatory decay rate
    lambda_c: float = 0.05     # Inhibitory growth rate
    dt: float = 0.001          # Integration timestep

    # Parameter bounds
    a_min: float = 0.0
    a_max: float = 1.0
    b_min: float = 0.0
    b_max: float = 2 * np.pi
    c_min: float = 0.0
    c_max: float = 1.0


class PNDynamics:
    """
    PN neuron dynamics simulator.

    Models excitatory-inhibitory balance with:
    - Fast excitatory decay (glutamatergic-like)
    - Slow inhibitory accumulation (GABAergic-like)
    """

    def __init__(self, config: Optional[PNConfig] = None):
        self.config = config or PNConfig()
        self.reset()

    def reset(self):
        """Reset parameters to initial conditions."""
        self.a = 0.0
        self.b = 0.0
        self.c = 0.0

    def step(self, f_t: float) -> Tuple[float, float, float]:
        """
        Single integration step.

        Args:
            f_t: Input signal value (will be rectified)

        Returns:
            (a, b, c): Updated parameter values
        """
        cfg = self.config
        f_t = abs(f_t)  # Rectify input

        # Compute derivatives
        da = cfg.dt * (-cfg.lambda_a * self.a + f_t * (1 - self.a))
        db = cfg.dt * (f_t * (1 - self.b))
        dc = cfg.dt * (cfg.lambda_c * self.c + f_t * (1 - self.c))

        # Update with clamping
        self.a = np.clip(self.a + da, cfg.a_min, cfg.a_max)
        self.b = np.clip(self.b + db, cfg.b_min, cfg.b_max)
        self.c = np.clip(self.c + dc, cfg.c_min, cfg.c_max)

        return (self.a, self.b, self.c)

    def evolve(self, input_signal: np.ndarray) -> Tuple[float, float, float]:
        """
        Evolve dynamics over entire input signal.

        Args:
            input_signal: 1D array of input values

        Returns:
            (a, b, c): Final parameter values
        """
        self.reset()
        for f_t in input_signal:
            self.step(f_t)
        return (self.a, self.b, self.c)

    def evolve_with_history(self, input_signal: np.ndarray) -> dict:
        """
        Evolve dynamics and record full history.

        Returns:
            dict with keys: 'a', 'b', 'c', 't', 'f' (all numpy arrays)
        """
        self.reset()
        n = len(input_signal)

        history = {
            'a': np.zeros(n),
            'b': np.zeros(n),
            'c': np.zeros(n),
            't': np.arange(n) * self.config.dt,
            'f': np.array(input_signal)
        }

        for i, f_t in enumerate(input_signal):
            self.step(f_t)
            history['a'][i] = self.a
            history['b'][i] = self.b
            history['c'][i] = self.c

        return history

    def get_state(self) -> Tuple[float, float, float]:
        """Get current parameter state."""
        return (self.a, self.b, self.c)

    def set_state(self, a: float, b: float, c: float):
        """Set parameter state directly."""
        cfg = self.config
        self.a = np.clip(a, cfg.a_min, cfg.a_max)
        self.b = np.clip(b, cfg.b_min, cfg.b_max)
        self.c = np.clip(c, cfg.c_min, cfg.c_max)


# === Input Signal Generators ===

def generate_sine_input(duration: float, frequency: float,
                        amplitude: float = 0.5, offset: float = 0.5,
                        dt: float = 0.001) -> np.ndarray:
    """Generate sinusoidal input signal."""
    t = np.arange(0, duration, dt)
    return offset + amplitude * np.sin(2 * np.pi * frequency * t)


def generate_pulse_input(duration: float, pulse_start: float,
                         pulse_width: float, pulse_height: float = 1.0,
                         dt: float = 0.001) -> np.ndarray:
    """Generate rectangular pulse input."""
    t = np.arange(0, duration, dt)
    signal = np.zeros_like(t)
    pulse_mask = (t >= pulse_start) & (t < pulse_start + pulse_width)
    signal[pulse_mask] = pulse_height
    return signal


def generate_noise_input(duration: float, mean: float = 0.5,
                         std: float = 0.2, dt: float = 0.001) -> np.ndarray:
    """Generate Gaussian noise input (clipped to [0, 1])."""
    n = int(duration / dt)
    signal = np.random.normal(mean, std, n)
    return np.clip(signal, 0, 1)


def generate_ramp_input(duration: float, start_val: float = 0.0,
                        end_val: float = 1.0, dt: float = 0.001) -> np.ndarray:
    """Generate linear ramp input."""
    n = int(duration / dt)
    return np.linspace(start_val, end_val, n)


def generate_step_input(duration: float, step_time: float,
                        low_val: float = 0.0, high_val: float = 1.0,
                        dt: float = 0.001) -> np.ndarray:
    """Generate step function input."""
    t = np.arange(0, duration, dt)
    signal = np.where(t >= step_time, high_val, low_val)
    return signal


def generate_burst_input(duration: float, burst_starts: list,
                         burst_width: float = 0.1, burst_height: float = 1.0,
                         dt: float = 0.001) -> np.ndarray:
    """Generate multiple burst/pulse input."""
    t = np.arange(0, duration, dt)
    signal = np.zeros_like(t)
    for start in burst_starts:
        mask = (t >= start) & (t < start + burst_width)
        signal[mask] = burst_height
    return signal


if __name__ == '__main__':
    # Quick test
    config = PNConfig(lambda_a=0.1, lambda_c=0.05, dt=0.01)
    pn = PNDynamics(config)

    signal = generate_sine_input(duration=5.0, frequency=0.5, dt=0.01)
    history = pn.evolve_with_history(signal)

    print(f"Duration: {history['t'][-1]:.2f}s, {len(history['t'])} samples")
    print(f"Final state: a={history['a'][-1]:.4f}, b={history['b'][-1]:.4f}, c={history['c'][-1]:.4f}")
    print(f"Max a: {history['a'].max():.4f}, Max c: {history['c'].max():.4f}")
