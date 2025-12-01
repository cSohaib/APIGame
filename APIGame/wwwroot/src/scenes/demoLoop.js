import { GRID_COLUMNS, GRID_ROWS } from './config.js';

const tank1Steps = [
    { id: 1, x: 0, y: 0, base: 1, head: 0 },
    { id: 1, x: 1, y: 0, base: 1, head: 1 },
    { id: 1, x: 2, y: 0, base: 1, head: 1 },
    { id: 1, x: 3, y: 0, base: 1, head: 1 },
    { id: 1, x: 3, y: 0, base: 2, head: 1 },
    { id: 1, x: 3, y: 1, base: 2, head: 2 },
    { id: 1, x: 3, y: 2, base: 2, head: 2 },
    { id: 1, x: 3, y: 3, base: 2, head: 2 },
    { id: 1, x: 3, y: 3, base: 3, head: 2 },
    { id: 1, x: 2, y: 3, base: 3, head: 3 },
    { id: 1, x: 1, y: 3, base: 3, head: 3 },
    { id: 1, x: 0, y: 3, base: 3, head: 3 },
    { id: 1, x: 0, y: 3, base: 0, head: 3 },
    { id: 1, x: 0, y: 2, base: 0, head: 0 },
    { id: 1, x: 0, y: 1, base: 0, head: 0 },
    { id: 1, x: 0, y: 0, base: 0, head: 0 }
];

const tank2Steps = [
    { id: 2, x: 23, y: 15, base: 1, head: 0 },
    { id: 2, x: 22, y: 15, base: 1, head: 1 },
    { id: 2, x: 21, y: 15, base: 1, head: 1 },
    { id: 2, x: 20, y: 15, base: 1, head: 1 },
    { id: 2, x: 20, y: 15, base: 2, head: 1 },
    { id: 2, x: 20, y: 14, base: 2, head: 2 },
    { id: 2, x: 20, y: 13, base: 2, head: 2 },
    { id: 2, x: 20, y: 12, base: 2, head: 2 },
    { id: 2, x: 20, y: 12, base: 3, head: 2 },
    { id: 2, x: 21, y: 12, base: 3, head: 3 },
    { id: 2, x: 22, y: 12, base: 3, head: 3 },
    { id: 2, x: 23, y: 12, base: 3, head: 3 },
    { id: 2, x: 23, y: 12, base: 0, head: 3 },
    { id: 2, x: 23, y: 13, base: 0, head: 0 },
    { id: 2, x: 23, y: 14, base: 0, head: 0 },
    { id: 2, x: 23, y: 15, base: 0, head: 0 }
];

const bulletSteps = [
    { id: 101, x: 0, y: 0 },
    { id: 101, x: 3, y: 0 },
    { id: 101, x: 7, y: 0 },
    { id: 101, x: 11, y: 0 },
    { id: 101, x: 15, y: 0 },
    { id: 101, x: 19, y: 0 },
    { id: 101, x: 23, y: 0 },
    { id: 101, x: 19, y: 3 },
    { id: 101, x: 15, y: 7 },
    { id: 101, x: 11, y: 9 },
    { id: 101, x: 7, y: 11 },
    { id: 101, x: 3, y: 13 },
    { id: 101, x: 0, y: 15 },
    { id: 101, x: 0, y: 11 },
    { id: 101, x: 0, y: 8 },
    { id: 101, x: 0, y: 4 }
];

const randomPosition = () => ({
    x: Phaser.Math.Between(0, GRID_COLUMNS - 1),
    y: Phaser.Math.Between(0, GRID_ROWS - 1)
});

export class DemoLoop {
    constructor(scene, renderGameState) {
        this.scene = scene;
        this.renderGameState = renderGameState;
        this.timer = null;
        this.stepIndex = 0;
    }

    start() {
        this.timer = this.scene.time.addEvent({
            delay: 500,
            loop: true,
            callback: () => this.tick()
        });
    }

    tick() {
        const tank1Step = tank1Steps[this.stepIndex];
        const tank2Step = tank2Steps[this.stepIndex];
        const bulletStep = bulletSteps[this.stepIndex % bulletSteps.length];
        const explosionCount = Phaser.Math.Between(0, 2);

        const tanks = [tank1Step, tank2Step];
        const bullets = [bulletStep];
        const explosions = Array.from({ length: explosionCount }, randomPosition);

        const infoText = [
            'Rendering demo updates every 500ms',
            `Step: ${this.stepIndex + 1} / ${tank1Steps.length}`,
            `Tank id ${tank1Step.id}: base ${tank1Step.base}, head ${tank1Step.head}`,
            `Tank id ${tank2Step.id}: base ${tank2Step.base}, head ${tank2Step.head}`,
            `Bullet id ${bulletStep.id}: (${bulletStep.x}, ${bulletStep.y})`,
            '',
            `Tanks: ${tanks.length}`,
            `Bullets: ${bullets.length}`,
            `Explosions: ${explosionCount}`
        ];

        this.renderGameState({ tanks, bullets, explosions, infoText });
        this.stepIndex = (this.stepIndex + 1) % tank1Steps.length;
    }
}
