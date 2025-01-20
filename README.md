# Quickstart:

Add the nuget package to your existing .NET web project

```bash
dotnet add package uldahlalex.websocket.boilerplate
```

Link to Nuget gallery: https://www.nuget.org/packages/uldahlalex.websocket.boilerplate

## Usage of event handler: Hello world (basic echo)


```cs
// ./test/Example.cs

﻿using System.Reflection;
using Fleck;
using WebSocketBoilerplate;

namespace test;

public static class Example
{
    public static void Main()
    {
        var builder = WebApplication.CreateBuilder();
        builder.CreateWebSocketApi();

        var app = builder.Build();
        
        app.StartWebSocketApi();
        
        app.Run();
    }
    
    public static WebApplicationBuilder CreateWebSocketApi(this WebApplicationBuilder builder)
    {
        builder.Services.InjectEventHandlers(Assembly.GetExecutingAssembly()); //Injects all event handler services
        
        return builder;
    }
    
    public static WebApplication StartWebSocketApi(this WebApplication app)
    {
        var server = new WebSocketServer("ws://0.0.0.0:8181");
        server.Start(socket =>
        {
            socket.OnOpen = () => Console.WriteLine("Open!");
            socket.OnClose = () => Console.WriteLine("Close!");
            socket.OnMessage = async message =>
            {
                try
                {
                    //STEP 2: ADD THIS LINE TO INVOKE THE EVENT HANDLER WHEN RECEIVING A MESSAGE
                    await app.CallEventHandler(socket, message); 
                }
                catch (Exception e)
                {
                    // trigger some global error handler to not ruin your life
                }

            };
        });
        return app;
    } 
}

public class ClientWantsToEchoDto : BaseDto
{
    public string message { get; set; }
}

public class ServerSendsEchoDto : BaseDto
{
    public string message { get; set; }
}

public class ClientWantsToEchoEventHandler : BaseEventHandler<ClientWantsToEchoDto>
{
    public override Task Handle(ClientWantsToEchoDto dto, IWebSocketConnection socket)
    {
        var echoDto = new ServerSendsEchoDto
        {
            message = dto.message
        };
        socket.SendDto(echoDto);
        return Task.CompletedTask;
    }
}
```

## Usage event filter. Example: Validate Data Annotations for DTO
#### Don't forget to implement an exception handler to catch the ValidationException thrown by Validator.ValidateObject()
EventFilters simply run before the BaseEventHandler's "Handle()"
```csharp
using System.ComponentModel.DataAnnotations;
using Fleck;
using lib;

namespace Api.ClientEventFilters;

//Simply extend BaseEventFilter and override the Handle<T> method
public class ValidateDataAnnotations : BaseEventFilter
{
    public override Task Handle<T>(IWebSocketConnection socket, T dto)
    {
        var validationContext = new ValidationContext(
            dto ?? throw new ArgumentNullException(nameof(dto)));
        Validator.ValidateObject(dto, validationContext, true);
        return Task.CompletedTask;
    }
}
//Use the annotation by adding it to the event handler class
[ValidateDataAnnotations]
public class ClientWantsToEnterRoom(
    ChatRepository chatRepository) : BaseEventHandler<ClientWantsToEnterRoomDto>
{
    public override Task Handle(ClientWantsToEnterRoomDto dto, IWebSocketConnection socket)
    {
        throw new NotImplementedException();
    }
}
```

# Basic usage of WebSocketTestClient
```csharp
[TestFixture]
public class Tests
{
    [Test]
    public async Task MyTest()
    {
        //Initialize the WebSocketTestClient and connect to the server (default URL = ws://localhost:8181)
        var ws = await new WebSocketTestClient().ConnectAsync();
        
        //Send an object extending BaseDto to the server without asserting and waiting
        await ws.DoAndAssert(new ClientWantsToEchoDto() {message = "hey"});
   
        //Send an object extending BaseDto to the server and wait for assertions to be true. If not, exception is thrown
        await ws.DoAndAssert(new ClientWantsToEchoDto() { message = "hey"}, 
            receivedMessages => receivedMessages.Count == 2);
    }
```
