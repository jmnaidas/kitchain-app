import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FeatureIntro } from '../../shared/feature-intro';

@Component({
  selector: 'app-courts',
  imports: [FeatureIntro],
  template: `<app-feature-intro
    pillar="01 — Courts"
    heading="Find your place to play."
    description="A future starting point for discovering pickleball courts in Metro Manila. Less searching, more time on court."
    scope="Courts is in the foundation phase. Court listings, availability and links to external booking providers are not available yet."
  />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Courts {}
