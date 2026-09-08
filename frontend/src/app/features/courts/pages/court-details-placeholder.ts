import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Entrance } from '../../../shared/entrance';

@Component({
  selector: 'app-court-details-placeholder',
  imports: [RouterLink, Entrance],
  template: `
    <section class="intro" appEntrance>
      <p class="eyebrow">Courts / A closer look</p>
      <h1>
        {{
          validId() ? 'More court-side detail. Coming soon.' : 'This court link doesn’t look right.'
        }}
      </h1>
      <p class="lead">
        {{
          validId()
            ? 'Venue details are still taking shape. For now, head back to Explore to compare places to play.'
            : 'Return to Explore and choose a venue from the list.'
        }}
      </p>
      <a class="text-link" routerLink="/courts" queryParamsHandling="preserve"
        >Back to Courts Explore</a
      >
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourtDetailsPlaceholder {
  private readonly route = inject(ActivatedRoute);
  private readonly params = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly validId = computed(() =>
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
      this.params().get('id') ?? '',
    ),
  );
}
