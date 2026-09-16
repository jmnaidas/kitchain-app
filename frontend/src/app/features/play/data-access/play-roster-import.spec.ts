import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PlayRosterImport, RosterImportProgress } from './play-roster-import';
import { PlaySession } from './play.models';
import { PLAY_RECENT_PLAYERS, RECENT_PLAYERS_KEY } from './play-recent-players';
import { RecentSessionRosters } from './play-previous-rosters';
import { RecentPlaySessions } from './recent-play-sessions';

const url = '/api/play/sessions/ABCDEF';
const session = (names: string[] = [], status = 'Draft'): PlaySession => ({
  joinCode: 'ABCDEF', status, players: names.map((displayName, i) => ({ id: 'backend-' + i, displayName, state: 'Waiting' })),
}) as PlaySession;
describe('Canonical roster imports', () => {
  let http: HttpTestingController;
  let results: RosterImportProgress[];
  beforeEach(() => {
    localStorage.removeItem(RECENT_PLAYERS_KEY); localStorage.removeItem('kitchain.play.recent.v1');
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController); results = [];
  });
  afterEach(() => { http.verify(); localStorage.removeItem(RECENT_PLAYERS_KEY); localStorage.removeItem('kitchain.play.recent.v1'); });
  const start = (names: string[]) => TestBed.inject(PlayRosterImport).add('ABCDEF', names).subscribe((p) => results.push(p));
  it('adds sequentially, skips normalized duplicates and refetches after every success', () => {
    start([' alex  lee ', 'Blair', 'Casey', 'BLAIR']);
    http.expectOne(url).flush(session(['Alex Lee']));
    const first = http.expectOne(url + '/players'); expect(first.request.body).toEqual({ displayName: 'Blair' });
    expect(results.at(-1)?.session?.players.map((p) => p.displayName)).toEqual(['Alex Lee']);
    first.flush({ id: 'new-id', displayName: 'Blair' });
    http.expectNone(url + '/players'); http.expectOne(url).flush(session(['Alex Lee', 'Blair']));
    http.expectOne(url + '/players').flush({ id: 'new-id-2', displayName: 'Casey' });
    http.expectOne(url).flush(session(['Alex Lee', 'Blair', 'Casey']));
    expect(results.at(-1)).toMatchObject({ added: 2, skipped: 1, remaining: 0, done: true, problem: '' });
    expect(TestBed.inject(PLAY_RECENT_PLAYERS).items().map((p) => p.displayName)).toEqual(['Casey', 'Blair']);
    expect(TestBed.inject(PLAY_RECENT_PLAYERS).items().some((p) => p.id === 'new-id')).toBe(false);
  });
  it('stops on partial failure and never creates phantom players or retries a POST', () => {
    start(['Alex', 'Blair', 'Casey']); http.expectOne(url).flush(session());
    http.expectOne(url + '/players').flush({ displayName: 'Alex' }); http.expectOne(url).flush(session(['Alex']));
    http.expectOne(url + '/players').flush({}, { status: 409, statusText: 'Full' });
    http.expectOne(url).flush(session(['Alex']));
    http.expectNone(url + '/players');
    expect(results.at(-1)).toMatchObject({ added: 1, remaining: 2, done: true, needsRefresh: false });
    expect(results.at(-1)?.session?.players).toHaveLength(1);
    expect(TestBed.inject(PLAY_RECENT_PLAYERS).items().map((p) => p.displayName)).toEqual(['Alex']);
  });
  it('retains a confirmed add and requires refresh if its canonical read fails', () => {
    start(['Alex', 'Blair']); http.expectOne(url).flush(session());
    http.expectOne(url + '/players').flush({ displayName: 'Alex' });
    http.expectOne(url).flush({}, { status: 503, statusText: 'Offline' });
    expect(results.at(-1)).toMatchObject({ added: 1, remaining: 1, done: true, needsRefresh: true });
    expect(results.at(-1)?.session).toBeUndefined(); http.expectNone(url + '/players');
  });
  it('does not post from stale Active or Ended sessions', () => {
    for (const status of ['Active', 'Ended']) {
      start(['Alex']); http.expectOne(url).flush(session([], status));
      expect(results.at(-1)?.problem).toContain('no longer a Draft'); http.expectNone(url + '/players');
    }
  });
  it('stops the remaining batch when another client starts the session', () => {
    start(['Alex', 'Blair']); http.expectOne(url).flush(session());
    http.expectOne(url + '/players').flush({ displayName: 'Alex' });
    http.expectOne(url).flush(session(['Alex'], 'Active'));
    expect(results.at(-1)).toMatchObject({ added: 1, remaining: 1, done: true }); http.expectNone(url + '/players');
  });
  it('cancels outstanding reads and remaining mutations when the room is left', () => {
    const subscription = start(['Alex', 'Blair']); http.expectOne(url).flush(session());
    http.expectOne(url + '/players').flush({ displayName: 'Alex' });
    const read = http.expectOne(url); subscription.unsubscribe(); expect(read.cancelled).toBe(true); http.expectNone(url + '/players');
  });
  it('fails safely before posting if the initial canonical read fails', () => {
    start(['Alex']); http.expectOne(url).flush({}, { status: 503, statusText: 'Offline' });
    expect(results.at(-1)).toMatchObject({ added: 0, needsRefresh: true, done: true }); http.expectNone(url + '/players');
  });
  it('loads previous rosters from real recent sessions, exposes only names, and forgets locally', async () => {
    const recent = TestBed.inject(RecentPlaySessions); recent.remember('GHJKLM'); recent.remember('ABCDEF');
    let result: unknown;
    const store = TestBed.inject(RecentSessionRosters);
    store.load('ABCDEF').subscribe((value) => result = value);
    http.expectNone(url);
    http.expectOne('/api/play/sessions/GHJKLM').flush({ ...session(['Alex', 'alex', 'Blair'], 'Ended'), name: 'Previous crew', matchHistory: [{ id: 'private-match' }], queue: { nextUp: ['Alex'] } });
    expect(result).toEqual([{ index: 0, code: 'GHJKLM', name: 'Previous crew', names: ['Alex', 'Blair'], unavailable: false }]);
    await store.forget('GHJKLM'); expect(recent.list().map((e) => e.code)).toEqual(['ABCDEF']);
  });
  it('handles corrupt recent-session storage and unavailable previous sessions', () => {
    localStorage.setItem('kitchain.play.recent.v1', 'broken');
    let result: unknown;
    const store = TestBed.inject(RecentSessionRosters); store.load('ABCDEF').subscribe((value) => result = value);
    expect(result).toEqual([]);
    TestBed.inject(RecentPlaySessions).remember('GHJKLM'); store.load('ABCDEF').subscribe((value) => result = value);
    http.expectOne('/api/play/sessions/GHJKLM').flush({}, { status: 404, statusText: 'Gone' });
    expect(result).toEqual([{ index: 0, code: 'GHJKLM', name: 'GHJKLM', names: [], unavailable: true }]);
  });
});
