import { TestBed } from '@angular/core/testing';
import { PLAY_HUB_FACTORY, PlayHubConnection, PlayLive, PlayLiveEvent } from './play-live';

describe('Play live transport', () => {
  let handler: (event: { code: string }) => void;
  let reconnecting: () => void;
  let reconnected: () => void;
  let closed: () => void;
  let connection: PlayHubConnection;
  const flush = async () => {
    await Promise.resolve();
    await Promise.resolve();
    await Promise.resolve();
  };

  beforeEach(() => {
    connection = {
      start: vi.fn().mockResolvedValue(undefined),
      stop: vi.fn().mockResolvedValue(undefined),
      invoke: vi.fn().mockResolvedValue(undefined),
      on: vi.fn((_name, callback) => {
        handler = callback;
      }),
      off: vi.fn(),
      onreconnecting: vi.fn((callback) => {
        reconnecting = callback;
      }),
      onreconnected: vi.fn((callback) => {
        reconnected = callback;
      }),
      onclose: vi.fn((callback) => {
        closed = callback;
      }),
    };
    TestBed.configureTestingModule({
      providers: [{ provide: PLAY_HUB_FACTORY, useValue: () => connection }],
    });
  });

  it('normalizes subscription, filters other sessions, rejoins after reconnect and stops on teardown', async () => {
    const events: PlayLiveEvent[] = [];
    const subscription = TestBed.inject(PlayLive)
      .watch(' abcdef ')
      .subscribe((event) => events.push(event));
    await flush();
    expect(connection.invoke).toHaveBeenCalledWith('JoinSessionGroup', 'ABCDEF');
    expect(events.at(-1)).toEqual({ kind: 'changed' });
    const count = events.length;
    handler({ code: 'GHJKLM' });
    expect(events).toHaveLength(count);
    handler({ code: 'ABCDEF' });
    expect(events).toHaveLength(count + 1);
    reconnecting();
    expect(events.at(-1)).toEqual({ kind: 'status', status: 'reconnecting' });
    reconnected();
    await flush();
    expect(connection.invoke).toHaveBeenCalledTimes(2);
    expect(events.at(-1)).toEqual({ kind: 'changed' });
    subscription.unsubscribe();
    expect(connection.off).toHaveBeenCalledWith('SessionChanged', handler);
    expect(connection.stop).toHaveBeenCalledTimes(1);
    const stopped = events.length;
    handler({ code: 'ABCDEF' });
    closed();
    expect(events).toHaveLength(stopped);
  });

  it('reports initial connection failure without erroring the subscriber or retrying in a loop', async () => {
    vi.mocked(connection.start).mockRejectedValue(new Error('Offline'));
    const events: PlayLiveEvent[] = [];
    const error = vi.fn();
    const subscription = TestBed.inject(PlayLive)
      .watch('ABCDEF')
      .subscribe({ next: (event) => events.push(event), error });
    await flush();
    expect(events.at(-1)).toEqual({ kind: 'status', status: 'offline' });
    expect(error).not.toHaveBeenCalled();
    expect(connection.start).toHaveBeenCalledTimes(1);
    expect(connection.invoke).not.toHaveBeenCalled();
    subscription.unsubscribe();
  });
});
