#!/usr/bin/env node
// The browser build on phones and tablets, played with real touches (several fingers at once):
// Builds/Pages under /AfterHours/ on a local server, in Playwright's WebKit with iPhone and iPad
// profiles and Chromium with an Android one (see mobile-lib.mjs). Each profile, from a fresh
// profile: the title loads with the on-screen controls ready but not drawn over the menus; the
// first tap unlocks audio; the Brightness page, Settings and New Game are used by tapping their
// buttons; in Night 1 the stick walks, a drag looks, both at once, and the buttons crouch, walk
// briskly, pin a tool, clean, spray, use and pause; the shift sheet opens and is paged and closed
// with its soft keys; turning the device upright asks to turn it back (and pauses); a key or the
// mouse hides the controls and a touch brings them back. Screenshots, a log and a JSON summary go
// to --out; memory is measured as in mobile-measure.mjs.
//   node Tools/web/mobile-test.mjs [--profile iphone,ipad,pixel] [--site Builds/Pages] [--port 8742] [--out Logs/mobile-test]
// The game is opened with ?arg=-ahWebTest, which makes it report its buttons' places and the
// player's position (WebPlatform.ReportTest) so taps land on real buttons and moves can be checked.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { serve, BASE } from "./serve.mjs";
import { logger, sleep } from "./lib.mjs";
import { launchProfile, descendants, procSummary, pageMem, fps, MB, silentAudioNoise, toucher } from "./mobile-lib.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const opt = (name, def) => { const i = args.indexOf(name); return i >= 0 && i + 1 < args.length ? args[i + 1] : def; };
const profiles = opt("--profile", "iphone,ipad,pixel").split(",");
const site = path.resolve(opt("--site", path.join(here, "..", "..", "Builds", "Pages")));
const port = Number(opt("--port", "8742"));
const out = path.resolve(opt("--out", path.join(here, "..", "..", "Logs", "mobile-test")));
const url = `http://127.0.0.1:${port}${BASE}?arg=-ahWebTest`;
if (!fs.existsSync(path.join(site, "index.html"))) { console.error(`no build in ${site}`); process.exit(2); }
fs.mkdirSync(out, { recursive: true });
const server = await serve(site, port);
console.log(`serving ${site} at ${url} (pid ${process.pid})`);

let allOk = true;
for (const name of profiles) {
  const logFile = path.join(out, `${name}.log`);
  fs.rmSync(logFile, { force: true });
  const log = logger(logFile);
  const results = [];
  const check = (ok, what) => { results.push([!!ok, what]); log(`${ok ? "PASS" : "FAIL"} ${what}`); console.log(`${name}: ${ok ? "PASS" : "FAIL"} ${what}`); };
  const summary = { profile: name, load: fs.readFileSync("/proc/loadavg", "utf8").split(" ")[0] };
  let peak = { webHwm: 0, gpuHwm: 0 };
  const sample = () => { const s = procSummary(descendants()); peak = { webHwm: Math.max(peak.webHwm, s.webHwm), gpuHwm: Math.max(peak.gpuHwm, s.gpuHwm) }; };
  const timer = setInterval(sample, 500);
  const b = await launchProfile(name);
  const { page, prof } = b;
  const errors = [];
  let audioNoise = 0;
  page.on("console", (m) => {
    log(`[console.${m.type()}] ${m.text()}`);
    if (m.type() !== "error") return;
    if (silentAudioNoise(prof.engine, m.text())) audioNoise++; else errors.push(m.text());
  });
  page.on("pageerror", (e) => { log(`[pageerror] ${e.message}`); if (silentAudioNoise(prof.engine, e.message)) audioNoise++; else errors.push(e.message); });
  page.on("crash", () => { log("[crash]"); errors.push("page crashed"); });
  const shot = (n) => page.screenshot({ path: path.join(out, `${name}-${n}.png`) }).catch((e) => log("screenshot failed: " + e.message));
  const state = () => page.evaluate(() => window.afterHours || null).catch(() => null);
  const until = async (pred, ms, what) => {
    const end = Date.now() + ms;
    while (Date.now() < end) {
      const s = await state();
      if (s && pred(s)) return s;
      await sleep(250);
    }
    log(`timed out waiting for ${what}: ${JSON.stringify(await state())}`);
    return null;
  };
  /** The middle of a game button (CSS px) from the game's own report, or null. */
  const uiAt = async (n) => page.evaluate((n) => { const r = window.afterHoursUi && window.afterHoursUi[n]; return r ? [r[0] + r[2] / 2, r[1] + r[3] / 2] : null; }, n);
  const btnAt = async (id) => page.evaluate((id) => {
    const e = document.getElementById(id); if (!e) return null;
    const r = e.getBoundingClientRect(); const cs = getComputedStyle(e);
    return cs.display === "none" || r.width === 0 ? null : [r.x + r.width / 2, r.y + r.height / 2];
  }, id).then(async (p) => {
    if (!p) log(`no ${id} showing: #tc ${await page.evaluate(() => document.querySelector("#tc").className)}, ${JSON.stringify(await state())}`);
    return p;
  });
  const tapUi = async (t, n, what) => {
    const p = await uiAt(n);
    if (!p) { check(false, `${what}: no ${n} on screen`); return false; }
    await t.tap(p[0], p[1], holdMs);
    await sleep(prof.engine === "webkit" ? 1500 : 800);
    return true;
  };
  const test = async () => (await state())?.test || null;
  const keysOn = () => page.evaluate(() => [...document.querySelectorAll("#tc-keys button")].map((b) => b.textContent));
  const softKey = async (t, label) => {
    const p = await page.evaluate((l) => { const b = [...document.querySelectorAll("#tc-keys button")].find((x) => x.textContent === l); if (!b) return null; const r = b.getBoundingClientRect(); return [r.x + r.width / 2, r.y + r.height / 2]; }, label);
    if (!p) return false;
    await t.tap(p[0], p[1]);
    await sleep(prof.engine === "webkit" ? 1500 : 700);
    return true;
  };
  const slow = prof.engine === "webkit" ? 2 : 1;
  // Taps on the game's own buttons last long enough to span a frame (headless WebKit can draw
  // under one a second here), as a finger's tap does on a phone running at speed.
  const holdMs = prof.engine === "webkit" ? 600 : 120;
  // The game offers to lower settings when it runs slowly (headless WebKit draws a few frames a
  // second here): answer by tapping "Keep these settings", the choice's second row.
  let choiceAnswered = null;
  const settle = async (t) => {
    const s0 = await state();
    if (!s0 || !String(s0.blockers).includes("choice")) return;
    await shot("choice");
    const ok = await tapUi(t, "Row#2", "the choice's second row");
    const s1 = await until((s) => !String(s.blockers).includes("choice"), 8000, "the choice to close");
    if (choiceAnswered === null) { choiceAnswered = ok && !!s1; check(choiceAnswered, `a choice menu ("Running slowly") is answered by a tap on a row`); }
  }; // headless WebKit draws a few frames a second here

  try {
    log(`${name}: ${prof.engine} ${b.version}, ${prof.device}`);
    const t0 = Date.now();
    await page.goto(url, { waitUntil: "load", timeout: 120000 });
    const env = await page.evaluate(() => ({ coarse: matchMedia("(pointer: coarse)").matches, fine: matchMedia("(any-pointer: fine)").matches,
      w: innerWidth, h: innerHeight, dpr: devicePixelRatio, mobile: window.afterHoursPage?.mobile }));
    log("env " + JSON.stringify(env));
    summary.env = env;
    let st = await until((s) => s.screen === "title", 300000, "the title");
    summary.titleSecs = +((Date.now() - t0) / 1000).toFixed(1);
    check(!!st, `the title shows, no "load anyway" question first (${summary.titleSecs} s)`);
    check(st && st.mobile && st.quality === 0, `a phone or tablet starts on Graphics fidelity Low (${st?.qualityName}, mobile ${st?.mobile})`);
    await sleep(2500);
    const tc = await page.evaluate(() => ({ on: document.querySelector("#tc")?.classList.contains("on"),
      visible: [...document.querySelectorAll(".tc-btn")].filter((e) => getComputedStyle(e).display !== "none").map((e) => e.id),
      rotate: document.querySelector("#rotate")?.classList.contains("on"), canvas: (() => { const c = document.querySelector("#unity-canvas"); return [c.width, c.height]; })() }));
    log("controls at the title " + JSON.stringify(tc));
    summary.canvas = tc.canvas;
    check(tc.on && tc.visible.length === 0 && !tc.rotate, `touch controls are ready, but no game buttons over the title (visible: ${tc.visible.join(",") || "none"}), no rotate prompt in landscape`);
    summary.atTitle = { ...(await pageMem(page)) };
    summary.titleFps = +(await fps(page, 3000)).toFixed(1);
    await shot("1-title");

    const t = await toucher(page, prof.engine);
    // The first launch shows the Brightness page; its Done button is tapped. That's the first touch.
    const asksBefore = await page.evaluate(() => (window.__ahAudioAsks || []).filter((a) => a.afterInput).length);
    const states0 = await page.evaluate(() => (window.__ahAudio || []).map((c) => c.state));
    check(!states0.includes("running"), `audio waits for a touch (AudioContext ${states0.join(",") || "none"})`);
    let tapped = await tapUi(t, "Btn_Done", "the Brightness page's Done");
    await sleep(800);
    const asks = await page.evaluate(() => (window.__ahAudioAsks || []).filter((a) => a.afterInput).length);
    const states1 = await page.evaluate(() => (window.__ahAudio || []).map((c) => c.state));
    const silent = await page.evaluate(() => !!window.__ahAudioSilent);
    check(asks > asksBefore && states1.includes("running"), `the first tap unlocks audio (resume asked after the touch ${asks - asksBefore}×, AudioContext ${states1.join(",")}${silent ? "; this WebKit has no audio output, so its context is a silent stand-in" : ""})`);
    st = await state();
    check(tapped && !(await uiAt("Btn_Default")), "a tap on Done closes the Brightness page");
    await shot("2-title-after-tap");

    // Settings by tap: Graphics fidelity steps up from Low, and three more taps go round to Low again.
    if (await tapUi(t, "Btn_Settings", "Settings")) {
      await shot("3-settings");
      const p = await uiAt("Steps_Graphics fidelity");
      if (p) {
        // One tap, then wait for the game to take it (headless WebKit can draw under a frame a second).
        const step = async (from) => { await t.tap(p[0] + 80, p[1], holdMs); return (await until((s) => s.quality !== from, 30000, `fidelity to change from ${from}`))?.quality ?? from; };
        const q1 = await step(0);
        let q2 = q1;
        for (let i = 0; i < 6 && q2 !== 0; i++) q2 = await step(q2);
        check(q1 === 1 && q2 === 0, `a tap on Graphics fidelity steps it (Low → ${q1}, and round again to ${q2})`);
      } else check(false, "Settings: no Graphics fidelity row reported");
      // Stepping through High and Ultra costs a lot on a phone for a moment: note that peak, then measure from here.
      summary.settingsPeakMB = +MB((await pageMem(page))?.gpu?.peak || 0);
      log(`WebGL estimate peak while stepping through every Graphics fidelity: ${summary.settingsPeakMB} MB`);
      await tapUi(t, "Btn_Done", "Settings' Done");
      check(!(await uiAt("Steps_Graphics fidelity")), "a tap on Done closes Settings");
    }

    // Into Night 1 by tap: Continue (the title already keeps Night 1 as the place to carry on from),
    // or New Game and the first row of its question.
    const cont = await page.evaluate(() => Object.keys(window.afterHoursUi || {}).find((k) => k.startsWith("Btn_Continue")));
    if (cont) await tapUi(t, cont, "Continue");
    else {
      await tapUi(t, "Btn_New Game", "New Game");
      if (await uiAt("Row")) await tapUi(t, "Row", "the New Game question's first row");
    }
    st = await until((s) => s.screen === "night", 120000, "Night 1");
    check(st && st.night === 1, `a tap on ${cont ? "Continue" : "New Game"} starts Night 1 (${st?.screen} ${st?.night})`);
    await sleep(2000 * slow);
    await page.evaluate(() => { if (window.__ahMem) window.__ahMem.peak = window.__ahMem.bytes; });
    await shot("4-night");

    // The controls in a night: thumb-sized, inside the safe area, not on top of each other.
    const layout = await page.evaluate(() => {
      const vis = [...document.querySelectorAll(".tc-btn")].filter((e) => getComputedStyle(e).display !== "none");
      const r = vis.map((e) => { const b = e.getBoundingClientRect(); return { id: e.id, x: b.x, y: b.y, w: b.width, h: b.height }; });
      const over = [];
      for (let i = 0; i < r.length; i++) for (let j = i + 1; j < r.length; j++) {
        const a = r[i], c = r[j];
        if (a.x < c.x + c.w && c.x < a.x + a.w && a.y < c.y + c.h && c.y < a.y + a.h) over.push(a.id + "/" + c.id);
      }
      return { r, over, w: innerWidth, h: innerHeight };
    });
    log("night layout " + JSON.stringify(layout));
    const small = layout.r.filter((b) => b.w < 44 || b.h < 44).map((b) => b.id);
    const outside = layout.r.filter((b) => b.x < 0 || b.y < 0 || b.x + b.w > layout.w || b.y + b.h > layout.h).map((b) => b.id);
    check(layout.r.length >= 8 && small.length === 0 && outside.length === 0 && layout.over.length === 0,
      `${layout.r.length} buttons in a night, all at least 44 px (smallest ${Math.min(...layout.r.map((b) => Math.min(b.w, b.h)))}), on screen, none overlapping${small.length + outside.length + layout.over.length ? ` (small ${small}, outside ${outside}, overlap ${layout.over})` : ""}`);
    summary.inNight = { ...(await pageMem(page)) };
    summary.nightFps = +(await fps(page, 4000)).toFixed(1);

    const W = layout.w, H = layout.h;
    // Look: a drag on the right half turns the view.
    await settle(t);
    let a = await test();
    await t.drag(1, W * 0.62, H * 0.45, W * 0.62 - 140, H * 0.45, 900 * slow, 14);
    await sleep(800 * slow);
    let c = await test();
    check(a && c && Math.abs(angle(c.yaw - a.yaw)) > 8, `a drag on the right turns the view (yaw ${a?.yaw.toFixed(1)}° → ${c?.yaw.toFixed(1)}°)`);
    // Walk: the stick (pushed up) moves the player.
    await settle(t);
    a = await test();
    await t.down(2, 150, H - 130); await t.move(2, 150, H - 175); await t.move(2, 150, H - 190);
    await shot("5-stick-held");
    await sleep(2000 * slow);
    await t.up(2);
    await sleep(600 * slow);
    c = await test();
    check(a && c && dist(a, c) > 0.4, `the left stick walks (moved ${a && c ? dist(a, c).toFixed(2) : "?"} m)`);
    // Both at once: walk and turn with two fingers.
    await settle(t);
    a = await test();
    await t.down(3, 150, H - 130); await t.move(3, 190, H - 170);
    await t.down(4, W * 0.6, H * 0.4);
    for (let i = 1; i <= 12; i++) { await t.move(4, W * 0.6 + i * 12, H * 0.4); await sleep(100 * slow); }
    await sleep(600 * slow);
    await t.up(4); await t.up(3);
    await sleep(600 * slow);
    c = await test();
    log("two fingers: before " + JSON.stringify(a) + " after " + JSON.stringify(c) + " screen " + (await state())?.screen);
    check(a && c && dist(a, c) > 0.3 && Math.abs(angle(c.yaw - a.yaw)) > 8, `two fingers at once: walks ${a && c ? dist(a, c).toFixed(2) : "?"} m and turns ${a && c ? angle(c.yaw - a.yaw).toFixed(1) : "?"}°`);

    // Crouch and brisk walk are switches.
    await settle(t);
    let p = await btnAt("b-crouch");
    await t.tap(...p); await sleep(900 * slow);
    c = await test();
    const crouched = c?.crouch;
    await t.tap(...p); await sleep(900 * slow);
    c = await test();
    check(crouched === true && c?.crouch === false, `Crouch switches crouching on and off (${crouched}, then ${c?.crouch})`);
    p = await btnAt("b-sprint");
    await t.tap(...p);
    await t.down(5, 150, H - 130); await t.move(5, 150, H - 190);
    await sleep(1200 * slow);
    c = await test();
    await t.up(5); await sleep(800 * slow);
    const after = await test();
    check(c?.brisk === true && after?.brisk === false, `Brisk walks briskly while the stick is held, and ends when it's let go (${c?.brisk}, then ${after?.brisk})`);

    // Tool: pins the next tool (automatic → cloth).
    await settle(t);
    a = await test();
    await t.tap(...(await btnAt("b-tool"))); await sleep(900 * slow);
    c = await test();
    check(a && c && c.pinned !== a.pinned, `Tool pins the next tool (${a?.pinned} → ${c?.pinned})`);
    await t.tap(...(await btnAt("b-tool"))); await sleep(400 * slow);
    for (let i = 0; i < 3 && (await test())?.pinned !== "None"; i++) { await t.tap(...(await btnAt("b-tool"))); await sleep(600 * slow); }

    // Clean and Spray are held.
    await settle(t);
    p = await btnAt("b-use");
    await t.down(6, ...p); await sleep(900 * slow);
    c = await test();
    await t.up(6); await sleep(500 * slow);
    a = await test();
    check(c?.use === true && a?.use === false, `Clean is held while the finger stays (${c?.use}, then ${a?.use})`);
    p = await btnAt("b-spray");
    await t.down(7, ...p); await sleep(900 * slow);
    c = await test();
    await t.up(7); await sleep(500 * slow);
    a = await test();
    check(c?.spray === true && a?.spray === false, `Spray is held while the finger stays (${c?.spray}, then ${a?.spray})`);
    await shot("6-buttons");

    // Use: whatever the prompt offers (the closet's door or a switch, or picking something up).
    await settle(t);
    st = await state();
    const promptE = st?.touch?.prompt?.E;
    log("prompt " + JSON.stringify(st?.touch));
    if (promptE) {
      const lit = await page.evaluate(() => document.querySelector("#b-interact").classList.contains("lit"));
      await t.tap(...(await btnAt("b-interact"))); await sleep(1500 * slow);
      const st2 = await state();
      check(lit && JSON.stringify(st2?.touch?.prompt) !== JSON.stringify(st?.touch?.prompt), `Use does what the prompt says ("${promptE}" → now ${JSON.stringify(st2?.touch?.prompt)}), and was lit for it`);
      if (st2?.touch?.holding) {
        await shot("7-holding");
        const label = await page.evaluate(() => document.querySelector("#b-use").textContent);
        p = await btnAt("b-use");
        await t.down(8, ...p); await sleep(1200 * slow); await t.up(8); await sleep(1500 * slow);
        const st3 = await state();
        check(label === "Throw" && !st3?.touch?.holding, `holding something, the big button says ${label} and a hold and release throws it (holding: ${st3?.touch?.holding})`);
      }
    } else log("no Use prompt in view; Use not checked");

    // The shift sheet: its button opens it, soft keys page it and close it.
    await settle(t);
    await t.tap(...(await btnAt("b-sheet")));
    st = await until((s) => s.screen === "clipboard", 10000, "the clipboard");
    await sleep(800 * slow);
    const keys = await keysOn();
    const ctl = await page.evaluate(() => [...document.querySelectorAll(".tc-btn")].filter((e) => getComputedStyle(e).display !== "none").map((e) => e.id));
    await shot("8-clipboard");
    check(!!st && keys.includes("Close") && keys.includes("Page ▶") && ctl.length === 0, `Sheet opens the shift sheet, with soft keys (${keys.join(" | ")}) and no game buttons`);
    const page0 = (await test())?.page;
    await softKey(t, "Page ▶");
    const page1 = (await test())?.page;
    await softKey(t, "Close");
    st = await until((s) => s.screen === "night", 10000, "back to the night");
    check(page1 !== page0 && !!st, `its soft keys turn the page (${page0} → ${page1}) and close it`);

    // Pause and resume by tap.
    await settle(t);
    await t.tap(...(await btnAt("b-pause")));
    st = await until((s) => s.screen === "pause", 10000, "the pause menu");
    await shot("9-pause");
    await tapUi(t, "Btn_Resume", "Resume");
    const resumed = await until((s) => s.screen === "night", 10000, "resumed");
    check(!!st && !!resumed, "❚❚ pauses the night and a tap on Resume carries on");

    // Upright: asked to turn back, and the night pauses.
    await settle(t);
    const vp = page.viewportSize();
    await page.setViewportSize({ width: vp.height, height: vp.width });
    await sleep(1500 * slow);
    const rot = await page.evaluate(() => document.querySelector("#rotate").classList.contains("on"));
    st = await until((s) => s.screen === "pause", 8000, "paused when upright");
    await shot("10-portrait");
    await page.setViewportSize(vp);
    await sleep(1500 * slow);
    const rot2 = await page.evaluate(() => document.querySelector("#rotate").classList.contains("on"));
    check(rot && !!st && !rot2, `held upright it asks to turn the device (${rot}) and pauses (${st?.screen}); sideways again the prompt goes (${!rot2})`);
    await tapUi(t, "Btn_Resume", "Resume");
    await until((s) => s.screen === "night", 10000, "resumed");

    // Another input hides them; a touch brings them back.
    await settle(t);
    await page.keyboard.press("Shift");
    await sleep(500);
    const hiddenByKey = await page.evaluate(() => !document.querySelector("#tc").classList.contains("on"));
    await t.tap(W * 0.7, H * 0.3); await sleep(500);
    const back = await page.evaluate(() => document.querySelector("#tc").classList.contains("on"));
    await sleep(1200); // mouse events within a second of a touch are taken as the browser's own, after a tap
    await page.mouse.move(W * 0.5, H * 0.5); await page.mouse.move(W * 0.5 + 60, H * 0.5 + 30, { steps: 4 });
    await sleep(500);
    const hiddenByMouse = await page.evaluate(() => !document.querySelector("#tc").classList.contains("on"));
    await shot("11-mouse");
    await t.tap(W * 0.7, H * 0.3); await sleep(500);
    check(hiddenByKey && back && hiddenByMouse, `a key hides the controls (${hiddenByKey}), a touch shows them again (${back}), the mouse hides them (${hiddenByMouse})`);

    summary.end = { ...(await pageMem(page)), test: await test() };
    sample();
    summary.peak = peak;
    check(errors.length === 0, `no console errors (${errors.length}${errors.length ? ": " + errors.slice(0, 4).join(" | ") : ""})${audioNoise ? `; ${audioNoise} audio decoding errors from this WebKit's missing AAC decoder not counted` : ""}`);
  } catch (e) {
    check(false, "test crashed: " + e.stack);
  } finally {
    clearInterval(timer);
    await b.close();
  }
  const g = summary.end?.gpu || summary.inNight?.gpu || {};
  const mono = summary.end?.test;
  const mem = `heap ${MB(summary.end?.heap || summary.inNight?.heap || 0)} MB${mono ? ` (Unity: managed ${MB(mono.monoHeap)} MB, native ${MB(mono.native)} MB of ${MB(mono.reserved)} reserved)` : ""}, WebGL est. peak in the night ${MB(g.peak || 0)} MB (stepping through Ultra ${summary.settingsPeakMB} MB), content process peak ${MB(peak.webHwm)} MB, GPU process peak ${MB(peak.gpuHwm)} MB, canvas ${summary.canvas}, fps title ${summary.titleFps} night ${summary.nightFps} (load ${summary.load})`;
  log(mem);
  console.log(`${name}: ${mem}`);
  const fails = results.filter((r) => !r[0]).length;
  summary.passed = results.length - fails;
  summary.total = results.length;
  fs.writeFileSync(path.join(out, `${name}.json`), JSON.stringify(summary, null, 1));
  log(`${name}: ${results.length - fails}/${results.length} passed`);
  console.log(`${name}: ${results.length - fails}/${results.length} passed (log ${logFile})`);
  allOk &&= fails === 0;
}
server.close();
process.exit(allOk ? 0 : 1);

function angle(d) { return ((d + 540) % 360) - 180; }
function dist(a, c) { return Math.hypot(a.x - c.x, a.z - c.z); }
