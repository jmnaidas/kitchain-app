import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { PlaySession } from '../data-access/play.models';
import { playerMatchups } from '../data-access/play-matchups';

@Component({
  selector: 'app-play-matchups',
  templateUrl: './play-matchups.html',
  styleUrl: './play-matchups.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayMatchups {
  readonly session = input.required<PlaySession>();
  protected readonly selectedId = signal('');
  protected readonly player = computed(
    () =>
      this.session().players.find((p) => p.id === this.selectedId()) ?? this.session().players[0],
  );
  protected readonly history = computed(() =>
    playerMatchups(this.session(), this.player()?.id ?? ''),
  );
  protected select(event: Event) {
    this.selectedId.set((event.target as HTMLSelectElement).value);
  }
}
