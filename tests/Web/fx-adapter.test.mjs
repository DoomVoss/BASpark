import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import vm from 'node:vm';

const adapterSource = readFileSync(
  new URL('../../src/Web/fx-adapter.js', import.meta.url),
  'utf8',
);

function createHarness(options = {})
{
  const calls =
  {
    addEventListener: [],
    clearTrail: 0,
    destroy: 0,
    messages: [],
    pointerCancel: [],
    pointerDown: [],
    pointerMove: [],
    pointerUp: [],
    setCompositingReference: [],
    setInputSamplingRate: [],
    setPaused: [],
    setThemeColor: [],
    updateConfig: [],
  };
  const windowListeners = new Map();
  const canvasListeners = new Map();
  const effectHost =
  {
    style:
    {
      opacity: '1',
    },
  };

  class FakeFx
  {
    constructor(config)
    {
      if (options.constructorError)
      {
        throw options.constructorError;
      }

      this.width = 800;
      this.height = 600;
      this.config = config;
      this.resolvedEffectBackend = 'pending';
      this.resolvedBloomBackend = 'pending';
      this.resolvedHostCompositing = 'pending';
      this.compositingWarning = null;
      this.canvas =
      {
        addEventListener(type, listener)
        {
          calls.addEventListener.push(type);
          canvasListeners.set(type, listener);
        },
      };
      FakeFx.instance = this;
    }

    destroy()
    {
      calls.destroy++;
    }

    clearTrail()
    {
      calls.clearTrail++;
    }

    getConfig()
    {
      return {
        effectBackend: this.config.effectBackend,
        bloomBackend: this.config.bloomBackend,
        resolvedEffectBackend: this.resolvedEffectBackend,
        resolvedBloomBackend: this.resolvedBloomBackend,
        hostCompositing: this.config.hostCompositing,
        resolvedHostCompositing: this.resolvedHostCompositing,
        hostCompositingSurface: this.config.hostCompositingSurface,
        compositingWarning: this.compositingWarning,
      };
    }

    pointerCancel(pointerId)
    {
      calls.pointerCancel.push(pointerId);
      return true;
    }

    pointerDown(input)
    {
      calls.pointerDown.push(input);
      return true;
    }

    pointerMove(input)
    {
      calls.pointerMove.push(input);
      return true;
    }

    pointerUp(pointerId)
    {
      calls.pointerUp.push(pointerId);
      return true;
    }

    setPaused(paused, pauseOptions)
    {
      calls.setPaused.push({ paused, pauseOptions });
    }

    setCompositingReference(source, referenceOptions)
    {
      calls.setCompositingReference.push({ source, referenceOptions });
      return true;
    }

    setThemeColor(color)
    {
      calls.setThemeColor.push(color);
    }

    setInputSamplingRate(rateHz)
    {
      calls.setInputSamplingRate.push(rateHz);
      this.config.inputSamplingRate = rateHz;
      return true;
    }

    updateConfig(config)
    {
      calls.updateConfig.push(config);
      Object.assign(this.config, config);
    }
  }

  const windowMock =
  {
    __basparkRendererGeneration: 'test-generation',
    BAClickFX:
    {
      BAClickFX: FakeFx,
      BLOOM_BACKEND_CHANGE_EVENT: 'baclickfxbackendchange',
      EFFECT_BACKEND_CHANGE_EVENT: 'baclickfxeffectbackendchange',
      HOST_COMPOSITING_CHANGE_EVENT: 'baclickfxhostcompositingchange',
    },
    chrome:
    {
      webview:
      {
        postMessage(message)
        {
          calls.messages.push(JSON.parse(message));
        },
      },
    },
    addEventListener(type, listener)
    {
      windowListeners.set(type, listener);
    },
  };
  const context =
  {
    console:
    {
      error()
      {
      },
      warn()
      {
      },
    },
    document:
    {
      readyState: 'complete',
      getElementById(id)
      {
        if (options.missingEffectHost || id !== 'baspark-fx-host')
        {
          return null;
        }

        return effectHost;
      },
      addEventListener()
      {
        throw new Error('DOMContentLoaded listener is not expected for a complete document.');
      },
    },
    window: windowMock,
  };

  vm.runInNewContext(adapterSource, context, { filename: 'fx-adapter.js' });

  return {
    calls,
    canvasListeners,
    effectHost,
    fx: FakeFx.instance,
    window: windowMock,
    windowListeners,
  };
}

test('initializes the vendored renderer in manual WebView2 mode', () =>
{
  const harness = createHarness();

  assert.equal(harness.fx.config.target, harness.effectHost);
  assert.equal(harness.fx.config.inputSource, 'manual');
  assert.equal(harness.fx.config.opacity, 1);
  assert.equal(harness.fx.config.effectBackend, 'webgl2');
  assert.equal(harness.fx.config.bloomBackend, 'webgl2');
  assert.equal(harness.fx.config.inputSamplingRate, 40);
  assert.equal(harness.fx.config.outputCompositing, 'browser-overlay');
  assert.equal(harness.fx.config.overlayAlphaPolicy, 'visual-max');
  assert.equal(harness.fx.config.overlayColorCompensation, 'bright-core');
  assert.equal(harness.fx.config.overlayAlphaLimit, 0.85);
  assert.equal(harness.fx.config.hostCompositing, 'source-over');
  assert.equal(harness.fx.config.hostCompositingSurface, 'transparent-window');
  assert.equal(harness.fx.config.themeColorMode, 'relative-oklch');
  assert.equal(harness.fx.config.isolatedCompositing, false);
  assert.equal(harness.fx.config.lightBackgroundContrastAlpha, 0);
  assert.equal(harness.fx.config.maxDpr, 2);
  assert.equal(harness.effectHost.style.opacity, '1');
  assert.equal(harness.calls.setCompositingReference.length, 0);
  assert.equal(harness.calls.messages.at(-1).type, 'ready');
  assert.equal(harness.calls.messages.at(-1).generation, 'test-generation');
  assert.equal(harness.calls.messages.at(-1).requestedEffectBackend, 'webgl2');
  assert.equal(harness.calls.messages.at(-1).resolvedEffectBackend, 'pending');
  assert.equal(harness.calls.messages.at(-1).requestedBloomBackend, 'webgl2');
  assert.equal(harness.calls.messages.at(-1).resolvedBloomBackend, 'pending');
  assert.deepEqual(
    harness.calls.addEventListener,
    [
      'baclickfxbackendchange',
      'baclickfxeffectbackendchange',
      'baclickfxhostcompositingchange',
    ],
  );
});

test('maps normalized host input and BASpark settings to BAClickFX', () =>
{
  const harness = createHarness();

  harness.window.updateEffectSettings(1.5, 0.75, 1.2, 0.8);
  const settings = harness.calls.updateConfig.at(-1);
  assert.equal(settings.scale, 1);
  assert.equal(settings.opacity, 1);
  assert.equal(settings.trailTimeScale, 1.2);
  assert.equal(settings.clickTimeScale, 0.8);
  assert.equal(harness.effectHost.style.opacity, '0.75');

  harness.window.updateColor('45,175,255');
  assert.equal(harness.calls.setThemeColor.at(-1), '#2dafff');

  assert.equal(harness.window.updateInputSamplingRate(60), true);
  assert.equal(harness.window.updateInputSamplingRate(1.5), true);
  assert.equal(harness.window.updateInputSamplingRate(0), true);
  assert.deepEqual(harness.calls.setInputSamplingRate, [60, 1.5, 0]);
  assert.equal(harness.fx.config.inputSamplingRate, 0);

  harness.window.setInputContext('mouse', true);
  assert.equal(harness.calls.updateConfig.at(-1).trailAlways, true);

  harness.window.externalMove(0.25, 0.5);
  assert.equal(harness.calls.pointerMove.at(-1).x, 200);
  assert.equal(harness.calls.pointerMove.at(-1).y, 300);
  assert.equal(harness.calls.pointerMove.at(-1).pointerType, 'mouse');

  harness.window.externalBoom(0.5, 0.25);
  assert.equal(harness.calls.pointerDown.at(-1).x, 400);
  assert.equal(harness.calls.pointerDown.at(-1).y, 150);
  assert.equal(harness.calls.pointerDown.at(-1).pointerId, 1);
  assert.equal(harness.calls.pointerCancel.length, 1);
  assert.equal(harness.calls.clearTrail, 1);

  harness.window.externalUp();
  assert.deepEqual(harness.calls.pointerUp, [1]);

  harness.window.externalCancel();
  assert.equal(harness.calls.pointerCancel.at(-1), 1);
  assert.equal(harness.calls.clearTrail, 2);
});

test('applies global opacity after the renderer has completed Bloom', () =>
{
  const harness = createHarness();

  for (const opacity of [0.1, 0.5, 1])
  {
    harness.window.updateEffectSettings(1.5, opacity, 1, 1);

    assert.equal(harness.effectHost.style.opacity, String(opacity));
    assert.equal(harness.calls.updateConfig.at(-1).opacity, 1);
    assert.equal(harness.fx.config.opacity, 1);
  }
});

test('keeps the current color when host configuration is invalid', () =>
{
  const harness = createHarness();
  const colorCallCount = harness.calls.setThemeColor.length;
  const errorMessageCount = harness.calls.messages.filter(
    (message) => message.type === 'error',
  ).length;

  assert.equal(harness.window.updateColor('45,not-a-number,255'), false);
  assert.equal(harness.calls.setThemeColor.length, colorCallCount);
  assert.equal(
    harness.calls.messages.filter((message) => message.type === 'error').length,
    errorMessageCount,
  );
});

test('keeps the current sampling rate when host configuration is invalid', () =>
{
  const harness = createHarness();

  assert.equal(harness.window.updateInputSamplingRate(-1), false);
  assert.equal(harness.window.updateInputSamplingRate(0.5), false);
  assert.equal(harness.window.updateInputSamplingRate(1001), false);
  assert.equal(harness.window.updateInputSamplingRate('40'), false);
  assert.equal(harness.window.updateInputSamplingRate('invalid'), false);
  assert.equal(harness.calls.setInputSamplingRate.length, 0);
  assert.equal(harness.fx.config.inputSamplingRate, 40);
});

test('disables always-trail for touch and blocks input while paused', () =>
{
  const harness = createHarness();

  harness.window.updateEffectSettings(1.5, 0.5, 1, 1);
  harness.window.setInputContext('touch', true);
  assert.equal(harness.calls.updateConfig.at(-1).trailAlways, false);

  harness.window.externalBoom(0.1, 0.2);
  assert.equal(harness.calls.pointerDown.at(-1).pointerType, 'touch');

  harness.window.setRenderingPaused(true);
  assert.equal(harness.calls.setPaused.at(-1).paused, true);
  assert.equal(harness.calls.setPaused.at(-1).pauseOptions.clear, true);

  const downCount = harness.calls.pointerDown.length;
  assert.equal(harness.window.externalBoom(0.3, 0.4), false);
  assert.equal(harness.calls.pointerDown.length, downCount);

  harness.window.setRenderingPaused(false);
  assert.equal(harness.calls.setPaused.at(-1).paused, false);
  assert.equal(harness.calls.setPaused.at(-1).pauseOptions, undefined);
  assert.equal(harness.effectHost.style.opacity, '0.5');
});

test('changes a software Bloom fallback to the bounded native backend', () =>
{
  const harness = createHarness();
  const listener = harness.canvasListeners.get('baclickfxbackendchange');

  harness.window.updateEffectSettings(1.5, 0.5, 1, 1);
  harness.fx.resolvedEffectBackend = 'canvas2d';
  harness.fx.resolvedBloomBackend = 'software';

  listener(
    {
      detail:
      {
        requestedBloomBackend: 'webgl2',
        resolvedBloomBackend: 'software',
      },
    },
  );

  assert.equal(harness.calls.messages.at(-1).type, 'backend');
  assert.equal(harness.calls.messages.at(-1).resolvedEffectBackend, 'canvas2d');
  assert.equal(harness.calls.messages.at(-1).resolvedBloomBackend, 'software');
  assert.equal(harness.calls.updateConfig.at(-1).bloomBackend, 'native');
  assert.equal(harness.effectHost.style.opacity, '0.5');
});

test('reports Full WebGL backend resolution to the host', () =>
{
  const harness = createHarness();
  const listener = harness.canvasListeners.get('baclickfxeffectbackendchange');

  harness.fx.resolvedEffectBackend = 'webgl2';
  harness.fx.resolvedBloomBackend = 'webgl2';
  listener(
    {
      detail:
      {
        requestedEffectBackend: 'webgl2',
        resolvedEffectBackend: 'webgl2',
      },
    },
  );

  const message = harness.calls.messages.at(-1);
  assert.equal(message.type, 'backend');
  assert.equal(message.backend, 'webgl2');
  assert.equal(message.requestedEffectBackend, 'webgl2');
  assert.equal(message.resolvedEffectBackend, 'webgl2');
  assert.equal(message.requestedBloomBackend, 'webgl2');
  assert.equal(message.resolvedBloomBackend, 'webgl2');
});

test('reports resolved source-over transparent-window compositing to the host', () =>
{
  const harness = createHarness();
  const listener = harness.canvasListeners.get(
    'baclickfxhostcompositingchange',
  );

  harness.fx.resolvedHostCompositing = 'source-over';
  harness.fx.compositingWarning = null;
  listener(
    {
      detail:
      {
        requestedHostCompositing: 'source-over',
        resolvedHostCompositing: 'source-over',
        hostCompositingSurface: 'transparent-window',
        compositingWarning: null,
      },
    },
  );

  const message = harness.calls.messages.at(-1);
  assert.equal(message.type, 'backend');
  assert.equal(message.requestedHostCompositing, 'source-over');
  assert.equal(message.resolvedHostCompositing, 'source-over');
  assert.equal(message.hostCompositingSurface, 'transparent-window');
  assert.equal(message.compositingWarning, null);
});

test('reports initialization failure without announcing readiness', () =>
{
  const harness = createHarness(
    {
      constructorError: new Error('unsupported runtime'),
    },
  );

  assert.equal(harness.calls.messages.some((message) => message.type === 'ready'), false);
  assert.equal(harness.calls.messages.at(-1).type, 'error');
  assert.equal(harness.calls.messages.at(-1).phase, 'initialize');
});

test('reports a missing renderer host without announcing readiness', () =>
{
  const harness = createHarness(
    {
      missingEffectHost: true,
    },
  );

  assert.equal(harness.fx, undefined);
  assert.equal(harness.calls.messages.some((message) => message.type === 'ready'), false);
  assert.equal(harness.calls.messages.at(-1).type, 'error');
  assert.equal(harness.calls.messages.at(-1).phase, 'initialize');
});

test('destroys renderer-owned resources before the document unloads', () =>
{
  const harness = createHarness();

  harness.windowListeners.get('beforeunload')();

  assert.equal(harness.calls.destroy, 1);
});
