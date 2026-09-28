import { PlaySession } from './play.models';

export function playerMatchups(
  session: Pick<PlaySession, 'players' | 'matchHistory'>,
  playerId: string,
) {
  const relationships = new Map(
    session.players
      .filter((p) => p.id !== playerId)
      .map((p) => [
        p.id,
        {
          playerId: p.id,
          displayName: p.displayName,
          teamedWith: 0,
          playedAgainst: 0,
        },
      ]),
  );
  // The canonical API includes only Completed matches in matchHistory. Current/Ready
  // matches are deliberately not an input, even when a completed court is still held.
  for (const match of session.matchHistory) {
    const selected = match.players.find((p) => p.playerId === playerId);
    if (!selected) continue;
    for (const other of match.players) {
      const row = relationships.get(other.playerId);
      if (!row) continue;
      if (selected.team === other.team) row.teamedWith++;
      else row.playedAgainst++;
    }
  }
  const rows = [...relationships.values()];
  return {
    rows,
    total: rows.length,
    teammatesMet: rows.filter((r) => r.teamedWith > 0).length,
    opponentsFaced: rows.filter((r) => r.playedAgainst > 0).length,
  };
}
