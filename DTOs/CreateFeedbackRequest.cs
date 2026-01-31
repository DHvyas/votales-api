using System.ComponentModel.DataAnnotations;

namespace VoTales.API.DTOs;

public class CreateFeedbackRequest : IValidatableObject
{
    public string? Email { get; set; }

    [Required]
    public string Message { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Email) && !new EmailAddressAttribute().IsValid(Email))
        {
            yield return new ValidationResult("Invalid email address.", [nameof(Email)]);
        }
    }
}
