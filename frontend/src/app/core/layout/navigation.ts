import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import {
  LucideDynamicIcon,
  LucideHouse,
  LucideMapPin,
  LucideUsers,
  LucideShoppingBag,
} from '@lucide/angular';

@Component({
  selector: 'app-navigation',
  imports: [RouterLink, RouterLinkActive, LucideDynamicIcon],
  templateUrl: './navigation.html',
  styleUrl: './navigation.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Navigation {
  protected readonly links = [
    { path: '/', label: 'Home', icon: LucideHouse },
    { path: '/courts', label: 'Courts', icon: LucideMapPin },
    { path: '/play', label: 'Play', icon: LucideUsers },
    { path: '/gear', label: 'Gear', icon: LucideShoppingBag },
  ];
}
