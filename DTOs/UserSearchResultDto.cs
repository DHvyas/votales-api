namespace VoTales.API.DTOs;

public class UserSearchResultDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarStyle { get; set; } = "initials";
}
