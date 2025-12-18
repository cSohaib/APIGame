package com.example.apigame.game;

import com.example.apigame.model.Entities;
import com.example.apigame.websocket.GameBroadcaster;
import org.springframework.stereotype.Component;

import jakarta.annotation.PostConstruct;
import jakarta.annotation.PreDestroy;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;

@Component
public class GameLoop {
    private final GameRuntime runtime;
    private final GameBroadcaster broadcaster;
    private final ScheduledExecutorService executor = Executors.newSingleThreadScheduledExecutor();
    private ScheduledFuture<?> loopHandle;

    public GameLoop(GameRuntime runtime, GameBroadcaster broadcaster) {
        this.runtime = runtime;
        this.broadcaster = broadcaster;
    }

    @PostConstruct
    public void start() {
        loopHandle = executor.scheduleAtFixedRate(this::tick, 0, 500, TimeUnit.MILLISECONDS);
    }

    @PreDestroy
    public void stop() {
        if (loopHandle != null) {
            loopHandle.cancel(true);
        }
        executor.shutdownNow();
    }

    private void tick() {
        Entities.GameSnapshot snapshot;
        synchronized (runtime.getGameLock()) {
            snapshot = GameLogic.advanceGameState(
                runtime.getTanks(),
                runtime.getBullets(),
                runtime.getCastles(),
                runtime.getStaticMap(),
                runtime::nextBulletId
            );
        }
        broadcaster.broadcast(snapshot);
    }
}
