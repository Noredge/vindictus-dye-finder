import { useEffect, useRef } from 'react';
import type { DecodedImage } from '../image/files';
import { rgbText, rgbCss, type Geometry, type SlotEstimate } from '../core/types';
import { displayColor } from '../preferences/store';

export function ZoomPreview({ image, geometry, slot, tolerance, targetName }: { image: DecodedImage; geometry: Geometry; slot: SlotEstimate; tolerance: number; targetName?: string | null }) {
  const canvas = useRef<HTMLCanvasElement>(null);
  useEffect(() => {
    const c = canvas.current!, ctx = c.getContext('2d')!; c.width = c.height = 180;
    ctx.fillStyle = '#0d1118'; ctx.fillRect(0, 0, 180, 180); ctx.imageSmoothingEnabled = false;
    const x = geometry.Panel.X + slot.Point.X, y = geometry.Panel.Y + slot.Point.Y;
    ctx.drawImage(image.canvas, x - 11, y - 11, 23, 23, 0, 0, 180, 180);
    ctx.strokeStyle = '#fff'; ctx.lineWidth = 1; ctx.strokeRect(86.5, 86.5, 7, 7);
    ctx.beginPath(); ctx.moveTo(80, 90); ctx.lineTo(100, 90); ctx.moveTo(90, 80); ctx.lineTo(90, 100); ctx.stroke();
  }, [image, geometry, slot]);
  const close = slot.Difference <= tolerance;
  return <section className="zoom-card" aria-label="Shared point preview">
    <canvas ref={canvas} className="zoom-canvas" aria-label={`Magnified point ${slot.Index}`} />
    <div className="point-detail"><div className="eyebrow">POINT {slot.Index} · {slot.Point.X}, {slot.Point.Y}</div>
      <h3>Estimated RGB</h3><div className="large-rgb"><span className="swatch" style={{ background: rgbCss(slot.Color) }} />{rgbText(slot.Color)}</div>
      <p>Closest target: <strong>{displayColor(slot.NearestTarget, targetName)}</strong><br />RGB {rgbText(slot.NearestTarget)}</p>
      <span className={`match-status ${close ? 'close' : 'outside'}`}>{close ? '✓ Within tolerance' : '△ Outside tolerance'} · ΔE {slot.Difference.toFixed(2)}</span>
      <small>Screenshot estimate. Preview the result in game.</small>
    </div>
  </section>;
}
