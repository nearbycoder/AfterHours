#!/usr/bin/env python3
"""Cuts the After Hours feature trailer from the clips filmed by Tools/trailer/record.sh.

    Tools/.venv/bin/python Tools/trailer/make_trailer.py [--clips Recordings/trailer] [--out docs/media/AfterHours-trailer.mp4]

Every beat in BEATS is a stretch of one clip with an animated caption. Beats are joined with xfade
transitions; the game audio from each clip is laid on the same timeline, and the game's own music
tracks make the bed, which ducks under the sound effects and dips for the highlights. Needs ffmpeg
(libx264, aac) and Python with numpy and Pillow (Tools/.venv).
"""
import argparse
import json
import os
import shutil
import subprocess
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cards  # noqa: E402

ROOT = cards.ROOT
FPS = 30
SR = 48000
AUDIO_DIR = os.path.join(ROOT, "Assets", "Resources", "Audio")

# ---------------------------------------------------------------------------------------------
# The cut. Each beat: clip, start (s into the clip), dur (s on screen), speed, transition into it
# (xfade name, seconds), and an optional caption (number, title, sub) or handwritten line.
# Special clips: "@title" (title card) and "@end" (end card), built over a filmed backdrop.
# ---------------------------------------------------------------------------------------------
BEATS = []  # filled in below

def beat(clip, start, dur, cap=None, hand=None, speed=1.0, trans="fade", tdur=0.4, cap_at=0.3, gain=1.0, duck=None,
         section=None, backdrop=None, look=None, blur=None, split=None):
    """duck=(at, length, depth): dip the music under a highlight. section marks where a music cue starts.
    look: extra ffmpeg filters for this shot (e.g. a gamma lift). blur: [(x, y, w, h)] boxes to blur (spoilers).
    split=(clip, left label, right label): this clip on the left and the other (filmed the same way) on the
    right, the divider sliding left over the beat. cap number "#" is replaced by the next feature number."""
    if cap and cap[0] == "#":
        NUMBER[0] += 1
        cap = (f"{NUMBER[0]:02d}",) + tuple(cap[1:])
    BEATS.append(dict(clip=clip, start=start, dur=dur, cap=cap, hand=hand, speed=speed, trans=trans, tdur=tdur,
                      cap_at=cap_at, gain=gain, duck=duck, section=section, backdrop=backdrop, look=look, blur=blur,
                      split=split))


NUMBER = [0]


def run(cmd, quiet=True):
    if not quiet:
        print(" ".join(cmd))
    r = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    if r.returncode != 0:
        print(r.stderr[-4000:])
        raise SystemExit(f"command failed: {cmd[:6]}...")
    return r


def nice(cmd):
    return ["nice", "-n", "10"] + cmd


def clip_frames(clips, name):
    d = os.path.join(clips, name)
    n = len([f for f in os.listdir(d) if f.endswith(".jpg")])
    return d, n


def read_audio(path, start=0.0, dur=None, speed=1.0):
    """Decode (a stretch of) an audio file to float32 stereo at SR."""
    cmd = ["ffmpeg", "-v", "error", "-ss", f"{start:.4f}"]
    if dur is not None:
        cmd += ["-t", f"{dur * speed:.4f}"]
    cmd += ["-i", path]
    if abs(speed - 1.0) > 1e-3:
        cmd += ["-af", f"atempo={speed:.4f}"]
    cmd += ["-f", "f32le", "-ac", "2", "-ar", str(SR), "-"]
    raw = subprocess.run(cmd, stdout=subprocess.PIPE, check=True).stdout
    a = np.frombuffer(raw, np.float32).reshape(-1, 2).copy()
    if dur is not None:
        n = int(round(dur * SR))
        if len(a) < n:
            a = np.vstack([a, np.zeros((n - len(a), 2), np.float32)])
        a = a[:n]
    return a


def fade(a, fin, fout):
    n = len(a)
    if fin > 0:
        k = min(n, int(fin * SR))
        a[:k] *= np.linspace(0, 1, k, dtype=np.float32)[:, None] ** 1.5
    if fout > 0:
        k = min(n, int(fout * SR))
        a[n - k:] *= np.linspace(1, 0, k, dtype=np.float32)[:, None] ** 1.5
    return a


def smooth_env(x, attack, release):
    """One-pole envelope follower over a per-sample level signal (seconds)."""
    out = np.empty_like(x)
    ga, gr = np.exp(-1.0 / (attack * SR)), np.exp(-1.0 / (release * SR))
    # Downsample for speed: work at 1 kHz then interpolate.
    step = SR // 1000
    xs = x[::step]
    o = np.empty_like(xs)
    v = 0.0
    ga, gr = np.exp(-1.0 / (attack * 1000)), np.exp(-1.0 / (release * 1000))
    for i, s in enumerate(xs):
        g = ga if s > v else gr
        v = g * v + (1 - g) * s
        o[i] = v
    out = np.interp(np.arange(len(x)), np.arange(len(xs)) * step, o).astype(np.float32)
    return out


# ---------------------------------------------------------------------------------------------

def build(clips, out, work, keep):
    os.makedirs(work, exist_ok=True)
    capdir = os.path.join(work, "captions")
    os.makedirs(capdir, exist_ok=True)

    # Timeline
    t = 0.0
    for i, b in enumerate(BEATS):
        if i == 0:
            b["t0"] = 0.0
            b["tdur"] = 0.0
        else:
            b["t0"] = t - b["tdur"]
        t = b["t0"] + b["dur"]
        if b["section"]:
            SECTIONS[b["section"]] = b["t0"]
    total = t
    print(f"{len(BEATS)} beats, {total:.1f} s")

    segs = []
    for i, b in enumerate(BEATS):
        seg = os.path.join(work, f"seg{i:02d}.mp4")
        segs.append(seg)
        # Re-render a segment only when its beat (or the next beat's transition) changed.
        spec = json.dumps({k: v for k, v in b.items() if k not in ("t0",)} | {"next": BEATS[i + 1]["tdur"] if i + 1 < len(BEATS) else None},
                          sort_keys=True, default=str)
        stamp = seg + ".json"
        if os.path.exists(seg) and os.path.exists(stamp) and open(stamp).read() == spec:
            continue
        render_segment(clips, b, i, seg, capdir)
        with open(stamp, "w") as f:
            f.write(spec)

    # ---- video: chain the xfades -----------------------------------------------------------
    inputs, chain = [], []
    for i, s in enumerate(segs):
        inputs += ["-i", s]
        chain.append(f"[{i}:v]settb=AVTB,fps={FPS},format=yuv420p[v{i}]")
    last = "v0"
    for i in range(1, len(segs)):
        b = BEATS[i]
        lbl = f"x{i}"
        if b["trans"] == "cut" or b["tdur"] <= 0.001:
            chain.append(f"[{last}][v{i}]concat=n=2:v=1:a=0[{lbl}]")
        else:
            chain.append(f"[{last}][v{i}]xfade=transition={b['trans']}:duration={b['tdur']:.3f}:offset={b['t0']:.3f}[{lbl}]")
        last = lbl
    video = os.path.join(work, "video.mp4")
    graph = ";".join(chain)
    with open(os.path.join(work, "video.graph"), "w") as f:
        f.write(graph)
    run(nice(["ffmpeg", "-y", "-v", "error"] + inputs + ["-/filter_complex", os.path.join(work, "video.graph"), "-map", f"[{last}]",
              "-c:v", "libx264", "-preset", "medium", "-crf", "12", "-pix_fmt", "yuv420p", "-r", str(FPS), video]))

    # ---- audio ----------------------------------------------------------------------------
    n = int(round(total * SR)) + SR
    sfx = np.zeros((n, 2), np.float32)
    for i, b in enumerate(BEATS):
        a = segment_audio(clips, b)
        if a is None:
            continue
        fin = b["tdur"] if i > 0 else 0.05
        fout = BEATS[i + 1]["tdur"] if i + 1 < len(BEATS) else 0.8
        a = fade(a, fin, max(fout, 0.05)) * b["gain"]
        s0 = int(round(b["t0"] * SR))
        sfx[s0:s0 + len(a)] += a[: max(0, min(len(a), n - s0))]

    music = music_bed(total, n)

    # Ducking: follow the effects level, plus manual dips at the highlights.
    # The clips carry a steady room tone (HVAC, city, rain) around -18 dB, so only hits well above
    # it pull the music down: a -6 dBFS hit ducks the bed by about 6 dB.
    level = np.abs(sfx).max(axis=1)
    env = smooth_env(level, 0.015, 0.5)
    duck = 1.0 / (1.0 + 4.0 * np.clip(env - 0.25, 0, None))
    duck = np.maximum(duck, 0.45)
    manual = np.ones(n, np.float32)
    for b in BEATS:
        if b["duck"]:
            at, length, depth = b["duck"]
            s0 = int((b["t0"] + at) * SR)
            k = int(length * SR)
            ramp = int(0.35 * SR)
            shape = np.ones(k + 2 * ramp, np.float32) * depth
            shape[:ramp] = np.linspace(1, depth, ramp)
            shape[-ramp:] = np.linspace(depth, 1, ramp)
            e = min(n, s0 + len(shape))
            if s0 < n:
                manual[s0:e] = np.minimum(manual[s0:e], shape[: e - s0])
    music *= (duck * manual)[:, None]

    mix = sfx * 0.8 + music
    mix = mix[: int(round(total * SR))]
    peak = np.abs(mix).max()
    if peak > 0.98:
        mix *= 0.98 / peak
    raw_wav = os.path.join(work, "mix_raw.wav")
    write_wav(raw_wav, mix)
    stems = {"sfx": sfx[: len(mix)], "music": music[: len(mix)]}
    for k, v in stems.items():
        write_wav(os.path.join(work, f"stem_{k}.wav"), v)

    # Loudness: two-pass loudnorm to -14 LUFS, -1.5 dBTP.
    r = run(["ffmpeg", "-v", "info", "-i", raw_wav, "-af", "loudnorm=I=-14:TP=-1.5:LRA=11:print_format=json", "-f", "null", "-"])
    js = json.loads(r.stderr[r.stderr.rindex("{"):r.stderr.rindex("}") + 1])
    af = (f"loudnorm=I=-14:TP=-1.5:LRA=11:measured_I={js['input_i']}:measured_TP={js['input_tp']}:measured_LRA={js['input_lra']}:"
          f"measured_thresh={js['input_thresh']}:offset={js['target_offset']}:linear=true,aresample={SR}")
    mix_wav = os.path.join(work, "mix.wav")
    run(["ffmpeg", "-y", "-v", "error", "-i", raw_wav, "-af", af, "-ar", str(SR), mix_wav])

    # ---- final encode (two-pass, sized for the README: well under 40 MB) ---------------------
    vbr = "2350k"
    os.makedirs(os.path.dirname(out), exist_ok=True)
    passlog = os.path.join(work, "x264pass")
    common = ["-i", video, "-i", mix_wav, "-map", "0:v", "-map", "1:a", "-c:v", "libx264", "-preset", "slow", "-b:v", vbr,
              "-maxrate", "6000k", "-bufsize", "8000k", "-pix_fmt", "yuv420p", "-profile:v", "high", "-r", str(FPS),
              "-g", str(FPS * 2), "-passlogfile", passlog]
    run(nice(["ffmpeg", "-y", "-v", "error"] + common + ["-pass", "1", "-an", "-f", "mp4", "/dev/null"]))
    run(nice(["ffmpeg", "-y", "-v", "error"] + common + ["-pass", "2", "-c:a", "aac", "-b:a", "192k", "-ar", str(SR),
                                                         "-movflags", "+faststart", out]))
    size = os.path.getsize(out) / 1e6
    print(f"wrote {out}: {total:.1f} s, {size:.1f} MB")
    with open(os.path.join(work, "timeline.json"), "w") as f:
        json.dump([{k: b[k] for k in ("clip", "start", "dur", "t0", "trans", "tdur", "cap", "hand")} for b in BEATS], f, indent=1)
    if not keep:
        for s in segs:
            os.remove(s)
            os.remove(s + ".json")


def write_wav(path, a):
    pcm = np.clip(a, -1, 1)
    run_in = ["ffmpeg", "-y", "-v", "error", "-f", "f32le", "-ac", "2", "-ar", str(SR), "-i", "-", "-c:a", "pcm_s16le", path]
    subprocess.run(run_in, input=pcm.astype(np.float32).tobytes(), check=True)


def segment_audio(clips, b):
    if b["clip"] in ("@title", "@end"):
        return None
    wav = os.path.join(clips, b["clip"], "audio.wav")
    if not os.path.exists(wav):
        return None
    return read_audio(wav, b["start"], b["dur"], b["speed"])


def music_bed(total, n):
    """The game's own score: title theme under the open and title, the calm night theme under the
    features, the tense theme from the late nights, the warm ending theme for the end card."""
    plan = [
        # (track, track offset, timeline start, timeline end, gain)
        ("music_night_tense", 0.0, 0.0, SECTIONS["title"] + 0.6, 0.62),
        ("music_title", 0.0, SECTIONS["title"] - 0.4, SECTIONS["features"] + 1.0, 1.0),
        ("music_night", 6.6667, SECTIONS["features"], SECTIONS["late"] + 1.5, 1.05),
        ("music_night_tense", 13.3333, SECTIONS["late"], SECTIONS["end"] + 1.5, 1.1),
        ("music_ending_warm", 0.0, SECTIONS["end"], total + 0.5, 1.05),
    ]
    bed = np.zeros((n, 2), np.float32)
    for track, off, a, b, gain in plan:
        dur = max(0.1, min(b, total + 0.5) - a)
        src = os.path.join(AUDIO_DIR, track + ".ogg")
        x = loop_read(src, off, dur)
        x = fade(x, 1.4 if a > 0 else 0.6, 1.6) * gain
        s0 = int(round(a * SR))
        e = min(n, s0 + len(x))
        bed[s0:e] += x[: e - s0]
    # Fade the whole bed out at the very end.
    k = int(2.5 * SR)
    end = int(round(total * SR))
    bed[end - k:end] *= np.linspace(1, 0, k, dtype=np.float32)[:, None]
    bed[end:] = 0
    return bed


def loop_read(path, off, dur):
    full = read_audio(path)
    reps = int(np.ceil((off + dur) * SR / len(full))) + 1
    x = np.vstack([full] * reps)
    s = int(off * SR)
    return x[s:s + int(dur * SR)].copy()


# ---- video segments --------------------------------------------------------------------------

def render_segment(clips, b, i, seg, capdir):
    dur = b["dur"]
    nframes = int(round(dur * FPS))
    filters = []
    extra_inputs = []
    if b["clip"] == "@title":
        return render_title(clips, b, seg, capdir)
    if b["clip"] == "@end":
        return render_end(clips, b, seg, capdir)
    d, total = clip_frames(clips, b["clip"])
    first = int(round(b["start"] * FPS))
    need = int(np.ceil(nframes * b["speed"]))
    if first + need > total:
        print(f"  ! beat {i} {b['clip']}: wants frames {first}-{first + need}, clip has {total}; holding the last frame")
    src = ["-framerate", f"{FPS * b['speed']:.4f}", "-start_number", str(first), "-i", os.path.join(d, "%06d.jpg")]
    vf = f"[0:v]fps={FPS},tpad=stop_mode=clone:stop_duration={dur:.3f},trim=duration={dur:.3f},setpts=PTS-STARTPTS,format=yuv420p"
    vf += ",scale=1920:1080:flags=lanczos"
    if b["look"]:
        vf += "," + b["look"]
    graph = vf + "[base]"
    last = "base"
    k = 1
    if b["split"]:
        other, left, right = b["split"]
        d2, total2 = clip_frames(clips, other)
        if first + need > total2:
            print(f"  ! beat {i} {other}: wants frames {first}-{first + need}, clip has {total2}; holding the last frame")
        extra_inputs += ["-framerate", f"{FPS * b['speed']:.4f}", "-start_number", str(first), "-i", os.path.join(d2, "%06d.jpg")]
        png = os.path.join(capdir, f"split{i:02d}.png")
        cards.split_labels(left, right, png)
        extra_inputs += ["-loop", "1", "-framerate", str(FPS), "-t", f"{dur:.3f}", "-i", png]
        # The divider eases from 80% to 30% of the width; a thin bright line marks it.
        edge = f"(W*(0.8-0.5*(clip(T/{dur:.3f},0,1)*clip(T/{dur:.3f},0,1)*(3-2*clip(T/{dur:.3f},0,1)))))"
        pick = f"if(lt(X,{edge}),A,B)"
        graph += (f";[{k}:v]fps={FPS},tpad=stop_mode=clone:stop_duration={dur:.3f},trim=duration={dur:.3f},setpts=PTS-STARTPTS,"
                  f"format=yuv420p,scale=1920:1080:flags=lanczos" + ("," + b["look"] if b["look"] else "") + "[other];"
                  f"[{last}][other]blend=c0_expr='if(lt(abs(X-{edge}),2),225,{pick})':c1_expr='{pick}':c2_expr='{pick}'[sp];"
                  f"[{k + 1}:v]format=rgba,fade=t=in:st=0.15:d=0.4:alpha=1[lab];[sp][lab]overlay=format=auto[spl]")
        last = "spl"
        k += 2
    for j, (x, y, w, h) in enumerate(b["blur"] or []):
        graph += (f";[{last}]split[bl{j}a][bl{j}b];[bl{j}b]crop={w}:{h}:{x}:{y},boxblur=luma_radius=22:luma_power=3:chroma_radius=22:chroma_power=3[bl{j}c];"
                  f"[bl{j}a][bl{j}c]overlay={x}:{y}[bl{j}]")
        last = f"bl{j}"
    if b["cap"] or b["hand"]:
        png = os.path.join(capdir, f"cap{i:02d}.png")
        if b["cap"]:
            num, title, sub = b["cap"]
            cards.caption(num, title, sub, png)
        else:
            cards.handwritten(b["hand"], png)
        extra_inputs += ["-loop", "1", "-framerate", str(FPS), "-t", f"{dur:.3f}", "-i", png]
        t0 = b["cap_at"]
        t1 = max(t0 + 0.8, dur - 0.55 - (BEATS[i + 1]["tdur"] if i + 1 < len(BEATS) else 0))
        slide = 0.6
        graph += (f";[{k}:v]format=rgba,fade=t=in:st={t0:.3f}:d=0.45:alpha=1,fade=t=out:st={t1:.3f}:d=0.4:alpha=1[cap];"
                  f"[{last}][cap]overlay=x='-70*pow(1-clip((t-{t0:.3f})/{slide},0,1),3)':y=0:format=auto[o]")
        last = "o"
        k += 1
    run(nice(["ffmpeg", "-y", "-v", "error"] + src + extra_inputs + ["-filter_complex", graph, "-map", f"[{last}]",
                                                                    "-frames:v", str(nframes), "-c:v", "libx264", "-preset", "veryfast",
                                                                    "-crf", "12", "-pix_fmt", "yuv420p", "-r", str(FPS), seg]))


def backdrop(clips, b, dark):
    d, total = clip_frames(clips, b["backdrop"])
    first = int(round(b["start"] * FPS))
    src = ["-framerate", str(FPS), "-start_number", str(first), "-i", os.path.join(d, "%06d.jpg")]
    vf = (f"[0:v]fps={FPS},tpad=stop_mode=clone:stop_duration={b['dur']:.3f},trim=duration={b['dur']:.3f},setpts=PTS-STARTPTS,"
          f"scale=1920:1080,eq=brightness={-dark:.2f}:saturation=0.85,gblur=sigma=2.2,format=yuv420p[bg]")
    return src, vf


def render_title(clips, b, seg, capdir):
    logo, dim, tag = (os.path.join(capdir, n) for n in ("title_logo.png", "title_logo_dim.png", "title_tag.png"))
    cards.title_card(logo, dim, tag)
    src, vf = backdrop(clips, b, 0.0)
    dur = b["dur"]
    ins = []
    for p in (logo, dim, tag):
        ins += ["-loop", "1", "-framerate", str(FPS), "-t", f"{dur:.3f}", "-i", p]
    # Tube-light warm-up: the logo stutters between full and dim before it holds.
    on = "+".join(f"between(t,{a},{c})" for a, c in [(0.55, 0.62), (0.70, 0.74), (0.86, 1.30), (1.36, 1.40), (1.48, 99)])
    graph = (vf + ";"
             f"[1:v]format=rgba,fade=t=out:st={dur - 0.7:.2f}:d=0.6:alpha=1[logo];"
             f"[2:v]format=rgba,fade=t=in:st=0.35:d=0.2:alpha=1,fade=t=out:st={dur - 0.7:.2f}:d=0.6:alpha=1[dim];"
             f"[3:v]format=rgba,fade=t=in:st=1.9:d=0.8:alpha=1,fade=t=out:st={dur - 0.7:.2f}:d=0.6:alpha=1[tag];"
             f"[bg][dim]overlay=enable='gte(t,0.35)*not({on})'[a];"
             f"[a][logo]overlay=enable='{on}'[b];"
             f"[b][tag]overlay=y='14*(1-clip((t-1.9)/0.9,0,1))'[o]")
    run(nice(["ffmpeg", "-y", "-v", "error"] + src + ins + ["-filter_complex", graph, "-map", "[o]", "-frames:v", str(int(round(dur * FPS))),
                                                           "-c:v", "libx264", "-preset", "veryfast", "-crf", "12", "-pix_fmt", "yuv420p", "-r", str(FPS), seg]))


def render_end(clips, b, seg, capdir):
    logo, info = os.path.join(capdir, "end_logo.png"), os.path.join(capdir, "end_info.png")
    cards.end_card(logo, info)
    src, vf = backdrop(clips, b, 0.08)
    dur = b["dur"]
    ins = []
    for p in (logo, info):
        ins += ["-loop", "1", "-framerate", str(FPS), "-t", f"{dur:.3f}", "-i", p]
    graph = (vf + ";"
             f"[1:v]format=rgba,fade=t=in:st=0.3:d=0.9:alpha=1[logo];"
             f"[2:v]format=rgba,fade=t=in:st=1.1:d=0.9:alpha=1[info];"
             f"[bg][logo]overlay=y='-18*(1-clip((t-0.3)/1.2,0,1))'[a];"
             f"[a][info]overlay[o0];[o0]fade=t=out:st={dur - 1.0:.2f}:d=1.0[o]")
    run(nice(["ffmpeg", "-y", "-v", "error"] + src + ins + ["-filter_complex", graph, "-map", "[o]", "-frames:v", str(int(round(dur * FPS))),
                                                           "-c:v", "libx264", "-preset", "veryfast", "-crf", "12", "-pix_fmt", "yuv420p", "-r", str(FPS), seg]))


SECTIONS = {}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--clips", default=os.path.join(ROOT, "Recordings", "trailer"))
    ap.add_argument("--out", default=os.path.join(ROOT, "docs", "media", "AfterHours-trailer.mp4"))
    ap.add_argument("--work", default=os.path.join(ROOT, "Tools", "trailer", "work"))
    ap.add_argument("--keep", action="store_true", help="keep the per-beat segments")
    args = ap.parse_args()
    if not shutil.which("ffmpeg"):
        raise SystemExit("ffmpeg not found")
    cut()
    build(os.path.abspath(args.clips), os.path.abspath(args.out), os.path.abspath(args.work), args.keep)


def cut():
    """The edit: cold open, title, one beat per feature, settings and access, the late nights, endings, end card."""
    LIFT = "eq=gamma=1.28:brightness=0.025:saturation=1.05"
    NEWS = [(300, 40, 1320, 340), (260, 495, 1400, 560)]           # ending headline and epilogue text

    # Cold open: the whiteboard (the trailer moment).
    beat("n3_whiteboard", 1.3, 3.4, speed=1.7, look=LIFT, section="open")
    beat("n3_whiteboard", 8.8, 4.6, hand="Some marks don't wipe off.", look=LIFT, tdur=0.3, cap_at=0.9, duck=(0.4, 3.2, 0.55))

    # Title.
    beat("@title", 2.0, 6.2, backdrop="title_drift", trans="fadeblack", tdur=0.9, section="title")

    # The job.
    beat("n1_card", 0.7, 3.6, cap=("", "THE NIGHT SHIFT", "You're the new night cleaner on the 14th floor."),
         trans="fadeblack", tdur=0.6, cap_at=0.5)
    beat("n1_clipboard", 0.7, 2.8, cap=("", "CLOCK IN", "The shift sheet lists tonight's jobs, room by room."), tdur=0.35)

    # Cleaning.
    beat("n1_wipe", 0.9, 4.0, speed=1.25, cap=("#", "WIPE IT DOWN", "Coffee rings lift under the cloth. A clean surface gleams and dings."), section="features")
    beat("n1_vacuum", 0.5, 3.4, speed=1.2, cap=("#", "VACUUM", "Stripe the carpet. Confetti rattles up the nozzle."))
    beat("n1_throw", 0.3, 4.3, cap=("#", "SORT AND THROW", "The label says which bin. Charge a throw and sink it."))
    beat("n2_foam", 0.4, 4.0, speed=1.15, cap=("#", "SPRAY AND SQUEEGEE", "Foam the glass first. Sometimes the foam shows a message."))
    beat("n2_mop", 0.6, 3.0, speed=1.4, cap=("#", "MOP", "Wet floors shine, then dry."))
    beat("n1_putback", 0.4, 3.6, cap=("#", "PUT IT BACK", "A ghost shows where things belong. Tuck chairs, switch off screens."))
    beat("n2_glint", 0.4, 3.6, cap=("#", "NEVER STUCK", "Stuck for a minute? What's left glints, and a caption says where."))

    # Secrets.
    beat("n1_key", 2.2, 3.6, cap=("#", "LOOK UNDER THINGS", "Crouch under the desks. The vacuum finds what someone dropped."))
    beat("n1_note", 1.4, 3.8, cap=("#", "READ WHAT THEY LEFT", "Keep it, put it back, or throw it away. The office remembers."))
    beat("n1_tray", 0.6, 3.6, cap=("#", "DECIDE WHERE IT GOES", "Deliver it to someone's tray, shred it, or leave an anonymous note."))
    beat("n2_uv", 0.9, 4.2, look=LIFT, cap=("#", "UV TORCH", "Invisible ink from the last cleaner, and every speck you missed."))
    beat("n4_rubbing", 0.6, 2.8, speed=1.3, cap=("#", "PENCIL RUBBING", "The last page of a notepad remembers what was written on it."))
    beat("n4_office", 0.2, 2.6, cap=("#", "THE CORNER OFFICE", "She notices anything you move. Some offers are hard to refuse."), cap_at=0.25)
    beat("n5_puzzle", 1.6, 4.2, speed=1.35, cap=("#", "TAPE IT BACK TOGETHER", "Rebuild a shredded page, strip by strip."))
    beat("n2_casefile", 1.2, 4.6, cap=("#", "THE CASE FILE", "Everything you've read, night by night, to read again before you decide."))
    beat("n1_remote", 0.8, 4.2, speed=1.25, cap=("#", "LIGHTS OUT", "Switch off the last light. Something else switches on."), duck=(1.4, 3.0, 0.6))

    # End of shift.
    beat("n1_report", 1.6, 3.8, cap=("#", "CLOCK OUT", "A grade from S to C, and before-and-after polaroids of every room."))
    beat("n1_report", 12.8, 3.2, cap=("#", "THE MORNING AFTER", "The office chat reacts to what you left, and what went missing."), tdur=0.35, cap_at=0.2)

    # Seven nights.
    for i, n in enumerate(range(2, 8)):
        beat(f"n{n}_card", 2.05, 0.95, trans="fade" if i else "fadeblack", tdur=0.25 if i else 0.5,
             cap=("", "SEVEN NIGHTS", "Every night the office has changed, and so has the mess.") if i == 0 else None, cap_at=0.15,
             section="late" if i == 0 else None)

    # Your way.
    beat("n2_fid_low", 1.0, 4.4, split=("n2_fid_ultra", "LOW", "ULTRA"), trans="fadeblack", tdur=0.4,
         cap=("", "GRAPHICS FIDELITY", "Low to Ultra. Ultra adds room reflections, lamp shadows and finer shadows."), cap_at=0.5)
    beat("title_settings", 1.0, 4.4, tdur=0.35, gain=0.0)  # its game audio has the title music in it
    beat("n1_pause", 0.6, 2.9, cap=("", "YOUR CONTROLS", "Keyboard and mouse or gamepad. Rebind any key or button."), tdur=0.35, cap_at=0.3)
    beat("n2_largest", 1.0, 3.0, cap=("", "READ IT YOUR WAY", "Larger text, plain lettering, captions, gentler flashes, mono audio."), tdur=0.35, cap_at=0.3)
    beat("title_nightselect", 0.5, 2.4, cap=("", "REPLAY ANY NIGHT", "Night Select starts any night you've reached, as you left it."), tdur=0.35)

    # Escalation.
    beat("n5_party", 0.5, 1.8, trans="fadeblack", tdur=0.4)
    beat("n6_archive", 0.4, 1.4, trans="fade", tdur=0.2)
    beat("n6_voicemail", 1.2, 1.9, tdur=0.2)
    beat("n7_storm", 1.0, 3.0, tdur=0.2, look="eq=gamma=1.1")
    beat("n7_jam", 0.2, 2.4, tdur=0.2, look=LIFT)
    beat("n7_folder_tray", 0.7, 2.7, tdur=0.25, hand="Decide what survives.", cap_at=0.4)

    # Endings: names only, headlines blurred; the secret one stays secret.
    beat("ending_audit", 1.2, 1.7, cap=("", "FOUR ENDINGS", "Which evidence survives is up to you."), trans="fadeblack", tdur=0.45, blur=NEWS, cap_at=0.2, section="end")
    beat("ending_cleanbooks", 1.2, 1.35, blur=NEWS, tdur=0.2)
    beat("ending_loose", 1.2, 1.35, blur=NEWS, tdur=0.2)
    beat("ending_spotless", 1.2, 1.6, blur=NEWS + [(300, 380, 1320, 120)], tdur=0.2)

    # End card.
    beat("@end", 6.0, 6.8, backdrop="title_drift", trans="fadeblack", tdur=0.7)


if __name__ == "__main__":
    main()
