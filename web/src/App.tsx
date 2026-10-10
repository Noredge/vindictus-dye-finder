import { useCallback, useEffect, useRef, useState } from 'react';
import { BoardView } from './components/BoardView';
import { ZoomPreview } from './components/ZoomPreview';
import { ColorDialog } from './components/ColorDialog';
import { FeedbackDialog, type FeedbackContext } from './components/FeedbackDialog';
import { decodeImage, download, type DecodedImage } from './image/files';
import { cropPixels, packed, rgbCss, rgbText, type Geometry, type PixelImage, type Point, type Rect, type SearchOptions, type Suggestion } from './core/types';
import { defaultPreferences, displayColor, loadPreferences, preferenceKey, validatePreferences, type Preferences } from './preferences/store';
import { webVersion, type ProblemStage, type SourceKind } from './feedback/package';

function initialPreferences() {
  try { return loadPreferences(localStorage); } catch { return { value: defaultPreferences(), writable: false, warning: 'Browser storage is unavailable. Settings work for this visit; use Export Settings to keep them.' }; }
}
export default function App() {
  const [initial] = useState(initialPreferences), [preferences, setPreferences] = useState(initial.value), [storageWarning, setStorageWarning] = useState(initial.warning);
  const writable = useRef(initial.writable);
  const [image, setImage] = useState<DecodedImage | null>(null), [geometry, setGeometry] = useState<Geometry | null>(null);
  const [sourceKind, setSourceKind] = useState<SourceKind>('none'), [sourceName, setSourceName] = useState('');
  const [suggestions, setSuggestions] = useState<Suggestion[]>([]), [spot, setSpot] = useState(0), [point, setPoint] = useState(1);
  const [mode, setMode] = useState<'view' | 'rect' | 'points'>('view');
  const [status, setStatus] = useState('Open a screenshot or try the real example to get started.');
  const [busy, setBusy] = useState<'decode' | 'detect' | 'search' | null>(null), [searchDone, setSearchDone] = useState(false);
  const [stage, setStage] = useState<ProblemStage>('NoScreenshot'), [elapsed, setElapsed] = useState<number | null>(null);
  const [colorEditor, setColorEditor] = useState<number | 'add' | null>(null), [feedback, setFeedback] = useState<FeedbackContext | null>(null);
  const [updateAvailable, setUpdateAvailable] = useState(false);
  const [session] = useState(() => crypto.randomUUID()), analysisId = useRef('');
  const worker = useRef<{ instance: Worker; kind: 'detect' | 'search' } | null>(null), revision = useRef(0), importTicket = useRef(0);
  const openedImage = useRef<DecodedImage | null>(null), fileInput = useRef<HTMLInputElement>(null), settingsInput = useRef<HTMLInputElement>(null), pointPreview = useRef<HTMLDivElement>(null);
  const targets = preferences.Targets.filter(t => t.Enabled).map(t => t.Color), selected = suggestions[spot];
  const invalidate = useCallback((all = false) => {
    if (worker.current && (all || worker.current.kind === 'search')) { worker.current.instance.terminate(); worker.current = null; revision.current++; setBusy(null); }
    setSuggestions([]); setSearchDone(false); setSpot(0); setPoint(1); setFeedback(null); analysisId.current = ''; setElapsed(null);
  }, []);
  const runWorker = useCallback((kind: 'detect' | 'search', pixels: PixelImage, g: Geometry | undefined, options: SearchOptions | undefined, accept: (result: Geometry | Suggestion[] | null, ms: number) => void) => {
    worker.current?.instance.terminate(); const id = ++revision.current;
    let instance: Worker;
    try { instance = new Worker(new URL('./workers/analyzer.ts', import.meta.url), { type: 'module' }); }
    catch { worker.current = null; setBusy(null); setStage('Other'); setStatus('This browser could not start an analysis worker. Try a current desktop browser.'); return; }
    worker.current = { instance, kind }; setBusy(kind);
    const finish = () => { instance.terminate(); worker.current = null; setBusy(null); };
    instance.onmessage = e => {
      if (worker.current?.instance !== instance || e.data.id !== revision.current) return;
      finish();
      if (e.data.error) { setStatus(e.data.error); setStage(kind === 'detect' ? 'BoardNotDetected' : 'Other'); return; }
      accept(e.data.result, e.data.elapsed);
    };
    instance.onerror = () => { if (worker.current?.instance !== instance) return; finish(); setStatus('Analysis could not complete. Try again or calibrate the board manually.'); setStage('Other'); };
    instance.postMessage({ id, kind, image: pixels, geometry: g, options }, [pixels.Pixels.buffer]);
  }, []);
  const open = useCallback(async (getBlob: () => Promise<Blob> | Blob, kind: SourceKind, name: string) => {
    const ticket = ++importTicket.current; invalidate(true); setBusy('decode'); setImage(null); setGeometry(null); setMode('view'); setSourceKind(kind); setSourceName(name); setStage('NoScreenshot'); setStatus('Opening image…');
    if (openedImage.current) { openedImage.current.canvas.width = openedImage.current.canvas.height = 0; openedImage.current = null; }
    try {
      const decoded = await decodeImage(await getBlob());
      if (ticket !== importTicket.current) { decoded.canvas.width = decoded.canvas.height = 0; return; }
      openedImage.current = decoded; setImage(decoded); setStatus('Detecting the board…'); setStage('BoardNotDetected');
      runWorker('detect', { ...decoded.pixels, Pixels: decoded.pixels.Pixels.slice() }, undefined, undefined, result => {
        const g = result as Geometry | null; setGeometry(g); setStage(g ? 'Other' : 'BoardNotDetected');
        setStatus(g ? 'Board detected. Check the yellow rings, then click Find Matches.' : 'Board not detected. Use Advanced to select the board and mark its six original crosses.');
      });
    } catch (e) { if (ticket !== importTicket.current) return; setBusy(null); setStatus((e as Error).message); setStage('ScreenshotUnreadable'); }
  }, [invalidate, runWorker]);
  useEffect(() => () => { importTicket.current++; revision.current++; worker.current?.instance.terminate(); if (openedImage.current) openedImage.current.canvas.width = openedImage.current.canvas.height = 0; }, []);
  useEffect(() => {
    const controller = new AbortController();
    fetch(`${import.meta.env.BASE_URL}version.json`, { cache: 'no-store', signal: controller.signal }).then(r => r.ok ? r.json() : null).then(v => { if (typeof v?.version === 'string' && v.version !== webVersion) setUpdateAvailable(true); }).catch(() => {});
    return () => controller.abort();
  }, []);
  useEffect(() => {
    const paste = (e: ClipboardEvent) => {
      const target = e.target as HTMLElement;
      if (target.closest('input, textarea, [contenteditable="true"], dialog')) return;
      const file = [...(e.clipboardData?.items ?? [])].find(i => i.type.startsWith('image/'))?.getAsFile();
      if (file) { e.preventDefault(); void open(() => file, 'user-image', 'Pasted image'); }
      else setStatus('No image was found in the paste. Use Open Screenshot if image pasting is unavailable.');
    };
    document.addEventListener('paste', paste); return () => document.removeEventListener('paste', paste);
  }, [open]);
  function update(p: Preferences) {
    invalidate(); setPreferences(p);
    if (stage === 'NoMatches') setStage('Other');
    if (image && geometry) setStatus('Colors or options changed. Click Find Matches to search again.');
    if (writable.current) try { localStorage.setItem(preferenceKey, JSON.stringify(p)); } catch { setStorageWarning('Could not save settings in this browser. You can still use the page and Export Settings.'); }
  }
  async function importSettings(file?: File) {
    if (!file) return;
    try { if (file.size > 65_536) throw new Error('Use a settings file smaller than 64 KB.'); const p = validatePreferences(JSON.parse(await file.text())); writable.current = true; setStorageWarning(''); update(p); setStatus('Settings imported. Click Find Matches to use these colors.'); }
    catch (e) { setStatus(`Settings were not changed. ${(e as Error).message}`); }
  }
  function find() {
    if (!image || !geometry || geometry.Markers.length !== 6 || !targets.length) return;
    invalidate(true); setStatus('Finding matching spots…'); setStage('Other');
    const options = { Targets: structuredClone(targets), Threshold: preferences.Threshold, PreferStable: preferences.PreferStable, MaxResults: 3 };
    runWorker('search', cropPixels(image.pixels, geometry.Panel), geometry, options, (result, ms) => {
      const found = result as Suggestion[]; setSuggestions(found); setSearchDone(true); setElapsed(ms); analysisId.current = crypto.randomUUID();
      setStage(found.length ? 'Other' : 'NoMatches'); setStatus(found.length ? `${found.length} suggested spot${found.length === 1 ? '' : 's'} ready. Preview the result in game.` : 'No matches found. Try a higher tolerance or another color.');
    });
  }
  function selectPoint(n: number) { setPoint(n); if (window.innerHeight < 760) pointPreview.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' }); }
  function selectRect(rect: Rect) { invalidate(true); setGeometry({ Panel: rect, Markers: [], Source: 'Manual calibration', Confidence: 1 }); setMode('points'); setStatus('Click the six original crosses: top row left to right, then bottom row (1 of 6).'); }
  function mark(p: Point) {
    if (!geometry || geometry.Markers.some(m => m.X === p.X && m.Y === p.Y)) { setStatus('Choose six different points.'); return; }
    const Markers = [...geometry.Markers, p]; setGeometry({ ...geometry, Markers });
    if (Markers.length === 6) { setMode('view'); setStage('Other'); setStatus('Six points marked. Check the yellow rings, then click Find Matches.'); }
    else setStatus(`Click the six original crosses: top row left to right, then bottom row (${Markers.length + 1} of 6).`);
  }
  function openFeedback(kind: 'trial' | 'problem') {
    setFeedback({ kind, image, session, stage, targets: structuredClone(targets), tolerance: preferences.Threshold, sourceKind,
      sourceSize: image ? { X: image.pixels.Width, Y: image.pixels.Height } : null,
      snapshot: kind === 'trial' && selected && image && geometry ? { id: analysisId.current, session, sourceKind, sourceSize: { X: image.pixels.Width, Y: image.pixels.Height }, geometry: structuredClone(geometry), options: { Targets: structuredClone(targets), Threshold: preferences.Threshold, PreferStable: preferences.PreferStable, MaxResults: 3 }, suggestion: structuredClone(selected), spot: spot + 1, point } : undefined });
  }
  const view: Rect | null = image ? mode === 'rect' || !geometry ? { X: 0, Y: 0, Width: image.pixels.Width, Height: image.pixels.Height } : geometry.Panel : null;
  const noMatches = searchDone && !suggestions.length;
  return <div className="app-shell" onDragOver={e => { if (e.dataTransfer.types.includes('Files')) e.preventDefault(); }} onDrop={e => { const file = e.dataTransfer.files[0]; if (file) { e.preventDefault(); void open(() => file, 'user-image', 'Opened screenshot'); } }}>
    <header className="app-header"><div className="brand"><img src={`${import.meta.env.BASE_URL}icon.png`} width="42" height="42" alt="" /><div><h1>Dye Finder <span>WEB PREVIEW 0.1</span></h1><p>Open a screenshot. Pick your colors. Find a spot.</p></div></div>
      <div className="header-actions"><button onClick={() => fileInput.current?.click()}>↥ Open Screenshot</button><button onClick={() => void open(async () => { const response = await fetch(`${import.meta.env.BASE_URL}demo.png`); if (!response.ok) throw new Error('Demo unavailable.'); return response.blob(); }, 'bundled-real-demo', 'Real example')}>Try Demo</button></div>
      <input ref={fileInput} className="visually-hidden" type="file" accept="image/png,image/jpeg,image/bmp,image/webp" aria-label="Open screenshot file" onChange={e => { const file = e.target.files?.[0]; if (file) void open(() => file, 'user-image', 'Opened screenshot'); e.target.value = ''; }} />
    </header>
    {storageWarning && <p className="storage-warning" role="status">{storageWarning}</p>}
    {updateAvailable && <p className="storage-warning" role="status">A newer version is available. Export settings if needed; screenshots will need to be reopened. <button onClick={() => window.location.reload()}>Reload Page</button></p>}
    <main className="workspace">
      <aside className="colors-panel panel"><div className="section-heading"><span className="step">1</span><h2>Choose Colors</h2><small>{targets.length} selected</small></div>
        <p className="muted">Which colors are you looking for?</p>
        <div className="color-list">{preferences.Targets.map((t, i) => <div key={packed(t.Color)} className={`color-row ${t.Enabled ? 'enabled' : ''}`}>
          <input aria-label={`Select ${displayColor(t.Color, t.Name)}`} type="checkbox" checked={t.Enabled} onChange={e => update({ ...preferences, Targets: preferences.Targets.map((v, j) => j === i ? { ...v, Enabled: e.target.checked } : v) })} />
          <span className="swatch" style={{ background: rgbCss(t.Color) }} /><button className="color-name" title="Click to rename" onClick={() => setColorEditor(i)}><strong>{displayColor(t.Color, t.Name)}</strong>{!displayColor(t.Color, t.Name).startsWith('RGB ') && <small>RGB {rgbText(t.Color)}</small>}</button>
          <button className="remove-color" aria-label={`Remove ${displayColor(t.Color, t.Name)}`} onClick={() => update({ ...preferences, Targets: preferences.Targets.filter((_, j) => j !== i) })}>×</button>
        </div>)}</div>
        <button className="add-color" disabled={preferences.Targets.length >= 30} onClick={() => setColorEditor('add')}>＋ Add Color</button>
        <div className="tolerance"><label htmlFor="tolerance">Color Tolerance <output>{preferences.Threshold}</output></label><input id="tolerance" type="range" min="1" max="40" step="1" value={preferences.Threshold} onChange={e => update({ ...preferences, Threshold: Number(e.target.value) })} /><div className="range-labels"><small>Closer colors</small><small>More variation</small></div></div>
        <button className="primary find-button" disabled={!image || geometry?.Markers.length !== 6 || !targets.length || !!busy || mode !== 'view'} onClick={find}>⌕ Find Matches</button>
        {busy && <button className="cancel-button" onClick={() => { importTicket.current++; invalidate(true); setBusy(null); setStatus('Analysis cancelled. Click Find Matches to try again, or reopen the screenshot if detection was cancelled.'); }}>Cancel Analysis</button>}
        <details className="advanced"><summary>Advanced</summary><label className="checkbox-label"><input type="checkbox" checked={preferences.PreferStable} onChange={e => update({ ...preferences, PreferStable: e.target.checked })} />Prefer easier alignment</label><p>Favors stable nearby pixels among spots with the same matching-slot count.</p>
          <button disabled={!image || !!busy} onClick={() => { invalidate(true); setGeometry(null); setMode('rect'); setStatus('Drag around the colorful board only, leaving out the border and swatches below.'); }}>Select Board Manually</button>
          <button disabled={!geometry || !!busy} onClick={() => { invalidate(true); setGeometry({ ...geometry!, Markers: [], Source: 'Manual calibration', Confidence: 1 }); setMode('points'); setStatus('Click the six original crosses: top row left to right, then bottom row (1 of 6).'); }}>Mark Six Points</button>
          {mode !== 'view' && <button onClick={() => { setMode('view'); setStatus('Calibration paused. Finish marking six points or reopen the image for automatic detection.'); }}>Stop Calibration</button>}
          <div className="settings-actions"><button onClick={() => download(JSON.stringify(preferences, null, 2), 'dye-finder-settings.json', 'application/json')}>Export Settings</button><button onClick={() => settingsInput.current?.click()}>Import Settings</button></div>
          <input ref={settingsInput} type="file" accept="application/json,.json" className="visually-hidden" aria-label="Import settings file" onChange={e => { void importSettings(e.target.files?.[0]); e.target.value = ''; }} />
        </details>
      </aside>
      <section className="board-column"><section className="panel board-panel"><div className="section-heading"><span className="step">2</span><h2>Dye Board</h2><small>{image ? `${geometry?.Panel.Width || image.pixels.Width} × ${geometry?.Panel.Height || image.pixels.Height}` : 'YOUR SCREENSHOT'}</small></div>
        {image && view ? <><div className="board-meta"><span>{sourceName}</span><span>{geometry?.Markers.length === 6 ? '✓ Six points ready' : mode === 'points' ? `${geometry?.Markers.length || 0} of 6 marked` : 'Manual setup available'}</span></div>
          <BoardView image={image} view={view} geometry={geometry} suggestion={selected} active={point} mode={mode} onPoint={selectPoint} onRect={selectRect} onMark={mark} />
          <div className="legend"><span><i className="original-dot" />Original crosses</span><span><i className="suggested-dot" />Suggested points</span><span><i className="selected-dot" />Selected point</span></div>
        </> : <div className="empty-board"><div className="empty-icon">↥</div><h3>Your next color starts here</h3><p>Drop a screenshot, paste an image with Ctrl+V,<br />or explore the built-in real example.</p><button onClick={() => fileInput.current?.click()}>Open Screenshot</button><small>PNG · JPEG · BMP · WebP / up to 24 MP</small></div>}
        {(mode !== 'view' || busy) && <p className="instruction" role="status">{busy ? <><span className="spinner" />{status}</> : status}</p>}
      </section>
      <div ref={pointPreview}>{image && geometry && selected ? <ZoomPreview image={image} geometry={geometry} slot={selected.Slots[point - 1]} tolerance={preferences.Threshold} targetName={preferences.Targets.find(t => packed(t.Color) === packed(selected.Slots[point - 1].NearestTarget))?.Name} /> : <div className="panel preview-placeholder"><span>＋</span><div><h3>One point. A closer look.</h3><p>Select a suggested numbered point to inspect its RGB and closest target here.</p></div></div>}</div>
      </section>
      <aside className="results-panel panel"><div className="section-heading"><span className="step">3</span><h2>Suggested Spots</h2></div><p className="muted">Find a useful starting position.</p>
        {suggestions.map((suggestion, index) => <button className={`suggestion-card ${spot === index ? 'selected' : ''}`} key={`${suggestion.Anchor.X},${suggestion.Anchor.Y}`} aria-label={`Select suggested spot ${index + 1}`} aria-pressed={spot === index} data-anchor={`${suggestion.Anchor.X},${suggestion.Anchor.Y}`} onClick={() => { setSpot(index); setPoint(1); }}>
          <div className="suggestion-heading"><strong>{index === 0 ? 'Try this first' : `Alternative ${index}`}</strong><span>{index === 0 ? '01' : `0${index + 1}`}</span></div><p className="matches">{suggestion.Matches} of 6 slots close to your colors</p><small className="anchor">Point 1 · {suggestion.Anchor.X}, {suggestion.Anchor.Y}</small>
          <div className="rgb-grid">{suggestion.Slots.map(slot => <div key={slot.Index} className="rgb-slot"><span className="slot-swatch" style={{ background: rgbCss(slot.Color) }}>{slot.Index}</span><span>{rgbText(slot.Color)}</span></div>)}</div><small className="estimate-label">Estimated RGB</small>
        </button>)}
        {!suggestions.length && <div className={`results-empty ${noMatches ? 'notice' : ''}`}>{busy === 'search' ? <><span className="spinner" /><h3>Finding matching spots…</h3><p>Your image is being analyzed in this browser.</p></> : noMatches ? <><span>△</span><h3>No matches found.</h3><p>Try a higher tolerance or another color.</p></> : <><span>⌕</span><h3>Suggestions appear here</h3><p>Choose your colors, check the yellow rings, then click Find Matches.</p></>}</div>}
        <div className="feedback-actions"><button disabled={!selected || !!busy} onClick={() => openFeedback('trial')}>Record Result</button><button className="quiet" disabled={!!busy} onClick={() => openFeedback('problem')}>Report a Problem</button><small>Download a ZIP. Share it only if you choose.</small></div>
        {elapsed !== null && <small className="timing">Search compute: {(elapsed / 1000).toFixed(2)} s · this run</small>}
      </aside>
    </main>
    <div className={`status-bar ${noMatches || stage === 'ScreenshotUnreadable' || (image && !geometry && !busy) ? 'notice' : ''}`} role="status" aria-live="polite">{busy && <span className="spinner" />}{status}</div>
    <footer><span>Your screenshots stay in this browser. RGB values are estimates.</span><span>Web {webVersion} · <a href={`${import.meta.env.BASE_URL}ASSET-NOTICES.md`}>Asset notices</a></span></footer>
    {feedback && <FeedbackDialog context={feedback} onClose={() => setFeedback(null)} onDownloaded={() => setStatus('Download started. Find the ZIP in your browser downloads and review it before sharing.')} />}
    {colorEditor !== null && <ColorDialog initial={colorEditor === 'add' ? undefined : { color: preferences.Targets[colorEditor].Color, name: preferences.Targets[colorEditor].Name || '' }} onClose={() => setColorEditor(null)} onSave={(color, name) => {
      if (colorEditor === 'add' && preferences.Targets.some(t => packed(t.Color) === packed(color))) throw new Error('That RGB color is already in your list. Rename it or enable it there.');
      const Targets = colorEditor === 'add' ? [...preferences.Targets, { Color: color, Name: name || null, Enabled: true }] : preferences.Targets.map((t, i) => i === colorEditor ? { ...t, Name: name || null } : t);
      update({ ...preferences, Targets }); setColorEditor(null);
    }} />}
  </div>;
}
