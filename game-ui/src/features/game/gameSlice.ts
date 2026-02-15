import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import type { BulletState, ExplosionState, GameState, GridPosition, TankState } from './types';

const initialState: GameState = {
  connectionStatus: 'connecting',
  connectionMessage: 'Connecting to API...',
  castles: [],
  rocks: [],
  tanks: [],
  bullets: [],
  explosions: [],
  infoText: [],
  snapshotCounter: 0,
};

const gameSlice = createSlice({
  name: 'game',
  initialState,
  reducers: {
    socketConnecting(state) {
      state.connectionStatus = 'connecting';
      state.connectionMessage = 'Connecting to API...';
    },
    socketConnected(state) {
      state.connectionStatus = 'connected';
      state.connectionMessage = 'Connected';
    },
    socketClosed(state) {
      state.connectionStatus = 'disconnected';
      state.connectionMessage = 'Disconnected. Reconnecting...';
    },
    socketError(state, action: PayloadAction<string>) {
      state.connectionStatus = 'error';
      state.connectionMessage = action.payload;
    },
    applyInitialization(state, action: PayloadAction<{ castles: GridPosition[]; rocks: GridPosition[] }>) {
      state.castles = action.payload.castles;
      state.rocks = action.payload.rocks;
    },
    applyState(
      state,
      action: PayloadAction<{
        tanks: TankState[];
        bullets: BulletState[];
        explosions: GridPosition[];
        infoText: string[];
      }>,
    ) {
      state.snapshotCounter += 1;
      state.tanks = action.payload.tanks;
      state.bullets = action.payload.bullets;
      state.explosions = action.payload.explosions.map((explosion, index): ExplosionState => ({
        ...explosion,
        id: `${state.snapshotCounter}-${index}`,
      }));
      state.infoText = action.payload.infoText;
    },
  },
});

export const {
  socketConnecting,
  socketConnected,
  socketClosed,
  socketError,
  applyInitialization,
  applyState,
} = gameSlice.actions;

export const gameReducer = gameSlice.reducer;
