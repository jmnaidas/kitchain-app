import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { CreatePlaySession, normalizeCode, PlayPlayer, PlaySession } from './play.models';

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
  rest(code: string, playerId: string) {
    return this.http.post<PlaySession>(
      `${this.url(code)}/players/${encodeURIComponent(playerId)}/rest`,
      {},
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
