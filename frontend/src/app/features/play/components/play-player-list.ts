import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { PlayPlayer } from '../data-access/play.models';
import { PlayDraftPlayer } from './play-draft-player';

export interface PlayerStateChange {
  player: PlayPlayer;
  action: 'rest' | 'rejoin';
}

@Component({
  selector: 'app-play-player-list',
  imports: [PlayDraftPlayer],
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
  readonly draft = input(false);
  readonly completedPlayers = input<Readonly<Record<string, boolean>>>({});
  readonly renamePlayer = output<{ playerId: string; displayName: string }>();
  readonly removePlayer = output<string>();

  protected change(player: PlayPlayer) {
    if (this.disabled() || this.draft() || player.state === 'Playing') return;
    this.stateChange.emit({ player, action: player.state === 'Waiting' ? 'rest' : 'rejoin' });
  }
}
