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
  protected readonly duration = computed(() => {
    const { startedAt, completedAt } = this.match();
    if (!startedAt || !completedAt) return null;
    const elapsed = Date.parse(completedAt) - Date.parse(startedAt);
    if (!Number.isFinite(elapsed) || elapsed < 0) return null;
    const seconds = Math.floor(elapsed / 1000);
    const minutes = Math.floor(seconds / 60);
    const remainder = `${(seconds % 60).toString().padStart(2, '0')}s`;
    return minutes < 60
      ? `${minutes}m ${remainder}`
      : `${Math.floor(minutes / 60)}h ${(minutes % 60).toString().padStart(2, '0')}m ${remainder}`;
  });
  protected readonly sameDay = computed(() => {
    const match = this.match();
    return (
      !!match.startedAt &&
      !!match.completedAt &&
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
