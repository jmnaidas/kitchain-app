import { inject, Injectable, InjectionToken } from '@angular/core';
import { catchError, from, map, mergeMap, Observable, of, toArray } from 'rxjs';
import { PlayApi } from './play-api';
import { RecentPlaySessions } from './recent-play-sessions';
import { uniqueNames } from './play-host-storage';

export interface PreviousPlayRoster { code: string; name: string; names: string[]; unavailable: boolean }
export interface PreviousRosterStore {
  load(excludeCode: string): Observable<PreviousPlayRoster[]>;
  forget(code: string): Promise<void>;
}
export const PLAY_PREVIOUS_ROSTERS = new InjectionToken<PreviousRosterStore>('Previous Play rosters', {
  providedIn: 'root', factory: () => inject(RecentSessionRosters),
});
/** Reuse the existing bounded browser history, fetching names from canonical sessions on demand. */
@Injectable({ providedIn: 'root' })
export class RecentSessionRosters implements PreviousRosterStore {
  private readonly recent = inject(RecentPlaySessions);
  private readonly api = inject(PlayApi);
  load(excludeCode: string) {
    return from(this.recent.list().filter((entry) => entry.code !== excludeCode)).pipe(
      mergeMap((entry, index) => this.api.get(entry.code).pipe(
        map((session) => ({ index, code: entry.code, name: session.name,
          names: uniqueNames(session.players.map((p) => p.displayName)), unavailable: false })),
        catchError(() => of({ index, code: entry.code, name: entry.code, names: [], unavailable: true })),
      ), 2),
      toArray(), map((entries) => entries.sort((a, b) => a.index - b.index)),
    );
  }
  async forget(code: string) { this.recent.remove(code); }
}
