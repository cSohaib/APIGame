import { GRID_COLUMNS, PANEL_STYLE, TILE_SIZE } from '../config.js';

export class InfoPanel {
    constructor(scene) {
        this.scene = scene;
        this.panelX = GRID_COLUMNS * TILE_SIZE;
        this.panelWidth = this.scene.scale.width - this.panelX;
        this.panelHeight = this.scene.scale.height;
        this.textBlock = null;
    }

    create(defaultText = []) {
        this.scene.add.rectangle(this.panelX, 0, this.panelWidth, this.panelHeight, 0x000000, 0.25).setOrigin(0);

        this.textBlock = this.scene.add.text(this.panelX + 16, 16, '', PANEL_STYLE).setDepth(1);

        if (defaultText.length) {
            this.update(defaultText);
        }
    }

    update(lines = []) {
        if (this.textBlock) {
            this.textBlock.setText(lines.join('\n'));
        }
    }
}
