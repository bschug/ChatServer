using Grpc.Core;

namespace ChatServer.Services
{
    /// <summary>
    /// Accepts GRPC connections from clients and forwards them to their respective session.
    /// </summary>
    public class ChatService : ChatServer.ChatServerBase
    {
        private readonly ILogger _logger;

        public class ConnectionLostException : Exception { }

        public ChatService(ILogger<ChatService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// This method is called by gRPC whenever a client establishes a connection.
        /// Each connected client will have its own call to this, with its own requestStream, responseStream and context.
        /// </summary>
        /// <param name="requestStream">You read from this stream to receive messages from the client.</param>
        /// <param name="responseStream">You write to this stream to send messages to the client.</param>
        /// <param name="context">Metadata, including CancellationToken, see https://docs.microsoft.com/en-us/aspnet/core/grpc/services?view=aspnetcore-6.0</param>
        /// <returns></returns>
        public override async Task Communicate(IAsyncStreamReader<ClientMessage> requestStream, IServerStreamWriter<ServerMessage> responseStream, ServerCallContext context)
        {
            while (true)
            {
                // For now, simply echo all messages back to the client
                
                // You will want to implement the actual chat room logic here

                var clientMessage = await ReadMessageWithTimeoutAsync(requestStream, Timeout.InfiniteTimeSpan);
                await SendChatAsync(responseStream, $"Received {clientMessage.ContentCase}");
            }
        }

        /// <summary>
        /// Read a single message from the client.
        /// </summary>
        /// <exception cref="ConnectionLostException"></exception>
        /// <exception cref="TimeoutException"></exception>
        async Task<ClientMessage> ReadMessageWithTimeoutAsync(IAsyncStreamReader<ClientMessage> requestStream, TimeSpan timeout)
        {
            CancellationTokenSource cancellationTokenSource = new();
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            cancellationTokenSource.CancelAfter(timeout);

            try
            {
                bool didMove = await requestStream.MoveNext(cancellationToken);

                if (didMove == false)
                {
                    throw new ConnectionLostException();
                }

                return requestStream.Current;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                throw new TimeoutException();
            }
        }

        /// <summary>
        /// Send a ServerMessageChat message to the client, using the provided text and username "SYSTEM".
        /// </summary>
        async Task SendChatAsync(IServerStreamWriter<ServerMessage> responseStream, string text)
        {
            await responseStream.WriteAsync(new ServerMessage
            {
                Chat = new ServerMessageChat
                {
                    Text = text,
                    UserName = "SYSTEM"
                }
            });
        }
    }
}