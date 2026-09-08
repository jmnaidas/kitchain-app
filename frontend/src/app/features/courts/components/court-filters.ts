import {
  afterRenderEffect,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { LucideSlidersHorizontal } from '@lucide/angular';
import {
  Amenity,
  amenityOptions,
  CourtSearch,
  IndoorOutdoor,
  indoorOutdoorTypes,
} from '../data-access/court.models';

@Component({
  selector: 'app-court-filters',
  imports: [LucideSlidersHorizontal],
  templateUrl: './court-filters.html',
  styleUrl: './court-filters.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourtFilters {
  readonly query = input.required<CourtSearch>();
  readonly count = input(0);
  readonly apply = output<CourtSearch>();
  readonly clear = output<void>();
  protected readonly expanded = signal(false);
  protected readonly amenities = amenityOptions;
  protected readonly settings = indoorOutdoorTypes;
  private readonly form = viewChild.required<ElementRef<HTMLFormElement>>('filterForm');
  private readonly toggle = viewChild.required<ElementRef<HTMLButtonElement>>('toggle');

  constructor() {
    // Synchronize every control after dynamic options exist, including unchanged
    // fields with unapplied edits when browser history or Clear changes the URL.
    afterRenderEffect(() => {
      const query = this.query();
      const form = this.form().nativeElement;
      for (const key of [
        'city',
        'indoorOutdoor',
        'amenity',
        'minCourts',
        'maxStartingPrice',
      ] as const) {
        const control = form.elements.namedItem(key) as HTMLInputElement | HTMLSelectElement;
        control.value = String(query[key] ?? '');
      }
    });
  }

  protected submit(event: Event) {
    event.preventDefault();
    const form = this.form().nativeElement;
    if (!form.reportValidity()) return;
    const values = new FormData(form);
    const text = (key: string) => String(values.get(key) ?? '').trim();
    const number = (key: string) => (text(key) === '' ? undefined : Number(text(key)));
    const maxStartingPrice = number('maxStartingPrice');
    this.apply.emit({
      city: text('city') || undefined,
      indoorOutdoor: (text('indoorOutdoor') || undefined) as IndoorOutdoor | undefined,
      amenity: (text('amenity') || undefined) as Amenity | undefined,
      minCourts: number('minCourts'),
      maxStartingPrice,
      currencyCode:
        maxStartingPrice !== undefined
          ? (this.query().currencyCode ?? 'PHP')
          : this.query().maxStartingPrice === undefined
            ? this.query().currencyCode
            : undefined,
      page: 1,
      pageSize: this.query().pageSize,
    });
    this.close();
  }

  protected reset() {
    this.form().nativeElement.reset();
    this.clear.emit();
    this.close();
  }

  protected close() {
    this.expanded.set(false);
    const toggle = this.toggle().nativeElement;
    if (toggle.getClientRects().length > 0) toggle.focus();
  }
}
