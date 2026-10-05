import { TestBed } from '@angular/core/testing';
import { PlayMatchSummary } from './play-match-summary';
import { PlayMatchSummary as Summary } from '../data-access/play.models';

const start = Date.parse('2026-10-05T10:00:00Z');
const summary = (seconds: number): Summary => ({
  id: 'completed',
  courtNumber: 1,
  players: [],
  startedAt: new Date(start).toISOString(),
  completedAt: new Date(start + seconds * 1000).toISOString(),
  teamAScore: null,
  teamBScore: null,
  winner: null,
  totalRallies: null,
  taggedRallies: null,
  callOutCounts: [],
  rallies: [],
});

describe('completed game duration', () => {
  it.each([
    [15, 900, '15m 00s'],
    [15, 763, '12m 43s'],
    [15, 1041, '17m 21s'],
    [null, 3852, '1h 04m 12s'],
    [null, 3600, '1h 00m 00s'],
  ])('uses actual timestamps with timer %s and elapsed %s', (timer, seconds, expected) => {
    const fixture = TestBed.createComponent(PlayMatchSummary);
    fixture.componentRef.setInput('match', { ...summary(seconds), timerDurationMinutes: timer });
    fixture.detectChanges();
    expect(
      fixture.nativeElement.querySelector('[aria-label="Game duration"]').textContent,
    ).toContain(expected);
    fixture.destroy();
    // A fresh view uses exactly the same canonical history, without a running clock.
    const refreshed = TestBed.createComponent(PlayMatchSummary);
    refreshed.componentRef.setInput('match', JSON.parse(JSON.stringify(summary(seconds))));
    refreshed.detectChanges();
    expect(refreshed.nativeElement.textContent).toContain(expected);
  });

  it.each([{ startedAt: null }, { completedAt: null }, { startedAt: '2026-10-05T11:00:00Z' }])(
    'omits duration when timing data is missing or reversed: %j',
    (timestamps) => {
      const fixture = TestBed.createComponent(PlayMatchSummary);
      fixture.componentRef.setInput('match', { ...summary(763), ...timestamps });
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('[aria-label="Game duration"]')).toBeNull();
      expect(fixture.nativeElement.textContent).not.toContain('NaN');
    },
  );
});
