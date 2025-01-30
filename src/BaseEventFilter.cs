using Fleck;

namespace WebSocketBoilerplate;

public interface IBaseEventFilter
{
    Task Handle<T>(IWebSocketConnection socket, T dto) where T : BaseDto;

}

/// <summary>
/// Use this attribute to trigger certain events before the event handler is called. (pre-action)
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public abstract class BaseEventFilter : Attribute, IBaseEventFilter
{
    public abstract Task Handle<T>(IWebSocketConnection socket, T dto) where T : BaseDto;
}