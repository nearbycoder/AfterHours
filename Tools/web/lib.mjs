// Shared by Tools/check-pages.mjs and Tools/web/play-test.mjs: find and launch headless Chromium
// (Playwright's cached build) or Firefox (the system one, over WebDriver BiDi), open the game,
// collect console errors, and wait on window.afterHours (what the game reports it's showing).
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execFileSync } from "node:child_process";
import puppeteer from "puppeteer-core";

export function findChromium() {
  if (process.env.CHROME_PATH) return process.env.CHROME_PATH;
  const cache = path.join(os.homedir(), ".cache", "ms-playwright");
  const dirs = fs.existsSync(cache) ? fs.readdirSync(cache) : [];
  const pick = (prefix, sub) => dirs.filter((d) => d.startsWith(prefix)).sort().reverse()
    .map((d) => path.join(cache, d, sub)).find((p) => fs.existsSync(p));
  return pick("chromium_headless_shell-", "chrome-headless-shell-linux64/chrome-headless-shell")
    || pick("chromium-", "chrome-linux64/chrome");
}

export function findFirefox() {
  if (process.env.FIREFOX_PATH) return process.env.FIREFOX_PATH;
  try { return execFileSync("sh", ["-c", "command -v firefox"], { encoding: "utf8" }).trim() || null; } catch { return null; }
}

/**
 * A headless browser with a throwaway profile in profileDir (deleted by close()). Autoplay needs a
 * click or key in both, as for a first visit, so the audio check means something. Chromium renders
 * WebGL in software (SwiftShader) unless gpu is set (ANGLE on Vulkan: the machine's GPU).
 */
export async function launch(name, profileDir, { gpu = false } = {}) {
  fs.rmSync(profileDir, { recursive: true, force: true });
  fs.mkdirSync(profileDir, { recursive: true });
  let browser;
  if (name === "chromium") {
    const executablePath = findChromium();
    if (!executablePath) throw new Error("no Chromium found (set CHROME_PATH or install Playwright's chromium)");
    browser = await puppeteer.launch({
      browser: "chrome", executablePath, headless: "shell", userDataDir: profileDir,
      args: ["--autoplay-policy=user-gesture-required", "--ignore-gpu-blocklist",
             ...(gpu ? ["--use-angle=vulkan", "--enable-features=Vulkan"] : ["--enable-unsafe-swiftshader"]),
             "--no-first-run", "--no-default-browser-check", "--window-size=1280,720"],
      defaultViewport: { width: 1280, height: 720 },
    });
  } else if (name === "firefox") {
    const executablePath = findFirefox();
    if (!executablePath) throw new Error("no Firefox found (set FIREFOX_PATH)");
    browser = await puppeteer.launch({
      browser: "firefox", executablePath, headless: true, userDataDir: profileDir,
      extraPrefsFirefox: { "media.autoplay.default": 1, "media.autoplay.blocking_policy": 0, "media.autoplay.block-webaudio": true, "webgl.force-enabled": true,
                           "browser.shell.checkDefaultBrowser": false, "datareporting.policy.dataSubmissionEnabled": false },
      defaultViewport: { width: 1280, height: 720 },
    });
  } else throw new Error("unknown browser " + name);
  const close = async () => {
    try { await browser.close(); } catch { }
    fs.rmSync(profileDir, { recursive: true, force: true });
  };
  const version = await browser.version();
  const page = await browser.newPage();
  const renderer = await page.evaluate(() => {
    const g = document.createElement("canvas").getContext("webgl2");
    const d = g && g.getExtension("WEBGL_debug_renderer_info");
    return g ? (d ? g.getParameter(d.UNMASKED_RENDERER_WEBGL) : g.getParameter(g.RENDERER)) : "no WebGL 2";
  }).catch(() => "?");
  await page.close();
  return { browser, close, version: `${version}, WebGL: ${renderer}` };
}

// Before the page's scripts: note every AudioContext the game makes, so tests can read its state.
// Headless browsers under automation count the page as already clicked, so they never hold audio
// back. With holdAudio each context behaves as on a first visit under the autoplay policy: it
// starts suspended, and resume() does nothing until a real click, key or touch has happened (the
// game must call it again from an input, as Unity does).
const AUDIO_PROBE = (holdAudio) => {
  window.__ahAudio = [];
  let activated = false;
  if (holdAudio) for (const t of ["keydown", "mousedown", "pointerdown", "touchend"])
    window.addEventListener(t, (e) => { if (e.isTrusted) activated = true; }, true);
  for (const n of ["AudioContext", "webkitAudioContext"]) {
    const C = window[n];
    if (!C) continue;
    window[n] = class extends C {
      constructor(...a) {
        super(...a);
        window.__ahAudio.push(this);
        if (holdAudio) super.suspend();
      }
      resume() {
        if (holdAudio && !activated) return new Promise(() => { }); // stays pending, as in a browser
        return super.resume();
      }
    };
  }
};

/** Open url in a new page; console errors, page errors and failed requests go to errors[]. */
export async function openGame(browser, url, log, { holdAudio = false } = {}) {
  const page = await browser.newPage();
  const errors = [];
  await page.evaluateOnNewDocument(AUDIO_PROBE, holdAudio);
  page.on("console", (m) => {
    const t = m.type(), text = m.text();
    log(`[console.${t}] ${text}`);
    if (t === "error") errors.push(text);
  });
  page.on("pageerror", (e) => { log(`[pageerror] ${e.message || e}`); errors.push(String(e.message || e)); });
  page.on("requestfailed", (r) => {
    log(`[requestfailed] ${r.url()} ${r.failure()?.errorText}`);
    // The loader hands the unpacked files to the engine as blob: URLs and revokes them once read;
    // Chromium reports those as aborted requests. Only the site's own files count.
    if (!r.url().startsWith("blob:")) errors.push("request failed: " + r.url());
  });
  page.on("response", (r) => { if (r.status() >= 400) { log(`[http ${r.status()}] ${r.url()}`); errors.push(`HTTP ${r.status()} ${r.url()}`); } });
  page.on("dialog", (d) => d.accept().catch(() => { }));
  const t0 = Date.now();
  await page.goto(url, { waitUntil: "load", timeout: 120000 });
  return { page, errors, t0 };
}

/** Wait until fn(window.afterHours) is true (polled in the page); returns the state, or null on timeout. */
export async function waitState(page, predicateSrc, timeoutMs, errors) {
  const end = Date.now() + timeoutMs;
  while (Date.now() < end) {
    const st = await page.evaluate(() => window.afterHours || null).catch(() => null);
    if (st && new Function("s", `return (${predicateSrc});`)(st)) return st;
    const failed = await page.evaluate(() => document.querySelector("#status.error")?.textContent || null).catch(() => null);
    if (failed) { errors.push("page says: " + failed); return null; }
    await new Promise((r) => setTimeout(r, 250));
  }
  return null;
}

/** Bytes the page downloaded (same-origin resources, as sent over the wire). */
export async function downloaded(page) {
  return page.evaluate(() => performance.getEntriesByType("navigation").concat(performance.getEntriesByType("resource"))
    .reduce((n, e) => n + (e.transferSize || e.encodedBodySize || 0), 0)).catch(() => 0);
}

export async function audioState(page) {
  return page.evaluate(() => (window.__ahAudio || []).map((c) => c.state)).catch(() => []);
}

export const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

export function logger(file) {
  if (file) fs.mkdirSync(path.dirname(file), { recursive: true });
  const lines = [];
  const fn = (s) => {
    const line = `${new Date().toISOString().slice(11, 23)} ${s}`;
    lines.push(line);
    if (file) fs.appendFileSync(file, line.slice(0, 4000) + "\n");
  };
  fn.lines = lines;
  return fn;
}

/**
 * How different two screenshots (PNG buffers) are in the middle of the screen, away from the HUD's
 * clock and prompts: the mean absolute difference per channel, 0 to 255. Decoded in the page.
 */
export async function centreDiff(page, a, b) {
  return page.evaluate(async (a, b) => {
    const load = (src) => new Promise((r, j) => { const i = new Image(); i.onload = () => r(i); i.onerror = j; i.src = src; });
    const [ia, ib] = await Promise.all([load(a), load(b)]);
    const w = ia.width, h = ia.height, x = w * 0.25, y = h * 0.15, cw = w * 0.5, ch = h * 0.55;
    const px = (img) => { const c = document.createElement("canvas"); c.width = cw; c.height = ch;
      const g = c.getContext("2d"); g.drawImage(img, x, y, cw, ch, 0, 0, cw, ch); return g.getImageData(0, 0, cw, ch).data; };
    const da = px(ia), db = px(ib);
    let sum = 0;
    for (let i = 0; i < da.length; i += 4) sum += Math.abs(da[i] - db[i]) + Math.abs(da[i + 1] - db[i + 1]) + Math.abs(da[i + 2] - db[i + 2]);
    return sum / (da.length / 4 * 3);
  }, "data:image/png;base64," + Buffer.from(a).toString("base64"), "data:image/png;base64," + Buffer.from(b).toString("base64"));
}
