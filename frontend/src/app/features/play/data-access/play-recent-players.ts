import { inject, Injectable, InjectionToken, Signal, signal } from '@angular/core';
import { LocalPlayPlayer } from './play-groups';
import { PlayHostStorage, playerNameKey, record, validId, validName, validTime } from './play-host-storage';

export interface RecentPlayPlayer extends LocalPlayPlayer { usedAt: number }
export interface RecentPlayerStore {
  readonly items: Signal<readonly RecentPlayPlayer[]>;
  remember(name: string): Promise<void>;
  remove(id: string): Promise<void>;
  clear(): Promise<void>;
}
export const PLAY_RECENT_PLAYERS = new InjectionToken<RecentPlayerStore>('Recent Play players', {
  providedIn: 'root', factory: () => inject(BrowserRecentPlayers),
});
export const RECENT_PLAYERS_KEY = 'kitchain.play.recent-players.v1';
@Injectable({ providedIn: 'root' })
export class BrowserRecentPlayers implements RecentPlayerStore {
  private readonly storage = inject(PlayHostStorage);
  private readonly state = signal(this.load());
  readonly items = this.state.asReadonly();
  private load() {
    const seen = new Set<string>(), ids = new Set<string>();
    return this.storage.read<RecentPlayPlayer>(RECENT_PLAYERS_KEY, (value) => {
      if (!record(value) || !validId(value['id']) || !validName(value['displayName']) || !validTime(value['usedAt'])) return null;
      return { id: value['id'], displayName: value['displayName'].trim(), usedAt: value['usedAt'] };
    }, 1000).sort((a, b) => b.usedAt - a.usedAt || a.id.localeCompare(b.id)).filter((p) => {
      const key = playerNameKey(p.displayName);
      if (seen.has(key) || ids.has(p.id)) return false;
      seen.add(key); ids.add(p.id); return true;
    }).slice(0, 50);
  }
  async remember(name: string) {
    if (!validName(name)) return;
    const items = this.load(), key = playerNameKey(name);
    const previous = items.find((p) => playerNameKey(p.displayName) === key);
    const usedAt = Math.max(Date.now(), (items[0]?.usedAt ?? 0) + 1);
    const next = [{ id: previous?.id ?? crypto.randomUUID(), displayName: name.trim(), usedAt },
      ...items.filter((p) => playerNameKey(p.displayName) !== key)].slice(0, 50);
    this.storage.write(RECENT_PLAYERS_KEY, next); this.state.set(next);
  }
  async remove(id: string) {
    const next = this.load().filter((p) => p.id !== id);
    this.storage.write(RECENT_PLAYERS_KEY, next); this.state.set(next);
  }
  async clear() { this.storage.write(RECENT_PLAYERS_KEY, []); this.state.set([]); }
}
