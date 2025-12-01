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
var actionTask = PlayerBot(async (X, Y) => await SendActionAsync(socket, username ?? "player", X, Y));

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
                case "castles":
                    var castles = JsonSerializer.Deserialize<Castle[]>(root.GetProperty("Data"));
                    if (castles is not null)
                    {
                        OnCastles(castles);
                    }
                    break;
                case "players":
                    var players = JsonSerializer.Deserialize<Player[]>(root.GetProperty("Data"));
                    if (players is not null)
                    {
                        OnPlayers(players);
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

static async Task SendActionAsync(ClientWebSocket socket, string username, int X, int Y)
{
    if (socket.State == WebSocketState.Open)
    {
        var action = new
        {
            type = "action",
            x = X,
            y = Y
        };

        await SendStringAsync(socket, JsonSerializer.Serialize(action));
        Console.WriteLine($"{username} sent action: ({action.x}, {action.y})");
    }
}

static async Task SendStringAsync(ClientWebSocket socket, string message)
{
    var buffer = Encoding.UTF8.GetBytes(message);
    await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
}

// ========== Handlers and Bot Logic ==========

static void OnCastles(Castle[] castles)
{
    Console.WriteLine($"Received castles: {castles.Length}");
}

static void OnPlayers(Player[] players)
{
    Console.WriteLine($"Players online: {players.Length}");
}

static async Task PlayerBot(Func<int, int, Task> sendAction)
{
    var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

    try
    {
        while (await timer.WaitForNextTickAsync())
        {
            int x = Random.Shared.Next(0, 21);
            int y = Random.Shared.Next(0, 21);

            await sendAction(x, y);
            Console.WriteLine($"Sent action: ({x}, {y})");
        }
    }
    catch (OperationCanceledException)
    {
    }
}

record Castle(int X, int Y, string Team);
record Player(string Username, string Yeam, int X, int Y);
