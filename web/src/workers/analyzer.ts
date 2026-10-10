import { detectGeometry } from '../core/geometry';
import { analyze } from '../core/search';
import type { Geometry, PixelImage, SearchOptions } from '../core/types';

self.onmessage = (event: MessageEvent<{ id: number; kind: 'detect' | 'search'; image: PixelImage; geometry?: Geometry; options?: SearchOptions }>) => {
  const { id, kind, image, geometry, options } = event.data;
  try {
    const start = performance.now();
    const result = kind === 'detect' ? detectGeometry(image) : analyze(image, geometry!, options!);
    self.postMessage({ id, kind, result, elapsed: performance.now() - start });
  } catch (error) { self.postMessage({ id, kind, error: error instanceof Error ? error.message : 'Analysis failed.' }); }
};
