using System.Net.WebSockets;

record Castle(int X, int Y, string Team)
{
    public int Hits { get; set; }
}

record Rock(int X, int Y);

class Tank
{
    public Tank(string username, string team, int x, int y, int @base, int head)
    {
        Username = username;
        Team = team;
        X = x;
        Y = y;
        Base = @base;
        Head = head;
    }

    public string Username { get; }
    public string Team { get; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Base { get; set; }
    public int Head { get; set; }
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
