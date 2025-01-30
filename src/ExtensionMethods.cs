using System.Reflection;
using System.Text.Json;
using Fleck;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace WebSocketBoilerplate;

public static class ExtensionMethods
{
    /// <summary>
    /// Legacy method to inject all event handlers in the assemblyReference.
    /// Modern interpertation is to use InjectEventHandlers (using IServiceCollection instead of WebApplicationBuilder!)
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="assemblyReference"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    public static HashSet<Type> FindAndInjectClientEventHandlers(
        this WebApplicationBuilder builder,
        Assembly assemblyReference,
        ServiceLifetime? lifetime = ServiceLifetime.Singleton)
    {
        var clientEventHandlers = new HashSet<Type>();
        foreach (var type in assemblyReference.GetTypes())
            if (type.BaseType != null &&
                type.BaseType.IsGenericType &&
                type.BaseType.GetGenericTypeDefinition() == typeof(BaseEventHandler<>))
            {
                if (lifetime.Equals(ServiceLifetime.Singleton))
                    builder.Services.AddSingleton(type);
                else if (lifetime.Equals(ServiceLifetime.Scoped))
                    builder.Services.AddScoped(type);
                clientEventHandlers.Add(type);
            }

        return clientEventHandlers;
    }

    /// <summary>
    /// Use this method to inject all event handlers in the assemblyReference
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assemblyReference"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    public static IServiceCollection InjectEventHandlers(
        this IServiceCollection services,
        Assembly assemblyReference,
        ServiceLifetime? lifetime = ServiceLifetime.Scoped)
    {
        var clientEventHandlers = new HashSet<Type>();
        foreach (var type in assemblyReference.GetTypes())
            if (type.BaseType != null &&
                type.BaseType.IsGenericType &&
                type.BaseType.GetGenericTypeDefinition() == typeof(BaseEventHandler<>))
            {
                if (lifetime.Equals(ServiceLifetime.Singleton))
                    services.AddSingleton(type);
                if (lifetime.Equals(ServiceLifetime.Transient))
                    services.AddTransient(type);
                else if (lifetime.Equals(ServiceLifetime.Scoped))
                    services.AddScoped(type);
                clientEventHandlers.Add(type);
            }

        var eventHandlersService = new EventHandlersService() { EventHandlers = clientEventHandlers };
        services.AddSingleton<IEventHandlersService>(eventHandlersService);

        return services;
    }


    /// <summary>
    /// Legacy method to invoke event handler based on "eventType".
    /// Deliberately left for backwards compatibility. Modern interpertation is to use CallEventHandler.
    /// (Deliberately uses duplicated code to avoid breaking changes)
    /// </summary>
    /// <param name="app"></param>
    /// <param name="types"></param>
    /// <param name="ws"></param>
    /// <param name="message"></param>
    /// <param name="lifetime"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public static async Task InvokeClientEventHandler(this WebApplication app, HashSet<Type> types,
        IWebSocketConnection ws, string message, ServiceLifetime? lifetime = ServiceLifetime.Singleton)
    {
        var dto = JsonSerializer.Deserialize<BaseDto>(message, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new ArgumentException("Could not deserialize string: " + message + " to " + nameof(BaseDto));

        var eventType = dto.eventType.EndsWith("Dto", StringComparison.OrdinalIgnoreCase)
            ? dto.eventType.Substring(0, dto.eventType.Length - 3)
            : dto.eventType;

        var handlerType = types.FirstOrDefault(t => t.Name.Equals(eventType, StringComparison.OrdinalIgnoreCase) ||
                                                    t.Name.Equals(eventType + "Dto",
                                                        StringComparison.OrdinalIgnoreCase));

        if (handlerType == null)
        {
            var dtoTypeName = dto.GetType().Name;
            handlerType = types.FirstOrDefault(t =>
                t.BaseType != null &&
                t.BaseType.IsGenericType &&
                t.BaseType.GetGenericTypeDefinition() == typeof(BaseEventHandler<>) &&
                (t.BaseType.GetGenericArguments()[0].Name.Equals(eventType, StringComparison.OrdinalIgnoreCase) ||
                 t.BaseType.GetGenericArguments()[0].Name
                     .Equals(eventType + "Dto", StringComparison.OrdinalIgnoreCase)));
        }

        if (handlerType == null)
            throw new InvalidOperationException($"Could not find handler for DTO type: {dto.eventType}");

        if (lifetime.Equals(ServiceLifetime.Scoped))
        {
            using (var scope = app.Services.CreateScope())
            {
                var scopedServiceProvider = scope.ServiceProvider;
                dynamic clientEventServiceClass =
                    scopedServiceProvider.GetService(handlerType)!;
                if (clientEventServiceClass == null)
                    throw new InvalidOperationException($"Could not resolve service for type: {dto.eventType}");
                await clientEventServiceClass.InvokeHandle(message, ws);
            }
        }
        else
        {
            dynamic clientEventServiceClass = app.Services.GetService(handlerType)!;
            if (clientEventServiceClass == null)
                throw new InvalidOperationException($"Could not resolve service for type: {handlerType}");

            await clientEventServiceClass.InvokeHandle(message, ws);
        }
    }

    /// <summary>
    /// Use this method to call the event handler when receiving a message from the client containing "eventType"
    /// </summary>
    /// <param name="app"></param>
    /// <param name="ws"></param>
    /// <param name="message"></param>
    /// <param name="lifetime"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public static async Task CallEventHandler(this IApplicationBuilder app,
        IWebSocketConnection ws, string message, ServiceLifetime? lifetime = ServiceLifetime.Scoped)
    {
        var dto = JsonSerializer.Deserialize<BaseDto>(message, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new ArgumentException("Could not deserialize string: " + message + " to " + nameof(BaseDto));

        var eventType = dto.eventType.EndsWith("Dto", StringComparison.OrdinalIgnoreCase)
            ? dto.eventType.Substring(0, dto.eventType.Length - 3)
            : dto.eventType;

        var eventHandlersService = app.ApplicationServices.GetRequiredService<IEventHandlersService>();
        var handlerType = eventHandlersService.EventHandlers.FirstOrDefault(t =>
            t.Name.Equals(eventType, StringComparison.OrdinalIgnoreCase) ||
            t.Name.Equals(eventType + "Dto",
                StringComparison.OrdinalIgnoreCase));

        if (handlerType == null)
        {
            handlerType = eventHandlersService.EventHandlers.FirstOrDefault(t =>
                t.BaseType != null &&
                t.BaseType.IsGenericType &&
                t.BaseType.GetGenericTypeDefinition() == typeof(BaseEventHandler<>) &&
                (t.BaseType.GetGenericArguments()[0].Name.Equals(eventType, StringComparison.OrdinalIgnoreCase) ||
                 t.BaseType.GetGenericArguments()[0].Name
                     .Equals(eventType + "Dto", StringComparison.OrdinalIgnoreCase)));
        }

        if (handlerType == null)
            throw new InvalidOperationException($"Could not find handler for DTO type: {dto.eventType}");

        if (lifetime.Equals(ServiceLifetime.Scoped))
        {
            using (var scope = app.ApplicationServices.CreateScope())
            {
                var scopedServiceProvider = scope.ServiceProvider;
                dynamic clientEventServiceClass =
                    scopedServiceProvider.GetService(handlerType)!;
                if (clientEventServiceClass == null)
                    throw new InvalidOperationException($"Could not resolve service for type: {dto.eventType}");
                await clientEventServiceClass.InvokeHandle(message, ws);
            }
        }
        else
        {
            dynamic clientEventServiceClass = app.ApplicationServices.GetService(handlerType)!;
            if (clientEventServiceClass == null)
                throw new InvalidOperationException($"Could not resolve service for type: {handlerType}");

            await clientEventServiceClass.InvokeHandle(message, ws);
        }
    }

    /// <summary>
    /// Serializes to JSON and camelcases json keys before sending the message
    /// </summary>
    /// <param name="websocketConnection"></param>
    /// <param name="dto"></param>
    /// <param name="enforceCamelCase"></param>
    /// <typeparam name="T"></typeparam>
    public static void SendDto<T>(this IWebSocketConnection websocketConnection, T dto, bool enforceCamelCase = false)
        where T : BaseDto
    {
        var serializerOptions = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase //almost every web framework assumes the json keys are camelcased
        };
        websocketConnection.Send(JsonSerializer.Serialize(dto, serializerOptions));
    }
}