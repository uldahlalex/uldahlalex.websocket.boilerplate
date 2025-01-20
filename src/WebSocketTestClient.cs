using System.Text.Json;
using Websocket.Client;

namespace WebSocketBoilerplate;

public class WebSocketTestClient
{
    public readonly WebsocketClient Client;
    public readonly List<BaseDto> ReceivedMessages = new();

    /// <summary>
    ///     Defaults to ws://localhost:{PORT} where PORT defaults to the env variable FULLSTACK_API_PORT or 8181 if env is null
    /// </summary>
    /// <param name="url, optional, like ws://localhost:1234"></param>
    public WebSocketTestClient(string? url = null)
    {
        if (url != null)
        {
            Client = new WebsocketClient(new Uri(url));
        }
        else
        {
            var port = Environment.GetEnvironmentVariable("FULLSTACK_API_PORT") ?? "8181";
            Client = new WebsocketClient(new Uri("ws://localhost:" + port));
        }

        Client.MessageReceived.Subscribe(msg =>
        {
            var dto = JsonSerializer.Deserialize<BaseDto>(msg.Text); // Adjust deserialization as needed
            lock (ReceivedMessages)
            {
                ReceivedMessages.Add(dto);
            }
        });
    }

    public async Task<WebSocketTestClient> ConnectAsync()
    {
        await Client.Start();
        if (!Client.IsRunning) throw new Exception("Could not start client!");
        return this;
    }

    public void Send<T>(T dto) where T : BaseDto
    {
        var serialized = JsonSerializer.Serialize(dto);
        Client.Send(serialized);
    }

    public async Task DoAndAssert<T>(T? action = null, Func<List<BaseDto>, bool>? condition = null) where T : BaseDto
    {
        if (action != null)
            Send(action);

        if (condition == null)
            return;
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(5))
        {
            lock (ReceivedMessages)
            {
                if (condition(ReceivedMessages)) return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("Condition not met: ");
    }
}