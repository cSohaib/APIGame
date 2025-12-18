package com.example.apigame.model;

import java.util.List;

public class Entities {
    public record Castle(int x, int y, String team) {
        private int hits;

        public int getHits() {
            return hits;
        }

        public void incrementHits() {
            hits++;
        }
    }

    public record Rock(int x, int y) {
    }

    public static class Tank {
        private final String username;
        private final String team;
        private int x;
        private int y;
        private int base;
        private int head;
        private int score;
        private boolean destroyed;
        private boolean destroyedThisTurn;
        private int actionA;
        private int actionB;

        public Tank(String username, String team, int x, int y, int base, int head) {
            this.username = username;
            this.team = team;
            this.x = x;
            this.y = y;
            this.base = base;
            this.head = head;
        }

        public String getUsername() { return username; }
        public String getTeam() { return team; }
        public int getX() { return x; }
        public int getY() { return y; }
        public int getBase() { return base; }
        public int getHead() { return head; }
        public int getScore() { return score; }
        public boolean isDestroyed() { return destroyed; }
        public boolean isDestroyedThisTurn() { return destroyedThisTurn; }
        public int getActionA() { return actionA; }
        public int getActionB() { return actionB; }

        public void setX(int x) { this.x = x; }
        public void setY(int y) { this.y = y; }
        public void setBase(int base) { this.base = base; }
        public void setHead(int head) { this.head = head; }
        public void setScore(int score) { this.score = score; }
        public void setDestroyed(boolean destroyed) { this.destroyed = destroyed; }
        public void setDestroyedThisTurn(boolean destroyedThisTurn) { this.destroyedThisTurn = destroyedThisTurn; }
        public void setActionA(int actionA) { this.actionA = actionA; }
        public void setActionB(int actionB) { this.actionB = actionB; }
    }

    public record TankState(String username, String team, int x, int y, int base, int head, int score, boolean isDestroyed) {
    }

    public record Bullet(long id, String username, String team, int x, int y, int direction) {
    }

    public record Explosion(int x, int y) {
    }

    public record BulletResolution(List<Bullet> activeBullets, List<Bullet> bulletsForSnapshot) {
    }

    public record GameInitialisation(List<Castle> castles, List<Rock> rocks) {
    }

    public record GameSnapshot(TankState[] tanks, Bullet[] bullets, Explosion[] explosions, String[] infoText) {
    }

    public record JoinRequest(String type, String role, String username, String team) {
    }

    public record PlayerAction(String type, int a, int b) {
    }

    public record ServerMessage<T>(String type, T data) {
    }

    public static class MovePlan {
        private final Tank tank;
        private final int targetX;
        private final int targetY;

        public MovePlan(Tank tank, int targetX, int targetY) {
            this.tank = tank;
            this.targetX = targetX;
            this.targetY = targetY;
        }

        public Tank getTank() { return tank; }
        public int getTargetX() { return targetX; }
        public int getTargetY() { return targetY; }
        public boolean willMove() { return tank.getX() != targetX || tank.getY() != targetY; }
    }

    public static class ClientConnection {
        private final org.springframework.web.socket.WebSocketSession session;
        private String role;
        private String username;
        private String team;

        public ClientConnection(org.springframework.web.socket.WebSocketSession session) {
            this.session = session;
        }

        public org.springframework.web.socket.WebSocketSession getSession() { return session; }
        public String getRole() { return role; }
        public String getUsername() { return username; }
        public String getTeam() { return team; }
        public void setRole(String role) { this.role = role; }
        public void setUsername(String username) { this.username = username; }
        public void setTeam(String team) { this.team = team; }
    }
}
