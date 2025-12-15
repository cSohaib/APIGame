using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

const int MapWidth = 24;
const int MapHeight = 16;
const int SimulationDepth = 10;
const int SimulationIterations = 100;

Castle[] castles = Array.Empty<Castle>();
Rock[] rocks = Array.Empty<Rock>();
HashSet<(int x, int y)> staticBlocked = new();
string playerName = string.Empty;
string? playerTeam = null;
ClientWebSocket? clientSocket;

const string WebSocketAddress = "ws://localhost:5291/ws";

Console.WriteLine("Welcome to the API Game websocket client.");
Console.Write("Enter username: ");
var username = Console.ReadLine();
playerName = username ?? "player";

Console.Write("Enter team code: ");
var team = Console.ReadLine();

using var socket = new ClientWebSocket();
clientSocket = socket;

try
{
    await socket.ConnectAsync(new Uri(WebSocketAddress), CancellationToken.None);
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to connect to server: {ex.Message}");
    return;
}

await SendJoinAsync(socket, username, team);

var receiveTask = ReceiveMessagesAsync(socket);

await receiveTask;

async Task SendJoinAsync(ClientWebSocket socket, string? username, string? team)
{
    var payload = new { type = "join", role = "player", username = username ?? string.Empty, team = team ?? string.Empty };

    await SendStringAsync(socket, JsonSerializer.Serialize(payload));
}

async Task ReceiveMessagesAsync(ClientWebSocket socket)
{
    var buffer = new byte[4096];

    while (socket.State == WebSocketState.Open)
    {
        var builder = new StringBuilder();

        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                return;
            }

            builder.Append(Encoding.UTF8.GetString(buffer.AsSpan(0, result.Count)));
        }
        while (!result.EndOfMessage);

        var message = builder.ToString();

        try
        {
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            var type = root.GetProperty("Type").GetString();

            switch (type)
            {
                case "initialization":
                    var init = JsonSerializer.Deserialize<GameInitialisation>(root.GetProperty("Data"));
                    if (init is not null)
                    {
                        OnInitialisation(init.Castles, init.Rocks);
                    }
                    break;
                case "state":
                    var state = JsonSerializer.Deserialize<GameState>(root.GetProperty("Data"));
                    if (state is not null)
                    {
                        await OnTurn(state.Tanks, state.Bullets, state.Explosions);
                    }
                    break;
                case "error":
                    Console.WriteLine($"Error from server: {root.GetProperty("Data").GetString()}");
                    break;
                default:
                    Console.WriteLine($"Received message: {message}");
                    break;
            }
        }
        catch (Exception)
        {
            Console.WriteLine($"Received raw message: {message}");
        }
    }
}

async Task SendActionAsync(ClientWebSocket socket, string username, int A, int B)
{
    if (socket.State == WebSocketState.Open)
    {
        var action = new
        {
            type = "action",
            a = A,
            b = B
        };

        await SendStringAsync(socket, JsonSerializer.Serialize(action));
        Console.WriteLine($"{username} sent action: ({action.a}, {action.b})");
    }
}

async Task SendStringAsync(ClientWebSocket socket, string message)
{
    var buffer = Encoding.UTF8.GetBytes(message);
    await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
}

// ========== Handlers and Bot Logic ==========

void OnInitialisation(Castle[] newCastles, Rock[] newRocks)
{
    Console.WriteLine($"Received castles: {newCastles.Length}, rocks: {newRocks.Length}");
    castles = newCastles;
    rocks = newRocks;
    staticBlocked = BuildStaticBlocked(castles, rocks);
}

async Task OnTurn(Tank[] tanks, Bullet[] bullets, Explosion[] explosions)
{
    if (clientSocket is null)
    {
        return;
    }

    Console.WriteLine($"Tanks: {tanks.Length}, bullets: {bullets.Length}, explosions: {explosions.Length}");

    var myTank = tanks.FirstOrDefault(t => string.Equals(t.Username, playerName, StringComparison.OrdinalIgnoreCase));
    if (myTank is null)
    {
        return;
    }

    playerTeam ??= myTank.Team;
    var enemyCastle = castles.FirstOrDefault(c => !string.Equals(c.Team, playerTeam, StringComparison.OrdinalIgnoreCase));
    if (enemyCastle is null)
    {
        await SendActionAsync(clientSocket, playerName, 0, 0);
        return;
    }

    var bestAction = ChooseBestAction(myTank, tanks, bullets, enemyCastle);
    await SendActionAsync(clientSocket, playerName, bestAction.a, bestAction.b);
}

(int a, int b) ChooseBestAction(Tank myTank, Tank[] allTanks, Bullet[] bullets, Castle enemyCastle)
{
    var existingBullets = bullets.Select(b => new SimBullet(b.X, b.Y, b.Direction, b.Team, b.Username)).ToList();
    var otherTanks = allTanks.Where(t => !string.Equals(t.Username, myTank.Username, StringComparison.OrdinalIgnoreCase))
                              .Select(t => new SimTank(t.Username, t.Team, t.X, t.Y, t.Base, t.Head, t.IsDestroyed))
                              .ToList();

    double bestScore = double.NegativeInfinity;
    (int a, int b) bestAction = (0, 0);

    for (int i = 0; i < SimulationIterations; i++)
    {
        var plan = GenerateRandomPlan(SimulationDepth);
        var score = SimulatePlan(myTank, otherTanks, existingBullets, enemyCastle, plan);

        if (score > bestScore)
        {
            bestScore = score;
            bestAction = plan[0];
        }
    }

    return bestAction;
}

(int a, int b)[] GenerateRandomPlan(int length)
{
    var plan = new (int a, int b)[length];

    for (int i = 0; i < length; i++)
    {
        plan[i] = (Random.Shared.Next(4), Random.Shared.Next(4));
    }

    return plan;
}

double SimulatePlan(Tank myTank, List<SimTank> otherTanks, List<SimBullet> bullets, Castle enemyCastle, (int a, int b)[] plan)
{
    var simMyTank = new SimTank(myTank.Username, myTank.Team, myTank.X, myTank.Y, myTank.Base, myTank.Head, myTank.IsDestroyed);
    var simOtherTanks = otherTanks.Select(t => t.Clone()).ToList();
    var simBullets = bullets.Select(b => b.Clone()).ToList();

    double score = 0;
    int previousVerticalDistance = VerticalDistance(simMyTank, enemyCastle);

    foreach (var action in plan)
    {
        var blocked = BuildBlockedSet(simMyTank, simOtherTanks);

        ApplyAction(simMyTank, action.a, action.b, blocked, simBullets);

        blocked = BuildBlockedSet(simMyTank, simOtherTanks);

        foreach (var tank in simOtherTanks.Where(t => !t.IsDestroyed))
        {
            ApplyAction(tank, 3, 3, blocked, simBullets);
        }

        ResolveBullets(simBullets, simMyTank, simOtherTanks, enemyCastle, ref score);

        var currentVerticalDistance = VerticalDistance(simMyTank, enemyCastle);
        if (currentVerticalDistance < previousVerticalDistance)
        {
            score += 1;
        }
        else if (currentVerticalDistance > previousVerticalDistance)
        {
            score -= 1;
        }

        previousVerticalDistance = currentVerticalDistance;

        if (simMyTank.IsDestroyed)
        {
            score = 0;
            break;
        }
    }

    return score;
}

void ApplyAction(SimTank tank, int actionA, int actionB, HashSet<(int x, int y)> blocked, List<SimBullet> bullets)
{
    if (tank.IsDestroyed)
    {
        return;
    }

    if (actionA == 1)
    {
        tank.Base = (tank.Base + 1) % 4;
    }
    else if (actionA == 2)
    {
        tank.Base = (tank.Base + 3) % 4;
    }

    if (actionB == 1)
    {
        tank.Head = (tank.Head + 1) % 4;
    }
    else if (actionB == 2)
    {
        tank.Head = (tank.Head + 3) % 4;
    }

    if (actionA == 3)
    {
        AttemptMove(tank, blocked);
    }

    if (actionB == 3)
    {
        var spawn = GetFrontCell(tank.X, tank.Y, tank.Head);
        if (spawn.HasValue)
        {
            var (bx, by) = spawn.Value;
            bullets.Add(new SimBullet(bx, by, tank.Head, tank.Team, tank.Username));
        }
    }
}

void AttemptMove(SimTank tank, HashSet<(int x, int y)> blocked)
{
    var offset = DirectionOffset(tank.Base);
    var newX = tank.X + offset.dx;
    var newY = tank.Y + offset.dy;

    if (newX < 0 || newX >= MapWidth || newY < 0 || newY >= MapHeight)
    {
        return;
    }

    if (blocked.Contains((newX, newY)))
    {
        return;
    }

    tank.X = newX;
    tank.Y = newY;
}

void ResolveBullets(List<SimBullet> bullets, SimTank myTank, List<SimTank> otherTanks, Castle enemyCastle, ref double score)
{
    foreach (var bullet in bullets.Where(b => b.IsActive))
    {
        var offset = DirectionOffset(bullet.Direction);
        var newX = bullet.X + offset.dx;
        var newY = bullet.Y + offset.dy;

        if (newX < 0 || newX >= MapWidth || newY < 0 || newY >= MapHeight)
        {
            bullet.IsActive = false;
            continue;
        }

        if (staticBlocked.Contains((newX, newY)))
        {
            if (IsInsideCastle(newX, newY, enemyCastle) && !string.Equals(bullet.Team, enemyCastle.Team, StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
            }

            bullet.IsActive = false;
            continue;
        }

        bullet.X = newX;
        bullet.Y = newY;

        if (!myTank.IsDestroyed && bullet.X == myTank.X && bullet.Y == myTank.Y && !string.Equals(bullet.Username, myTank.Username, StringComparison.OrdinalIgnoreCase))
        {
            myTank.IsDestroyed = true;
            score = 0;
            bullet.IsActive = false;
            continue;
        }

        foreach (var tank in otherTanks.Where(t => !t.IsDestroyed))
        {
            if (tank.X == bullet.X && tank.Y == bullet.Y)
            {
                if (!string.Equals(tank.Team, myTank.Team, StringComparison.OrdinalIgnoreCase))
                {
                    score += 20;
                }

                tank.IsDestroyed = true;
                bullet.IsActive = false;
                break;
            }
        }
    }

    bullets.RemoveAll(b => !b.IsActive);
}

int VerticalDistance(SimTank tank, Castle castle)
{
    var castleTop = castle.Y;
    var castleBottom = castle.Y + 1;

    if (tank.Y < castleTop)
    {
        return castleTop - tank.Y;
    }

    if (tank.Y > castleBottom)
    {
        return tank.Y - castleBottom;
    }

    return 0;
}

HashSet<(int x, int y)> BuildStaticBlocked(Castle[] castles, Rock[] rocks)
{
    var blocked = new HashSet<(int x, int y)>();

    foreach (var rock in rocks)
    {
        blocked.Add((rock.X, rock.Y));
    }

    foreach (var castle in castles)
    {
        blocked.Add((castle.X, castle.Y));
        blocked.Add((castle.X + 1, castle.Y));
        blocked.Add((castle.X, castle.Y + 1));
        blocked.Add((castle.X + 1, castle.Y + 1));
    }

    return blocked;
}

HashSet<(int x, int y)> BuildBlockedSet(SimTank myTank, List<SimTank> otherTanks)
{
    var blocked = new HashSet<(int x, int y)>(staticBlocked);

    if (!myTank.IsDestroyed)
    {
        blocked.Add((myTank.X, myTank.Y));
    }

    foreach (var tank in otherTanks.Where(t => !t.IsDestroyed))
    {
        blocked.Add((tank.X, tank.Y));
    }

    return blocked;
}

(int dx, int dy) DirectionOffset(int direction) => direction switch
{
    0 => (0, -1),
    1 => (1, 0),
    2 => (0, 1),
    3 => (-1, 0),
    _ => (0, 0)
};

(int x, int y)? GetFrontCell(int x, int y, int direction)
{
    var offset = DirectionOffset(direction);
    var newX = x + offset.dx;
    var newY = y + offset.dy;

    if (newX < 0 || newX >= MapWidth || newY < 0 || newY >= MapHeight)
    {
        return null;
    }

    return (newX, newY);
}

bool IsInsideCastle(int x, int y, Castle castle)
{
    return x >= castle.X && x <= castle.X + 1 && y >= castle.Y && y <= castle.Y + 1;
}

class SimTank
{
    public SimTank(string username, string team, int x, int y, int baseDir, int headDir, bool isDestroyed)
    {
        Username = username;
        Team = team;
        X = x;
        Y = y;
        Base = baseDir;
        Head = headDir;
        IsDestroyed = isDestroyed;
    }

    public string Username { get; }
    public string Team { get; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Base { get; set; }
    public int Head { get; set; }
    public bool IsDestroyed { get; set; }

    public SimTank Clone() => new(Username, Team, X, Y, Base, Head, IsDestroyed);
}

class SimBullet
{
    public SimBullet(int x, int y, int direction, string team, string username)
    {
        X = x;
        Y = y;
        Direction = direction;
        Team = team;
        Username = username;
    }

    public int X { get; set; }
    public int Y { get; set; }
    public int Direction { get; }
    public string Team { get; }
    public string Username { get; }
    public bool IsActive { get; set; } = true;

    public SimBullet Clone() => new(X, Y, Direction, Team, Username) { IsActive = IsActive };
}

record Castle(int X, int Y, string Team, int Hits);
record Rock(int X, int Y);
record Tank(string Username, string Team, int X, int Y, int Base, int Head, int Score, bool IsDestroyed);
record Bullet(long Id, string Username, string Team, int X, int Y, int Direction);
record Explosion(int X, int Y);
record GameInitialisation(Castle[] Castles, Rock[] Rocks);
record GameState(Tank[] Tanks, Bullet[] Bullets, Explosion[] Explosions, string[] InfoText);
