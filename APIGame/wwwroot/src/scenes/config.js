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

export const DEFAULT_CASTLES = [
    { x: 11, y: 0 },
    { x: 11, y: 14 }
];

export const DEFAULT_ROCKS = [
    { x: 0, y: 7 },
    { x: 1, y: 7 },
    { x: 2, y: 7 },
    { x: 3, y: 7 },
    { x: 4, y: 7 },
    { x: 5, y: 7 },
    { x: 6, y: 4 },
    { x: 7, y: 4 },
    { x: 8, y: 4 },
    { x: 9, y: 4 },
    { x: 10, y: 4 },
    { x: 11, y: 4 },
    { x: 12, y: 4 },
    { x: 13, y: 4 },
    { x: 14, y: 4 },
    { x: 15, y: 4 },
    { x: 16, y: 4 },
    { x: 17, y: 4 },
    { x: 18, y: 7 },
    { x: 19, y: 7 },
    { x: 20, y: 7 },
    { x: 21, y: 7 },
    { x: 22, y: 7 },
    { x: 23, y: 7 },
    { x: 0, y: 8 },
    { x: 1, y: 8 },
    { x: 2, y: 8 },
    { x: 3, y: 8 },
    { x: 4, y: 8 },
    { x: 5, y: 8 },
    { x: 6, y: 11 },
    { x: 7, y: 11 },
    { x: 8, y: 11 },
    { x: 9, y: 11 },
    { x: 10, y: 11 },
    { x: 11, y: 11 },
    { x: 12, y: 11 },
    { x: 13, y: 11 },
    { x: 14, y: 11 },
    { x: 15, y: 11 },
    { x: 16, y: 11 },
    { x: 17, y: 11 },
    { x: 18, y: 8 },
    { x: 19, y: 8 },
    { x: 20, y: 8 },
    { x: 21, y: 8 },
    { x: 22, y: 8 },
    { x: 23, y: 8 }
];

export const gridToPixels = (gridX, gridY) => ({
    x: gridX * TILE_SIZE,
    y: gridY * TILE_SIZE
});
