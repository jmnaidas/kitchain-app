import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import {
  PlayMatchSummary as MatchSummary,
  callOutLabel,
  PlayTeam,
} from '../data-access/play.models';
import { PlayRallyHistory } from './play-rally-history';

@Component({
  selector: 'app-play-match-summary',
  imports: [DatePipe, PlayRallyHistory],
  templateUrl: './play-match-summary.html',
  styleUrl: './play-match-summary.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayMatchSummary {
  readonly match = input.required<MatchSummary>();
  readonly sequence = input<number | null>(null);
  protected readonly label = callOutLabel;
  protected readonly expanded = signal(false);
  protected readonly sameDay = computed(() => {
    const match = this.match();
    return (
      match.completedAt !== null &&
      new Date(match.startedAt).toDateString() === new Date(match.completedAt).toDateString()
    );
  });
  protected teamNames(team: PlayTeam) {
    return this.match()
      .players.filter((p) => p.team === team)
      .map((p) => p.displayName)
      .join(' + ');
  }
}
