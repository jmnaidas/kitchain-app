import { TestBed } from '@angular/core/testing';
import { PlayGameTimer, gameTimer } from './play-game-timer';
import { PlayMatch } from '../data-access/play.models';

const startedAt = '2026-10-05T10:00:00Z';
const match = (minutes: number | null = 15, courtNumber = 1): PlayMatch => ({
  id: `game-${courtNumber}`,
  courtNumber,
  status: 'Active',
  startedAt,
  timerDurationMinutes: minutes,
  completedAt: null,
  winner: null,
  nextLineup: [],
  eligiblePlayers: [],
  players: [],
  rallies: [],
  teamAScore: 0,
  teamBScore: 0,
  servingTeam: 'A',
  currentServerNumber: 2,
});

describe('canonical game timer', () => {
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.useRealTimers();
  });

  it('derives remaining time, TIME and overtime from the persisted start', () => {
    const start = Date.parse(startedAt);
    expect(gameTimer(match(), start + 1000)?.text).toBe('14:59');
    expect(gameTimer(match(), start + 900_000)).toEqual({
      text: '+00:00',
      expired: true,
      urgent: true,
    });
    expect(gameTimer(match(), start + 997_000)?.text).toBe('+01:37');
    expect(gameTimer(match(null), start + 997_000)).toBeNull();
  });

  it('ticks through expiry, corrects on refetch, and cleans up on destruction', async () => {
    vi.useFakeTimers();
    vi.setSystemTime(Date.parse(startedAt) + 899_000);
    const fixture = TestBed.createComponent(PlayGameTimer);
    fixture.componentRef.setInput('match', match());
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('00:01');
    vi.advanceTimersByTime(2000);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('TIME');
    expect(fixture.nativeElement.textContent).toContain('+00:01');
    fixture.componentRef.setInput('match', {
      ...match(),
      startedAt: new Date(Date.now()).toISOString(),
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('15:00');
    expect(fixture.nativeElement.textContent).not.toContain('TIME');
    fixture.destroy();
    expect(vi.getTimerCount()).toBe(0);
  });

  it('refresh and separate court instances retain independent canonical timers', () => {
    vi.useFakeTimers();
    vi.setSystemTime(Date.parse(startedAt) + 11 * 60_000);
    const first = TestBed.createComponent(PlayGameTimer);
    const second = TestBed.createComponent(PlayGameTimer);
    first.componentRef.setInput('match', match(10));
    second.componentRef.setInput('match', match(20, 2));
    first.detectChanges();
    second.detectChanges();
    expect(first.nativeElement.textContent).toContain('+01:00');
    expect(second.nativeElement.textContent).toContain('09:00');
    first.destroy();
    const refreshed = TestBed.createComponent(PlayGameTimer);
    refreshed.componentRef.setInput('match', match(10));
    refreshed.detectChanges();
    expect(refreshed.nativeElement.textContent).toContain('+01:00');
    second.componentRef.setInput('match', match(null, 2));
    second.detectChanges();
    expect(second.nativeElement.textContent).toContain('No limit');
    expect(second.nativeElement.querySelector('[role="timer"]')).toBeNull();
  });
});
