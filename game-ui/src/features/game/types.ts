export type ConnectionStatus = 'connecting' | 'connected' | 'disconnected' | 'error';

export interface GridPosition {
  x: number;
  y: number;
}

export interface TankState extends GridPosition {
  username: string;
  base: number;
  head: number;
}

export interface BulletState extends GridPosition {
  id: string;
}

export interface ExplosionState extends GridPosition {
  id: string;
}

export interface GameState {
  connectionStatus: ConnectionStatus;
  connectionMessage: string;
  castles: GridPosition[];
  rocks: GridPosition[];
  tanks: TankState[];
  bullets: BulletState[];
  explosions: ExplosionState[];
  infoText: string[];
  snapshotCounter: number;
}
