import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { PlayNextRound, PlayPlannerAction } from '../data-access/play.models';

@Component({
  selector: 'app-play-round-planner',
  templateUrl: './play-round-planner.html',
  styleUrl: './play-next-game.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayRoundPlanner {
  readonly plan = input.required<PlayNextRound>();
  readonly disabled = input(false);
  readonly action = output<PlayPlannerAction>();
  protected readonly available = computed(() => {
    const assigned = new Set(this.plan().courts.flatMap((c) => c.players.map((p) => p.playerId)));
    return this.plan().eligiblePlayers.filter((p) => !assigned.has(p.id));
  });
  protected select(matchId: string, position: number, event: Event) {
    if (this.disabled()) return;
    const select = event.target as HTMLSelectElement;
    const playerId = select.value;
    select.value = this.plan()
      .courts.find((c) => c.matchId === matchId)!
      .players.find((p) => p.position === position)!.playerId;
    this.action.emit({
      action: 'edit',
      matchId,
      position,
      playerId,
      expectedRevision: this.plan().revision,
    });
  }
  protected emit(action: 'finalize' | 'reset') {
    if (!this.disabled()) this.action.emit({ action, expectedRevision: this.plan().revision });
  }
}
