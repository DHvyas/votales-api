namespace VoTales.API.DTOs;

public class TaleSummaryDto
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string ContentPreview { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int VotesReceived { get; set; }
}
