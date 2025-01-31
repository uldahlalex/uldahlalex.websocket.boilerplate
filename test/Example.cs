using System.Reflection;
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