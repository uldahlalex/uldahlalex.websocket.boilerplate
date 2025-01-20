using System.Reflection;
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