import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  isDevMode,
  signal,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { LucideArrowLeft, LucideArrowRight } from '@lucide/angular';
import { CourtFilters } from './components/court-filters';
import { CourtResult } from './components/court-result';
import { CourtPage, CourtSearch } from './data-access/court.models';
import { CourtsApi } from './data-access/courts-api';
import {
  activeFilterLabels,
  courtQueryParams,
  defaultSearch,
  readCourtQuery,
} from './data-access/court-query';
import { Entrance } from '../../shared/entrance';

type ExploreState =
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'invalid' }
  | { kind: 'ready'; page: CourtPage };

@Component({
  selector: 'app-courts',
  imports: [CourtFilters, CourtResult, Entrance, LucideArrowLeft, LucideArrowRight],
  templateUrl: './courts.html',
  styleUrl: './courts.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Courts {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(CourtsApi);
  private readonly params = toSignal(this.route.queryParamMap, {
    initialValue: this.route.snapshot.queryParamMap,
  });
  private readonly parsed = computed(() => readCourtQuery(this.params()));
  private readonly refresh = signal(0);
  protected readonly query = computed(() => this.parsed().query);
  protected readonly queryParams = computed(() => courtQueryParams(this.query()));
  protected readonly filters = computed(() => activeFilterLabels(this.query()));
  protected readonly state = signal<ExploreState>({ kind: 'loading' });
  protected readonly page = computed(() => {
    const state = this.state();
    return state.kind === 'ready' ? state.page : null;
  });
  protected readonly development = isDevMode();
  protected readonly pageSizes = [5, 10, 20];
  private readonly heading = viewChild<ElementRef<HTMLHeadingElement>>('resultsHeading');

  constructor() {
    effect((onCleanup) => {
      const { query, invalid } = this.parsed();
      this.refresh();
      if (invalid) {
        this.state.set({ kind: 'invalid' });
        return;
      }
      this.state.set({ kind: 'loading' });
      const request = this.api.search(query).subscribe({
        next: (page) => this.state.set({ kind: 'ready', page }),
        error: () => this.state.set({ kind: 'error' }),
      });
      onCleanup(() => request.unsubscribe());
    });
  }

  protected apply(query: CourtSearch) {
    void this.router
      .navigate([], { relativeTo: this.route, queryParams: courtQueryParams(query) })
      .then((navigated) => {
        if (!navigated) this.retry();
      });
  }

  protected reset() {
    this.apply({ ...defaultSearch });
  }
  protected retry() {
    this.refresh.update((value) => value + 1);
  }
  protected goToPage(page: number) {
    this.heading()?.nativeElement.focus({ preventScroll: true });
    this.apply({ ...this.query(), page });
  }
  protected changePageSize(event: Event) {
    const pageSize = Number((event.target as HTMLSelectElement).value);
    this.apply({ ...this.query(), page: 1, pageSize });
  }
}
