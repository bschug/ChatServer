using Grpc.Core;
using Proto;
using Proto.Timers;
using Log = Serilog.Log;

namespace ChatServer.Actors;

/// <summary>
/// Represents a single chat room.
/// This manages the list of clients connected to the room and routes messages between them.
/// When the last client disconnects, the room destroys itself.
/// </summary>
public class ChatRoomActor : IActor
{
    public record JoinRequest(string Name, IServerStreamWriter<ServerMessage> ResponseStream);
    public record ChatMessage(string Text, PID ClientPid);
    private record NextTickMessage();

    private record ConnectionData(PID Pid, string Name);

    /// <summary>
    /// Actor context for current request.
    /// </summary>
    private IContext _context = null!;

    private Dictionary<PID, ConnectionData> _connections = new();

    private CancellationTokenSource? _tickCancellation = null!;
    private int _tickCount = 0;

    public Task ReceiveAsync(IContext context)
    {
        _context = context;

        switch (context.Message)
        {
            case Started:
                HandleStarted();
                break;

            case JoinRequest joinMessage:
            {
                var clientPid = HandleJoin(joinMessage);
                context.Respond(clientPid);
                break;
            }

            case ChatMessage chatMessage:
                HandleChat(chatMessage);
                break;

            case NextTickMessage:
                HandleNextTick();
                break;

            case Terminated terminated:
                HandleTerminated(terminated);
                break;

            case Stopping:
                HandleStopping();
                break;
        }

        return Task.CompletedTask;
    }

    private void HandleStarted()
    {
        var scheduler = new Scheduler(_context);
        _tickCancellation = scheduler.SendRepeatedly(
            TimeSpan.FromSeconds(30),
            _context.Self,
            new NextTickMessage());
    }

    private PID HandleJoin(JoinRequest joinRequest)
    {
        var props = Props.FromProducer(() => new ClientActor(joinRequest.ResponseStream));
        var clientActorPid = _context.SpawnNamed(props, joinRequest.Name);
        var name = joinRequest.Name;

        _connections[clientActorPid] = new ConnectionData(clientActorPid, name);
        Log.Information("{Name} joined chat room {RoomPid} from {ClientPid}", name, _context.Self, clientActorPid);

        return clientActorPid;
    }

    private void HandleTerminated(Terminated terminated)
    {
        // Note: we don't need to explicitly .Watch() the client actors because they are children
        // of the room and an actor is always notified about the death of its children.

        if (_connections.Remove(terminated.Who, out var removedConnection))
        {
            Log.Information("{Name} left chat room {Pid}", removedConnection.Name, _context.Self);
        }

        if (_connections.Count == 0)
        {
            Log.Information("Last user has left, room {Pid} is closing", _context.Self);
            _context.Poison(_context.Self);
        }
    }

    private void HandleChat(ChatMessage chatMessage)
    {
        if (!_connections.TryGetValue(chatMessage.ClientPid, out var sender))
        {
            Log.Error("Ignoring chat message from non-member {Pid}", _context.Sender);
            return;
        }

        Log.Debug("{UserName}: {Text}", sender.Name, chatMessage.Text);

        var serverMessage = new ServerMessage { Chat = new()
        {
            Text = chatMessage.Text,
            UserName = sender.Name
        } };

        foreach (var connection in _connections.Values)
        {
            if (connection != sender)
            {
                _context.Send(connection.Pid, serverMessage);
            }
        }
    }

    private void HandleNextTick()
    {
        _tickCount += 1;
        var serverMessage = new ServerMessage { Tick = new() { Tick = _tickCount } };

        Log.Debug("Room {Pid} sending tick {Tick}", _context.Self, _tickCount);

        foreach (var connection in _connections.Values)
        {
            _context.Send(connection.Pid, serverMessage);
        }
    }

    private void HandleStopping()
    {
        _tickCancellation?.Cancel();
    }
}
