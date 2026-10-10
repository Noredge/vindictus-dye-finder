import { useEffect, useMemo, useRef, useState } from 'react';
import { cropPixels, parseRgb, type PixelImage, type Point, type Rgb } from '../core/types';
import { decodeImage, download, pngBytes, type DecodedImage } from '../image/files';
import { feedbackZip, makeTrial, makeProblem, outcomeLabel, sha256, type AnalysisSnapshot, type Outcome, type ProblemStage, type SourceKind } from '../feedback/package';
import { CropCanvas, CropEditor } from './CropEditor';

export interface FeedbackContext { kind: 'trial' | 'problem'; image: DecodedImage | null; snapshot?: AnalysisSnapshot; session: string; stage: ProblemStage; targets: Rgb[]; tolerance: number; sourceKind: SourceKind; sourceSize: Point | null }
export function FeedbackDialog({ context, onClose, onDownloaded }: { context: FeedbackContext; onClose: () => void; onDownloaded: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null), alive = useRef(true), attachmentRequest = useRef(0);
  const [outcome, setOutcome] = useState<Outcome | ''>(''), [notes, setNotes] = useState(''), [settings, setSettings] = useState('');
  const [actual, setActual] = useState(''), [verified, setVerified] = useState(false);
  const [evidence, setEvidence] = useState<PixelImage | null>(null), [cropSource, setCropSource] = useState<DecodedImage | null>(null);
  const [error, setError] = useState(''), [saving, setSaving] = useState(false);
  useEffect(() => { alive.current = true; const previous = document.activeElement as HTMLElement | null; dialog.current!.showModal(); return () => { alive.current = false; attachmentRequest.current++; previous?.focus(); }; }, []);
  const trial = context.kind === 'trial';
  const board = useMemo(() => trial ? cropPixels(context.image!.pixels, context.snapshot!.geometry.Panel) : null, [context, trial]);
  useEffect(() => () => { if (cropSource) cropSource.canvas.width = cropSource.canvas.height = 0; }, [cropSource]);
  async function attach(file?: File) {
    if (!file) return; const ticket = ++attachmentRequest.current; setError(''); setCropSource(null);
    try { const decoded = await decodeImage(file); if (alive.current && ticket === attachmentRequest.current) setCropSource(decoded); else decoded.canvas.width = decoded.canvas.height = 0; }
    catch (e) { if (alive.current && ticket === attachmentRequest.current) setError((e as Error).message); }
  }
  async function save() {
    setError(''); setSaving(true);
    try {
      let rgb: Rgb[] | null = null;
      if (trial && !outcome) throw new Error('Choose a result before downloading.');
      if (actual.trim()) {
        const parsed = actual.trim().split(/\r?\n/).filter(s => s.trim()).map(parseRgb);
        if (parsed.length !== 6 || parsed.some(c => !c) || !verified || outcome !== 'MatchedHere') throw new Error('Enter six RGB lines and confirm the original suggested position.');
        rgb = parsed as Rgb[];
      }
      const cropBytes = evidence ? await pngBytes(evidence) : undefined;
      let zip: Uint8Array;
      if (trial) {
        const boardBytes = await pngBytes(board!);
        const record = makeTrial(context.snapshot!, { outcome: outcome as Outcome, notes, settings, actual: rgb, verified }, await sha256(boardBytes), !!cropBytes);
        zip = feedbackZip(record, boardBytes, cropBytes);
      } else {
        const record = makeProblem(context.session, context.stage, context.targets, context.tolerance, context.sourceKind, context.sourceSize, notes, !!cropBytes);
        zip = feedbackZip(record, undefined, cropBytes);
      }
      if (!alive.current) return;
      download(zip, `dye-finder-${trial ? 'trial' : 'problem'}-${crypto.randomUUID().slice(0, 8)}.zip`, 'application/zip');
      onDownloaded(); onClose();
    } catch (e) { if (alive.current) setError((e as Error).message); }
    finally { if (alive.current) setSaving(false); }
  }
  return <dialog ref={dialog} className="feedback-dialog" onCancel={e => { e.preventDefault(); onClose(); }} aria-labelledby="feedback-title">
    <div className="dialog-heading"><div><div className="eyebrow">LOCAL FEEDBACK</div><h2 id="feedback-title">{trial ? 'Record Result' : 'Report a Problem'}</h2></div><button aria-label="Close feedback" type="button" onClick={onClose}>×</button></div>
    <div className="dialog-scroll">
      {trial ? <><p>Suggested spot {context.snapshot!.spot} · Point 1 at {context.snapshot!.suggestion.Anchor.X}, {context.snapshot!.suggestion.Anchor.Y}</p>
        <fieldset className="outcomes"><legend>How did this spot work?</legend>{(Object.keys(outcomeLabel) as Outcome[]).map(o => <label key={o}><input type="radio" name="outcome" value={o} checked={outcome === o} onChange={() => { setOutcome(o); if (o !== 'MatchedHere') { setActual(''); setVerified(false); } }} />{outcomeLabel[o]}<small>{o === 'MatchedHere' ? 'Useful at the original suggested position' : o === 'FoundNearby' ? 'Needed a nearby adjustment' : 'No useful color found'}</small></label>)}</fieldset></>
        : <p className="notice">Problem stage: {context.stage.replace(/([a-z])([A-Z])/g, '$1 $2')}. This is a problem report, with no recommendation or verified game RGB.</p>}
      <label className="field">Notes (optional)<textarea aria-label="Feedback notes" maxLength={3000} rows={3} value={notes} onChange={e => setNotes(e.target.value)} /></label>
      <label className="file-button">Add {trial ? 'Result ' : ''}Screenshot (optional)<input type="file" accept="image/png,image/jpeg,image/bmp,image/webp" onChange={e => { void attach(e.target.files?.[0]); e.target.value = ''; }} /></label>
      {cropSource && <CropEditor image={cropSource} onCancel={() => { attachmentRequest.current++; cropSource.canvas.width = cropSource.canvas.height = 0; setCropSource(null); }} onUse={p => { setEvidence(p); cropSource.canvas.width = cropSource.canvas.height = 0; setCropSource(null); }} />}
      {evidence && <div className="evidence"><p>Reviewed crop: {evidence.Width} × {evidence.Height}. Coordinates and RGB remain unverified.</p><CropCanvas pixels={evidence} /><button onClick={() => setEvidence(null)}>Remove screenshot</button></div>}
      {trial && <details><summary>More Details (optional)</summary><p>Only RGB checked at the original suggested position can label this recommendation.</p>
        <label className="field">Six game RGB values, top row then bottom row<textarea aria-label="Verified game RGB" disabled={outcome !== 'MatchedHere'} rows={6} placeholder={'25, 25, 25\n230, 230, 230\n… six lines'} value={actual} onChange={e => setActual(e.target.value)} /></label>
        <label className="checkbox-label"><input type="checkbox" disabled={outcome !== 'MatchedHere'} checked={verified} onChange={e => setVerified(e.target.checked)} />All six values were checked at the original suggested position.</label>
        <label className="field">Game settings (optional)<textarea maxLength={1500} rows={2} value={settings} onChange={e => setSettings(e.target.value)} /></label>
      </details>}
      <details className="package-review"><summary>Review package contents</summary><p><code>feedback.json</code> · <code>summary.txt</code>{trial && <> · <code>board.png</code></>}{evidence && <> · <code>{trial ? 'result-crop.png' : 'screenshot-crop.png'}</code></>}</p>
        {board && <><p>Board crop included with this trial:</p><CropCanvas pixels={board} /></>}
        <p>No source file path is recorded. Notes are included as entered.</p>{context.sourceKind === 'bundled-real-demo' && <p className="notice">Built-in demo: this record is not an independent player trial.</p>}
      </details>
    </div>
    <div className="dialog-footer"><p className="error" role="alert">{error}</p><small>A local ZIP is generated in your browser. Review it before sharing. Nothing uploads automatically.</small>
      <button className="primary" disabled={saving || !!cropSource} onClick={() => void save()}>{saving ? 'Preparing ZIP…' : 'Download Feedback'}</button></div>
  </dialog>;
}
