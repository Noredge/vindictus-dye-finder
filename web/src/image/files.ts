import { cropPixels, roundEven, type PixelImage, type Rect } from '../core/types';

export interface DecodedImage { pixels: PixelImage; canvas: HTMLCanvasElement }
export function compositeRgba(bytes: Uint8ClampedArray): Uint8Array {
  const rgb = new Uint8Array(bytes.length / 4 * 3);
  for (let i = 0, j = 0; i < bytes.length; i += 4, j += 3) {
    const alpha = bytes[i + 3] / 255;
    rgb[j] = roundEven(bytes[i] * alpha); rgb[j + 1] = roundEven(bytes[i + 1] * alpha); rgb[j + 2] = roundEven(bytes[i + 2] * alpha);
  }
  return rgb;
}
export function pixelCanvas(image: PixelImage): HTMLCanvasElement {
  const canvas = document.createElement('canvas'); canvas.width = image.Width; canvas.height = image.Height;
  const ctx = canvas.getContext('2d', { colorSpace: 'srgb' })!;
  const data = ctx.createImageData(image.Width, image.Height);
  for (let i = 0, j = 0; i < image.Pixels.length; i += 3, j += 4) { data.data[j] = image.Pixels[i]; data.data[j + 1] = image.Pixels[i + 1]; data.data[j + 2] = image.Pixels[i + 2]; data.data[j + 3] = 255; }
  ctx.putImageData(data, 0, 0); return canvas;
}
export async function decodeImage(blob: Blob): Promise<DecodedImage> {
  let bitmap: ImageBitmap | undefined;
  try {
    bitmap = await createImageBitmap(blob, { colorSpaceConversion: 'default', premultiplyAlpha: 'none', imageOrientation: 'from-image' });
    if (bitmap.width < 16 || bitmap.height < 16 || bitmap.width * bitmap.height > 24_000_000) throw new Error('Use an image at least 16 × 16 and no larger than 24 megapixels.');
    const scratch = document.createElement('canvas'); scratch.width = bitmap.width; scratch.height = bitmap.height;
    const ctx = scratch.getContext('2d', { colorSpace: 'srgb', willReadFrequently: true });
    if (!ctx) throw new Error('This browser cannot read Canvas pixels.');
    ctx.drawImage(bitmap, 0, 0);
    const data = ctx.getImageData(0, 0, bitmap.width, bitmap.height, { colorSpace: 'srgb' });
    const pixels = { Width: bitmap.width, Height: bitmap.height, Pixels: compositeRgba(data.data) };
    const canvas = pixelCanvas(pixels); scratch.width = scratch.height = 0;
    return { pixels, canvas };
  } catch (error) {
    if (error instanceof Error && error.message.startsWith('Use an image')) throw error;
    throw new Error('Could not read that image. Try an original PNG, JPEG, BMP or WebP screenshot.');
  } finally { bitmap?.close(); }
}
export async function pngBytes(image: PixelImage): Promise<Uint8Array> {
  const canvas = pixelCanvas(image);
  try {
    const blob = await new Promise<Blob>((resolve, reject) => canvas.toBlob(b => b ? resolve(b) : reject(new Error('Could not encode the crop.')), 'image/png'));
    return new Uint8Array(await blob.arrayBuffer());
  } finally { canvas.width = canvas.height = 0; }
}
export const cropImage = (image: DecodedImage, rect: Rect) => cropPixels(image.pixels, rect);
export function download(bytes: Uint8Array | string, filename: string, type: string) {
  const blob = new Blob([typeof bytes === 'string' ? bytes : new Uint8Array(bytes)], { type });
  const url = URL.createObjectURL(blob), link = document.createElement('a'); link.href = url; link.download = filename;
  document.body.append(link); link.click(); link.remove(); window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}
