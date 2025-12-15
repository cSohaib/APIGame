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
            const targetX = x * TILE_SIZE + TILE_SIZE / 2;
            const targetY = y * TILE_SIZE + TILE_SIZE / 2;

            if (!existing) {
                this.bulletsById.set(id, { lastX: targetX, lastY: targetY });
                seenIds.add(id);
                return;
            }

            const { sprite, lastX, lastY } = existing;
            this.stopBulletTween(id);

            if (!sprite) {
                const bullet = this.scene.add.circle(lastX, lastY, TILE_SIZE / 8, 0xfff275, 1);
                bullet.setStrokeStyle(2, 0xffc107, 0.8);
                bullet.setDepth(1);

                existing.sprite = bullet;
            }

            const tween = this.scene.tweens.add({
                targets: existing.sprite,
                x: targetX,
                y: targetY,
                duration: ANIMATION_DURATION_MS,
                ease: 'Linear'
            });

            this.bulletTweens.set(id, tween);
            existing.lastX = targetX;
            existing.lastY = targetY;
            seenIds.add(id);
        });

        [...this.bulletsById.keys()].forEach((bulletId) => {
            if (!seenIds.has(bulletId)) {
                this.stopBulletTween(bulletId);
                const { sprite } = this.bulletsById.get(bulletId);
                if (sprite) {
                    sprite.destroy();
                }
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
