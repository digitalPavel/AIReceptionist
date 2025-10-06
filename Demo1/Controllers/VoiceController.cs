using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security;
using Twilio.AspNet.Core;
using Twilio.TwiML;
using Twilio.TwiML.Voice;

namespace Demo1.Controllers
{
    [Route("api/voice")]
    [ApiController]
    public class VoiceController : TwilioController
    {
        // Handle incoming call
        [HttpPost("incoming")]
        public TwiMLResult Incoming()
        {
            // Welcome message for the caller
            var welcome =
                "Hi bitch! This is the AI motherfucker. " +
                "Dasha Gaevskaya Please say what you need after the beep.";

            // Root Twilio object
            var resp = new VoiceResponse();

            // STT config
            var gather = new Gather(
                input:  new List<Gather.InputEnum> {Gather.InputEnum.Speech} ,
                action: new Uri("/api/voice/process", UriKind.Relative),
                language: "en-US",
                timeout: 3);

            // Say welcome message
            gather.Say(welcome, voice: "alice");

            // Add the prepared Gather to the <Response> 
            resp.Append(gather);

            // Fallback
            resp.Say("I did not catch that. Fuck you!", voice: "alice");

            // Hang up
            resp.Hangup();

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
            resp.Say($"You said: {safe}. Thanks for calling, Bitch.");

            // Hang up
            resp.Hangup();

            return TwiML(resp);
        }
    }
}
