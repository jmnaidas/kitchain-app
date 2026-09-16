import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal } from '@angular/core';
import { PLAY_GROUPS, PlayGroup } from '../data-access/play-groups';

@Component({ selector: 'app-play-group-editor', templateUrl: './play-group-editor.html',
  styleUrl: './play-host-tools.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class PlayGroupEditor {
  readonly group = input<PlayGroup | null>(null);
  readonly names = input<readonly string[]>([]);
  readonly saved = output<PlayGroup>();
  readonly cancelEdit = output<void>();
  private readonly store = inject(PLAY_GROUPS);
  protected readonly name = signal('');
  protected readonly players = signal('');
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  constructor() {
    effect(() => {
      this.name.set(this.group()?.name ?? '');
      this.players.set((this.group()?.players.map((p) => p.displayName) ?? this.names()).join('\n'));
    });
  }
  protected async save(event: Event) {
    event.preventDefault();
    if (this.busy()) return;
    this.busy.set(true); this.error.set('');
    try {
      const names = this.players().split(/\r?\n/).map((name) => name.trim()).filter(Boolean);
      this.saved.emit(await this.store.save(this.name(), names, this.group()?.id));
    } catch (error) { this.error.set((error as Error).message); }
    finally { this.busy.set(false); }
  }
}
