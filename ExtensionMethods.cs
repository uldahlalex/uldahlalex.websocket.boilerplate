using System.Reflection;
using System.Text.Json;
using Fleck;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace lib;

public static class ExtensionMethods
{
    public static HashSet<Type> FindAndInjectClientEventHandlers(this WebApplicationBuilder builder, Assembly assemblyReference,
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

    public static async Task InvokeClientEventHandler(this WebApplication app, HashSet<Type> types,
        IWebSocketConnection ws, string message, ServiceLifetime? lifetime = ServiceLifetime.Singleton)
    {
        var dto = JsonSerializer.Deserialize<BaseDto>(message, new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new ArgumentException("Could not deserialize string: " + message + " to "+ nameof(BaseDto));
        var handlerType = types.FirstOrDefault(t => t.Name.Equals(dto.eventType, StringComparison.OrdinalIgnoreCase));
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
}