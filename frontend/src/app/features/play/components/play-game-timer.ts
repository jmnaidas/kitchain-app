import { ChangeDetectionStrategy, Component, computed, effect, input, signal } from '@angular/core';
import { PlayMatch } from '../data-access/play.models';

// Recalculate from absolute time, rather than decrementing a counter: background tab
// throttling, refresh and canonical refetch cannot reset or accumulate timer drift.
export function gameTimer(
  match: Pick<PlayMatch, 'startedAt' | 'timerDurationMinutes'>,
  now: number,
) {
  if (match.timerDurationMinutes == null || !match.startedAt) return null;
  const remaining = Math.ceil(
    (Date.parse(match.startedAt) + match.timerDurationMinutes * 60_000 - now) / 1000,
  );
  const seconds = Math.abs(remaining);
  return {
    expired: remaining <= 0,
    urgent: remaining <= 60,
    text: `${remaining <= 0 ? '+' : ''}${Math.floor(seconds / 60)
      .toString()
      .padStart(2, '0')}:${(seconds % 60).toString().padStart(2, '0')}`,
  };
}

@Component({
  selector: 'app-play-game-timer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (display(); as timer) {
      <div
        role="timer"
        aria-live="off"
        [attr.aria-label]="'Game timer on Court ' + match().courtNumber"
        [class.urgent]="timer.urgent"
      >
        @if (timer.expired) {
          <strong>TIME</strong>
        }
        <span>{{ timer.text }}</span>
      </div>
    } @else {
      <small>No limit</small>
    }
  `,
  styles: `
    :host {
      display: block;
      margin: 0.75rem 0;
    }
    div {
      display: flex;
      align-items: baseline;
      gap: 0.5rem;
      font-variant-numeric: tabular-nums;
    }
    span {
      font-size: 1.4rem;
      font-weight: 700;
    }
    .urgent {
      color: #97361f;
    }
  `,
})
export class PlayGameTimer {
  readonly match = input.required<PlayMatch>();
  private readonly now = signal(Date.now());
  protected readonly display = computed(() => gameTimer(this.match(), this.now()));
  constructor() {
    effect((cleanup) => {
      const match = this.match();
      this.now.set(Date.now());
      if (match.status !== 'Active' || match.timerDurationMinutes == null) return;
      const tick = setInterval(() => this.now.set(Date.now()), 1000);
      cleanup(() => clearInterval(tick));
    });
  }
}
