import { inject, Injectable, InjectionToken, Signal, signal } from '@angular/core';
import { PlayRotationMode, PlaySessionMode, rotationStyles } from './play.models';
import { PlayHostStorage, playerNameKey, record, savedName, validId, validName } from './play-host-storage';

export interface PlayConfiguration {
  mode: PlaySessionMode;
  rotationMode: PlayRotationMode;
  numberOfCourts: number;
  maximumPlayers: number | null;
}
export interface PlayPreset extends PlayConfiguration { id: string; name: string }
export interface PlayPresetStore {
  readonly items: Signal<readonly PlayPreset[]>;
  save(name: string, configuration: PlayConfiguration, id?: string): Promise<PlayPreset>;
  remove(id: string): Promise<void>;
}
export const PLAY_PRESETS = new InjectionToken<PlayPresetStore>('Play session presets', {
  providedIn: 'root', factory: () => inject(BrowserPlayPresets),
});
export const PRESETS_KEY = 'kitchain.play.presets.v1';
const positive = (n: unknown): n is number => typeof n === 'number' && Number.isInteger(n) && n > 0 && n <= 2147483647;
export function configuration(value: unknown): PlayConfiguration | null {
  if (!record(value) || !['QueueOnly', 'LiveScoring'].includes(String(value['mode'])) ||
    !rotationStyles.some((style) => style.value === value['rotationMode']) || !positive(value['numberOfCourts']) ||
    !(value['maximumPlayers'] === null || positive(value['maximumPlayers']))) return null;
  return { mode: value['mode'] as PlaySessionMode, rotationMode: value['rotationMode'] as PlayRotationMode,
    numberOfCourts: value['numberOfCourts'], maximumPlayers: value['maximumPlayers'] as number | null };
}
@Injectable({ providedIn: 'root' })
export class BrowserPlayPresets implements PlayPresetStore {
  private readonly storage = inject(PlayHostStorage);
  private readonly state = signal(this.load());
  readonly items = this.state.asReadonly();
  private load() {
    const ids = new Set<string>(), names = new Set<string>();
    return this.storage.read<PlayPreset>(PRESETS_KEY, (value) => {
      const config = configuration(value);
      if (!record(value) || !config || !validId(value['id']) || !validName(value['name'], 60)) return null;
      const name = value['name'].trim(), key = playerNameKey(name);
      if (ids.has(value['id']) || names.has(key)) return null;
      ids.add(value['id']); names.add(key);
      return { id: value['id'], name, ...config };
    }, 30);
  }
  async save(name: string, value: PlayConfiguration, id?: string) {
    const items = this.load();
    const config = configuration(value);
    if (!config) throw new Error('Choose valid session settings before saving a preset.');
    if (id && !items.some((item) => item.id === id)) throw new Error('This preset is no longer saved.');
    if (!id && items.length >= 30) throw new Error('You can save up to 30 presets. Delete one first.');
    const preset = { id: id ?? crypto.randomUUID(), name: savedName(name, items, id), ...config };
    const next = [...items.filter((item) => item.id !== preset.id), preset].sort((a, b) => a.name.localeCompare(b.name) || a.id.localeCompare(b.id));
    this.storage.write(PRESETS_KEY, next); this.state.set(next);
    return preset;
  }
  async remove(id: string) {
    const next = this.load().filter((item) => item.id !== id);
    this.storage.write(PRESETS_KEY, next); this.state.set(next);
  }
}
