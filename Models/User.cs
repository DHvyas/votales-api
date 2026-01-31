using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VoTales.API.Models;

/// <summary>
/// User profile table. The Id is a FK to Supabase auth.users(id).
/// Profile rows are created automatically via a Supabase trigger on signup.
/// </summary>
public class User
{
    /// <summary>
    /// Primary key - matches the Supabase auth.users(id).
    /// This is NOT auto-generated; it comes from the JWT 'sub' claim.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Bio { get; set; }

    [MaxLength(50)]
    public string AvatarStyle { get; set; } = "initials";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
