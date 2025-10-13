using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Options;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Twilio.TwiML.Messaging;
using static Demo1.Program;
using System.Runtime.InteropServices;




namespace Demo1.Services;

public static class MediaStreamHandler
{
    // Records for deserializing Twilio Media Stream JSON messages
    private record BaseMsg(string @event);
    private record StartMsg(string @event, StartPlayload start );
    private record StartPlayload (string streamSid, string accauntSid, string callSid);
    private record MediaMsg(string @event, MediaPayload media);
    // Media payload contains base64 encoded audio chunk
    private record MediaPayload(string payload);
    private record StopMsg(string @event);


    /// <summary>
    /// Handle WebSocket connection from Twilio Media Stream and process audio via Azure Speech SDK
    /// </summary>
    public static async Task HandleAsync(HttpContext ctx)
    {
        // Check if the request is a WebSocket request
        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsync("WebSocket request expected");
            return;
        }

        #region 1) Accept and open the WebSocket connection

        using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        Console.WriteLine("WebSocket connection established");

        #endregion

        #region 2) Azure Speech configuration
 
        // DI to get Azure Speech options(Bc we are in static class and can't do DI via a construction)
        var opts = ctx.RequestServices
                   .GetService<IOptions<AzureSpeachOptions>>()
                   .Value;

        Console.WriteLine($"[CFG] Region='{opts.Region}' KeyLen={opts.Key?.Length ?? 0}");
        if (string.IsNullOrWhiteSpace(opts.Region) || string.IsNullOrWhiteSpace(opts.Key))
            throw new InvalidOperationException("AzureSpeechOptions are empty (Region/Key). Check user-secrets or appsettings.");

        var speechCfg = SpeechConfig.FromSubscription(opts.Key, opts.Region);
        speechCfg.SpeechRecognitionLanguage = "en-US";

        #endregion

        #region 3) Create a Audio stream with necessary format and a recognizer

        // Audio format: 8kHz, 16bit, mono PCM
        var fmt = AudioStreamFormat.GetWaveFormatPCM(8000, 16, 1);
        // Create a push stream to send audio to
        var push = AudioInputStream.CreatePushStream(fmt);
        // Tell Azure Speech to get audio from our stream
        using var audioCfg = AudioConfig.FromStreamInput(push);
        // The recognizer listens the audio stream and recognizes speech via Azure Speech
        using var recognizer = new SpeechRecognizer(speechCfg, audioCfg);

        #endregion

        #region 4) Subscribtions to events from recognizer: recognized, canceled, session started/stopped

        // Gets recognized text chunks from the caller 
        recognizer.Recognizing += (_, e) =>
        {
            Console.WriteLine($"[Azure partial] Recognizing: {e.Result.Text}");
        };

        // Gets final results(phraze) from recognizer (when user stops talking)
        recognizer.Recognized += (_, e) =>
        {
            if (e.Result.Reason == ResultReason.RecognizedSpeech)
                Console.WriteLine($"[Azure final] Recognized: {e.Result.Text}");
            else if (e.Result.Reason == ResultReason.NoMatch)
                Console.WriteLine($"[Azure] NOMATCH: Speech could not be recognized.");
        };

        // Gets error messages if something goes wrong and call got canceled
        recognizer.Canceled += (_, e) =>
        {
            Console.WriteLine($"[Azure canceled]{e.ErrorDetails}");
        };

        // Session stoped - call is over
        recognizer.SessionStopped += (_, e) =>
        {
            Console.WriteLine("Azure stopped");
        };

        await recognizer.StartContinuousRecognitionAsync();

        #endregion

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
                //{ "event":"start"}
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

                    switch(kind)
                    {
                        case "start":
                            var s = JsonSerializer.Deserialize<StartMsg>(json)!;
                            Console.WriteLine($"[Twilio start]Stream started: streamSid = {s.start.streamSid}, " +
                                $"callSid = {s.start.callSid}");

                            break;

                        case "media":
                            var m = JsonSerializer.Deserialize<MediaMsg>(json)!;

                            // Decode base64 audio chunk to ulaw
                            var ulaw = Convert.FromBase64String(m.media.payload);
                            // Convert ulaw to pcm16(Azure requerement)
                            var pcm16 = MuLawDecoder.Decode(ulaw);
                            // Send pcm16 chunk to the push stream
                            push.Write(pcm16);

                            break;

                        case "stop":
                            Console.WriteLine("[Twilio stop]Stream stopped by Twilio");
                            // Stop Azure Speech SDK
                            await recognizer.StopContinuousRecognitionAsync();
                            // Close the push stream - we don't need it anymore
                            push.Close();
                            // Close WebSocket connection
                            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);

                            return;

                        default:
                            Console.WriteLine($"[Twilio] Unknown event: {kind}");
                            break;

                    }
                }
            }
        }
        finally
        {
            // Close the push stream 
            push.Close();

            // Stop Azure Speech SDK
            await recognizer.StopContinuousRecognitionAsync();

            // Close WebSocket connection if not closed yet
            if (ws.State != WebSocketState.Closed)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);

            Console.WriteLine("[WS] disconnected");
        }
    }
}

/// <summary>
/// Decoder from MuLaw to PCM16
/// </summary>
internal static class MuLawDecoder
{
    internal static byte[] Decode (ReadOnlySpan<byte> ulaw)
    {
        var dest = new byte[ulaw.Length * 2];
        var span16 = MemoryMarshal.Cast<byte, short>(dest.AsSpan());
        for (int i = 0; i < ulaw.Length; i++)
        {
            span16[i] = ToPcm16(ulaw[i]);
        }
        return dest;
    }

    private static short ToPcm16(byte b)
    {
        // Standart decoded formula G.711 μ-law
        b = (byte)~b;
        int sign = b & 0x80;
        int exponent = (b & 0x70) >> 4;
        int mantissa = b & 0x0F;
        int sample = ((mantissa << 4) + 0x08) << (exponent + 2);
        sample -= 0x84;
        return (short)(sign != 0 ? -sample : sample);
    }
}
