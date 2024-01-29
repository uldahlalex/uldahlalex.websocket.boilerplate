using System.Reflection;
using System.Text.Json;
using Fleck;
using lib;

public abstract class BaseEventHandler<T> where T : BaseDto
{
    public string eventType => GetType().Name;

    public async Task InvokeHandle(string message, IWebSocketConnection socket) //todo cancellationtoken
    {

        var dto = JsonSerializer.Deserialize<T>(message, new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        });
        await Handle(dto, socket);
    }

    public abstract Task Handle(T dto, IWebSocketConnection socket);
}