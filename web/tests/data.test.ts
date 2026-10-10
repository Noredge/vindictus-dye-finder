import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { unzipSync, strFromU8 } from 'fflate';
import { defaultPreferences, displayColor, loadPreferences, validatePreferences } from '../src/preferences/store';
import { compositeRgba } from '../src/image/files';
import { makeProblem, makeTrial, feedbackZip, sha256, type AnalysisSnapshot } from '../src/feedback/package';

const golden = JSON.parse(readFileSync(new URL('./fixtures/golden.json', import.meta.url), 'utf8'));
const fixture = golden.Cases[0];
const snapshot: AnalysisSnapshot = { id: crypto.randomUUID(), session: crypto.randomUUID(), sourceKind: 'bundled-real-demo', sourceSize: { X: 270, Y: 375 }, geometry: fixture.Geometry, options: fixture.Options, suggestion: fixture.Suggestions[0], spot: 1, point: 2 };
const input = { notes: 'Trial notes', settings: '', outcome: 'MatchedHere' as const, actual: null, verified: false };
const output = new URL('../../.work/web-feedback-compat/', import.meta.url); mkdirSync(output, { recursive: true });

test('desktop settings import, RGB fallback, empty target lists and schema validation', () => {
  assert.equal(validatePreferences({ SchemaVersion: 1, Targets: [{ Color: { R: 153, G: 153, B: 255 }, Enabled: true }], Threshold: 5, PreferStable: false }).Targets[0].Name, null);
  assert.equal(displayColor({ R: 153, G: 153, B: 255 }), 'RGB 153, 153, 255');
  assert.equal(displayColor({ R: 230, G: 230, B: 230 }, ' Snow '), 'Snow');
  assert.equal(validatePreferences({ ...defaultPreferences(), Targets: [] }).Targets.length, 0);
  for (const bad of [{ SchemaVersion: 2 }, { Threshold: NaN }, { Threshold: 41 }, { Targets: [null] }, { Targets: Array(31).fill(defaultPreferences().Targets[0]) }, { Targets: [defaultPreferences().Targets[0], defaultPreferences().Targets[0]] }, { Targets: [{ ...defaultPreferences().Targets[0], Name: 'x'.repeat(41) }] }]) assert.throws(() => validatePreferences({ ...defaultPreferences(), ...bad }));
});
test('corrupt or inaccessible storage is preserved and page remains usable', () => {
  const corrupt = loadPreferences({ getItem: () => '{broken' }); assert.equal(corrupt.writable, false); assert.equal(corrupt.value.Threshold, 15); assert.ok(corrupt.warning);
  assert.equal(loadPreferences({ getItem: () => { throw new Error('denied'); } }).writable, false);
});
test('transparent RGBA explicitly composites onto black with midpoint-to-even rounding', () => {
  assert.deepEqual([...compositeRgba(new Uint8ClampedArray([255, 1, 200, 0, 255, 1, 200, 255, 255, 128, 10, 128]))], [0, 0, 0, 255, 1, 200, 128, 64, 5]);
});
test('trial ZIP allowlist, original analysis, demo distinction and PNG-byte hash', async () => {
  const bytes = new Uint8Array(readFileSync(new URL('./fixtures/demo-board.png', import.meta.url)));
  const hash = await sha256(bytes), record = makeTrial(snapshot, input, hash, false);
  const zip = unzipSync(feedbackZip(record, bytes));
  assert.deepEqual(Object.keys(zip).sort(), ['board.png', 'feedback.json', 'summary.txt']);
  const saved = JSON.parse(strFromU8(zip['feedback.json']));
  assert.equal(saved.AnalysisId, snapshot.id); assert.equal(saved.InspectedPoint, 2); assert.equal(saved.Recommendation.SourceKind, 'bundled-real-demo');
  assert.equal(saved.Recommendation.ActualGameRgb, null); assert.equal(saved.Recommendation.ActualValuesVerifiedAtSuggestedPosition, false); assert.equal(saved.BoardSha256, hash);
  assert.match(strFromU8(zip['summary.txt']), /not.*independent live-game trial/);
  writeFileSync(new URL('trial.json', output), JSON.stringify(record));
});
test('nearby and not useful outcomes reject game RGB labels; Matched here needs six confirmed values', () => {
  const actual = Array(6).fill({ R: 25, G: 25, B: 25 });
  for (const outcome of ['FoundNearby', 'NotUseful'] as const) {
    assert.throws(() => makeTrial(snapshot, { ...input, outcome, actual, verified: true }, 'a'.repeat(64), false));
    const r = makeTrial(snapshot, { ...input, outcome }, 'a'.repeat(64), false); assert.equal(r.Recommendation.ActualGameRgb, null);
    writeFileSync(new URL(`${outcome}.json`, output), JSON.stringify(r));
  }
  assert.throws(() => makeTrial(snapshot, { ...input, actual }, 'a'.repeat(64), false));
  assert.throws(() => makeTrial(snapshot, { ...input, actual: actual.slice(1), verified: true }, 'a'.repeat(64), false));
  assert.deepEqual(makeTrial(snapshot, { ...input, actual, verified: true }, 'a'.repeat(64), false).Recommendation.ActualGameRgb, actual);
});
test('problem ZIP is independent, contains no board or suggestion, optional crop relationship remains unverified', () => {
  const record = makeProblem(snapshot.session, 'NoScreenshot', [], 15, 'none', null, 'No screenshot', false);
  const zip = unzipSync(feedbackZip(record)); assert.deepEqual(Object.keys(zip).sort(), ['feedback.json', 'summary.txt']);
  assert.equal('Recommendation' in record, false); assert.equal('ActualGameRgb' in record, false); assert.equal('BoardSha256' in record, false);
  assert.throws(() => feedbackZip(record, new Uint8Array([1])));
  const attached = makeProblem(snapshot.session, 'ScreenshotUnreadable', [], 15, 'user-image', null, '', true);
  const withCrop = unzipSync(feedbackZip(attached, undefined, new Uint8Array([1, 2])));
  assert.deepEqual(Object.keys(withCrop).sort(), ['feedback.json', 'screenshot-crop.png', 'summary.txt']); assert.match(attached.ResultImageRelationship, /not automatically verified/);
  writeFileSync(new URL('problem.json', output), JSON.stringify(record));
  writeFileSync(new URL('unreadable.json', output), JSON.stringify(attached));
});
