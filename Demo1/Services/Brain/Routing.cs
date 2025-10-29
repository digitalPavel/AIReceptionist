using Demo1.Models;

/// <summary>
/// Routes recognized intents to the corresponding dialog actions.
/// Currently serves as a stub for future integrations such as
/// slot filling, TTS responses, and human handoff.
/// </summary>

namespace Demo1.Services.Brain;

public static class IntentRouter
{
    /// <summary>
    /// Routes the given intent to the appropriate handler based on its type.
    /// </summary>
    public static void Route(Intent intent, string? callSid, string text)
    {
        switch (intent)
        {
            case Intent.Book:
                Console.WriteLine("[Route] Book → slot finding stub");
                break;

            case Intent.Cancel:
                Console.WriteLine("[Route] Cancel → locate upcoming appointment and confirm");
                break;

            case Intent.Reschedule:
                Console.WriteLine("[Route] Reschedule → ask for new day/time");
                break;

            case Intent.Handoff:
                Console.WriteLine("[Route] Handoff → escalate to a human agent");
                break;

            case Intent.Faq:
                Console.WriteLine("[Route] FAQ → answer from knowledge base");
                break;

            default:
                Console.WriteLine("[Route] Unknown → clarification prompt");
                break;
        }
    }
}
