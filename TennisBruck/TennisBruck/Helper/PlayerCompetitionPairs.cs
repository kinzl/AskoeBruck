namespace TennisBruck.Dto;

public abstract class PlayerCompetitionPairs
{
    public int Id { get; set; }
    public int? TeamId { get; set; }
    public int? SinglePlayerId { get; set; }
    public int? DoublePlayerId { get; set; }
}
