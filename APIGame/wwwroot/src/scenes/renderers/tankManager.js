import { ANIMATION_DURATION_MS, FRAMES, SPRITESHEET_KEY, TILE_SIZE } from '../config.js';

export class TankManager {
    constructor(scene) {
        this.scene = scene;
        this.tanksById = new Map();
        this.tankTweens = new Map();
    }

    render(tanks) {
        const seenIds = new Set();

        tanks.forEach(({ id, x, y, base = 0, head = 0 }) => {
            const existing = this.tanksById.get(id);
            const isNew = !existing;
            const targetX = x * TILE_SIZE + TILE_SIZE / 2;
            const targetY = y * TILE_SIZE + TILE_SIZE / 2;
            const targetBaseAngle = base * 90;
            const targetHeadAngle = head * 90;

            if (isNew) {
                this.createTank(id, targetX, targetY, targetBaseAngle, targetHeadAngle);
                seenIds.add(id);
                return;
            }

            const { container, baseSprite, headSprite } = existing;
            this.stopTankTweens(id);

            const currentBaseAngle = Phaser.Math.Angle.WrapDegrees(baseSprite.angle);
            const currentHeadAngle = Phaser.Math.Angle.WrapDegrees(headSprite.angle);
            const adjustedBaseTarget = currentBaseAngle + Phaser.Math.Angle.ShortestBetween(currentBaseAngle, targetBaseAngle);
            const adjustedHeadTarget = currentHeadAngle + Phaser.Math.Angle.ShortestBetween(currentHeadAngle, targetHeadAngle);

            const positionTween = this.scene.tweens.add({
                targets: container,
                x: targetX,
                y: targetY,
                duration: ANIMATION_DURATION_MS,
                ease: 'Linear'
            });

            const baseTween = this.scene.tweens.add({
                targets: baseSprite,
                angle: adjustedBaseTarget,
                duration: ANIMATION_DURATION_MS,
                ease: 'Linear'
            });

            const headTween = this.scene.tweens.add({
                targets: headSprite,
                angle: adjustedHeadTarget,
                duration: ANIMATION_DURATION_MS,
                ease: 'Linear'
            });

            this.tankTweens.set(id, { positionTween, baseTween, headTween });
            seenIds.add(id);
        });

        [...this.tanksById.keys()].forEach((tankId) => {
            if (!seenIds.has(tankId)) {
                const { container } = this.tanksById.get(tankId);
                this.stopTankTweens(tankId);
                container.destroy();
                this.tanksById.delete(tankId);
            }
        });
    }

    createTank(id, x, y, baseAngle, headAngle) {
        const container = this.scene.add.container(0, 0);
        const baseSprite = this.scene.add.image(0, 0, SPRITESHEET_KEY, FRAMES.tankBase).setOrigin(0.5);
        const headSprite = this.scene.add.image(0, 0, SPRITESHEET_KEY, FRAMES.tankHead).setOrigin(0.5);
        container.add([baseSprite, headSprite]);

        container.setPosition(x, y);
        baseSprite.setAngle(baseAngle);
        headSprite.setAngle(headAngle);

        this.tanksById.set(id, { container, baseSprite, headSprite });
    }

    stopTankTweens(tankId) {
        const tweens = this.tankTweens.get(tankId);
        if (!tweens) {
            return;
        }

        ['positionTween', 'baseTween', 'headTween'].forEach((key) => {
            const tween = tweens[key];
            if (tween && tween.isPlaying()) {
                tween.stop();
            }
        });

        this.tankTweens.delete(tankId);
    }
}
