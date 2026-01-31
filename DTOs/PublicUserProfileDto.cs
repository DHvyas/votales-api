namespace VoTales.API.DTOs;

public class PublicUserProfileDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string AvatarStyle { get; set; } = "initials";
    public int TaleCount { get; set; }
    public int VoteCount { get; set; }
    public DateTime JoinedDate { get; set; }
}
