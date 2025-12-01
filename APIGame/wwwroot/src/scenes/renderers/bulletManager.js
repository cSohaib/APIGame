import { ANIMATION_DURATION_MS, TILE_SIZE } from '../config.js';

export class BulletManager {
    constructor(scene) {
        this.scene = scene;
        this.bulletsById = new Map();
        this.bulletTweens = new Map();
    }

    render(bullets) {
        const seenIds = new Set();

        bullets.forEach(({ id, x, y }) => {
            const existing = this.bulletsById.get(id);
            const isNew = !existing;
            const targetX = x * TILE_SIZE + TILE_SIZE / 2;
            const targetY = y * TILE_SIZE + TILE_SIZE / 2;

            if (isNew) {
                const bullet = this.scene.add.circle(targetX, targetY, TILE_SIZE / 8, 0xfff275, 1);
                bullet.setStrokeStyle(2, 0xffc107, 0.8);
                bullet.setDepth(1);

                this.bulletsById.set(id, { sprite: bullet });
                seenIds.add(id);
                return;
            }

            const { sprite } = existing;
            this.stopBulletTween(id);

            const tween = this.scene.tweens.add({
                targets: sprite,
                x: targetX,
                y: targetY,
                duration: ANIMATION_DURATION_MS,
                ease: 'Linear'
            });

            this.bulletTweens.set(id, tween);
            seenIds.add(id);
        });

        [...this.bulletsById.keys()].forEach((bulletId) => {
            if (!seenIds.has(bulletId)) {
                this.stopBulletTween(bulletId);
                const { sprite } = this.bulletsById.get(bulletId);
                sprite.destroy();
                this.bulletsById.delete(bulletId);
            }
        });
    }

    stopBulletTween(bulletId) {
        const tween = this.bulletTweens.get(bulletId);
        if (tween && tween.isPlaying()) {
            tween.stop();
        }
        this.bulletTweens.delete(bulletId);
    }
}
