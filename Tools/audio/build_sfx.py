#!/usr/bin/env python3
"""Synthesises every sound effect into Assets/Resources/Audio (WAV, 44.1 kHz).

    Tools/.venv/bin/python Tools/audio/build_sfx.py            # everything
    Tools/.venv/bin/python Tools/audio/build_sfx.py cloth vac  # names containing these
"""
import os
import zlib
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from synth import *  # noqa: E402,F401

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio")
SOUNDS = {}


def sound(name, variations=1):
    def deco(fn):
        SOUNDS[name] = (fn, variations)
        return fn
    return deco


def out(name, x, **kw):
    write(os.path.join(OUT, name + ".wav"), x, **kw)


# =============================================================================================
# Cleaning
# =============================================================================================

@sound("cloth_loop")
def cloth_loop(v):
    d = 3.0
    base = bp(pink(d + 0.3), 700, 5200)
    hiss = bp(white(d + 0.3), 3000, 9000) * 0.25
    # Two-speed scrub rhythm with irregular pressure.
    t = t_of(len(base))
    rhythm = 0.55 + 0.45 * np.abs(np.sin(2 * np.pi * 2.3 * t + 0.6 * np.sin(2 * np.pi * 0.7 * t)))
    pressure = smooth_noise(d + 0.3, 6, 0.6, 1.0)
    fibres = grains(d + 0.3, 140, lambda i: bp(white(0.006), 2500, 8000) * env_exp(0.006, 0.002) * RNG.uniform(0.2, 1.0))
    x = (base + hiss) * rhythm * pressure + fibres[:len(base)] * 0.35
    x = lp(x, 7500)
    return loopify(x, 0.3)


@sound("marker_erase")
def marker_erase(v):
    # Whiteboard eraser: squeakier, felt-on-melamine.
    d = 2.5
    x = bp(pink(d + 0.3), 1200, 6000)
    t = t_of(len(x))
    squeak = sine(1800 + 220 * np.sin(2 * np.pi * 3.1 * t), d + 0.3) * 0.12 * smooth_noise(d + 0.3, 8, 0, 1) ** 3
    x = x * (0.6 + 0.4 * np.abs(np.sin(2 * np.pi * 2.6 * t))) + squeak
    return loopify(x, 0.3)


@sound("vacuum_loop")
def vacuum_loop(v):
    d = 4.0
    n = n_of(d + 0.4)
    t = t_of(n)
    f0 = 118 * (1 + 0.004 * np.sin(2 * np.pi * 0.4 * t))
    motor = saw(f0, d + 0.4, 25) * 0.35
    motor = lp(motor, 1800)
    whine_f = 1460 * (1 + 0.003 * np.sin(2 * np.pi * 5.3 * t))
    whine = sine(whine_f, d + 0.4) * 0.07 + sine(whine_f * 2.01, d + 0.4) * 0.025
    air = bp(white(d + 0.4), 350, 7000) * 0.5
    air = lp(air, 5000) * (0.9 + 0.1 * smooth_noise(d + 0.4, 3))
    rumble = lp(brown(d + 0.4), 160) * 0.35
    x = motor + whine + air + rumble
    return loopify(soft_clip(x * 0.9, 1.2), 0.4)


@sound("vacuum_suck")
def vacuum_suck(v):
    d = 2.5
    air = bp(white(d + 0.3), 900, 9000)
    whistle = sine(2600 + 300 * smooth_noise(d + 0.3, 4, -1, 1), d + 0.3) * 0.06
    ticks = grains(d + 0.3, 45, lambda i: hp(white(0.004), 2500) * env_exp(0.004, 0.0012) * RNG.uniform(0.3, 1.2))
    x = air * smooth_noise(d + 0.3, 5, 0.6, 1.0) + whistle + ticks[:len(air)] * 0.8
    return loopify(x, 0.3)


def _vac_ramp(up):
    d = 1.3 if up else 1.6
    n = n_of(d)
    t = t_of(n)
    k = t / d
    curve = (1 - np.exp(-k * 4)) if up else np.exp(-k * 3.2)
    f0 = 30 + 88 * curve
    motor = lp(saw(f0, d, 25), 1600) * 0.4 * (0.2 + 0.8 * curve)
    whine = sine(300 + 1160 * curve, d) * 0.06 * curve
    air = bp(white(d), 350, 6000) * 0.45 * curve
    click = np.zeros(n)
    if up:
        place(click, bp(white(0.02), 800, 5000) * env_exp(0.02, 0.004), 0)
    else:
        place(click, bp(white(0.02), 800, 5000) * env_exp(0.02, 0.004) * 0.8, 0)
    return fade(motor + whine + air + click * 0.8, 0.002, 0.15)


@sound("vacuum_start")
def vacuum_start(v):
    return _vac_ramp(True)


@sound("vacuum_stop")
def vacuum_stop(v):
    return _vac_ramp(False)


@sound("suck_pop", 5)
def suck_pop(v):
    d = 0.09
    f = np.linspace(RNG.uniform(1500, 2400), RNG.uniform(500, 800), n_of(d))
    x = sine(f, d) * env_exp(d, 0.025) * 0.6
    x += hp(white(d), 2000) * env_exp(d, 0.008) * 0.5
    return fade(x, 0.001, 0.01)


@sound("vacuum_clunk")
def vacuum_clunk(v):
    d = 0.7
    rattle = grains(0.35, 70, lambda i: bp(white(0.01), 1800, 6000) * env_exp(0.01, 0.003) * RNG.uniform(0.3, 1))
    metal = sum(sine(f, d) * env_exp(d, dk) * a for f, dk, a in [(2350, 0.12, 0.3), (3720, 0.08, 0.2), (5100, 0.05, 0.15)])
    thunk = lp(white(0.08), 600) * env_exp(0.08, 0.02)
    x = mix(metal, thunk * 0.8, rattle * 0.6)
    return fade(x, 0.001, 0.05)


@sound("squeegee_loop")
def squeegee_loop(v):
    d = 2.0
    n = n_of(d + 0.3)
    t = t_of(n)
    # Stick-slip squeak: a pulse train modulating a resonant tone.
    f = 820 + 260 * smooth_noise(d + 0.3, 2.5, -1, 1)
    tone = sine(f, d + 0.3) + 0.45 * sine(f * 2.0, d + 0.3) + 0.2 * sine(f * 3.02, d + 0.3)
    stick = 0.5 + 0.5 * np.sin(2 * np.pi * 38 * t + 2 * np.sin(2 * np.pi * 3 * t))
    rub = bp(white(d + 0.3), 1500, 6000) * 0.35
    wet = bp(pink(d + 0.3), 300, 2500) * 0.25
    x = tone * stick ** 2 * 0.35 + rub + wet
    return loopify(x, 0.3)


@sound("mop_loop")
def mop_loop(v):
    d = 3.0
    x = bp(pink(d + 0.3), 250, 3500)
    t = t_of(len(x))
    swish = 0.4 + 0.6 * np.abs(np.sin(2 * np.pi * 0.9 * t))
    drops = grains(d + 0.3, 12, lambda i: sine(np.linspace(RNG.uniform(900, 1600), 400, n_of(0.03)), 0.03) * env_exp(0.03, 0.008) * RNG.uniform(0.1, 0.4))
    x = x * swish + drops[:len(x)]
    return loopify(lp(x, 4000), 0.3)


@sound("mop_slosh", 3)
def mop_slosh(v):
    d = 0.7
    x = bp(pink(d), 200, 2200) * env_adsr(d, 0.03, 0.2, 0.5, 0.4)
    drops = grains(d, 25, lambda i: sine(np.linspace(RNG.uniform(700, 1400), 300, n_of(0.04)), 0.04) * env_exp(0.04, 0.01) * RNG.uniform(0.2, 0.6))
    return fade(mix(x, drops), 0.005, 0.1)


@sound("spray_loop")
def spray_loop(v):
    d = 1.2
    x = bp(white(d + 0.2), 2500, 11000) * (0.85 + 0.15 * smooth_noise(d + 0.2, 20))
    return loopify(x, 0.15)


@sound("spray", 3)
def spray(v):
    d = RNG.uniform(0.18, 0.3)
    x = bp(white(d), 2500, 11000) * env_adsr(d, 0.008, 0.05, 0.7, 0.08)
    click = bp(white(0.012), 1500, 5000) * env_exp(0.012, 0.003)
    return fade(mix(click * 0.6, x), 0.001, 0.02)


@sound("clean_ding")
def clean_ding(v):
    d = 1.6
    f = 1046.5  # C6; the game pitches it up a pentatonic ladder
    x = fm(f, 2.0, 2.2, d, env_exp(d, 0.08)) * env_exp(d, 0.55)
    x += sine(f * 2, d) * env_exp(d, 0.25) * 0.25
    x += sine(f * 3.01, d) * env_exp(d, 0.12) * 0.12
    x += sine(f * 0.5, d) * env_exp(d, 0.3) * 0.15
    x = reverb(x, 0.25, 1.0, 0.3, 7000)
    return fade(x, 0.001, 0.2)


@sound("sparkle", 3)
def sparkle(v):
    d = 1.1
    out = np.zeros(n_of(d))
    notes = [88, 91, 95, 98, 100, 103]
    RNG.shuffle(notes)
    for i, nn in enumerate(notes[:5]):
        f = midi(nn)
        g = sine(f, 0.4) * env_exp(0.4, 0.09) * 0.25 + sine(f * 2.7, 0.4) * env_exp(0.4, 0.03) * 0.08
        place(out, g, i * 0.045 + RNG.uniform(0, 0.02))
    return fade(reverb(out, 0.35, 1.0, 0.35, 9000), 0.001, 0.2)


@sound("room_complete")
def room_complete(v):
    d = 2.6
    out = np.zeros(n_of(d))
    chord = [72, 76, 79, 84, 88]
    for i, nn in enumerate(chord):
        f = midi(nn)
        g = (fm(f, 1.0, 1.2, 1.8, env_exp(1.8, 0.2)) * env_exp(1.8, 0.7)) * 0.22
        place(out, g, i * 0.07)
    out = reverb(out, 0.35, 1.6, 0.5, 6000)
    return fade(out, 0.001, 0.3)


@sound("tool_swap")
def tool_swap(v):
    d = 0.18
    x = bp(white(d), 1500, 7000) * env_exp(d, 0.03) * 0.5
    click = bp(white(0.01), 2000, 6000) * env_exp(0.01, 0.002)
    out = mix(x, np.concatenate([np.zeros(n_of(0.06)), click]))
    return fade(out, 0.001, 0.02)


# =============================================================================================
# Movement and handling
# =============================================================================================

@sound("step_carpet", 6)
def step_carpet(v):
    d = 0.16
    thud = lp(white(d), RNG.uniform(350, 550)) * env_exp(d, 0.025)
    low = sine(RNG.uniform(70, 95), d) * env_exp(d, 0.03) * 0.5
    scuff = bp(white(d), 1500, 4000) * env_exp(d, 0.012) * 0.12
    return fade(mix(thud, low, scuff), 0.002, 0.03)


@sound("step_tile", 6)
def step_tile(v):
    d = 0.35
    heel = bp(white(0.03), 1800, 5200) * env_exp(0.03, 0.004)
    body = bp(white(0.12), 150, 600) * env_exp(0.12, 0.025) * 0.6
    sole = bp(white(0.08), 600, 2500) * env_exp(0.08, 0.015) * 0.3
    x = mix(body, sole)
    place(x, heel * 0.9, RNG.uniform(0.0, 0.02))
    x = reverb(x, 0.18, 0.6, 0.18, 5000)
    return fade(x[:n_of(d)], 0.001, 0.05)


@sound("pickup", 4)
def pickup(v):
    d = 0.14
    x = bp(white(d), 1200, 6000) * env_exp(d, 0.02) * 0.6
    tick = sine(RNG.uniform(1800, 2600), 0.03) * env_exp(0.03, 0.006) * 0.3
    return fade(mix(x, tick), 0.001, 0.02)


@sound("paper_crumple", 4)
def paper_crumple(v):
    d = RNG.uniform(0.35, 0.55)
    x = grains(d, 380, lambda i: bp(white(0.01), RNG.uniform(1200, 3000), 9000) * env_exp(0.01, 0.002) * RNG.uniform(0.1, 1.0))
    x *= env_adsr(d, 0.02, 0.1, 0.8, 0.15)
    return fade(x, 0.002, 0.03)


@sound("paper_unfold", 3)
def paper_unfold(v):
    d = 0.6
    x = grains(d, 160, lambda i: bp(white(0.015), 900, 7000) * env_exp(0.015, 0.004) * RNG.uniform(0.1, 0.8))
    swish = bp(pink(d), 600, 4000) * env_adsr(d, 0.1, 0.2, 0.4, 0.3) * 0.4
    return fade(mix(x, swish), 0.002, 0.05)


@sound("toss", 3)
def toss(v):
    d = 0.3
    x = bp(pink(d), 400, 3000) * np.sin(np.linspace(0, np.pi, n_of(d))) ** 2
    return fade(x * 0.6, 0.002, 0.03)


@sound("bin_thunk", 4)
def bin_thunk(v):
    d = 0.45
    body = sum(sine(f * RNG.uniform(0.95, 1.05), d) * env_exp(d, dk) * a for f, dk, a in [(140, 0.09, 0.6), (290, 0.05, 0.3), (510, 0.03, 0.2)])
    rattle = bp(white(d), 400, 3500) * env_exp(d, 0.03) * 0.5
    x = mix(body, rattle)
    x = reverb(x, 0.12, 0.5, 0.15, 4000)
    return fade(x[:n_of(d)], 0.001, 0.05)


@sound("bin_metal", 3)
def bin_metal(v):
    d = 0.8
    parts = [(410, 0.25, 0.4), (1130, 0.18, 0.25), (2240, 0.1, 0.15), (3550, 0.06, 0.1)]
    x = sum(sine(f * RNG.uniform(0.97, 1.03), d) * env_exp(d, dk) * a for f, dk, a in parts)
    x += bp(white(d), 800, 5000) * env_exp(d, 0.02) * 0.4
    return fade(reverb(x, 0.15, 0.6, 0.2, 6000)[:n_of(d)], 0.001, 0.08)


@sound("can_clank", 3)
def can_clank(v):
    d = 0.5
    parts = [(1650, 0.12, 0.35), (2810, 0.08, 0.25), (4400, 0.05, 0.2), (6100, 0.03, 0.12)]
    x = sum(sine(f * RNG.uniform(0.96, 1.04), d) * env_exp(d, dk) * a for f, dk, a in parts)
    x += hp(white(d), 3000) * env_exp(d, 0.005) * 0.4
    return fade(x, 0.001, 0.05)


@sound("drop_soft", 3)
def drop_soft(v):
    d = 0.2
    x = lp(white(d), 900) * env_exp(d, 0.02)
    return fade(x, 0.001, 0.03)


@sound("drop_hard", 3)
def drop_hard(v):
    d = 0.35
    x = bp(white(d), 300, 4000) * env_exp(d, 0.012)
    x += sine(RNG.uniform(180, 320), d) * env_exp(d, 0.04) * 0.5
    return fade(reverb(x, 0.12, 0.5, 0.15, 5000)[:n_of(d)], 0.001, 0.05)


@sound("ceramic_clink", 3)
def ceramic_clink(v):
    d = 0.6
    parts = [(2100, 0.15, 0.4), (3300, 0.1, 0.3), (5200, 0.06, 0.2)]
    x = sum(sine(f * RNG.uniform(0.95, 1.05), d) * env_exp(d, dk) * a for f, dk, a in parts)
    x += hp(white(d), 2000) * env_exp(d, 0.003) * 0.4
    return fade(x, 0.001, 0.05)


@sound("swish")
def swish(v):
    # "Nice shot": a soft whoosh up into a chime.
    d = 0.9
    w = bp(pink(0.35), 600, 5000) * np.sin(np.linspace(0, np.pi, n_of(0.35))) ** 2 * 0.5
    ch = (sine(midi(84), 0.6) * env_exp(0.6, 0.15) + sine(midi(91), 0.6) * env_exp(0.6, 0.12) * 0.6) * 0.3
    out = np.zeros(n_of(d))
    place(out, w, 0)
    place(out, ch, 0.22)
    return fade(reverb(out, 0.25, 0.9, 0.3, 8000)[:n_of(d)], 0.001, 0.1)


@sound("snap_home", 3)
def snap_home(v):
    d = 0.25
    click = bp(white(0.015), 2000, 7000) * env_exp(0.015, 0.003)
    knock = sine(RNG.uniform(500, 700), d) * env_exp(d, 0.03) * 0.5
    blip = sine(midi(88), 0.18) * env_exp(0.18, 0.05) * 0.18
    out = mix(click, knock)
    place(out, blip, 0.03)
    return fade(out, 0.001, 0.03)


@sound("wrong_bin")
def wrong_bin(v):
    d = 0.4
    a = sine(midi(62), 0.18) * env_exp(0.18, 0.08) * 0.3
    b = sine(midi(58), 0.22) * env_exp(0.22, 0.1) * 0.3
    out = np.zeros(n_of(d))
    place(out, a, 0)
    place(out, b, 0.12)
    return fade(lp(out, 2500), 0.001, 0.05)


# =============================================================================================
# Office
# =============================================================================================

@sound("light_switch", 2)
def light_switch(v):
    d = 0.12
    a = bp(white(0.008), 2000, 8000) * env_exp(0.008, 0.0015)
    b = bp(white(0.02), 600, 3000) * env_exp(0.02, 0.004) * 0.5
    out = np.zeros(n_of(d))
    place(out, a, 0)
    place(out, b, 0.012)
    return fade(out, 0.0005, 0.02)


@sound("fluoro_on")
def fluoro_on(v):
    d = 1.6
    n = n_of(d)
    t = t_of(n)
    out = np.zeros(n)
    for at in [0.0, 0.09, 0.16, 0.34]:
        place(out, bp(white(0.012), 1500, 7000) * env_exp(0.012, 0.003) * RNG.uniform(0.3, 0.7), at)
    hum = (sine(120, d) * 0.5 + sine(240, d) * 0.3 + sine(360, d) * 0.15) * 0.15
    gate = np.clip((t - 0.34) * 3, 0, 1)
    flick = (np.sin(2 * np.pi * 9 * t) > 0) * (t < 0.34) * 0.7
    tink = sum(sine(f, d) * env_exp(d, 0.4) * a for f, a in [(3150, 0.05), (4720, 0.03)])
    out += hum * np.maximum(gate, flick) * np.exp(-np.maximum(t - 0.6, 0) * 1.5) + tink * (t > 0.34)
    return fade(out, 0.001, 0.2)


@sound("fluoro_hum")
def fluoro_hum(v):
    d = 3.0
    t = t_of(n_of(d + 0.3))
    x = sine(120, d + 0.3) * 0.5 + sine(240, d + 0.3) * 0.25 + sine(360, d + 0.3) * 0.12 + sine(480, d + 0.3) * 0.06
    buzz = bp(white(d + 0.3), 2000, 6000) * 0.05 * (0.5 + 0.5 * np.sin(2 * np.pi * 120 * t))
    return loopify(x + buzz, 0.3)


@sound("door_open")
def door_open(v):
    d = 1.0
    latch = bp(white(0.03), 1200, 5000) * env_exp(0.03, 0.006)
    creak_f = 300 + 180 * smooth_noise(0.6, 6, -1, 1)
    creak = sine(creak_f, 0.6) * (0.3 + 0.7 * smooth_noise(0.6, 25)) * env_adsr(0.6, 0.05, 0.2, 0.6, 0.2) * 0.12
    swoosh = bp(pink(0.7), 200, 1500) * env_adsr(0.7, 0.2, 0.2, 0.4, 0.3) * 0.25
    out = np.zeros(n_of(d))
    place(out, latch, 0)
    place(out, creak, 0.05)
    place(out, swoosh, 0.1)
    return fade(reverb(out, 0.18, 0.8, 0.25, 4000)[:n_of(d)], 0.001, 0.1)


@sound("door_close")
def door_close(v):
    d = 0.9
    thud = lp(white(0.2), 400) * env_exp(0.2, 0.04)
    latch = bp(white(0.03), 1500, 6000) * env_exp(0.03, 0.005) * 0.6
    out = np.zeros(n_of(d))
    place(out, thud, 0)
    place(out, latch, 0.03)
    return fade(reverb(out, 0.22, 0.8, 0.25, 4000)[:n_of(d)], 0.001, 0.1)


@sound("chair_roll", 2)
def chair_roll(v):
    d = 0.8
    x = bp(pink(d), 150, 1800) * env_adsr(d, 0.05, 0.2, 0.7, 0.3)
    rattle = grains(d, 60, lambda i: bp(white(0.006), 1500, 5000) * env_exp(0.006, 0.002) * RNG.uniform(0.1, 0.4))
    squeak = sine(1250 + 150 * smooth_noise(0.25, 10, -1, 1), 0.25) * env_adsr(0.25, 0.02, 0.05, 0.5, 0.1) * 0.08
    out = mix(x * 0.7, rattle[:n_of(d)] * 0.5)
    place(out, squeak, d - 0.3)
    return fade(out, 0.005, 0.08)


@sound("monitor_off", 2)
def monitor_off(v):
    d = 0.35
    f = np.linspace(2200, 300, n_of(0.12))
    blip = sine(f, 0.12) * env_exp(0.12, 0.05) * 0.25
    click = bp(white(0.01), 2000, 6000) * env_exp(0.01, 0.002) * 0.5
    out = np.zeros(n_of(d))
    place(out, click, 0)
    place(out, blip, 0.01)
    return fade(out, 0.0005, 0.05)


@sound("drawer_open", 2)
def drawer_open(v):
    d = 0.6
    x = bp(pink(0.45), 300, 3000) * env_adsr(0.45, 0.03, 0.1, 0.8, 0.15) * 0.5
    clunk = lp(white(0.08), 700) * env_exp(0.08, 0.02)
    out = np.zeros(n_of(d))
    place(out, x, 0)
    place(out, clunk, 0.45)
    return fade(out, 0.002, 0.05)


@sound("punch_clock")
def punch_clock(v):
    d = 1.0
    ka = lp(white(0.06), 1200) * env_exp(0.06, 0.012)
    chunk = mix(lp(white(0.12), 500) * env_exp(0.12, 0.03), sine(95, 0.15) * env_exp(0.15, 0.04) * 0.8)
    bell = sum(sine(f, 0.8) * env_exp(0.8, 0.25) * a for f, a in [(1320, 0.12), (2640, 0.05)])
    out = np.zeros(n_of(d))
    place(out, ka, 0)
    place(out, chunk, 0.11)
    place(out, bell, 0.12)
    return fade(reverb(out, 0.2, 0.8, 0.25, 5000)[:n_of(d)], 0.001, 0.1)


@sound("shredder")
def shredder(v):
    d = 2.2
    t = t_of(n_of(d))
    motor = lp(saw(92 + 6 * np.sin(2 * np.pi * 7 * t), d, 30), 2500) * 0.35
    grind = bp(white(d), 600, 5000) * (0.6 + 0.4 * (np.sin(2 * np.pi * 23 * t) > 0)) * 0.5
    tear = grains(d, 90, lambda i: bp(white(0.012), 1500, 7000) * env_exp(0.012, 0.003) * RNG.uniform(0.2, 0.9))
    x = (motor + grind + tear[:n_of(d)] * 0.5) * env_adsr(d, 0.08, 0.2, 0.9, 0.4)
    return fade(x, 0.005, 0.1)


@sound("elevator_ding")
def elevator_ding(v):
    d = 2.5
    out = np.zeros(n_of(d))
    for i, f in enumerate([midi(79), midi(75)]):
        g = (fm(f, 3.5, 1.5, 1.6, env_exp(1.6, 0.15)) * env_exp(1.6, 0.6)) * 0.3
        place(out, g, i * 0.42)
    return fade(reverb(out, 0.45, 2.0, 0.7, 4000)[:n_of(d)], 0.001, 0.3)


# =============================================================================================
# UI
# =============================================================================================

@sound("ui_hover")
def ui_hover(v):
    d = 0.06
    return fade(sine(midi(96), d) * env_exp(d, 0.015) * 0.2 + hp(white(d), 4000) * env_exp(d, 0.004) * 0.1, 0.001, 0.01)


@sound("ui_click")
def ui_click(v):
    d = 0.12
    x = sine(midi(84), d) * env_exp(d, 0.03) * 0.4 + sine(midi(91), d) * env_exp(d, 0.02) * 0.2
    x += bp(white(d), 2000, 7000) * env_exp(d, 0.003) * 0.3
    return fade(x, 0.001, 0.02)


@sound("ui_back")
def ui_back(v):
    d = 0.14
    f = np.linspace(midi(79), midi(72), n_of(d))
    return fade(sine(f, d) * env_exp(d, 0.05) * 0.35, 0.001, 0.02)


@sound("ui_page", 3)
def ui_page(v):
    return paper_unfold(v)


@sound("pen_scratch", 2)
def pen_scratch(v):
    d = 0.35
    x = grains(d, 260, lambda i: bp(white(0.006), 2500, 9000) * env_exp(0.006, 0.0015) * RNG.uniform(0.1, 0.7))
    return fade(x * env_adsr(d, 0.02, 0.05, 0.9, 0.08), 0.001, 0.02)


@sound("notify")
def notify(v):
    d = 0.6
    out = np.zeros(n_of(d))
    place(out, sine(midi(88), 0.25) * env_exp(0.25, 0.07) * 0.3, 0)
    place(out, sine(midi(93), 0.3) * env_exp(0.3, 0.09) * 0.3, 0.08)
    return fade(reverb(out, 0.2, 0.6, 0.2, 8000)[:n_of(d)], 0.001, 0.05)


@sound("discover")
def discover(v):
    # Discovery stinger: low swell, a minor-ninth shimmer and a soft bell.
    d = 3.2
    t = t_of(n_of(d))
    swell = lp(brown(d), 200) * np.clip(t / 0.8, 0, 1) * np.exp(-np.maximum(t - 1.2, 0) * 1.2) * 0.4
    out = swell.copy()
    for i, nn in enumerate([57, 64, 68, 71, 74]):
        g = fm(midi(nn + 12), 1.0, 0.8, 2.6, env_exp(2.6, 0.6)) * env_adsr(2.6, 0.3, 0.4, 0.6, 1.2) * 0.12
        place(out, g, 0.25 + i * 0.09)
    bell = fm(midi(81), 3.5, 1.2, 2.0, env_exp(2.0, 0.2)) * env_exp(2.0, 0.7) * 0.2
    place(out, bell, 0.9)
    return fade(reverb(out, 0.45, 2.2, 0.8, 5000)[:n_of(d)], 0.01, 0.4)


# =============================================================================================
# Ambience beds (stereo, seamless)
# =============================================================================================

@sound("amb_hvac")
def amb_hvac(v):
    d = 24.0
    l = lp(brown(d + 1), 380) * 0.6 + lp(pink(d + 1), 1200) * 0.08
    r = lp(brown(d + 1), 380) * 0.6 + lp(pink(d + 1), 1200) * 0.08
    hum = (sine(60, d + 1) * 0.04 + sine(120, d + 1) * 0.025 + sine(180, d + 1) * 0.01)
    swell = smooth_noise(d + 1, 0.15, 0.8, 1.0)
    x = np.stack([(l + hum) * swell, (r + hum) * swell], axis=1)
    return loopify(x, 1.5)


@sound("amb_city")
def amb_city(v):
    d = 30.0
    base_l = lp(brown(d + 2), 250) * smooth_noise(d + 2, 0.2, 0.5, 1.0)
    base_r = lp(brown(d + 2), 250) * smooth_noise(d + 2, 0.2, 0.5, 1.0)
    out = np.stack([base_l, base_r], axis=1) * 0.6
    # Distant cars passing by (filtered noise swells panned across).
    for k in range(7):
        dur = RNG.uniform(3, 6)
        at = RNG.uniform(0, d - dur)
        car = lp(pink(dur), RNG.uniform(500, 900)) * np.sin(np.linspace(0, np.pi, n_of(dur))) ** 2 * 0.35
        pan = np.linspace(RNG.uniform(0, 0.3), RNG.uniform(0.7, 1.0), n_of(dur))
        if RNG.random() > 0.5:
            pan = pan[::-1]
        st = np.stack([car * np.cos(pan * np.pi / 2), car * np.sin(pan * np.pi / 2)], axis=1)
        place(out, st, at)
    return loopify(out, 2.0)


@sound("amb_rain")
def amb_rain(v):
    d = 20.0
    l = bp(pink(d + 1), 400, 9000) * 0.5
    r = bp(pink(d + 1), 400, 9000) * 0.5
    drops_l = grains(d + 1, 120, lambda i: bp(white(0.01), 1500, 8000) * env_exp(0.01, 0.003) * RNG.uniform(0.05, 0.4))
    drops_r = grains(d + 1, 120, lambda i: bp(white(0.01), 1500, 8000) * env_exp(0.01, 0.003) * RNG.uniform(0.05, 0.4))
    x = np.stack([l + drops_l[:len(l)], r + drops_r[:len(r)]], axis=1)
    return loopify(lp(x, 7000), 1.5)


@sound("amb_fridge")
def amb_fridge(v):
    d = 10.0
    t = t_of(n_of(d + 1))
    x = sine(98, d + 1) * 0.3 + sine(196, d + 1) * 0.12 + lp(brown(d + 1), 300) * 0.3
    x *= 0.85 + 0.15 * np.sin(2 * np.pi * 0.3 * t)
    return loopify(x, 1.0)


@sound("thunder", 2)
def thunder(v):
    d = 5.0
    t = t_of(n_of(d))
    crack = bp(white(0.3), 300, 4000) * env_exp(0.3, 0.06) * 0.6
    roll = lp(brown(d), 160) * np.exp(-t / 1.6) * (0.6 + 0.4 * smooth_noise(d, 4))
    out = mix(roll, crack * RNG.uniform(0.3, 1.0))
    return fade(reverb(out, 0.4, 2.0, 0.8, 2000)[:n_of(d)], 0.01, 0.6)


def main():
    filters = sys.argv[1:]
    os.makedirs(OUT, exist_ok=True)
    count = 0
    for name, (fn, variations) in SOUNDS.items():
        if filters and not any(f in name for f in filters):
            continue
        for i in range(variations):
            seed(zlib.crc32(f"{name}:{i}".encode()))
            x = fn(i)
            fname = name if variations == 1 else f"{name}_{i + 1}"
            peak_db = -1.0
            if name.startswith("amb_") or name.endswith("_loop") or name == "fluoro_hum":
                peak_db = -3.0
            out(fname, x, peak_db=peak_db)
            count += 1
    print(f"wrote {count} files to {os.path.relpath(OUT, ROOT)}")


if __name__ == "__main__":
    main()
