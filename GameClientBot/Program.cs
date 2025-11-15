// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");



record Player(Guid Id, string Username, string ColorHex);
record JoinRequest(string Username, string ColorHex);
record JoinResponse(Guid PlayerId);
record ActionRequest(Guid PlayerId, string Command);