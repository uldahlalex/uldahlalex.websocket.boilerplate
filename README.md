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



## WebSocket Request Client

A simple WebSocket client for testing WebSocket APIs, with support for typed DTOs and request/response patterns.

## Features
- Automatic request/response correlation using requestId
- Type-safe message sending and receiving
- Support for both fire-and-forget messages and request-response patterns
- Built-in timeout handling
- Thread-safe message collection

## Installation

Is automatically bundled in the same package as everything else.

## Basic Usage

### Connect to WebSocket Server
```csharp
// Defaults to ws://localhost:8181
var client = new WsRequestClient();
await client.ConnectAsync();

// Or specify custom URL
var client = new WsRequestClient("ws://localhost:1234");
await client.ConnectAsync();
```

### Send Message Without Response
```csharp
await client.SendMessage(new BroadcastMessageDto //BroadcastMessageDto extends BaseDto 
{ 
    message = "Hello everyone!" 
});
```

### Send Message With Expected Response
```csharp
var response = await client.SendMessage<LoginRequestDto, LoginResponseDto>( //Both generics extending BaseDto
    new LoginRequestDto 
    { 
        username = "test",
        password = "password123"
    }
);
```

### Custom Timeout
```csharp
// Wait up to 15 seconds for response
var response = await client.SendMessage<LoginRequestDto, LoginResponseDto>( //Both generics extending BaseDto
    new LoginRequestDto { username = "test" },
    timeoutSeconds: 15
);
```

## Testing Examples

Here's how to use the client in xUnit tests:

```csharp
public class WebSocketTests : IAsyncLifetime
{
    private WsRequestClient _client;

    public WebSocketTests()
    {
        _client = new WsRequestClient();
    }

    public async Task InitializeAsync()
    {
        await _client.ConnectAsync();
    }

    public async Task DisposeAsync()
    {
        await _client.Client.Stop();
        _client.Client.Dispose();
    }

    [Fact]
    public async Task LoginRequest_ShouldReceiveSuccessResponse()
    {
        // Arrange
        var loginRequest = new LoginRequestDto
        {
            username = "testUser",
            password = "testPass"
        };

        // Act
        var response = await _client.SendMessage<LoginRequestDto, LoginResponseDto>(loginRequest);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(loginRequest.requestId, response.requestId);
        Assert.True(response.success);
    }

    [Fact]
    public async Task BroadcastMessage_ShouldBeReceivedByOtherClients()
    {
        // Arrange
        var broadcastMessage = new BroadcastMessageDto
        {
            message = "Test broadcast"
        };

        // Act
        await _client.SendMessage(broadcastMessage);
        await Task.Delay(1000); // Allow time for message processing

        // Assert
        var receivedMessages = _client.GetMessagesOfType<BroadcastMessageDto>();
        Assert.Contains(receivedMessages, msg => msg.message == "Test broadcast");
    }

    [Fact]
    public async Task InvalidRequest_ShouldTimeoutAfterSpecifiedDuration()
    {
        // Arrange
        var invalidRequest = new InvalidRequestDto();

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(() =>
            _client.SendMessage<InvalidRequestDto, ResponseDto>(
                invalidRequest,
                timeoutSeconds: 2
            )
        );
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
