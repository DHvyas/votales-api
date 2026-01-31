namespace VoTales.API.DTOs;

public class CreateTaleRequest
{
    public Guid AuthorId { get; set; }
    public string? Title { get; set; }
    public string AuthorName { get; set; } = "Anonymous";
    public string Content { get; set; } = string.Empty;
    public Guid? ParentTaleId { get; set; }
}
