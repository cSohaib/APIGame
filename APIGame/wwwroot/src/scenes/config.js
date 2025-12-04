import ASSETS from '../assets.js';

export const GRID_COLUMNS = 24;
export const GRID_ROWS = 16;
export const TILE_SIZE = 64;

export const SPRITESHEET_KEY = ASSETS.spritesheet.assets.key;

export const FRAMES = {
    ground: [0, 1, 4, 5],
    explosion: [2, 3],
    tankBase: 6,
    tankHead: 7,
    rock: 10,
    castle: [8, 9, 12, 13]
};

export const PANEL_STYLE = {
    fontSize: '20px',
    fontFamily: 'Arial',
    color: '#ffffff',
    backgroundColor: '#00000099',
    padding: { x: 8, y: 6 }
};

export const ANIMATION_DURATION_MS = 500;

export const gridToPixels = (gridX, gridY) => ({
    x: gridX * TILE_SIZE,
    y: gridY * TILE_SIZE
});
