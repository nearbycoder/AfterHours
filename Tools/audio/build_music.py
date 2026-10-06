#!/usr/bin/env python3
"""Procedural lo-fi night jazz for After Hours -> Assets/Resources/Audio/music_*.ogg

    Tools/.venv/bin/python Tools/audio/build_music.py [names...]
    Tools/.venv/bin/python Tools/audio/build_music.py --check [names...]   numbers only, writes nothing

Every track is a seamless loop: bars are rendered, the reverb tail is wrapped back onto the start.
"""
import os
import subprocess
import sys
import tempfile

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from synth import *  # noqa: E402,F401

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio")

NOTE = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6, "G": 7, "G#": 8, "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}
QUAL = {
    "maj7": [0, 4, 7, 11], "maj9": [0, 4, 7, 11, 14], "m7": [0, 3, 7, 10], "m9": [0, 3, 7, 10, 14], "7": [0, 4, 7, 10],
    "9": [0, 4, 7, 10, 14], "13": [0, 4, 10, 14, 21], "7b9": [0, 4, 7, 10, 13], "m": [0, 3, 7], "": [0, 4, 7],
    "6": [0, 4, 7, 9], "m6": [0, 3, 7, 9], "sus": [0, 5, 7, 10], "madd9": [0, 3, 7, 14], "maj7#11": [0, 4, 7, 11, 18],
    "m11": [0, 3, 7, 10, 14, 17], "dim7": [0, 3, 6, 9],
}


def chord(name, octave=3):
    root = name[:2] if len(name) > 1 and name[1] in "#b" else name[:1]
    q = name[len(root):]
    base = 12 * (octave + 1) + NOTE[root]
    return base, [base + i for i in QUAL[q]]


# ---- instruments -----------------------------------------------------------------------------

def epiano(f, dur, vel=0.7):
    """FM tine piano with tremolo and a soft bark on hard hits."""
    d = dur + 1.6
    n = n_of(d)
    t = t_of(n)
    idx_env = np.exp(-t / (0.35 + 0.3 * (1 - vel))) * (1.2 + 1.5 * vel)
    mod = np.sin(2 * np.pi * f * t) * idx_env
    car = np.sin(2 * np.pi * f * t + mod)
    tine = np.sin(2 * np.pi * f * 14.0 * t) * np.exp(-t / 0.02) * 0.08 * vel
    amp = np.exp(-t / (1.4 + 220 / f)) * (1 - np.exp(-t / 0.004))
    rel = np.clip((dur + 0.25 - t) / 0.25, 0, 1) ** 0.5 * 0.8 + 0.2 * np.exp(-np.maximum(t - dur, 0) / 0.2)
    trem = 1 + 0.06 * np.sin(2 * np.pi * 5.2 * t)
    return (car * amp * rel * trem + tine) * vel


def bass(f, dur, vel=0.8):
    d = dur + 0.4
    t = t_of(n_of(d))
    x = np.sin(2 * np.pi * f * t) + 0.25 * np.sin(4 * np.pi * f * t) + 0.08 * np.sin(6 * np.pi * f * t)
    env = (1 - np.exp(-t / 0.006)) * np.exp(-t / 1.2) * np.clip((dur + 0.08 - t) / 0.08, 0, 1)
    return lp(x * env * vel, 900)


def pad(fs, dur, bright=0.3):
    d = dur + 1.5
    n = n_of(d)
    t = t_of(n)
    out = np.zeros(n)
    for f in fs:
        for det in (-0.12, 0.0, 0.11):
            ff = f * 2 ** (det / 12)
            out += saw(ff, d, 12) * 0.12
    out = lp(out, 600 + 2400 * bright)
    env = np.clip(t / 1.2, 0, 1) * np.clip((dur + 1.2 - t) / 1.2, 0, 1)
    return out * env


def bell(f, dur=2.5, vel=0.5):
    t = t_of(n_of(dur))
    x = fm(f, 3.5, 2.0, dur, np.exp(-t / 0.4)) * np.exp(-t / 0.9)
    return x * vel


def kick(vel=1.0):
    d = 0.4
    t = t_of(n_of(d))
    f = 45 + 75 * np.exp(-t / 0.03)
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.13)
    x += lp(white(d), 3000) * np.exp(-t / 0.004) * 0.25
    return x * vel


def brush(vel=0.6, long=False):
    d = 0.5 if long else 0.18
    t = t_of(n_of(d))
    x = bp(white(d), 1800, 8000) * (np.exp(-t / (0.12 if long else 0.05)))
    if long:
        x *= np.clip(t / 0.06, 0, 1)
    return x * vel


def rim(vel=0.5):
    d = 0.08
    t = t_of(n_of(d))
    x = sine(1650, d) * np.exp(-t / 0.012) * 0.6 + bp(white(d), 2000, 6000) * np.exp(-t / 0.006)
    return x * vel


def hat(vel=0.3):
    d = 0.07
    t = t_of(n_of(d))
    return hp(white(d), 7000) * np.exp(-t / 0.02) * vel


def crackle(seconds, density=7.0):
    out = np.zeros(n_of(seconds))
    k = int(seconds * density)
    pos = RNG.integers(0, len(out) - 60, k)
    for p in pos:
        out[p:p + 40] += hp(white(40 / SR), 2000)[:40] * RNG.uniform(0.05, 0.3) * np.exp(-np.arange(40) / 8)
    hiss = lp(hp(white(seconds), 3000), 9000) * 0.012
    return out + hiss


# ---- arrangement -----------------------------------------------------------------------------

def plan(spec):
    """Bar by bar: (settings for that bar, chord). A track is either one progression looped for
    `bars`, or `sections`: a list of {bars, chords?, drums?, melody?, ...} overriding the track's
    settings, played in order (intro, A, B, breakdown...) so a long loop doesn't feel like one."""
    out = []
    for sec in spec.get("sections") or [dict(bars=spec["bars"])]:
        s = {**spec, **sec}
        prog = s["chords"]
        out += [(s, prog[i % len(prog)]) for i in range(sec["bars"])]
    return out


def render(spec):
    seed(spec["seed"])
    bpm = spec["bpm"]
    beat = 60.0 / bpm
    bar = beat * 4
    bars = sum(sec["bars"] for sec in spec["sections"]) if spec.get("sections") else spec["bars"]
    length = bars * bar
    tail = 4.0
    n = n_of(length + tail)
    L = np.zeros(n)
    R = np.zeros(n)
    swing = spec.get("swing", 0.12)

    def put(x, at, pan=0.0, gain=1.0):
        i = n_of(at)
        if i >= n:
            return
        m = min(len(x), n - i)
        l = np.cos((pan + 1) * np.pi / 4)
        r = np.sin((pan + 1) * np.pi / 4)
        L[i:i + m] += x[:m] * gain * l
        R[i:i + m] += x[:m] * gain * r

    for b, (s, name) in enumerate(plan(spec)):
        root, notes = chord(name, s.get("octave", 3))
        t0 = b * bar
        # Comping: chord on 1 (sometimes the "and" of 2), voiced up an octave for the top notes.
        rh = [x + 12 if i >= 2 else x for i, x in enumerate(notes)]
        hits = s.get("comp", [(0.0, 3.5, 0.6)])
        for (pos, dur, vel) in hits:
            if s.get("sparse") and RNG.random() < 0.3 and pos > 0:
                continue
            strum = 0.0
            for k, mn in enumerate(rh):
                put(epiano(midi(mn), dur * beat, vel * RNG.uniform(0.85, 1.0)), t0 + pos * beat + strum, pan=-0.3 + k * 0.15, gain=0.22)
                strum += 0.012
        # Bass: root on 1, fifth or approach on 3.
        bl = s.get("bassline", [(0, 0.0, 1.8), (7, 2.0, 1.8)])
        for (iv, pos, dur) in bl:
            put(bass(midi(root - 12 + iv), dur * beat, 0.85), t0 + pos * beat, 0.0, 0.5)
        # Pad
        if s.get("pad"):
            put(pad([midi(x) for x in notes[:3]], bar, s.get("bright", 0.3)), t0, 0.0, s["pad"])
        # Melody: pentatonic phrases every other bar.
        if s.get("melody") and b % 2 == 1:
            scale = [0, 2, 3, 7, 9, 10] if "m" in name and "maj" not in name else [0, 2, 4, 7, 9, 11]
            p = 0.0
            while p < 4.0:
                d = RNG.choice([0.5, 1.0, 1.5])
                if RNG.random() < 0.7:
                    note = root + 24 + RNG.choice(scale) + (12 if RNG.random() < 0.2 else 0)
                    put(epiano(midi(note), d * beat * 0.9, 0.5), t0 + p * beat + (swing * beat if p % 1 else 0), 0.25, 0.25 * s["melody"])
                p += d
        if s.get("bells") and b % 4 == 3:
            put(bell(midi(root + 36 + RNG.choice([0, 7, 10, 14])), 3.0, 0.3), t0 + 2 * beat, RNG.uniform(-0.6, 0.6), s["bells"])
        # Drums
        if s.get("drums"):
            dv = s["drums"]
            for beat_i in range(4):
                bt = t0 + beat_i * beat
                if beat_i in (0, 2) or (beat_i == 3 and RNG.random() < 0.25):
                    put(kick(0.9 if beat_i == 0 else 0.7), bt, 0.0, 0.55 * dv)
                if beat_i in (1, 3):
                    put(brush(0.7, True), bt, 0.15, 0.35 * dv)
                    if RNG.random() < 0.5:
                        put(rim(0.4), bt + 0.02, -0.2, 0.2 * dv)
                for sub in (0.0, 0.5):
                    off = (swing * beat) if sub else 0.0
                    put(hat(0.25 if sub else 0.35), bt + sub * beat + off, 0.35, 0.25 * dv)
                put(brush(0.3), bt + 0.5 * beat + swing * beat, -0.1, 0.15 * dv)

    mix_ = np.stack([L, R], axis=1)
    # Tape wobble and a gentle lowpass for warmth.
    wob = 1 + 0.0025 * np.sin(2 * np.pi * 0.33 * t_of(n)) + 0.001 * np.sin(2 * np.pi * 5.1 * t_of(n))
    idx = np.clip(np.cumsum(wob) - wob[0], 0, n - 1)
    for c in range(2):
        mix_[:, c] = np.interp(idx, np.arange(n), mix_[:, c])
    mix_ = lp(mix_, spec.get("lowpass", 6500), 2)
    wet = reverb(mix_, spec.get("reverb", 0.22), 2.4, 0.9, 5000)[:n]
    if spec.get("crackle", 0) > 0:
        c = crackle(length + tail) * spec["crackle"]
        wet[:, 0] += c
        wet[:, 1] += np.roll(c, 1200)
    # Seamless loop: fold the tail back onto the start.
    loop_n = n_of(length)
    out = wet[:loop_n].copy()
    t = wet[loop_n:]
    out[:len(t)] += t
    return rms_normalize(out, spec.get("rms", -20.0), -2.0)


TRACKS = {
    # Nights 1-2. 48 bars at 72 BPM (160 s): intro, the A tune twice, a B section in the relative
    # key, a drumless breakdown, B again with bells, then A to lead back round to the intro.
    "music_night": dict(seed=11, bpm=72, chords=["Dm9", "G13", "Cmaj9", "A7b9"], drums=0.8, crackle=1.0, melody=0.8,
                        comp=[(0.0, 2.6, 0.62), (2.5, 1.4, 0.45)], bassline=[(0, 0.0, 1.8), (7, 2.0, 1.4), (5, 3.5, 0.45)], pad=0.18, bright=0.25,
                        sections=[
                            dict(bars=4, drums=0.0, melody=0.0, pad=0.3, comp=[(0.0, 3.6, 0.5)], bassline=[(0, 0.0, 3.6)]),
                            dict(bars=8),
                            dict(bars=8, melody=1.1, drums=0.9),
                            dict(bars=8, chords=["Fmaj9", "Em9", "Dm9", "G13"], melody=0.6, bells=0.4, drums=0.7,
                                 comp=[(0.0, 1.4, 0.55), (1.5, 2.4, 0.5)]),
                            dict(bars=4, chords=["Bbmaj7#11", "A7b9"], drums=0.0, melody=0.0, pad=0.35, bells=0.6,
                                 comp=[(0.0, 3.8, 0.45)], bassline=[(0, 0.0, 3.6)]),
                            dict(bars=8, chords=["Fmaj9", "Em9", "Dm9", "G13"], melody=0.9, bells=0.5, drums=0.75),
                            dict(bars=8, melody=0.7, drums=0.85),
                        ]),
    # Nights 3-4: same room tone, a little more uneasy. 48 bars at 68 BPM (169 s).
    "music_night_mid": dict(seed=17, bpm=68, chords=["Fm9", "Bb13", "Ebmaj9", "C7b9"], drums=0.6, crackle=0.9, melody=0.6,
                            comp=[(0.0, 2.8, 0.55), (3.0, 0.9, 0.4)], bassline=[(0, 0.0, 1.8), (7, 2.0, 1.2), (-2, 3.5, 0.45)], pad=0.24, bright=0.2,
                            bells=0.3, lowpass=6000, rms=-20.5,
                            sections=[
                                dict(bars=4, drums=0.0, melody=0.0, bells=0.5, comp=[(0.0, 3.6, 0.45)], bassline=[(0, 0.0, 3.6)]),
                                dict(bars=8),
                                dict(bars=8, chords=["Dbmaj7#11", "Cm9", "Bbm9", "C7b9"], melody=0.8, drums=0.5),
                                dict(bars=8, melody=0.9, drums=0.7),
                                dict(bars=4, chords=["Abmaj7#11", "Gsus"], drums=0.0, melody=0.0, pad=0.4, bells=0.6,
                                     comp=[(0.0, 3.8, 0.4)], bassline=[(0, 0.0, 3.6)]),
                                dict(bars=8, chords=["Dbmaj7#11", "Cm9", "Bbm9", "C7b9"], melody=0.5, drums=0.65, bells=0.45),
                                dict(bars=8, melody=0.6, drums=0.6),
                            ]),
    # Nights 5-7. 44 bars at 64 BPM (165 s); stays sparse, the bells carry the tune.
    "music_night_tense": dict(seed=23, bpm=64, chords=["Cm9", "Abmaj7#11", "Fm9", "Gsus", "Cm9", "Abmaj7#11", "Dbmaj7", "G7b9"], drums=0.45,
                              crackle=0.8, melody=0.0, bells=0.7, sparse=True, comp=[(0.0, 3.6, 0.5)], bassline=[(0, 0.0, 0.9), (0, 1.0, 0.9), (0, 2.0, 0.9), (-1, 3.0, 0.9)],
                              pad=0.3, bright=0.15, lowpass=5200, rms=-21.0,
                              sections=[
                                  dict(bars=4, drums=0.0, bells=0.4, bassline=[(0, 0.0, 3.6)]),
                                  dict(bars=8),
                                  dict(bars=8, melody=0.35, drums=0.55),
                                  dict(bars=4, chords=["Fm9", "Dbmaj7", "Bbm9", "C7b9"], bells=0.9, drums=0.5,
                                       bassline=[(0, 0.0, 1.8), (7, 2.0, 1.8)]),
                                  dict(bars=4, chords=["Dbmaj7", "Bbm9", "Abmaj7#11", "G7b9"], bells=0.7, drums=0.6, melody=0.3,
                                       comp=[(0.0, 2.2, 0.5), (2.5, 1.2, 0.4)], bassline=[(0, 0.0, 1.4), (7, 1.5, 1.0), (5, 3.0, 0.9)]),
                                  dict(bars=4, chords=["Abmaj7#11", "G7b9"], drums=0.0, bells=0.5, pad=0.45,
                                       comp=[(0.0, 3.8, 0.4)], bassline=[(0, 0.0, 3.6)]),
                                  dict(bars=8, melody=0.25, drums=0.5),
                                  dict(bars=4, chords=["Cm9", "Abmaj7#11", "Dbmaj7", "G7b9"], drums=0.3, bells=1.0, pad=0.4,
                                       comp=[(0.0, 1.8, 0.45), (2.0, 1.8, 0.4)], bassline=[(0, 0.0, 1.8), (-5, 2.0, 1.8)]),
                              ]),
    "music_title": dict(seed=37, bpm=58, bars=12, chords=["Ebmaj9", "Cm9", "Abmaj7", "Bb6"], drums=0.0, crackle=1.2, melody=0.7, pad=0.35, bright=0.2,
                        comp=[(0.0, 3.8, 0.5)], bassline=[(0, 0.0, 3.6)], bells=0.5, reverb=0.32, rms=-21.0),
    "music_daylight": dict(seed=41, bpm=92, bars=16, chords=["Fmaj7", "Em7", "Dm9", "Cmaj9"], drums=0.7, crackle=0.5, melody=1.0, bright=0.5,
                           comp=[(0.0, 1.4, 0.6), (1.5, 1.0, 0.45), (3.0, 0.8, 0.5)], bassline=[(0, 0.0, 0.9), (7, 1.0, 0.9), (0, 2.0, 0.9), (4, 3.0, 0.9)], lowpass=8000, rms=-21.0),
    "music_ending_warm": dict(seed=53, bpm=66, bars=16, chords=["Dbmaj7", "Bbm9", "Gbmaj7", "Ab6"], drums=0.35, crackle=0.9, melody=1.0, pad=0.3, bright=0.35,
                              comp=[(0.0, 3.6, 0.6)], bassline=[(0, 0.0, 1.8), (7, 2.0, 1.8)], reverb=0.3),
    "music_ending_cold": dict(seed=67, bpm=56, bars=12, chords=["Am", "Fmaj7", "Dm9", "E7b9"], drums=0.0, crackle=1.2, melody=0.6, pad=0.4, bright=0.1, sparse=True,
                              comp=[(0.0, 3.8, 0.5)], bassline=[(0, 0.0, 3.6)], bells=0.6, reverb=0.35, lowpass=4500, rms=-22.0),
}


def to_ogg(x, path):
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
        write(tmp.name, x, normalize_peak=False)
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", tmp.name, "-c:a", "libvorbis", "-q:a", "5", path], check=True)
        os.unlink(tmp.name)
    print("wrote", os.path.relpath(path, ROOT), f"{len(x) / SR:.1f}s")


def check(name, x):
    """Length, how alike neighbouring stretches are, and the loop seam. Repetition is the mean
    correlation between each 4-bar block and the next (1.0 = the same four bars twice in a row);
    "max" is the most alike pair anywhere in the track."""
    spec = TRACKS[name]
    bar = 4 * 60.0 / spec["bpm"]
    mono = x.mean(axis=1)
    k = 24  # ~2 kHz envelope-ish resolution is plenty to compare arrangements
    m = mono[: len(mono) // k * k].reshape(-1, k).mean(axis=1)
    blk = int(round(4 * bar * SR / k))
    blocks = [m[i:i + blk] for i in range(0, len(m) - blk + 1, blk)]
    cors = []
    for a, b in zip(blocks, blocks[1:]):
        a, b = a - a.mean(), b - b.mean()
        cors.append(float(a @ b / (np.linalg.norm(a) * np.linalg.norm(b) + 1e-9)))
    seam = float(np.abs(x[0] - x[-1]).max())
    step = float(np.abs(np.diff(x, axis=0)).max())
    print(f"{name}: {len(x) / SR:.1f}s, {len(blocks)} four-bar blocks, neighbour correlation mean {np.mean(cors) if cors else 0:.2f}"
          f" max {max(cors) if cors else 0:.2f}, loop seam jump {seam:.4f} (largest in-track step {step:.4f})")


def main():
    args = sys.argv[1:]
    only_check = "--check" in args
    names = [a for a in args if not a.startswith("--")] or list(TRACKS)
    for name in names:
        x = render(TRACKS[name])
        check(name, x)
        if not only_check:
            to_ogg(x, os.path.join(OUT, name + ".ogg"))


if __name__ == "__main__":
    main()
