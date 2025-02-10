using System.Text.Json;
using Websocket.Client;

namespace WebSocketBoilerplate;

public class WsRequestClient
{
    public readonly WebsocketClient Client;
    public readonly List<BaseDto> ReceivedMessages = new();

    /// <summary>
    /// Defaults to ws://localhost:8181 if no other url string is specified
    /// </summary>
    /// <param name="url"></param>
    public WsRequestClient(string? url = "ws://localhost:8181")
    {

            Client = new WebsocketClient(new Uri(url ?? "ws://localhost:8181"));

        Client.MessageReceived.Subscribe(msg =>
        {
            var baseDto = JsonSerializer.Deserialize<BaseDto>(msg.Text);
            if (baseDto == null) return;

            var typeName = baseDto.eventType + (baseDto.eventType.EndsWith("Dto") ? "" : "Dto");
            var fullTypeName = $"WebSocketBoilerplate.{typeName}";
            
            var dtoType = Type.GetType(fullTypeName) ?? typeof(BaseDto);
            var fullDto = JsonSerializer.Deserialize(msg.Text, dtoType) as BaseDto;
            
            lock (ReceivedMessages)
            {
                ReceivedMessages.Add(fullDto);
            }
        });
    }

    public async Task<WsRequestClient> ConnectAsync()
    {
        await Client.Start();
        if (!Client.IsRunning) throw new Exception("Could not start client!");
        return this;
    }
    
    private void Send<T>(T dto) where T : BaseDto
    {
        var serialized = JsonSerializer.Serialize(dto, new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        Client.Send(serialized);
    }

    /// <summary>
    /// For sending a message without expecing a response
    /// </summary>
    /// <param name="sendDto"></param>
    /// <typeparam name="T"></typeparam>
    public async Task SendMessage<T>(T sendDto) where T : BaseDto
    {
        Send(sendDto);
    }

    /// <summary>
    /// When a response is expected
    /// </summary>
    /// <param name="sendDto"></param>
    /// <param name="timeoutSeconds">Defaults to 7 seconds. Supply int param to change</param>
    /// <typeparam name="T">The sending DTO type</typeparam>
    /// <typeparam name="TR">The responding DTO type</typeparam>
    /// <returns></returns>
    /// <exception cref="TimeoutException"></exception>
    public async Task<TR> SendMessage<T, TR>(T sendDto, int timeoutSeconds = 7) 
        where T : BaseDto 
        where TR : BaseDto
    {
        if (string.IsNullOrEmpty(sendDto.requestId))
        {
            sendDto.requestId = Guid.NewGuid().ToString();
        }
        
        Send(sendDto);

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

        throw new TimeoutException($"Did not receive expected response of type {typeof(TR).Name} with requestId {sendDto.requestId} within {timeoutSeconds} seconds");
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
}