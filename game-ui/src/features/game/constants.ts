export const GRID_COLUMNS = 24;
export const GRID_ROWS = 16;
export const TILE_SIZE = 64;
export const ANIMATION_DURATION_MS = 500;

export const FRAMES = {
  groundTopLeft: 0,
  groundTopRight: 1,
  explosionA: 2,
  explosionB: 3,
  groundBottomLeft: 4,
  groundBottomRight: 5,
  tankBase: 6,
  tankHead: 7,
  castleTopLeft: 8,
  castleTopRight: 9,
  rock: 10,
  castleBottomLeft: 12,
  castleBottomRight: 13,
} as const;

export const SPRITE_SHEET_URL = '/assets/assets.png';
export const SPRITE_SHEET_COLUMNS = 4;
