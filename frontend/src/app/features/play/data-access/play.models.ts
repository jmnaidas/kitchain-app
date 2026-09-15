export type SessionStatus = 'Draft' | 'Active' | 'Ended';
export type PlayerState = 'Waiting' | 'Playing' | 'Resting';
export type PlayTeam = 'A' | 'B';
export type PlaySessionMode = 'QueueOnly' | 'LiveScoring';
export type PlayRallyCallOut =
  'Drive' | 'Dink' | 'Lob' | 'Fault' | 'Out' | 'Kitchen' | 'ServiceBreak';
export interface PlayRallyEvent {
  id: string;
  matchId: string;
  sequence: number;
  winner: PlayTeam;
  pointAwarded: boolean;
  callOut: PlayRallyCallOut | null;
  teamAScore: number;
  teamBScore: number;
  servingTeam: PlayTeam;
  currentServerNumber: number;
  createdAt: string;
}
export interface PlayCallOutEdit {
  callOut: PlayRallyCallOut | null;
  expectedCallOut: PlayRallyCallOut | null;
}
export interface PlayCallOutChange {
  matchId: string;
  rallyId: string;
  edit: PlayCallOutEdit;
}
export interface NextPlayGame {
  playerIds: string[];
  overrideLineup: boolean;
}
export interface PlayLineupPlayer {
  playerId: string;
  displayName: string;
  team: PlayTeam;
  position: number;
}

export interface PlayScoreCorrection {
  teamAScore: number;
  teamBScore: number;
  servingTeam: PlayTeam;
  currentServerNumber: number;
}

export interface PlayMatch {
  rallies: PlayRallyEvent[];
  id: string;
  courtNumber: number;
  status: 'Active' | 'Completed';
  completedAt: string | null;
  winner: PlayTeam | null;
  nextLineup: PlayLineupPlayer[];
  eligiblePlayers: PlayPlayer[];
  startedAt: string;
  teamAScore: number;
  teamBScore: number;
  servingTeam: PlayTeam;
  currentServerNumber: number;
  players: { playerId: string; displayName: string; team: 'A' | 'B'; position: number }[];
}

export interface PlayPlayer {
  id: string;
  sessionId: string;
  displayName: string;
  identityType: 'Guest';
  state: PlayerState;
  joinedAt: string;
  updatedAt: string;
  queueOrder: number | null;
}

export interface PlaySession {
  queue: {
    nextUp: PlayPlayer[];
    waiting: PlayPlayer[];
    neededPlayers: number;
    heldPlayers: number;
    courtNumber: number | null;
  };
  insights: PlayInsights;
  matchHistory: PlayMatchSummary[];
  mode: PlaySessionMode;
  currentMatches: PlayMatch[];
  id: string;
  joinCode: string;
  name: string;
  sessionDate: string;
  endDate: string;
  startTime: string;
  endTime: string;
  numberOfCourts: number;
  maximumPlayers: number | null;
  status: SessionStatus;
  rotationMode: 'FairRotation';
  scoringMode: 'Traditional';
  gameTo: number;
  winBy: number;
  createdAt: string;
  updatedAt: string;
  players: PlayPlayer[];
  waitingQueue: PlayPlayer[];
  activeMatches: PlayMatch[];
}

export interface PlayInsights {
  totalPlayers: number;
  numberOfCourts: number;
  completedGames: number;
  playerAppearances: number;
  recordedRallies: number | null;
  taggedRallies: number | null;
  players: {
    playerId: string;
    displayName: string;
    gamesPlayed: number;
    wins: number;
    losses: number;
    distinctTeammates: number;
    distinctOpponents: number;
    isRemoved?: boolean;
  }[];
}

export interface PlayMatchSummary {
  id: string;
  courtNumber: number;
  players: PlayLineupPlayer[];
  startedAt: string;
  completedAt: string | null;
  teamAScore: number | null;
  teamBScore: number | null;
  winner: PlayTeam | null;
  totalRallies: number | null;
  taggedRallies: number | null;
  callOutCounts: { callOut: PlayRallyCallOut; count: number }[];
  rallies: PlayRallyEvent[];
}

export interface CreatePlaySession {
  mode: PlaySessionMode;
  name: string;
  date: string;
  endDate: string;
  startTime: string;
  endTime: string;
  numberOfCourts: number;
  maximumPlayers: number | null;
}

export const normalizeCode = (code: string) => code.trim().toUpperCase();
export const validCode = (code: string) => /^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{6}$/.test(code);

export function localDate(now = new Date()) {
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
}
