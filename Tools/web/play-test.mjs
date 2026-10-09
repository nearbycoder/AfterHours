#!/usr/bin/env node
// The browser build as GitHub Pages will serve it: Builds/Pages under /AfterHours/ on a local
// static server (no headers), played in headless Chromium and Firefox with fresh profiles.
//   node Tools/web/play-test.mjs [--browser chromium|firefox|all] [--port 8737] [--out Logs/web-test] [--software]
// Chromium uses the GPU (ANGLE on Vulkan) unless --software.
// Each browser: the title loads with no console errors (time and download noted); audio is held
// until the first click and runs after it; Graphics fidelity changed in Settings survives a
// reload (saved in IndexedDB); a short night: Enter starts Night 1, S walks, Esc pauses and
// resumes, Tab opens the shift sheet. Screenshots and a log per browser go to --out.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { serve, BASE } from "./serve.mjs";
import { launch, openGame, waitState, downloaded, audioState, centreDiff, findChromium, findFirefox, logger, sleep } from "./lib.mjs";

const here = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const opt = (name, def) => { const i = args.indexOf(name); return i >= 0 && i + 1 < args.length ? args[i + 1] : def; };
const which = opt("--browser", "all");
const port = Number(opt("--port", "8737"));
const out = path.resolve(opt("--out", path.join(here, "..", "..", "Logs", "web-test")));
const site = path.resolve(opt("--site", path.join(here, "..", "..", "Builds", "Pages")));
const url = `http://127.0.0.1:${port}${BASE}`;
if (!fs.existsSync(path.join(site, "index.html"))) { console.error(`no build in ${site} (Tools/build-pages.sh)`); process.exit(2); }

const browsers = which === "all" ? ["chromium", "firefox"].filter((b) => (b === "chromium" ? findChromium() : findFirefox())) : [which];
fs.mkdirSync(out, { recursive: true });
const server = await serve(site, port);
console.log(`serving ${site} at ${url} (pid ${process.pid})`);

// The canvas is laid out 1080 units tall; the viewport is 1280x720, so one unit is 2/3 px.
const U = 720 / 1080;
const W = 1280, H = 720;
/** A key held for a few frames (a press and release in one frame can be missed). */
const tap = async (page, key) => {
  // WebDriver BiDi (Firefox) sends "Enter" as the keypad's Enter; U+E006 is the main Return key.
  if (key === "Enter" && name === "firefox") key = "\uE006";
  await page.keyboard.down(key); await sleep(90); await page.keyboard.up(key); await sleep(250);
};
/** A point given in canvas units from the bottom-left (as Ui.Place uses), as viewport pixels. */
const at = (x, y) => [x * U, H - y * U];

let allOk = true;
let name; // the browser being tested (tap needs it)
for (name of browsers) {
  const log = logger(path.join(out, `${name}.log`));
  fs.rmSync(path.join(out, `${name}.log`), { force: true });
  const results = [];
  const check = (ok, what) => { results.push([ok, what]); log(`${ok ? "PASS" : "FAIL"} ${what}`); console.log(`${name}: ${ok ? "PASS" : "FAIL"} ${what}`); };
  const shot = (page, n) => page.screenshot({ path: path.join(out, `${name}-${n}.png`) }).catch((e) => log("screenshot failed: " + e.message));
  const b = await launch(name, path.join(out, `profile-${name}`), { gpu: !args.includes("--software") });
  log(`${name} ${b.version}, ${url}`);
  try {
    // 1. Load to the title.
    let { page, errors, t0 } = await openGame(b.browser, url, log, { holdAudio: true });
    let st = await waitState(page, "s.screen === 'title'", 300000, errors);
    const secs = (Date.now() - t0) / 1000;
    const mb = (await downloaded(page)) / 1048576;
    check(!!st, `the title screen shows (${secs.toFixed(1)} s, ${mb.toFixed(1)} MB downloaded, load ${fs.readFileSync("/proc/loadavg", "utf8").split(" ")[0]})`);
    check(st && st.quality === 1, `first visit starts on Graphics fidelity Medium (${st?.qualityName})`);
    await sleep(3000);
    await shot(page, "1-title");
    const before = await audioState(page);
    check(before.length > 0 && !before.includes("running"), `audio waits for input (AudioContext ${before.join(",") || "none"}; held as a first visit's autoplay policy does)`);

    // 2. First input: Esc closes the first-launch Brightness page, then a click on the backdrop.
    await tap(page, "Escape");
    await sleep(1200);
    await page.mouse.click(W * 0.75, H * 0.5);
    await sleep(1500);
    const after = await audioState(page);
    check(after.includes("running"), `audio runs after the first key and click (AudioContext ${after.join(",")})`);
    await shot(page, "2-after-input");

    // 3. Settings: the arrow keys walk the title menu to Settings; a click on Graphics fidelity's row
    // steps it up (Medium to High), then Left twice takes it to Low. The panel is 1360x1040 in the
    // middle of the 1920x1080 canvas; its right column starts 740 in, under Display and Brightness.
    for (let i = 0; i < 8 && (await page.evaluate(() => window.afterHours.selected)) !== "Btn_Settings"; i++) {
      await tap(page, "ArrowDown");
    }
    st = await page.evaluate(() => window.afterHours);
    check(st.selected === "Btn_Settings", `the arrow keys reach Settings on the title (${st.selected})`);
    await tap(page, "Enter");
    await sleep(1500);
    await shot(page, "3-settings");
    await page.mouse.click(...at(1020 + 100, 828));
    await sleep(800);
    st = await page.evaluate(() => window.afterHours);
    check(st.selected === "Steps_Graphics fidelity" && st.quality === 2, `a click on Graphics fidelity steps it up (${st.selected}, ${st.qualityName})`);
    await tap(page, "ArrowLeft");
    await tap(page, "ArrowLeft");
    await sleep(800);
    st = await page.evaluate(() => window.afterHours);
    log("after Left twice: " + JSON.stringify(st));
    await shot(page, "4-settings-changed");
    await tap(page, "Escape"); // closes Settings and saves
    await sleep(1500);
    const changed = st?.quality;
    check(changed === 0, `Settings changed Graphics fidelity to Low (now ${st?.qualityName})`);

    // 4. Reload: the setting comes back from IndexedDB.
    await page.reload({ waitUntil: "load" });
    st = await waitState(page, "s.screen === 'title'", 300000, errors);
    check(st && st.quality === changed, `after a reload Graphics fidelity is still ${st?.qualityName}`);
    await sleep(2000);
    await shot(page, "5-reloaded");

    // 5. A short night.
    await page.mouse.click(W * 0.75, H * 0.5);
    await tap(page, "Enter");
    st = await waitState(page, "s.screen === 'night'", 120000, errors);
    check(st && st.night === 1, `Enter on the title starts Night 1 (${JSON.stringify(st)})`);
    await sleep(1500);
    await shot(page, "6-night");
    await page.mouse.click(W / 2, H / 2); // capture the mouse (where the browser allows it)
    await sleep(800);
    const still = await page.screenshot();
    await page.keyboard.down("s"); await sleep(1500); await page.keyboard.up("s"); // backs away from the closet door
    await sleep(500);
    const walked = await page.screenshot({ path: path.join(out, `${name}-7-walked.png`) });
    const diff = await centreDiff(page, still, walked);
    check(diff > 4, `S walks the player back (the middle of the view changed by ${diff.toFixed(1)}/255)`);
    await tap(page, "Escape");
    st = await waitState(page, "s.screen === 'pause'", 10000, errors);
    check(!!st, "Esc pauses the night (or losing the mouse does)");
    await shot(page, "8-pause");
    await tap(page, "Escape");
    st = await waitState(page, "s.screen === 'night'", 10000, errors);
    check(!!st, "Esc again resumes");
    await sleep(500);
    await tap(page, "Tab");
    st = await waitState(page, "s.screen === 'clipboard'", 10000, errors);
    check(!!st, "Tab opens the shift sheet");
    await sleep(800);
    await shot(page, "9-clipboard");
    await tap(page, "Tab");
    st = await waitState(page, "s.screen === 'night'", 10000, errors);
    check(!!st, "Tab again puts it away");

    check(errors.length === 0, `no console errors (${errors.length}${errors.length ? ": " + errors.slice(0, 4).join(" | ") : ""})`);
  } catch (e) {
    check(false, "test crashed: " + e.message);
  } finally {
    await b.close();
  }
  const fails = results.filter((r) => !r[0]).length;
  log(`${name}: ${results.length - fails}/${results.length} passed`);
  allOk &&= fails === 0;
}
server.close();
process.exit(allOk ? 0 : 1);
