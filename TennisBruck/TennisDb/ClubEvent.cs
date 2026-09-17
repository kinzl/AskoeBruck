namespace TennisDb;

public class ClubEvent
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Allgemein"; // "Turnier", "Fest", "Arbeitseinsatz", "Jugend", "Allgemein"
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Location { get; set; } = "Tennisanlage ASKÖ Bruck";
    public bool IsPinned { get; set; }
    public string? ImageUrl { get; set; }
    public string? AttachmentUrl { get; set; }
    public string? AttachmentName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
