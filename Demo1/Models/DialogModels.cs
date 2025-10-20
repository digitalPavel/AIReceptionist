namespace Demo1.Models;

/// <summary>
/// Caller's intention 
/// </summary>
public enum Intent
{
    Unknown,
    Faq,
    Book,
    Reschedule,
    Cancel,
    Handoff
}

/// <summary>
/// What has been asked from the user to fill out the rest of slots
/// and how to interpret the next user answer
/// </summary>
public enum Asked
{
    None,
    Intent,
    Service,
    When,
    Name,
    Phone,
    Master,
    // For group bookings. Clarify if they want at the same time or different times
    SameOrDifferent,
    // Explicit confirmation of the booking details(best practice)
    Confirm
}
// For group bookings
public record PartyMember(string Role, string? Name = null);

/// <summary>
/// Slots to be filled during the dialog
/// </summary>
public record Slots
{
    // Few services can be booked at once
    public List<string> Services { get; init; } = new();
    public string? Service { get; init; }
    public DateTimeOffset? When { get; init; }
    public string? Name { get; init; }
    public string? PhoneE164 { get; init; }
    public string? Master { get; init; }

    #region Booking policy related slots
    // Switch to true, if the user has no preference about the master
    public bool NoPreferenceMaster { get; init; } = false;
    public string[] PreferredMasters { get; init; } = Array.Empty<string>();

    // True when time is the priority
    public bool TimeFirst { get; init; } = false;

    // For rescheduling
    public DateTimeOffset? ExistingWhen { get; init; }
    public string? ExistingMaster { get; init; }

    #endregion
}

/// <summary>
/// "Memory" of the conversation. It stores evreything the bot already knows 
///  and what still needs to be asked
/// </summary>
public record DialogState
{
    public string CallSid { get; init; } = String.Empty; // A unique ID from Twilio for each call
    public string Lang { get; init; } = "en";// Just English for now
    public string TimeZoneId { get; init; } = "America/New_York";// Timezone that is compatible with Outlook calendar
    public Intent Intent { get; init; } = Intent.Unknown;// “I don’t yet know what the user wants"
    public Slots Slots { get; init; } = new();// Because we want a fresh, empty object, not null
    public int RetryCount { get; init; } = 0;// How many times the user has been asked to clarify
        
    public Asked Asked { get; init; } = Asked.None;// What is being asked from the user right now("Nothing is asked yet")
    public bool IsDone { get; init; } = false;// True when the dialog is over

    #region Group booking related
    public List<PartyMember> PartyMembers { get; init; } = new(); // List of people in the group
    public  int PartyIndex { get; init; } = 0; // Index of the current person in the group being processed
    public bool SameTimeGroup { get; init; } = false; // True if the group wants to book at the same time
    public DateTimeOffset? GroupAnchorWhen { get; init; } // The time for the group booking if SameTimeGroup is true

    public HashSet<string> TakenMastersAtAnchor { get; init; } = new(StringComparer.OrdinalIgnoreCase); // Masters already taken at the anchor time(To avoid duplicate 1 master at the same time bc of HashSet ability)

    #endregion

    public bool ReadyToConfirm { get; init; } = false; // True when all slots are filled and we can ask for confirmation
}
