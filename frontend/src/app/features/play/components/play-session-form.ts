import { ChangeDetectionStrategy, Component, computed, effect, input, output, signal } from '@angular/core';
import {
  CreatePlaySession,
  localDate,
  PlaySession,
  PlaySessionMode,
  PlayRotationMode,
  rotationStyles,
} from '../data-access/play.models';

import { PlayConfiguration } from '../data-access/play-presets';
import { PlayPresetPicker } from './play-preset-picker';

@Component({
  selector: 'app-play-session-form',
  imports: [PlayPresetPicker],
  host: { class: 'session-form' },
  templateUrl: './play-session-form.html',
  styleUrl: '../pages/play-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlaySessionForm {
  readonly allowPresets = input(false);
  readonly initial = input<PlaySession | null>(null);
  readonly disabled = input(false);
  readonly serverErrors = input<Record<string, string>>({});
  readonly save = output<CreatePlaySession>();
  readonly cancelEdit = output<void>();
  protected readonly rotationStyles = rotationStyles;
  protected readonly rotationMode = signal<PlayRotationMode>('FairRotation');
  protected readonly courtCount = signal('1');
  protected readonly playerLimit = signal('');
  protected readonly configuration = computed<PlayConfiguration>(() => ({
    mode: this.mode(), rotationMode: this.rotationMode(), numberOfCourts: Number(this.courtCount()),
    maximumPlayers: this.playerLimit().trim() ? Number(this.playerLimit()) : null,
  }));
  protected applyConfiguration(config: PlayConfiguration) {
    this.mode.set(config.mode); this.rotationMode.set(config.rotationMode);
    this.courtCount.set(String(config.numberOfCourts)); this.playerLimit.set(config.maximumPlayers === null ? '' : String(config.maximumPlayers));
    this.errors.update(errors => ({ ...errors, numberOfCourts: '', maximumPlayers: '', rotationMode: '' }));
  }
  protected readonly today = localDate();
  protected readonly mode = signal<PlaySessionMode>('QueueOnly');
  protected readonly endDate = signal(this.today);
  protected readonly errors = signal<Record<string, string>>({});
  private endDateEdited = false;

  constructor() {
    effect(() => {
      const initial = this.initial();
      this.mode.set(initial?.mode ?? 'QueueOnly');
      this.courtCount.set(String(initial?.numberOfCourts ?? 1));
      this.playerLimit.set(initial?.maximumPlayers == null ? '' : String(initial.maximumPlayers));
      this.rotationMode.set(initial?.rotationMode ?? 'FairRotation');
      this.endDate.set(initial?.endDate ?? this.today);
      this.endDateEdited = initial !== null;
    });
    effect(() => this.errors.set(this.serverErrors()));
  }
  protected startDateChanged(event: Event) {
    if (!this.endDateEdited) this.endDate.set((event.target as HTMLInputElement).value);
  }
  protected endDateChanged(event: Event) {
    this.endDateEdited = true;
    this.endDate.set((event.target as HTMLInputElement).value);
  }
  protected validate(event: Event) {
    this.validateField(event.target as HTMLInputElement);
  }
  private validateField(field: HTMLInputElement) {
    field.setCustomValidity('');
    if (field.required && !field.value.trim()) field.setCustomValidity('This field is required.');
    this.errors.update((errors) => ({ ...errors, [field.name]: field.validationMessage }));
  }
  protected submit(event: Event) {
    event.preventDefault();
    if (this.disabled()) return;
    const form = event.target as HTMLFormElement;
    const fields = Array.from(form.querySelectorAll('input'));
    this.errors.set({});
    fields.forEach((field) => this.validateField(field));
    const values = new FormData(form);
    const text = (name: string) => String(values.get(name) ?? '').trim();
    const time = (name: string) => (text(name).length === 5 ? `${text(name)}:00` : text(name));
    if (
      text('date') &&
      text('endDate') &&
      text('startTime') &&
      text('endTime') &&
      `${text('endDate')}T${time('endTime')}` <= `${text('date')}T${time('startTime')}`
    ) {
      this.errors.update((errors) => ({
        ...errors,
        endTime: 'End date and time must be after start date and time.',
      }));
    }
    if (Object.values(this.errors()).some(Boolean)) {
      fields.find((field) => this.errors()[field.name])?.focus();
      return;
    }
    this.save.emit({
      name: text('name'),
      date: text('date'),
      endDate: text('endDate'),
      startTime: time('startTime'),
      endTime: time('endTime'),
      mode: this.mode(),
      rotationMode: this.rotationMode(),
      numberOfCourts: Number(text('numberOfCourts')),
      maximumPlayers: text('maximumPlayers') ? Number(text('maximumPlayers')) : null,
    });
  }
}
