// On-screen controls for phones and tablets. The page draws them over the game; the game reads
// their state once a frame through window.afterHoursTouch.read() (AH_TouchRead in
// Assets/Plugins/WebGL/AfterHoursWeb.jslib, TouchInput.cs) and says what to show through
// window.afterHours.touch (TouchInput.Describe): whether a night is being played (the stick and
// buttons), which buttons its prompt names (they light up), and soft keys for screens read with keys.
//
// Shown only while touch is the input in use: from the start on a touch-first device (a coarse
// pointer and no fine one), or after a real touch; hidden again the moment a key, the mouse or a
// gamepad is used. The left of the screen is a stick that appears under the thumb, a drag on the
// right turns the view, and every finger is tracked on its own (multi-touch).
(function () {
  "use strict";
  // Button bits, as in TouchInput.cs.
  var BIT = { use: 1, spray: 2, interact: 4, drop: 8, torch: 16, clipboard: 32, sprint: 64, crouch: 128, pause: 256, tool: 512,
              up: 1 << 10, down: 1 << 11, left: 1 << 12, right: 1 << 13, confirm: 1 << 14, back: 1 << 15, keep: 1 << 16, alt: 1 << 17 };
  var HOLD = BIT.use | BIT.spray; // held as long as the finger stays (and also turn the view while held)
  var STICK_R = 54, DEAD = 0.12;

  var touchFirst = matchMedia("(pointer: coarse)").matches && !matchMedia("(any-pointer: fine)").matches;
  var st = { active: touchFirst, mx: 0, my: 0, lx: 0, ly: 0, held: 0, pressed: 0, toggles: 0 };
  var shown = { play: false, keys: "" };
  var root, zone, stick, knob, keysBar, rotateEl;
  var stickId = null, stickC = null, lookId = null, lookP = null;
  var buttons = {};

  function el(tag, attrs, parent) {
    var e = document.createElement(tag);
    for (var k in attrs) if (k === "text") e.textContent = attrs[k]; else e.setAttribute(k, attrs[k]);
    if (parent) parent.appendChild(e);
    return e;
  }

  function build() {
    root = el("div", { id: "tc", "aria-hidden": "true" }, document.body);
    zone = el("div", { id: "tc-zone" }, root);
    stick = el("div", { id: "tc-stick" }, root);
    knob = el("div", { id: "tc-knob" }, stick);
    // id, bit, label, classes
    [["use", "use", "Clean", "big p"], ["interact", "interact", "Use", "p"], ["spray", "spray", "Spray", "p"],
     ["drop", "drop", "Drop", "p gone"], ["torch", "torch", "UV", "p small gone"], ["tool", "tool", "Tool", "p small"],
     ["crouch", "crouch", "Crouch", "p small"], ["sprint", "sprint", "Brisk", "p small"],
     ["pause", "pause", "❚❚", "p small"], ["sheet", "clipboard", "Sheet", "p small"], ["full", null, "⛶", "p small gone"]]
      .forEach(function (b) {
        var e = el("button", { id: "b-" + b[0], class: "tc-btn " + b[3], type: "button", text: b[2] }, root);
        buttons[b[0]] = e;
        if (b[1]) bindButton(e, BIT[b[1]], b[1] === "crouch" || b[1] === "sprint");
      });
    var full = buttons.full;
    if (document.fullscreenEnabled || document.webkitFullscreenEnabled) full.classList.remove("gone");
    full.addEventListener("click", toggleFullscreen);
    keysBar = el("div", { id: "tc-keys" }, root);
    rotateEl = document.querySelector("#rotate");

    zone.addEventListener("pointerdown", zoneDown);
    zone.addEventListener("pointermove", zoneMove);
    zone.addEventListener("pointerup", zoneUp);
    zone.addEventListener("pointercancel", zoneUp);
    restStick();
  }

  // ---- the stick and looking --------------------------------------------------------------------

  function restStick() {
    stick.style.left = "calc(env(safe-area-inset-left) + 110px)";
    stick.style.top = "calc(100% - env(safe-area-inset-bottom) - 110px)";
    knob.style.transform = "";
    stick.classList.remove("held");
  }

  function zoneDown(e) {
    if (e.pointerType === "mouse") return;
    e.preventDefault();
    zone.setPointerCapture(e.pointerId);
    if (stickId === null && e.clientX < window.innerWidth * 0.42) {
      stickId = e.pointerId;
      stickC = { x: e.clientX, y: e.clientY };
      stick.style.left = e.clientX + "px";
      stick.style.top = e.clientY + "px";
      stick.classList.add("held");
      setStick(e.clientX, e.clientY);
    } else if (lookId === null) {
      lookId = e.pointerId;
      lookP = { x: e.clientX, y: e.clientY };
    }
  }

  function zoneMove(e) {
    if (e.pointerId === stickId) setStick(e.clientX, e.clientY);
    else if (e.pointerId === lookId) look(e);
  }

  function zoneUp(e) {
    if (e.pointerId === stickId) {
      stickId = null; st.mx = st.my = 0; restStick();
      // Brisk walk ends when the stick is let go.
      st.toggles &= ~BIT.sprint; buttons.sprint.classList.remove("on");
    }
    if (e.pointerId === lookId) lookId = null;
  }

  function setStick(x, y) {
    var dx = x - stickC.x, dy = y - stickC.y, d = Math.sqrt(dx * dx + dy * dy);
    if (d > STICK_R) { dx *= STICK_R / d; dy *= STICK_R / d; d = STICK_R; }
    knob.style.transform = "translate(" + dx + "px," + dy + "px)";
    var k = d / STICK_R < DEAD ? 0 : (d / STICK_R - DEAD) / (1 - DEAD) / (d / STICK_R);
    st.mx = (dx / STICK_R) * k;
    st.my = (-dy / STICK_R) * k;
  }

  function look(e) {
    var p = lookP;
    st.lx += e.clientX - p.x;
    st.ly -= e.clientY - p.y;
    lookP = { x: e.clientX, y: e.clientY };
  }

  // ---- buttons ----------------------------------------------------------------------------------

  function bindButton(e, bit, toggle) {
    var id = null, at = null;
    e.addEventListener("pointerdown", function (ev) {
      if (ev.pointerType === "mouse") return;
      ev.preventDefault();
      ev.stopPropagation();
      e.setPointerCapture(ev.pointerId);
      id = ev.pointerId;
      at = { x: ev.clientX, y: ev.clientY };
      st.pressed |= bit;
      if (toggle) { st.toggles ^= bit; e.classList.toggle("on", (st.toggles & bit) !== 0); }
      else { st.held |= bit; e.classList.add("down"); }
    });
    e.addEventListener("pointermove", function (ev) {
      // Holding Clean or Spray, the same thumb can still turn the view.
      if (ev.pointerId !== id || !(bit & HOLD)) return;
      st.lx += ev.clientX - at.x;
      st.ly -= ev.clientY - at.y;
      at = { x: ev.clientX, y: ev.clientY };
    });
    var up = function (ev) {
      if (ev.pointerId !== id) return;
      id = null;
      if (!toggle) { st.held &= ~bit; e.classList.remove("down"); }
    };
    e.addEventListener("pointerup", up);
    e.addEventListener("pointercancel", up);
    e.addEventListener("contextmenu", function (ev) { ev.preventDefault(); });
  }

  function releaseAll() {
    st.held = 0; st.mx = st.my = 0; stickId = lookId = null; restStick();
    for (var k in buttons) buttons[k].classList.remove("down");
  }

  function toggleFullscreen() {
    var d = document, fs = d.fullscreenElement || d.webkitFullscreenElement;
    if (fs) (d.exitFullscreen || d.webkitExitFullscreen).call(d);
    else {
      var g = d.documentElement, req = g.requestFullscreen || g.webkitRequestFullscreen;
      if (!req) return;
      var p = req.call(g);
      // Android can hold landscape once fullscreen; elsewhere this is refused, which is fine.
      if (p && p.then) p.then(function () { if (screen.orientation && screen.orientation.lock) screen.orientation.lock("landscape").catch(function () { }); }).catch(function () { });
    }
  }

  // ---- what the game says to show ---------------------------------------------------------------

  function onGame(s) {
    var t = s && s.touch;
    // Crouch and brisk walk don't carry over to the title or the next night.
    if (s && s.screen !== "night" && s.screen !== "pause" && s.screen !== "clipboard" && st.toggles) {
      st.toggles = 0; buttons.crouch.classList.remove("on"); buttons.sprint.classList.remove("on");
    }
    var play = !!(t && t.play);
    if (play !== shown.play) {
      shown.play = play;
      root.classList.toggle("play", play);
      releaseAll();
    }
    if (t && play) {
      var pr = t.prompt || {};
      buttons.use.textContent = t.holding ? "Throw" : "Clean";
      buttons.use.classList.toggle("lit", "LMB" in pr);
      buttons.interact.classList.toggle("lit", "E" in pr);
      buttons.spray.classList.toggle("lit", "RMB" in pr);
      buttons.drop.classList.toggle("gone", !t.holding);
      buttons.torch.classList.toggle("gone", !t.torch);
    }
    var keys = t && t.keys ? JSON.stringify(t.keys) : "";
    if (keys !== shown.keys) {
      shown.keys = keys;
      keysBar.textContent = "";
      (t && t.keys || []).forEach(function (k) {
        var b = el("button", { type: "button", text: k[1] }, keysBar);
        b.addEventListener("pointerdown", function (ev) {
          if (ev.pointerType === "mouse") return;
          ev.preventDefault(); st.pressed |= k[0]; b.classList.add("down");
        });
        var up = function () { b.classList.remove("down"); };
        b.addEventListener("pointerup", up);
        b.addEventListener("pointercancel", up);
      });
    }
  }

  // ---- which input is in use --------------------------------------------------------------------

  function setActive(on) {
    if (st.active === on) return;
    st.active = on;
    if (!on) releaseAll();
    refresh();
  }

  function portrait() { return window.innerHeight > window.innerWidth; }

  var wasPortrait = false;
  function refresh() {
    if (!root) return;
    root.classList.toggle("on", st.active && started);
    // The game is laid out for landscape: ask for it on a touch screen (a night pauses first).
    var turn = started && (st.active || touchFirst) && portrait();
    if (rotateEl) rotateEl.classList.toggle("on", turn);
    if (turn && !wasPortrait && shown.play) st.pressed |= BIT.pause;
    wasPortrait = turn;
  }

  var started = false;
  window.addEventListener("pointerdown", function (e) {
    if (!e.isTrusted) return;
    if (e.pointerType === "touch" || e.pointerType === "pen") setActive(true);
    else if (e.pointerType === "mouse") setActive(false);
  }, true);
  window.addEventListener("touchstart", function (e) { if (e.isTrusted) setActive(true); }, { capture: true, passive: true });
  var mouseFrom = null;
  window.addEventListener("pointermove", function (e) {
    if (!e.isTrusted || e.pointerType !== "mouse" || !st.active) return;
    // A real mouse moving a little way (not a stray event after a tap) means it's in use again.
    if (!mouseFrom) { mouseFrom = { x: e.clientX, y: e.clientY }; return; }
    if (Math.abs(e.clientX - mouseFrom.x) + Math.abs(e.clientY - mouseFrom.y) > 24) { mouseFrom = null; setActive(false); }
  }, true);
  // Browsers that don't report the mouse as a pointer: plain mouse moves, ignoring the ones a
  // browser makes up right after a tap.
  var lastTouch = 0, plainFrom = null;
  ["touchstart", "touchend"].forEach(function (n) { window.addEventListener(n, function () { lastTouch = Date.now(); plainFrom = mouseFrom = null; }, { capture: true, passive: true }); });
  window.addEventListener("mousemove", function (e) {
    if (!e.isTrusted || !st.active || Date.now() - lastTouch < 1000) return;
    if (!plainFrom) { plainFrom = { x: e.clientX, y: e.clientY }; return; }
    if (Math.abs(e.clientX - plainFrom.x) + Math.abs(e.clientY - plainFrom.y) > 24) { plainFrom = null; setActive(false); }
  }, true);
  window.addEventListener("keydown", function (e) { if (e.isTrusted) setActive(false); }, true);
  window.addEventListener("resize", refresh);
  window.addEventListener("orientationchange", refresh);

  // A gamepad in use hides them too (polled only while they show).
  function pollPads() {
    if (st.active && navigator.getGamepads) {
      var pads = navigator.getGamepads();
      for (var i = 0; i < pads.length; i++) {
        var p = pads[i];
        if (!p) continue;
        var used = p.buttons.some(function (b) { return b.pressed; }) || p.axes.some(function (a) { return Math.abs(a) > 0.5; });
        if (used) { setActive(false); break; }
      }
    }
    requestAnimationFrame(pollPads);
  }

  // ---- the game's side --------------------------------------------------------------------------

  window.afterHoursTouch = {
    /** Called by the game once a frame: the state, with look and presses since the last call. */
    read: function () {
      var held = st.held | (st.pressed & HOLD) | st.toggles; // a quick tap still counts as held for one frame
      var v = [st.active ? 1 : 0, st.mx, st.my, st.lx, st.ly, held, st.pressed, 0];
      st.lx = st.ly = 0;
      st.pressed = 0;
      return v;
    },
    /** The page: the game is running (the controls can show). */
    start: function () {
      started = true;
      if (!root) build();
      var prev = window.onAfterHours;
      window.onAfterHours = function (s) { onGame(s); if (prev) prev(s); };
      if (window.afterHours) onGame(window.afterHours);
      refresh();
      requestAnimationFrame(pollPads);
    },
    get active() { return st.active; },
    touchFirst: touchFirst,
  };
})();
