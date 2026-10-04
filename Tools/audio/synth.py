"""Tiny DSP toolkit for synthesising every sound in After Hours (numpy + scipy).

All signals are float64 numpy arrays at SR. Stereo signals are shape (n, 2).
"""
import math
import os
import wave

import numpy as np
from scipy import signal

SR = 44100
RNG = np.random.default_rng(1234)


def seed(s):
    global RNG
    RNG = np.random.default_rng(s)


def n_of(seconds):
    return int(round(seconds * SR))


def t_of(n):
    return np.arange(n) / SR


# ---- sources ---------------------------------------------------------------------------------

def white(seconds):
    return RNG.standard_normal(n_of(seconds))


def pink(seconds):
    n = n_of(seconds)
    spec = np.fft.rfft(RNG.standard_normal(n))
    f = np.fft.rfftfreq(n, 1 / SR)
    f[0] = f[1]
    spec /= np.sqrt(f)
    x = np.fft.irfft(spec, n)
    return x / (np.std(x) + 1e-9)


def brown(seconds):
    n = n_of(seconds)
    spec = np.fft.rfft(RNG.standard_normal(n))
    f = np.fft.rfftfreq(n, 1 / SR)
    f[0] = f[1]
    spec /= f
    x = np.fft.irfft(spec, n)
    return x / (np.std(x) + 1e-9)


def sine(freq, seconds, phase=0.0):
    t = t_of(n_of(seconds))
    if np.ndim(freq) == 0:
        return np.sin(2 * np.pi * freq * t + phase)
    ph = 2 * np.pi * np.cumsum(freq) / SR
    return np.sin(ph + phase)


def saw(freq, seconds, harmonics=30):
    t = t_of(n_of(seconds))
    if np.ndim(freq) == 0:
        ph = 2 * np.pi * freq * t
    else:
        ph = 2 * np.pi * np.cumsum(freq) / SR
    out = np.zeros_like(t)
    for k in range(1, harmonics + 1):
        out += ((-1) ** (k + 1)) * np.sin(k * ph) / k
    return out * (2 / np.pi)


def fm(carrier, mod_ratio, index, seconds, index_env=None):
    n = n_of(seconds)
    t = t_of(n)
    idx = index if index_env is None else index * index_env
    mod = np.sin(2 * np.pi * carrier * mod_ratio * t)
    return np.sin(2 * np.pi * carrier * t + idx * mod)


# ---- filters ---------------------------------------------------------------------------------

def _sos(kind, f, order=2):
    nyq = SR / 2
    if isinstance(f, (tuple, list)):
        wn = [max(1e-4, min(0.999, x / nyq)) for x in f]
    else:
        wn = max(1e-4, min(0.999, f / nyq))
    return signal.butter(order, wn, btype=kind, output="sos")


def lp(x, f, order=2):
    return signal.sosfilt(_sos("lowpass", f, order), x, axis=0)


def hp(x, f, order=2):
    return signal.sosfilt(_sos("highpass", f, order), x, axis=0)


def bp(x, lo, hi, order=2):
    return signal.sosfilt(_sos("bandpass", (lo, hi), order), x, axis=0)


def peak(x, f, q=4.0, gain_db=6.0):
    b, a = signal.iirpeak(f / (SR / 2), q)
    y = signal.lfilter(b, a, x, axis=0)
    g = 10 ** (gain_db / 20) - 1
    return x + y * g


def sweep_lp(x, f_start, f_end, q=0.7):
    """Time-varying one-pole-ish lowpass via block processing (cheap and good enough)."""
    out = np.zeros_like(x)
    block = 256
    n = len(x)
    zi = None
    for i in range(0, n, block):
        k = i / max(1, n - 1)
        f = f_start * (f_end / f_start) ** k
        sos = _sos("lowpass", f, 2)
        if zi is None:
            zi = signal.sosfilt_zi(sos) * 0
        seg, zi = signal.sosfilt(sos, x[i:i + block], zi=zi)
        out[i:i + block] = seg
    return out


# ---- envelopes -------------------------------------------------------------------------------

def env_exp(seconds, decay, attack=0.002):
    n = n_of(seconds)
    t = t_of(n)
    e = np.exp(-t / max(decay, 1e-4))
    a = n_of(attack)
    if a > 0:
        e[:a] *= np.linspace(0, 1, a)
    return e


def env_adsr(seconds, a=0.01, d=0.1, s=0.7, r=0.2):
    n = n_of(seconds)
    e = np.ones(n) * s
    na, nd, nr = n_of(a), n_of(d), n_of(r)
    na = min(na, n)
    e[:na] = np.linspace(0, 1, na)
    e[na:na + nd] = np.linspace(1, s, len(e[na:na + nd]))
    if nr > 0:
        e[-nr:] *= np.linspace(1, 0, nr)
    return e


def fade(x, fin=0.005, fout=0.02):
    x = x.copy()
    a, b = n_of(fin), n_of(fout)
    if a:
        x[:a] *= np.linspace(0, 1, a)[:, None] if x.ndim == 2 else np.linspace(0, 1, a)
    if b:
        x[-b:] *= np.linspace(1, 0, b)[:, None] if x.ndim == 2 else np.linspace(1, 0, b)
    return x


def smooth_noise(seconds, rate_hz, lo=0.0, hi=1.0):
    """Slowly varying random control signal."""
    n = n_of(seconds)
    pts = max(4, int(seconds * rate_hz) + 3)
    v = RNG.random(pts)
    xs = np.linspace(0, n, pts)
    y = np.interp(np.arange(n), xs, v)
    y = lp(y, max(0.5, rate_hz * 1.5), 1)
    y = (y - y.min()) / (y.max() - y.min() + 1e-9)
    return lo + (hi - lo) * y


# ---- space -----------------------------------------------------------------------------------

def reverb_ir(seconds=1.2, decay=0.35, damp=6000, stereo=True, predelay=0.01):
    n = n_of(seconds)
    ch = 2 if stereo else 1
    ir = RNG.standard_normal((n, ch))
    t = t_of(n)[:, None]
    ir *= np.exp(-t / decay)
    ir = lp(ir, damp, 1)
    pd = n_of(predelay)
    ir = np.vstack([np.zeros((pd, ch)), ir])
    ir /= np.sqrt(np.sum(ir ** 2, axis=0, keepdims=True)) + 1e-9
    return ir if stereo else ir[:, 0]


def reverb(x, wet=0.25, seconds=1.0, decay=0.3, damp=5000):
    mono = x.ndim == 1
    src = x[:, None] if mono else x
    ir = reverb_ir(seconds, decay, damp, stereo=True)
    wet_sig = np.stack([signal.fftconvolve(src[:, min(c, src.shape[1] - 1)], ir[:, c])[:len(src) + len(ir) - 1] for c in range(2)], axis=1)
    dry = np.vstack([np.repeat(src, 2, axis=1) if src.shape[1] == 1 else src, np.zeros((len(ir) - 1, 2))])
    out = dry * (1 - wet) + wet_sig * wet
    return out.mean(axis=1) if mono else out


# ---- utilities -------------------------------------------------------------------------------

def mix(*parts):
    n = max(len(p) for p in parts)
    shape = (n, 2) if any(p.ndim == 2 for p in parts) else (n,)
    out = np.zeros(shape)
    for p in parts:
        if out.ndim == 2 and p.ndim == 1:
            p = np.repeat(p[:, None], 2, axis=1)
        out[:len(p)] += p
    return out


def place(dst, src, at_seconds, gain=1.0):
    i = n_of(at_seconds)
    if i >= len(dst):
        return dst
    m = min(len(src), len(dst) - i)
    if dst.ndim == 2 and src.ndim == 1:
        src = np.repeat(src[:, None], 2, axis=1)
    dst[i:i + m] += src[:m] * gain
    return dst


def loopify(x, xfade=0.25):
    """Make a seamless loop by crossfading the tail into the head."""
    k = n_of(xfade)
    head, body, tail = x[:k], x[k:-k] if k else x, x[-k:]
    w = np.linspace(0, 1, k)
    if x.ndim == 2:
        w = w[:, None]
    blended = tail * (1 - np.sqrt(w)) + head * np.sqrt(w)
    return np.concatenate([body[:len(body)], blended]) if k else x


def normalize(x, peak_db=-1.0):
    p = np.max(np.abs(x)) + 1e-9
    return x * (10 ** (peak_db / 20) / p)


def rms_normalize(x, rms_db=-18.0, peak_db=-1.0):
    r = np.sqrt(np.mean(x ** 2)) + 1e-9
    y = x * (10 ** (rms_db / 20) / r)
    p = np.max(np.abs(y))
    lim = 10 ** (peak_db / 20)
    if p > lim:
        y = soft_clip(y / p * lim * 1.15) * lim / 1.0
    return y


def soft_clip(x, drive=1.0):
    return np.tanh(x * drive) / np.tanh(drive)


def write(path, x, peak_db=-1.0, normalize_peak=True):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if normalize_peak:
        x = normalize(x, peak_db)
    x = np.clip(x, -1, 1)
    data = (x * 32767).astype("<i2")
    ch = 1 if x.ndim == 1 else x.shape[1]
    with wave.open(path, "wb") as w:
        w.setnchannels(ch)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def grains(seconds, rate, grain_fn, jitter=1.0):
    """Scatter short grains (from grain_fn(i) -> array) at a Poisson-ish rate."""
    out = np.zeros(n_of(seconds))
    t = 0.0
    i = 0
    while t < seconds:
        g = grain_fn(i)
        place(out, g, t)
        t += RNG.exponential(1.0 / rate) * jitter + (1 - jitter) / rate
        i += 1
    return out


def midi(n):
    return 440.0 * 2 ** ((n - 69) / 12)
