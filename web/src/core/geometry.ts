import { cropPixels, pixelPacked, roundEven, type Geometry, type PixelImage, type Point } from './types';

export function buildMask(g: Geometry, width: number, height: number): Uint8Array {
  const mask = new Uint8Array(width * height), radius = Math.max(5, roundEven(5 * g.Panel.Width / 256));
  for (const p of g.Markers) for (let y = Math.max(0, p.Y - radius); y <= Math.min(height - 1, p.Y + radius); y++)
    for (let x = Math.max(0, p.X - radius); x <= Math.min(width - 1, p.X + radius); x++) mask[y * width + x] = 1;
  return mask;
}
interface Box { X: number; Y: number; W: number; H: number; Color: number }
export function detectGeometry(image: PixelImage): Geometry | null {
  let active = new Map<string, Box>(); const boxes: Box[] = [];
  for (let y = 0; y < image.Height; y++) {
    const next = new Map<string, Box>();
    for (let x = 0; x < image.Width;) {
      const start = x, color = pixelPacked(image, x, y);
      while (++x < image.Width && pixelPacked(image, x, y) === color) { /* flat runs */ }
      const w = x - start; if (w < 18 || w > 100) continue;
      const key = `${start},${w},${color}`, previous = active.get(key);
      next.set(key, previous ? { ...previous, H: previous.H + 1 } : { X: start, Y: y, W: w, H: 1, Color: color });
    }
    for (const [key, box] of active) if (!next.has(key) && Math.abs(box.H - box.W) <= 3) boxes.push(box);
    active = next;
  }
  boxes.push(...[...active.values()].filter(b => Math.abs(b.H - b.W) <= 3));
  for (const first of boxes) {
    const scale = first.W / 33, dx = 91 * scale, dy = 53 * scale, tolerance = Math.max(3, 3 * scale);
    const near = (x: number, y: number) => boxes.some(b => Math.abs(b.W - first.W) <= 2 && Math.abs(b.X - x) <= tolerance && Math.abs(b.Y - y) <= tolerance);
    if (!near(first.X + dx, first.Y) || !near(first.X + 2 * dx, first.Y) || !near(first.X, first.Y + dy) || !near(first.X + dx, first.Y + dy) || !near(first.X + 2 * dx, first.Y + dy)) continue;
    const Panel = { X: roundEven(first.X - scale), Y: roundEven(first.Y - 280 * scale), Width: roundEven(256 * scale), Height: roundEven(256 * scale) };
    if (Panel.X < 0 || Panel.Y < 0 || Panel.X + Panel.Width > image.Width || Panel.Y + Panel.Height > image.Height) continue;
    const markers = findMarkers(cropPixels(image, Panel), scale);
    if (markers) return { Panel, Markers: markers.points, Source: 'Board detected · check yellow rings', Confidence: markers.confidence };
  }
  return null;
}
function findMarkers(image: PixelImage, scale: number): { points: Point[]; confidence: number } | null {
  const step = roundEven(40 * scale), size = Math.max(4, roundEven(8 * scale));
  const score = new Float64Array(image.Width * image.Height);
  for (let y = 0; y <= image.Height - size; y++) for (let x = 0; x <= image.Width - size; x++) {
    let arms = 0, corners = 0, na = 0, nc = 0;
    for (let j = 0; j < size; j++) for (let i = 0; i < size; i++) {
      const c = pixelPacked(image, x + i, y + j), yellow = Math.min(c >>> 16, (c >>> 8) & 255) - (c & 255);
      const cross = (i >= Math.trunc(size * 3 / 8) && i < Math.trunc(size * 5 / 8)) || (j >= Math.trunc(size * 3 / 8) && j < Math.trunc(size * 5 / 8));
      if (cross) { arms += yellow; na++; } else { corners += yellow; nc++; }
    }
    score[y * image.Width + x] = arms / na - corners / nc;
  }
  let best = -Infinity, bx = 0, by = 0;
  for (let y = Math.trunc(-size / 2); y < image.Height - step; y++) for (let x = Math.trunc(-size / 2); x < image.Width - 2 * step; x++) {
    let sum = 0, count = 0;
    for (let row = 0; row < 2; row++) for (let col = 0; col < 3; col++) {
      const px = x + col * step, py = y + row * step;
      if (px < 0 || py < 0 || px > image.Width - size || py > image.Height - size) continue;
      sum += score[py * image.Width + px]; count++;
    }
    if (count < 2) continue;
    const value = sum / count; if (value > best) { best = value; bx = x; by = y; }
  }
  if (best < 8) return null;
  const points: Point[] = [];
  for (let row = 0; row < 2; row++) for (let col = 0; col < 3; col++) points.push({ X: bx + Math.trunc(size / 2) + col * step, Y: by + Math.trunc(size / 2) + row * step });
  return { points, confidence: Math.min(1, Math.max(0, best / 50)) };
}
