using System;
using System.Collections.Concurrent;
using Demo1.Models;

namespace Demo1.Services.Brain;

/// <summary>
/// Application state lifecycle (per-call dialog memory stored in-process).
/// </summary>
public interface IStateStore
{
    DialogState GetOrCreate(string callSid);
    void Update(string callSid, Func<DialogState, DialogState> updater);
    void Remove(string callSid);
}

/// <summary>
/// Application state lifecycle in memory
/// </summary>
public sealed class InMemoryStateStore : IStateStore
{
    // A thread-safe dictionary (in-memory database) that maps each phone call’s unique ID (CallSid)
    // to its current DialogState. Multiple Twilio events for the same call reuse the same state.
    private readonly ConcurrentDictionary<string, DialogState> _map = new();

    /// <summary>
    /// Gets the <see cref="DialogState"/> for this callSid, or creates one if it's a new call.
    /// </summary>
    public DialogState GetOrCreate(string callSid)
    {
        ArgumentNullException.ThrowIfNull(callSid);

        return _map.GetOrAdd(callSid, key => new DialogState { CallSid = key });
    }

    /// <summary>
    /// Updates the <see cref="DialogState"/> for the given callSid, or creates and initializes one if it doesn't exist.
    /// </summary>
    public void Update(string callSid, Func<DialogState, DialogState> updater)
    {
        ArgumentNullException.ThrowIfNull(callSid);
        ArgumentNullException.ThrowIfNull(updater);

        _map.AddOrUpdate(
            callSid,
            key => updater(new DialogState { CallSid = key }),
            (_, current) => updater(current)
        );
    }

    /// <summary>
    /// Removes the <see cref="DialogState"/> for the given callSid (if present).
    /// </summary>
    public void Remove(string callSid)
    {
        ArgumentNullException.ThrowIfNull(callSid);

        _map.TryRemove(callSid, out _);
    }
}
