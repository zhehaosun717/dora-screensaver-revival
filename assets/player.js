"use strict";

(function () {
  const SWF_NAME_PATTERN = /^[A-Za-z0-9_-]+\.swf$/;

  function report(kind, message) {
    const text = `[${kind}] ${message}`;
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage(text);
    } else {
      console.log(text);
    }
  }

  window.addEventListener("error", (e) => report("error", `${e.message} @ ${e.filename}:${e.lineno}`));
  window.addEventListener("unhandledrejection", (e) => report("error", String(e.reason)));
  document.addEventListener("contextmenu", (e) => e.preventDefault());
  // Backup wake path for the host's global input hooks.
  window.addEventListener("keydown", () => report("wake", "key"), true);
  window.addEventListener("pointerdown", () => report("wake", "pointer"), true);
  window.addEventListener("wheel", () => report("wake", "wheel"), { capture: true, passive: true });

  const params = new URLSearchParams(location.search);
  const swf = params.get("swf") || "";
  if (!SWF_NAME_PATTERN.test(swf)) {
    report("error", `rejected swf parameter: ${JSON.stringify(swf)}`);
    return;
  }

  // fit: 4:3 with black bars | extend: widen the stage and show off-stage art |
  // fill: scale up and crop | stretch: distort to fill the screen
  const LAYOUTS = {
    fit: { letterbox: "on", scale: "showAll" },
    extend: { letterbox: "off", scale: "showAll" },
    fill: { letterbox: "off", scale: "noBorder" },
    stretch: { letterbox: "off", scale: "exactFit" },
  };
  const layout = LAYOUTS[params.get("layout")] || LAYOUTS.fit;

  window.RufflePlayer = window.RufflePlayer || {};
  window.RufflePlayer.config = {
    publicPath: "ruffle/",
    polyfills: false,
    autoplay: "on",
    unmuteOverlay: "hidden",
    splashScreen: false,
    letterbox: layout.letterbox,
    scale: layout.scale,
    forceScale: true,
    contextMenu: "off",
    menu: false,
    allowFullscreen: false,
    allowScriptAccess: false,
    openUrlMode: "deny",
    allowNetworking: "none",
    upgradeToHttps: false,
    warnOnUnsupportedContent: false,
    showSwfDownload: false,
    logLevel: "error",
    quality: "high",
    frameRate: Number(params.get("fps")) || null,
    deviceFontRenderer: "canvas",
  };

  try {
    const ruffle = window.RufflePlayer.newest();
    const player = ruffle.createPlayer();
    document.getElementById("stage").appendChild(player);
    const loaded = player.ruffle ? player.ruffle().load({ url: `swf/${swf}` }) : player.load({ url: `swf/${swf}` });
    Promise.resolve(loaded)
      .then(() => report("info", `loaded ${swf}`))
      .catch((err) => report("error", `load failed: ${err}`));
  } catch (err) {
    report("error", `player init failed: ${err}`);
  }
})();
