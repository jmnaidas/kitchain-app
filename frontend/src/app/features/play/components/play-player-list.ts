import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { PlayPlayer } from '../data-access/play.models';

export interface PlayerStateChange {
  player: PlayPlayer;
  action: 'rest' | 'rejoin';
}

@Component({
  selector: 'app-play-player-list',
  templateUrl: './play-player-list.html',
  styleUrl: './play-player-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayPlayerList {
  readonly players = input.required<readonly PlayPlayer[]>();
  readonly positions = input.required<Readonly<Record<string, number>>>();
  readonly courts = input<Readonly<Record<string, number>>>({});
  readonly featured = input(false);
  readonly disabled = input(false);
  readonly stateChange = output<PlayerStateChange>();

  protected change(player: PlayPlayer) {
    if (this.disabled() || player.state === 'Playing') return;
    this.stateChange.emit({ player, action: player.state === 'Waiting' ? 'rest' : 'rejoin' });
  }
}
