string url;

if (args.Length == 0)
{
    Console.Write("Url: ");
    string? urlInput = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(urlInput))
    {
        Console.WriteLine("Must not be empty");
        return;
    }
    url = urlInput;
}
else if (args.Length == 1)
{
    url = args[0];
}
else
{
    Console.WriteLine("Usage: TestClient.exe https://localhost:5001");
    return;
}

Console.Write("TeamId: ");
string? teamId = Console.ReadLine();

if (string.IsNullOrEmpty(teamId))
{
    Console.WriteLine("Must not be empty");
    return;
}

Console.Write("CharacterId: ");
string? characterId = Console.ReadLine();

if (string.IsNullOrEmpty(characterId))
{
    Console.WriteLine("Must not be empty");
    return;
}

try
{
    ChatClient client = new ChatClient();
    await client.Run(url, teamId, characterId);
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
    Console.WriteLine(ex.StackTrace);
}
Console.ReadKey();