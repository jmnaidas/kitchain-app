import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PLAY_GROUPS, PlayGroup } from '../data-access/play-groups';
import { PLAY_RECENT_PLAYERS } from '../data-access/play-recent-players';
import { PLAY_PREVIOUS_ROSTERS, PreviousPlayRoster } from '../data-access/play-previous-rosters';
import { playerNameKey } from '../data-access/play-host-storage';
import { PlaySession } from '../data-access/play.models';
import { PlayGroupEditor } from './play-group-editor';

@Component({ selector: 'app-play-roster-builder', imports: [PlayGroupEditor], templateUrl: './play-roster-builder.html',
  styleUrl: './play-host-tools.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class PlayRosterBuilder {
  readonly session = input.required<PlaySession>();
  readonly disabled = input(false);
  readonly addPlayers = output<string[]>();
  protected readonly groups = inject(PLAY_GROUPS);
  protected readonly recent = inject(PLAY_RECENT_PLAYERS);
  private readonly previous = inject(PLAY_PREVIOUS_ROSTERS);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly source = signal<'recent' | 'groups' | 'previous'>('recent');
  protected readonly groupId = signal('');
  protected readonly previousCode = signal('');
  protected readonly rosters = signal<PreviousPlayRoster[]>([]);
  protected readonly loading = signal(false);
  protected readonly selection = signal<string[]>([]);
  protected readonly editing = signal(false);
  protected readonly editedGroup = signal<PlayGroup | null>(null);
  protected readonly seedNames = signal<string[]>([]);
  protected readonly confirmation = signal<'group' | 'recent' | null>(null);
  protected readonly error = signal('');
  protected readonly message = signal('');
  protected readonly saving = signal(false);
  protected readonly selectedGroup = computed(() => this.groups.items().find((g) => g.id === this.groupId()));
  protected readonly names = computed(() => {
    switch (this.source()) {
      case 'recent': return this.recent.items().map((p) => p.displayName);
      case 'groups': return this.selectedGroup()?.players.map((p) => p.displayName) ?? [];
      case 'previous': return this.rosters().find((p) => p.code === this.previousCode())?.names ?? [];
    }
  });
  protected readonly rosterKeys = computed(() => new Set(this.session().players.map((p) => playerNameKey(p.displayName))));
  protected already(name: string) { return this.rosterKeys().has(playerNameKey(name)); }
  protected tab(source: 'recent' | 'groups' | 'previous') {
    this.source.set(source); this.selection.set([]); this.confirmation.set(null);
    if (source === 'previous') this.loadPrevious();
  }
  protected choose(event: Event) {
    const value = (event.target as HTMLSelectElement).value;
    if (this.source() === 'groups') this.groupId.set(value); else this.previousCode.set(value);
    this.selection.set([]); this.confirmation.set(null);
  }
  protected toggle(name: string) {
    this.selection.update((names) => names.includes(name) ? names.filter((n) => n !== name) : [...names, name]);
  }
  protected add(all = false) {
    if (this.disabled() || this.session().status !== 'Draft') return;
    const names = all ? this.names() : this.selection().filter((name) => this.names().includes(name));
    if (names.length) this.addPlayers.emit([...names]);
    this.selection.set([]);
  }
  protected edit(group: PlayGroup | null, useRoster = false) {
    this.editedGroup.set(group); this.seedNames.set(useRoster ? this.session().players.map((p) => p.displayName) : []);
    this.editing.set(true); this.error.set(''); this.confirmation.set(null);
  }
  protected saved(group: PlayGroup) {
    this.groupId.set(group.id); this.selection.set([]); this.editing.set(false);
    this.message.set('Group saved on this browser. Repeated names were merged.');
  }
  protected loadPrevious() {
    if (this.loading()) return;
    this.loading.set(true);
    this.previous.load(this.session().joinCode).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (rosters) => { this.rosters.set(rosters); this.loading.set(false); },
      error: () => { this.error.set('Previous sessions could not load. Try again.'); this.loading.set(false); },
    });
  }
  protected async forgetPlayer(name: string) {
    const player = this.recent.items().find((p) => p.displayName === name);
    if (player) await this.run(() => this.recent.remove(player.id), 'Removed from Recent Players.');
    this.selection.update((names) => names.filter((n) => n !== name));
  }
  protected async forgetRoster() {
    await this.run(async () => {
      await this.previous.forget(this.previousCode());
      this.rosters.update((items) => items.filter((item) => item.code !== this.previousCode()));
      this.previousCode.set(''); this.selection.set([]);
    }, 'Session forgotten on this browser. The shared session is unchanged.');
  }
  protected async confirm() {
    const action = this.confirmation();
    await this.run(async () => {
      if (action === 'group') { await this.groups.remove(this.groupId()); this.groupId.set(''); }
      if (action === 'recent') await this.recent.clear();
      this.selection.set([]); this.confirmation.set(null);
    }, action === 'group' ? 'Saved group deleted.' : 'Recent Players cleared.');
  }
  private async run(operation: () => Promise<void>, message: string) {
    if (this.saving()) return;
    this.saving.set(true); this.error.set(''); this.message.set('');
    try { await operation(); this.message.set(message); }
    catch (error) { this.error.set((error as Error).message); }
    finally { this.saving.set(false); }
  }
}
