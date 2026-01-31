using System.ComponentModel.DataAnnotations;

namespace VoTales.API.DTOs;

public class UpdateUserProfileRequest
{
    [MaxLength(100)]
    public string? DisplayName { get; set; }

    [MaxLength(300)]
    public string? Bio { get; set; }

    [MaxLength(50)]
    public string? AvatarStyle { get; set; }
}
