import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PlayInsights as Insights } from '../data-access/play.models';

@Component({
  selector: 'app-play-insights',
  templateUrl: './play-insights.html',
  styleUrl: './play-insights.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayInsights {
  readonly insights = input.required<Insights>();
}
