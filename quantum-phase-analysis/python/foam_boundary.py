"""
Quantum foam boundary analysis.
"""

import numpy as np
import matplotlib
matplotlib.use('TkAgg')
import matplotlib.pyplot as plt
from typing import Dict, Tuple
from scipy.signal import find_peaks
from scipy.fft import fft, fftfreq

from .dynamics_core import DynamicsConfig, integrate_rk4


def detect_boundary_regions(trajectory: np.ndarray,
                            threshold: float = 0.05) -> Dict:
    """
    Detect when trajectory is near parameter space boundaries.
    """
    a, c = trajectory[:, 0], trajectory[:, 2]

    near_boundary = (
        (a < threshold) | (a > 1 - threshold) |
        (c < threshold) | (c > 1 - threshold)
    )

    return {
        'mask': near_boundary,
        'fraction': np.mean(near_boundary),
        'a_low_fraction': np.mean(a < threshold),
        'a_high_fraction': np.mean(a > 1 - threshold),
        'c_low_fraction': np.mean(c < threshold),
        'c_high_fraction': np.mean(c > 1 - threshold),
    }


def analyze_foam_oscillations(trajectory: np.ndarray,
                              config: DynamicsConfig) -> Dict:
    """
    Analyze oscillatory structure of foam dynamics.
    """
    a = trajectory[:, 0]
    t = np.arange(len(trajectory)) * config.dt

    # FFT analysis
    n = len(a)
    freq = fftfreq(n, config.dt)[:n//2]
    power = np.abs(fft(a - np.mean(a)))[:n//2]

    # Dominant frequency
    if len(power) > 1:
        peak_idx = np.argmax(power[1:]) + 1
        dominant_freq = freq[peak_idx] if peak_idx < len(freq) else 0
    else:
        dominant_freq = 0

    # Peak analysis for period
    peaks, _ = find_peaks(a, height=0.5, distance=50)
    if len(peaks) > 2:
        periods = np.diff(t[peaks])
        mean_period = np.mean(periods)
        period_std = np.std(periods)
    else:
        mean_period = period_std = 0

    return {
        'dominant_frequency': dominant_freq,
        'mean_period': mean_period,
        'period_std': period_std,
        'frequencies': freq,
        'power_spectrum': power,
        'n_oscillations': len(peaks)
    }


def plot_foam_analysis(trajectory: np.ndarray,
                       config: DynamicsConfig) -> plt.Figure:
    """
    Comprehensive foam visualization.
    """
    fig = plt.figure(figsize=(16, 10), facecolor='#0a0a1a')

    a, c = trajectory[:, 0], trajectory[:, 2]
    t = np.arange(len(trajectory)) * config.dt

    boundary = detect_boundary_regions(trajectory)
    osc = analyze_foam_oscillations(trajectory, config)

    # 1. Phase space with boundary highlighting
    ax1 = fig.add_subplot(2, 3, 1, facecolor='#0a0a1a')
    ax1.scatter(a[~boundary['mask']], c[~boundary['mask']],
               s=1, c='#7b68ee', alpha=0.3, label='Interior')
    ax1.scatter(a[boundary['mask']], c[boundary['mask']],
               s=1, c='#ff6b35', alpha=0.5, label='Foam')
    ax1.axhline(0.95, color='red', linestyle=':', alpha=0.3)
    ax1.axvline(0.95, color='red', linestyle=':', alpha=0.3)
    ax1.axhline(0.05, color='red', linestyle=':', alpha=0.3)
    ax1.axvline(0.05, color='red', linestyle=':', alpha=0.3)
    ax1.set_xlabel('a', color='white')
    ax1.set_ylabel('c', color='white')
    ax1.set_title(f'Phase Space ({boundary["fraction"]:.1%} in foam)', color='white')
    ax1.tick_params(colors='white')
    ax1.legend(facecolor='#1a1a2e', labelcolor='white')

    # 2. Time series
    ax2 = fig.add_subplot(2, 3, 2, facecolor='#0a0a1a')
    ax2.plot(t, a, color='#ff6b35', linewidth=0.5, label='a')
    ax2.plot(t, c, color='#7b68ee', linewidth=0.5, label='c')
    ax2.fill_between(t, 0, 1, where=boundary['mask'], alpha=0.2, color='yellow')
    ax2.set_xlabel('Time (s)', color='white')
    ax2.set_ylabel('Value', color='white')
    ax2.set_title('Time Series', color='white')
    ax2.tick_params(colors='white')
    ax2.legend(facecolor='#1a1a2e', labelcolor='white')

    # 3. Power spectrum
    ax3 = fig.add_subplot(2, 3, 3, facecolor='#0a0a1a')
    max_idx = min(500, len(osc['frequencies']))
    ax3.semilogy(osc['frequencies'][:max_idx], osc['power_spectrum'][:max_idx] + 1e-10, color='#00d084')
    ax3.axvline(osc['dominant_frequency'], color='red', linestyle='--',
               label=f'f={osc["dominant_frequency"]:.3f} Hz')
    ax3.set_xlabel('Frequency (Hz)', color='white')
    ax3.set_ylabel('Power', color='white')
    ax3.set_title('Frequency Spectrum', color='white')
    ax3.tick_params(colors='white')
    ax3.legend(facecolor='#1a1a2e', labelcolor='white')

    # 4. Boundary distance
    ax4 = fig.add_subplot(2, 3, 4, facecolor='#0a0a1a')
    dist_a = np.minimum(a, 1 - a)
    dist_c = np.minimum(c, 1 - c)
    boundary_dist = np.minimum(dist_a, dist_c)
    ax4.plot(t, boundary_dist, color='#ffcc00', linewidth=0.5)
    ax4.axhline(0.05, color='red', linestyle='--', alpha=0.5, label='Foam threshold')
    ax4.set_xlabel('Time (s)', color='white')
    ax4.set_ylabel('Distance to boundary', color='white')
    ax4.set_title('Boundary Distance', color='white')
    ax4.tick_params(colors='white')
    ax4.legend(facecolor='#1a1a2e', labelcolor='white')

    # 5. a-c correlation
    ax5 = fig.add_subplot(2, 3, 5, facecolor='#0a0a1a')
    # Compute cross-correlation
    a_norm = (a - np.mean(a)) / (np.std(a) + 1e-10)
    c_norm = (c - np.mean(c)) / (np.std(c) + 1e-10)
    corr = np.correlate(a_norm, c_norm, mode='same') / len(a)
    lag = np.arange(len(corr)) - len(corr) // 2
    lag_time = lag * config.dt
    ax5.plot(lag_time, corr, color='#ff6b9d', linewidth=1)
    ax5.set_xlabel('Lag (s)', color='white')
    ax5.set_ylabel('Cross-correlation', color='white')
    ax5.set_title('a-c Phase Coherence', color='white')
    ax5.tick_params(colors='white')

    # 6. Statistics summary
    ax6 = fig.add_subplot(2, 3, 6, facecolor='#0a0a1a')
    ax6.axis('off')
    stats_text = f"""
    FOAM ANALYSIS SUMMARY
    =====================

    Boundary fraction: {boundary['fraction']:.1%}

    a near 0: {boundary['a_low_fraction']:.1%}
    a near 1: {boundary['a_high_fraction']:.1%}
    c near 0: {boundary['c_low_fraction']:.1%}
    c near 1: {boundary['c_high_fraction']:.1%}

    Dominant freq: {osc['dominant_frequency']:.4f} Hz
    Mean period: {osc['mean_period']:.3f} s
    Period std: {osc['period_std']:.4f} s
    N oscillations: {osc['n_oscillations']}

    a range: [{np.min(a):.3f}, {np.max(a):.3f}]
    c range: [{np.min(c):.3f}, {np.max(c):.3f}]
    """
    ax6.text(0.1, 0.9, stats_text, transform=ax6.transAxes,
            ha='left', va='top', color='white', fontsize=10,
            family='monospace')

    plt.tight_layout()
    return fig
