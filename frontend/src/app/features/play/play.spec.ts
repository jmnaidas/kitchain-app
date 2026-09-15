import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { App } from '../../app';
import { routes } from '../../app.routes';
import {
  localDate,
  PlayMatchSummary,
  PlayPlayer,
  PlayRallyEvent,
  PlaySession,
} from './data-access/play.models';
import { RecentPlaySessions } from './data-access/recent-play-sessions';
import { Observable, Subject } from 'rxjs';
import { PlayLive, PlayLiveEvent } from './data-access/play-live';

const base = '/api/play/sessions';
const code = 'ABCDEF';
const joinedAt = '2026-09-09T01:00:00Z';
const player = (
  id: string,
  displayName: string,
  queueOrder: number | null,
  state: PlayPlayer['state'] = 'Waiting',
): PlayPlayer => ({
  id,
  displayName,
  queueOrder,
  state,
  sessionId: 'session-id',
  identityType: 'Guest',
  joinedAt,
  updatedAt: joinedAt,
});
const crew = [
  player('a', 'Alex', 7),
  player('b', 'Blair', 12),
  player('c', 'Casey', 22),
  player('d', 'Drew', 40),
  player('e', 'Ellis', 41),
];
const room = (
  players: PlayPlayer[] = [],
  waitingQueue = players.filter((p) => p.state === 'Waiting'),
): PlaySession => ({
  insights: {
    totalPlayers: players.length,
    numberOfCourts: 2,
    completedGames: 0,
    playerAppearances: 0,
    recordedRallies: null,
    taggedRallies: null,
    players: players.map((p) => ({
      playerId: p.id,
      displayName: p.displayName,
      gamesPlayed: 0,
      wins: 0,
      losses: 0,
      distinctTeammates: 0,
      distinctOpponents: 0,
    })),
  },
  id: 'session-id',
  joinCode: code,
  name: 'Evening crew',
  sessionDate: '2026-09-10',
  endDate: '2026-09-10',
  startTime: '18:00:00',
  endTime: '21:00:00',
  numberOfCourts: 2,
  maximumPlayers: null,
  status: 'Draft',
  rotationMode: 'FairRotation',
  scoringMode: 'Traditional',
  gameTo: 11,
  winBy: 2,
  createdAt: joinedAt,
  updatedAt: joinedAt,
  players,
  waitingQueue,
  matchHistory: [],
  currentMatches: [],
  activeMatches: [],
  mode: 'QueueOnly',
});

describe('Play experience', () => {
  let fixture: ComponentFixture<App>;
  let http: HttpTestingController;
  let router: Router;
  let element: HTMLElement;
  let liveEvents: Subject<PlayLiveEvent>;
  let listened: string[];
  let stopped: string[];

  beforeEach(async () => {
    localStorage.removeItem('kitchain.play.recent.v1');
    liveEvents = new Subject<PlayLiveEvent>();
    listened = [];
    stopped = [];
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter(routes),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: PlayLive,
          useValue: {
            watch: (code: string) =>
              new Observable<PlayLiveEvent>((observer) => {
                listened.push(code);
                const subscription = liveEvents.subscribe(observer);
                return () => {
                  stopped.push(code);
                  subscription.unsubscribe();
                };
              }),
          },
        },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(App);
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    element = fixture.nativeElement;
    fixture.detectChanges();
  });
  afterEach(() => {
    localStorage.removeItem('kitchain.play.recent.v1');
    http.verify();
  });
  async function navigate(url: string) {
    await router.navigateByUrl(url);
    fixture.detectChanges();
    TestBed.tick();
  }
  async function settle() {
    await fixture.whenStable();
    fixture.detectChanges();
  }
  function fill(id: string, value: string) {
    const input = element.querySelector<HTMLInputElement>(`#${id}`)!;
    input.value = value;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.detectChanges();
  }
  function submit() {
    element
      .querySelector('form')!
      .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    fixture.detectChanges();
    TestBed.tick();
  }
  function button(label: string) {
    const result = Array.from(element.querySelectorAll<HTMLButtonElement>('button')).find(
      (b) =>
        b.textContent?.replace(/\s+/g, ' ').trim() === label ||
        b.getAttribute('aria-label') === label,
    );
    if (!result) throw new Error(`Missing button: ${label}`);
    return result;
  }
  function click(label: string) {
    button(label).click();
    fixture.detectChanges();
    TestBed.tick();
  }
  async function openRoom(session = room()) {
    await navigate(`/play/s/${code}`);
    http.expectOne(`${base}/${code}`).flush(session);
    await settle();
  }
  const queueNames = () =>
    Array.from(element.querySelectorAll('app-play-player-list strong'), (e) =>
      e.textContent?.trim(),
    );

  it('subscribes after loading, coalesces live reads, and cleans up when changing sessions', async () => {
    await openRoom();
    expect(listened).toEqual([code]);
    liveEvents.next({ kind: 'changed' });
    liveEvents.next({ kind: 'changed' });
    TestBed.tick();
    http.expectOne(`${base}/${code}`).flush(room([crew[0]]));
    await settle();
    expect(queueNames()).toEqual(['Alex']);
    await navigate('/play/s/GHJKLM');
    expect(stopped).toEqual([code]);
    http.expectOne(`${base}/GHJKLM`).flush({ ...room(), joinCode: 'GHJKLM' });
    await settle();
    expect(listened).toEqual([code, 'GHJKLM']);
    fixture.destroy();
    expect(stopped).toEqual([code, 'GHJKLM']);
  });

  it('defers live reload during a mutation and keeps manual refresh usable offline', async () => {
    await openRoom();
    fill('guest-name', 'Alex');
    submit();
    liveEvents.next({ kind: 'changed' });
    TestBed.tick();
    http.expectNone(`${base}/${code}`);
    http.expectOne(`${base}/${code}/players`).flush(crew[0]);
    http.expectOne(`${base}/${code}`).flush(room([crew[0]]));
    TestBed.tick();
    http.expectOne(`${base}/${code}`).flush(room([crew[0], crew[1]]));
    await settle();
    expect(queueNames()).toEqual(['Alex', 'Blair']);
    liveEvents.next({ kind: 'status', status: 'offline' });
    fixture.detectChanges();
    click('Refresh');
    http.expectOne(`${base}/${code}`).flush(room([crew[0], crew[1]]));
    await settle();
    expect(element.querySelector<HTMLInputElement>('#guest-name')?.matches(':disabled')).toBe(
      false,
    );
    expect(listened).toEqual([code, code]);
  });

  it('offers create and join from the Play landing without placeholder functionality claims', async () => {
    await navigate('/play');
    await settle();
    expect(element.querySelector('h1')?.textContent).toContain('Bring the group');
    expect(element.querySelector('a[href="/play/new"]')?.textContent).toContain('Start a Session');
    expect(element.querySelector('a[href="/play/join"]')?.textContent).toContain('Join with Code');
    expect(element.textContent).not.toContain('not available yet');
  });

  it('loads recent sessions in access order, removes missing codes and resumes using the live API', async () => {
    localStorage.setItem(
      'kitchain.play.recent.v1',
      JSON.stringify([
        { code: 'GHJKLM', accessedAt: 1 },
        { code, accessedAt: 3 },
        { code: 'NPQRST', accessedAt: 2 },
      ]),
    );
    await navigate('/play');
    http.expectOne(`${base}/${code}`).flush(room());
    http
      .expectOne(`${base}/NPQRST`)
      .flush({ ...room(), joinCode: 'NPQRST', name: 'Older crew', status: 'Ended' });
    http.expectOne(`${base}/GHJKLM`).flush({}, { status: 404, statusText: 'Not Found' });
    await settle();
    expect(
      Array.from(element.querySelectorAll('app-play-recent h3'), (e) => e.textContent),
    ).toEqual(['Evening crew', 'Older crew']);
    expect(
      TestBed.inject(RecentPlaySessions)
        .list()
        .map((e) => e.code),
    ).toEqual([code, 'NPQRST']);
    element.querySelector<HTMLAnchorElement>('a[aria-label="Resume Evening crew"]')!.click();
    await settle();
    expect(router.url).toBe(`/play/s/${code}`);
    http.expectOne(`${base}/${code}`).flush(room());
    await settle();
    expect(element.querySelector('h1')?.textContent).toContain('Evening crew');
    const stored = JSON.parse(localStorage.getItem('kitchain.play.recent.v1')!);
    expect(Object.keys(stored[0]).sort()).toEqual(['accessedAt', 'code']);
  });

  it('bounds and deduplicates recent codes, tolerates invalid storage and retains codes on network failure', async () => {
    localStorage.setItem('kitchain.play.recent.v1', '{broken');
    const recent = TestBed.inject(RecentPlaySessions);
    expect(recent.list()).toEqual([]);
    for (const last of 'ABCDEFGHJ') recent.remember(`AAAAA${last}`);
    recent.remember(' aaaaaj ');
    expect(recent.list()).toHaveLength(8);
    expect(recent.list()[0].code).toBe('AAAAAJ');
    for (const entry of recent.list()) recent.remove(entry.code);
    recent.remember(code);
    await navigate('/play');
    http.expectOne(`${base}/${code}`).flush({}, { status: 503, statusText: 'Unavailable' });
    await settle();
    expect(recent.list()[0].code).toBe(code);
    expect(element.textContent).toContain('codes are still saved');
  });

  it('renders active courts and player court labels while keeping Playing players out of Next Up', async () => {
    const playing = crew
      .slice(0, 4)
      .map((p) => ({ ...p, state: 'Playing' as const, queueOrder: null }));
    const waiting = [
      crew[4],
      player('f', 'Frankie', 42),
      player('g', 'Gale', 43),
      player('h', 'Harper', 44),
      player('i', 'Indy', 45),
    ];
    const active: PlaySession = {
      ...room([...playing, ...waiting], waiting),
      status: 'Active',
      mode: 'LiveScoring',
      currentMatches: [
        {
          id: 'match-1',
          courtNumber: 1,
          status: 'Active',
          startedAt: joinedAt,
          completedAt: null,
          winner: null,
          rallies: [],
          nextLineup: [],
          eligiblePlayers: [],
          teamAScore: 0,
          teamBScore: 0,
          servingTeam: 'A',
          currentServerNumber: 2,
          players: playing.map((p, i) => ({
            playerId: p.id,
            displayName: p.displayName,
            team: i < 2 ? 'A' : 'B',
            position: i + 1,
          })),
        },
      ],
    };
    await openRoom(active);
    expect(queueNames()).toEqual(['Ellis', 'Frankie', 'Gale', 'Harper', 'Indy']);
    expect(element.querySelectorAll('app-play-player-list li')).toHaveLength(5);
    click('Courts');
    expect(element.querySelector('article[aria-label="Court 1"]')?.textContent).toContain('Alex');
    expect(element.querySelector('article[aria-label="Court 1"]')?.textContent).toContain('Team B');
    expect(element.querySelector('article[aria-label="Court 2"]')?.textContent).toContain(
      'Waiting for four',
    );
    click('Players 9');
    expect(element.textContent).toMatch(/Playing\s+·\s+Court 1/);
    expect(element.querySelector('[aria-label="Take a break for Alex"]')).toBeNull();
    click('Courts');
    click('Finish game on Court 1');
    http.expectNone(`${base}/${code}/matches/match-1/finish`);
    click('Cancel');
    click('Finish game on Court 1');
    click('Confirm Finish');
    const finish = http.expectOne(`${base}/${code}/matches/match-1/finish`);
    expect(finish.request.method).toBe('POST');
    const returned = {
      ...active,
      currentMatches: [
        {
          ...active.currentMatches[0],
          id: 'match-2',
          players: waiting.slice(0, 4).map((p, i) => ({
            playerId: p.id,
            displayName: p.displayName,
            team: i < 2 ? 'A' : 'B',
            position: i + 1,
          })),
        },
      ],
      waitingQueue: [waiting[4], ...crew.slice(0, 4)],
      players: [
        ...waiting.slice(0, 4).map((p) => ({ ...p, state: 'Playing' as const, queueOrder: null })),
        waiting[4],
        ...crew.slice(0, 4),
      ],
    };
    const completed = completedRoom(active);
    finish.flush(completed);
    await settle();
    expect(element.querySelector('article[aria-label="Court 1"]')?.textContent).toContain('Alex');
    expect(element.textContent).toContain('Game complete');
    click('Proceed to next game');
    http.expectNone((request) => request.method === 'POST');
    click('Cancel');
    click('Proceed to next game');
    click('Start Next Game');
    const confirm = http.expectOne(`${base}/${code}/matches/match-1/next`);
    expect(confirm.request.body).toEqual({
      playerIds: waiting.slice(0, 4).map((p) => p.id),
      overrideLineup: false,
    });
    confirm.flush(returned);
    await settle();
    expect(element.querySelector('article[aria-label="Court 1"]')?.textContent).toContain('Ellis');
    click('Queue 5');
    expect(queueNames()).toEqual(['Indy', 'Alex', 'Blair', 'Casey', 'Drew']);
    http.expectNone(`${base}/${code}`);
  });

  it('defaults to local date and one court, and validates required fields, times and whole numbers', async () => {
    await navigate('/play/new');
    expect(element.querySelector<HTMLInputElement>('#session-name')?.value).toBe(
      'Pickleball Session',
    );
    expect(element.querySelector<HTMLInputElement>('#session-date')?.value).toBe(localDate());
    expect(element.querySelector<HTMLInputElement>('#court-count')?.value).toBe('1');
    fill('session-name', ' ');
    submit();
    expect(element.querySelector('#name-help')?.textContent).toContain('required');
    expect(element.querySelector('#start-time')?.getAttribute('aria-invalid')).toBe('true');
    fill('session-name', 'Crew');
    fill('start-time', '19:00');
    fill('end-time', '18:00');
    fill('court-count', '1.5');
    submit();
    expect(element.querySelector('#end-help')?.textContent).toContain('after start');
    expect(element.querySelector('#court-count')?.getAttribute('aria-invalid')).toBe('true');
    http.expectNone((request) => request.method === 'POST');
  });

  it('creates exactly the editable contract with TimeOnly-compatible values and navigates to the room', async () => {
    await navigate('/play/new');
    fill('session-name', ' Evening crew ');
    fill('session-date', '2026-09-10');
    fill('start-time', '18:00');
    fill('end-time', '21:00');
    fill('court-count', '2');
    submit();
    const create = http.expectOne(base);
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({
      name: 'Evening crew',
      date: '2026-09-10',
      endDate: '2026-09-10',
      startTime: '18:00:00',
      endTime: '21:00:00',
      numberOfCourts: 2,
      maximumPlayers: null,
      mode: 'QueueOnly',
    });
    submit();
    http.expectNone(base);
    create.flush(room());
    await settle();
    expect(router.url).toBe(`/play/s/${code}`);
    http.expectOne(`${base}/${code}`).flush(room());
    await settle();
    expect(element.textContent).toContain('Your court crew starts here.');
  });

  it('maps server validation to readable field messages and preserves create form values', async () => {
    await navigate('/play/new');
    fill('start-time', '18:00');
    fill('end-time', '21:00');
    submit();
    http
      .expectOne(base)
      .flush(
        { errors: { EndTime: ['internal exception must never be displayed'] } },
        { status: 400, statusText: 'Bad Request' },
      );
    await settle();
    expect(element.querySelector('#end-help')?.textContent).toContain('after start');
    expect(element.textContent).not.toContain('internal exception');
    expect(element.querySelector<HTMLInputElement>('#start-time')?.value).toBe('18:00');
  });

  it('normalizes a pasted join code, checks the session and opens its canonical URL', async () => {
    await navigate('/play/join');
    fill('join-code', ' abcdef ');
    submit();
    http.expectOne(`${base}/${code}`).flush(room());
    await settle();
    expect(router.url).toBe(`/play/s/${code}`);
    http.expectOne(`${base}/${code}`).flush(room());
    await settle();
    expect(element.querySelector('code')?.textContent).toBe(code);
  });

  it('keeps a missing join code for retry and handles a missing direct session URL', async () => {
    await navigate('/play/join');
    fill('join-code', 'bad');
    submit();
    http.expectNone((request) => request.method === 'GET');
    expect(element.textContent).toContain('six-character code');
    fill('join-code', code);
    submit();
    http.expectOne(`${base}/${code}`).flush({}, { status: 404, statusText: 'Not Found' });
    await settle();
    expect(element.textContent).toContain('Session not found');
    expect(element.querySelector<HTMLInputElement>('#join-code')?.value).toBe(code);
    await navigate(`/play/s/${code}`);
    http.expectOne(`${base}/${code}`).flush({}, { status: 404, statusText: 'Not Found' });
    await settle();
    expect(element.querySelector('h1')?.textContent).toContain('Session not found');
  });

  it('shows the complete waiting list without promising a FIFO next quartet', async () => {
    await navigate(`/play/s/${code}`);
    expect(element.textContent).toContain('Loading your session');
    const authoritative = [crew[2], crew[0], crew[4], crew[1], crew[3]];
    http.expectOne(`${base}/${code}`).flush(room(crew, authoritative));
    await settle();
    expect(queueNames()).toEqual(['Casey', 'Alex', 'Ellis', 'Blair', 'Drew']);
    expect(element.querySelectorAll('app-play-player-list li')).toHaveLength(5);
    expect(element.querySelectorAll('app-play-player-list .position')[4]?.textContent).toContain(
      '5',
    );
    expect(element.textContent).not.toContain('Assigned');
  });

  it('adds a guest once, refetches canonical state and clears the name for the next guest', async () => {
    await openRoom();
    fill('guest-name', ' Alex ');
    submit();
    const add = http.expectOne(`${base}/${code}/players`);
    expect(add.request.body).toEqual({ displayName: 'Alex' });
    submit();
    http.expectNone(`${base}/${code}/players`);
    add.flush(crew[0]);
    fixture.detectChanges();
    expect(queueNames()).toEqual([]);
    http.expectOne(`${base}/${code}`).flush(room([crew[0]]));
    await settle();
    expect(queueNames()).toEqual(['Alex']);
    expect(element.querySelector<HTMLInputElement>('#guest-name')?.value).toBe('');
    expect(element.textContent).toContain('Waiting players');
  });

  it('takes a break using the returned session without optimistic reordering', async () => {
    await openRoom({ ...room(crew), status: 'Active' });
    click('Take a break for Alex');
    const rest = http.expectOne(`${base}/${code}/players/a/rest`);
    expect(queueNames()[0]).toBe('Alex');
    rest.flush(
      room([{ ...crew[0], state: 'Resting', queueOrder: null }, ...crew.slice(1)], crew.slice(1)),
    );
    await settle();
    expect(queueNames()).toEqual(['Blair', 'Casey', 'Drew', 'Ellis']);
    expect(element.textContent).toContain('1 taking a break');
    http.expectNone(`${base}/${code}`);
  });

  it('shows the whole roster and rejoins a resting player at the server-defined back', async () => {
    const resting = { ...crew[0], state: 'Resting' as const, queueOrder: null };
    const playing = player('f', 'Frankie', null, 'Playing');
    await openRoom({ ...room([resting, crew[1], playing], [crew[1]]), status: 'Active' });
    click('Players 3');
    expect(queueNames()).toEqual(['Alex', 'Blair', 'Frankie']);
    expect(element.textContent).toContain('Playing');
    expect(element.querySelector('[aria-label="Take a break for Frankie"]')).toBeNull();
    click('Rejoin queue for Alex');
    http
      .expectOne(`${base}/${code}/players/a/rejoin`)
      .flush(
        room(
          [{ ...crew[0], queueOrder: 13 }, crew[1], playing],
          [crew[1], { ...crew[0], queueOrder: 13 }],
        ),
      );
    await settle();
    click('Queue 2');
    expect(queueNames()).toEqual(['Blair', 'Alex']);
  });

  it('starts Draft sessions and refreshes a conflict into an Ended read-only room', async () => {
    await openRoom(room(crew));
    click('Start Session');
    http.expectOne(`${base}/${code}/start`).flush({ ...room(crew), status: 'Active' });
    await settle();
    expect(element.textContent).toContain('Session active');
    expect(element.textContent).not.toContain('Start Session');
    click('Take a break for Alex');
    http
      .expectOne(`${base}/${code}/players/a/rest`)
      .flush({ title: 'This session has ended.' }, { status: 409, statusText: 'Conflict' });
    http.expectOne(`${base}/${code}`).flush({ ...room(crew), status: 'Ended' });
    await settle();
    expect(element.textContent).toContain('view-only');
    expect(element.querySelector<HTMLInputElement>('#guest-name')?.matches(':disabled')).toBe(true);
    expect(button('Take a break for Alex').disabled).toBe(true);
  });

  it('requires refresh if a confirmed guest cannot be refetched, and never silently repeats the POST', async () => {
    await openRoom();
    fill('guest-name', 'Alex');
    submit();
    http.expectOne(`${base}/${code}/players`).flush(crew[0]);
    http.expectOne(`${base}/${code}`).flush({}, { status: 503, statusText: 'Unavailable' });
    await settle();
    expect(element.textContent).toContain('was added, but the room couldn’t refresh');
    expect(element.querySelector<HTMLInputElement>('#guest-name')?.matches(':disabled')).toBe(true);
    submit();
    http.expectNone(`${base}/${code}/players`);
    click('Refresh');
    http.expectOne(`${base}/${code}`).flush(room([crew[0]]));
    await settle();
    expect(queueNames()).toEqual(['Alex']);
    expect(element.querySelector<HTMLInputElement>('#guest-name')?.matches(':disabled')).toBe(
      false,
    );
  });
  function rallyEvent(sequence = 1, changes: Partial<PlayRallyEvent> = {}): PlayRallyEvent {
    return {
      id: `rally-${sequence}`,
      matchId: 'scored-match',
      sequence,
      winner: 'A',
      pointAwarded: true,
      callOut: null,
      teamAScore: 4,
      teamBScore: 2,
      servingTeam: 'A',
      currentServerNumber: 1,
      createdAt: joinedAt,
      ...changes,
    };
  }

  function withRallies(events: PlayRallyEvent[]): PlaySession {
    const state = scoredRoom();
    const last = events.at(-1);
    return {
      ...state,
      currentMatches: [
        {
          ...state.currentMatches[0],
          rallies: events,
          ...(last
            ? {
                teamAScore: last.teamAScore,
                teamBScore: last.teamBScore,
                servingTeam: last.servingTeam,
                currentServerNumber: last.currentServerNumber,
              }
            : {}),
        },
      ],
    };
  }

  function chooseCallOut(sequence: number, value: string) {
    const select = element.querySelector<HTMLSelectElement>(
      `select[aria-label="Call-out for rally ${sequence}"]`,
    )!;
    select.value = value;
    select.dispatchEvent(new Event('change', { bubbles: true }));
    fixture.detectChanges();
    TestBed.tick();
  }

  it('scores immediately without call-outs, prevents an in-flight double click and shows compact ordered history', async () => {
    await openRoom(scoredRoom());
    click('Courts');
    expect(element.querySelector('app-play-rally-history')?.textContent).toContain(
      'No rallies recorded',
    );
    click('Team A won rally');
    const score = http.expectOne(`${base}/${code}/matches/scored-match/rallies`);
    expect(score.request.body).toEqual({ winner: 'A' });
    click('Team A won rally');
    http.expectNone((request) => request.method === 'POST');
    const first = rallyEvent();
    score.flush(withRallies([first]));
    await settle();
    expect(element.querySelector('[aria-label="Team A score"]')?.textContent).toBe('4');
    expect(element.querySelector('app-play-rally-history')?.textContent).toContain(
      'Rally 1 · Team A',
    );
    expect(element.querySelector('app-play-rally-history select')).toBeNull();
    expect(button('Team B won rally').disabled).toBe(false);
    click('Team B won rally');
    const second = rallyEvent(2, { winner: 'B', pointAwarded: false, currentServerNumber: 2 });
    http
      .expectOne(`${base}/${code}/matches/scored-match/rallies`)
      .flush(withRallies([first, second]));
    await settle();
    expect(element.querySelector('app-play-rally-history')?.textContent).toContain(
      'No point awarded',
    );
    expect(element.querySelectorAll('app-play-rally-history li')).toHaveLength(1);
    click('Show rally history (2)');
    expect(
      Array.from(
        element.querySelectorAll('app-play-rally-history strong'),
        (node) => node.textContent,
      ),
    ).toEqual(['Rally 2 · Team B', 'Rally 1 · Team A']);
    click('Show latest rally');
    expect(element.querySelectorAll('app-play-rally-history li')).toHaveLength(1);
    http.expectNone((request) => request.method === 'PATCH');
  });

  it('adds, changes and removes a call-out using canonical metadata without touching the score', async () => {
    const rally = rallyEvent();
    await openRoom(withRallies([rally]));
    click('Courts');
    click('Add call-out for rally 1');
    expect(button('Team A won rally').disabled).toBe(false);
    const url = `${base}/${code}/matches/scored-match/rallies/rally-1/call-out`;
    chooseCallOut(1, 'Drive');
    let edit = http.expectOne(url);
    expect(edit.request.method).toBe('PATCH');
    expect(edit.request.body).toEqual({ callOut: 'Drive', expectedCallOut: null });
    expect(element.querySelector('app-play-rally-history .tag')).toBeNull();
    edit.flush(withRallies([{ ...rally, callOut: 'Drive' }]));
    await settle();
    expect(element.querySelector('app-play-rally-history .tag')?.textContent).toBe('Drive');
    chooseCallOut(1, 'ServiceBreak');
    edit = http.expectOne(url);
    expect(edit.request.body).toEqual({ callOut: 'ServiceBreak', expectedCallOut: 'Drive' });
    edit.flush(withRallies([{ ...rally, callOut: 'ServiceBreak' }]));
    await settle();
    expect(element.querySelector('app-play-rally-history .tag')?.textContent).toBe('Service break');
    chooseCallOut(1, '');
    edit = http.expectOne(url);
    expect(edit.request.body).toEqual({ callOut: null, expectedCallOut: 'ServiceBreak' });
    edit.flush(withRallies([rally]));
    await settle();
    expect(element.querySelector('app-play-rally-history .tag')).toBeNull();
    expect(element.querySelector('[aria-label="Team A score"]')?.textContent).toBe('4');
    expect(element.textContent).toContain('Serving: Team A · Server 1');
    http.expectNone((request) => request.method === 'POST');
  });

  it('refetches a stale call-out conflict and receives call-out changes through existing live updates', async () => {
    const rally = rallyEvent();
    await openRoom(withRallies([rally]));
    click('Courts');
    click('Add call-out for rally 1');
    chooseCallOut(1, 'Out');
    http
      .expectOne(`${base}/${code}/matches/scored-match/rallies/rally-1/call-out`)
      .flush(
        { title: 'This rally call-out has changed. Refresh before editing it.' },
        { status: 409, statusText: 'Conflict' },
      );
    http.expectOne(`${base}/${code}`).flush(withRallies([{ ...rally, callOut: 'Dink' }]));
    await settle();
    expect(element.textContent).toContain('Someone changed this call-out');
    expect(element.querySelector('app-play-rally-history .tag')?.textContent).toBe('Dink');
    liveEvents.next({ kind: 'changed' });
    TestBed.tick();
    http.expectOne(`${base}/${code}`).flush(withRallies([{ ...rally, callOut: 'Kitchen' }]));
    await settle();
    expect(element.querySelector('app-play-rally-history .tag')?.textContent).toBe('Kitchen');
  });

  it('retains history after correction and Finish override, then gives the next match a fresh history', async () => {
    const rally = rallyEvent(1, { callOut: 'Lob' });
    const initial = withRallies([rally]);
    await openRoom(initial);
    click('Courts');
    click('Correct score');
    const correction = element.querySelector<HTMLFormElement>('app-play-score-editor form')!;
    correction.querySelector<HTMLInputElement>('[name="teamAScore"]')!.value = '7';
    correction.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    fixture.detectChanges();
    const corrected = {
      ...initial,
      currentMatches: [{ ...initial.currentMatches[0], teamAScore: 7 }],
    };
    http.expectOne(`${base}/${code}/matches/scored-match/score`).flush(corrected);
    await settle();
    expect(element.querySelector('[aria-label="Team A score"]')?.textContent).toBe('7');
    expect(element.querySelector('app-play-rally-history .context')?.textContent).toContain(
      'A 4–2 B',
    );
    expect(element.querySelector('app-play-rally-history .tag')?.textContent).toBe('Lob');
    click('Finish game on Court 1');
    click('Confirm Finish');
    const held = completedRoom(corrected);
    http.expectOne(`${base}/${code}/matches/scored-match/finish`).flush(held);
    await settle();
    expect(element.querySelector('.rally-actions')).toBeNull();
    expect(element.querySelector('app-play-rally-history .tag')?.textContent).toBe('Lob');
    expect(button('Edit call-out for rally 1').disabled).toBe(false);
    click('Proceed to next game');
    click('Start Next Game');
    const fresh = scoredRoom();
    fresh.currentMatches[0] = {
      ...fresh.currentMatches[0],
      id: 'next-match',
      teamAScore: 0,
      teamBScore: 0,
    };
    http.expectOne(`${base}/${code}/matches/scored-match/next`).flush(fresh);
    await settle();
    expect(element.querySelector('app-play-rally-history')?.textContent).toContain(
      'No rallies recorded',
    );
    expect(element.querySelector('app-play-rally-history .tag')).toBeNull();
    click('Team B won rally');
    http.expectOne(`${base}/${code}/matches/next-match/rallies`).flush({
      ...fresh,
      currentMatches: [
        {
          ...fresh.currentMatches[0],
          rallies: [
            rallyEvent(1, {
              id: 'fresh-rally',
              matchId: 'next-match',
              winner: 'B',
              pointAwarded: false,
              teamAScore: 0,
              teamBScore: 0,
              servingTeam: 'B',
              currentServerNumber: 1,
            }),
          ],
        },
      ],
    });
    await settle();
    expect(element.querySelector('app-play-rally-history strong')?.textContent).toBe(
      'Rally 1 · Team B',
    );
  });

  it('keeps Ended rally history read-only and hides the entire scoring history in Queue Only', async () => {
    const state = {
      ...completedRoom(withRallies([rallyEvent(1, { callOut: 'Fault' })])),
      status: 'Ended' as const,
    };
    await openRoom(state);
    click('Courts');
    expect(element.querySelector('app-play-rally-history .tag')?.textContent).toBe('Fault');
    expect(element.querySelector('app-play-rally-history select')).toBeNull();
    expect(element.querySelector('[aria-label="Edit call-out for rally 1"]')).toBeNull();
    click('Refresh');
    http.expectOne(`${base}/${code}`).flush({ ...scoredRoom(), mode: 'QueueOnly' });
    await settle();
    expect(element.querySelector('app-play-rally-history')).toBeNull();
    expect(element.querySelector('.rally-actions')).toBeNull();
    expect(element.querySelector('app-play-score-editor')).toBeNull();
    expect(button('Finish game on Court 1')).toBeDefined();
  });

  it('keeps Draft save and cancel in one accessible action group', async () => {
    await openRoom(room());
    click('Edit Session');
    const actions = element.querySelector('app-play-session-form .form-actions')!;
    const buttons = actions.querySelectorAll<HTMLButtonElement>('button');
    expect(buttons).toHaveLength(2);
    expect(buttons[0].type).toBe('submit');
    expect(buttons[0].classList.contains('primary')).toBe(true);
    expect(buttons[1].type).toBe('button');
    expect(buttons[1].textContent?.trim()).toBe('Cancel');
    click('Cancel');
    expect(element.querySelector('app-play-session-form')).toBeNull();
    http.expectNone((request) => request.method !== 'GET');
  });

  it('uses View details for Ended recent sessions and navigates to the existing final room', async () => {
    TestBed.inject(RecentPlaySessions).remember(code);
    await navigate('/play');
    const ended = { ...room(), status: 'Ended' as const };
    http.expectOne(`${base}/${code}`).flush(ended);
    await settle();
    const link = element.querySelector<HTMLAnchorElement>(
      'a[aria-label="View details for Evening crew"]',
    )!;
    expect(link.textContent?.trim()).toBe('View details →');
    link.click();
    await settle();
    expect(router.url).toBe(`/play/s/${code}`);
    http.expectOne(`${base}/${code}`).flush(ended);
    await settle();
    expect(element.textContent).toContain('view-only');
  });

  function summary(changes: Partial<PlayMatchSummary> = {}): PlayMatchSummary {
    return {
      id: 'historic-match',
      courtNumber: 1,
      players: scoredRoom().currentMatches[0].players,
      startedAt: joinedAt,
      completedAt: '2026-09-09T01:20:00Z',
      teamAScore: 11,
      teamBScore: 8,
      winner: 'A',
      totalRallies: 3,
      taggedRallies: 2,
      callOutCounts: [{ callOut: 'Drive', count: 2 }],
      rallies: [
        rallyEvent(1, { callOut: 'Drive' }),
        rallyEvent(2),
        rallyEvent(3, { callOut: 'Drive' }),
      ],
      ...changes,
    };
  }

  it('shows canonical completed summaries and full read-only rally history in an Ended room', async () => {
    const newest = summary({
      id: 'newest',
      courtNumber: 2,
      winner: 'B',
      teamAScore: 8,
      teamBScore: 11,
    });
    const older = summary();
    await openRoom({ ...scoredRoom(), status: 'Ended', matchHistory: [newest, older] });
    click('Match History');
    const cards = element.querySelectorAll('app-play-match-summary');
    expect(cards).toHaveLength(2);
    expect(cards[0].querySelector('h3')?.textContent).toBe('Court 2');
    expect(cards[0].textContent).toContain('Team B won');
    expect(cards[0].textContent).toContain('A 8–11 B');
    expect(cards[1].textContent).toContain('Team A won');
    expect(cards[1].textContent).toContain('Alex + Blair');
    expect(cards[1].textContent).toContain('Casey + Drew');
    expect(cards[1].textContent).toContain('3 recorded rallies · 2 tagged');
    expect(cards[1].querySelector('[aria-label="Call-out counts"]')?.textContent).toContain(
      'Drive 2',
    );
    expect(cards[1].textContent).not.toContain('historic-match');
    expect(cards[1].textContent).not.toContain('Match ID');
    expect(cards[1].querySelector('article')?.getAttribute('aria-label')).not.toContain(
      'historic-match',
    );
    expect(cards[1].textContent).toContain('Started');
    expect(cards[1].textContent).toContain('Finished');
    click('View rally history');
    expect(element.querySelector('app-play-rally-history')).not.toBeNull();
    click('Show rally history (3)');
    expect(element.querySelectorAll('app-play-rally-history li')).toHaveLength(3);
    expect(element.querySelector('app-play-rally-history select')).toBeNull();
    expect(
      element.querySelector('app-play-rally-history [aria-label^="Edit call-out"]'),
    ).toBeNull();
    expect(element.querySelector('.rally-actions')).toBeNull();
    expect(element.querySelector('app-play-next-game')).toBeNull();
    http.expectNone((request) => request.method !== 'GET');
  });

  it('renders Queue Only history without invented scores or scoring controls', async () => {
    const queue = summary({
      teamAScore: null,
      teamBScore: null,
      winner: null,
      totalRallies: null,
      taggedRallies: null,
      rallies: [],
      callOutCounts: [],
    });
    await openRoom({ ...room(crew), status: 'Ended', matchHistory: [queue] });
    click('Match History');
    const card = element.querySelector('app-play-match-summary')!;
    expect(card.textContent).toContain('Score and rallies were not tracked');
    expect(card.textContent).toContain('Winner not recorded');
    expect(card.querySelector('.final-score')).toBeNull();
    expect(card.querySelector('button')).toBeNull();
    expect(card.querySelector('app-play-rally-history')).toBeNull();
    expect(card.textContent).not.toContain('0–0');
  });

  it('retains a completed summary after next-game confirmation and refreshes history through SignalR', async () => {
    const saved = summary({ id: 'scored-match' });
    const held = { ...completedRoom(scoredRoom(), 'A'), matchHistory: [saved] };
    await openRoom(held);
    click('Courts');
    click('Proceed to next game');
    click('Start Next Game');
    const next = scoredRoom();
    next.currentMatches[0].id = 'next-match';
    next.matchHistory = [saved];
    http.expectOne(`${base}/${code}/matches/scored-match/next`).flush(next);
    await settle();
    click('Match History');
    expect(element.querySelector('app-play-match-summary')?.textContent).toContain('Team A won');
    liveEvents.next({ kind: 'changed' });
    TestBed.tick();
    http
      .expectOne(`${base}/${code}`)
      .flush({ ...next, matchHistory: [summary({ id: 'new-completed', courtNumber: 2 }), saved] });
    await settle();
    expect(element.querySelectorAll('app-play-match-summary')).toHaveLength(2);
    expect(element.querySelector('app-play-match-summary h3')?.textContent).toBe('Court 2');
  });

  it('keeps no-completed-match history honest and clearly separates call-outs and their editor', async () => {
    await openRoom(withRallies([rallyEvent(1, { callOut: 'Drive' })]));
    click('Match History');
    expect(element.textContent).toContain('No completed matches yet');
    click('Courts');
    const heading = element.querySelector('app-play-rally-history .event-heading')!;
    expect(heading.textContent?.replace(/\s+/g, ' ').trim()).toBe('Rally 1 · Team A · Drive');
    click('Edit call-out for rally 1');
    const editor = element.querySelector('.call-out-editor')!;
    expect(editor.querySelector('label select')).not.toBeNull();
    expect(editor.querySelector('button')?.textContent).toContain('Close call-out editor');
    expect(button('Team A won rally').disabled).toBe(false);
    http.expectNone((request) => request.method !== 'GET');
  });

  it('shows canonical session-scoped Insights with ordered players and no shot attribution in Ended sessions', async () => {
    const state = scoredRoom();
    state.status = 'Ended';
    state.insights = {
      totalPlayers: 8,
      numberOfCourts: 2,
      completedGames: 3,
      playerAppearances: 12,
      recordedRallies: 28,
      taggedRallies: 7,
      players: [
        {
          playerId: 'b',
          displayName: 'Blair',
          gamesPlayed: 3,
          wins: 2,
          losses: 0,
          distinctTeammates: 2,
          distinctOpponents: 4,
        },
        {
          playerId: 'a',
          displayName: 'Alex',
          gamesPlayed: 2,
          wins: 0,
          losses: 1,
          distinctTeammates: 1,
          distinctOpponents: 3,
        },
      ],
    };
    await openRoom(state);
    click('Insights');
    const insights = element.querySelector('app-play-insights')!;
    const text = insights.textContent!.replace(/\s+/g, ' ');
    expect(text).toContain('8 players · 2 courts · 3 games completed');
    expect(text).toContain('12 player appearances');
    expect(text).toContain('28 recorded rallies · 7 tagged rallies');
    expect(text).toContain('recorded winners only');
    expect(Array.from(insights.querySelectorAll('thead th'), (node) => node.textContent)).toContain(
      'Wins',
    );
    expect(Array.from(insights.querySelectorAll('thead th'), (node) => node.textContent)).toContain(
      'Losses',
    );
    expect(insights.querySelector('abbr')).toBeNull();
    expect(Array.from(insights.querySelectorAll('tbody th'), (node) => node.textContent)).toEqual([
      'Blair',
      'Alex',
    ]);
    expect(
      Array.from(insights.querySelectorAll('tbody tr:first-child td'), (node) => node.textContent),
    ).toEqual(['3', '2', '0', '2', '4']);
    expect(insights.querySelector('.table-scroll')?.getAttribute('tabindex')).toBe('0');
    expect(insights.querySelector('button, input, select')).toBeNull();
    expect(text).not.toContain('Drive');
    expect(text).not.toContain('Dink');
    http.expectNone((request) => request.method !== 'GET');
  });

  it('clearly distinguishes Queue Only participation from unavailable scoring insights', async () => {
    const state = room(crew);
    state.insights = { ...state.insights, completedGames: 1, playerAppearances: 4 };
    await openRoom({ ...state, status: 'Ended' });
    click('Insights');
    const insights = element.querySelector('app-play-insights')!;
    expect(insights.textContent).toContain('4 player appearances');
    expect(insights.textContent).toContain('Score and rallies were not tracked');
    expect(insights.textContent).not.toContain('0 recorded rallies');
    expect(insights.querySelectorAll('tbody tr')).toHaveLength(5);
    http.expectNone((request) => request.method !== 'GET');
  });

  it('updates Insights from the existing live canonical session response without another data channel', async () => {
    const state = scoredRoom();
    state.insights = { ...state.insights, recordedRallies: 0, taggedRallies: 0 };
    await openRoom(state);
    click('Insights');
    expect(element.querySelector('app-play-insights')?.textContent).toContain('0 games completed');
    liveEvents.next({ kind: 'changed' });
    TestBed.tick();
    http.expectOne(`${base}/${code}`).flush({
      ...state,
      insights: {
        ...state.insights,
        completedGames: 1,
        playerAppearances: 4,
        recordedRallies: 9,
        taggedRallies: 2,
      },
    });
    await settle();
    expect(element.querySelector('app-play-insights')?.textContent).toContain('1 games completed');
    expect(element.querySelector('app-play-insights')?.textContent).toContain('9 recorded rallies');
    http.expectNone((request) => request.url.includes('insights'));
  });

  it('uses identical recent-session action slots for Resume and View details', async () => {
    const recent = TestBed.inject(RecentPlaySessions);
    recent.remember(code);
    recent.remember('GHJKLM');
    await navigate('/play');
    http.expectOne(`${base}/${code}`).flush({ ...room(), status: 'Active' });
    http
      .expectOne(`${base}/GHJKLM`)
      .flush({ ...room(), joinCode: 'GHJKLM', name: 'Finished crew', status: 'Ended' });
    await settle();
    const rows = element.querySelectorAll('app-play-recent .recent-list > article');
    expect(rows).toHaveLength(2);
    for (const row of rows) {
      expect(Array.from(row.children, (child) => child.className)).toEqual([
        'session-details',
        'session-actions',
      ]);
      expect(row.querySelector('a.session-action')?.getAttribute('href')).toContain('/play/s/');
      expect(row.querySelector('button.device-action')?.textContent?.trim()).toBe('Remove');
      expect(row.querySelector('button.device-action')?.getAttribute('aria-label')).toContain(
        'from this device',
      );
      expect(row.querySelector('.session-actions')?.children).toHaveLength(2);
    }
    const labels = Array.from(element.querySelectorAll('app-play-recent .session-action'), (node) =>
      node.textContent?.trim(),
    );
    expect(labels).toContain('Resume →');
    expect(labels).toContain('View details →');
    http.expectNone((request) => request.method !== 'GET');
  });

  it('uses short readable match times without losing overnight dates or revealing technical IDs', async () => {
    const game = summary({
      id: 'private-technical-match-id',
      startedAt: '2026-09-14T23:00:00',
      completedAt: '2026-09-15T02:00:00',
    });
    await openRoom({ ...scoredRoom(), status: 'Ended', matchHistory: [game] });
    click('Match History');
    const card = element.querySelector('app-play-match-summary')!;
    expect(card.textContent).not.toContain(game.id);
    expect(card.textContent).not.toContain('Match ID');
    expect(card.querySelector('.timing')?.textContent).toContain('Sep 14, 2026');
    expect(card.querySelector('.timing')?.textContent).toContain('Sep 15, 2026');
    expect(card.querySelector('.timing')?.textContent).not.toContain(':00:00');
    expect(card.querySelector('.final-score')?.textContent).toContain('A 11–8 B');
  });

  function completedRoom(state: PlaySession, winner: 'A' | 'B' | null = null): PlaySession {
    return {
      ...state,
      activeMatches: [],
      currentMatches: [
        {
          ...state.currentMatches[0],
          status: 'Completed',
          completedAt: joinedAt,
          winner,
          eligiblePlayers: [
            ...state.waitingQueue,
            ...state.players.filter((p) =>
              state.currentMatches[0].players.some((slot) => slot.playerId === p.id),
            ),
          ],
          teamAScore: winner === 'A' ? 11 : state.currentMatches[0].teamAScore,
          nextLineup: state.waitingQueue.slice(0, 4).map((p, i) => ({
            playerId: p.id,
            displayName: p.displayName,
            team: i < 2 ? 'A' : 'B',
            position: i + 1,
          })),
        },
      ],
    };
  }
  function scoredRoom(): PlaySession {
    const playing = crew
      .slice(0, 4)
      .map((p) => ({ ...p, state: 'Playing' as const, queueOrder: null }));
    const waiting = [
      crew[4],
      player('f', 'Frankie', 42),
      player('g', 'Gale', 43),
      player('h', 'Harper', 44),
    ];
    return {
      ...room([...playing, ...waiting], waiting),
      status: 'Active',
      mode: 'LiveScoring',
      currentMatches: [
        {
          id: 'scored-match',
          courtNumber: 1,
          status: 'Active',
          startedAt: joinedAt,
          completedAt: null,
          winner: null,
          rallies: [],
          nextLineup: [],
          eligiblePlayers: [],
          teamAScore: 3,
          teamBScore: 2,
          servingTeam: 'A',
          currentServerNumber: 1,
          players: playing.map((p, i) => ({
            playerId: p.id,
            displayName: p.displayName,
            team: i < 2 ? 'A' : 'B',
            position: i + 1,
          })),
        },
      ],
    };
  }

  it('renders canonical scores and service, sends both rally intents, and accepts live score reloads', async () => {
    let state = scoredRoom();
    await openRoom(state);
    click('Courts');
    expect(element.querySelector('[aria-label="Team A score"]')?.textContent).toBe('3');
    expect(element.querySelector('[aria-label="Team B score"]')?.textContent).toBe('2');
    expect(element.textContent).toContain('Serving: Team A · Server 1');
    click('Team A won rally');
    const first = http.expectOne(`${base}/${code}/matches/scored-match/rallies`);
    expect(first.request.body).toEqual({ winner: 'A' });
    expect(element.querySelector('[aria-label="Team A score"]')?.textContent).toBe('3');
    state = { ...state, currentMatches: [{ ...state.currentMatches[0], teamAScore: 4 }] };
    first.flush(state);
    await settle();
    click('Team B won rally');
    const second = http.expectOne(`${base}/${code}/matches/scored-match/rallies`);
    expect(second.request.body).toEqual({ winner: 'B' });
    state = { ...state, currentMatches: [{ ...state.currentMatches[0], currentServerNumber: 2 }] };
    second.flush(state);
    await settle();
    expect(element.querySelector('[aria-label="Team B score"]')?.textContent).toBe('2');
    expect(element.textContent).toContain('Serving: Team A · Server 2');
    liveEvents.next({ kind: 'changed' });
    TestBed.tick();
    state = {
      ...state,
      currentMatches: [{ ...state.currentMatches[0], servingTeam: 'B', currentServerNumber: 1 }],
    };
    http.expectOne(`${base}/${code}`).flush(state);
    await settle();
    expect(element.textContent).toContain('Serving: Team B · Server 1');
    click('Team A won rally');
    http
      .expectOne(`${base}/${code}/matches/scored-match/rallies`)
      .flush(
        { errors: { winner: ['Raw internal details'] } },
        { status: 400, statusText: 'Bad Request' },
      );
    await settle();
    expect(element.textContent).toContain('Check your details');
    expect(element.textContent).not.toContain('Raw internal details');
    expect(element.querySelector('[aria-label="Team A score"]')?.textContent).toBe('4');
  });

  it('validates a correction and holds a winning result until next game confirmation', async () => {
    let state = scoredRoom();
    await openRoom(state);
    click('Courts');
    click('Correct score');
    const form = element.querySelector<HTMLFormElement>('form[aria-label="Correct score"]')!;
    const set = (name: string, value: string) => {
      form.querySelector<HTMLInputElement | HTMLSelectElement>(`[name="${name}"]`)!.value = value;
    };
    const save = () => {
      form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
      fixture.detectChanges();
      TestBed.tick();
    };
    set('teamAScore', '-1');
    save();
    expect(element.textContent).toContain('non-negative whole scores');
    http.expectNone((request) => request.method === 'PATCH');
    set('teamAScore', '10');
    set('teamBScore', '9');
    set('servingTeam', 'A');
    set('currentServerNumber', '2');
    save();
    const correction = http.expectOne(`${base}/${code}/matches/scored-match/score`);
    expect(correction.request.method).toBe('PATCH');
    expect(correction.request.body).toEqual({
      teamAScore: 10,
      teamBScore: 9,
      servingTeam: 'A',
      currentServerNumber: 2,
    });
    state = {
      ...state,
      currentMatches: [{ ...state.currentMatches[0], ...correction.request.body }],
    };
    correction.flush(state);
    await settle();
    expect(element.querySelector('app-play-score-editor')).toBeNull();
    expect(element.querySelector('[aria-label="Team A score"]')?.textContent).toBe('10');
    click('Team A won rally');
    const next = state.waitingQueue;
    const rotated: PlaySession = {
      ...state,
      waitingQueue: crew.slice(0, 4),
      players: [
        ...crew.slice(0, 4),
        ...next.map((p) => ({ ...p, state: 'Playing' as const, queueOrder: null })),
      ],
      currentMatches: [
        {
          ...state.currentMatches[0],
          id: 'next-match',
          teamAScore: 0,
          teamBScore: 0,
          servingTeam: 'A',
          currentServerNumber: 2,
          players: next.map((p, i) => ({
            playerId: p.id,
            displayName: p.displayName,
            team: i < 2 ? 'A' : 'B',
            position: i + 1,
          })),
        },
      ],
    };
    const completed = completedRoom(state, 'A');
    http.expectOne(`${base}/${code}/matches/scored-match/rallies`).flush(completed);
    await settle();
    expect(element.textContent).toContain('Team A wins');
    expect(element.querySelector('[aria-label="Team A score"]')?.textContent).toBe('11');
    expect(element.querySelector('article[aria-label="Court 1"]')?.textContent).toContain('Alex');
    expect(element.querySelector('.rally-actions')).toBeNull();
    click('Proceed to next game');
    click('Start Next Game');
    http.expectOne(`${base}/${code}/matches/scored-match/next`).flush(rotated);
    await settle();
    expect(element.querySelector('article[aria-label="Court 1"]')?.textContent).toContain('Ellis');
    expect(element.querySelector('[aria-label="Team A score"]')?.textContent).toBe('0');
    http.expectNone((request) => request.url.endsWith('/finish'));
    click('Queue 4');
    expect(queueNames()).toEqual(['Alex', 'Blair', 'Casey', 'Drew']);
  });
  it('removes a recent entry only from this device without a mutation request', async () => {
    TestBed.inject(RecentPlaySessions).remember(code);
    await navigate('/play');
    http.expectOne(`${base}/${code}`).flush(room());
    await settle();
    click('Remove Evening crew from this device');
    expect(TestBed.inject(RecentPlaySessions).list()).toEqual([]);
    expect(element.querySelector('app-play-recent h3')).toBeNull();
    expect(element.textContent).toContain('session remains available');
    http.expectNone(() => true);
  });

  it('defaults to Queue Only and submits the selected Live Scoring mode', async () => {
    await navigate('/play/new');
    expect(element.querySelector<HTMLInputElement>('[value="QueueOnly"]')?.checked).toBe(true);
    element.querySelector<HTMLInputElement>('[value="LiveScoring"]')!.click();
    fill('start-time', '18:00');
    fill('end-time', '21:00');
    submit();
    const create = http.expectOne(base);
    expect(create.request.body.mode).toBe('LiveScoring');
    create.flush({ ...room(), mode: 'LiveScoring' });
    await settle();
    http.expectOne(`${base}/${code}`).flush({ ...room(), mode: 'LiveScoring' });
    await settle();
  });

  it('groups Draft name, status and compact actions together without break controls', async () => {
    await openRoom(room(crew));
    const identity = element.querySelector('app-play-player-list li .identity')!;
    expect(identity.querySelector('strong')?.textContent).toBe('Alex');
    const metadata = identity.querySelector('.player-meta')!;
    expect(metadata.querySelector('.state')?.textContent).toContain('Waiting');
    expect(metadata.querySelector('[aria-label="Edit Alex"]')?.tagName).toBe('BUTTON');
    expect(metadata.querySelector('[aria-label="Remove Alex"]')?.tagName).toBe('BUTTON');
    expect(element.querySelector('[aria-label^="Take a break"]')).toBeNull();
    expect(element.querySelector('[aria-label^="Rejoin queue"]')).toBeNull();
    click('Edit Alex');
    expect(identity.querySelector('form[aria-label="Edit Alex"] input')).not.toBeNull();
    click('Cancel');
    click('Remove Alex');
    expect(identity.querySelector('[role="group"][aria-label="Remove Alex?"]')).not.toBeNull();
    http.expectNone((request) => request.method !== 'GET');
  });

  it('edits all Draft settings with an overnight range and accepts canonical state', async () => {
    await openRoom(room(crew));
    click('Edit Session');
    expect(element.querySelector('form[aria-label="Edit session details"]')).not.toBeNull();
    expect(element.querySelector<HTMLInputElement>('#end-date')?.value).toBe('2026-09-10');
    fill('session-name', ' Midnight crew ');
    fill('session-date', '2026-09-14');
    fill('start-time', '23:00');
    fill('end-date', '2026-09-15');
    fill('end-time', '02:00');
    fill('court-count', '3');
    fill('player-limit', '12');
    const mode = element.querySelector<HTMLInputElement>('[value="LiveScoring"]')!;
    mode.checked = true;
    mode.dispatchEvent(new Event('change', { bubbles: true }));
    submit();
    const edit = http.expectOne(`${base}/${code}`);
    expect(edit.request.method).toBe('PATCH');
    expect(edit.request.body).toEqual({
      name: 'Midnight crew',
      date: '2026-09-14',
      startTime: '23:00:00',
      endDate: '2026-09-15',
      endTime: '02:00:00',
      numberOfCourts: 3,
      maximumPlayers: 12,
      mode: 'LiveScoring',
    });
    edit.flush({
      ...room(crew),
      name: 'Midnight crew',
      sessionDate: '2026-09-14',
      startTime: '23:00:00',
      endDate: '2026-09-15',
      endTime: '02:00:00',
      numberOfCourts: 3,
      maximumPlayers: 12,
      mode: 'LiveScoring',
    });
    await settle();
    expect(element.querySelector('app-play-session-form')).toBeNull();
    expect(element.querySelector('app-play-session-header')?.textContent).toContain(
      'Midnight crew',
    );
    expect(queueNames()).toEqual(crew.map((p) => p.displayName));
    http.expectNone(`${base}/${code}`);
  });

  it('keeps Draft edits after a roster-limit conflict and removes structural editing when Active', async () => {
    await openRoom(room(crew));
    click('Edit Session');
    fill('session-name', 'Keep this draft');
    fill('player-limit', '4');
    submit();
    http
      .expectOne(`${base}/${code}`)
      .flush(
        { title: 'Maximum players cannot be lower than the current roster size.' },
        { status: 409, statusText: 'Conflict' },
      );
    http.expectOne(`${base}/${code}`).flush(room(crew));
    await settle();
    expect(element.textContent).toContain('must include everyone currently on the roster');
    expect(element.querySelector<HTMLInputElement>('#session-name')?.value).toBe('Keep this draft');
    click('Cancel');
    click('Start Session');
    http.expectOne(`${base}/${code}/start`).flush({ ...room(crew), status: 'Active' });
    await settle();
    expect(element.querySelector('app-play-session-form')).toBeNull();
    expect(element.textContent).not.toContain('Edit Session');
    expect(element.querySelector('[aria-label="Take a break for Alex"]')).not.toBeNull();
  });

  it('validates complete dates and creates a session that crosses midnight', async () => {
    await navigate('/play/new');
    fill('session-date', '2026-09-14');
    fill('start-time', '23:00');
    fill('end-time', '02:00');
    submit();
    expect(element.querySelector('#end-help')?.textContent).toContain('after start date and time');
    http.expectNone(base);
    fill('end-date', '2026-09-15');
    submit();
    const create = http.expectOne(base);
    expect(create.request.body.endDate).toBe('2026-09-15');
    expect(create.request.body.startTime).toBe('23:00:00');
    expect(create.request.body.endTime).toBe('02:00:00');
    create.flush({ ...room(), endDate: '2026-09-15' });
    await settle();
    http.expectOne(`${base}/${code}`).flush({ ...room(), endDate: '2026-09-15' });
    await settle();
  });

  it('edits Draft names inline and confirms roster removal', async () => {
    await openRoom(room(crew.slice(0, 2)));
    click('Edit Alex');
    const editor = element.querySelector<HTMLFormElement>('form[aria-label="Edit Alex"]')!;
    const input = editor.querySelector<HTMLInputElement>('input')!;
    input.value = 'Alec';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    editor.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    fixture.detectChanges();
    const rename = http.expectOne(`${base}/${code}/players/a`);
    expect(rename.request.method).toBe('PATCH');
    expect(rename.request.body).toEqual({ displayName: 'Alec' });
    rename.flush(room([{ ...crew[0], displayName: 'Alec' }, crew[1]]));
    await settle();
    expect(queueNames()).toEqual(['Alec', 'Blair']);
    click('Cancel');
    click('Remove Blair');
    http.expectNone(() => true);
    click('Cancel');
    click('Remove Blair');
    element
      .querySelector<HTMLButtonElement>(
        '[role="group"][aria-label="Remove Blair?"] button:last-child',
      )!
      .click();
    const remove = http.expectOne(`${base}/${code}/players/b`);
    expect(remove.request.method).toBe('DELETE');
    remove.flush(room([{ ...crew[0], displayName: 'Alec' }]));
    await settle();
    expect(queueNames()).toEqual(['Alec']);
    click('Start Session');
    http
      .expectOne(`${base}/${code}/start`)
      .flush({ ...room([{ ...crew[0], displayName: 'Alec' }]), status: 'Active' });
    await settle();
    expect(element.querySelector('app-play-draft-player')).toBeNull();
  });

  it('hides Queue Only scoring and requires End Session confirmation before becoming read-only', async () => {
    const state = { ...scoredRoom(), mode: 'QueueOnly' as const };
    await openRoom(state);
    click('Courts');
    expect(element.querySelector('.score')).toBeNull();
    expect(element.querySelector('.service')).toBeNull();
    expect(element.querySelector('.rally-actions')).toBeNull();
    expect(element.textContent).not.toContain('Correct score');
    click('End Session');
    http.expectNone(`${base}/${code}/end`);
    click('Cancel');
    click('End Session');
    click('End Session');
    http.expectOne(`${base}/${code}/end`).flush({ ...state, status: 'Ended' });
    await settle();
    expect(element.textContent).toContain('This session has ended');
    expect(element.querySelector('[aria-label="Finish game on Court 1"]')).toBeNull();
    expect(element.querySelector<HTMLInputElement>('#guest-name')?.matches(':disabled')).toBe(true);
    expect(element.querySelector('article[aria-label="Court 1"]')?.textContent).toContain('Alex');
  });

  it('edits all eligible slots, rejects duplicates, resets recommendation and preserves a continuing player override', async () => {
    const state = completedRoom(scoredRoom());
    await openRoom(state);
    click('Courts');
    click('Proceed to next game');
    click('Edit lineup');
    const selects = Array.from(
      element.querySelectorAll<HTMLSelectElement>('app-play-next-game select'),
    );
    expect(selects).toHaveLength(4);
    expect(Array.from(selects[0].options).map((option) => option.value)).toEqual([
      '',
      'e',
      'f',
      'g',
      'h',
      'a',
      'b',
      'c',
      'd',
    ]);
    selects[0].value = 'f';
    selects[0].dispatchEvent(new Event('change'));
    fixture.detectChanges();
    click('Start Next Game');
    http.expectNone((request) => request.method === 'POST');
    expect(element.querySelector('app-play-next-game [role="alert"]')).not.toBeNull();
    click('Reset to fair rotation');
    expect(element.querySelector('app-play-next-game select')).toBeNull();
    click('Edit lineup');
    const changed = element.querySelectorAll<HTMLSelectElement>('app-play-next-game select');
    changed[0].value = 'a';
    changed[0].dispatchEvent(new Event('change'));
    changed[1].value = 'e';
    changed[1].dispatchEvent(new Event('change'));
    fixture.detectChanges();
    click('Start Next Game');
    const next = http.expectOne(`${base}/${code}/matches/scored-match/next`);
    expect(next.request.body).toEqual({ playerIds: ['a', 'e', 'g', 'h'], overrideLineup: true });
    next.flush(
      { title: 'The next lineup has changed. Refresh and choose four eligible players.' },
      { status: 409, statusText: 'Conflict' },
    );
    const stale = {
      ...state,
      waitingQueue: state.waitingQueue.slice(1),
      currentMatches: [
        {
          ...state.currentMatches[0],
          nextLineup: state.currentMatches[0].nextLineup.slice(1),
          eligiblePlayers: state.currentMatches[0].eligiblePlayers.filter((p) => p.id !== 'a'),
        },
      ],
    };
    http.expectOne(`${base}/${code}`).flush(stale);
    await settle();
    expect(element.textContent).toContain('no longer eligible');
    click('Start Next Game');
    http.expectNone((request) => request.method === 'POST');
    expect(element.textContent).toContain('Choose four distinct eligible players');
    click('Reset to fair rotation');
    expect(element.textContent).toContain('Fair rotation needs four eligible players');
    expect(element.querySelector('article[aria-label="Court 1"]')?.textContent).toContain('Alex');
    expect(element.querySelector('app-play-next-game .primary')).toBeNull();
  });

  it('shows a completed game and then an ended read-only state through canonical live refetch', async () => {
    const active = scoredRoom();
    await openRoom(active);
    click('Courts');
    liveEvents.next({ kind: 'changed' });
    TestBed.tick();
    const completed = completedRoom(active, 'A');
    http.expectOne(`${base}/${code}`).flush(completed);
    await settle();
    expect(element.textContent).toContain('Team A wins');
    expect(element.querySelector('.rally-actions')).toBeNull();
    click('Players 8');
    expect(element.textContent).toMatch(/Game complete\s+·\s+Court 1/);
    click('Courts');
    liveEvents.next({ kind: 'changed' });
    TestBed.tick();
    http.expectOne(`${base}/${code}`).flush({ ...completed, status: 'Ended' });
    await settle();
    expect(element.textContent).toContain('Team A wins');
    expect(element.querySelector('app-play-next-game')).toBeNull();
    http.expectNone((request) => request.method !== 'GET');
  });
  for (const winner of ['A', 'B'] as const) {
    it(`captures Queue Only Team ${winner} result after a cancellable chooser`, async () => {
      const active = { ...scoredRoom(), mode: 'QueueOnly' as const };
      await openRoom(active);
      click('Courts');
      click('Finish game on Court 1');
      expect(element.textContent).toContain('Who won this game?');
      expect(element.querySelector('.confirmation')?.textContent).toContain('Alex');
      expect(element.querySelector('.confirmation input')).toBeNull();
      click('Cancel');
      expect(element.querySelector('.confirmation')).toBeNull();
      http.expectNone((request) => request.method !== 'GET');
      click('Finish game on Court 1');
      click(`Team ${winner} won`);
      const request = http.expectOne(`${base}/${code}/matches/scored-match/finish`);
      expect(request.request.body).toEqual({ winner });
      request.flush({
        ...completedRoom(active, winner),
        matchHistory: [
          summary({
            winner,
            teamAScore: null,
            teamBScore: null,
            totalRallies: null,
            taggedRallies: null,
            rallies: [],
            callOutCounts: [],
          }),
        ],
      });
      await settle();
      expect(element.textContent).toContain(`Team ${winner} wins`);
      expect(element.querySelector('.score')).toBeNull();
      click('Match History');
      expect(element.textContent).toContain(`Team ${winner} won`);
      expect(element.querySelector('.final-score')).toBeNull();
      expect(element.textContent).toContain('Queue Only · Score and rallies were not tracked.');
    });
  }

  it('finishes Queue Only without scores or an invented winner and holds the current players', async () => {
    const active = { ...scoredRoom(), mode: 'QueueOnly' as const };
    await openRoom(active);
    click('Courts');
    expect(button('Finish game on Court 1').textContent?.trim()).toBe('Finish Game');
    click('Finish game on Court 1');
    click('Finish without result');
    const request = http.expectOne(`${base}/${code}/matches/scored-match/finish`);
    expect(request.request.body).toEqual({ winner: null });
    request.flush(completedRoom(active));
    await settle();
    expect(element.textContent).toContain('Game complete');
    expect(element.textContent).not.toContain('wins');
    expect(element.querySelector('.score')).toBeNull();
    expect(element.querySelector('article[aria-label="Court 1"]')?.textContent).toContain('Alex');
    click('Proceed to next game');
    expect(element.querySelector('app-play-next-game')?.textContent).toContain('Ellis');
    http.expectNone((request) => request.method === 'POST');
  });
  it('uses the backend recommendation on reset and excludes other-court and resting players from overrides', async () => {
    const state = completedRoom(scoredRoom());
    const otherPlayers = ['i', 'j', 'k', 'l'].map((id, i) =>
      player(id, `Other ${i}`, null, 'Playing'),
    );
    const resting = player('r', 'Resting', null, 'Resting');
    const deeper = player('z', 'Later in queue', 50);
    const otherMatch = {
      ...scoredRoom().currentMatches[0],
      id: 'other-match',
      courtNumber: 2,
      teamAScore: 7,
      teamBScore: 8,
      players: otherPlayers.map((p, i) => ({
        playerId: p.id,
        displayName: p.displayName,
        team: i < 2 ? ('A' as const) : ('B' as const),
        position: i + 1,
      })),
    };
    state.players.push(...otherPlayers, resting, deeper);
    state.waitingQueue.push(deeper);
    state.currentMatches[0].eligiblePlayers.push(deeper);
    state.currentMatches.push(otherMatch);
    const proposal = state.currentMatches[0].nextLineup;
    state.currentMatches[0].nextLineup = [proposal[0], proposal[2], proposal[1], proposal[3]].map(
      (p, i) => ({ ...p, position: i + 1, team: i < 2 ? 'A' : 'B' }),
    );
    await openRoom(state);
    click('Courts');
    const otherBefore = element.querySelector('article[aria-label="Court 2"]')!.textContent;
    click('Proceed to next game');
    click('Edit lineup');
    const select = element.querySelector<HTMLSelectElement>('app-play-next-game select')!;
    const options = Array.from(select.options).map((option) => option.value);
    expect(options).toContain('a');
    expect(options).toContain('z');
    expect(options).not.toContain('i');
    expect(options).not.toContain('r');
    select.value = 'a';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    click('Reset to fair rotation');
    expect(
      Array.from(element.querySelectorAll('app-play-next-game strong')).map(
        (node) => node.textContent,
      ),
    ).toEqual(['Ellis', 'Gale', 'Frankie', 'Harper']);
    click('Cancel');
    expect(element.querySelector('article[aria-label="Court 2"]')!.textContent).toBe(otherBefore);
    expect(element.querySelector('article[aria-label="Court 1"]')!.textContent).toContain(
      'Game complete',
    );
    click('Proceed to next game');
    click('Start Next Game');
    const next = http.expectOne(`${base}/${code}/matches/scored-match/next`);
    expect(next.request.body).toEqual({ playerIds: ['e', 'g', 'f', 'h'], overrideLineup: false });
    const nextPlayers = state.currentMatches[0].nextLineup;
    next.flush({
      ...state,
      currentMatches: [
        {
          ...scoredRoom().currentMatches[0],
          id: 'confirmed',
          players: nextPlayers,
          teamAScore: 0,
          teamBScore: 0,
          currentServerNumber: 2,
        },
        otherMatch,
      ],
    });
    await settle();
    expect(element.querySelector('article[aria-label="Court 2"]')!.textContent).toBe(otherBefore);
  });

  it('allows four completed players to continue by explicit override when nobody is waiting', async () => {
    const state = completedRoom(scoredRoom());
    state.players = state.players.filter((p) => p.state === 'Playing');
    state.waitingQueue = [];
    state.currentMatches[0].nextLineup = [];
    state.currentMatches[0].eligiblePlayers = state.players;
    await openRoom(state);
    click('Courts');
    click('Proceed to next game');
    expect(element.textContent).toContain('Fair rotation needs four eligible players');
    click('Edit lineup');
    const selects = element.querySelectorAll<HTMLSelectElement>('app-play-next-game select');
    ['a', 'b', 'c', 'd'].forEach((id, i) => {
      selects[i].value = id;
      selects[i].dispatchEvent(new Event('change'));
    });
    fixture.detectChanges();
    click('Start Next Game');
    const next = http.expectOne(`${base}/${code}/matches/scored-match/next`);
    expect(next.request.body).toEqual({ playerIds: ['a', 'b', 'c', 'd'], overrideLineup: true });
    next.flush({
      ...state,
      currentMatches: [
        {
          ...state.currentMatches[0],
          id: 'continued',
          status: 'Active',
          winner: null,
          completedAt: null,
          teamAScore: 0,
          teamBScore: 0,
          servingTeam: 'A',
          currentServerNumber: 2,
          eligiblePlayers: [],
        },
      ],
    });
    await settle();
    expect(element.querySelector('article[aria-label="Court 1"]')!.textContent).toContain('Alex');
    expect(element.querySelector('app-play-next-game')).toBeNull();
    expect(element.querySelector('[aria-label="Team A score"]')?.textContent).toBe('0');
  });

  it('holds a winning score correction until the host confirms a next game', async () => {
    const state = scoredRoom();
    await openRoom(state);
    click('Courts');
    click('Correct score');
    const form = element.querySelector<HTMLFormElement>('form[aria-label="Correct score"]')!;
    form.querySelector<HTMLInputElement>('[name="teamAScore"]')!.value = '11';
    form.querySelector<HTMLInputElement>('[name="teamBScore"]')!.value = '9';
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    fixture.detectChanges();
    const correction = http.expectOne(`${base}/${code}/matches/scored-match/score`);
    expect(correction.request.body.teamAScore).toBe(11);
    correction.flush(completedRoom(state, 'A'));
    await settle();
    expect(element.textContent).toContain('Team A wins');
    expect(element.querySelector('app-play-score-editor')).toBeNull();
    expect(element.querySelector('article[aria-label="Court 1"]')!.textContent).toContain('Alex');
    click('Proceed to next game');
    click('Cancel');
    http.expectNone((request) => request.method === 'POST');
    expect(element.querySelector('[aria-label="Team A score"]')?.textContent).toBe('11');
  });
});
