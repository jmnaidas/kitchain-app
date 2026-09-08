import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { LucideArrowLeft, LucideArrowUpRight } from '@lucide/angular';
import { Entrance } from '../../../shared/entrance';
import {
  amenityLabels,
  bookingLabels,
  CourtDetail,
  priceUnitLabels,
  sourceLabels,
} from '../data-access/court.models';
import { CourtsApi } from '../data-access/courts-api';
import { CourtGallery } from '../components/court-gallery';

type DetailState =
  | { kind: 'loading' }
  | { kind: 'ready'; court: CourtDetail }
  | { kind: 'not-found' }
  | { kind: 'error' };

/** Keep API-provided destinations external without trusting arbitrary URL schemes. */
function externalUrl(value: string | null | undefined): string | null {
  if (!value) return null;
  try {
    const url = new URL(value);
    return ['https:', 'http:'].includes(url.protocol) && !url.username && !url.password
      ? url.href
      : null;
  } catch {
    return null;
  }
}

@Component({
  selector: 'app-court-details',
  imports: [RouterLink, Entrance, LucideArrowLeft, LucideArrowUpRight, CourtGallery],
  templateUrl: './court-details.html',
  styleUrl: './court-details.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourtDetails {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(CourtsApi);
  private readonly title = inject(Title);
  private readonly params = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  private readonly refresh = signal(0);
  protected readonly state = signal<DetailState>({ kind: 'loading' });
  protected readonly court = computed(() => {
    const state = this.state();
    return state.kind === 'ready' ? state.court : null;
  });
  protected readonly amenityLabels = amenityLabels;
  protected readonly bookingLabels = bookingLabels;
  protected readonly sourceLabels = sourceLabels;
  protected readonly priceUnitLabels = priceUnitLabels;
  protected readonly bookingUrl = computed(() => externalUrl(this.court()?.bookingUrl));
  protected readonly websiteUrl = computed(() => externalUrl(this.court()?.websiteUrl));
  protected readonly socialUrl = computed(() => externalUrl(this.court()?.socialUrl));
  protected readonly phoneUrl = computed(() => {
    const phone = this.court()?.phone?.trim();
    // Free-text contact descriptions remain readable, without inventing a dial target.
    return phone && /^\+?[\d\s().-]+$/.test(phone) && /\d/.test(phone)
      ? `tel:${phone.replace(/[\s().-]/g, '')}`
      : null;
  });
  protected readonly price = computed(() => {
    const court = this.court();
    if (!court || court.startingPrice === null || !court.currencyCode) return null;
    return new Intl.NumberFormat('en-PH', {
      style: 'currency',
      currency: court.currencyCode,
      currencyDisplay: 'narrowSymbol',
      minimumFractionDigits: 0,
      maximumFractionDigits: 2,
    }).format(court.startingPrice);
  });

  constructor() {
    effect((onCleanup) => {
      const id = this.params().get('id') ?? '';
      this.refresh();
      this.title.setTitle('Court details | Kitchain');
      if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)) {
        this.state.set({ kind: 'not-found' });
        this.title.setTitle('Court not found | Kitchain');
        return;
      }
      this.state.set({ kind: 'loading' });
      const request = this.api.detail(id).subscribe({
        next: (court) => {
          this.state.set({ kind: 'ready', court });
          this.title.setTitle(`${court.name} | Kitchain`);
        },
        error: (error: HttpErrorResponse) => {
          const kind = error.status === 404 ? 'not-found' : 'error';
          this.state.set({ kind });
          this.title.setTitle(
            kind === 'not-found' ? 'Court not found | Kitchain' : 'Court details | Kitchain',
          );
        },
      });
      onCleanup(() => request.unsubscribe());
    });
  }

  protected retry() {
    this.refresh.update((value) => value + 1);
  }
}
