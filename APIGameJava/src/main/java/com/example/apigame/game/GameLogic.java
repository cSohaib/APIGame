package com.example.apigame.game;

import com.example.apigame.model.Entities;

import java.util.*;
import java.util.concurrent.ConcurrentMap;
import java.util.stream.Collectors;

public final class GameLogic {
    private GameLogic() {
    }

    public static Entities.GameSnapshot advanceGameState(
        ConcurrentMap<String, Entities.Tank> tanks,
        List<Entities.Bullet> bullets,
        Entities.Castle[] castles,
        int[][] staticMap,
        java.util.function.LongSupplier nextBulletId
    ) {
        List<Entities.Explosion> explosions = new ArrayList<>();
        int[][] map = buildMapWithTanks(staticMap, tanks.values());

        Entities.BulletResolution bulletResolution = resolveBullets(bullets, tanks, castles, staticMap, map, explosions);

        List<Entities.MovePlan> movePlans = new ArrayList<>();
        for (Entities.Tank tank : tanks.values()) {
            if (tank.isDestroyed()) {
                continue;
            }
            applyRotations(tank);
            int targetX = tank.getX();
            int targetY = tank.getY();
            if (tank.getActionA() == 3) {
                int[] delta = directionDelta(tank.getBase());
                int candidateX = tank.getX() + delta[0];
                int candidateY = tank.getY() + delta[1];
                if (isInside(candidateX, candidateY, staticMap) && map[candidateX][candidateY] == 0) {
                    targetX = candidateX;
                    targetY = candidateY;
                }
            }
            movePlans.add(new Entities.MovePlan(tank, targetX, targetY));
        }

        List<Entities.MovePlan> collisions = movePlans.stream()
            .filter(Entities.MovePlan::willMove)
            .collect(Collectors.groupingBy(plan -> plan.getTargetX() + "," + plan.getTargetY()))
            .values().stream()
            .filter(group -> group.size() > 1)
            .flatMap(Collection::stream)
            .toList();

        for (Entities.MovePlan plan : movePlans) {
            if (plan.getTank().isDestroyed() || !plan.willMove()) {
                continue;
            }
            resetMapCell(map, staticMap, plan.getTank().getX(), plan.getTank().getY());
            plan.getTank().setX(plan.getTargetX());
            plan.getTank().setY(plan.getTargetY());
            map[plan.getTank().getX()][plan.getTank().getY()] = 2;
        }

        if (!collisions.isEmpty()) {
            Map<String, List<Entities.MovePlan>> grouped = movePlans.stream()
                .filter(Entities.MovePlan::willMove)
                .collect(Collectors.groupingBy(plan -> plan.getTargetX() + "," + plan.getTargetY()));
            for (Map.Entry<String, List<Entities.MovePlan>> entry : grouped.entrySet()) {
                if (entry.getValue().size() > 1) {
                    String[] coords = entry.getKey().split(",");
                    int cx = Integer.parseInt(coords[0]);
                    int cy = Integer.parseInt(coords[1]);
                    explosions.add(new Entities.Explosion(cx, cy));
                    resetMapCell(map, staticMap, cx, cy);
                    for (Entities.MovePlan plan : entry.getValue()) {
                        plan.getTank().setDestroyed(true);
                        plan.getTank().setDestroyedThisTurn(true);
                    }
                }
            }
        }

        bullets.clear();
        bullets.addAll(bulletResolution.activeBullets());
        List<Entities.Bullet> newBullets = collectNewBullets(tanks.values(), nextBulletId);
        bullets.addAll(newBullets);

        Entities.Bullet[] snapshotBullets = combineBullets(bulletResolution.bulletsForSnapshot(), newBullets);
        clearActions(tanks.values());

        Entities.TankState[] tanksToBroadcast = tanks.values().stream()
            .filter(t -> !t.isDestroyed() || t.isDestroyedThisTurn())
            .map(t -> new Entities.TankState(t.getUsername(), t.getTeam(), t.getX(), t.getY(), t.getBase(), t.getHead(), t.getScore(), t.isDestroyed()))
            .toArray(Entities.TankState[]::new);

        Entities.GameSnapshot snapshot = new Entities.GameSnapshot(
            tanksToBroadcast,
            snapshotBullets,
            explosions.toArray(new Entities.Explosion[0]),
            buildInfoText(tanks.values(), castles)
        );

        tanks.values().stream().filter(Entities.Tank::isDestroyedThisTurn).forEach(t -> t.setDestroyedThisTurn(false));

        return snapshot;
    }

    private static Entities.Bullet[] combineBullets(List<Entities.Bullet> existing, List<Entities.Bullet> newBullets) {
        List<Entities.Bullet> combined = new ArrayList<>(existing);
        combined.addAll(newBullets);
        return combined.toArray(new Entities.Bullet[0]);
    }

    public static int[][] buildMapWithTanks(int[][] baseMap, Collection<Entities.Tank> tanks) {
        int[][] map = new int[baseMap.length][baseMap[0].length];
        for (int x = 0; x < baseMap.length; x++) {
            System.arraycopy(baseMap[x], 0, map[x], 0, baseMap[x].length);
        }
        for (Entities.Tank tank : tanks) {
            if (!tank.isDestroyed() && isInside(tank.getX(), tank.getY(), map)) {
                map[tank.getX()][tank.getY()] = 2;
            }
        }
        return map;
    }

    public static Entities.Tank createTank(String username, String team, int[][] staticMap, Collection<Entities.Tank> existingTanks) {
        int[][] map = buildMapWithTanks(staticMap, existingTanks);
        int y = team.equalsIgnoreCase("red") ? 0 : GameSetup.GRID_ROWS - 1;
        List<Integer> xCandidates = new ArrayList<>();
        for (int i = 0; i < GameSetup.GRID_COLUMNS; i++) {
            xCandidates.add(i);
        }
        Collections.shuffle(xCandidates);
        for (int x : xCandidates) {
            if (map[x][y] == 0) {
                int orientation = team.equalsIgnoreCase("red") ? 3 : 0;
                return new Entities.Tank(username, team, x, y, orientation, orientation);
            }
        }
        return null;
    }

    public static void removeTank(String username, ConcurrentMap<String, Entities.Tank> tanks, Object gameLock) {
        if (username == null) {
            return;
        }
        synchronized (gameLock) {
            tanks.remove(username);
        }
    }

    public static boolean isInside(int x, int y, int[][] map) {
        return x >= 0 && y >= 0 && x < map.length && y < map[0].length;
    }

    private static List<Entities.Bullet> collectNewBullets(Collection<Entities.Tank> tanks, java.util.function.LongSupplier nextBulletId) {
        List<Entities.Bullet> newBullets = new ArrayList<>();
        for (Entities.Tank tank : tanks) {
            if (!tank.isDestroyed() && tank.getActionB() == 3) {
                newBullets.add(new Entities.Bullet(nextBulletId.getAsLong(), tank.getUsername(), tank.getTeam(), tank.getX(), tank.getY(), tank.getHead()));
            }
        }
        return newBullets;
    }

    private static Entities.BulletResolution resolveBullets(
        Collection<Entities.Bullet> bullets,
        ConcurrentMap<String, Entities.Tank> tanks,
        Entities.Castle[] castles,
        int[][] staticMap,
        int[][] map,
        List<Entities.Explosion> explosions
    ) {
        List<Entities.Bullet> remaining = new ArrayList<>();
        List<Entities.Bullet> snapshot = new ArrayList<>();

        for (Entities.Bullet bullet : bullets) {
            Entities.Bullet current = bullet;
            boolean destroyed = false;
            for (int step = 0; step < 4; step++) {
                int[] delta = directionDelta(current.direction());
                int targetX = current.x() + delta[0];
                int targetY = current.y() + delta[1];
                if (!isInside(targetX, targetY, staticMap)) {
                    destroyed = true;
                    break;
                }
                if (map[targetX][targetY] != 0) {
                    explosions.add(new Entities.Explosion(targetX, targetY));
                    handleImpact(targetX, targetY, current, tanks, castles, map, staticMap);
                    current = new Entities.Bullet(current.id(), current.username(), current.team(), targetX, targetY, current.direction());
                    destroyed = true;
                    break;
                }
                current = new Entities.Bullet(current.id(), current.username(), current.team(), targetX, targetY, current.direction());
            }
            snapshot.add(current);
            if (!destroyed) {
                remaining.add(current);
            }
        }
        return new Entities.BulletResolution(remaining, snapshot);
    }

    private static void clearActions(Collection<Entities.Tank> tanks) {
        for (Entities.Tank tank : tanks) {
            tank.setActionA(0);
            tank.setActionB(0);
        }
    }

    private static void resetMapCell(int[][] map, int[][] staticMap, int x, int y) {
        if (isInside(x, y, map)) {
            map[x][y] = staticMap[x][y];
        }
    }

    private static void handleImpact(int x, int y, Entities.Bullet bullet, ConcurrentMap<String, Entities.Tank> tanks, Entities.Castle[] castles, int[][] map, int[][] staticMap) {
        Entities.Castle hitCastle = Arrays.stream(castles)
            .filter(castle -> x >= castle.x() && x < castle.x() + 2 && y >= castle.y() && y < castle.y() + 2)
            .findFirst().orElse(null);
        if (hitCastle != null) {
            if (!hitCastle.team().equalsIgnoreCase(bullet.team())) {
                hitCastle.incrementHits();
                awardScore(bullet.username(), tanks);
            }
            return;
        }
        Entities.Tank hitTank = tanks.values().stream()
            .filter(tank -> !tank.isDestroyed() && tank.getX() == x && tank.getY() == y)
            .findFirst().orElse(null);
        if (hitTank != null && !hitTank.getTeam().equalsIgnoreCase(bullet.team())) {
            hitTank.setDestroyed(true);
            hitTank.setDestroyedThisTurn(true);
            awardScore(bullet.username(), tanks);
            if (isInside(hitTank.getX(), hitTank.getY(), map)) {
                map[hitTank.getX()][hitTank.getY()] = staticMap[hitTank.getX()][hitTank.getY()];
            }
        }
    }

    private static void awardScore(String username, ConcurrentMap<String, Entities.Tank> tanks) {
        Entities.Tank shooter = tanks.get(username);
        if (shooter != null) {
            shooter.setScore(shooter.getScore() + 1);
        }
    }

    private static String[] buildInfoText(Collection<Entities.Tank> tanks, Entities.Castle[] castles) {
        List<String> info = new ArrayList<>();
        for (Entities.Castle castle : castles) {
            info.add("Castle " + castle.team() + ": Hits " + castle.getHits());
        }
        info.add("Scores:");
        tanks.stream()
            .sorted(Comparator.comparingInt(Entities.Tank::getScore).reversed())
            .forEach(tank -> {
                String status = tank.isDestroyed() ? " (destroyed)" : "";
                info.add(tank.getUsername() + " [" + tank.getTeam() + "] - " + tank.getScore() + status);
            });
        return info.toArray(new String[0]);
    }

    private static void applyRotations(Entities.Tank tank) {
        tank.setBase(normalizeDirection(tank.getBase() + (tank.getActionA() == 1 ? 1 : tank.getActionA() == 2 ? -1 : 0)));
        tank.setHead(normalizeDirection(tank.getHead() + (tank.getActionB() == 1 ? 1 : tank.getActionB() == 2 ? -1 : 0)));
    }

    private static int normalizeDirection(int value) {
        int normalized = value % 4;
        return normalized < 0 ? normalized + 4 : normalized;
    }

    private static int[] directionDelta(int direction) {
        return switch (direction) {
            case 0 -> new int[]{0, -1};
            case 1 -> new int[]{1, 0};
            case 2 -> new int[]{0, 1};
            default -> new int[]{-1, 0};
        };
    }
}
