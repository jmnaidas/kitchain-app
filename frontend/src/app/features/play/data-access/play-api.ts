import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import {
  CreatePlaySession,
  normalizeCode,
  PlayPlayer,
  PlaySession,
  PlayScoreCorrection,
  PlayTeam,
} from './play.models';

@Injectable({ providedIn: 'root' })
export class PlayApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/play/sessions';

  create(input: CreatePlaySession) {
    return this.http.post<PlaySession>(this.base, input);
  }
  get(code: string) {
    return this.http.get<PlaySession>(this.url(code));
  }
  addPlayer(code: string, displayName: string) {
    return this.http.post<PlayPlayer>(`${this.url(code)}/players`, { displayName });
  }
  start(code: string) {
    return this.http.post<PlaySession>(`${this.url(code)}/start`, {});
  }
  finish(code: string, matchId: string) {
    return this.http.post<PlaySession>(
      `${this.url(code)}/matches/${encodeURIComponent(matchId)}/finish`,
      {},
    );
  }
  rest(code: string, playerId: string) {
    return this.http.post<PlaySession>(
      `${this.url(code)}/players/${encodeURIComponent(playerId)}/rest`,
      {},
    );
  }
  rally(code: string, matchId: string, winner: PlayTeam) {
    return this.http.post<PlaySession>(
      `${this.url(code)}/matches/${encodeURIComponent(matchId)}/rallies`,
      { winner },
    );
  }
  correctScore(code: string, matchId: string, correction: PlayScoreCorrection) {
    return this.http.patch<PlaySession>(
      `${this.url(code)}/matches/${encodeURIComponent(matchId)}/score`,
      correction,
    );
  }
  rejoin(code: string, playerId: string) {
    return this.http.post<PlaySession>(
      `${this.url(code)}/players/${encodeURIComponent(playerId)}/rejoin`,
      {},
    );
  }
  private url(code: string) {
    return `${this.base}/${encodeURIComponent(normalizeCode(code))}`;
  }
}
