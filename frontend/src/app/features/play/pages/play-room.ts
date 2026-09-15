import { DatePipe, DOCUMENT } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, Subscription, switchMap, tap } from 'rxjs';
import { PlayPlayerList, PlayerStateChange } from '../components/play-player-list';
import { PlaySessionHeader } from '../components/play-session-header';
import { PlayCourts } from '../components/play-courts';
import { PlayMatchSummary } from '../components/play-match-summary';
import { PlaySessionForm } from '../components/play-session-form';
import { RecentPlaySessions } from '../data-access/recent-play-sessions';
import { PlayApi } from '../data-access/play-api';
import { PlayLive, PlayLiveStatus } from '../data-access/play-live';
import { playIssue } from '../data-access/play-errors';
import {
  normalizeCode,
  CreatePlaySession,
  NextPlayGame,
  PlayScoreCorrection,
  PlaySession,
  PlayTeam,
  PlayCallOutChange,
  validCode,
} from '../data-access/play.models';

@Component({
  selector: 'app-play-room',
  imports: [
    RouterLink,
    DatePipe,
    PlayPlayerList,
    PlaySessionHeader,
    PlayCourts,
    PlaySessionForm,
    PlayMatchSummary,
  ],
  templateUrl: './play-room.html',
  styleUrl: './play-room.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayRoom {
  private readonly api = inject(PlayApi);
  private readonly live = inject(PlayLive);
  private liveSubscription?: Subscription;
  private readonly livePending = signal(false);
  protected readonly liveStatus = signal<PlayLiveStatus>('offline');
  private readonly recent = inject(RecentPlaySessions);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly title = inject(Title);
  private readonly document = inject(DOCUMENT);
  private readonly params = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  private readonly guestInput = viewChild<ElementRef<HTMLInputElement>>('guestInput');
  private readonly focusName = signal(false);
  private code = '';
  private readRequest?: Subscription;
  private mutationRequest?: Subscription;
  protected readonly session = signal<PlaySession | null>(null);
  protected readonly editingDetails = signal<PlaySession | null>(null);
  protected readonly detailsErrors = signal<Record<string, string>>({});
  protected readonly confirmEnd = signal(false);
  protected readonly completedPlayers = computed(() =>
    Object.fromEntries(
      (this.session()?.currentMatches ?? [])
        .filter((match) => match.status === 'Completed')
        .flatMap((match) => match.players.map((player) => [player.playerId, true])),
    ),
  );
  protected readonly correctionSaved = signal(0);
  protected readonly state = signal<'loading' | 'ready' | 'error' | 'not-found'>('loading');
  protected readonly refreshing = signal(false);
  protected readonly busy = signal<string | null>(null);
  protected readonly needsRefresh = signal(false);
  protected readonly problem = signal('');
  protected readonly notice = signal('');
  protected readonly shareNotice = signal('');
  protected readonly shareFallback = signal('');
  protected readonly guestName = signal('');
  protected readonly nameError = signal('');
  protected readonly view = signal<'courts' | 'queue' | 'players' | 'history'>('queue');
  protected readonly playerCourts = computed(() =>
    Object.fromEntries(
      (this.session()?.currentMatches ?? []).flatMap((match) =>
        match.players.map((player) => [player.playerId, match.courtNumber]),
      ),
    ),
  );
  protected readonly refreshedAt = signal<Date | null>(null);
  protected readonly blocked = computed(
    () =>
      this.busy() !== null ||
      this.refreshing() ||
      this.needsRefresh() ||
      this.session()?.status === 'Ended',
  );
  // The server's array order is authoritative. Tickets are never sorted or changed here.
  protected readonly positions = computed(() =>
    Object.fromEntries(
      (this.session()?.waitingQueue ?? []).map((player, index) => [player.id, index + 1]),
    ),
  );
  protected readonly restingCount = computed(
    () => this.session()?.players.filter((player) => player.state === 'Resting').length ?? 0,
  );

  constructor() {
    effect(() => {
      if (this.livePending() && !this.busy() && !this.refreshing()) {
        untracked(() => {
          this.livePending.set(false);
          if (this.session()) this.fetch(true);
        });
      }
    });
    afterRenderEffect(() => {
      if (this.focusName() && this.guestInput()) {
        this.guestInput()!.nativeElement.focus();
        this.focusName.set(false);
      }
    });
    effect((onCleanup) => {
      const raw = this.params().get('code') ?? '';
      const code = normalizeCode(raw);
      this.code = code;
      this.livePending.set(false);
      this.liveStatus.set('offline');
      this.session.set(null);
      this.state.set('loading');
      this.busy.set(null);
      this.refreshing.set(false);
      this.needsRefresh.set(false);
      this.problem.set('');
      this.notice.set('');
      this.guestName.set('');
      this.nameError.set('');
      this.shareNotice.set('');
      this.shareFallback.set('');
      this.view.set('queue');
      this.confirmEnd.set(false);
      this.editingDetails.set(null);
      this.title.setTitle('Session Room | Kitchain');
      onCleanup(() => {
        this.stopLive();
        this.readRequest?.unsubscribe();
        this.mutationRequest?.unsubscribe();
      });
      if (!validCode(code)) {
        this.state.set('not-found');
        return;
      }
      if (raw !== code) {
        void this.router.navigate(['/play/s', code], { replaceUrl: true });
        return;
      }
      this.fetch();
    });
  }

  private accept(session: PlaySession) {
    this.recent.remember(session.joinCode);
    this.session.set(session);
    this.state.set('ready');
    this.needsRefresh.set(false);
    this.refreshedAt.set(new Date());
    this.title.setTitle(`${session.name} | Kitchain Play`);
  }
  private stopLive() {
    this.liveSubscription?.unsubscribe();
    this.liveSubscription = undefined;
    this.livePending.set(false);
    this.liveStatus.set('offline');
  }
  private connectLive() {
    if (this.liveSubscription) return;
    const code = this.code;
    this.liveSubscription = this.live.watch(code).subscribe((event) => {
      if (this.code !== code) return;
      if (event.kind === 'status') this.liveStatus.set(event.status);
      else this.livePending.set(true);
    });
  }
  private fetch(preserveProblem = false) {
    this.readRequest?.unsubscribe();
    this.refreshing.set(true);
    if (!preserveProblem) this.problem.set('');
    this.readRequest = this.api.get(this.code).subscribe({
      next: (session) => {
        this.accept(session);
        this.refreshing.set(false);
        this.connectLive();
      },
      error: (error: HttpErrorResponse) => {
        this.refreshing.set(false);
        if (error.status === 404) {
          this.stopLive();
          this.recent.remove(this.code);
          this.session.set(null);
          this.state.set('not-found');
        } else if (this.session()) {
          this.needsRefresh.set(true);
          this.problem.set(
            'We couldn’t refresh the room. You’re seeing the last loaded state. Refresh before making changes.',
          );
        } else {
          this.state.set('error');
        }
      },
    });
  }
  protected reload() {
    if (this.busy() || this.refreshing()) return;
    if (this.liveStatus() === 'offline') this.stopLive();
    if (!this.session()) this.state.set('loading');
    this.notice.set('');
    this.fetch();
  }
  protected editName(event: Event) {
    this.guestName.set((event.target as HTMLInputElement).value);
    this.nameError.set('');
  }
  protected focusAdd() {
    this.focusName.set(true);
  }

  protected addPlayer(event: Event) {
    event.preventDefault();
    if (this.blocked()) return;
    const name = this.guestName().trim();
    if (!name || name.length > 80) {
      this.nameError.set('Enter a player name of 1–80 characters.');
      this.focusAdd();
      return;
    }
    this.busy.set('add');
    this.problem.set('');
    this.notice.set('');
    let added = false;
    this.mutationRequest = this.api
      .addPlayer(this.code, name)
      .pipe(
        tap(() => {
          added = true;
          this.guestName.set('');
        }),
        switchMap(() => this.api.get(this.code)),
      )
      .subscribe({
        next: (session) => {
          this.accept(session);
          this.busy.set(null);
          this.notice.set(`${name} joined the session.`);
          this.focusAdd();
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(null);
          if (added) {
            this.needsRefresh.set(true);
            this.problem.set(
              `${name} was added, but the room couldn’t refresh. Refresh to see the current queue.`,
            );
          } else this.changeFailed(error);
        },
      });
  }

  protected editDetails(input: CreatePlaySession) {
    if (this.session()?.status !== 'Draft') return;
    this.detailsErrors.set({});
    this.change('details', this.api.edit(this.code, input), 'Session details updated.', () =>
      this.editingDetails.set(null),
    );
  }
  protected start() {
    if (this.session()?.status !== 'Draft') return;
    this.change('start', this.api.start(this.code), 'Session started. Your crew is ready.');
  }
  protected renameGuest(intent: { playerId: string; displayName: string }) {
    if (this.session()?.status !== 'Draft') return;
    this.change(
      'rename:' + intent.playerId,
      this.api.renameGuest(this.code, intent.playerId, intent.displayName),
      'Player name updated.',
    );
  }
  protected removeGuest(playerId: string) {
    if (this.session()?.status !== 'Draft') return;
    this.change(
      'remove:' + playerId,
      this.api.removeGuest(this.code, playerId),
      'Player removed from the Draft roster.',
    );
  }
  protected endSession() {
    if (this.session()?.status !== 'Active') return;
    this.change(
      'end',
      this.api.end(this.code),
      'Session ended. The final state remains viewable.',
      () => this.confirmEnd.set(false),
    );
  }
  protected startNextGame(intent: { matchId: string; lineup: NextPlayGame }) {
    this.change(
      'next:' + intent.matchId,
      this.api.startNext(this.code, intent.matchId, intent.lineup),
      'Next game started. Courts and queue are up to date.',
    );
  }
  protected finishGame(matchId: string) {
    if (this.session()?.status !== 'Active') return;
    this.change(
      `finish:${matchId}`,
      this.api.finish(this.code, matchId),
      'Game complete. Review the next lineup when ready.',
    );
  }
  protected recordRally(intent: { matchId: string; winner: PlayTeam }) {
    this.change(
      `rally:${intent.matchId}`,
      this.api.rally(this.code, intent.matchId, intent.winner),
      'Rally recorded. Score, service and courts are up to date.',
    );
  }
  protected editCallOut(intent: PlayCallOutChange) {
    if (this.session()?.mode !== 'LiveScoring' || this.session()?.status !== 'Active') return;
    this.change(
      `call-out:${intent.rallyId}`,
      this.api.editCallOut(this.code, intent.matchId, intent.rallyId, intent.edit),
      intent.edit.callOut ? 'Rally call-out updated.' : 'Rally call-out removed.',
    );
  }
  protected correctScore(intent: { matchId: string; score: PlayScoreCorrection }) {
    this.change(
      `score:${intent.matchId}`,
      this.api.correctScore(this.code, intent.matchId, intent.score),
      'Score and service corrected.',
      () => this.correctionSaved.update((value) => value + 1),
    );
  }
  protected changePlayer(change: PlayerStateChange) {
    if (this.session()?.status !== 'Active') return;
    const { player, action } = change;
    this.change(
      `${action}:${player.id}`,
      action === 'rest'
        ? this.api.rest(this.code, player.id)
        : this.api.rejoin(this.code, player.id),
      action === 'rest'
        ? `${player.displayName} is taking a break.`
        : `${player.displayName} rejoined the session. Courts and queue are up to date.`,
    );
  }
  private change(
    key: string,
    operation: Observable<PlaySession>,
    notice: string,
    onSuccess?: () => void,
  ) {
    if (this.blocked()) return;
    this.busy.set(key);
    this.problem.set('');
    this.notice.set('');
    this.mutationRequest = operation.subscribe({
      next: (session) => {
        this.accept(session);
        this.busy.set(null);
        this.notice.set(notice);
        onSuccess?.();
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(null);
        this.changeFailed(error);
      },
    });
  }
  private changeFailed(error: HttpErrorResponse) {
    const issue = playIssue(error, 'change');
    this.problem.set(issue.message);
    this.detailsErrors.set(issue.fields);
    this.nameError.set(issue.fields['displayName'] ?? '');
    if (error.status === 409 || error.status === 404) {
      // A single corrective read handles a stale room; there is no polling.
      this.needsRefresh.set(true);
      this.fetch(true);
    } else this.needsRefresh.set(error.status !== 400 && error.status !== 422);
  }

  protected async copy(kind: 'code' | 'link') {
    const code = this.session()?.joinCode;
    if (!code) return;
    const origin = this.document.defaultView?.location.origin ?? '';
    const value =
      kind === 'code'
        ? code
        : origin + this.router.serializeUrl(this.router.createUrlTree(['/play/s', code]));
    this.shareNotice.set('');
    this.shareFallback.set('');
    try {
      const clipboard = this.document.defaultView?.navigator.clipboard;
      if (!clipboard) throw new Error('Clipboard unavailable');
      await clipboard.writeText(value);
      if (code === this.session()?.joinCode)
        this.shareNotice.set(kind === 'code' ? 'Session code copied.' : 'Session link copied.');
    } catch {
      if (code === this.session()?.joinCode) {
        this.shareNotice.set('Copy isn’t available here. Select and copy the text below.');
        this.shareFallback.set(value);
      }
    }
  }
}
