# Legendary Play Backend Test

Your task in this test assignment is to write a simple chat server using a gRPC API.

## Overview
In this solution folder, you will find two projects: ChatClient and ChatServer.

The ChatClient is a command line tool that allows you to connect to a ChatServer instance,
select a username and chat room, and exchange messages with other users that have joined
the same room.

The ChatServer project contains the protobuf definitions for the gRPC API that the 
client and server communicate with. It also contains a very basic implementation of an 
ASP.Net server that serves this gRPC API. 

Your task is to implement the ChatService in the ChatServer project, which implements 
the gRPC API. You can see the full API definition in `ChatServer/protos/server.proto`.
You can read more about gRPC and protobuf here:
 - Protobuf language spec: https://developers.google.com/protocol-buffers/docs/proto3
 - Google (C# specific): https://developers.google.com/protocol-buffers/docs/csharptutorial
 - MSDN: https://docs.microsoft.com/en-us/aspnet/core/grpc/protobuf?view=aspnetcore-6.0

## Before You Start 

Please verify that you can actually run both the server and client and connect successfully.
Even in the current state, the client should be able to establish a connection and the 
server should echo the message type back to the client. 

Build both projects. Then, if you're on Windows, you can start the server with ChatServer.exe
and the Client with ChatClient.exe. The server should listen to port 5219.

If you're on a Mac, the server may not work out of the box. You can instead build and run the 
Docker image, in which case you will need to connect the client to the port that Docker maps it to.


A successful session looks like this: 
```
Url: http://localhost:5219
TeamId: Legendary
CharacterId: Alice
Connecting...
Logging in...
Type a chat message. Press Enter without a message to quit.
[SYSTEM] Received Login
Hello
[SYSTEM] Received Chat
```

## Requirement 1: Chat Messages

The communication flows through a single bidirectional gRPC channel called Communicate.

The first message that a client sends must be ClientMessageLogin, which tells the server 
which username and chat room to associate with this client. 
The server will always reply with ServerMessageLoginSuccess.

Once a client is logged in, it is allowed to send ClientMessageChat messages through 
the channel. The server will then send a ServerMessageChat message to every client in 
the same room, except for the one who sent it. 

Example: 
 - Alice, Charlie and Bob are in room Legendary
 - Dave is in room Play 
 - Alice sends a ClientMessageChat with text = "Hello"
 - The server sends a ServerMessageChat with text = "Hello" and username = "Alice" to Charlie and Bob
 - The server does NOT send the message to Alice and Dave 

## Requirement 2: Automated Messages

Additionally, the server sends an automated message every 30 seconds to all members of
a room, telling them how old this room currently is. This message is of type 
ServerMessageTick and its `tick` property is the number of ticks since this room was created.
Once the last person leaves the room, it should be destroyed. When a new room with the same 
name is created, its timer should start at 0 again.

Example:
 - 0:00 Alice is the first person to join Legendary
 - 0:30 The server sends ServerMessageTick with tick = 1
 - 0:40 Bob joins Legendary
 - 0:45 Alice leaves Legendary
 - 1:00 The server sends ServerMessageTick with tick = 2
 - 1:10 Bob leaves Legendary, the room is empty now and thus destroyed
 - 1:20 Alice joins Legendary again 
 - 1:50 The server sends ServerMessageTick with tick = 1
 
 
## Additional Info 

You do not need to use a database or persist data in any way. The chat rooms and all data in the 
server are meant to be transient and lost on server restart.

Please do not modify the gRPC / protobuf definitions or the client implementation.

You do not have to worry about malicious clients. You only need to make the chat server work 
with the provided chat client.

Try to avoid memory leaks and concurrency related bugs. If I run 100 clients that login and spam 
messages all at the same time, the server should still behave correctly.