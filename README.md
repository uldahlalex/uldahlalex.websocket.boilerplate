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
    public static List<IWebSocketConnection> ClientConnections { get; set; } = new List<IWebSocketConnection>() { };

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
        var logger = LoggerFactory.Create(conf => { })
            .CreateLogger("Example logger");
        var server = new WebSocketServer("ws://0.0.0.0:8181");
        server.Start(socket =>
        {
            socket.OnOpen = () => ClientConnections.Add(socket);
            socket.OnClose = () => ClientConnections = ClientConnections.Where(c => c.ConnectionInfo.Id != socket.ConnectionInfo.Id).ToList();
            socket.OnMessage = message =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await app.CallEventHandler(socket, message);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Error handling message: {Error}", e.Message);

                        socket.SendDto(new ServerSendsErrorMessageDto { Error = e.Message });
                    }
                });
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
public class ServerSendsErrorMessageDto : BaseDto
{
    public string Error { get; set; }
}
```


# WebSocket Request Client

A WebSocket client wrapper that handles DTO-based message exchange. Designed for type-safe communication with WebSocket servers that follow a request/response pattern using DTOs.

## Features
- Type-safe message handling
- Automatic DTO type resolution from provided assemblies
- Built-in request/response correlation using requestId
- Support for fire-and-forget and request-response patterns
- Configurable timeout handling
- Thread-safe message processing

## Usage

### Initialization
```csharp
// Initialize with assemblies containing your DTOs
var client = new WsRequestClient(
    new[] { 
        typeof(RequestDto).Assembly,  // Assembly containing request DTOs. If there are dto's in other assemblies add these aswell
    }
);

// Optional: Specify custom WebSocket URL (defaults to ws://localhost:8181)
var client = new WsRequestClient(
    assemblies: new[] { typeof(RequestDto).Assembly },
    url: "ws://custom-server:1234"
);
```

### Connection
```csharp
await client.ConnectAsync();
```

### Fire-and-Forget Messages
```csharp
var notification = new NotificationDto //this class must extend BaseDto
{ 
    eventType = "Notification",
    message = "Hello!" 
};
await client.SendMessage(notification);
```

### Request-Response Pattern
```csharp
// Send request and await response
var request = new EchoRequestDto
{ 
    message = "Hello server!" 
};
var response = await client.SendMessage<EchoRequestDto, EchoResponseDto>(request); //the generics must extend BaseDto

// With custom timeout
var response = await client.SendMessage<EchoRequestDto, EchoResponseDto>(
    request, 
    timeoutSeconds: 15
);
```

## Testing Example with xUnit

```csharp
public class WebSocketTests : IAsyncLifetime
{
    private WsRequestClient _client;

    public WebSocketTests()
    {
        _client = new WsRequestClient(new[] 
        { 
            typeof(EchoRequestDto).Assembly 
        });
    }

    public async Task InitializeAsync()
    {
        await _client.ConnectAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
    }

    [Fact]
    public async Task Echo_ShouldReturnSameMessage()
    {
        // Arrange
        var request = new EchoRequestDto
        {
            message = "Test message"
        };

        // Act
        var response = await _client.SendMessage<EchoRequestDto, EchoResponseDto>(request);

        // Assert
        Assert.Equal(request.message, response.message);
        Assert.Equal(request.requestId, response.requestId);
    }
}
```







### Legacy version of event handlers: (before 2.0 this was preferred syntax):

```cs
using System.Reflection;
using Fleck;
using WebSocketBoilerplate; //previously the namespace was also called "lib" (before version 2)

var builder = WebApplication.CreateBuilder(args);

// STEP 1: ADD THIS LINE TO FIND CLASSES EXTENDING BaseEventHandler<Dto>
var services = builder.FindAndInjectClientEventHandlers(Assembly.GetExecutingAssembly()); //There also exists an extension method for IServiceCollection and not just WebApplicationBuilder

var app = builder.Build();
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
            await app.InvokeClientEventHandler(services, socket, message); //There is also an extension method for IApplicationBuilder so you don't have to use WebApplication type
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

//STEP 3: ADD EVENTS BY EXTENDING BaseEventHandler<T> WHERE T IS YOUR DEFINED DTO EXTENDING BaseDto
public class ClientWantsToEcho : BaseEventHandler<ClientWantsToEchoDto>
{
    //STEP 4: IMPLEMENT THE Handle(dto, socket) METHOD DEFINED BY BaseEventHandler
    public override Task Handle(ClientWantsToEchoDto dto, IWebSocketConnection socket)
    {
        // Step 5: profit
        socket.Send(dto.message);
        socket.SendDto(new ServerSendsEchoDto() { message = dto.message}); //or using the SendDto which enforces extensions of BaseDto
        return Task.CompletedTask;
        
    }
}

public class ServerSendsEchoDto : BaseDto 
{
    public string message { get; set; }

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

# Basic usage of WebSocketTestClient (Legacy)
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
