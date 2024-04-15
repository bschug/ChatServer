using Proto;
using Log = Serilog.Log;

namespace ChatServer.Actors;

/// <summary>
/// This actor knows about all active chat rooms and can map between their names (ChatRoomId) and PIDs.
/// Sessions interact with this actor on start to get or create the right room.
/// </summary>
public class ChatRoomSupervisor : IActor
{
    public record GetOrCreateRoomRequest(string ChatRoomId);

    private IContext _context = null!; // This is initialized at the start of each request

    private readonly Dictionary<string, PID> _nameToPid = new();
    private readonly Dictionary<PID, string> _pidToName = new();

    public Task ReceiveAsync(IContext context)
    {
        _context = context;

        switch (context.Message)
        {
            case GetOrCreateRoomRequest getOrCreateRoomRequest:
            {
                try
                {
                    var pid = GetOrCreateRoom(getOrCreateRoomRequest);
                    _context.Respond(pid);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "GetOrCreateRoom failed");
                }
                break;
            }

            case Terminated terminated:
                HandleTerminated(terminated);
                break;

            case Stopping:
            case Restarting:
                Log.Error("Supervisor is {Action}", context.Message.GetType().Name);
                break;
        }

        return Task.CompletedTask;
    }

    private PID GetOrCreateRoom(GetOrCreateRoomRequest request)
    {
        if (_nameToPid.TryGetValue(request.ChatRoomId, out var pid))
        {
            return pid;
        }

        var props = Props.FromProducer(() => new ChatRoomActor());
        var newPid = _context.SpawnNamed(props, request.ChatRoomId);
        _nameToPid[request.ChatRoomId] = newPid;
        _pidToName[newPid] = request.ChatRoomId;
        return newPid;
    }

    private void HandleTerminated(Terminated terminated)
    {
        if (_pidToName.Remove(terminated.Who, out var name))
        {
            _nameToPid.Remove(name);
            Log.Information("Chat room {ChatRoomId} terminated", name);
        }
    }
}
