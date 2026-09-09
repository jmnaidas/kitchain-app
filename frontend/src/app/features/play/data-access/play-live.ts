import { inject, Injectable, InjectionToken } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Observable } from 'rxjs';
import { normalizeCode, validCode } from './play.models';

export type PlayLiveStatus = 'connecting' | 'live' | 'reconnecting' | 'offline';
export type PlayLiveEvent = { kind: 'status'; status: PlayLiveStatus } | { kind: 'changed' };
export type PlayHubConnection = Pick<
  HubConnection,
  'start' | 'stop' | 'invoke' | 'on' | 'off' | 'onreconnecting' | 'onreconnected' | 'onclose'
>;

export const PLAY_HUB_FACTORY = new InjectionToken<() => PlayHubConnection>('Play hub connection', {
  providedIn: 'root',
  factory: () => () =>
    new HubConnectionBuilder()
      .withUrl('/hubs/play')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.None)
      .build(),
});

@Injectable({ providedIn: 'root' })
export class PlayLive {
  private readonly createConnection = inject(PLAY_HUB_FACTORY);

  watch(raw: string): Observable<PlayLiveEvent> {
    return new Observable((observer) => {
      const code = normalizeCode(raw);
      let disposed = false;
      let connection: PlayHubConnection | undefined;
      const status = (status: PlayLiveStatus) => {
        if (!disposed) observer.next({ kind: 'status', status });
      };
      const changed = (event: { code?: unknown }) => {
        if (!disposed && typeof event?.code === 'string' && normalizeCode(event.code) === code)
          observer.next({ kind: 'changed' });
      };
      const join = async () => {
        try {
          if (disposed) return;
          await connection!.invoke('JoinSessionGroup', code);
          if (disposed) return;
          status('live');
          // Close the gap between the initial REST read and group subscription, or reconnect.
          observer.next({ kind: 'changed' });
        } catch {
          status('offline');
          await connection?.stop().catch(() => undefined);
        }
      };
      status('connecting');
      try {
        if (!validCode(code)) throw new Error('Invalid session code');
        connection = this.createConnection();
        connection.on('SessionChanged', changed);
        connection.onreconnecting(() => status('reconnecting'));
        connection.onreconnected(() => {
          void join();
        });
        connection.onclose(() => status('offline'));
        void connection
          .start()
          .then(join)
          .catch(() => status('offline'));
      } catch {
        status('offline');
      }
      return () => {
        disposed = true;
        connection?.off('SessionChanged', changed);
        // Stopping the room's dedicated connection also removes its group membership.
        void connection?.stop().catch(() => undefined);
      };
    });
  }
}
