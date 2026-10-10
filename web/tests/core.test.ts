import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { toLab, distance } from '../src/core/colors';
import { analyze } from '../src/core/search';
import { detectGeometry, buildMask } from '../src/core/geometry';
import { cropPixels, roundEven, parseRgb, type Rgb } from '../src/core/types';

const golden = JSON.parse(readFileSync(new URL('./fixtures/golden.json', import.meta.url), 'utf8'));
const near = (a: number, b: number) => assert.ok(Math.abs(a - b) <= 1e-6, `${a} != ${b}`);
const rgb = (c: Rgb) => ({ R: c.R, G: c.G, B: c.B });
test('10 C# Lab values and 100 C# CIEDE2000 distances agree within 1e-6', () => {
  for (const pair of golden.Labs) { const actual = toLab(pair.Color); for (const field of ['L', 'A', 'B'] as const) near(actual[field], pair.Lab[field]); }
  for (const pair of golden.Distances) near(distance(toLab(pair.A), toLab(pair.B)), pair.Difference);
});
for (const fixture of golden.Cases) test(`C# raw-pixel parity: ${fixture.Name}`, () => {
  const image = { Width: fixture.Width, Height: fixture.Height, Pixels: new Uint8Array(readFileSync(new URL(`./fixtures/${fixture.PixelFile}`, import.meta.url))) };
  const detected = detectGeometry(image);
  if (!fixture.Detected) assert.equal(detected, null);
  else { assert.deepEqual(detected!.Panel, fixture.Detected.Panel); assert.deepEqual(detected!.Markers, fixture.Detected.Markers); near(detected!.Confidence, fixture.Detected.Confidence); }
  const board = cropPixels(image, fixture.Geometry.Panel), found = analyze(board, fixture.Geometry, fixture.Options);
  assert.equal(found.length, fixture.Suggestions.length);
  for (let i = 0; i < found.length; i++) {
    const a = found[i], b = fixture.Suggestions[i];
    assert.deepEqual(a.Anchor, b.Anchor); assert.equal(a.Matches, b.Matches); near(a.Quality, b.Quality); near(a.Stability, b.Stability);
    for (let j = 0; j < 6; j++) {
      assert.deepEqual(a.Slots[j].Point, b.Slots[j].Point); assert.deepEqual(a.Slots[j].Color, rgb(b.Slots[j].Color));
      assert.deepEqual(rgb(a.Slots[j].NearestTarget), rgb(b.Slots[j].NearestTarget)); near(a.Slots[j].Difference, b.Slots[j].Difference);
      const p = a.Slots[j].Point; assert.equal(buildMask(fixture.Geometry, board.Width, board.Height)[p.Y * board.Width + p.X], 0);
    }
  }
});
test('.NET midpoint rounding, RGB validation and crop bounds', () => {
  assert.deepEqual([.5, 1.5, 2.5, -1.5, -2.5].map(roundEven), [0, 2, 2, -2, -2]);
  assert.deepEqual(parseRgb('200，130，35'), { R: 200, G: 130, B: 35 });
  for (const text of ['256 0 0', '-1 0 0', '1.2 2 3', '1e2 2 3', '1 2']) assert.equal(parseRgb(text), null);
  assert.throws(() => cropPixels({ Width: 1, Height: 1, Pixels: new Uint8Array(3) }, { X: -1, Y: 0, Width: 1, Height: 1 }));
});
