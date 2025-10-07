using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Demo1.Services;

public static class MediaStreamHandler
{
    private record BaseMsg(string @event);

    public static async Task HandleAsync(HttpContext ctx)
    {
        // Check if the request is a WebSocket request
        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsync("WebSocket request expected");
            return;
        }

        // Accept and open the WebSocket connection
        using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        Console.WriteLine("WebSocket connection established");

        // Buffer for receiving data(default size for WebSockets)
        var buffer = new byte[64 * 1024];

        var sb = new StringBuilder();

        try
        {
            // Main loop to receive messages from the Twilio Media Stream
            while (ws.State == WebSocketState.Open)
            {
                // Receive a message
                var res = await ws.ReceiveAsync(buffer, CancellationToken.None);

                // Close connection if requested by client
                if (res.MessageType == WebSocketMessageType.Close)
                {
                   Console.WriteLine("WebSocket connection closed by client");
                   break;
                }

                // Twillo sends data in JSON fragments:
                //{"event":"start"}
                //{ "event":"media","media":{ "payload":"..."} }
                //{ "event":"stop"}
                if (res.MessageType == WebSocketMessageType.Text)
                {
                    // Taking data from buffer and appending to StringBuilder
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, res.Count));
                    if (!res.EndOfMessage)
                       continue;

                    var json = sb.ToString();
                    sb.Clear();

                    // Parse the JSON to determine the event type
                    var kind = JsonSerializer.Deserialize<BaseMsg>(json)?.@event ?? "(none)";

                    if (kind == "start")
                        Console.WriteLine("[Twilio] event=start");
                    else if (kind == "media")
                        Console.WriteLine("[Twilio] event=media (audio chunk)");
                    else if (kind == "stop")
                        Console.WriteLine("[Twilio] event=stop");
                    else
                        Console.WriteLine($"[Twilio] event={kind}");
                }
            }
        }
        finally
        {
            if(ws.State != WebSocketState.Closed)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }

            Console.WriteLine("WebSocket connection closed");
        }
    }
}
