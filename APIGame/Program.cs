using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();

var playersById = new Dictionary<Guid, Player>();
var usernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var colors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var syncRoot = new object();

app.MapPost("/join", (JoinRequest? request) =>
{
    if (request is null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.ColorHex))
    {
        return Results.BadRequest("Username and color are required.");
    }

    var username = request.Username.Trim();
    var normalizedColor = NormalizeColor(request.ColorHex);

    if (normalizedColor is null)
    {
        return Results.BadRequest("Color must be a 6-digit hex code (e.g. #AABBCC).");
    }

    Player player;
    lock (syncRoot)
    {
        if (usernames.Contains(username))
        {
            return Results.Conflict("Username already exists.");
        }

        if (colors.Contains(normalizedColor))
        {
            return Results.Conflict("Color already exists.");
        }

        player = new Player(Guid.NewGuid(), username, normalizedColor);
        playersById[player.Id] = player;
        usernames.Add(username);
        colors.Add(normalizedColor);
    }

    return Results.Ok(new JoinResponse(player.Id));
});

app.MapGet("/stats", () =>
{
    int playerCount;
    lock (syncRoot)
    {
        playerCount = playersById.Count;
    }

    return Results.Text($"Players online: {playerCount}");
});

app.MapPost("/action", (ActionRequest? request) =>
{
    if (request is null || request.PlayerId == Guid.Empty || string.IsNullOrWhiteSpace(request.Command))
    {
        return Results.BadRequest("PlayerId and command are required.");
    }

    lock (syncRoot)
    {
        if (!playersById.ContainsKey(request.PlayerId))
        {
            return Results.NotFound("Player not found.");
        }
    }

    Console.WriteLine($"[{DateTimeOffset.UtcNow:u}] {request.PlayerId}: {request.Command}");
    return Results.NoContent();
});

app.Run();

static string? NormalizeColor(string color)
{
    var match = Regex.Match(color.Trim(), "^#?([0-9a-fA-F]{6})$");
    return match.Success ? $"#{match.Groups[1].Value.ToUpperInvariant()}" : null;
}

record Player(Guid Id, string Username, string ColorHex);
record JoinRequest(string Username, string ColorHex);
record JoinResponse(Guid PlayerId);
record ActionRequest(Guid PlayerId, string Command);
