import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { NextPlayGame, PlayMatch } from '../data-access/play.models';

@Component({
  selector: 'app-play-next-game',
  templateUrl: './play-next-game.html',
  styleUrl: './play-next-game.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayNextGame {
  readonly match = input.required<PlayMatch>();
  readonly disabled = input(false);
  readonly startNext = output<NextPlayGame>();
  protected readonly open = signal(false);
  protected readonly editing = signal(false);
  protected readonly selected = signal<string[]>([]);
  protected readonly error = signal('');
  protected readonly positions = [
    'Team A player 1',
    'Team A player 2',
    'Team B player 1',
    'Team B player 2',
  ];

  protected reset() {
    this.selected.set(this.positions.map((_, i) => this.match().nextLineup[i]?.playerId ?? ''));
    this.editing.set(false);
    this.error.set('');
  }
  protected proceed() {
    this.reset();
    this.open.set(true);
  }
  protected select(index: number, event: Event) {
    const value = (event.target as HTMLSelectElement).value;
    this.selected.update((ids) => ids.map((id, i) => (i === index ? value : id)));
  }
  protected name(id: string) {
    return (
      this.match().eligiblePlayers.find((player) => player.id === id)?.displayName ??
      'Player no longer eligible'
    );
  }
  protected confirm() {
    if (this.disabled()) return;
    const ids = this.selected();
    if (
      ids.length !== 4 ||
      new Set(ids).size !== 4 ||
      ids.some((id) => !this.match().eligiblePlayers.some((player) => player.id === id))
    ) {
      this.error.set(
        'Choose four distinct eligible players. Reset to fair rotation if the lineup has changed.',
      );
      return;
    }
    this.error.set('');
    this.startNext.emit({ playerIds: [...ids], overrideLineup: this.editing() });
  }
}
