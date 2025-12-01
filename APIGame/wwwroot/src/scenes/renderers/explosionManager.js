import { ANIMATION_DURATION_MS, FRAMES, SPRITESHEET_KEY, TILE_SIZE } from '../config.js';

export class ExplosionManager {
    constructor(scene) {
        this.scene = scene;
    }

    createAnimation() {
        this.scene.anims.create({
            key: 'tank-explosion',
            frames: this.scene.anims.generateFrameNumbers(SPRITESHEET_KEY, { frames: FRAMES.explosion }),
            duration: ANIMATION_DURATION_MS,
            repeat: 0
        });
    }

    render(explosions) {
        explosions.forEach(({ x, y }) => {
            const explosion = this.scene.add.sprite(x * TILE_SIZE, y * TILE_SIZE, SPRITESHEET_KEY, FRAMES.explosion[0]).setOrigin(0);
            explosion.play('tank-explosion');
            explosion.once('animationcomplete', () => explosion.destroy());
        });
    }
}
