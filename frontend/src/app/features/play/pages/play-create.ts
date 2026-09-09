import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { PlayApi } from '../data-access/play-api';
import { playIssue } from '../data-access/play-errors';
import { localDate } from '../data-access/play.models';

@Component({
  selector: 'app-play-create',
  imports: [RouterLink],
  templateUrl: './play-create.html',
  styleUrl: './play-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayCreate {
  private readonly api = inject(PlayApi);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly today = localDate();
  protected readonly busy = signal(false);
  protected readonly errors = signal<Record<string, string>>({});
  protected readonly problem = signal('');
  protected readonly createdCode = signal('');

  protected validate(event: Event) {
    const field = event.target as HTMLInputElement;
    this.validateField(field);
  }
  private validateField(field: HTMLInputElement) {
    field.setCustomValidity('');
    if (field.required && !field.value.trim()) field.setCustomValidity('This field is required.');
    this.errors.update((errors) => ({ ...errors, [field.name]: field.validationMessage }));
  }
  protected submit(event: Event) {
    event.preventDefault();
    if (this.busy() || this.createdCode()) return;
    const form = event.target as HTMLFormElement;
    const fields = Array.from(form.querySelectorAll('input'));
    this.errors.set({});
    this.problem.set('');
    fields.forEach((field) => this.validateField(field));
    const values = new FormData(form);
    const text = (name: string) => String(values.get(name) ?? '').trim();
    if (text('startTime') && text('endTime') && text('endTime') <= text('startTime')) {
      this.errors.update((errors) => ({
        ...errors,
        endTime: 'End time must be after start time on the same day.',
      }));
    }
    if (Object.values(this.errors()).some(Boolean)) {
      fields.find((field) => this.errors()[field.name])?.focus();
      return;
    }
    const time = (name: string) => (text(name).length === 5 ? `${text(name)}:00` : text(name));
    this.busy.set(true);
    this.api
      .create({
        name: text('name'),
        date: text('date'),
        startTime: time('startTime'),
        endTime: time('endTime'),
        numberOfCourts: Number(text('numberOfCourts')),
        maximumPlayers: text('maximumPlayers') ? Number(text('maximumPlayers')) : null,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (session) => {
          this.createdCode.set(session.joinCode);
          void this.router.navigate(['/play/s', session.joinCode]);
        },
        error: (error: HttpErrorResponse) => {
          const issue = playIssue(error, 'create');
          this.problem.set(issue.message);
          this.errors.set(issue.fields);
          this.busy.set(false);
        },
      });
  }
}
