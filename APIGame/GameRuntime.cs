using System.Collections.Concurrent;

class GameRuntime
{
    public GameRuntime()
    {
        Castles = GameSetup.CreateCastles();
        Rocks = GameSetup.CreateRocks();
        StaticMap = GameSetup.CreateStaticMap(Castles, Rocks);
    }

    public Castle[] Castles { get; }
    public Rock[] Rocks { get; }
    public int[,] StaticMap { get; }
    public ConcurrentDictionary<Guid, ClientConnection> Connections { get; } = new();
    public ConcurrentDictionary<string, Tank> Tanks { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<Bullet> Bullets { get; } = new();
    public object GameLock { get; } = new();
    public CancellationTokenSource BroadcasterCts { get; } = new();

    long _bulletIdCounter = 0;
    public long NextBulletId() => Interlocked.Increment(ref _bulletIdCounter);
}
