using ChatServer.Actors;
using Grpc.Core;
using Proto;
using Log = Serilog.Log;

namespace ChatServer.Services
{
    /// <summary>
    /// Accepts GRPC connections from clients and forwards them to their respective chat room.
    /// </summary>
    public class ChatService : ChatServer.ChatServerBase
    {
        public class ConnectionLostException : Exception { }

        private ActorSystem _actorSystem;
        private PID _supervisorPid;

        public ChatService(ActorSystem actorSystem)
        {
            _actorSystem = actorSystem;

            var props = Props.FromProducer(() => new ChatRoomSupervisor());
            _supervisorPid = _actorSystem.Root.SpawnNamed(props, "ChatRooms");
        }

        /// <summary>
        /// This method is called by gRPC whenever a client establishes a connection.
        /// Each connected client will have its own call to this, with its own requestStream, responseStream and context.
        /// </summary>
        /// <param name="requestStream">You read from this stream to receive messages from the client.</param>
        /// <param name="responseStream">You write to this stream to send messages to the client.</param>
        /// <param name="context">Metadata, including CancellationToken, see https://docs.microsoft.com/en-us/aspnet/core/grpc/services?view=aspnetcore-6.0</param>
        /// <returns></returns>
        public override async Task Communicate(
            IAsyncStreamReader<ClientMessage> requestStream,
            IServerStreamWriter<ServerMessage> responseStream,
            ServerCallContext context)
        {
            var loginMessage = await WaitForLoginMessage(requestStream);
            var chatRoomId = loginMessage.ChatRoomId;
            var userName = loginMessage.UserName;

            var roomPid = await GetOrCreateRoom(chatRoomId);
            Log.Debug("{UserName} joining chat room {ChatRoomId} at {RoomPid}", userName, chatRoomId, roomPid);

            var joinRequest = new ChatRoomActor.JoinRequest(userName, responseStream);
            var clientPid = await _actorSystem.Root.RequestAsync<PID>(roomPid, joinRequest);
            Log.Debug("{UserName} PID is {Pid}", userName, clientPid);

            try
            {
                await foreach (var clientMessage in requestStream.ReadAllAsync())
                {
                    var chatMessage = new ChatRoomActor.ChatMessage(clientMessage.Chat.Text, clientPid);
                    _actorSystem.Root.Send(roomPid, chatMessage);
                }
            }
            catch (IOException)
            {
            }
            finally
            {
                Log.Information("{UserName} has disconnected", userName);
                await _actorSystem.Root.PoisonAsync(clientPid);
            }
        }

        private async Task<PID> GetOrCreateRoom(string chatRoomId)
        {
            var request = new ChatRoomSupervisor.GetOrCreateRoomRequest(chatRoomId);
            var response = await _actorSystem.Root.RequestAsync<PID>(_supervisorPid, request);
            return response;
        }

        async Task<ClientMessageLogin> WaitForLoginMessage(IAsyncStreamReader<ClientMessage> requestStream)
        {
            try
            {
                bool didMove = await requestStream.MoveNext();

                if (didMove == false)
                {
                    throw new ConnectionLostException();
                }

                if (requestStream.Current.ContentCase != ClientMessage.ContentOneofCase.Login)
                {
                    throw new ApplicationException("Expected Login message");
                }

                return requestStream.Current.Login;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                throw new TimeoutException();
            }
        }
    }
}
