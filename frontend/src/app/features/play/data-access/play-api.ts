import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import {
  CreatePlaySession,
  normalizeCode,
  PlayPlayer,
  PlaySession,
  PlayScoreCorrection,
  PlayTeam,
  PlayCallOutEdit,
} from './play.models';

@Injectable({ providedIn: 'root' })
export class PlayApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/play/sessions';

  create(input: CreatePlaySession) {
    return this.http.post<PlaySession>(this.base, input);
  }
  edit(code: string, input: CreatePlaySession) {
    return this.http.patch<PlaySession>(this.url(code), input);
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
  finish(code: string, matchId: string, winner: PlayTeam | null = null) {
    return this.http.post<PlaySession>(
      `${this.url(code)}/matches/${encodeURIComponent(matchId)}/finish`,
      { winner },
    );
  }
  editCallOut(code: string, matchId: string, rallyId: string, edit: PlayCallOutEdit) {
    return this.http.patch<PlaySession>(
      `${this.url(code)}/matches/${encodeURIComponent(matchId)}/rallies/${encodeURIComponent(rallyId)}/call-out`,
      edit,
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
  renameGuest(code: string, playerId: string, displayName: string) {
    return this.http.patch<PlaySession>(
      `${this.url(code)}/players/${encodeURIComponent(playerId)}`,
      { displayName },
    );
  }
  removeGuest(code: string, playerId: string) {
    return this.http.delete<PlaySession>(
      `${this.url(code)}/players/${encodeURIComponent(playerId)}`,
    );
  }
  end(code: string) {
    return this.http.post<PlaySession>(`${this.url(code)}/end`, {});
  }
  startNext(
    code: string,
    matchId: string,
    lineup: { playerIds: string[]; overrideLineup: boolean },
  ) {
    return this.http.post<PlaySession>(
      `${this.url(code)}/matches/${encodeURIComponent(matchId)}/next`,
      lineup,
    );
  }
  private url(code: string) {
    return `${this.base}/${encodeURIComponent(normalizeCode(code))}`;
  }
  changeLineup(
    code: string,
    matchId: string,
    position: number,
    playerId: string,
    expectedRevision: number,
    otherMatchId?: string,
    otherExpectedRevision?: number,
  ) {
    return this.http.patch<PlaySession>(
      `${this.url(code)}/matches/${encodeURIComponent(matchId)}/lineup`,
      {
        position,
        playerId,
        expectedRevision,
        ...(otherMatchId ? { otherMatchId, otherExpectedRevision } : {}),
      },
    );
  }
  readyAction(
    code: string,
    matchId: string,
    action: 'start' | 'lineup/reset',
    expectedRevision: number,
  ) {
    return this.http.post<PlaySession>(
      `${this.url(code)}/matches/${encodeURIComponent(matchId)}/${action}`,
      { expectedRevision },
    );
  }
}
