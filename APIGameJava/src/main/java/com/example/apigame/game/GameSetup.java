package com.example.apigame.game;

import com.example.apigame.model.Entities;

public final class GameSetup {
    private GameSetup() {
    }

    public static final int GRID_COLUMNS = 24;
    public static final int GRID_ROWS = 16;

    public static Entities.Castle[] createCastles() {
        return new Entities.Castle[]{
            new Entities.Castle(GRID_COLUMNS / 2 - 1, 0, "red"),
            new Entities.Castle(GRID_COLUMNS / 2 - 1, GRID_ROWS - 2, "blue")
        };
    }

    public static Entities.Rock[] createRocks() {
        return new Entities.Rock[]{
            new Entities.Rock(0, 7),
            new Entities.Rock(1, 7),
            new Entities.Rock(2, 7),
            new Entities.Rock(3, 7),
            new Entities.Rock(4, 7),
            new Entities.Rock(5, 7),
            new Entities.Rock(6, 4),
            new Entities.Rock(7, 4),
            new Entities.Rock(8, 4),
            new Entities.Rock(9, 4),
            new Entities.Rock(10, 4),
            new Entities.Rock(11, 4),
            new Entities.Rock(12, 4),
            new Entities.Rock(13, 4),
            new Entities.Rock(14, 4),
            new Entities.Rock(15, 4),
            new Entities.Rock(16, 4),
            new Entities.Rock(17, 4),
            new Entities.Rock(18, 7),
            new Entities.Rock(19, 7),
            new Entities.Rock(20, 7),
            new Entities.Rock(21, 7),
            new Entities.Rock(22, 7),
            new Entities.Rock(23, 7),
            new Entities.Rock(0, 8),
            new Entities.Rock(1, 8),
            new Entities.Rock(2, 8),
            new Entities.Rock(3, 8),
            new Entities.Rock(4, 8),
            new Entities.Rock(5, 8),
            new Entities.Rock(6, 11),
            new Entities.Rock(7, 11),
            new Entities.Rock(8, 11),
            new Entities.Rock(9, 11),
            new Entities.Rock(10, 11),
            new Entities.Rock(11, 11),
            new Entities.Rock(12, 11),
            new Entities.Rock(13, 11),
            new Entities.Rock(14, 11),
            new Entities.Rock(15, 11),
            new Entities.Rock(16, 11),
            new Entities.Rock(17, 11),
            new Entities.Rock(18, 8),
            new Entities.Rock(19, 8),
            new Entities.Rock(20, 8),
            new Entities.Rock(21, 8),
            new Entities.Rock(22, 8),
            new Entities.Rock(23, 8)
        };
    }

    public static int[][] createStaticMap(Entities.Castle[] castles, Entities.Rock[] rocks) {
        int[][] map = new int[GRID_COLUMNS][GRID_ROWS];

        for (Entities.Rock rock : rocks) {
            if (GameLogic.isInside(rock.x(), rock.y(), map)) {
                map[rock.x()][rock.y()] = 1;
            }
        }

        for (Entities.Castle castle : castles) {
            for (int dx = 0; dx < 2; dx++) {
                for (int dy = 0; dy < 2; dy++) {
                    int x = castle.x() + dx;
                    int y = castle.y() + dy;
                    if (GameLogic.isInside(x, y, map)) {
                        map[x][y] = 1;
                    }
                }
            }
        }

        return map;
    }
}
