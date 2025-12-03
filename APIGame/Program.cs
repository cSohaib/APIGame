using System.Net.WebSockets;

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

var runtime = new GameRuntime();

_ = Task.Run(() => GameLoop.RunAsync(runtime, runtime.BroadcasterCts.Token));

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

    runtime.Connections[connectionId] = connection;

    try
    {
        if (!await WebSocketHelpers.ReceiveJoinAsync(socket, connection, runtime, context.RequestAborted))
        {
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Invalid join request", context.RequestAborted);
            return;
        }

        await WebSocketHelpers.SendJsonAsync(socket, new ServerMessage<GameInitialisation>("initialization", new GameInitialisation(runtime.Castles, runtime.Rocks)), context.RequestAborted);

        await WebSocketHelpers.ReceiveActionsAsync(connectionId, connection, runtime, context.RequestAborted);
    }
    finally
    {
        runtime.Connections.TryRemove(connectionId, out _);
        GameLogic.RemoveTank(connection.Username, runtime.Tanks, runtime.GameLock);
    }
});

app.Lifetime.ApplicationStopping.Register(() => runtime.BroadcasterCts.Cancel());

app.Run();
