using System.Threading.Channels;
using ChatServer;
using Grpc.Core;
using Grpc.Net.Client;

namespace IntegrationTests;

public class TestClient : IAsyncDisposable
{
    public List<ServerMessage> MessagesReceived { get; } = new();

    private GrpcChannel _channel;
    private ChatServer.ChatServer.ChatServerClient _client;
    private AsyncDuplexStreamingCall<ClientMessage, ServerMessage> _connection;

    private CancellationTokenSource _cancellationTokenSource = new();
    private Task? _readServerMessagesTask;

    private DateTimeOffset _lastMessageSentTime = DateTimeOffset.UtcNow;

    private TestClient(
        GrpcChannel channel,
        ChatServer.ChatServer.ChatServerClient client,
        AsyncDuplexStreamingCall<ClientMessage, ServerMessage> connection)
    {
        _channel = channel;
        _client = client;
        _connection = connection;
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.RequestStream.CompleteAsync();
        await CastAndDispose(_connection);

        if (_readServerMessagesTask != null)
        {
            _cancellationTokenSource.Cancel();
            await _readServerMessagesTask;
            await CastAndDispose(_cancellationTokenSource);
        }

        await CastAndDispose(_channel);

        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
                await resourceAsyncDisposable.DisposeAsync();
            else
                resource.Dispose();
        }
    }

    public static async Task<TestClient> Login(TestServer server, string chatRoomId, string userName)
    {
        var channel = GrpcChannel.ForAddress(server.Url);
        var client = new ChatServer.ChatServer.ChatServerClient(channel);
        var connection = client.Communicate();

        var result = new TestClient(channel, client, connection);
        result.BeginReadingMessages();
        _ = result.Keepalive();

        await connection.RequestStream.WriteAsync(new ClientMessage
        {
            Login = new ClientMessageLogin
            {
                UserName = userName,
                ChatRoomId = chatRoomId,
            }
        });

        return result;
    }

    public Task Say(string text)
    {
        if (_connection == null)
        {
            throw new ApplicationException("not logged in");
        }

        lock (this)
        {
            _lastMessageSentTime = DateTimeOffset.UtcNow;

            return _connection.RequestStream.WriteAsync(
                new ClientMessage { Chat = new() { Text = text } });
        }
    }

    private void BeginReadingMessages()
    {
        _readServerMessagesTask = ReadServerMessages();
    }

    private async Task ReadServerMessages()
    {
        var cancellationToken = _cancellationTokenSource.Token;

        try
        {
            await foreach (ServerMessage message in _connection.ResponseStream.ReadAllAsync(cancellationToken))
            {
                MessagesReceived.Add(message);
            }
        }
        catch (RpcException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }

        Console.WriteLine("Response channel closed.");
    }

    private async Task Keepalive()
    {
        var cancellationToken = _cancellationTokenSource.Token;

        while (!cancellationToken.IsCancellationRequested)
        {
            var nextKeepaliveTime = _lastMessageSentTime + TimeSpan.FromSeconds(30);
            var timeToWait = nextKeepaliveTime - DateTimeOffset.UtcNow;

            if (timeToWait <= TimeSpan.Zero)
            {
                await Say("Keepalive");
            }
            else
            {
                await Task.Delay(timeToWait, cancellationToken);
            }
        }
    }
}
