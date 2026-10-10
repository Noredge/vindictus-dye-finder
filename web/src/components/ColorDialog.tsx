import { useEffect, useRef, useState } from 'react';
import { parseRgb, rgbCss, type Rgb } from '../core/types';

export function ColorDialog({ initial, onSave, onClose }: { initial?: { name: string; color: Rgb }; onSave: (color: Rgb, name: string) => void; onClose: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [text, setText] = useState(initial ? `${initial.color.R}, ${initial.color.G}, ${initial.color.B}` : ''), [name, setName] = useState(initial?.name || ''), [error, setError] = useState('');
  useEffect(() => { const previous = document.activeElement as HTMLElement | null; dialog.current!.showModal(); dialog.current!.querySelector<HTMLInputElement>(initial ? 'input[aria-label="Color name"]' : 'input[aria-label="Color RGB"]')?.focus(); return () => previous?.focus(); }, []);
  const color = parseRgb(text);
  return <dialog ref={dialog} className="color-dialog" onCancel={e => { e.preventDefault(); onClose(); }} aria-labelledby="color-title">
    <form onSubmit={e => { e.preventDefault(); if (!color) { setError('Use three whole numbers from 0 to 255.'); return; } try { onSave(color, name.trim()); } catch (e) { setError((e as Error).message); } }}>
      <div className="dialog-heading"><h2 id="color-title">{initial ? 'Rename Color' : 'Add Color'}</h2><button type="button" aria-label="Close color editor" onClick={onClose}>×</button></div>
      <label className="field">RGB<input autoFocus aria-label="Color RGB" placeholder="153, 153, 255" value={text} readOnly={!!initial} onChange={e => setText(e.target.value)} /></label>
      <label className="field">Name (optional)<input aria-label="Color name" maxLength={40} placeholder="e.g. Lavender" value={name} onChange={e => setName(e.target.value)} /></label>
      <p>{color && <span className="swatch" style={{ background: rgbCss(color) }} />} {name || (color ? `RGB ${text}` : 'Enter a color to preview')}</p>
      <p className="error" role="alert">{error}</p><button className="primary" type="submit">Save Color</button>
    </form>
  </dialog>;
}
