import { zipSync, strToU8 } from 'fflate';
import type { Geometry, Point, Rgb, SearchOptions, Suggestion } from '../core/types';
import { rgbText } from '../core/types';

export const webVersion = '0.1.0';
export const baselineVersion = '0.3.4';
export const sampler = 'browser-srgb-v1';
export type SourceKind = 'none' | 'user-image' | 'bundled-real-demo';
export type Outcome = 'MatchedHere' | 'FoundNearby' | 'NotUseful';
export type ProblemStage = 'NoScreenshot' | 'ScreenshotUnreadable' | 'BoardNotDetected' | 'NoMatches' | 'Other';
export const outcomeLabel: Record<Outcome, string> = { MatchedHere: 'Matched here', FoundNearby: 'Found nearby', NotUseful: 'Not useful' };
export interface AnalysisSnapshot { id: string; session: string; sourceKind: SourceKind; sourceSize: Point; geometry: Geometry; options: SearchOptions; suggestion: Suggestion; spot: number; point: number }
export interface FeedbackInput { notes: string; settings: string; outcome: Outcome; actual: Rgb[] | null; verified: boolean }
const metadata = { WebVersion: webVersion, Platform: 'web', SamplingMethod: sampler, DesktopAlgorithmBaseline: baselineVersion };
const relationship = 'user-selected-final-state; coordinates and RGB not automatically verified';
export function makeTrial(snapshot: AnalysisSnapshot, input: FeedbackInput, boardHash: string, hasEvidence: boolean) {
  if (!outcomeLabel[input.outcome] || input.notes.length > 3000 || input.settings.length > 1500 || snapshot.geometry.Markers.length !== 6 || snapshot.suggestion.Slots.length !== 6 || snapshot.sourceKind === 'none') throw new Error('Incomplete trial feedback.');
  if (input.actual && (input.outcome !== 'MatchedHere' || !input.verified || input.actual.length !== 6 || input.actual.some(c => ![c.R, c.G, c.B].every(v => Number.isInteger(v) && v >= 0 && v <= 255)))) throw new Error('Six verified RGB values are only allowed at the original suggested position with Matched here.');
  return {
    SchemaVersion: 2, RecordId: crypto.randomUUID(), AnalysisId: snapshot.id, SessionId: snapshot.session,
    AppVersion: webVersion, RankerVersion: 'ranker-v1', Outcome: input.outcome,
    SuggestedSpot: snapshot.spot, InspectedPoint: snapshot.point, SourceImageSize: snapshot.sourceSize,
    BoardSha256: boardHash, ResultImage: hasEvidence ? 'result-crop.png' : null,
    ResultImageRelationship: hasEvidence ? relationship : 'not-provided', ...metadata,
    Recommendation: {
      SchemaVersion: 1, CreatedUtc: new Date().toISOString(), Sampler: sampler,
      PanelImage: 'board.png', PanelSize: { X: snapshot.geometry.Panel.Width, Y: snapshot.geometry.Panel.Height },
      OriginalMarkers: snapshot.geometry.Markers, GeometrySource: snapshot.geometry.Source,
      Targets: snapshot.options.Targets, Threshold: snapshot.options.Threshold, PreferStable: snapshot.options.PreferStable,
      Suggestion: snapshot.suggestion, ActualGameRgb: input.actual, ActualValuesVerifiedAtSuggestedPosition: !!input.actual && input.verified,
      Usefulness: outcomeLabel[input.outcome], RenderingSettings: input.settings,
      MaterialAndAppearanceNotes: input.notes, SourceKind: snapshot.sourceKind,
    },
  };
}
export function makeProblem(session: string, stage: ProblemStage, targets: Rgb[], tolerance: number, sourceKind: SourceKind, sourceSize: Point | null, notes: string, hasEvidence: boolean) {
  if (notes.length > 3000 || (sourceKind === 'none' && sourceSize !== null) || !Number.isFinite(tolerance) || tolerance < 1 || tolerance > 40) throw new Error('Incomplete problem report.');
  return {
    SchemaVersion: 1, RecordType: 'problem-feedback', RecordId: crypto.randomUUID(), CreatedUtc: new Date().toISOString(),
    SessionId: session, AppVersion: webVersion, Stage: stage, Targets: targets, ColorTolerance: tolerance,
    SourceKind: sourceKind, SourceImageSize: sourceSize, Notes: notes,
    ResultImage: hasEvidence ? 'screenshot-crop.png' : null,
    ResultImageRelationship: hasEvidence ? 'user-selected and reviewed crop; contents and RGB not automatically verified' : 'not-provided', ...metadata,
  };
}
export type TrialRecord = ReturnType<typeof makeTrial>;
export type ProblemRecord = ReturnType<typeof makeProblem>;
export function feedbackZip(record: TrialRecord | ProblemRecord, board?: Uint8Array, evidence?: Uint8Array): Uint8Array {
  const files: Record<string, Uint8Array> = { 'feedback.json': strToU8(JSON.stringify(record, null, 2)) };
  let summary = `Vindictus Dye Finder — ${'RecordType' in record ? 'problem report' : 'trial feedback'}\nWeb: ${webVersion} | Sampler: ${sampler} | Ranker: ranker-v1\n`;
  const source = 'Recommendation' in record ? record.Recommendation.SourceKind : record.SourceKind;
  summary += `Source: ${source}\n`;
  if (source === 'bundled-real-demo') summary += 'Built-in example: do not count this as an independent live-game trial.\n';
  if ('Recommendation' in record) {
    if (!board) throw new Error('The trial board crop is required.');
    files['board.png'] = board;
    const r = record.Recommendation;
    summary += `Outcome: ${r.Usefulness}\nSuggested spot: ${record.SuggestedSpot} | Point inspected: ${record.InspectedPoint}\nTargets: ${r.Targets.map(rgbText).join(' / ')}\nColor tolerance: ${r.Threshold}\n\nPoint | Estimated RGB | Verified game RGB at suggested position\n`;
    for (const slot of r.Suggestion.Slots) summary += `${slot.Index} | ${rgbText(slot.Color)} | ${r.ActualGameRgb ? rgbText(r.ActualGameRgb[slot.Index - 1]) : 'not supplied'}\n`;
    summary += `\nSettings: ${r.RenderingSettings}\nNotes: ${r.MaterialAndAppearanceNotes}\nMatched here describes usefulness, not proof of exact RGB equality. Nearby results never label the original recommendation.\n`;
  } else {
    if (board) throw new Error('Problem reports cannot include an automatic board attachment.');
    summary += `Problem: ${record.Stage}\nChosen targets: ${record.Targets.map(rgbText).join(' / ') || 'none'}\nColor tolerance: ${record.ColorTolerance}\nNotes: ${record.Notes}\nThis report contains no recommendation or verified game RGB. It is not a calibration sample.\n`;
  }
  if (evidence && record.ResultImage) files[record.ResultImage] = evidence;
  if (Boolean(evidence) !== Boolean(record.ResultImage)) throw new Error('The crop and feedback record do not agree.');
  summary += `\nScreenshot: ${record.ResultImageRelationship}\nReview this ZIP before sharing. Nothing is uploaded automatically. Only the stated crops are included; notes are included as entered.\n`;
  files['summary.txt'] = strToU8(summary);
  return zipSync(files, { level: 6 });
}
export async function sha256(bytes: Uint8Array): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', new Uint8Array(bytes));
  return [...new Uint8Array(digest)].map(n => n.toString(16).padStart(2, '0')).join('');
}
