namespace TennisBruck.Features.Pyramid.Models;

public class PyramidLevel
{
    public int LevelNumber { get; set; }
    public List<PyramidPositionNode> Nodes { get; set; } = [];
}
