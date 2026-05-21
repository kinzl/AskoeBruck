namespace TennisDb;

public class PlayerNotificationSettings
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    /// <summary>Email when both opponents in a K.O. match are now known.</summary>
    public bool EmailOnOpponentAssigned { get; set; } = true;

    /// <summary>Email when a partner board slot you are in is completely full.</summary>
    public bool EmailOnSlotFull { get; set; } = true;

    /// <summary>Email when a partner board slot you had joined is deleted by the creator.</summary>
    public bool EmailOnSlotCancelled { get; set; } = true;

    /// <summary>Email to the slot creator when a new player joins their partner board slot.</summary>
    public bool EmailOnSlotJoined { get; set; } = true;
}
