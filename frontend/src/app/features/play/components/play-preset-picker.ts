import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { PLAY_PRESETS, PlayConfiguration, PlayPreset } from '../data-access/play-presets';

@Component({ selector: 'app-play-preset-picker', templateUrl: './play-preset-picker.html',
  styleUrl: './play-host-tools.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class PlayPresetPicker {
  readonly configuration = input.required<PlayConfiguration>();
  readonly disabled = input(false);
  readonly apply = output<PlayConfiguration>();
  protected readonly store = inject(PLAY_PRESETS);
  protected readonly selected = signal('');
  protected readonly name = signal('');
  protected readonly busy = signal(false);
  protected readonly message = signal('');
  protected readonly error = signal('');
  protected readonly confirmDelete = signal(false);
  protected readonly applied = signal('');
  protected choose(event: Event) {
    this.selected.set((event.target as HTMLSelectElement).value);
    this.name.set(this.current()?.name ?? ''); this.confirmDelete.set(false); this.message.set('');
  }
  protected current(): PlayPreset | undefined { return this.store.items().find((item) => item.id === this.selected()); }
  protected use() {
    const preset = this.current();
    if (!preset || this.disabled()) return;
    this.apply.emit(preset); this.applied.set(preset.name);
  }
  protected async save(action: 'new' | 'rename' | 'update') {
    if (this.busy() || this.disabled()) return;
    this.busy.set(true); this.error.set('');
    try {
      const previous = this.current();
      if (action !== 'new' && !previous) return;
      const saved = await this.store.save(this.name(), action === 'rename' ? previous! : this.configuration(), action === 'new' ? undefined : previous?.id);
      this.selected.set(saved.id); this.name.set(saved.name); this.message.set('Preset saved on this browser.');
    } catch (error) { this.error.set((error as Error).message); }
    finally { this.busy.set(false); }
  }
  protected async remove() {
    if (this.busy() || this.disabled()) return;
    this.busy.set(true); this.error.set('');
    try {
      await this.store.remove(this.selected()); this.selected.set(''); this.name.set('');
      this.confirmDelete.set(false); this.message.set('Preset deleted. Session settings are unchanged.');
    } catch (error) { this.error.set((error as Error).message); }
    finally { this.busy.set(false); }
  }
}
