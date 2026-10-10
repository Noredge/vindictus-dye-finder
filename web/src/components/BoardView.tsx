import { useEffect, useRef, useState } from 'react';
import type { DecodedImage } from '../image/files';
import type { Geometry, Point, Rect, Suggestion } from '../core/types';

export function BoardView({ image, view, geometry, suggestion, active, mode, onPoint, onRect, onMark }: {
  image: DecodedImage; view: Rect; geometry: Geometry | null; suggestion?: Suggestion; active: number;
  mode: 'view' | 'rect' | 'points'; onPoint: (n: number) => void; onRect: (r: Rect) => void; onMark: (p: Point) => void;
}) {
  const canvas = useRef<HTMLCanvasElement>(null), area = useRef<HTMLDivElement>(null);
  const start = useRef<Point | null>(null); const [selection, setSelection] = useState<Rect | null>(null);
  useEffect(() => {
    const c = canvas.current!; c.width = view.Width; c.height = view.Height;
    c.getContext('2d')!.drawImage(image.canvas, view.X, view.Y, view.Width, view.Height, 0, 0, view.Width, view.Height);
    setSelection(null); start.current = null;
  }, [image, view.X, view.Y, view.Width, view.Height, mode]);
  function coordinate(clientX: number, clientY: number): Point {
    const box = canvas.current!.getBoundingClientRect();
    return { X: Math.min(view.Width - 1, Math.max(0, Math.floor((clientX - box.left) / box.width * view.Width))), Y: Math.min(view.Height - 1, Math.max(0, Math.floor((clientY - box.top) / box.height * view.Height))) };
  }
  const rectangle = (a: Point, b: Point): Rect => ({ X: Math.min(a.X, b.X), Y: Math.min(a.Y, b.Y), Width: Math.abs(a.X - b.X) + 1, Height: Math.abs(a.Y - b.Y) + 1 });
  const position = (p: Point) => ({ left: `${(p.X + .5) / view.Width * 100}%`, top: `${(p.Y + .5) / view.Height * 100}%` });
  return <div ref={area} className={`board-image ${mode !== 'view' ? 'calibrating' : ''}`} style={{ aspectRatio: `${view.Width}/${view.Height}` }}
    onPointerDown={e => { if (mode === 'view' || e.button !== 0) return; const p = coordinate(e.clientX, e.clientY); if (mode === 'points') { onMark(p); return; } start.current = p; e.currentTarget.setPointerCapture(e.pointerId); setSelection(rectangle(p, p)); }}
    onPointerMove={e => { if (start.current) setSelection(rectangle(start.current, coordinate(e.clientX, e.clientY))); }}
    onPointerUp={e => { if (!start.current) return; const r = rectangle(start.current, coordinate(e.clientX, e.clientY)); start.current = null; if (r.Width >= 16 && r.Height >= 16) onRect({ ...r, X: r.X + view.X, Y: r.Y + view.Y }); }}
    onPointerCancel={() => { start.current = null; setSelection(null); }}>
    <canvas ref={canvas} aria-label="Dye board from your screenshot" />
    {geometry && geometry.Markers.map((p, i) => <span key={`original-${i}`} aria-hidden="true" className="original-marker" style={position(p)} />)}
    {mode === 'view' && suggestion?.Slots.map(slot => <button key={slot.Index} className={`point-marker ${active === slot.Index ? 'active' : ''}`} style={position(slot.Point)} aria-label={`Inspect point ${slot.Index}`} aria-pressed={active === slot.Index} onClick={() => onPoint(slot.Index)}>{slot.Index}</button>)}
    {selection && mode === 'rect' && <div className="crop-selection" style={{ left: `${selection.X / view.Width * 100}%`, top: `${selection.Y / view.Height * 100}%`, width: `${selection.Width / view.Width * 100}%`, height: `${selection.Height / view.Height * 100}%` }} />}
  </div>;
}
