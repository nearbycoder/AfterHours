// Phones and tablets for Tools/web/mobile-test.mjs: Playwright's WebKit (an iPhone or iPad profile:
// touch, a coarse pointer, WebGL 2) and Chromium (an Android profile), plus what the page costs in
// memory. Neither browser enforces iOS's per-tab limit, so the page measures itself: the wasm heap,
// an estimate of every texture, buffer and renderbuffer it gives WebGL (counted as uploaded, freed
// when deleted), and the browser processes' peak resident memory (VmHWM) from /proc.
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { chromium, webkit, devices } from "playwright-core";
import { findChromium } from "./lib.mjs";

export const WEBKIT = process.env.WEBKIT_PATH || path.join(os.homedir(), ".cache", "webkit-libs", "webkit-2359", "pw_run.sh");

/** Profiles by short name: the Playwright device, and the engine it runs in. */
export const PROFILES = {
  iphone: { engine: "webkit", device: "iPhone 15 landscape" },
  "iphone-portrait": { engine: "webkit", device: "iPhone 15" },
  ipad: { engine: "webkit", device: "iPad Pro 11 landscape" },
  "ipad-portrait": { engine: "webkit", device: "iPad Pro 11" },
  pixel: { engine: "chromium", device: "Pixel 7 landscape" },
  "pixel-portrait": { engine: "chromium", device: "Pixel 7" },
  // A desktop with a mouse (no touch): the controls must stay hidden.
  desktop: { engine: "chromium", device: null },
};

// Before the page's scripts: count what WebGL is given and how often frames are drawn.
const PROBE = () => {
  const gpu = { bytes: 0, peak: 0, textures: 0, buffers: 0, renderbuffers: 0, texBytes: 0, bufBytes: 0, rbBytes: 0, byFormat: {} };
  window.__ahMem = gpu;
  const sizes = new WeakMap(); // object -> Map(key -> bytes)
  const kind = new WeakMap();
  const fmtOf = new WeakMap();
  const add = (obj, key, bytes, k, fmt) => {
    if (!obj) return;
    if (fmt !== undefined) fmtOf.set(obj, "0x" + fmt.toString(16));
    const f = fmtOf.get(obj) || k;
    let m = sizes.get(obj);
    if (!m) { m = new Map(); sizes.set(obj, m); kind.set(obj, k); gpu[k === "tex" ? "textures" : k === "buf" ? "buffers" : "renderbuffers"]++; }
    const old = m.get(key) || 0;
    m.set(key, bytes);
    gpu.bytes += bytes - old; gpu[k + "Bytes"] += bytes - old;
    gpu.byFormat[f] = (gpu.byFormat[f] || 0) + bytes - old;
    if (gpu.bytes > gpu.peak) gpu.peak = gpu.bytes;
  };
  const drop = (obj) => {
    const m = obj && sizes.get(obj);
    if (!m) return;
    const k = kind.get(obj);
    let n = 0; for (const v of m.values()) n += v;
    gpu.bytes -= n; gpu[k + "Bytes"] -= n;
    const f = fmtOf.get(obj) || k; gpu.byFormat[f] = (gpu.byFormat[f] || 0) - n;
    gpu[k === "tex" ? "textures" : k === "buf" ? "buffers" : "renderbuffers"]--;
    sizes.delete(obj);
  };
  // Bytes per pixel by internal format (GL enums); compressed formats per pixel too.
  const BPP = {
    0x8058: 4, 0x8C43: 4, 0x8051: 3, 0x8C41: 3, 0x881A: 8, 0x8814: 16, 0x881B: 6, 0x8815: 12, 0x8229: 1, 0x822B: 2,
    0x822D: 2, 0x822E: 4, 0x822F: 4, 0x8230: 8, 0x8C3A: 4, 0x8059: 4, 0x8D62: 2, 0x8056: 2, 0x8057: 2, 0x1908: 4, 0x1907: 3,
    0x1909: 1, 0x190A: 2, 0x1906: 1, 0x81A5: 2, 0x81A6: 4, 0x8CAC: 4, 0x88F0: 4, 0x8CAD: 8, 0x1902: 4, 0x84F9: 4, 0x8D48: 1,
    0x8D9F: 4, 0x8C3D: 4, 0x8235: 4, 0x8236: 4, 0x8231: 1, 0x8232: 1, 0x8233: 2, 0x8234: 2, 0x823B: 2, 0x823C: 2,
    // S3TC/DXT, ETC2/EAC, ASTC 4x4, BC7 (bytes per pixel)
    0x83F0: 0.5, 0x83F1: 0.5, 0x83F2: 1, 0x83F3: 1, 0x8C4C: 0.5, 0x8C4D: 0.5, 0x8C4E: 1, 0x8C4F: 1,
    0x9274: 0.5, 0x9275: 0.5, 0x9278: 1, 0x9279: 1, 0x9270: 0.5, 0x9272: 1, 0x9276: 0.5, 0x9277: 0.5, 0x93B0: 1, 0x93D0: 1,
    0x8E8C: 1, 0x8E8D: 1, 0x8E8E: 1, 0x8E8F: 1,
  };
  const bpp = (f) => BPP[f] || 4;
  for (const C of [window.WebGL2RenderingContext, window.WebGLRenderingContext]) {
    if (!C) continue;
    const P = C.prototype;
    const units = new WeakMap(); // ctx -> {unit, bound: {unit:target -> tex}}
    const st = (gl) => { let s = units.get(gl); if (!s) { s = { unit: 0, tex: {}, buf: {}, rb: null }; units.set(gl, s); } return s; };
    const faceTarget = (t) => (t >= 0x8515 && t <= 0x851A ? 0x8513 : t);
    const boundTex = (gl, t) => st(gl).tex[st(gl).unit + ":" + faceTarget(t)];
    const wrap = (name, fn) => { const orig = P[name]; if (!orig) return; P[name] = function (...a) { const r = orig.apply(this, a); try { fn(this, a, r); } catch (e) { } return r; }; };
    wrap("activeTexture", (gl, [u]) => { st(gl).unit = u; });
    wrap("bindTexture", (gl, [t, tex]) => { st(gl).tex[st(gl).unit + ":" + t] = tex; });
    wrap("bindBuffer", (gl, [t, b]) => { st(gl).buf[t] = b; });
    wrap("bindRenderbuffer", (gl, [t, rb]) => { st(gl).rb = rb; });
    wrap("texImage2D", (gl, a) => {
      const [t, level, ifmt] = a;
      let w, h;
      if (a.length >= 8) { w = a[3]; h = a[4]; } else { const src = a[5]; w = src?.width || src?.videoWidth || 0; h = src?.height || src?.videoHeight || 0; }
      add(boundTex(gl, t), `${t}:${level}`, w * h * bpp(ifmt), "tex", ifmt);
    });
    wrap("texImage3D", (gl, [t, level, ifmt, w, h, d]) => add(boundTex(gl, t), `${t}:${level}`, w * h * d * bpp(ifmt), "tex", ifmt));
    // Unity passes views of the whole heap with an offset and length: size from the dimensions.
    wrap("compressedTexImage2D", (gl, [t, level, ifmt, w, h]) => add(boundTex(gl, t), `${t}:${level}`, w * h * bpp(ifmt), "tex", ifmt));
    wrap("texStorage2D", (gl, [t, levels, ifmt, w, h]) => {
      let n = 0; for (let l = 0; l < levels; l++) n += Math.max(1, w >> l) * Math.max(1, h >> l) * bpp(ifmt);
      add(boundTex(gl, t), "storage", n * (t === 0x8513 ? 6 : 1), "tex", ifmt);
    });
    wrap("texStorage3D", (gl, [t, levels, ifmt, w, h, d]) => {
      let n = 0; for (let l = 0; l < levels; l++) n += Math.max(1, w >> l) * Math.max(1, h >> l) * (t === 0x806F ? Math.max(1, d >> l) : d) * bpp(ifmt);
      add(boundTex(gl, t), "storage", n, "tex", ifmt);
    });
    wrap("bufferData", (gl, a) => {
      const [t, data] = a;
      const n = typeof data === "number" ? data : a.length >= 5 ? a[4] * (data.BYTES_PER_ELEMENT || 1)
        : a.length === 4 ? data.byteLength - a[3] * (data.BYTES_PER_ELEMENT || 1) : (data?.byteLength || 0);
      add(st(gl).buf[t], "data", n, "buf");
    });
    wrap("renderbufferStorage", (gl, [, ifmt, w, h]) => add(st(gl).rb, "rb", w * h * bpp(ifmt), "rb"));
    wrap("renderbufferStorageMultisample", (gl, [, samples, ifmt, w, h]) => add(st(gl).rb, "rb", w * h * bpp(ifmt) * Math.max(1, samples), "rb"));
    wrap("deleteTexture", (gl, [o]) => drop(o));
    wrap("deleteBuffer", (gl, [o]) => drop(o));
    wrap("deleteRenderbuffer", (gl, [o]) => drop(o));
  }
  // Frames: count animation frames (Unity draws in requestAnimationFrame).
  const fr = { n: 0 };
  window.__ahFrames = fr;
  const tick = () => { fr.n++; requestAnimationFrame(tick); };
  requestAnimationFrame(tick);
  // The heap: Emscripten's memory, polled (it only ever grows).
  window.__ahHeap = () => {
    const m = window.unityInstance?.Module;
    const b = (m?.HEAPU8 || m?.HEAPU32 || m?.HEAPF32)?.buffer;
    return b ? b.byteLength : 0;
  };
};

// Audio as on a first visit: each AudioContext starts suspended and resume() only works after a
// trusted tap or key (window.__ahAudio lists them; __ahAudioAsks notes each resume() and whether
// it came after such an input). This machine's WPE WebKit has no GStreamer audio sink and aborts
// the page when output starts, so there (silent) resume() is only noted, never passed on.
const AUDIO = (silent) => {
  window.__ahAudio = [];
  window.__ahAudioAsks = [];
  let activated = false;
  for (const t of ["keydown", "mousedown", "pointerdown", "touchend", "pointerup"])
    window.addEventListener(t, (e) => { if (e.isTrusted) activated = true; }, true);
  // Silent: an offline context (same nodes and decoding, no output device) that only says it runs.
  class Silent extends OfflineAudioContext {
    constructor(o) { super(2, 44100, (o && o.sampleRate) || 44100); this._st = "suspended"; }
    get state() { return this._st; }
    get baseLatency() { return 0; }
    get outputLatency() { return 0; }
    _set(s) { if (this._st !== s) { this._st = s; this.dispatchEvent(new Event("statechange")); } return Promise.resolve(); }
    suspend() { return this._set("suspended"); }
    close() { return this._set("closed"); }
    resume() { return this._set("running"); }
    createMediaElementSource(el) { const g = this.createGain(); g.mediaElement = el; return g; }
  }
  // Unity streams compressed clips through <audio> elements, which need the sink too: silent ones.
  class SilentMedia extends EventTarget {
    constructor() { super(); Object.assign(this, { src: "", preload: "", autoplay: false, currentTime: 0, duration: NaN, paused: true, loop: false, volume: 1, muted: false, playbackRate: 1, readyState: 0 }); }
    play() { this.paused = false; return Promise.resolve(); }
    pause() { this.paused = true; }
    load() { }
    canPlayType() { return ""; }
    setAttribute(k, v) { this[k] = v; }
    removeAttribute(k) { this[k] = ""; }
  }
  if (silent) window.Audio = SilentMedia;
  for (const n of ["AudioContext", "webkitAudioContext"]) {
    const C = silent ? Silent : window[n];
    if (!C) continue;
    window[n] = class extends C {
      constructor(...a) { super(...a); window.__ahAudio.push(this); if (!silent) super.suspend(); }
      resume() {
        window.__ahAudioAsks.push({ afterInput: activated, t: performance.now() });
        if (!activated) return new Promise(() => { });
        return super.resume();
      }
    };
  }
  window.__ahAudioSilent = silent;
};

/**
 * This machine's WPE WebKit can't decode the game's AAC clips (no GStreamer AAC decoder), so Unity
 * logs each clip as failed there; iOS Safari decodes them. Those lines aren't counted as errors.
 */
export const silentAudioNoise = (engine, text) => engine === "webkit" && /^(Loading FSB failed for audio clip|EncodingError: Decoding failed)/.test(text);

/** Every process started under this one (the browser and its children), with peak and current RSS in bytes. */
export function descendants(root = process.pid) {
  const kids = new Map();
  for (const d of fs.readdirSync("/proc")) {
    if (!/^\d+$/.test(d)) continue;
    try {
      const stat = fs.readFileSync(`/proc/${d}/stat`, "utf8");
      const ppid = Number(stat.slice(stat.lastIndexOf(")") + 2).split(" ")[1]);
      if (!kids.has(ppid)) kids.set(ppid, []);
      kids.get(ppid).push(Number(d));
    } catch { }
  }
  const out = [];
  const walk = (p) => {
    for (const c of kids.get(p) || []) {
      try {
        const s = fs.readFileSync(`/proc/${c}/status`, "utf8");
        const kb = (k) => Number((s.match(new RegExp(`^${k}:\\s+(\\d+)`, "m")) || [0, 0])[1]) * 1024;
        // Chromium rewrites its children's command lines into one string: search the whole thing.
        const cmd = fs.readFileSync(`/proc/${c}/cmdline`, "utf8");
        const name = (s.match(/^Name:\s+(.*)$/m) || [0, "?"])[1];
        const type = (cmd.match(/--type=([a-z-]+)/) || [0, ""])[1];
        out.push({ pid: c, name, type, hwm: kb("VmHWM"), rss: kb("VmRSS") });
      } catch { }
      walk(c);
    }
  };
  walk(root);
  return out;
}

/** The content process (where the page and its wasm heap live): WebKitWebProcess, or Chromium's renderer. */
export function procSummary(list) {
  const pick = (f) => list.filter(f).reduce((a, p) => (p.hwm > (a?.hwm || 0) ? p : a), null);
  const web = pick((p) => /^(WebKit|WPE)WebProces/.test(p.name) || p.type === "renderer");
  const gpu = pick((p) => /^(WebKit|WPE)GPUProces/.test(p.name) || p.type === "gpu-process");
  const total = list.reduce((n, p) => n + p.rss, 0);
  return { webHwm: web?.hwm || 0, webRss: web?.rss || 0, gpuHwm: gpu?.hwm || 0, totalRss: total, procs: list.length };
}

// A GPU without desktop (BC/S3TC) texture formats, as most phones are: the page can't see them.
const NO_S3TC = () => {
  const hide = /s3tc|bptc|rgtc/i;
  for (const C of [window.WebGL2RenderingContext, window.WebGLRenderingContext]) {
    if (!C) continue;
    const ge = C.prototype.getExtension, gs = C.prototype.getSupportedExtensions;
    C.prototype.getExtension = function (n) { return hide.test(n) ? null : ge.call(this, n); };
    C.prototype.getSupportedExtensions = function () { return (gs.call(this) || []).filter((n) => !hide.test(n)); };
  }
};

export async function launchProfile(name, { gpu = true, noS3tc = false } = {}) {
  const prof = PROFILES[name];
  if (!prof) throw new Error(`unknown profile ${name} (${Object.keys(PROFILES).join(", ")})`);
  let browser;
  if (prof.engine === "webkit") {
    browser = await webkit.launch({ headless: true, executablePath: WEBKIT });
  } else {
    browser = await chromium.launch({
      headless: true, executablePath: findChromium(),
      args: ["--autoplay-policy=user-gesture-required", "--ignore-gpu-blocklist",
             ...(gpu ? ["--use-angle=vulkan", "--enable-features=Vulkan"] : ["--enable-unsafe-swiftshader"])],
    });
  }
  const opts = prof.device ? { ...devices[prof.device] } : { viewport: { width: 1280, height: 720 }, hasTouch: false, isMobile: false };
  const context = await browser.newContext(opts);
  await context.addInitScript(PROBE);
  await context.addInitScript(AUDIO, prof.engine === "webkit");
  if (noS3tc) await context.addInitScript(NO_S3TC);
  const page = await context.newPage();
  return { browser, context, page, prof, version: browser.version(), close: async () => { try { await browser.close(); } catch { } } };
}

/** What the page costs now: heap, WebGL estimate, frames counted so far. */
export async function pageMem(page) {
  return page.evaluate(() => ({ heap: window.__ahHeap ? window.__ahHeap() : 0, gpu: { ...(window.__ahMem || {}) }, frames: window.__ahFrames?.n || 0 })).catch(() => null);
}

export const MB = (b) => (b / 1048576).toFixed(0);

/** Frames per second over ms (animation frames). */
export async function fps(page, ms = 4000) {
  const a = await pageMem(page);
  await new Promise((r) => setTimeout(r, ms));
  const b = await pageMem(page);
  return a && b ? ((b.frames - a.frames) * 1000) / ms : 0;
}

/**
 * Real touches, several fingers at once: Chromium over CDP (Input.dispatchTouchEvent), WebKit over
 * its own protocol (the same command, reached through Playwright's in-process server; Playwright
 * itself only offers taps there). Both arrive as trusted touch and pointer events.
 *   const t = await toucher(page, engine); await t.down(1, x, y); await t.move(1, x2, y2); await t.up(1);
 */
export async function toucher(page, engine) {
  const pts = new Map();
  const list = () => [...pts.entries()].map(([id, p]) => ({ x: Math.round(p.x), y: Math.round(p.y), id }));
  let send;
  if (engine === "chromium") {
    const cdp = await page.context().newCDPSession(page);
    send = async (type, changed) => cdp.send("Input.dispatchTouchEvent", {
      type, touchPoints: type === "touchEnd" ? changed : list(),
    });
  } else {
    const pw = (await import("playwright-core")).default;
    const impl = pw._connection.toImpl(page);
    const session = (impl.delegate || impl._delegate)._pageProxySession;
    send = async (type, changed) => session.send("Input.dispatchTouchEvent", {
      type, modifiers: 0, touchPoints: type === "touchMove" ? list() : changed,
    });
  }
  const one = (id) => { const p = pts.get(id); return [{ x: Math.round(p.x), y: Math.round(p.y), id }]; };
  return {
    async down(id, x, y) { pts.set(id, { x, y }); await send("touchStart", one(id)); },
    async move(id, x, y) { pts.set(id, { x, y }); await send("touchMove", one(id)); },
    async up(id) { const c = one(id); pts.delete(id); await send("touchEnd", c); },
    /** A finger from a to b over ms, in steps. */
    async drag(id, x0, y0, x1, y1, ms = 600, steps = 12) {
      await this.down(id, x0, y0);
      for (let i = 1; i <= steps; i++) { await this.move(id, x0 + ((x1 - x0) * i) / steps, y0 + ((y1 - y0) * i) / steps); await new Promise((r) => setTimeout(r, ms / steps)); }
      await this.up(id);
    },
    async tap(x, y, hold = 120) { await this.down(99, x, y); await new Promise((r) => setTimeout(r, hold)); await this.up(99); },
  };
}
