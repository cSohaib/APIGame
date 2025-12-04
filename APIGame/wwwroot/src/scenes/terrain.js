import ASSETS from '../assets.js';
import { FRAMES, GRID_COLUMNS, GRID_ROWS, SPRITESHEET_KEY, TILE_SIZE, gridToPixels } from './config.js';

export class Terrain {
    constructor(scene) {
        this.scene = scene;
        this.blockedPositions = new Set();
        this.groundKey = 'ground-tile';
    }

    build(castles = [], rocks = []) {
        this.blockedPositions.clear();
        this.createGround();
        this.createCastles(castles);
        this.createRocks(rocks);
    }

    createGround() {
        const width = GRID_COLUMNS * TILE_SIZE;
        const height = GRID_ROWS * TILE_SIZE;

        if (!this.scene.textures.exists(this.groundKey)) {
            const groundTexture = this.scene.textures.createCanvas(this.groundKey, TILE_SIZE * 2, TILE_SIZE * 2);
            const ctx = groundTexture.getContext();
            const groundFrames = FRAMES.ground;

            groundFrames.forEach((frameIndex, idx) => {
                const frame = this.scene.textures.getFrame(ASSETS.spritesheet.assets.key, frameIndex);
                if (!frame) {
                    return;
                }

                const offsetX = (idx % 2) * TILE_SIZE;
                const offsetY = Math.floor(idx / 2) * TILE_SIZE;

                ctx.drawImage(
                    frame.source.image,
                    frame.cutX,
                    frame.cutY,
                    frame.cutWidth,
                    frame.cutHeight,
                    offsetX,
                    offsetY,
                    TILE_SIZE,
                    TILE_SIZE
                );
            });

            groundTexture.refresh();
        }

        this.scene.add.tileSprite(0, 0, width, height, this.groundKey).setOrigin(0);
    }

    createCastles(positions) {
        positions.forEach(({ x, y }) => this.buildCastle(x, y));
    }

    buildCastle(gridX, gridY) {
        const { x, y } = gridToPixels(gridX, gridY);
        const castle = this.scene.add.container(x, y);
        const tiles = [
            { frame: FRAMES.castle[0], offsetX: 0, offsetY: 0 },
            { frame: FRAMES.castle[1], offsetX: TILE_SIZE, offsetY: 0 },
            { frame: FRAMES.castle[2], offsetX: 0, offsetY: TILE_SIZE },
            { frame: FRAMES.castle[3], offsetX: TILE_SIZE, offsetY: TILE_SIZE }
        ];

        tiles.forEach(({ frame, offsetX, offsetY }) => {
            castle.add(
                this.scene.add.image(offsetX, offsetY, SPRITESHEET_KEY, frame).setOrigin(0)
            );
        });

        for (let dx = 0; dx < 2; dx += 1) {
            for (let dy = 0; dy < 2; dy += 1) {
                this.blockedPositions.add(`${gridX + dx},${gridY + dy}`);
            }
        }
    }

    createRocks(rocks) {
        rocks.forEach(({ x, y }) => {
            this.blockedPositions.add(`${x},${y}`);
            const { x: px, y: py } = gridToPixels(x, y);
            this.scene.add.image(px, py, SPRITESHEET_KEY, FRAMES.rock).setOrigin(0);
        });
    }
}
