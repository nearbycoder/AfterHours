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

  // What the game is showing, for the page and for Tools/check-pages.mjs: window.afterHours.
  AH_Report: function (json) {
    try { window.afterHours = JSON.parse(UTF8ToString(json)); } catch (e) { }
    if (window.onAfterHours) window.onAfterHours(window.afterHours);
  },
});
