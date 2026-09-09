export type SessionStatus = 'Draft' | 'Active' | 'Ended';
export type PlayerState = 'Waiting' | 'Playing' | 'Resting';
export type PlayTeam = 'A' | 'B';

export interface PlayScoreCorrection {
  teamAScore: number;
  teamBScore: number;
  servingTeam: PlayTeam;
  currentServerNumber: number;
}

export interface PlayMatch {
  id: string;
  courtNumber: number;
  status: 'Active';
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
  id: string;
  joinCode: string;
  name: string;
  sessionDate: string;
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

export interface CreatePlaySession {
  name: string;
  date: string;
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
