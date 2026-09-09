import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { PlayApi } from '../data-access/play-api';
import { PlaySession } from '../data-access/play.models';
import { RecentPlaySessions } from '../data-access/recent-play-sessions';

@Component({
  selector: 'app-play-recent',
  imports: [DatePipe, RouterLink],
  templateUrl: './play-recent.html',
  styleUrl: './play-recent.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayRecent {
  private readonly recent = inject(RecentPlaySessions);
  private readonly api = inject(PlayApi);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly sessions = signal<PlaySession[]>([]);
  protected readonly loading = signal(false);
  protected readonly failed = signal(false);

  constructor() {
    this.load();
  }

  protected load() {
    if (this.loading()) return;
    const entries = this.recent.list();
    if (!entries.length) return;
    this.loading.set(true);
    this.failed.set(false);
    forkJoin(
      entries.map(({ code }) =>
        this.api.get(code).pipe(
          catchError((error: HttpErrorResponse) => {
            if (error.status === 404) this.recent.remove(code);
            else this.failed.set(true);
            return of(null);
          }),
        ),
      ),
    )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((sessions) => {
        this.sessions.set(sessions.filter((session): session is PlaySession => session !== null));
        this.loading.set(false);
      });
  }
}
