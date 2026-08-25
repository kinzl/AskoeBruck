namespace TennisBruck.Dto;

public class SwapRequestData
{
    public int Player1Id { get; set; }
    public int Player2Id { get; set; }
    public int? Court1Id { get; set; } // Nullable, weil Ersatzspieler keinen Court haben
    public int? Court2Id { get; set; } // Nullable
}