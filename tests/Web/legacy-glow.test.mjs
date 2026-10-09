import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import vm from 'node:vm';

test('legacy fallback toggles only trail shadows and retains clicks and core strokes', () =>
{
  const html = readFileSync(new URL('../../src/Web/index.legacy.html', import.meta.url), 'utf8');
  const strokes = [];
  let fills = 0;
  const ctx = new Proxy({}, {get(target, key)
  {
    if (key === 'stroke') return () => strokes.push({width: target.lineWidth, blur: target.shadowBlur, color: target.shadowColor});
    if (key === 'fill') return () => fills++;
    if (key === 'createLinearGradient') return () => ({addColorStop() {}});
    if (!(key in target)) target[key] = () => {};
    return target[key];
  }});
  const context = {
    window: {innerWidth: 800, innerHeight: 600, devicePixelRatio: 1, addEventListener() {}},
    document: {getElementById: () => ({getContext: () => ctx}), createElement: () => ({getContext: () => ctx})},
    performance: {now: () => 0},
    requestAnimationFrame: () => 1,
  };
  vm.runInNewContext(html.match(/<script>([\s\S]*?)<\/script>/)[1], context);
  const spark = context.window.spark;
  spark.trail = [{x: 10, y: 20, life: 1}, {x: 40, y: 30, life: 1}];
  spark.lastPos = {x: 70, y: 20};
  assert.equal(context.window.enableEffectGlow, false);
  for (const enabled of [false, true, false])
  {
    context.window.setEffectGlow(enabled);
    strokes.length = 0;
    spark._updateTrail(0);
    assert.equal(strokes.length, 2);
    assert.ok(strokes.every(s => s.width === 5 && s.blur === (enabled ? 3 : 0)));
    if (!enabled) assert.ok(strokes.every(s => s.color === 'transparent'));
  }
  spark.createEffects(100, 100);
  assert.equal(spark.waves.length, 1);
  assert.equal(spark.sparks.length, 4);
  spark._updateWaves(1);
  spark._updateSparks(1, 1);
  assert.ok(fills > 0);
});
