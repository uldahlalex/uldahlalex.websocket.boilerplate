using Fleck;

namespace lib;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public abstract class BaseEventFilter : Attribute
{
    public abstract Task Handle<T>(IWebSocketConnection socket, T dto) where T : BaseDto;
}