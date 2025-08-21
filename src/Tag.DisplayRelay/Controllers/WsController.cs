using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;

namespace Tag.DisplayRelay.Controllers;

[ApiController]
[Route("ws")]
public class WsController : ControllerBase
{
    private readonly GroupHub _hub;
    public WsController(GroupHub hub) => _hub = hub;

    [HttpGet]
    public async Task Get([FromQuery] string siteId, [FromQuery] string roomId)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = 400;
            await HttpContext.Response.WriteAsync("Expected WebSocket");
            return;
        }

        using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        var group = $"{siteId}:{roomId}";
        _hub.Add(group, socket);

        var buffer = new byte[4096];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, HttpContext.RequestAborted);
                if (result.CloseStatus.HasValue) break; // we ignore inbound messages in PoC
            }
        }
        finally
        {
            _hub.Remove(group, socket);
        }
    }
}
