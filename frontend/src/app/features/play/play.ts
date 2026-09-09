import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LucideArrowUpRight } from '@lucide/angular';
import { Entrance } from '../../shared/entrance';

@Component({
  selector: 'app-play',
  imports: [RouterLink, Entrance, LucideArrowUpRight],
  templateUrl: './play.html',
  styleUrl: './play.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Play {}
