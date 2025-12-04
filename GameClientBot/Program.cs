using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

const string WebSocketAddress = "ws://localhost:5291/ws";

Console.WriteLine("Welcome to the API Game websocket client.");
Console.Write("Enter username: ");
var username = Console.ReadLine();

Console.Write("Enter team code: ");
var team = Console.ReadLine();

using var socket = new ClientWebSocket();

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
var actionTask = PlayerBot(async (a, b) => await SendActionAsync(socket, username ?? "player", a, b));

await Task.WhenAny(receiveTask, actionTask);

static async Task SendJoinAsync(ClientWebSocket socket, string? username, string? team)
{
    var payload = new { type = "join", role = "player", username = username ?? string.Empty, team = team ?? string.Empty };

    await SendStringAsync(socket, JsonSerializer.Serialize(payload));
}

static async Task ReceiveMessagesAsync(ClientWebSocket socket)
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
                        OnTurn(state.Tanks, state.Bullets, state.Explosions);
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

static async Task SendActionAsync(ClientWebSocket socket, string username, int A, int B)
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

static async Task SendStringAsync(ClientWebSocket socket, string message)
{
    var buffer = Encoding.UTF8.GetBytes(message);
    await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
}

// ========== Handlers and Bot Logic ==========

static void OnInitialisation(Castle[] castles, Rock[] rocks)
{
    Console.WriteLine($"Received castles: {castles.Length}, rocks: {rocks.Length}");
}

static void OnTurn(Tank[] tanks, Bullet[] bullets, Explosion[] explosions)
{
    Console.WriteLine($"Tanks: {tanks.Length}, bullets: {bullets.Length}, explosions: {explosions.Length}");
}

static async Task PlayerBot(Func<int, int, Task> sendAction)
{
    var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

    try
    {
        while (await timer.WaitForNextTickAsync())
        {
            int a = new[] { 0, 1, 2, 3, 3, 3, 3 }[Random.Shared.Next(7)];
            int b = new[] { 0, 1, 2, 3, 3, 3, 3 }[Random.Shared.Next(7)];

            await sendAction(a, b);
            Console.WriteLine($"Sent action: ({a}, {b})");
        }
    }
    catch (OperationCanceledException)
    {
    }
}

record Castle(int X, int Y, string Team, int Hits);
record Rock(int X, int Y);
record Tank(string Username, string Team, int X, int Y, int Base, int Head, int Score, bool IsDestroyed);
record Bullet(long Id, string Username, string Team, int X, int Y, int Direction);
record Explosion(int X, int Y);
record GameInitialisation(Castle[] Castles, Rock[] Rocks);
record GameState(Tank[] Tanks, Bullet[] Bullets, Explosion[] Explosions, string[] InfoText);
