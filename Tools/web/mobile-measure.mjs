#!/usr/bin/env node
// What the browser build costs on a phone or tablet profile: load to the title, start Night 1 and
// stand in it, noting the download, the wasm heap, the WebGL estimate (textures, buffers,
// renderbuffers; see mobile-lib.mjs), the content process's peak resident memory, and frames per
// second. Starts the night with keys, so it measures builds without touch controls too.
//   node Tools/web/mobile-measure.mjs [--profile iphone|ipad|pixel|...] [--site Builds/Pages] [--port 8741]
//        [--out Logs/mobile] [--tag before] [--no-s3tc]
// One JSON line per profile goes to <out>/<tag>-<profile>.json, a log and screenshots beside it.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { serve, BASE } from "./serve.mjs";
import { logger, sleep } from "./lib.mjs";
import { launchProfile, descendants, procSummary, pageMem, fps, MB, silentAudioNoise } from "./mobile-lib.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const opt = (name, def) => { const i = args.indexOf(name); return i >= 0 && i + 1 < args.length ? args[i + 1] : def; };
const profiles = opt("--profile", "iphone,ipad,pixel").split(",");
const site = path.resolve(opt("--site", path.join(here, "..", "..", "Builds", "Pages")));
const port = Number(opt("--port", "8741"));
const out = path.resolve(opt("--out", path.join(here, "..", "..", "Logs", "mobile")));
const tag = opt("--tag", "run");
const noS3tc = args.includes("--no-s3tc"); // hide desktop texture formats, as most phones lack them
const url = `http://127.0.0.1:${port}${BASE}`;
fs.mkdirSync(out, { recursive: true });
const server = await serve(site, port);
console.log(`serving ${site} at ${url} (pid ${process.pid})`);

for (const name of profiles) {
  const logFile = path.join(out, `${tag}-${name}.log`);
  fs.rmSync(logFile, { force: true });
  const log = logger(logFile);
  const res = { profile: name, tag, site, noS3tc, load: fs.readFileSync("/proc/loadavg", "utf8").split(" ")[0] };
  let peak = { webHwm: 0, gpuHwm: 0, totalRss: 0 };
  const sample = () => {
    const s = procSummary(descendants());
    peak = { webHwm: Math.max(peak.webHwm, s.webHwm), gpuHwm: Math.max(peak.gpuHwm, s.gpuHwm), totalRss: Math.max(peak.totalRss, s.totalRss) };
    return s;
  };
  const timer = setInterval(sample, 500);
  const b = await launchProfile(name, { noS3tc });
  const { page } = b;
  log(`${name}: ${b.prof.engine} ${b.version}, ${b.prof.device || "desktop"}`);
  const errors = [];
  let audioNoise = 0;
  page.on("console", (m) => {
    log(`[console.${m.type()}] ${m.text()}`);
    if (m.type() !== "error") return;
    if (silentAudioNoise(b.prof.engine, m.text())) audioNoise++;
    else errors.push(m.text());
  });
  page.on("pageerror", (e) => { log(`[pageerror] ${e.message}`); if (silentAudioNoise(b.prof.engine, e.message)) audioNoise++; else errors.push(e.message); });
  page.on("crash", () => { log("[crash] the page crashed"); res.crashed = true; });
  try {
    res.env = await page.evaluate(() => ({
      coarse: matchMedia("(pointer: coarse)").matches, fine: matchMedia("(any-pointer: fine)").matches,
      touch: "ontouchstart" in window, webgl2: !!document.createElement("canvas").getContext("webgl2"), webgpu: !!navigator.gpu,
      dpr: devicePixelRatio, w: innerWidth, h: innerHeight, ua: navigator.userAgent,
    }));
    const t0 = Date.now();
    await page.goto(url, { waitUntil: "load", timeout: 120000 });
    log("env " + JSON.stringify(res.env));
    // Today's page asks first on touch-only devices ("Load it anyway").
    await sleep(800);
    if (await page.locator("#touch-continue").isVisible().catch(() => false)) {
      res.askedFirst = true;
      await page.screenshot({ path: path.join(out, `${tag}-${name}-0-asked.png`) });
      await page.locator("#touch-continue").tap();
    }
    const st = await page.waitForFunction(() => window.afterHours && window.afterHours.screen === "title", null, { timeout: 300000, polling: 250 })
      .then(() => page.evaluate(() => window.afterHours)).catch(() => null);
    res.title = !!st;
    res.titleSecs = +((Date.now() - t0) / 1000).toFixed(1);
    res.downloadMB = +((await page.evaluate(() => performance.getEntriesByType("resource").concat(performance.getEntriesByType("navigation"))
      .reduce((n, e) => n + (e.transferSize || e.encodedBodySize || 0), 0))) / 1048576).toFixed(1);
    res.quality = st?.qualityName;
    log(`title: ${res.title} in ${res.titleSecs} s, ${res.downloadMB} MB, quality ${res.quality}`);
    await sleep(3000);
    res.canvas = await page.evaluate(() => { const c = document.querySelector("#unity-canvas"); return `${c.width}x${c.height}`; });
    res.atTitle = { ...(await pageMem(page)), proc: sample() };
    res.titleFps = +(await fps(page)).toFixed(1);
    await page.screenshot({ path: path.join(out, `${tag}-${name}-1-title.png`) });
    // Into Night 1 with keys (Esc closes the first-visit Brightness page).
    await page.keyboard.press("Escape"); await sleep(1000);
    await page.keyboard.press("Enter");
    const night = await page.waitForFunction(() => window.afterHours && window.afterHours.screen === "night", null, { timeout: 120000, polling: 250 }).then(() => true).catch(() => false);
    res.night = night;
    await sleep(8000);
    res.inNight = { ...(await pageMem(page)), proc: sample() };
    res.nightFps = +(await fps(page, 5000)).toFixed(1);
    await page.screenshot({ path: path.join(out, `${tag}-${name}-2-night.png`) });
    // Walk a little (the office around the player streams nothing, but turns and steps cost frames).
    await page.keyboard.down("s"); await sleep(1500); await page.keyboard.up("s");
    await sleep(2000);
    res.end = { ...(await pageMem(page)), proc: sample() };
  } catch (e) {
    log("error: " + e.message);
    res.error = e.message;
  } finally {
    clearInterval(timer);
    sample();
    res.peak = peak;
    res.errors = errors.length;
    res.audioDecodeErrors = audioNoise;
    await b.close();
  }
  const g = res.end?.gpu || res.inNight?.gpu || {};
  const line = `${name}: title ${res.title ? `in ${res.titleSecs} s` : "NOT reached"}, ${res.downloadMB} MB, canvas ${res.canvas}, ${res.quality}; ` +
    `heap ${MB(res.end?.heap || res.atTitle?.heap || 0)} MB; WebGL est. peak ${MB(g.peak || 0)} MB (now ${MB(g.bytes || 0)}: tex ${MB(g.texBytes || 0)}, buf ${MB(g.bufBytes || 0)}, rb ${MB(g.rbBytes || 0)}); ` +
    `content process peak ${MB(peak.webHwm)} MB, GPU process peak ${MB(peak.gpuHwm)} MB, all ${MB(peak.totalRss)} MB; ` +
    `fps title ${res.titleFps}, night ${res.nightFps} (load ${res.load}); night ${res.night}; errors ${res.errors}${res.crashed ? "; CRASHED" : ""}`;
  log(line);
  console.log(line);
  fs.writeFileSync(path.join(out, `${tag}-${name}.json`), JSON.stringify(res, null, 1));
}
server.close();
