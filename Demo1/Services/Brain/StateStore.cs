using System;
using System.Collections.Concurrent;
using Demo1.Models;

/// <summary>
/// Application state lifecycle (per-call dialog memory stored in-process).
/// </summary>
namespace Demo1.Services.Brain;

public interface IStateStore
{
    DialogState GetOrCreate(string callSid);
    void Update(string callSid, Func<DialogState, DialogState> updater);
    void Remove(string callSid);
}

/// <summary>
/// Application state lifecycle in memory
/// Short-lived, per-call dialog memory stored in-process
/// </summary>
public sealed class InMemoryStateStore : IStateStore
{

    #region Key validation 

    public static void ValidateCallSid(string callSid)
    {
        if (callSid is null)
            throw new ArgumentNullException(nameof(callSid), "callSid cannot be null"); // null

        if (string.IsNullOrWhiteSpace(callSid))
            throw new ArgumentException("callSid cannot be empty or whitespace.", nameof(callSid)); // whitespace
    }

    #endregion

    // A thread-safe dictionary (in-memory database) that maps each phone call’s unique ID (CallSid)
    // to its current DialogState. Multiple Twilio events for the same call reuse the same state.
    private readonly ConcurrentDictionary<string, DialogState> _map = new();

    /// <summary>
    /// Gets the <see cref="DialogState"/> for this callSid, or creates one if it's a new call.
    /// </summary>
    public DialogState GetOrCreate(string callSid)
    {
        ValidateCallSid(callSid);

        return _map.GetOrAdd(callSid, key => new DialogState { CallSid = key, LastUpdated = DateTimeOffset.UtcNow });
    }

    /// <summary>
    /// Updates the <see cref="DialogState"/> for the given callSid, or creates and initializes one if it doesn't exist.
    /// </summary>
    public void Update(string callSid, Func<DialogState, DialogState> updater)
    {
        ValidateCallSid(callSid);
        ArgumentNullException.ThrowIfNull(updater);

        _map.AddOrUpdate(
            callSid,
            key => updater(new DialogState { CallSid = key, LastUpdated = DateTimeOffset.UtcNow }),
            (_, current) =>
            {
                var updated = updater(current);
                return updated with { LastUpdated = DateTimeOffset.UtcNow };
            } 
        );
    }

    /// <summary>
    /// Removes the <see cref="DialogState"/> for the given callSid (if present).
    /// </summary>
    public void Remove(string callSid)
    {
         ValidateCallSid(callSid);

        _map.TryRemove(callSid, out _);
    }
}
