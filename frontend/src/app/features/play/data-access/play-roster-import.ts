import { inject, Injectable } from '@angular/core';
import { catchError, concat, concatMap, defer, map, Observable, of, switchMap, tap } from 'rxjs';
import { PlayApi } from './play-api';
import { playerNameKey, uniqueNames, validName } from './play-host-storage';
import { PLAY_RECENT_PLAYERS } from './play-recent-players';
import { PlaySession } from './play.models';

export interface RosterImportProgress {
  session?: PlaySession;
  added: number;
  skipped: number;
  remaining: number;
  done: boolean;
  needsRefresh: boolean;
  problem: string;
}
/** Sequential canonical mutations. Unsubscribing cancels all not-yet-started additions. */
@Injectable({ providedIn: 'root' })
export class PlayRosterImport {
  private readonly api = inject(PlayApi);
  private readonly recent = inject(PLAY_RECENT_PLAYERS);
  add(code: string, requested: readonly string[]): Observable<RosterImportProgress> {
    return defer(() => {
      const names = uniqueNames(requested);
      let added = 0, skipped = 0;
      const result = (remaining: number, done: boolean, session?: PlaySession, problem = '', needsRefresh = false): RosterImportProgress =>
        ({ session, added, skipped, remaining, done, problem, needsRefresh });
      if (!requested.length || requested.length > 100 || requested.some((name) => !validName(name)))
        return of(result(requested.length, true, undefined, 'Choose up to 100 valid player names.'));
      const step = (index: number, session: PlaySession): Observable<RosterImportProgress> => {
        if (index === names.length) return of(result(0, true, session));
        if (session.status !== 'Draft') return of(result(names.length - index, true, session, 'Import stopped: this session is no longer a Draft.'));
        const name = names[index];
        if (session.players.some((p) => playerNameKey(p.displayName) === playerNameKey(name))) {
          skipped++;
          return step(index + 1, session);
        }
        let confirmed = false;
        return this.api.addPlayer(code, name).pipe(
          tap((player) => {
            confirmed = true; added++;
            void this.recent.remember(player.displayName).catch(() => undefined);
          }),
          switchMap(() => this.api.get(code)),
          concatMap((canonical) => concat(of(result(names.length - index - 1, false, canonical)), step(index + 1, canonical))),
          catchError(() => {
            const remaining = names.length - index - (confirmed ? 1 : 0);
            const problem = confirmed
              ? `${name} was added, but the room could not refresh. Refresh before importing again.`
              : `Import stopped at ${name}. Check the roster and player limit before trying again; the last request may need confirmation.`;
            if (confirmed) return of(result(remaining, true, undefined, problem, true));
            return this.api.get(code).pipe(
              map((canonical) => result(remaining, true, canonical, problem)),
              catchError(() => of(result(remaining, true, undefined, problem, true))),
            );
          }),
        );
      };
      return this.api.get(code).pipe(
        concatMap((session) => concat(of(result(names.length, false, session)), step(0, session))),
        catchError(() => of(result(names.length, true, undefined, 'The roster could not refresh. No import requests were confirmed. Refresh and try again.', true))),
      );
    });
  }
}
