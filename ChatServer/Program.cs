using ChatServer.Services;
using Proto;
using Serilog;
using Log = Serilog.Log;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Services.AddGrpc();
builder.Services.AddSingleton<ChatService>();
builder.Services.AddSingleton<ActorSystem>();

var app = builder.Build();

app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapGrpcService<ChatService>();
});

app.Run();
