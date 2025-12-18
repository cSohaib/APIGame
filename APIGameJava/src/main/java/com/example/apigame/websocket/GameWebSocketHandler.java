package com.example.apigame.websocket;

import com.example.apigame.game.GameLogic;
import com.example.apigame.game.GameRuntime;
import com.example.apigame.model.Entities;
import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;
import org.springframework.web.socket.CloseStatus;
import org.springframework.web.socket.TextMessage;
import org.springframework.web.socket.WebSocketSession;
import org.springframework.web.socket.handler.TextWebSocketHandler;

import java.io.IOException;

@Component
public class GameWebSocketHandler extends TextWebSocketHandler {
    private static final Logger logger = LoggerFactory.getLogger(GameWebSocketHandler.class);
    private final GameRuntime runtime;
    private final ObjectMapper objectMapper;

    public GameWebSocketHandler(GameRuntime runtime, ObjectMapper objectMapper) {
        this.runtime = runtime;
        this.objectMapper = objectMapper;
    }

    @Override
    public void afterConnectionEstablished(WebSocketSession session) {
        runtime.getConnections().put(session.getId(), new Entities.ClientConnection(session));
    }

    @Override
    protected void handleTextMessage(WebSocketSession session, TextMessage message) throws Exception {
        Entities.ClientConnection connection = runtime.getConnections().get(session.getId());
        if (connection == null) {
            session.close(CloseStatus.POLICY_VIOLATION);
            return;
        }

        if (connection.getRole() == null) {
            if (!handleJoin(session, connection, message.getPayload())) {
                session.close(CloseStatus.POLICY_VIOLATION);
            }
            return;
        }

        handleAction(connection, message.getPayload());
    }

    @Override
    public void afterConnectionClosed(WebSocketSession session, CloseStatus status) {
        Entities.ClientConnection connection = runtime.getConnections().remove(session.getId());
        if (connection != null) {
            GameLogic.removeTank(connection.getUsername(), runtime.getTanks(), runtime.getGameLock());
        }
    }

    private boolean handleJoin(WebSocketSession session, Entities.ClientConnection connection, String payload) throws IOException {
        Entities.JoinRequest joinRequest;
        try {
            joinRequest = objectMapper.readValue(payload, Entities.JoinRequest.class);
        } catch (JsonProcessingException e) {
            return false;
        }
        if (joinRequest == null || joinRequest.type() == null || !"join".equalsIgnoreCase(joinRequest.type())) {
            return false;
        }

        String role = joinRequest.role() != null ? joinRequest.role().trim().toLowerCase() : null;
        if (!("player".equals(role) || "spectator".equals(role))) {
            return false;
        }

        connection.setRole(role);
        if ("player".equals(role)) {
            if (joinRequest.username() == null || joinRequest.username().isBlank() || joinRequest.team() == null || joinRequest.team().isBlank()) {
                sendError(session, "Username and team are required.");
                return false;
            }
            String username = joinRequest.username().trim();
            String team = joinRequest.team().trim();
            String errorMessage = null;
            synchronized (runtime.getGameLock()) {
                if (runtime.getTanks().containsKey(username)) {
                    errorMessage = "Username already exists.";
                } else {
                    Entities.Tank tank = GameLogic.createTank(username, team, runtime.getStaticMap(), runtime.getTanks().values());
                    if (tank == null) {
                        errorMessage = "No available spawn point.";
                    } else {
                        runtime.getTanks().put(username, tank);
                    }
                }
            }
            if (errorMessage != null) {
                sendError(session, errorMessage);
                return false;
            }
            connection.setUsername(username);
            connection.setTeam(team);
        }

        Entities.GameInitialisation init = new Entities.GameInitialisation(
            java.util.Arrays.asList(runtime.getCastles()),
            java.util.Arrays.asList(runtime.getRocks())
        );
        sendMessage(session, new Entities.ServerMessage<>("initialization", init));
        return true;
    }

    private void handleAction(Entities.ClientConnection connection, String payload) {
        try {
            Entities.PlayerAction action = objectMapper.readValue(payload, Entities.PlayerAction.class);
            if (action != null && "action".equalsIgnoreCase(action.type()) && connection.getUsername() != null) {
                synchronized (runtime.getGameLock()) {
                    Entities.Tank tank = runtime.getTanks().get(connection.getUsername());
                    if (tank != null) {
                        tank.setActionA(action.a());
                        tank.setActionB(action.b());
                    }
                }
            }
        } catch (IOException e) {
            logger.debug("Ignoring malformed action payload", e);
        }
    }

    private void sendMessage(WebSocketSession session, Object payload) throws IOException {
        String json = objectMapper.writeValueAsString(payload);
        session.sendMessage(new TextMessage(json));
    }

    private void sendError(WebSocketSession session, String message) throws IOException {
        sendMessage(session, new Entities.ServerMessage<>("error", message));
    }
}
