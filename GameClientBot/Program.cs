using System.Net.Http.Json;

const string ApiBaseAddress = "https://localhost:7253";

var handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
};

using var httpClient = new HttpClient(handler)
{
    BaseAddress = new Uri(ApiBaseAddress)
};

Console.WriteLine("Welcome to the API Game client.");

var playerId = await JoinAsync(httpClient);
Console.WriteLine($"Joined successfully. PlayerId: {playerId}");

await PollAndCommandAsync(httpClient, playerId);

static async Task<Guid> JoinAsync(HttpClient httpClient)
{
    while (true)
    {
        Console.Write("Enter username: ");
        var username = Console.ReadLine() ?? string.Empty;

        Console.Write("Enter color hex (#RRGGBB): ");
        var colorHex = Console.ReadLine() ?? string.Empty;

        try
        {
            var response = await httpClient.PostAsJsonAsync("/join", new JoinRequest(username, colorHex));
            if (response.IsSuccessStatusCode)
            {
                var joinResponse = await response.Content.ReadFromJsonAsync<JoinResponse>();
                if (joinResponse?.PlayerId != Guid.Empty)
                {
                    return joinResponse!.PlayerId;
                }

                Console.WriteLine("Join succeeded but response was invalid. Retrying...");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Join failed ({(int)response.StatusCode} {response.ReasonPhrase}): {error}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling /join: {ex.Message}");
        }

        Console.WriteLine("Please try again.\n");
    }
}

static async Task PollAndCommandAsync(HttpClient httpClient, Guid playerId)
{
    while (true)
    {
        await GetStatsAsync(httpClient);
        await SendCommandAsync(httpClient, playerId);
        await Task.Delay(500);
    }
}

static async Task GetStatsAsync(HttpClient httpClient)
{
    try
    {
        var response = await httpClient.GetAsync("/stats");
        if (response.IsSuccessStatusCode)
        {
            var stats = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Stats: {stats}");
        }
        else
        {
            Console.WriteLine($"Failed to get stats: {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error getting stats: {ex.Message}");
    }
}

static async Task SendCommandAsync(HttpClient httpClient, Guid playerId)
{
    try
    {
        var actionRequest = new ActionRequest(playerId, "COMMAND");
        var response = await httpClient.PostAsJsonAsync("/action", actionRequest);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Failed to send command: {(int)response.StatusCode} {response.ReasonPhrase} - {error}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error sending command: {ex.Message}");
    }
}

record JoinRequest(string Username, string ColorHex);
record JoinResponse(Guid PlayerId);
record ActionRequest(Guid PlayerId, string Command);
