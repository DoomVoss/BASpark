import { createClickFx } from "blue-archive-touch-effect";

const defaultSettings = {
  color: "45,175,255",
  scale: 1.5,
  opacity: 1.0,
  speed: 1.0,
  triangleRenderCount: 4
};
const clickFxBaseScaleMultiplier = 0.6;

let currentSettings = { ...defaultSettings };
let clickFx = null;

function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value));
}

function parseRgb(color) {
  const parts = `${color}`.split(",").map((part) => Number.parseInt(part.trim(), 10));
  if (parts.length !== 3 || parts.some((part) => Number.isNaN(part))) {
    return [45, 175, 255];
  }

  return parts.map((part) => clamp(part, 0, 255));
}

function mapSettingsToConfig(settings) {
  const [r, g, b] = parseRgb(settings.color);
  const scale = clamp(Number(settings.scale) || defaultSettings.scale, 0.5, 3);
  const opacity = clamp(Number(settings.opacity) || defaultSettings.opacity, 0.1, 1);
  const speed = clamp(Number(settings.speed) || defaultSettings.speed, 0.2, 3);
  const triangleRenderCount = clamp(
    Math.round(Number(settings.triangleRenderCount) || defaultSettings.triangleRenderCount),
    1,
    10
  );

  return {
    themeColor: {
      r: r / 255,
      g: g / 255,
      b: b / 255
    },
    effectScale: 0.2 * clickFxBaseScaleMultiplier * (scale / defaultSettings.scale),
    duration: clamp(0.7 / speed, 0.2, 1.2),
    globalAlpha: opacity,
    d5CountMin: triangleRenderCount,
    d5CountMax: triangleRenderCount
  };
}

function ensureClickFx() {
  if (clickFx) {
    return clickFx;
  }

  const target = document.getElementById("clickFxRoot");
  if (!target) {
    console.error("BASpark click FX host element was not found.");
    return null;
  }

  clickFx = createClickFx({
    target,
    config: mapSettingsToConfig(currentSettings)
  });

  return clickFx;
}

function updateSettings(partial) {
  currentSettings = { ...currentSettings, ...partial };
  const fx = ensureClickFx();
  if (!fx) {
    return;
  }

  fx.updateConfig(mapSettingsToConfig(currentSettings));
}

function spawnAt(x, y) {
  const fx = ensureClickFx();
  if (!fx) {
    return;
  }

  fx.spawnAtLocal(x, y);
}

function resize() {
  clickFx?.resize();
}

window.BASparkBlueArchiveFx = {
  resize,
  spawnAt,
  updateSettings
};

if (document.readyState === "loading") {
  document.addEventListener("DOMContentLoaded", () => {
    ensureClickFx();
  });
} else {
  ensureClickFx();
}

window.addEventListener("resize", resize);
