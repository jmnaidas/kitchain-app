import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import {
  PlayCallOutChange,
  PlayMatch,
  PlayRallyCallOut,
  PlayRallyEvent,
  playCallOuts,
  callOutLabel,
} from '../data-access/play.models';

@Component({
  selector: 'app-play-rally-history',
  templateUrl: './play-rally-history.html',
  styleUrl: './play-rally-history.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayRallyHistory {
  readonly match = input.required<Pick<PlayMatch, 'id' | 'courtNumber' | 'rallies'>>();
  readonly disabled = input(false);
  readonly readOnly = input(false);
  readonly callOut = output<PlayCallOutChange>();
  protected readonly expanded = signal(false);
  protected readonly editing = signal<string | null>(null);
  protected readonly types = playCallOuts;
  protected readonly label = callOutLabel;
  protected readonly ordered = computed(() =>
    [...this.match().rallies].sort((a, b) => b.sequence - a.sequence),
  );
  protected readonly visible = computed(() =>
    this.expanded() ? this.ordered() : this.ordered().slice(0, 1),
  );
  protected change(rally: PlayRallyEvent, event: Event) {
    const select = event.target as HTMLSelectElement;
    const value = select.value as PlayRallyCallOut | '';
    // Keep displaying canonical data until the server accepts the edit.
    select.value = rally.callOut ?? '';
    this.setCallOut(rally, value || null);
  }
  protected setCallOut(rally: PlayRallyEvent, callOut: PlayRallyCallOut | null) {
    if (this.disabled() || this.readOnly() || (callOut && !this.types.includes(callOut))) return;
    if (callOut === rally.callOut) return;
    this.callOut.emit({
      matchId: this.match().id,
      rallyId: rally.id,
      edit: { callOut, expectedCallOut: rally.callOut },
    });
  }
}
