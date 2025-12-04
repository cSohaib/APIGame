using System.Net.WebSockets;

static class GameLoop
{
    public static async Task RunAsync(GameRuntime runtime, CancellationToken cancellationToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                GameSnapshot snapshot;
                lock (runtime.GameLock)
                {
                    snapshot = GameLogic.AdvanceGameState(runtime.Tanks, runtime.Bullets, runtime.Castles, runtime.StaticMap, runtime.NextBulletId);
                }

                await BroadcastAsync(runtime, snapshot, cancellationToken);
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

    static async Task BroadcastAsync(GameRuntime runtime, GameSnapshot snapshot, CancellationToken cancellationToken)
    {
        var payload = new ServerMessage<GameSnapshot>("state", snapshot);

        foreach (var (id, connection) in runtime.Connections)
        {
            if (connection.Socket.State != WebSocketState.Open)
            {
                runtime.Connections.TryRemove(id, out _);
                continue;
            }

            try
            {
                await WebSocketHelpers.SendJsonAsync(connection.Socket, payload, cancellationToken);
            }
            catch
            {
                runtime.Connections.TryRemove(id, out _);
            }
        }
    }
}
