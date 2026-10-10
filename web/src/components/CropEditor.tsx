import { useState } from 'react';
import { BoardView } from './BoardView';
import { cropPixels, type PixelImage, type Rect } from '../core/types';
import { pixelCanvas, type DecodedImage } from '../image/files';

export function CropEditor({ image, onUse, onCancel }: { image: DecodedImage; onUse: (image: PixelImage) => void; onCancel: () => void }) {
  const [crop, setCrop] = useState<Rect | null>(null);
  const view = { X: 0, Y: 0, Width: image.pixels.Width, Height: image.pixels.Height };
  const selected = crop ? cropPixels(image.pixels, crop) : null;
  return <section className="crop-editor"><h3>Crop & review</h3><p>Drag around the dye panel and its RGB labels. Leave out chat, names and character views.</p>
    <BoardView image={image} view={view} geometry={null} active={1} mode="rect" onPoint={() => {}} onRect={setCrop} onMark={() => {}} />
    {selected && <div className="crop-review"><p>Only this {selected.Width} × {selected.Height} crop will be included.</p><CropCanvas pixels={selected} /></div>}
    <div className="actions"><button type="button" onClick={onCancel}>Cancel crop</button><button type="button" className="primary" disabled={!selected || selected.Width * selected.Height >= image.pixels.Width * image.pixels.Height} onClick={() => selected && onUse(selected)}>Use this crop</button></div>
    {!selected && <small>Select an area at least 16 × 16 pixels.</small>}
  </section>;
}
export function CropCanvas({ pixels }: { pixels: PixelImage }) {
  return <canvas className="crop-thumbnail" ref={c => { if (c) { c.width = pixels.Width; c.height = pixels.Height; c.getContext('2d')!.drawImage(pixelCanvas(pixels), 0, 0); } }} aria-label="Reviewed screenshot crop" />;
}
