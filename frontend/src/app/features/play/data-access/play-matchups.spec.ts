import { playerMatchups } from './play-matchups';
import { PlayMatchSummary, PlayPlayer } from './play.models';

const players: PlayPlayer[] = ['JM', 'Ryan', 'Carlo', 'Alex', 'New player'].map((name, i) => ({
  id: String(i),
  sessionId: 'one',
  displayName: name,
  identityType: 'Guest',
  state: 'Waiting',
  joinedAt: '2026-09-29T00:00:00Z',
  updatedAt: '2026-09-29T00:00:00Z',
  queueOrder: i + 1,
}));
const completed = (id = 'match', ids = ['0', '1', '2', '3']): PlayMatchSummary => ({
  id,
  courtNumber: 1,
  players: ids.map((playerId, i) => ({
    playerId,
    displayName: playerId,
    team: i < 2 ? 'A' : 'B',
    position: i + 1,
  })),
  startedAt: '2026-09-29T00:00:00Z',
  completedAt: '2026-09-29T00:20:00Z',
  teamAScore: null,
  teamBScore: null,
  winner: null,
  totalRallies: null,
  taggedRallies: null,
  callOutCounts: [],
  rallies: [],
});

describe('session player matchups', () => {
  it('counts same-team and opposite-team relationships symmetrically without score requirements', () => {
    const state = { players, matchHistory: [completed()] };
    for (const id of ['0', '1', '2', '3']) {
      const result = playerMatchups(state, id);
      expect(result.teammatesMet).toBe(1);
      expect(result.opponentsFaced).toBe(2);
      expect(result.total).toBe(4);
      expect(result.rows.some((r) => r.playerId === id)).toBe(false);
      const teammate = String(Number(id) ^ 1);
      expect(result.rows.find((r) => r.playerId === teammate)?.teamedWith).toBe(1);
      const opposite = Number(id) < 2 ? ['2', '3'] : ['0', '1'];
      opposite.forEach((other) =>
        expect(result.rows.find((r) => r.playerId === other)?.playedAgainst).toBe(1),
      );
    }
  });
  it('accumulates repeated games and counts distinct coverage rather than game totals', () => {
    const result = playerMatchups(
      {
        players,
        matchHistory: [completed(), completed('second'), completed('third', ['0', '2', '1', '3'])],
      },
      '0',
    );
    expect(result.rows.find((r) => r.playerId === '1')).toMatchObject({
      teamedWith: 2,
      playedAgainst: 1,
    });
    expect(result.rows.find((r) => r.playerId === '2')).toMatchObject({
      teamedWith: 1,
      playedAgainst: 2,
    });
    expect(result.rows.find((r) => r.playerId === '3')).toMatchObject({
      teamedWith: 0,
      playedAgainst: 3,
    });
    expect(result.teammatesMet).toBe(2);
    expect(result.opponentsFaced).toBe(3);
    expect(result.rows).toHaveLength(4);
  });
  it('retains zero relationships and ignores Ready/unstarted or incomplete current games', () => {
    const state = {
      players,
      matchHistory: [],
      currentMatches: [
        { ...completed(), status: 'Ready', startedAt: null, completedAt: null },
        { ...completed('active'), status: 'Active', completedAt: null },
      ],
    };
    const result = playerMatchups(state, '0');
    expect(result.rows).toHaveLength(4);
    expect(result.rows.every((r) => r.teamedWith === 0 && r.playedAgainst === 0)).toBe(true);
    expect(result.teammatesMet).toBe(0);
    expect(result.opponentsFaced).toBe(0);
  });
  it('does not double count a completed match still held on a current court', () => {
    const state = {
      players,
      matchHistory: [completed()],
      currentMatches: [{ ...completed(), status: 'Completed' }],
    };
    expect(playerMatchups(state, '0').rows.find((r) => r.playerId === '1')?.teamedWith).toBe(1);
  });
  it('uses only the supplied session and never caches relationships between sessions', () => {
    const first = { players, matchHistory: [completed()] };
    const second = { players: players.map((p) => ({ ...p, sessionId: 'two' })), matchHistory: [] };
    expect(playerMatchups(first, '0').teammatesMet).toBe(1);
    expect(playerMatchups(second, '0').teammatesMet).toBe(0);
    expect(playerMatchups(first, '0').teammatesMet).toBe(1);
  });
  it('uses current roster names and excludes removed players while including sitting-out players', () => {
    const roster = players
      .filter((p) => p.id !== '1')
      .map((p) => ({ ...p, state: 'Resting' as const, displayName: 'Current ' + p.displayName }));
    const result = playerMatchups({ players: roster, matchHistory: [completed()] }, '0');
    expect(result.total).toBe(3);
    expect(result.teammatesMet).toBe(0);
    expect(result.opponentsFaced).toBe(2);
    expect(result.rows.find((r) => r.playerId === '2')?.displayName).toBe('Current Carlo');
    expect(result.rows.find((r) => r.playerId === '4')).toMatchObject({
      teamedWith: 0,
      playedAgainst: 0,
    });
  });
});
