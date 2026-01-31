namespace VoTales.API.DTOs;

public class TaleResponseDto
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string AuthorName { get; set; } = "Anonymous";
    public string Content { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Votes { get; set; }
    public bool HasVoted { get; set; }
    public int SeriesVotes { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public List<TaleChoiceDto> Choices { get; set; } = [];
}
