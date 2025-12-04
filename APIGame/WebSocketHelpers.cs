using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

static class WebSocketHelpers
{
    public static async Task<bool> ReceiveJoinAsync(WebSocket socket, ClientConnection connection, GameRuntime runtime, CancellationToken cancellationToken)
    {
        var message = await ReceiveTextMessageAsync(socket, cancellationToken);
        if (message is null)
        {
            return false;
        }

        JoinRequest? joinRequest;
        try
        {
            joinRequest = JsonSerializer.Deserialize<JoinRequest>(message, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            return false;
        }

        if (joinRequest is null || !string.Equals(joinRequest.Type, "join", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var role = joinRequest.Role?.Trim().ToLowerInvariant();
        if (role is not ("player" or "spectator"))
        {
            return false;
        }

        connection.Role = role;

        if (role == "player")
        {
            if (string.IsNullOrWhiteSpace(joinRequest.Username) || string.IsNullOrWhiteSpace(joinRequest.Team))
            {
                return false;
            }

            var username = joinRequest.Username.Trim();
            var team = joinRequest.Team.Trim();
            var errorMessage = string.Empty;

            lock (runtime.GameLock)
            {
                if (runtime.Tanks.ContainsKey(username))
                {
                    errorMessage = "Username already exists.";
                }
                else
                {
                    var tank = GameLogic.CreateTank(username, team, runtime.StaticMap, runtime.Tanks.Values);
                    if (tank is null)
                    {
                        errorMessage = "No available spawn point.";
                    }
                    else
                    {
                        runtime.Tanks[username] = tank;
                    }
                }
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                await SendJsonAsync(socket, new ServerMessage<string>("error", errorMessage), cancellationToken);
                return false;
            }

            connection.Username = username;
            connection.Team = team;
        }

        return true;
    }

    public static async Task ReceiveActionsAsync(Guid connectionId, ClientConnection connection, GameRuntime runtime, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && connection.Socket.State == WebSocketState.Open)
        {
            var message = await ReceiveTextMessageAsync(connection.Socket, cancellationToken);
            if (message is null)
            {
                break;
            }

            if (!string.Equals(connection.Role, "player", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var action = JsonSerializer.Deserialize<PlayerAction>(message, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (action is not null && string.Equals(action.Type, "action", StringComparison.OrdinalIgnoreCase) &&
                    connection.Username is { } username)
                {
                    lock (runtime.GameLock)
                    {
                        if (runtime.Tanks.TryGetValue(username, out var tank))
                        {
                            tank.ActionA = action.A;
                            tank.ActionB = action.B;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // ignore malformed action
            }
        }

        if (runtime.Connections.TryGetValue(connectionId, out var trackedConnection) && trackedConnection.Socket.State == WebSocketState.Open)
        {
            await trackedConnection.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", cancellationToken);
        }
    }

    public static async Task SendJsonAsync<T>(WebSocket socket, T payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);
        var buffer = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken);
    }

    static async Task<string?> ReceiveTextMessageAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var builder = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            builder.Append(Encoding.UTF8.GetString(buffer.AsSpan(0, result.Count)));

            if (result.EndOfMessage)
            {
                return builder.ToString();
            }
        }

        return null;
    }
}
