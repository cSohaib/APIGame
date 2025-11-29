using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();

var castles = new[]
{
    new Castle(11, 0, "red"),
    new Castle(11, 14, "blue")
};

var connections = new ConcurrentDictionary<Guid, ClientConnection>();
var players = new ConcurrentDictionary<string, PlayerState>(StringComparer.OrdinalIgnoreCase);
var broadcasterCts = new CancellationTokenSource();

_ = Task.Run(() => BroadcastPlayersAsync(connections, players, broadcasterCts.Token));

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var connectionId = Guid.NewGuid();
    var connection = new ClientConnection(socket);

    connections[connectionId] = connection;

    try
    {
        if (!await ReceiveJoinAsync(socket, connection, players, context.RequestAborted))
        {
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Invalid join request", context.RequestAborted);
            return;
        }

        await SendJsonAsync(socket, new ServerMessage<Castle[]>("castles", castles), context.RequestAborted);

        await ReceiveActionsAsync(connectionId, connection, connections, players, context.RequestAborted);
    }
    finally
    {
        connections.TryRemove(connectionId, out _);
        if (connection.Username is { } username)
        {
            players.TryRemove(username, out _);
        }
    }
});

app.Lifetime.ApplicationStopping.Register(() => broadcasterCts.Cancel());

app.Run();

static async Task<bool> ReceiveJoinAsync(WebSocket socket, ClientConnection connection, ConcurrentDictionary<string, PlayerState> players, CancellationToken cancellationToken)
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

        var state = new PlayerState(username, team, Random.Shared.Next(0, 21), Random.Shared.Next(0, 21));
        if (!players.TryAdd(username, state))
        {
            await SendJsonAsync(socket, new ServerMessage<string>("error", "Username already exists."), cancellationToken);
            return false;
        }

        connection.Username = username;
        connection.Team = team;
    }

    return true;
}

static async Task ReceiveActionsAsync(Guid connectionId, ClientConnection connection, ConcurrentDictionary<Guid, ClientConnection> connections, ConcurrentDictionary<string, PlayerState> players, CancellationToken cancellationToken)
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
                players.AddOrUpdate(username, _ => new PlayerState(username, connection.Team ?? string.Empty, action.X, action.Y),
                    (_, existing) => existing with { X = action.X, Y = action.Y });
            }
        }
        catch (JsonException)
        {
            // ignore malformed action
        }
    }

    if (connections.TryGetValue(connectionId, out var trackedConnection) && trackedConnection.Socket.State == WebSocketState.Open)
    {
        await trackedConnection.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", cancellationToken);
    }
}

static async Task BroadcastPlayersAsync(ConcurrentDictionary<Guid, ClientConnection> connections, ConcurrentDictionary<string, PlayerState> players, CancellationToken cancellationToken)
{
    var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

    try
    {
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var snapshot = players.Values.ToArray();
            var payload = new ServerMessage<PlayerState[]>("players", snapshot);

            foreach (var (id, connection) in connections)
            {
                if (connection.Socket.State != WebSocketState.Open)
                {
                    connections.TryRemove(id, out _);
                    continue;
                }

                try
                {
                    await SendJsonAsync(connection.Socket, payload, cancellationToken);
                }
                catch
                {
                    connections.TryRemove(id, out _);
                }
            }
        }
    }
    catch (OperationCanceledException)
    {
    }
    finally
    {
        timer.Dispose();
    }
}

static async Task SendJsonAsync<T>(WebSocket socket, T payload, CancellationToken cancellationToken)
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

record Castle(int X, int Y, string Team);
record PlayerState(string Username, string Team, int X, int Y);
record JoinRequest(string Type, string Role, string? Username, string? Team);
record PlayerAction(string Type, int X, int Y);
record ServerMessage<T>(string Type, T Data);

class ClientConnection
{
    public ClientConnection(WebSocket socket)
    {
        Socket = socket;
    }

    public WebSocket Socket { get; }
    public string? Role { get; set; }
    public string? Username { get; set; }
    public string? Team { get; set; }
}
