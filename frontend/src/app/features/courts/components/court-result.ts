import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Params, RouterLink } from '@angular/router';
import { LucideArrowUpRight, LucideMapPin } from '@lucide/angular';
import {
  amenityLabels,
  bookingLabels,
  CourtSummary,
  priceUnitLabels,
  sourceLabels,
} from '../data-access/court.models';

@Component({
  selector: 'app-court-result',
  imports: [RouterLink, LucideMapPin, LucideArrowUpRight],
  templateUrl: './court-result.html',
  styleUrl: './court-result.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourtResult {
  readonly court = input.required<CourtSummary>();
  readonly queryParams = input<Params>({});
  protected readonly amenityLabels = amenityLabels;
  protected readonly bookingLabels = bookingLabels;
  protected readonly sourceLabels = sourceLabels;
  protected readonly priceUnitLabels = priceUnitLabels;
  protected readonly price = computed(() => {
    const court = this.court();
    if (court.startingPrice === null || !court.currencyCode) return null;
    return new Intl.NumberFormat('en-PH', {
      style: 'currency',
      currency: court.currencyCode,
      currencyDisplay: 'narrowSymbol',
      minimumFractionDigits: 0,
      maximumFractionDigits: 2,
    }).format(court.startingPrice);
  });
}
