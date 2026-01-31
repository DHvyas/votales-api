using System.Security.Claims;
using System.Text.Json;

namespace VoTales.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string GetAuthorName(this ClaimsPrincipal user, string defaultName = "Anonymous")
    {
        var userMetadataClaim = user.FindFirst("user_metadata")?.Value;

        if (string.IsNullOrEmpty(userMetadataClaim))
            return defaultName;

        try
        {
            var metadata = JsonSerializer.Deserialize<JsonElement>(userMetadataClaim);
            return metadata.TryGetProperty("full_name", out var fullName)
                ? fullName.GetString() ?? defaultName
                : defaultName;
        }
        catch
        {
            return defaultName;
        }
    }

    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)
            ?? user.FindFirst("sub");

        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        return null;
    }
}
