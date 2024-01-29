# Quickstart:

Add the nuget package to your existing .NET web project

```bash
dotnet add package uldahlalex.websocket.boilerplate
```

Link to Nuget gallery: https://www.nuget.org/packages/uldahlalex.websocket.boilerplate

## Usage: Hello world with Fleck

```csharp

using System.Reflection;
using Fleck;
using lib;

var builder = WebApplication.CreateBuilder(args);

// STEP 1: ADD THIS LINE
var services = builder.FindAndInjectClientEventHandlers(Assembly.GetExecutingAssembly());

var app = builder.Build();
var server = new WebSocketServer("ws://0.0.0.0:8181");
server.Start(socket =>
{
    socket.OnOpen = () => Console.WriteLine("Open!");
    socket.OnClose = () => Console.WriteLine("Close!");
    socket.OnMessage = message =>
    {
        try
        {
            //STEP 2: ADD THIS LINE TO INVOKE THE EVENT HANDLER
            app.InvokeClientEventHandler(services, socket, message);
        }
        catch (Exception e)
        {
            // trigger some global error handler to not ruin your life
        }

    };
});
Console.ReadLine();

public class ClientWantsToEchoDto : BaseDto
{
    public string message { get; set; }
}

//STEP 3: ADD EVENTS BY EXTENDING BaseEventHandler<T> WHERE T IS YOUR DEFINED DTO
public class ClientWantsToEcho : BaseEventHandler<ClientWantsToEchoDto>
{
    //STEP 4: IMPLEMENT THE Handle(dto, socket) METHOD DEFINED BY BASEEVENTHANDLER
    public override Task Handle(ClientWantsToEchoDto dto, IWebSocketConnection socket)
    {
        // Step 5: profit
        socket.Send("hey");
        return Task.CompletedTask;
    }
}

```
