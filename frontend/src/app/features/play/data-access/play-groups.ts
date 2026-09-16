import { inject, Injectable, InjectionToken, Signal, signal } from '@angular/core';
import { PlayHostStorage, playerNameKey, record, savedName, uniqueNames, validId, validName, validTime } from './play-host-storage';

export interface LocalPlayPlayer { id: string; displayName: string }
export interface PlayGroup { id: string; name: string; players: LocalPlayPlayer[]; createdAt: number; updatedAt: number }
export interface PlayGroupStore {
  readonly items: Signal<readonly PlayGroup[]>;
  save(name: string, players: readonly string[], id?: string): Promise<PlayGroup>;
  remove(id: string): Promise<void>;
}
export const PLAY_GROUPS = new InjectionToken<PlayGroupStore>('Play groups', {
  providedIn: 'root', factory: () => inject(BrowserPlayGroups),
});
export const GROUPS_KEY = 'kitchain.play.groups.v1';
@Injectable({ providedIn: 'root' })
export class BrowserPlayGroups implements PlayGroupStore {
  private readonly storage = inject(PlayHostStorage);
  private readonly state = signal(this.load());
  readonly items = this.state.asReadonly();
  private load() {
    const ids = new Set<string>(), names = new Set<string>();
    return this.storage.read<PlayGroup>(GROUPS_KEY, (value) => {
      if (!record(value) || !validId(value['id']) || !validName(value['name'], 60) ||
        !validTime(value['createdAt']) || !validTime(value['updatedAt']) || !Array.isArray(value['players'])) return null;
      const name = value['name'].trim();
      if (ids.has(value['id']) || names.has(playerNameKey(name))) return null;
      const memberIds = new Set<string>(), memberNames = new Set<string>();
      const players: LocalPlayPlayer[] = [];
      for (const entry of value['players'].slice(0, 100)) {
        if (!record(entry) || !validId(entry['id']) || !validName(entry['displayName'])) continue;
        const key = playerNameKey(entry['displayName']);
        if (memberIds.has(entry['id']) || memberNames.has(key)) continue;
        memberIds.add(entry['id']); memberNames.add(key);
        players.push({ id: entry['id'], displayName: entry['displayName'].trim() });
      }
      if (!players.length) return null;
      ids.add(value['id']); names.add(playerNameKey(name));
      return { id: value['id'], name, players, createdAt: value['createdAt'], updatedAt: value['updatedAt'] };
    }, 30);
  }
  async save(name: string, names: readonly string[], id?: string) {
    const items = this.load(), previous = items.find((item) => item.id === id);
    if (id && !previous) throw new Error('This group is no longer saved.');
    if (!id && items.length >= 30) throw new Error('You can save up to 30 groups. Delete one first.');
    if (!names.length || names.length > 100 || names.some((name) => !validName(name)))
      throw new Error('Use 1–100 player names, each 1–80 characters.');
    const group: PlayGroup = { id: id ?? crypto.randomUUID(), name: savedName(name, items, id),
      createdAt: previous?.createdAt ?? Date.now(), updatedAt: Date.now(),
      players: uniqueNames(names).map((displayName) => ({ displayName,
        id: previous?.players.find((p) => playerNameKey(p.displayName) === playerNameKey(displayName))?.id ?? crypto.randomUUID() })) };
    const next = [...items.filter((item) => item.id !== group.id), group].sort((a, b) => a.name.localeCompare(b.name) || a.id.localeCompare(b.id));
    this.storage.write(GROUPS_KEY, next); this.state.set(next); return group;
  }
  async remove(id: string) {
    const next = this.load().filter((item) => item.id !== id);
    this.storage.write(GROUPS_KEY, next); this.state.set(next);
  }
}
