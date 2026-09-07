import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Entrance } from './entrance';

@Component({
  selector: 'app-feature-intro',
  imports: [RouterLink, Entrance],
  template: `
    <section class="intro" appEntrance aria-labelledby="page-title">
      <p class="eyebrow">{{ pillar() }} / Coming into play</p>
      <h1 id="page-title">{{ heading() }}</h1>
      <p class="lead">{{ description() }}</p>
      <p class="foundation-note">{{ scope() }}</p>
      <a class="text-link" routerLink="/">Back to home <span aria-hidden="true">↗</span></a>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FeatureIntro {
  readonly pillar = input.required<string>();
  readonly heading = input.required<string>();
  readonly description = input.required<string>();
  readonly scope = input.required<string>();
}
