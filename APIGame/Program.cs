using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Linq;

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

const int GridColumns = 24;
const int GridRows = 16;

var castles = new[]
{
    new Castle(11, 0, "red"),
    new Castle(11, 14, "blue"),
};

var rocks = new[]
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

var staticMap = CreateMap(GridColumns, GridRows, castles, rocks);

var connections = new ConcurrentDictionary<Guid, ClientConnection>();
var tanks = new ConcurrentDictionary<string, Tank>(StringComparer.OrdinalIgnoreCase);
var bullets = new List<Bullet>();
var gameLock = new object();
var broadcasterCts = new CancellationTokenSource();
long bulletIdCounter = 0;

_ = Task.Run(() => RunGameLoopAsync(connections, tanks, bullets, castles, staticMap, gameLock, () => Interlocked.Increment(ref bulletIdCounter), broadcasterCts.Token));

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
        if (!await ReceiveJoinAsync(socket, connection, tanks, gameLock, staticMap, context.RequestAborted))
        {
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Invalid join request", context.RequestAborted);
            return;
        }

        await SendJsonAsync(socket, new ServerMessage<GameInitialisation>("initialization", new GameInitialisation(castles, rocks)), context.RequestAborted);

        await ReceiveActionsAsync(connectionId, connection, connections, tanks, gameLock, context.RequestAborted);
    }
    finally
    {
        connections.TryRemove(connectionId, out _);
        RemoveTank(connection.Username, tanks, gameLock);
    }
});

app.Lifetime.ApplicationStopping.Register(() => broadcasterCts.Cancel());

app.Run();

static async Task<bool> ReceiveJoinAsync(WebSocket socket, ClientConnection connection, ConcurrentDictionary<string, Tank> tanks, object gameLock, int[,] staticMap, CancellationToken cancellationToken)
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

        lock (gameLock)
        {
            if (tanks.ContainsKey(username))
            {
                await SendJsonAsync(socket, new ServerMessage<string>("error", "Username already exists."), cancellationToken);
                return false;
            }

            var tank = CreateTank(username, team, staticMap, tanks.Values);
            if (tank is null)
            {
                await SendJsonAsync(socket, new ServerMessage<string>("error", "No available spawn point."), cancellationToken);
                return false;
            }

            tanks[username] = tank;
        }

        connection.Username = username;
        connection.Team = team;
    }

    return true;
}

static async Task ReceiveActionsAsync(Guid connectionId, ClientConnection connection, ConcurrentDictionary<Guid, ClientConnection> connections, ConcurrentDictionary<string, Tank> tanks, object gameLock, CancellationToken cancellationToken)
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
                lock (gameLock)
                {
                    if (tanks.TryGetValue(username, out var tank))
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

    if (connections.TryGetValue(connectionId, out var trackedConnection) && trackedConnection.Socket.State == WebSocketState.Open)
    {
        await trackedConnection.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", cancellationToken);
    }
}

static async Task RunGameLoopAsync(ConcurrentDictionary<Guid, ClientConnection> connections, ConcurrentDictionary<string, Tank> tanks, List<Bullet> bullets, Castle[] castles, int[,] staticMap, object gameLock, Func<long> nextBulletId, CancellationToken cancellationToken)
{
    var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

    try
    {
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            GameSnapshot snapshot;
            lock (gameLock)
            {
                snapshot = AdvanceGameState(tanks, bullets, castles, staticMap, nextBulletId);
            }

            var payload = new ServerMessage<GameSnapshot>("state", snapshot);

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

static GameSnapshot AdvanceGameState(ConcurrentDictionary<string, Tank> tanks, List<Bullet> bullets, Castle[] castles, int[,] staticMap, Func<long> nextBulletId)
{
    var explosions = new List<Explosion>();
    var map = BuildMapWithTanks(staticMap, tanks.Values);
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
        .Where(group => group.Count() > 1);

    foreach (var group in collisions)
    {
        explosions.Add(new Explosion(group.Key.Item1, group.Key.Item2));

        foreach (var plan in group)
        {
            map[plan.Tank.X, plan.Tank.Y] = staticMap[plan.Tank.X, plan.Tank.Y];
            map[plan.TargetX, plan.TargetY] = staticMap[plan.TargetX, plan.TargetY];
            plan.Tank.IsDestroyed = true;
            plan.Tank.DestroyedThisTurn = true;
        }
    }

    foreach (var plan in movePlans.Where(plan => !plan.Tank.IsDestroyed && plan.WillMove))
    {
        map[plan.Tank.X, plan.Tank.Y] = staticMap[plan.Tank.X, plan.Tank.Y];
        plan.Tank.X = plan.TargetX;
        plan.Tank.Y = plan.TargetY;
        map[plan.Tank.X, plan.Tank.Y] = 2;
    }

    var newBullets = new List<Bullet>();

    foreach (var tank in tanks.Values)
    {
        if (!tank.IsDestroyed && tank.ActionB == 3)
        {
            newBullets.Add(new Bullet(nextBulletId(), tank.Username, tank.Team, tank.X, tank.Y, tank.Head));
        }
    }

    bullets.AddRange(newBullets);

    var remainingBullets = new List<Bullet>();

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
                destroyed = true;
                break;
            }

            currentBullet = currentBullet with { X = targetX, Y = targetY };
        }

        if (!destroyed)
        {
            remainingBullets.Add(currentBullet);
        }
    }

    bullets.Clear();
    bullets.AddRange(remainingBullets);

    foreach (var tank in tanks.Values)
    {
        tank.ActionA = 0;
        tank.ActionB = 0;
    }

    var tanksToBroadcast = tanks.Values
        .Where(tank => !tank.IsDestroyed || tank.DestroyedThisTurn)
        .Select(tank => new TankState(tank.Username, tank.Team, tank.X, tank.Y, tank.Base, tank.Head, tank.Score, tank.IsDestroyed))
        .ToArray();

    var snapshot = new GameSnapshot(
        tanksToBroadcast,
        bullets.ToArray(),
        explosions.ToArray(),
        BuildInfoText(tanks.Values, castles)
    );

    foreach (var tank in tanks.Values.Where(t => t.DestroyedThisTurn))
    {
        tank.DestroyedThisTurn = false;
    }

    return snapshot;
}

static void HandleImpact(int x, int y, Bullet bullet, ConcurrentDictionary<string, Tank> tanks, Castle[] castles, int[,] map, int[,] staticMap)
{
    var hitCastle = castles.FirstOrDefault(castle => x >= castle.X && x < castle.X + 2 && y >= castle.Y && y < castle.Y + 2);
    if (hitCastle is not null)
    {
        hitCastle.Hits++;
        AwardScore(bullet.Username, tanks);
        return;
    }

    var hitTank = tanks.Values.FirstOrDefault(tank => !tank.IsDestroyed && tank.X == x && tank.Y == y);
    if (hitTank is not null)
    {
        hitTank.IsDestroyed = true;
        hitTank.DestroyedThisTurn = true;
        AwardScore(bullet.Username, tanks);
        if (IsInside(hitTank.X, hitTank.Y, map))
        {
            map[hitTank.X, hitTank.Y] = staticMap[hitTank.X, hitTank.Y];
        }
        return;
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

static bool IsInside(int x, int y, int[,] map) => x >= 0 && y >= 0 && x < map.GetLength(0) && y < map.GetLength(1);

static int[,] CreateMap(int columns, int rows, IEnumerable<Castle> castles, IEnumerable<Rock> rocks)
{
    var map = new int[columns, rows];

    foreach (var rock in rocks)
    {
        if (IsInside(rock.X, rock.Y, map))
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
                if (IsInside(x, y, map))
                {
                    map[x, y] = 1;
                }
            }
        }
    }

    return map;
}

static int[,] BuildMapWithTanks(int[,] staticMap, IEnumerable<Tank> tanks)
{
    var map = (int[,])staticMap.Clone();

    foreach (var tank in tanks)
    {
        if (tank.IsDestroyed)
        {
            continue;
        }

        if (IsInside(tank.X, tank.Y, map))
        {
            map[tank.X, tank.Y] = 2;
        }
    }

    return map;
}

static Tank? CreateTank(string username, string team, int[,] staticMap, IEnumerable<Tank> existingTanks)
{
    var map = BuildMapWithTanks(staticMap, existingTanks);
    var y = string.Equals(team, "red", StringComparison.OrdinalIgnoreCase) ? 0 : GridRows - 1;
    var xCandidates = Enumerable.Range(0, GridColumns).OrderBy(_ => Random.Shared.Next()).ToList();

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

static void RemoveTank(string? username, ConcurrentDictionary<string, Tank> tanks, object gameLock)
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

record Castle(int X, int Y, string Team)
{
    public int Hits { get; set; }
}

record Rock(int X, int Y);

record Tank(string Username, string Team, int X, int Y, int Base, int Head)
{
    public int Score { get; set; }
    public bool IsDestroyed { get; set; }
    public bool DestroyedThisTurn { get; set; }
    public int ActionA { get; set; }
    public int ActionB { get; set; }
}

record TankState(string Username, string Team, int X, int Y, int Base, int Head, int Score, bool IsDestroyed);
record Bullet(long Id, string Username, string Team, int X, int Y, int Direction);
record Explosion(int X, int Y);
record GameInitialisation(IEnumerable<Castle> Castles, IEnumerable<Rock> Rocks);
record GameSnapshot(TankState[] Tanks, Bullet[] Bullets, Explosion[] Explosions, string[] InfoText);
record JoinRequest(string Type, string Role, string? Username, string? Team);
record PlayerAction(string Type, int A, int B);
record ServerMessage<T>(string Type, T Data);

class MovePlan
{
    public MovePlan(Tank tank, int targetX, int targetY)
    {
        Tank = tank;
        TargetX = targetX;
        TargetY = targetY;
    }

    public Tank Tank { get; }
    public int TargetX { get; }
    public int TargetY { get; }
    public bool WillMove => Tank.X != TargetX || Tank.Y != TargetY;
}

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
