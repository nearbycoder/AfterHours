// The browser side of WebPlatform (Assets/Scripts/Core/WebPlatform.cs). Only built into the web player.
mergeInto(LibraryManager.library, {
  // The mouse is captured (pointer lock) on the game's canvas. The browser releases it on Esc
  // without passing the key on, so the game watches this to pause.
  AH_PointerLocked: function () {
    return document.pointerLockElement ? 1 : 0;
  },

  // Ask before the tab closes or reloads while a night that isn't saved yet is being played.
  AH_SetLeaveWarning: function (on) {
    if (!window.__ahLeave) {
      window.__ahLeave = function (e) {
        if (!window.__ahLeaveOn) return;
        e.preventDefault();
        e.returnValue = "";
      };
      window.addEventListener("beforeunload", window.__ahLeave);
    }
    window.__ahLeaveOn = !!on;
  },

  // The browser can capture the mouse at all (iPhones can't).
  AH_CanLockPointer: function () {
    return Module["canvas"] && Module["canvas"].requestPointerLock ? 1 : 0;
  },

  // A phone or tablet (the page decides, before the game loads): lighter defaults.
  AH_IsMobile: function () {
    return window.afterHoursPage && window.afterHoursPage.mobile ? 1 : 0;
  },

  // The on-screen controls' state (TemplateData/touch.js), once a frame, into a float[] of length n:
  // active, stick x, y, look dx, dy, held buttons, buttons pressed since the last read.
  AH_TouchRead: function (ptr, n) {
    var t = window.afterHoursTouch;
    if (!t) return;
    var v = t.read();
    for (var i = 0; i < n && i < v.length; i++) HEAPF32[(ptr >> 2) + i] = v[i];
  },

  // Browser tests (-ahWebTest): the buttons that can be tapped, as CSS-pixel rectangles from the
  // top left (window.afterHoursUi); the game gives them in canvas pixels from the bottom left.
  AH_ReportUi: function (json) {
    try {
      var c = Module["canvas"], k = c.clientWidth / c.width, h = c.clientHeight, ui = JSON.parse(UTF8ToString(json));
      for (var n in ui) { var r = ui[n]; ui[n] = [r[0] * k, h - (r[1] + r[3]) * k, r[2] * k, r[3] * k]; }
      window.afterHoursUi = ui;
    } catch (e) { }
  },

  // What the game is showing, for the page and for Tools/check-pages.mjs: window.afterHours.
  AH_Report: function (json) {
    try { window.afterHours = JSON.parse(UTF8ToString(json)); } catch (e) { }
    if (window.onAfterHours) window.onAfterHours(window.afterHours);
  },
});
