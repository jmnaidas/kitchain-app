import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FeatureIntro } from '../../shared/feature-intro';

@Component({
  selector: 'app-gear',
  imports: [FeatureIntro],
  template: `<app-feature-intro
    pillar="03 — Gear"
    heading="Make it your game."
    description="A future guide to finding gear that fits the way you play. PaddleMatch will live here, with clear reasons behind every recommendation."
    scope="Gear is in the foundation phase. Paddle catalogs, comparisons and PaddleMatch recommendations are not available yet."
  />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Gear {}
