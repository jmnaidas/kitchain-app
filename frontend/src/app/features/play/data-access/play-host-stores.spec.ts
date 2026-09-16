import { TestBed } from '@angular/core/testing';
import { BrowserPlayPresets, PRESETS_KEY, PlayConfiguration } from './play-presets';
import { BrowserPlayGroups, GROUPS_KEY } from './play-groups';
import { BrowserRecentPlayers, RECENT_PLAYERS_KEY } from './play-recent-players';
import { rotationStyles } from './play.models';

const config: PlayConfiguration = { mode: 'QueueOnly', rotationMode: 'FairRotation', numberOfCourts: 2, maximumPlayers: 16 };
const keys = [PRESETS_KEY, GROUPS_KEY, RECENT_PLAYERS_KEY];
describe('Browser-local Play host stores', () => {
  beforeEach(() => { keys.forEach((key) => localStorage.removeItem(key)); TestBed.configureTestingModule({}); });
  afterEach(() => { vi.restoreAllMocks(); keys.forEach((key) => localStorage.removeItem(key)); });
  const presets = () => TestBed.inject(BrowserPlayPresets);
  const groups = () => TestBed.inject(BrowserPlayGroups);
  const recent = () => TestBed.inject(BrowserRecentPlayers);

  it('starts empty and stores only reusable preset fields across recreation', async () => {
    expect(presets().items()).toEqual([]);
    const saved = await presets().save(' Saturday ', { ...config, name: 'Session name', date: '2026-09-16', players: ['Alex'], status: 'Ended' } as PlayConfiguration);
    expect(Object.keys(saved).sort()).toEqual(['id', 'name', 'mode', 'rotationMode', 'numberOfCourts', 'maximumPlayers'].sort());
    expect(saved.name).toBe('Saturday');
    TestBed.resetTestingModule();
    expect(presets().items()).toEqual([saved]);
  });
  for (const mode of ['QueueOnly', 'LiveScoring'] as const) {
    for (const style of rotationStyles) {
      it('persists ' + mode + ' with ' + style.value, async () => {
        const saved = await presets().save('Crew', { ...config, mode, rotationMode: style.value });
        TestBed.resetTestingModule();
        expect(presets().items()[0]).toEqual(saved);
        expect(saved.numberOfCourts).toBe(2); expect(saved.maximumPlayers).toBe(16);
      });
    }
  }
  it('rejects invalid/duplicate names and invalid configurations; renames, updates and deletes', async () => {
    await expect(presets().save('  ', config)).rejects.toThrow('1–60');
    await expect(presets().save('x'.repeat(61), config)).rejects.toThrow('1–60');
    await expect(presets().save('Bad', { ...config, numberOfCourts: 0 })).rejects.toThrow('valid session settings');
    await expect(presets().save('Bad', { ...config, maximumPlayers: -1 })).rejects.toThrow('valid session settings');
    const first = await presets().save('Weekend', config);
    await expect(presets().save(' WEEKEND ', config)).rejects.toThrow('already saved');
    const updated = await presets().save('Sunday', { ...config, maximumPlayers: null, numberOfCourts: 3 }, first.id);
    expect(updated.id).toBe(first.id); expect(presets().items()).toHaveLength(1);
    expect(updated.maximumPlayers).toBeNull();
    await presets().remove(first.id); expect(presets().items()).toEqual([]);
  });
  it('bounds saved presets and groups instead of growing without limit', async () => {
    for (let i = 0; i < 30; i++) { await presets().save('Preset ' + i, config); await groups().save('Group ' + i, ['Alex']); }
    await expect(presets().save('Extra', config)).rejects.toThrow('30 presets');
    await expect(groups().save('Extra', ['Alex'])).rejects.toThrow('30 groups');
  });
  it('creates independent local player IDs, merges normalized names and preserves IDs on group edits', async () => {
    const first = await groups().save(' Office ', [' Alex  Lee ', 'alex lee', 'Sam', 'SAM']);
    expect(first.players.map((p) => p.displayName)).toEqual(['Alex  Lee', 'Sam']);
    expect(new Set(first.players.map((p) => p.id)).size).toBe(2);
    await expect(groups().save('office', ['Other'])).rejects.toThrow('already saved');
    await expect(groups().save(' ', ['Alex'])).rejects.toThrow('1–60');
    await expect(groups().save('Empty', [])).rejects.toThrow('1–100');
    await expect(groups().save('Too long', ['x'.repeat(81)])).rejects.toThrow('1–100');
    await expect(groups().save('Too many', Array.from({ length: 101 }, (_, i) => 'P' + i))).rejects.toThrow('1–100');
    const changed = await groups().save('Office crew', ['alex lee', 'Taylor'], first.id);
    expect(changed.players[0].id).toBe(first.players[0].id);
    expect(changed.players[1].id).not.toBe(first.players[1].id);
    expect(changed.createdAt).toBe(first.createdAt);
    TestBed.resetTestingModule(); expect(groups().items()).toEqual([changed]);
    await groups().remove(first.id); expect(groups().items()).toEqual([]);
  });
  it('deduplicates recent names, moves reused names to front, bounds and clears them', async () => {
    await recent().remember(' Alex  Lee '); const id = recent().items()[0].id;
    await recent().remember('Sam'); await recent().remember('alex lee');
    expect(recent().items().map((p) => p.displayName)).toEqual(['alex lee', 'Sam']);
    expect(recent().items()[0].id).toBe(id);
    for (let i = 0; i < 55; i++) await recent().remember('Player ' + i);
    expect(recent().items()).toHaveLength(50); expect(recent().items()[0].displayName).toBe('Player 54');
    TestBed.resetTestingModule(); expect(recent().items()).toHaveLength(50);
    await recent().remove(recent().items()[0].id); expect(recent().items()).toHaveLength(49);
    await recent().clear(); expect(recent().items()).toEqual([]);
  });
  for (const raw of ['broken JSON', '{}', 'null', '[null,1,{"id":"bad"}]']) {
    it('recovers safely from malformed storage: ' + raw, async () => {
      keys.forEach((key) => localStorage.setItem(key, raw));
      expect(presets().items()).toEqual([]); expect(groups().items()).toEqual([]); expect(recent().items()).toEqual([]);
      await presets().save('Recovered', config); await groups().save('Recovered', ['Alex']); await recent().remember('Alex');
      expect(presets().items()).toHaveLength(1); expect(groups().items()).toHaveLength(1); expect(recent().items()).toHaveLength(1);
    });
  }
  it('drops corrupt entries and unexpected sensitive/session fields while preserving valid data', async () => {
    const saved = await presets().save('Valid', config);
    localStorage.setItem(PRESETS_KEY, JSON.stringify([null, { ...saved, joinCode: 'ABCDEF', scores: [11, 8] }, saved]));
    localStorage.setItem(GROUPS_KEY, JSON.stringify([{ id: 'g', name: 'Crew', createdAt: 1, updatedAt: 2,
      players: [null, { id: 'p', displayName: 'Alex', score: 11 }, { id: 'q', displayName: 'alex' }] }]));
    TestBed.resetTestingModule();
    expect(presets().items()).toEqual([saved]);
    expect(groups().items()[0].players).toEqual([{ id: 'p', displayName: 'Alex' }]);
  });
  it('handles unavailable storage without throwing at construction or falsely reporting a save', async () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('Disabled'); });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('Full'); });
    expect(presets().items()).toEqual([]); expect(groups().items()).toEqual([]); expect(recent().items()).toEqual([]);
    await expect(presets().save('Crew', config)).rejects.toThrow('could not be saved');
    await expect(groups().save('Crew', ['Alex'])).rejects.toThrow('could not be saved');
    await expect(recent().remember('Alex')).rejects.toThrow('could not be saved');
    expect(presets().items()).toEqual([]); expect(groups().items()).toEqual([]);
  });
});
