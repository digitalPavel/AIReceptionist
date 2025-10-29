using Demo1.Models;
using System.Globalization;


/// <summary>
/// Brain pipeline that processes ASR results and NLU intent classification,
/// then decides the next dialog action (Act, Confirm, Reprompt, Handoff, etc.).
/// </summary>
namespace Demo1.Services.Brain;

/// <summary>
/// Final ASR result from Azure Speech service.
/// </summary>
public sealed record AsrFinal(
    string CallSid,
    string Text,
    double AsrConfidence,
    string Language
);

#region NLU services

/// <summary>
/// Defines a contract for rule-based NLU services that classify input text into intents.
/// </summary>
public interface INluService
{
    IntentRules.IntentResult Classify(string text);
}

public sealed class RulesNluService : INluService
{
    public IntentRules.IntentResult Classify(string text) =>
        IntentRules.ClassifyWithConfidence(text);
}

#endregion

#region LLM services

/// <summary>
/// Stub interface for an LLM-based NLU service (fallback).
/// </summary>
public interface ILlmNluService
{
    Task<IntentRules.IntentResult> ClassifyAsync(string text, CancellationToken ct);
}

#endregion

#region Decision Policies

public enum DecisionKind
{
    Act,      // Understood. Proceed with the action.
    Reprompt, // Didn't hear or understand well enough. Ask to repeat or clarify.
    Confirm,  // Understood but not sure. Ask the user to confirm.
    Handoff,  // Transfer to a human agent.
    Ignore
}

/// <summary>
/// Decision result from the brain pipeline.
/// </summary>
public sealed record Decision(DecisionKind Kind, string? Note = null);

/// <summary>
/// Defines a contract for decision-making policies based on ASR and NLU results.
/// </summary>
public interface IDecisionPolicy
{
    Decision Decide(AsrFinal asr, IntentRules.IntentResult intent);
}

#endregion

public sealed class DefaultDecisionPolicy : IDecisionPolicy
{
    // Thresholds (consider moving to IOptions)
    private const double ASR_CONFIDENCE_THRESHOLD = 0.70;
    private const double HANDOFF_CONFIRM_THRESHOLD = 0.75;

    /// <summary>
    /// Determines if the intent result is tentative.
    /// </summary>
    private static bool IsTentative(IntentRules.IntentResult r) =>
        r.Intent != Intent.Unknown &&
        (r.Reason?.StartsWith("tentative", StringComparison.OrdinalIgnoreCase) ?? false);

    public Decision Decide(AsrFinal asr, IntentRules.IntentResult i)
    {
        // 1) Poor ASR → reprompt
        if (asr.AsrConfidence < ASR_CONFIDENCE_THRESHOLD)
            return new Decision(DecisionKind.Reprompt, "Low ASR confidence");

        // 2) Unknown intent → reprompt
        if (i.Intent == Intent.Unknown)
            return new Decision(DecisionKind.Reprompt, "Unknown intent");

        // 3) Tentative intent → confirm
        if (IsTentative(i))
            return new Decision(DecisionKind.Confirm, $"Tentative {i.Intent}");

        // 4) Combine confidences conservatively
        var overall = Math.Min(asr.AsrConfidence, i.Confidence);

        // 5) Handoff is expensive → confirm if overall is low
        if (i.Intent == Intent.Handoff && overall < HANDOFF_CONFIRM_THRESHOLD)
            return new Decision(DecisionKind.Confirm, "Handoff needs confirmation");

        // 6) Act
        return new Decision(DecisionKind.Act, $"Confident {i.Intent} (overall {overall.ToString("F2", CultureInfo.InvariantCulture)})");
    }
}
