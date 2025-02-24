using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Websocket.Client;

namespace WebSocketBoilerplate;

/// <summary>
/// A WebSocket client wrapper that handles DTO-based message exchange
/// </summary>
public class WsRequestClient
{
    private readonly WebsocketClient _client;
    public readonly List<BaseDto> ReceivedMessages = new();
    public readonly List<string> ReceivedMessagesAsJsonStrings = new();
    private readonly Assembly[] _assemblies;
    private static readonly JsonSerializerOptions DefaultSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    /// <summary>
    /// Initializes a new WebSocket client with DTO type resolution capabilities
    /// </summary>
    /// <param name="assemblies">Assemblies containing the DTO types. Must include assemblies for both request and response DTOs</param>
    /// <param name="url">WebSocket server URL. Defaults to ws://localhost:8181</param>
    public WsRequestClient(Assembly[] assemblies, string? url = "ws://localhost:8181")
    {
        _assemblies = assemblies.Length > 0 ? assemblies : new[] { Assembly.GetExecutingAssembly() };
        _client = new WebsocketClient(new Uri(url ?? "ws://localhost:8181"));

        _client.MessageReceived.Subscribe(HandleMessage);
    }

    private void HandleMessage(ResponseMessage msg)
    {
        try 
        {
            var baseDto = JsonSerializer.Deserialize<BaseDto>(msg.Text, DefaultSerializerOptions);
            if (baseDto == null) return;

            string eventType = baseDto.eventType.Replace("Dto", "", StringComparison.OrdinalIgnoreCase) + "Dto";

            var dtoType = _assemblies
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name.Equals(eventType, StringComparison.OrdinalIgnoreCase));

            if (dtoType == null) return;
            ReceivedMessagesAsJsonStrings.Add(msg.Text);
            var fullDto = JsonSerializer.Deserialize(msg.Text, dtoType, DefaultSerializerOptions) as BaseDto;
            if (fullDto != null)
            {
                lock (ReceivedMessages)
                {
                    ReceivedMessages.Add(fullDto);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    /// <summary>
    /// Establishes the WebSocket connection
    /// </summary>
    /// <returns>The current instance for method chaining</returns>
    /// <exception cref="Exception">Thrown when connection fails</exception>
    public async Task<WsRequestClient> ConnectAsync()
    {
        try
        {
            _client.ReconnectTimeout = null;
            await _client.Start();
            
            if (!_client.IsRunning)
                throw new Exception("WebSocket client failed to start");

            return this;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to connect: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sends a message without expecting a response
    /// </summary>
    /// <param name="dto">The DTO to send</param>
    /// <typeparam name="T">Type of the DTO, must inherit from BaseDto</typeparam>
    public Task SendMessage<T>(T dto) where T : BaseDto
    {
        _client.Send(JsonSerializer.Serialize(dto, DefaultSerializerOptions));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a message and waits for a response
    /// </summary>
    /// <param name="sendDto">The DTO to send</param>
    /// <param name="timeoutSeconds">Maximum time to wait for response</param>
    /// <typeparam name="T">Type of the request DTO</typeparam>
    /// <typeparam name="TR">Expected type of the response DTO</typeparam>
    /// <returns>The response DTO</returns>
    /// <exception cref="TimeoutException">Thrown when no response is received within the timeout period</exception>
    public async Task<TR> SendMessage<T, TR>(T sendDto, int timeoutSeconds = 7) 
        where T : BaseDto 
        where TR : BaseDto
    {
        sendDto.requestId ??= Guid.NewGuid().ToString();
        await SendMessage(sendDto);

        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(timeoutSeconds))
        {
            lock (ReceivedMessages)
            {
                var response = GetMessagesOfType<TR>()
                    .FirstOrDefault(msg => msg.requestId == sendDto.requestId);
                
                if (response != null)
                    return response;
            }
            await Task.Delay(100);
        }

        throw new TimeoutException(
            $"Did not receive expected response of type {typeof(TR).Name} " +
            $"with requestId {sendDto.requestId} within {timeoutSeconds} seconds");
    }

    public IEnumerable<T> GetMessagesOfType<T>() where T : BaseDto
    {
        lock (ReceivedMessages)
        {
            return ReceivedMessages
                .Where(msg => msg is T)
                .Cast<T>()
                .ToList();
        }
    }

    /// <summary>
    /// Disposes the WebSocket client
    /// </summary>
    public void Dispose()
    {
        _client.Dispose();
    }
}