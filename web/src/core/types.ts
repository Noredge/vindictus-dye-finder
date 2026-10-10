export interface Rgb { R: number; G: number; B: number }
export interface Lab { L: number; A: number; B: number }
export interface Point { X: number; Y: number }
export interface Rect extends Point { Width: number; Height: number }
export interface PixelImage { Width: number; Height: number; Pixels: Uint8Array }
export interface Geometry { Panel: Rect; Markers: Point[]; Source: string; Confidence: number }
export interface SearchOptions { Targets: Rgb[]; Threshold: number; PreferStable: boolean; MaxResults: number }
export interface SlotEstimate { Index: number; Point: Point; Color: Rgb; NearestTarget: Rgb; Difference: number }
export interface Suggestion { Anchor: Point; Slots: SlotEstimate[]; Matches: number; Quality: number; Stability: number }
export const rgbText = (c: Rgb) => `${c.R}, ${c.G}, ${c.B}`;
export const rgbCss = (c: Rgb) => `rgb(${rgbText(c)})`;
export const packed = (c: Rgb) => (c.R << 16) | (c.G << 8) | c.B;
export const unpack = (n: number): Rgb => ({ R: n >>> 16, G: (n >>> 8) & 255, B: n & 255 });
export function roundEven(n: number): number {
  const lower = Math.floor(n), fraction = n - lower;
  return fraction === 0.5 ? (lower % 2 === 0 ? lower : lower + 1) : Math.round(n);
}
export function parseRgb(text: string): Rgb | null {
  const parts = text.trim().split(/[ ,，;\t]+/);
  if (parts.length !== 3 || parts.some(p => !/^\+?\d+$/.test(p) || Number(p) > 255)) return null;
  return { R: Number(parts[0]), G: Number(parts[1]), B: Number(parts[2]) };
}
export function pixelPacked(image: PixelImage, x: number, y: number): number {
  const i = (y * image.Width + x) * 3;
  return (image.Pixels[i] << 16) | (image.Pixels[i + 1] << 8) | image.Pixels[i + 2];
}
export function cropPixels(image: PixelImage, rect: Rect): PixelImage {
  if (![rect.X, rect.Y, rect.Width, rect.Height].every(Number.isInteger) || rect.Width < 1 || rect.Height < 1 || rect.X < 0 || rect.Y < 0 || rect.X + rect.Width > image.Width || rect.Y + rect.Height > image.Height) throw new Error('The selected area is outside the image.');
  const Pixels = new Uint8Array(rect.Width * rect.Height * 3);
  for (let y = 0; y < rect.Height; y++) {
    const start = ((y + rect.Y) * image.Width + rect.X) * 3;
    Pixels.set(image.Pixels.subarray(start, start + rect.Width * 3), y * rect.Width * 3);
  }
  return { Width: rect.Width, Height: rect.Height, Pixels };
}
