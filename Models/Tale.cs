using System.ComponentModel.DataAnnotations;

namespace VoTales.API.Models;

public class Tale
{
    [Key]
    public Guid Id { get; set; }

    public Guid AuthorId { get; set; }

    public string? Title { get; set; }

    public string AuthorName { get; set; } = "Anonymous";

    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string Status { get; set; } = "Draft";

    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Sum of all votes across the entire story tree (Root nodes only).
    /// </summary>
    public int SeriesVotes { get; set; } = 0;

    /// <summary>
    /// Time of the latest branch creation or vote in the story tree (Root nodes only).
    /// </summary>
    public DateTime? LastActivityAt { get; set; }
}
