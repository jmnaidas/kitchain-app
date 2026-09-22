import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import {
  NextPlayGame,
  PlayMatch,
  PlayRotationMode,
  rotationLabel,
} from '../data-access/play.models';

export interface ReadyLineupAction {
  matchId: string;
  expectedRevision: number;
  action: 'slot' | 'start' | 'reset';
  position?: number;
  playerId?: string;
  otherMatchId?: string;
  otherExpectedRevision?: number;
}

@Component({
  selector: 'app-play-next-game',
  templateUrl: './play-next-game.html',
  styleUrl: './play-next-game.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayNextGame {
  readonly match = input.required<PlayMatch>();
  readonly currentMatches = input<PlayMatch[]>([]);
  protected readonly otherReadyCourts = computed(() =>
    this.currentMatches()
      .filter((m) => m.id !== this.match().id && m.status === 'Ready')
      .sort((a, b) => a.courtNumber - b.courtNumber),
  );
  readonly rotationMode = input<PlayRotationMode>('FairRotation');
  readonly disabled = input(false);
  readonly startNext = output<NextPlayGame>();
  readonly readyAction = output<ReadyLineupAction>();
  protected readonly rotationLabel = rotationLabel;
  protected readonly slots = computed(() =>
    [...this.match().players].sort((a, b) => a.position - b.position),
  );
  protected readonly lineupOptions = computed(() =>
    [...this.match().players].sort((a, b) => a.playerId.localeCompare(b.playerId)),
  );
  protected readonly waiting = computed(() =>
    this.match().eligiblePlayers.filter(
      (p) => p.state === 'Waiting' && !this.match().players.some((slot) => slot.playerId === p.id),
    ),
  );
  protected select(position: number, event: Event) {
    if (this.disabled()) return;
    const control = event.target as HTMLSelectElement;
    const playerId = control.value;
    // Keep the canonical label visible until the server accepts the change.
    control.value = this.slots().find((p) => p.position === position)?.playerId ?? '';
    this.emit('slot', position, playerId);
  }
  protected emit(action: ReadyLineupAction['action'], position?: number, playerId?: string) {
    if (this.disabled()) return;
    const other =
      action === 'slot'
        ? this.otherReadyCourts().find((m) => m.players.some((p) => p.playerId === playerId))
        : undefined;
    this.readyAction.emit({
      ...(other
        ? { otherMatchId: other.id, otherExpectedRevision: other.lineupRevision ?? 0 }
        : {}),
      matchId: this.match().id,
      expectedRevision: this.match().lineupRevision ?? 0,
      action,
      position,
      playerId,
    });
  }
  protected prepare() {
    if (this.disabled()) return;
    this.startNext.emit({
      playerIds: this.match().nextLineup.map((p) => p.playerId),
      overrideLineup: false,
    });
  }
}
