import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { PlayMatch, PlayScoreCorrection, PlaySession, PlayTeam } from '../data-access/play.models';
import { PlayScoreEditor } from './play-score-editor';

@Component({
  selector: 'app-play-courts',
  imports: [PlayScoreEditor],
  templateUrl: './play-courts.html',
  styleUrl: './play-courts.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayCourts {
  readonly session = input.required<PlaySession>();
  readonly disabled = input(false);
  readonly finish = output<string>();
  readonly rally = output<{ matchId: string; winner: PlayTeam }>();
  readonly correction = output<{ matchId: string; score: PlayScoreCorrection }>();
  readonly correctionSaved = input(0);
  protected readonly editing = signal<PlayMatch | null>(null);
  protected readonly confirming = signal<string | null>(null);
  protected readonly page = signal(0);
  constructor() {
    effect(() => {
      this.correctionSaved();
      untracked(() => this.editing.set(null));
    });
  }

  protected recordRally(matchId: string, winner: PlayTeam) {
    if (this.disabled() || this.session().status !== 'Active') return;
    this.rally.emit({ matchId, winner });
  }
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
