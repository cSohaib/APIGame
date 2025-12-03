import { ANIMATION_DURATION_MS, FRAMES, SPRITESHEET_KEY, TILE_SIZE } from '../config.js';

export class TankManager {
    constructor(scene) {
        this.scene = scene;
        this.tanksByUsername = new Map();
        this.tankTweens = new Map();
    }

    render(tanks) {
        const seenIds = new Set();

        tanks.forEach(({ username, x, y, base = 0, head = 0 }) => {
            const existing = this.tanksByUsername.get(username);
            const isNew = !existing;
            const targetX = x * TILE_SIZE + TILE_SIZE / 2;
            const targetY = y * TILE_SIZE + TILE_SIZE / 2;
            const targetBaseAngle = base * 90;
            const targetHeadAngle = head * 90;

            if (isNew) {
                this.createTank(username, targetX, targetY, targetBaseAngle, targetHeadAngle);
                seenIds.add(username);
                return;
            }

            const { container, baseSprite, headSprite } = existing;
            this.stopTankTweens(username);

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

            this.tankTweens.set(username, { positionTween, baseTween, headTween });
            seenIds.add(username);
        });

        [...this.tanksByUsername.keys()].forEach((tankUsername) => {
            if (!seenIds.has(tankUsername)) {
                const { container } = this.tanksByUsername.get(tankUsername);
                this.stopTankTweens(tankUsername);
                container.destroy();
                this.tanksByUsername.delete(tankUsername);
            }
        });
    }

    createTank(username, x, y, baseAngle, headAngle) {
        const container = this.scene.add.container(0, 0);
        const baseSprite = this.scene.add.image(0, 0, SPRITESHEET_KEY, FRAMES.tankBase).setOrigin(0.5);
        const headSprite = this.scene.add.image(0, 0, SPRITESHEET_KEY, FRAMES.tankHead).setOrigin(0.5);
        container.add([baseSprite, headSprite]);

        container.setPosition(x, y);
        baseSprite.setAngle(baseAngle);
        headSprite.setAngle(headAngle);

        this.tanksByUsername.set(username, { container, baseSprite, headSprite });
    }

    stopTankTweens(tankUsername) {
        const tweens = this.tankTweens.get(tankUsername);
        if (!tweens) {
            return;
        }

        ['positionTween', 'baseTween', 'headTween'].forEach((key) => {
            const tween = tweens[key];
            if (tween && tween.isPlaying()) {
                tween.stop();
            }
        });

        this.tankTweens.delete(tankUsername);
    }
}
