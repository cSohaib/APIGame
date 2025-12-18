package com.example.apigame.websocket;

import com.example.apigame.game.GameRuntime;
import com.example.apigame.model.Entities;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;
import org.springframework.web.socket.TextMessage;
import org.springframework.web.socket.WebSocketSession;

import java.io.IOException;
import java.util.Map;

@Component
public class GameBroadcaster {
    private static final Logger logger = LoggerFactory.getLogger(GameBroadcaster.class);
    private final GameRuntime runtime;
    private final ObjectMapper objectMapper;

    public GameBroadcaster(GameRuntime runtime, ObjectMapper objectMapper) {
        this.runtime = runtime;
        this.objectMapper = objectMapper;
    }

    public void broadcast(Entities.GameSnapshot snapshot) {
        Entities.ServerMessage<Entities.GameSnapshot> payload = new Entities.ServerMessage<>("state", snapshot);
        String serialized;
        try {
            serialized = objectMapper.writeValueAsString(payload);
        } catch (IOException e) {
            logger.error("Failed to serialize snapshot", e);
            return;
        }

        for (Map.Entry<String, Entities.ClientConnection> entry : runtime.getConnections().entrySet()) {
            WebSocketSession session = entry.getValue().getSession();
            if (!session.isOpen()) {
                runtime.getConnections().remove(entry.getKey());
                continue;
            }
            try {
                session.sendMessage(new TextMessage(serialized));
            } catch (IOException e) {
                logger.warn("Failed to send update to {}", entry.getKey(), e);
                runtime.getConnections().remove(entry.getKey());
            }
        }
    }
}
