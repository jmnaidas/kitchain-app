import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { PlayApi } from '../data-access/play-api';
import { playIssue } from '../data-access/play-errors';
import { normalizeCode, validCode } from '../data-access/play.models';

@Component({
  selector: 'app-play-join',
  imports: [RouterLink],
  templateUrl: './play-join.html',
  styleUrl: './play-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayJoin {
  private readonly api = inject(PlayApi);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly code = signal('');
  protected readonly problem = signal('');
  protected readonly busy = signal(false);
  protected readonly invalid = signal(false);

  protected input(event: Event) {
    this.code.set((event.target as HTMLInputElement).value.toUpperCase());
    this.invalid.set(false);
    this.problem.set('');
  }
  protected submit(event: Event) {
    event.preventDefault();
    if (this.busy()) return;
    const code = normalizeCode(this.code());
    this.code.set(code);
    if (!validCode(code)) {
      this.invalid.set(true);
      this.problem.set('Enter the six-character code shared by your group.');
      return;
    }
    this.busy.set(true);
    this.problem.set('');
    this.api
      .get(code)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (session) => {
          void this.router.navigate(['/play/s', session.joinCode]);
        },
        error: (error: HttpErrorResponse) => {
          this.problem.set(playIssue(error, 'join').message);
          this.invalid.set(error.status === 404);
          this.busy.set(false);
        },
      });
  }
}
