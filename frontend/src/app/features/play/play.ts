import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LucideArrowUpRight } from '@lucide/angular';
import { Entrance } from '../../shared/entrance';
import { PlayRecent } from './components/play-recent';

@Component({
  selector: 'app-play',
  imports: [RouterLink, Entrance, LucideArrowUpRight, PlayRecent],
  templateUrl: './play.html',
  styleUrl: './play.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Play {}
