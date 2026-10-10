import { distance, toLab } from './colors';
import { buildMask } from './geometry';
import { pixelPacked, unpack, type Geometry, type PixelImage, type SearchOptions, type Suggestion } from './types';

export function analyze(image: PixelImage, geometry: Geometry, options: SearchOptions): Suggestion[] {
  if (!options.Targets.length || !Number.isFinite(options.Threshold) || options.Threshold <= 0 || options.Threshold > 100 || options.MaxResults < 1 || options.MaxResults > 20) throw new Error('Select a target and a valid tolerance.');
  if (geometry.Markers.length !== 6 || new Set(geometry.Markers.map(p => `${p.X},${p.Y}`)).size !== 6) throw new Error('Six different sampling points are required.');
  const offsets = geometry.Markers.map(p => ({ X: p.X - geometry.Markers[0].X, Y: p.Y - geometry.Markers[0].Y }));
  const mask = buildMask(geometry, image.Width, image.Height), labs = options.Targets.map(toLab);
  const distances = new Float64Array(image.Width * image.Height), targets = new Uint8Array(distances.length);
  const cache = new Map<number, [number, number]>();
  for (let y = 0; y < image.Height; y++) for (let x = 0; x < image.Width; x++) {
    const color = pixelPacked(image, x, y); let value = cache.get(color);
    if (!value) {
      const lab = toLab(unpack(color)); let min = Number.MAX_VALUE, nearest = 0;
      for (let i = 0; i < labs.length; i++) { const d = distance(lab, labs[i]); if (d < min) { min = d; nearest = i; } }
      value = [min, nearest]; cache.set(color, value);
    }
    distances[y * image.Width + x] = value[0]; targets[y * image.Width + x] = value[1];
  }
  const at = (x: number, y: number): [number, number] | null => {
    let hits = 0, quality = 0;
    for (const p of offsets) {
      const px = x + p.X, py = y + p.Y;
      if (px < 1 || py < 1 || px >= image.Width - 1 || py >= image.Height - 1 || mask[py * image.Width + px]) return null;
      const d = distances[py * image.Width + px]; if (d <= options.Threshold) hits++;
      quality += Math.exp(-((d / options.Threshold) ** 2));
    }
    return [hits, quality / 6];
  };
  const candidates: { x: number; y: number; hits: number; quality: number; stable: number; score: number }[] = [];
  for (let y = 1; y < image.Height - 1; y++) for (let x = 1; x < image.Width - 1; x++) {
    const v = at(x, y); if (!v || v[0] === 0) continue;
    let stable = 0;
    for (const [nx, ny] of [[-1, 0], [1, 0], [0, -1], [0, 1]]) stable += (at(x + nx, y + ny)?.[0] ?? 0) / 6;
    stable /= 4;
    candidates.push({ x, y, hits: v[0], quality: v[1], stable, score: options.PreferStable ? .6 * stable + .4 * v[1] : .85 * v[1] + .15 * stable });
  }
  candidates.sort((a, b) => b.hits - a.hits || b.score - a.score || a.y - b.y || a.x - b.x);
  const result: Suggestion[] = [], separation = Math.max(10, Math.trunc(image.Width / 20));
  for (const c of candidates) {
    if (result.some(r => (r.Anchor.X - c.x) ** 2 + (r.Anchor.Y - c.y) ** 2 < separation ** 2)) continue;
    const Slots = offsets.map((p, i) => {
      const x = c.x + p.X, y = c.y + p.Y, index = y * image.Width + x;
      return { Index: i + 1, Point: { X: x, Y: y }, Color: unpack(pixelPacked(image, x, y)), NearestTarget: options.Targets[targets[index]], Difference: distances[index] };
    });
    result.push({ Anchor: { X: c.x, Y: c.y }, Slots, Matches: c.hits, Quality: c.quality, Stability: c.stable });
    if (result.length >= options.MaxResults) break;
  }
  return result;
}
