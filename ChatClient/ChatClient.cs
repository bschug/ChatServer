using ChatServer;
using Grpc.Core;
using Grpc.Net.Client;

internal class ChatClient
{
    public async Task Run(string url, string chatRoomId, string userName)
    {
        Console.WriteLine("Connecting...");

        using var channel = GrpcChannel.ForAddress(url);
        var client = new ChatServer.ChatServer.ChatServerClient(channel);
        var connection = client.Communicate();

        Task readServerMessagesTask = ReadServerMessages(connection);

        Console.WriteLine("Logging in...");
        await Login(connection, chatRoomId, userName);

        Console.WriteLine("Type a chat message. Press Enter without a message to quit.");
        while (true)
        {
            string? input = Console.ReadLine();
            if (string.IsNullOrEmpty(input))
            {
                break;
            }

            await connection.RequestStream.WriteAsync(new ClientMessage 
            { 
                Chat = new ClientMessageChat 
                { 
                    Text = input 
                } 
            });
        }

        Console.WriteLine("Disconnecting...");
        await connection.RequestStream.CompleteAsync();
        await readServerMessagesTask;
    }

    private async Task ReadServerMessages(AsyncDuplexStreamingCall<ClientMessage, ServerMessage> connection)
    {
        await foreach (ServerMessage message in connection.ResponseStream.ReadAllAsync())
        {
            switch (message.ContentCase)
            {
                case ServerMessage.ContentOneofCase.LoginFailure:
                    Console.WriteLine($"Login Failed: {message.LoginFailure.Reason}");
                    return;

                case ServerMessage.ContentOneofCase.LoginSuccess:
                    Console.WriteLine("Login Successful");
                    break;

                case ServerMessage.ContentOneofCase.UserJoined:
                    Console.WriteLine($"{message.UserJoined.UserName} joined");
                    break;

                case ServerMessage.ContentOneofCase.UserLeft:
                    Console.WriteLine($"{message?.UserLeft.UserName} left");
                    break;

                case ServerMessage.ContentOneofCase.ShutDown:
                    Console.WriteLine("Server is shutting down.");
                    break;

                case ServerMessage.ContentOneofCase.Chat:
                    Console.WriteLine($"[{message.Chat.UserName}] {message.Chat.Text}");
                    break;

                case ServerMessage.ContentOneofCase.Tick:
                    Console.WriteLine($"Tick {message.Tick.Tick}");
                    break;

                default:
                    Console.WriteLine($"Unknown message received: {message.ContentCase}");
                    break;
            }
        }

        Console.WriteLine("Response channel closed.");
    }

    private async Task Login(AsyncDuplexStreamingCall<ClientMessage, ServerMessage> connection, string chatRoomId, string userName) 
    {
        await connection.RequestStream.WriteAsync(new ClientMessage
        {
            Login = new ClientMessageLogin
            {
                UserName = userName,
                ChatRoomId = chatRoomId,
            }
        });
    }
}
