using Fleck;
using WebSocketBoilerplate;

namespace ExerciseASolution.EventHandlers;

public class ClientWantsToSignInDto : BaseDto
{
    public string Username { get; set; }
    public string Password { get; set; }
    public string RequestId { get; set; }
}

public class ServerAuthenticatesClientDto : BaseDto
{
    public string RequestId { get; set; }
    public string Jwt { get; set; }
}

public class ClientWantsToSignInEventHandler
    : BaseEventHandler<ClientWantsToSignInDto>
{
    public override async Task Handle(ClientWantsToSignInDto dto, IWebSocketConnection socket)
    {
        socket.SendDto(new ServerAuthenticatesClientDto()
        {
            RequestId = dto.RequestId,
            Jwt = "imaginethisisajwt"
        });
    }
}