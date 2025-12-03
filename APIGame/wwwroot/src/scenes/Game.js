import { GRID_COLUMNS, GRID_ROWS, TILE_SIZE } from './config.js';
import { Terrain } from './terrain.js';
import { InfoPanel } from './ui/infoPanel.js';
import { TankManager } from './renderers/tankManager.js';
import { BulletManager } from './renderers/bulletManager.js';
import { ExplosionManager } from './renderers/explosionManager.js';

export class Game extends Phaser.Scene {
    constructor() {
        super('Game');
        this.blockedPositions = new Set();
        this.socket = null;
    }

    create() {
        this.terrain = new Terrain(this);
        this.blockedPositions = this.terrain.blockedPositions;

        this.infoPanel = new InfoPanel(this);
        this.infoPanel.create([
            'Connecting to API...',
            `Grid: ${GRID_COLUMNS} x ${GRID_ROWS}`,
            `Tile size: ${TILE_SIZE}px`,
            `Info panel width: ${this.infoPanel.panelWidth}px`
        ]);

        this.tanks = new TankManager(this);
        this.bullets = new BulletManager(this);
        this.explosions = new ExplosionManager(this);

        this.explosions.createAnimation();
        this.connectToApi();
    }

    renderGameState({ tanks = [], bullets = [], explosions = [], infoText = [] }) {
        this.tanks.render(tanks);
        this.bullets.render(bullets);
        this.explosions.render(explosions);

        if (infoText.length) {
            this.infoPanel.update(infoText);
        }
    }

    connectToApi() {
        const protocol = window.location.protocol === 'https:' ? 'wss' : 'ws';
        this.socket = new WebSocket(`${protocol}://${window.location.host}/ws`);

        this.socket.addEventListener('open', () => {
            this.socket.send(JSON.stringify({ type: 'join', role: 'spectator' }));
        });

        this.socket.addEventListener('message', (event) => {
            try {
                const message = JSON.parse(event.data);

                if (message.Type === 'initialization') {
                    const castles = (message.Data?.Castles || []).map((castle) => ({
                        x: castle.X,
                        y: castle.Y
                    }));
                    const rocks = (message.Data?.Rocks || []).map((rock) => ({ x: rock.X, y: rock.Y }));

                    this.terrain.build(castles, rocks);
                    return;
                }

                if (message.Type === 'state') {
                    const data = message.Data || {};
                    const tanks = (data.Tanks || []).map((tank) => ({
                        username: tank.Username,
                        x: tank.X,
                        y: tank.Y,
                        base: tank.Base,
                        head: tank.Head
                    }));
                    const bullets = (data.Bullets || []).map((bullet) => ({
                        id: bullet.Id,
                        x: bullet.X,
                        y: bullet.Y
                    }));
                    const explosions = (data.Explosions || []).map((explosion) => ({
                        x: explosion.X,
                        y: explosion.Y
                    }));
                    const infoText = data.InfoText || [];

                    this.renderGameState({ tanks, bullets, explosions, infoText });
                    return;
                }

                if (message.Type === 'error') {
                    this.infoPanel.update([message.Data]);
                }
            }
            catch (err) {
                console.error('Failed to parse message', err, event.data);
            }
        });
    }
}
