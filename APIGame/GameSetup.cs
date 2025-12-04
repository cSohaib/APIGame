
static class GameSetup
{
    public const int GridColumns = 24;
    public const int GridRows = 16;

    public static Castle[] CreateCastles() => new[]
    {
        new Castle(GridColumns / 2 - 1, 0, "red"),
        new Castle(GridColumns / 2 - 1, GridRows - 2, "blue"),
    };

    public static Rock[] CreateRocks() => new[]
    {
        new Rock(0, 7),
        new Rock(1, 7),
        new Rock(2, 7),
        new Rock(3, 7),
        new Rock(4, 7),
        new Rock(5, 7),
        new Rock(6, 4),
        new Rock(7, 4),
        new Rock(8, 4),
        new Rock(9, 4),
        new Rock(10, 4),
        new Rock(11, 4),
        new Rock(12, 4),
        new Rock(13, 4),
        new Rock(14, 4),
        new Rock(15, 4),
        new Rock(16, 4),
        new Rock(17, 4),
        new Rock(18, 7),
        new Rock(19, 7),
        new Rock(20, 7),
        new Rock(21, 7),
        new Rock(22, 7),
        new Rock(23, 7),
        new Rock(0, 8),
        new Rock(1, 8),
        new Rock(2, 8),
        new Rock(3, 8),
        new Rock(4, 8),
        new Rock(5, 8),
        new Rock(6, 11),
        new Rock(7, 11),
        new Rock(8, 11),
        new Rock(9, 11),
        new Rock(10, 11),
        new Rock(11, 11),
        new Rock(12, 11),
        new Rock(13, 11),
        new Rock(14, 11),
        new Rock(15, 11),
        new Rock(16, 11),
        new Rock(17, 11),
        new Rock(18, 8),
        new Rock(19, 8),
        new Rock(20, 8),
        new Rock(21, 8),
        new Rock(22, 8),
        new Rock(23, 8),
    };

    public static int[,] CreateStaticMap(Castle[] castles, Rock[] rocks)
    {
        var map = new int[GridColumns, GridRows];

        foreach (var rock in rocks)
        {
            if (GameLogic.IsInside(rock.X, rock.Y, map))
            {
                map[rock.X, rock.Y] = 1;
            }
        }

        foreach (var castle in castles)
        {
            for (var dx = 0; dx < 2; dx++)
            {
                for (var dy = 0; dy < 2; dy++)
                {
                    var x = castle.X + dx;
                    var y = castle.Y + dy;

                    if (GameLogic.IsInside(x, y, map))
                    {
                        map[x, y] = 1;
                    }
                }
            }
        }

        return map;
    }
}
