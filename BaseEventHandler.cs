using System.Reflection;
using System.Text.Json;
using Fleck;

namespace lib;

public abstract class BaseEventHandler<T> where T : BaseDto
{
    public string eventType => GetType().Name;

    public async Task InvokeHandle(string message, IWebSocketConnection socket) //todo cancellationtoken
    {
        var dto = JsonSerializer.Deserialize<T>(message, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new ArgumentException("Could not deserialize into " + typeof(T).Name + "from string: " + message);
        foreach (var baseEventFilterAttribute in GetType().GetCustomAttributes().OfType<BaseEventFilter>())
            await baseEventFilterAttribute.Handle(socket, dto);
        await Handle(dto, socket);
    }

    public abstract Task Handle(T dto, IWebSocketConnection socket);
}