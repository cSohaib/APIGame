package com.example.apigame.game;

import com.example.apigame.model.Entities;
import org.springframework.stereotype.Component;

import java.util.List;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicLong;

@Component
public class GameRuntime {
    private final Entities.Castle[] castles;
    private final Entities.Rock[] rocks;
    private final int[][] staticMap;
    private final Map<String, Entities.Tank> tanks = new ConcurrentHashMap<>();
    private final Map<String, Entities.ClientConnection> connections = new ConcurrentHashMap<>();
    private final List<Entities.Bullet> bullets = new java.util.concurrent.CopyOnWriteArrayList<>();
    private final Object gameLock = new Object();
    private final AtomicLong bulletIdCounter = new AtomicLong();

    public GameRuntime() {
        this.castles = GameSetup.createCastles();
        this.rocks = GameSetup.createRocks();
        this.staticMap = GameSetup.createStaticMap(castles, rocks);
    }

    public Entities.Castle[] getCastles() { return castles; }
    public Entities.Rock[] getRocks() { return rocks; }
    public int[][] getStaticMap() { return staticMap; }
    public Map<String, Entities.Tank> getTanks() { return tanks; }
    public Map<String, Entities.ClientConnection> getConnections() { return connections; }
    public List<Entities.Bullet> getBullets() { return bullets; }
    public Object getGameLock() { return gameLock; }

    public long nextBulletId() {
        return bulletIdCounter.incrementAndGet();
    }
}
