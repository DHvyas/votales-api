namespace VoTales.API.DTOs;

public class StoryMapDto
{
    public List<MapNodeDto> Nodes { get; set; } = [];
    public List<MapEdgeDto> Edges { get; set; } = [];
}

public class MapNodeDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class MapEdgeDto
{
    public Guid SourceId { get; set; }
    public Guid TargetId { get; set; }
    public int Votes { get; set; }
}
