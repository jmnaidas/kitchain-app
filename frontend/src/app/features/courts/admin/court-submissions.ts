import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { bookingLabels } from '../data-access/court.models';
import { CourtModerationApi, SubmissionSummary } from './court-moderation-api';

@Component({
  selector: 'app-court-submissions',
  imports: [RouterLink, DatePipe],
  templateUrl: './court-submissions.html',
  styleUrl: './court-moderation.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourtSubmissions {
  private readonly api = inject(CourtModerationApi);
  private readonly refresh = signal(0);
  protected readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  protected readonly submissions = signal<SubmissionSummary[]>([]);
  protected readonly bookingLabels = bookingLabels;

  constructor() {
    effect((onCleanup) => {
      this.refresh();
      this.state.set('loading');
      const request = this.api.pending().subscribe({
        next: (submissions) => {
          this.submissions.set(submissions);
          this.state.set('ready');
        },
        error: () => this.state.set('error'),
      });
      onCleanup(() => request.unsubscribe());
    });
  }
  protected reload() {
    this.refresh.update((value) => value + 1);
  }
}
