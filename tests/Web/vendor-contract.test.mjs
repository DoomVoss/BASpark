import assert from 'node:assert/strict';
import { Buffer } from 'node:buffer';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import vm from 'node:vm';

const expectedSha256 =
  '7AC5DCB27EB652C7709751B2337A18EAD5ECFBEA02B49CC651AC6B2A841CA946';
const vendorPath = new URL(
  '../../src/Web/vendor/ba-click-fx.iife.js',
  import.meta.url,
);
const adapterPath = new URL('../../src/Web/fx-adapter.js', import.meta.url);
const templatePath = new URL('../../src/Web/index.html', import.meta.url);
const versionPath = new URL(
  '../../src/licenses/ba-click-fx/VERSION.txt',
  import.meta.url,
);

test('vendored artifact matches the reviewed v1.2.29 build', () =>
{
  const bytes = readFileSync(vendorPath);
  const actual = createHash('sha256').update(bytes).digest('hex').toUpperCase();

  assert.equal(actual, expectedSha256);
});

test('vendored metadata identifies the exact reviewed artifact', () =>
{
  const metadata = readFileSync(versionPath, 'utf8');

  assert.match(metadata, /^Version: v1\.2\.29$/m);
  assert.match(
    metadata,
    /^Commit: e02336877fa74b46dff1f95d6876c58267251b1a$/m,
  );
  assert.match(metadata, new RegExp(`^SHA256: ${expectedSha256}$`, 'm'));
});

test('vendored IIFE exposes every host API required by BASpark', () =>
{
  const source = readFileSync(vendorPath, 'utf8');
  const context =
  {
    // 当前 IIFE 在模块初始化时解码内嵌纹理，浏览器会原生提供 atob。
    atob(encoded)
    {
      return Buffer.from(encoded, 'base64').toString('latin1');
    },
  };

  vm.runInNewContext(source, context, { filename: 'ba-click-fx.iife.js' });

  assert.equal(typeof context.BAClickFX.BAClickFX, 'function');
  assert.equal(typeof context.BAClickFX.createConfig, 'function');
  assert.equal(context.BAClickFX.BLOOM_BACKEND_CHANGE_EVENT, 'baclickfxbackendchange');
  assert.equal(context.BAClickFX.EFFECT_BACKEND_CHANGE_EVENT, 'baclickfxeffectbackendchange');
  assert.equal(context.BAClickFX.HOST_COMPOSITING_CHANGE_EVENT, 'baclickfxhostcompositingchange');

  const lightBackgroundConfig = context.BAClickFX.createConfig(
    {
      outputCompositing: 'browser-overlay',
      overlayAlphaPolicy: 'visual-max',
      overlayColorCompensation: 'bright-core',
      overlayAlphaLimit: 0.85,
      hostCompositing: 'source-over',
      hostCompositingSurface: 'transparent-window',
      themeColorMode: 'relative-oklch',
      inputSamplingRate: 40,
    },
  );

  assert.equal(lightBackgroundConfig.outputCompositing, 'browser-overlay');
  assert.equal(lightBackgroundConfig.overlayAlphaPolicy, 'visual-max');
  assert.equal(lightBackgroundConfig.overlayColorCompensation, 'bright-core');
  assert.equal(lightBackgroundConfig.overlayAlphaLimit, 0.85);
  assert.equal(lightBackgroundConfig.hostCompositing, 'source-over');
  assert.equal(lightBackgroundConfig.hostCompositingSurface, 'transparent-window');
  assert.equal(lightBackgroundConfig.themeColorMode, 'relative-oklch');
  assert.equal(lightBackgroundConfig.inputSamplingRate, 40);

  const prototype = context.BAClickFX.BAClickFX.prototype;
  for (const method of [
    'pointerDown',
    'pointerMove',
    'pointerUp',
    'pointerCancel',
    'clearTrail',
    'setPaused',
    'updateConfig',
    'setThemeColor',
    'setThemeColorMode',
    'setInputSamplingRate',
    'destroy',
  ])
  {
    assert.equal(typeof prototype[method], 'function', `${method} must remain public`);
  }
});

test('inline resources cannot terminate their script element early', () =>
{
  const vendor = readFileSync(vendorPath, 'utf8');
  const adapter = readFileSync(adapterPath, 'utf8');

  assert.equal(/<\/script/i.test(vendor), false);
  assert.equal(/<\/script/i.test(adapter), false);
  assert.equal(vendor.includes('sourceMappingURL'), false);
});

test('renderer template contains one host before its injection marker', () =>
{
  const template = readFileSync(templatePath, 'utf8');
  const host = 'id="baspark-fx-host"';
  const marker = '<!-- BASPARK_RENDERER_SCRIPTS -->';

  assert.equal(template.split(host).length - 1, 1);
  assert.equal(template.split(marker).length - 1, 1);
  assert.ok(template.indexOf(host) < template.indexOf(marker));
});
