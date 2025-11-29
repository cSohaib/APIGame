using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

const string WebSocketAddress = "ws://localhost:5291/ws";

Console.WriteLine("Welcome to the API Game websocket client.");
Console.Write("Connect as player or spectator? (p/s): ");
var roleInput = Console.ReadLine()?.Trim().ToLowerInvariant();
var isPlayer = roleInput == "p" || roleInput == "player";

string? username = null;
string? team = null;

if (isPlayer)
{
    Console.Write("Enter username: ");
    username = Console.ReadLine();

    Console.Write("Enter team code: ");
    team = Console.ReadLine();
}

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

await SendJoinAsync(socket, isPlayer, username, team);

var receiveTask = ReceiveMessagesAsync(socket);

if (isPlayer)
{
    var actionTask = SendRandomActionsAsync(socket, username ?? "player");
    await Task.WhenAny(receiveTask, actionTask);
}
else
{
    await receiveTask;
}

static async Task SendJoinAsync(ClientWebSocket socket, bool isPlayer, string? username, string? team)
{
    var payload = isPlayer
        ? new { type = "join", role = "player", username = username ?? string.Empty, team = team ?? string.Empty }
        : new { type = "join", role = "spectator" };

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
            var type = root.GetProperty("type").GetString();

            switch (type)
            {
                case "castles":
                    Console.WriteLine($"Received castles: {root.GetProperty("data").GetRawText()}");
                    break;
                case "players":
                    Console.WriteLine($"Players: {root.GetProperty("data").GetArrayLength()} online");
                    break;
                case "error":
                    Console.WriteLine($"Error from server: {root.GetProperty("data").GetString()}");
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

static async Task SendRandomActionsAsync(ClientWebSocket socket, string username)
{
    var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

    try
    {
        while (socket.State == WebSocketState.Open && await timer.WaitForNextTickAsync())
        {
            var action = new
            {
                type = "action",
                x = Random.Shared.Next(0, 21),
                y = Random.Shared.Next(0, 21)
            };

            await SendStringAsync(socket, JsonSerializer.Serialize(action));
            Console.WriteLine($"{username} sent action: ({action.x}, {action.y})");
        }
    }
    catch (OperationCanceledException)
    {
    }
}

static async Task SendStringAsync(ClientWebSocket socket, string message)
{
    var buffer = Encoding.UTF8.GetBytes(message);
    await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
}
