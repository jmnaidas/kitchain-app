import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { PlaySession } from '../data-access/play.models';

@Component({
  selector: 'app-play-session-header',
  imports: [DatePipe],
  templateUrl: './play-session-header.html',
  styleUrl: './play-session-header.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlaySessionHeader {
  readonly session = input.required<PlaySession>();
  readonly shareNotice = input('');
  readonly shareFallback = input('');
  readonly copyRequested = output<'code' | 'link'>();
}
