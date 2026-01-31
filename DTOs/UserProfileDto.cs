namespace VoTales.API.DTOs;

public class UserProfileDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string AvatarStyle { get; set; } = "initials";
    public int TotalTalesWritten { get; set; }
    public int TotalVotesReceived { get; set; }
    public DateTime JoinedDate { get; set; }
    public List<TaleSummaryDto> MyRoots { get; set; } = [];
    public List<TaleSummaryDto> MyBranches { get; set; } = [];
}
