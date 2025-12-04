using System.Collections.Concurrent;
using System.Linq;

static class GameLogic
{
    public static GameSnapshot AdvanceGameState(
        ConcurrentDictionary<string, Tank> tanks,
        List<Bullet> bullets,
        Castle[] castles,
        int[,] staticMap,
        Func<long> nextBulletId)
    {
        var explosions = new List<Explosion>();
        var map = BuildMapWithTanks(staticMap, tanks.Values);

        var bulletResolution = ResolveBullets(bullets, tanks, castles, staticMap, map, explosions);

        var movePlans = new List<MovePlan>();

        foreach (var tank in tanks.Values)
        {
            if (tank.IsDestroyed)
            {
                continue;
            }

            ApplyRotations(tank);

            var targetX = tank.X;
            var targetY = tank.Y;

            if (tank.ActionA == 3)
            {
                var (dx, dy) = DirectionDelta(tank.Base);
                var candidateX = tank.X + dx;
                var candidateY = tank.Y + dy;

                if (IsInside(candidateX, candidateY, staticMap) && map[candidateX, candidateY] == 0)
                {
                    targetX = candidateX;
                    targetY = candidateY;
                }
            }

            movePlans.Add(new MovePlan(tank, targetX, targetY));
        }

        var collisions = movePlans.Where(plan => plan.WillMove)
            .GroupBy(plan => (plan.TargetX, plan.TargetY))
            .Where(group => group.Count() > 1).ToList();

        foreach (var plan in movePlans.Where(plan => !plan.Tank.IsDestroyed && plan.WillMove))
        {
            ResetMapCell(map, staticMap, plan.Tank.X, plan.Tank.Y);
            plan.Tank.X = plan.TargetX;
            plan.Tank.Y = plan.TargetY;
            map[plan.Tank.X, plan.Tank.Y] = 2;
        }

        foreach (var group in collisions)
        {
            explosions.Add(new Explosion(group.Key.TargetX, group.Key.TargetY));
            ResetMapCell(map, staticMap, group.Key.TargetX, group.Key.TargetY);

            foreach (var plan in group)
            {
                plan.Tank.IsDestroyed = true;
                plan.Tank.DestroyedThisTurn = true;
            }
        }

        bullets.Clear();
        bullets.AddRange(bulletResolution.ActiveBullets);

        var newBullets = CollectNewBullets(tanks.Values, nextBulletId).ToList();
        bullets.AddRange(newBullets);

        var snapshotBullets = bulletResolution.BulletsForSnapshot
            .Concat(newBullets)
            .ToArray();

        ClearActions(tanks.Values);

        var tanksToBroadcast = tanks.Values
            .Where(tank => !tank.IsDestroyed || tank.DestroyedThisTurn)
            .Select(tank => new TankState(tank.Username, tank.Team, tank.X, tank.Y, tank.Base, tank.Head, tank.Score, tank.IsDestroyed))
            .ToArray();

        var snapshot = new GameSnapshot(
            tanksToBroadcast,
            snapshotBullets,
            explosions.ToArray(),
            BuildInfoText(tanks.Values, castles)
        );

        foreach (var tank in tanks.Values.Where(t => t.DestroyedThisTurn))
        {
            tank.DestroyedThisTurn = false;
        }

        return snapshot;
    }

    public static int[,] BuildMapWithTanks(int[,] baseMap, IEnumerable<Tank> tanks)
    {
        var map = new int[baseMap.GetLength(0), baseMap.GetLength(1)];
        Array.Copy(baseMap, map, baseMap.Length);

        foreach (var tank in tanks.Where(t => !t.IsDestroyed))
        {
            if (IsInside(tank.X, tank.Y, map))
            {
                map[tank.X, tank.Y] = 2;
            }
        }

        return map;
    }

    public static Tank? CreateTank(string username, string team, int[,] staticMap, IEnumerable<Tank> existingTanks)
    {
        var map = BuildMapWithTanks(staticMap, existingTanks);
        var y = string.Equals(team, "red", StringComparison.OrdinalIgnoreCase) ? 0 : GameSetup.GridRows - 1;
        var xCandidates = Enumerable.Range(0, GameSetup.GridColumns).OrderBy(_ => Random.Shared.Next()).ToList();

        foreach (var x in xCandidates)
        {
            if (map[x, y] == 0)
            {
                var orientation = string.Equals(team, "red", StringComparison.OrdinalIgnoreCase) ? 3 : 0;
                return new Tank(username, team, x, y, orientation, orientation);
            }
        }

        return null;
    }

    public static void RemoveTank(string? username, ConcurrentDictionary<string, Tank> tanks, object gameLock)
    {
        if (username is null)
        {
            return;
        }

        lock (gameLock)
        {
            tanks.TryRemove(username, out _);
        }
    }

    public static bool IsInside(int x, int y, int[,] map) => x >= 0 && y >= 0 && x < map.GetLength(0) && y < map.GetLength(1);

    static IEnumerable<Bullet> CollectNewBullets(IEnumerable<Tank> tanks, Func<long> nextBulletId)
    {
        var newBullets = new List<Bullet>();

        foreach (var tank in tanks)
        {
            if (!tank.IsDestroyed && tank.ActionB == 3)
            {
                newBullets.Add(new Bullet(nextBulletId(), tank.Username, tank.Team, tank.X, tank.Y, tank.Head));
            }
        }

        return newBullets;
    }

    static BulletResolution ResolveBullets(
        IEnumerable<Bullet> bullets,
        ConcurrentDictionary<string, Tank> tanks,
        Castle[] castles,
        int[,] staticMap,
        int[,] map,
        List<Explosion> explosions)
    {
        var remainingBullets = new List<Bullet>();
        var snapshotBullets = new List<Bullet>();

        foreach (var bullet in bullets)
        {
            var currentBullet = bullet;
            var destroyed = false;

            for (var step = 0; step < 4; step++)
            {
                var (dx, dy) = DirectionDelta(currentBullet.Direction);
                var targetX = currentBullet.X + dx;
                var targetY = currentBullet.Y + dy;

                if (!IsInside(targetX, targetY, staticMap))
                {
                    destroyed = true;
                    break;
                }

                if (map[targetX, targetY] != 0)
                {
                    explosions.Add(new Explosion(targetX, targetY));
                    HandleImpact(targetX, targetY, currentBullet, tanks, castles, map, staticMap);
                    currentBullet = currentBullet with { X = targetX, Y = targetY };
                    destroyed = true;
                    break;
                }

                currentBullet = currentBullet with { X = targetX, Y = targetY };
            }

            snapshotBullets.Add(currentBullet);

            if (!destroyed)
            {
                remainingBullets.Add(currentBullet);
            }
        }

        return new BulletResolution(remainingBullets, snapshotBullets);
    }

    static void ClearActions(IEnumerable<Tank> tanks)
    {
        foreach (var tank in tanks)
        {
            tank.ActionA = 0;
            tank.ActionB = 0;
        }
    }

    static void ResetMapCell(int[,] map, int[,] staticMap, int x, int y)
    {
        if (IsInside(x, y, map))
        {
            map[x, y] = staticMap[x, y];
        }
    }

    static void HandleImpact(int x, int y, Bullet bullet, ConcurrentDictionary<string, Tank> tanks, Castle[] castles, int[,] map, int[,] staticMap)
    {
        var hitCastle = castles.FirstOrDefault(castle => x >= castle.X && x < castle.X + 2 && y >= castle.Y && y < castle.Y + 2);
        if (hitCastle is not null)
        {
            if (!string.Equals(hitCastle.Team, bullet.Team, StringComparison.OrdinalIgnoreCase))
            {
                hitCastle.Hits++;
                AwardScore(bullet.Username, tanks);
            }
            return;
        }

        var hitTank = tanks.Values.FirstOrDefault(tank => !tank.IsDestroyed && tank.X == x && tank.Y == y);
        if (hitTank is not null)
        {
            if (!string.Equals(hitTank.Team, bullet.Team, StringComparison.OrdinalIgnoreCase))
            {
                hitTank.IsDestroyed = true;
                hitTank.DestroyedThisTurn = true;
                AwardScore(bullet.Username, tanks);
                if (IsInside(hitTank.X, hitTank.Y, map))
                {
                    map[hitTank.X, hitTank.Y] = staticMap[hitTank.X, hitTank.Y];
                }
            }
        }
    }

    static void AwardScore(string username, ConcurrentDictionary<string, Tank> tanks)
    {
        if (tanks.TryGetValue(username, out var shooter))
        {
            shooter.Score++;
        }
    }

    static string[] BuildInfoText(IEnumerable<Tank> tanks, IEnumerable<Castle> castles)
    {
        var info = new List<string>();

        foreach (var castle in castles)
        {
            info.Add($"Castle {castle.Team}: Hits {castle.Hits}");
        }

        info.Add("Scores:");

        foreach (var tank in tanks.OrderByDescending(t => t.Score))
        {
            var status = tank.IsDestroyed ? " (destroyed)" : string.Empty;
            info.Add($"{tank.Username} [{tank.Team}] - {tank.Score}{status}");
        }

        return info.ToArray();
    }

    static void ApplyRotations(Tank tank)
    {
        tank.Base = NormalizeDirection(tank.Base + (tank.ActionA == 1 ? 1 : tank.ActionA == 2 ? -1 : 0));
        tank.Head = NormalizeDirection(tank.Head + (tank.ActionB == 1 ? 1 : tank.ActionB == 2 ? -1 : 0));
    }

    static int NormalizeDirection(int value)
    {
        var normalized = value % 4;
        return normalized < 0 ? normalized + 4 : normalized;
    }

    static (int dx, int dy) DirectionDelta(int direction) => direction switch
    {
        0 => (0, -1),
        1 => (1, 0),
        2 => (0, 1),
        _ => (-1, 0)
    };
}
