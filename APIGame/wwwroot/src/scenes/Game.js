import { GRID_COLUMNS, GRID_ROWS, TILE_SIZE } from './config.js';
import { Terrain } from './terrain.js';
import { InfoPanel } from './ui/infoPanel.js';
import { TankManager } from './renderers/tankManager.js';
import { BulletManager } from './renderers/bulletManager.js';
import { ExplosionManager } from './renderers/explosionManager.js';
import { DemoLoop } from './demoLoop.js';

export class Game extends Phaser.Scene {
    constructor() {
        super('Game');
        this.blockedPositions = new Set();
    }

    create() {
        this.terrain = new Terrain(this);
        this.terrain.build();
        this.blockedPositions = this.terrain.blockedPositions;

        this.infoPanel = new InfoPanel(this);
        this.infoPanel.create([
            'Rendering demo updates every 500ms',
            'Positions driven by external data',
            '',
            `Grid: ${GRID_COLUMNS} x ${GRID_ROWS}`,
            `Tile size: ${TILE_SIZE}px`,
            `Info panel width: ${this.infoPanel.panelWidth}px`
        ]);

        this.tanks = new TankManager(this);
        this.bullets = new BulletManager(this);
        this.explosions = new ExplosionManager(this);

        this.explosions.createAnimation();

        this.demo = new DemoLoop(this, (state) => this.renderGameState(state));
        this.demo.start();
    }

    renderGameState({ tanks = [], bullets = [], explosions = [], infoText = [] }) {
        this.tanks.render(tanks);
        this.bullets.render(bullets);
        this.explosions.render(explosions);

        if (infoText.length) {
            this.infoPanel.update(infoText);
        }
    }
}
