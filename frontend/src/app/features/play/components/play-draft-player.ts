import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { PlayPlayer } from '../data-access/play.models';

@Component({
  selector: 'app-play-draft-player',
  templateUrl: './play-draft-player.html',
  styleUrl: './play-draft-player.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayDraftPlayer {
  readonly player = input.required<PlayPlayer>();
  readonly disabled = input(false);
  readonly renamePlayer = output<{ playerId: string; displayName: string }>();
  readonly removePlayer = output<string>();
  protected readonly action = signal<'edit' | 'remove' | null>(null);
  protected readonly name = signal('');
  protected readonly error = signal('');
  protected edit() {
    this.name.set(this.player().displayName);
    this.error.set('');
    this.action.set('edit');
  }
  protected inputName(event: Event) {
    this.name.set((event.target as HTMLInputElement).value);
  }
  protected save(event: Event) {
    event.preventDefault();
    if (this.disabled()) return;
    const name = this.name().trim();
    if (!name || name.length > 80) {
      this.error.set('Enter a name of 1–80 characters.');
      return;
    }
    this.renamePlayer.emit({ playerId: this.player().id, displayName: name });
  }
  protected remove() {
    if (!this.disabled()) this.removePlayer.emit(this.player().id);
  }
}
