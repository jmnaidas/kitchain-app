import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FeatureIntro } from '../../shared/feature-intro';

@Component({
  selector: 'app-play',
  imports: [FeatureIntro],
  template: `<app-feature-intro
    pillar="02 — Play"
    heading="Good games. Good company."
    description="A future home for getting a group on court and keeping the game moving. Simple to join. Fair by default."
    scope="Play is in the foundation phase. Sessions, guest joining, queues and scoring are not available yet."
  />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Play {}
