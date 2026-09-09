import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { PlaySession } from '../data-access/play.models';

@Component({
  selector: 'app-play-courts',
  templateUrl: './play-courts.html',
  styleUrl: './play-courts.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayCourts {
  readonly session = input.required<PlaySession>();
  readonly disabled = input(false);
  readonly finish = output<string>();
  protected readonly confirming = signal<string | null>(null);
  protected readonly page = signal(0);
  protected readonly courts = computed(() => {
    const session = this.session();
    const start = this.page() * 8;
    return Array.from(
      { length: Math.max(0, Math.min(8, session.numberOfCourts - start)) },
      (_, i) => {
        const number = start + i + 1;
        const match = session.activeMatches.find((match) => match.courtNumber === number);
        return {
          number,
          match,
          teamA: match?.players.filter((p) => p.team === 'A') ?? [],
          teamB: match?.players.filter((p) => p.team === 'B') ?? [],
        };
      },
    );
  });

  protected confirm(id: string) {
    if (this.disabled() || this.session().status !== 'Active') return;
    this.confirming.set(null);
    this.finish.emit(id);
  }
}
