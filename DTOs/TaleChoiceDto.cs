namespace VoTales.API.DTOs;

public class TaleChoiceDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Votes { get; set; }
    public string PreviewText { get; set; } = string.Empty;
}
