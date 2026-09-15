import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { PlayApi } from '../data-access/play-api';
import { playIssue } from '../data-access/play-errors';
import { CreatePlaySession } from '../data-access/play.models';
import { PlaySessionForm } from '../components/play-session-form';
import { RecentPlaySessions } from '../data-access/recent-play-sessions';

@Component({
  selector: 'app-play-create',
  imports: [RouterLink, PlaySessionForm],
  templateUrl: './play-create.html',
  styleUrl: './play-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayCreate {
  private readonly api = inject(PlayApi);
  private readonly recent = inject(RecentPlaySessions);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly busy = signal(false);
  protected readonly errors = signal<Record<string, string>>({});
  protected readonly problem = signal('');
  protected readonly createdCode = signal('');

  protected create(input: CreatePlaySession) {
    if (this.busy() || this.createdCode()) return;
    this.busy.set(true);
    this.problem.set('');
    this.api
      .create(input)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (session) => {
          this.recent.remember(session.joinCode);
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
