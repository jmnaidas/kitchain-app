import { DatePipe } from '@angular/common';
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
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { CourtGallery } from '../components/court-gallery';
import { amenityLabels, bookingLabels, priceUnitLabels } from '../data-access/court.models';
import {
  CourtModerationApi,
  ModerationDecision,
  ModerationReceipt,
  SubmissionReview,
} from './court-moderation-api';

@Component({
  selector: 'app-court-submission-review',
  imports: [RouterLink, DatePipe, CourtGallery],
  templateUrl: './court-submission-review.html',
  styleUrl: './court-moderation.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourtSubmissionReview {
  private readonly api = inject(CourtModerationApi);
  private readonly route = inject(ActivatedRoute);
  private readonly params = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  private readonly refresh = signal(0);
  private decisionRequest?: Subscription;
  private readonly confirmButton = viewChild<ElementRef<HTMLButtonElement>>('confirmButton');
  private readonly approveButton = viewChild<ElementRef<HTMLButtonElement>>('approveButton');
  private readonly rejectButton = viewChild<ElementRef<HTMLButtonElement>>('rejectButton');
  private readonly returnFocus = signal<ModerationDecision | null>(null);

  protected readonly state = signal<'loading' | 'ready' | 'not-found' | 'error'>('loading');
  protected readonly submission = signal<SubmissionReview | null>(null);
  protected readonly confirmation = signal<ModerationDecision | null>(null);
  protected readonly action = signal<
    'idle' | 'saving' | 'success' | 'conflict' | 'error' | 'invalid'
  >('idle');
  protected readonly receipt = signal<ModerationReceipt | null>(null);

  protected readonly amenityLabels = amenityLabels;
  protected readonly bookingLabels = bookingLabels;
  protected readonly priceUnitLabels = priceUnitLabels;

  protected readonly canDecide = computed(
    () => this.submission()?.status === 'Pending' && ['idle', 'invalid'].includes(this.action()),
  );

  protected readonly links = computed(() => {
    const submission = this.submission();

    return [
      { label: 'Website', value: submission?.websiteUrl },
      { label: 'Social page', value: submission?.socialUrl },
      { label: 'Booking page', value: submission?.bookingUrl },
    ];
  });

  constructor() {
    afterRenderEffect(() => {
      const confirmButton = this.confirmButton();

      if (confirmButton) {
        confirmButton.nativeElement.focus();
      }

      const decision = this.returnFocus();
      const target = decision === 'approve' ? this.approveButton() : this.rejectButton();

      if (decision && target) {
        target.nativeElement.focus();
        this.returnFocus.set(null);
      }
    });

    effect((onCleanup) => {
      const id = this.params().get('id') ?? '';

      this.refresh();
      this.state.set('loading');
      this.submission.set(null);
      this.confirmation.set(null);
      this.receipt.set(null);
      this.action.set('idle');

      if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)) {
        this.state.set('not-found');
        return;
      }

      const request = this.api.detail(id).subscribe({
        next: (submission) => {
          this.submission.set(submission);
          this.state.set('ready');
        },
        error: (error: HttpErrorResponse) => {
          this.state.set(error.status === 404 ? 'not-found' : 'error');
        },
      });

      onCleanup(() => {
        request.unsubscribe();
        this.decisionRequest?.unsubscribe();
      });
    });
  }

  protected safeUrl(value: string | null | undefined): string | null {
    if (!value) return null;

    try {
      const url = new URL(value);

      return ['http:', 'https:'].includes(url.protocol) &&
        !!url.hostname &&
        !url.username &&
        !url.password
        ? url.href
        : null;
    } catch {
      return null;
    }
  }

  protected reload() {
    this.refresh.update((value) => value + 1);
  }

  protected ask(decision: ModerationDecision) {
    if (this.canDecide()) {
      this.confirmation.set(decision);
    }
  }

  protected cancel() {
    if (this.action() !== 'saving') {
      this.returnFocus.set(this.confirmation());
      this.confirmation.set(null);
    }
  }

  protected confirm() {
    const submission = this.submission();
    const decision = this.confirmation();

    if (!submission || !decision || !this.canDecide()) {
      return;
    }

    this.action.set('saving');

    this.decisionRequest = this.api.decide(submission.id, decision).subscribe({
      next: (receipt) => {
        this.receipt.set(receipt);
        this.submission.set({
          ...submission,
          status: receipt.submissionStatus,
          updatedAt: receipt.updatedAt,
        });
        this.confirmation.set(null);
        this.action.set('success');
      },
      error: (error: HttpErrorResponse) => {
        this.confirmation.set(null);

        if (error.status === 404) {
          this.state.set('not-found');
        }

        this.action.set(
          error.status === 409 ? 'conflict' : error.status === 422 ? 'invalid' : 'error',
        );
      },
    });
  }
}
