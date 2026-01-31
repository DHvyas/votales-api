using System.ComponentModel.DataAnnotations;

namespace VoTales.API.Models;

public class Vote
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid TaleId { get; set; }

    public DateTime VotedAt { get; set; } = DateTime.UtcNow;
}
