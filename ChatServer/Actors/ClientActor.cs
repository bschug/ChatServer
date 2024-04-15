using Grpc.Core;
using Proto;

namespace ChatServer.Actors;

/// <summary>
/// Writes to the response stream.
/// Additionally, the existence of this actor provides the session with a way of detecting when
/// a client disconnects.
/// </summary>
public class ClientActor : IActor
{
    private IServerStreamWriter<ServerMessage> _responseStream;

    public ClientActor(IServerStreamWriter<ServerMessage> responseStream)
    {
        _responseStream = responseStream;
    }

    public Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case ServerMessage serverMessage:
                return _responseStream.WriteAsync(serverMessage);
        }

        return Task.CompletedTask;
    }
}
