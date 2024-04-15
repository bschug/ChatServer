using Serilog;

namespace IntegrationTests;

[Collection("IntegrationTests")]
public class IntegrationTests
{
    [Fact]
    public async Task I_do_not_receive_my_own_chat_messages()
    {
        using var server = await TestServer.StartNew();
        var alice = await TestClient.Login(server, "Legendary", "Alice");

        await alice.Say("Hello");
        await Task.Delay(TimeSpan.FromSeconds(3));

        Assert.DoesNotContain(alice.MessagesReceived, x => x.Chat?.Text == "Hello");
    }

    [Fact]
    public async Task Others_receive_my_chat_messages()
    {
        using var server = await TestServer.StartNew();
        var alice = await TestClient.Login(server, "Legendary", "Alice");
        var bob = await TestClient.Login(server, "Legendary", "Bob");

        await alice.Say("Hello");
        await Task.Delay(TimeSpan.FromSeconds(3));

        Assert.Contains(bob.MessagesReceived, x => x.Chat is { Text: "Hello", UserName: "Alice" });
    }

    [Fact]
    public async Task I_can_only_see_messages_from_my_room()
    {
        using var server = await TestServer.StartNew();
        var alice = await TestClient.Login(server, "Legendary", "Alice");
        var bob = await TestClient.Login(server, "Legendary", "Bob");
        var stranger = await TestClient.Login(server, "MetaGames", "Stranger");

        await bob.Say("Hello Alice");
        await stranger.Say("Hello Alice");
        await Task.Delay(TimeSpan.FromSeconds(3));

        Assert.Contains(alice.MessagesReceived, x => x.Chat is { Text: "Hello Alice", UserName: "Bob" });
        Assert.DoesNotContain(alice.MessagesReceived, x => x.Chat is { Text: "Hello Alice", UserName: "Stranger" });
    }

    [Fact]
    public async Task Automated_messages()
    {
        using var server = await TestServer.StartNew();
        var startTime = DateTimeOffset.UtcNow;

        var alice = await TestClient.Login(server, "Legendary", "Alice");

        await WaitUntil(0, 40);
        Assert.Contains(alice.MessagesReceived, x => x.Tick is { Tick: 1 });
        var bob = await TestClient.Login(server, "Legendary", "Bob");

        await WaitUntil(0, 45);
        await alice.DisposeAsync();

        await WaitUntil(1, 10);
        Assert.DoesNotContain(bob.MessagesReceived, x => x.Tick is { Tick: 1 });
        Assert.Contains(bob.MessagesReceived, x => x.Tick is { Tick: 2 });
        await bob.DisposeAsync();

        await WaitUntil(1, 20);
        var alice2 = await TestClient.Login(server, "Legendary", "Alice");

        await WaitUntil(2, 0);
        Assert.Contains(alice2.MessagesReceived, x => x.Tick is { Tick: 1 });
        Assert.DoesNotContain(alice2.MessagesReceived, x => x.Tick is { Tick: 2 });


        Task WaitUntil(int minutes, int seconds)
        {
            var targetTime = startTime + TimeSpan.FromSeconds(minutes * 60 + seconds);
            var remaining = targetTime - DateTimeOffset.UtcNow;

            if (remaining < TimeSpan.Zero)
            {
                Log.Error("{Minutes}:{Seconds} has already passed", minutes, seconds);
                return Task.CompletedTask;
            }

            return Task.Delay(remaining);
        }
    }

    [Fact]
    public async Task StressTest_many_rooms()
    {
        using var server = await TestServer.StartNew();

        var cancellationTokenSource = new CancellationTokenSource();
        var spamTask = SpamOtherRooms(server, cancellationTokenSource.Token);

        await Task.Delay(TimeSpan.FromSeconds(0.1f));
        var alice = await TestClient.Login(server, "Legendary", "Alice");
        var bob = await TestClient.Login(server, "Legendary", "Bob");

        var expectedMessages = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            var text = $"Message{i}";
            await alice.Say(text);
            expectedMessages.Add(text);
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        cancellationTokenSource.Cancel();
        await spamTask;

        await Task.Delay(TimeSpan.FromSeconds(3));

        var actualMessages = bob.MessagesReceived
            .Where(x => x.Chat?.UserName == "Alice")
            .Select(x => x.Chat.Text);

        Assert.Equal(expectedMessages, actualMessages);


        async Task SpamOtherRooms(TestServer testServer, CancellationToken token)
        {
            var clients = new List<TestClient>();

            try
            {
                for (var i=0; i < 100; i++)
                {
                    var randomRoom = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                    var randomName = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                    var client = await TestClient.Login(testServer, randomRoom, randomName);
                    clients.Add(client);
                }
            }
            finally
            {
                foreach (var client in clients)
                {
                    await client.DisposeAsync();
                }
            }
        }
    }
}
