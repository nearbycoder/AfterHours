#!/usr/bin/env node
// Does the browser build reach the title? Opens the page in headless Chromium and/or Firefox with a
// fresh profile and passes only when the game reports its title screen (window.afterHours.screen,
// set by WebPlatform.Report) with no console errors, page errors or failed requests, and with no
// on-screen touch controls showing (they're for phones and tablets).
//   npm install --prefix Tools/web          (once: puppeteer-core)
//   node Tools/check-pages.mjs https://nearbycoder.github.io/AfterHours/ [--browser chromium|firefox|all]
//        [--timeout 240] [--log Logs/check-pages.log] [--shot Logs/check-pages] [--gpu]
// Chromium renders in software unless --gpu (works without a GPU, but the load takes longer).
// Exit 0: every browser reached the title cleanly. 1: one didn't. 2: bad usage or no browser.
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const opt = (name, def) => { const i = args.indexOf(name); return i >= 0 && i + 1 < args.length ? args.splice(i, 2)[1] : def; };
const which = opt("--browser", "all");
const timeout = Number(opt("--timeout", "240")) * 1000;
const logFile = opt("--log", path.join(here, "..", "Logs", "check-pages.log"));
const shot = opt("--shot", null);
const gpu = args.includes("--gpu");
if (gpu) args.splice(args.indexOf("--gpu"), 1);
const url = args[0];
if (!url || !/^https?:\/\//.test(url)) {
  console.error("usage: node Tools/check-pages.mjs <url> [--browser chromium|firefox|all] [--timeout s] [--log file] [--shot prefix]");
  process.exit(2);
}

let lib;
try { lib = await import("./web/lib.mjs"); } catch (e) {
  console.error(`can't load the browser driver (${e.message}). Run: npm install --prefix ${path.join(here, "web")}`);
  process.exit(2);
}
const { launch, openGame, waitState, downloaded, findChromium, findFirefox, logger } = lib;

const browsers = which === "all" ? ["chromium", "firefox"].filter((b) => (b === "chromium" ? findChromium() : findFirefox()))
  : [which];
if (browsers.length === 0) { console.error("no browser found"); process.exit(2); }

const log = logger(logFile);
log(`check ${url} in ${browsers.join(", ")}`);
let ok = true;
for (const name of browsers) {
  const profile = path.join(here, "..", "Logs", `check-pages-profile-${name}-${process.pid}`);
  let b;
  try {
    b = await launch(name, profile, { gpu });
    log(`${name}: ${b.version}`);
    const { page, errors, t0 } = await openGame(b.browser, url, (s) => log(`${name} ${s}`));
    const st = await waitState(page, "s.screen === 'title'", timeout, errors);
    const secs = ((Date.now() - t0) / 1000).toFixed(1);
    await new Promise((r) => setTimeout(r, 2000)); // errors raised just after the title appears count too
    const mb = ((await downloaded(page)) / 1048576).toFixed(1);
    // A desktop with a mouse never shows the on-screen controls or the rotate prompt (touch.js).
    const touch = await page.evaluate(() => ({
      on: !!document.querySelector("#tc.on"), rotate: !!document.querySelector("#rotate.on"),
      buttons: [...document.querySelectorAll(".tc-btn, #tc-keys button")].filter((e) => e.getClientRects().length > 0).length,
    })).catch(() => ({ on: null }));
    if (touch.on !== false || touch.rotate || touch.buttons) errors.push(`touch controls showing on a desktop: ${JSON.stringify(touch)}`);
    if (shot) await page.screenshot({ path: `${shot}-${name}.png` }).catch(() => { });
    const pass = !!st && errors.length === 0;
    const line = `${pass ? "PASS" : "FAIL"} ${name}: ${st ? `title in ${secs} s` : `no title after ${secs} s`}, ${mb} MB downloaded, ` +
      `${st ? `graphics ${st.qualityName}, ` : ""}touch controls ${touch.on === false && !touch.buttons ? "hidden" : "SHOWING"}, ${errors.length} error(s)${errors.length ? ": " + errors.slice(0, 5).join(" | ") : ""}`;
    log(line);
    console.log(line);
    ok &&= pass;
  } catch (e) {
    const line = `FAIL ${name}: ${e.message}`;
    log(line);
    console.log(line);
    ok = false;
  } finally {
    if (b) await b.close();
  }
}
console.log(`log: ${logFile}`);
process.exit(ok ? 0 : 1);
