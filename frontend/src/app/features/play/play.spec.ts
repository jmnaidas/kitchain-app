import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { App } from '../../app';
import { routes } from '../../app.routes';
import { localDate, PlayPlayer, PlaySession } from './data-access/play.models';
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
  id: 'session-id',
  joinCode: code,
  name: 'Evening crew',
  sessionDate: '2026-09-10',
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
  activeMatches: [],
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
      activeMatches: [
        {
          id: 'match-1',
          courtNumber: 1,
          status: 'Active',
          startedAt: joinedAt,
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
    expect(element.querySelectorAll('.next-up li')).toHaveLength(4);
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
      activeMatches: [
        {
          ...active.activeMatches[0],
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
    finish.flush(returned);
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
      startTime: '18:00:00',
      endTime: '21:00:00',
      numberOfCourts: 2,
      maximumPlayers: null,
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

  it('shows loading then the server queue order, with only its first four shown as Next Up', async () => {
    await navigate(`/play/s/${code}`);
    expect(element.textContent).toContain('Loading your session');
    const authoritative = [crew[2], crew[0], crew[4], crew[1], crew[3]];
    http.expectOne(`${base}/${code}`).flush(room(crew, authoritative));
    await settle();
    expect(queueNames()).toEqual(['Casey', 'Alex', 'Ellis', 'Blair', 'Drew']);
    expect(element.querySelectorAll('.next-up app-play-player-list li')).toHaveLength(4);
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
    expect(element.textContent).toContain('3 more players');
  });

  it('takes a break using the returned session without optimistic reordering', async () => {
    await openRoom(room(crew));
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
    await openRoom(room([resting, crew[1], playing], [crew[1]]));
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
      activeMatches: [
        {
          id: 'scored-match',
          courtNumber: 1,
          status: 'Active',
          startedAt: joinedAt,
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
    state = { ...state, activeMatches: [{ ...state.activeMatches[0], teamAScore: 4 }] };
    first.flush(state);
    await settle();
    click('Team B won rally');
    const second = http.expectOne(`${base}/${code}/matches/scored-match/rallies`);
    expect(second.request.body).toEqual({ winner: 'B' });
    state = { ...state, activeMatches: [{ ...state.activeMatches[0], currentServerNumber: 2 }] };
    second.flush(state);
    await settle();
    expect(element.querySelector('[aria-label="Team B score"]')?.textContent).toBe('2');
    expect(element.textContent).toContain('Serving: Team A · Server 2');
    liveEvents.next({ kind: 'changed' });
    TestBed.tick();
    state = {
      ...state,
      activeMatches: [{ ...state.activeMatches[0], servingTeam: 'B', currentServerNumber: 1 }],
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

  it('validates a correction, saves the repair, then renders automatic game rotation without a finish click', async () => {
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
      activeMatches: [{ ...state.activeMatches[0], ...correction.request.body }],
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
      activeMatches: [
        {
          ...state.activeMatches[0],
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
    http.expectOne(`${base}/${code}/matches/scored-match/rallies`).flush(rotated);
    await settle();
    expect(element.querySelector('article[aria-label="Court 1"]')?.textContent).toContain('Ellis');
    expect(element.querySelector('[aria-label="Team A score"]')?.textContent).toBe('0');
    http.expectNone((request) => request.url.endsWith('/finish'));
    click('Queue 4');
    expect(queueNames()).toEqual(['Alex', 'Blair', 'Casey', 'Drew']);
  });
});
