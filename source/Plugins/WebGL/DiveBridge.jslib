// Calls from the game into the browser page.
mergeInto(LibraryManager.library, {
  // The student's local date as YYYY-MM-DD (the same dayKey as the other games and the checker).
  DiveLocalDay: function () {
    var d = new Date();
    var p = function (n) { return (n < 10 ? "0" : "") + n; };
    var s = d.getFullYear() + "-" + p(d.getMonth() + 1) + "-" + p(d.getDate());
    var size = lengthBytesUTF8(s) + 1;
    var buf = _malloc(size);
    stringToUTF8(s, buf, size);
    return buf;
  },

  // Keeps a copy of the log rows still waiting to be sent. If the tab is closed or hidden before
  // the game sends them, the page sends this copy with navigator.sendBeacon (text/plain, so no
  // preflight). The Sheet ignores a row it already has, so sending twice does no harm.
  DiveSetBackup: function (urlPtr, jsonPtr) {
    var w = window;
    w.__diveBackup = { url: UTF8ToString(urlPtr), json: UTF8ToString(jsonPtr), sent: false };
    if (w.__diveBackupHooked) return;
    w.__diveBackupHooked = true;
    var send = function () {
      var b = w.__diveBackup;
      if (!b || !b.url || !b.json || b.sent || !navigator.sendBeacon) return;
      try { b.sent = navigator.sendBeacon(b.url, new Blob([b.json], { type: "text/plain;charset=utf-8" })); } catch (e) { }
    };
    w.addEventListener("pagehide", send);
    document.addEventListener("visibilitychange", function () { if (document.visibilityState === "hidden") send(); });
  },

  // Copies the completion code for the student to paste into D2L.
  DiveCopy: function (textPtr) {
    var s = UTF8ToString(textPtr);
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) { navigator.clipboard.writeText(s); return; }
    } catch (e) { }
    var ta = document.createElement("textarea");
    ta.value = s; document.body.appendChild(ta); ta.select();
    try { document.execCommand("copy"); } catch (e) { }
    document.body.removeChild(ta);
  },

  // Screen (CSS) pixels per pixel of the game's drawing, so the game knows how big its text really
  // looks: about 0.5 on a phone, whose drawing uses 2 device pixels per screen pixel.
  DiveCssPerDevicePixel: function () {
    try { var c = Module.canvas; if (c && c.width > 0 && c.clientWidth > 0) return c.clientWidth / c.width; } catch (e) { }
    return 1 / Math.min(window.devicePixelRatio || 1, 2);
  },

  // The size of the game's canvas (its area in the browser window) in CSS pixels, for the log.
  // No other device details are recorded.
  DiveCanvasWidth: function () { try { return Math.round(Module.canvas.getBoundingClientRect().width) || 0; } catch (e) { return 0; } },
  DiveCanvasHeight: function () { try { return Math.round(Module.canvas.getBoundingClientRect().height) || 0; } catch (e) { return 0; } },

  // 1 when the browser or system asks for reduced motion.
  DivePrefersReducedMotion: function () {
    try { return window.matchMedia("(prefers-reduced-motion: reduce)").matches ? 1 : 0; } catch (e) { return 0; }
  }
});
