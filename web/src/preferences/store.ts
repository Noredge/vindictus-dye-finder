import { packed, rgbText, type Rgb } from '../core/types';

export interface TargetSetting { Color: Rgb; Enabled: boolean; Name: string | null }
export interface Preferences { SchemaVersion: number; Targets: TargetSetting[]; Threshold: number; PreferStable: boolean }
export const preferenceKey = 'vindictus-dye-finder.preferences.v1';
export const presetNames = new Map([[0xe6e6e6, 'White'], [0x191919, 'Black'], [0xc88223, 'Gold'], [0x8c2423, 'Deep Red'], [0xb87d2e, 'Amber'], [0xab2826, 'Red']]);
export const displayColor = (c: Rgb, name?: string | null) => name?.trim() || presetNames.get(packed(c)) || `RGB ${rgbText(c)}`;
export const defaultPreferences = (): Preferences => ({ SchemaVersion: 1, Targets: [...presetNames.keys()].map(n => ({ Color: { R: n >>> 16, G: (n >>> 8) & 255, B: n & 255 }, Enabled: true, Name: null })), Threshold: 15, PreferStable: false });
export function validatePreferences(value: unknown): Preferences {
  const p = value as Preferences;
  if (!p || p.SchemaVersion !== 1 || !Array.isArray(p.Targets) || p.Targets.length > 30 || !Number.isFinite(p.Threshold) || p.Threshold < 1 || p.Threshold > 40 || typeof p.PreferStable !== 'boolean') throw new Error('Unsupported settings. Use a schema 1 settings JSON with tolerance 1–40.');
  const Targets = p.Targets.map(t => {
    if (!t || !t.Color || ![t.Color.R, t.Color.G, t.Color.B].every(v => Number.isInteger(v) && v >= 0 && v <= 255) || typeof t.Enabled !== 'boolean' || !(t.Name == null || (typeof t.Name === 'string' && t.Name.length <= 40))) throw new Error('Invalid RGB, color name or enabled setting.');
    return { Color: { R: t.Color.R, G: t.Color.G, B: t.Color.B }, Enabled: t.Enabled, Name: t.Name ?? null };
  });
  if (new Set(Targets.map(t => packed(t.Color))).size !== Targets.length) throw new Error('Settings contain duplicate colors.');
  return { SchemaVersion: 1, Targets, Threshold: p.Threshold, PreferStable: p.PreferStable };
}
export function loadPreferences(storage: Pick<Storage, 'getItem'>) {
  try { const saved = storage.getItem(preferenceKey); return { value: saved ? validatePreferences(JSON.parse(saved)) : defaultPreferences(), writable: true, warning: '' }; }
  catch { return { value: defaultPreferences(), writable: false, warning: 'Saved settings are unavailable or unreadable. Defaults are in use; existing data is preserved. You can still use this page and export settings.' }; }
}
