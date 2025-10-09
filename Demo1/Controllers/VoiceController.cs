using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security;
using Twilio.AspNet.Core;
using Twilio.TwiML;
using Twilio.TwiML.Voice;

namespace Demo1.Controllers;

[Route("api/voice")]
[ApiController]
public class VoiceController : TwilioController
{
    // Handle incoming call
    [HttpPost("incoming")]
    public TwiMLResult Incoming()
    {
        // Compose WebSocket URL for Media Stream
        var scheme = Request.IsHttps ? "wss" : "ws";
        var host = Request.Host.Value;
        var wsUrl = $"{scheme}://{host}/ws/stream";

        // Create a response
        var resp = new VoiceResponse();
        resp.Say("Hello! This is a AI recognitionist created by Pavel Gaevsky.", voice: "alice");

        resp.Pause(length: 2);

        // Ask Twilio to open Media Stream to our WebSocket endpoint
        var connect = new Connect();
        connect.Stream(url: wsUrl, track: "inbound_track");

        resp.Append(connect);

        return TwiML(resp);
    }

    // Processer of a incoming call result: POST /api/voice/process
    [HttpPost("process")]
    public TwiMLResult Process([FromForm] string? SpeechResult)
    {
        // If nothing recognized put "nothing"
        var said = string.IsNullOrWhiteSpace(SpeechResult) ? "nothing" : SpeechResult;

        // If user said some symbols
        var safe = SecurityElement.Escape(said);

        // Create a response 
        var resp = new VoiceResponse();

        // Tell what we got from a user and say goodbuy
        resp.Say($"You said: {safe}. Thanks for calling, Biiitch.");

        // Hang up
        resp.Hangup();

        return TwiML(resp);
    }
}
